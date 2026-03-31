using DeployFlow.Application.Common;
using Renci.SshNet;
using System.Text.RegularExpressions;

namespace DeployFlow.Infrastructure.Services;

public class SshService : ISshService
{
    // Strips ANSI/VT100 control sequences so interactive programs (nano, top, etc.)
    // don't produce garbage output in the non-PTY exec channel.
    private static readonly Regex _ansiEscape = new Regex(
        "\x1b[@-Z\\\\-_]|\x1b\\[[0-?]*[ -/]*[@-~]",
        RegexOptions.Compiled);

    private static string StripAnsi(string s) =>
        string.IsNullOrEmpty(s) ? s : _ansiEscape.Replace(s, "");

    public async Task<bool> TestConnectionAsync(
        string host, int port, string user, string privateKey, CancellationToken ct = default)
    {
        try
        {
            using var client = CreateClient(host, port, user, privateKey);
            // 15-second connection timeout for health checks
            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(15);
            await Task.Run(() => client.Connect(), ct);
            var pong = client.IsConnected;
            client.Disconnect();
            return pong;
        }
        catch
        {
            return false;
        }
    }

    public async Task<SshCommandResult> ExecuteCommandAsync(
        string host, int port, string user, string privateKey, string command, CancellationToken ct = default)
    {
        using var client = CreateClient(host, port, user, privateKey);
        // 30-second TCP connection timeout; commands themselves may take much longer
        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);

        // Run the connect with both the SSH.NET timeout and the CancellationToken
        await Task.Run(() => client.Connect(), ct);

        using var cmd = client.CreateCommand(command);
        // Individual command timeout: 90 minutes — covers slow Docker builds/pulls
        cmd.CommandTimeout = TimeSpan.FromMinutes(90);

        // Allow CancellationToken to abort command execution
        using var _ = ct.Register(() =>
        {
            try { cmd.CancelAsync(); } catch { /* ignore */ }
            try { client.Disconnect(); } catch { /* ignore */ }
        });

        var result = await Task.Run(() => cmd.Execute(), ct);

        client.Disconnect();

        return new SshCommandResult(
            ExitCode: cmd.ExitStatus ?? 0,
            StdOut: StripAnsi(result),
            StdErr: StripAnsi(cmd.Error),
            Success: cmd.ExitStatus == 0);
    }

    /// <inheritdoc />
    public async Task<SshCommandResult> ExecuteCommandStreamingAsync(
        string host, int port, string user, string privateKey, string command,
        Func<string, Task> onStdOutLine,
        CancellationToken ct = default)
    {
        using var client = CreateClient(host, port, user, privateKey);
        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
        await Task.Run(() => client.Connect(), ct);

        // If the command looks like a shell script (starts with #!) run it explicitly
        // under bash via a temp file so bash-specific options (pipefail) always work,
        // regardless of the server's default login shell (dash, sh, etc.).
        bool isScript = command.TrimStart().StartsWith("#!");
        string sshCommand = command;
        string? tempScriptPath = null;
        if (isScript)
        {
            tempScriptPath = $"/tmp/deployflow_{Guid.NewGuid():N}.sh";
            // Normalize to LF before encoding — Windows builds may embed \r\n
            var scriptLf = command.Replace("\r\n", "\n").Replace("\r", "\n");
            var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(scriptLf));
            // base64 -d writes the script, sed strips any residual \r, then bash runs it
            sshCommand = $"echo {encoded} | base64 -d | sed 's/\\r//' > {tempScriptPath} && chmod +x {tempScriptPath} && bash {tempScriptPath}; _ec=$?; rm -f {tempScriptPath}; exit $_ec";
        }

        using var cmd = client.CreateCommand(sshCommand);
        cmd.CommandTimeout = TimeSpan.FromMinutes(90);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var _reg = ct.Register(() =>
        {
            try { cmd.CancelAsync(); } catch { /* ignore */ }
            try { client.Disconnect(); } catch { /* ignore */ }
        });

        var asyncResult = cmd.BeginExecute();

        try
        {
            // Stream stdout line-by-line while command runs
            using var reader = new System.IO.StreamReader(cmd.OutputStream);
            var remainder = new System.Text.StringBuilder();

            // Poll the output stream while the command hasn't finished
            while (!asyncResult.IsCompleted || cmd.OutputStream.Length > 0)
            {
                ct.ThrowIfCancellationRequested();

                int ch;
                while ((ch = reader.Read()) >= 0)
                {
                    if (ch == '\n')
                    {
                        var line = StripAnsi(remainder.ToString().TrimEnd('\r'));
                        remainder.Clear();
                        if (!string.IsNullOrWhiteSpace(line))
                            await onStdOutLine(line);
                    }
                    else
                    {
                        remainder.Append((char)ch);
                    }
                }

                if (!asyncResult.IsCompleted)
                    await Task.Delay(80, ct);
            }

            // Flush any remaining partial line
            if (remainder.Length > 0)
            {
                var trailing = StripAnsi(remainder.ToString().TrimEnd('\r'));
                if (!string.IsNullOrWhiteSpace(trailing))
                    await onStdOutLine(trailing);
            }
        }
        finally
        {
            try { cmd.EndExecute(asyncResult); } catch { /* ignore */ }
        }

        var stderr = StripAnsi(cmd.Error ?? "");
        client.Disconnect();

        return new SshCommandResult(
            ExitCode: cmd.ExitStatus ?? 0,
            StdOut: "",   // streamed — no buffered stdout
            StdErr: stderr,
            Success: cmd.ExitStatus == 0);
    }

    public async Task<bool> TransferFileAsync(
        string host, int port, string user, string privateKey,
        string localPath, string remotePath, CancellationToken ct = default)
    {
        try
        {
            using var sftp = new SftpClient(host, port, user, new PrivateKeyFile(
                new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(privateKey))));
            await Task.Run(() => sftp.Connect(), ct);

            using var fileStream = System.IO.File.OpenRead(localPath);
            await Task.Run(() => sftp.UploadFile(fileStream, remotePath, true), ct);
            sftp.Disconnect();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SshClient CreateClient(string host, int port, string user, string privateKey)
    {
        var keyStream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(privateKey));
        var keyFile = new PrivateKeyFile(keyStream);
        return new SshClient(host, port, user, keyFile);
    }
}
