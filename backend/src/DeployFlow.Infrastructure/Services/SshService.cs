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
        await Task.Run(() => client.Connect(), ct);

        using var cmd = client.CreateCommand(command);
        var result = await Task.Run(() => cmd.Execute(), ct);

        client.Disconnect();

        return new SshCommandResult(
            ExitCode: cmd.ExitStatus ?? 0,
            StdOut: StripAnsi(result),
            StdErr: StripAnsi(cmd.Error),
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
