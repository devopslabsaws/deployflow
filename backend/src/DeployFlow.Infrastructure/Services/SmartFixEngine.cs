using DeployFlow.Application.Common;
using System.Text.RegularExpressions;

namespace DeployFlow.Infrastructure.Services;

/// <summary>
/// Rule-based smart fix engine.
/// Analyses deploy stderr/stdout and recommends the first matching remediation.
///
/// Rules are evaluated in priority order; the first rule whose
/// <see cref="IFixRule.CanFix"/> returns true wins.
/// </summary>
public class SmartFixEngine : ISmartFixEngine
{
    private readonly IReadOnlyList<IFixRule> _rules;

    public SmartFixEngine()
    {
        _rules = new List<IFixRule>
        {
            // Hardware / environment constraints — highest priority
            new DiskSpaceRule(),
            new MemoryOomRule(),

            // Docker daemon / socket
            new DockerInstallRule(),
            new DockerDaemonRule(),
            new DockerSocketPermissionRule(),

            // Container name/port conflicts
            new PortConflictRule(),
            new ContainerNameConflictRule(),

            // Runtime tool installers
            new NodeInstallRule(),
            new PythonInstallRule(),
            new PipInstallRule(),
            new GoInstallRule(),
            new GitInstallRule(),
            new DotNetSdkMissingRule(),
            new JavaMissingRule(),

            // Build-time package issues
            new NpmCacheCorruptionRule(),
            new DockerfileNotFoundRule(),
        };
    }

    /// <inheritdoc />
    public SmartFixResult Analyze(string stdErr, string stdOut)
    {
        var combined = stdErr + "\n" + stdOut;
        foreach (var rule in _rules)
        {
            if (rule.CanFix(stdErr, stdOut))
                return new SmartFixResult(
                    ShouldRetry: true,
                    FixScript:   rule.GenerateFixScript(stdErr, stdOut),
                    RuleName:    rule.Name,
                    Diagnosis:   rule.Diagnosis);
        }
        return SmartFixResult.NoFix;
    }
}

// ── Fix rules ──────────────────────────────────────────────────────────────────

/// <summary>No disk space remaining on device.</summary>
internal sealed class DiskSpaceRule : IFixRule
{
    public string Name      => "DiskSpace";
    public string Diagnosis => "Disk space exhausted — cleaning Docker build cache and unused images";

    public bool CanFix(string stdErr, string stdOut) =>
        stdErr.Contains("no space left on device", StringComparison.OrdinalIgnoreCase) ||
        stdOut.Contains("no space left on device", StringComparison.OrdinalIgnoreCase);

    public string GenerateFixScript(string stdErr, string stdOut) =>
        """
        log "🧹 Cleaning Docker build cache + dangling images to free disk space..."
        docker builder prune -af 2>/dev/null || true
        docker image prune -af  2>/dev/null || true
        docker container prune -f 2>/dev/null || true
        df -h /
        """;
}

/// <summary>Process killed by OOM killer.</summary>
internal sealed class MemoryOomRule : IFixRule
{
    public string Name      => "MemoryOOM";
    public string Diagnosis => "Process was OOM-killed — reducing build parallelism";

    public bool CanFix(string stdErr, string stdOut)
    {
        var combined = stdErr + stdOut;
        return Regex.IsMatch(combined, @"Killed|kill signal 9|exit code: 137", RegexOptions.IgnoreCase) ||
               combined.Contains("OOMKilled", StringComparison.OrdinalIgnoreCase);
    }

    public string GenerateFixScript(string stdErr, string stdOut) =>
        """
        log "💾 OOM detected — limiting build parallelism on next attempt..."
        export MAKEFLAGS="-j1"
        export NODE_OPTIONS="--max-old-space-size=512"
        """;
}

/// <summary>Docker CLI is not installed.</summary>
internal sealed class DockerInstallRule : IFixRule
{
    public string Name      => "DockerInstall";
    public string Diagnosis => "Docker CLI not found — installing Docker Engine";

    public bool CanFix(string stdErr, string stdOut) =>
        stdErr.Contains("docker: command not found") ||
        stdErr.Contains("docker: not found");

    public string GenerateFixScript(string stdErr, string stdOut) =>
        """
        log "🐳 Installing Docker Engine..."
        curl -fsSL https://get.docker.com | sh
        sudo systemctl start docker 2>/dev/null || sudo service docker start 2>/dev/null || true
        sudo chmod 666 /var/run/docker.sock 2>/dev/null || true
        log "✅ Docker installed"
        """;
}

/// <summary>Docker daemon not running or socket not accessible.</summary>
internal sealed class DockerDaemonRule : IFixRule
{
    public string Name      => "DockerDaemon";
    public string Diagnosis => "Docker daemon not running — starting Docker service";

    public bool CanFix(string stdErr, string stdOut) =>
        stdErr.Contains("Cannot connect to the Docker daemon") ||
        stdErr.Contains("Is the docker daemon running?");

    public string GenerateFixScript(string stdErr, string stdOut) =>
        """
        log "🐳 Starting Docker daemon..."
        sudo systemctl start docker 2>/dev/null || sudo service docker start 2>/dev/null || true
        for i in 1 2 3 4 5; do docker info >/dev/null 2>&1 && break || sleep 2; done
        sudo chmod 666 /var/run/docker.sock 2>/dev/null || true
        """;
}

/// <summary>Docker socket permission denied.</summary>
internal sealed class DockerSocketPermissionRule : IFixRule
{
    public string Name      => "DockerSocketPermission";
    public string Diagnosis => "Docker socket permission denied — fixing socket permissions";

    public bool CanFix(string stdErr, string stdOut) =>
        stdErr.Contains("permission denied") && stdErr.Contains("docker.sock");

    public string GenerateFixScript(string stdErr, string stdOut) =>
        "sudo chmod 666 /var/run/docker.sock 2>/dev/null || true\n";
}

/// <summary>Host port already allocated.</summary>
internal sealed class PortConflictRule : IFixRule
{
    public string Name      => "PortConflict";
    public string Diagnosis => "Port already in use — releasing port binding";

    public bool CanFix(string stdErr, string stdOut) =>
        Regex.IsMatch(stdErr, @"port is already allocated|address already in use", RegexOptions.IgnoreCase);

    public string GenerateFixScript(string stdErr, string stdOut)
    {
        var sb = new System.Text.StringBuilder();
        var portMatch = Regex.Match(stdErr + stdOut, @":(\d{2,5}).*already in use|already allocated.*:(\d{2,5})", RegexOptions.IgnoreCase);
        if (portMatch.Success)
        {
            var port = portMatch.Groups[1].Value.IfEmpty(portMatch.Groups[2].Value);
            if (!string.IsNullOrEmpty(port))
                sb.AppendLine($"fuser -k {port}/tcp 2>/dev/null || true");
        }
        sb.AppendLine("sleep 2");
        return sb.ToString();
    }
}

/// <summary>Container name already in use.</summary>
internal sealed class ContainerNameConflictRule : IFixRule
{
    public string Name      => "ContainerNameConflict";
    public string Diagnosis => "Container name already in use — removing existing container";

    public bool CanFix(string stdErr, string stdOut) =>
        Regex.IsMatch(stdErr, @"name.*is already in use|the name.*already in use", RegexOptions.IgnoreCase);

    public string GenerateFixScript(string stdErr, string stdOut)
    {
        var m = Regex.Match(stdErr, @"The container name ""?(/\S+?)""? is already in use by container", RegexOptions.IgnoreCase);
        if (!m.Success) m = Regex.Match(stdErr, @"name ""?(/\w+?)""? already", RegexOptions.IgnoreCase);
        var name = m.Success ? m.Groups[1].Value.TrimStart('/') : "$CONTAINER_NAME";
        return $"""
        log "🛑 Removing conflicting container '{name}'..."
        docker stop "{name}" 2>/dev/null || true
        docker rm   "{name}" 2>/dev/null || true
        """;
    }
}

/// <summary>Node.js / npm not installed.</summary>
internal sealed class NodeInstallRule : IFixRule
{
    public string Name      => "NodeInstall";
    public string Diagnosis => "Node.js not found — installing Node.js 20 LTS";

    public bool CanFix(string stdErr, string stdOut) =>
        stdErr.Contains("npm: command not found") ||
        stdErr.Contains("node: command not found") ||
        stdErr.Contains("npm: not found") ||
        stdErr.Contains("node: not found");

    public string GenerateFixScript(string stdErr, string stdOut) =>
        """
        log "📦 Installing Node.js 20 LTS..."
        curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
        sudo apt-get install -y nodejs
        node -v && npm -v
        """;
}

/// <summary>Python 3 not installed.</summary>
internal sealed class PythonInstallRule : IFixRule
{
    public string Name      => "PythonInstall";
    public string Diagnosis => "Python 3 not found — installing Python 3";

    public bool CanFix(string stdErr, string stdOut) =>
        (stdErr.Contains("python3: command not found") || stdErr.Contains("python3: not found")) &&
        !stdErr.Contains("pip");  // PipInstallRule handles pip-only scenarios

    public string GenerateFixScript(string stdErr, string stdOut) =>
        """
        log "📦 Installing Python 3..."
        sudo apt-get update -qq && sudo apt-get install -y python3 python3-pip python3-venv
        python3 --version
        """;
}

/// <summary>pip not installed.</summary>
internal sealed class PipInstallRule : IFixRule
{
    public string Name      => "PipInstall";
    public string Diagnosis => "pip not found — installing pip";

    public bool CanFix(string stdErr, string stdOut) =>
        stdErr.Contains("pip: command not found") ||
        stdErr.Contains("pip3: command not found") ||
        stdErr.Contains("pip: not found") ||
        stdErr.Contains("pip3: not found");

    public string GenerateFixScript(string stdErr, string stdOut) =>
        "sudo apt-get update -qq && sudo apt-get install -y python3-pip\n";
}

/// <summary>Go toolchain not installed.</summary>
internal sealed class GoInstallRule : IFixRule
{
    public string Name      => "GoInstall";
    public string Diagnosis => "Go not found — installing Go 1.22";

    public bool CanFix(string stdErr, string stdOut) =>
        stdErr.Contains("go: command not found") || stdErr.Contains("go: not found");

    public string GenerateFixScript(string stdErr, string stdOut) =>
        """
        log "📦 Installing Go 1.22..."
        curl -fsSL https://go.dev/dl/go1.22.5.linux-amd64.tar.gz | sudo tar -C /usr/local -xz
        export PATH=$PATH:/usr/local/go/bin
        echo 'export PATH=$PATH:/usr/local/go/bin' >> ~/.profile
        go version
        """;
}

/// <summary>git not installed.</summary>
internal sealed class GitInstallRule : IFixRule
{
    public string Name      => "GitInstall";
    public string Diagnosis => "git not found — installing git";

    public bool CanFix(string stdErr, string stdOut) =>
        stdErr.Contains("git: command not found") || stdErr.Contains("git: not found");

    public string GenerateFixScript(string stdErr, string stdOut) =>
        "sudo apt-get update -qq && sudo apt-get install -y git\n";
}

/// <summary>.NET SDK not installed in the Docker build environment.</summary>
internal sealed class DotNetSdkMissingRule : IFixRule
{
    public string Name      => "DotNetSdkMissing";
    public string Diagnosis => ".NET SDK not available in build image — verify base image";

    public bool CanFix(string stdErr, string stdOut) =>
        stdErr.Contains("dotnet: command not found") ||
        (stdErr.Contains("dotnet") && stdErr.Contains("not found"));

    public string GenerateFixScript(string stdErr, string stdOut) =>
        """
                echo "[fix] Installing .NET 8 SDK (host fallback)..."
                if command -v dotnet >/dev/null 2>&1; then
                    echo "[fix] dotnet already available: $(dotnet --version)"
                else
                    if command -v apt-get >/dev/null 2>&1; then
                        sudo apt-get update -qq || true
                        sudo apt-get install -y dotnet-sdk-8.0 || true
                    fi

                    if ! command -v dotnet >/dev/null 2>&1; then
                        curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh || true
                        bash /tmp/dotnet-install.sh --version 8.0.204 --install-dir "$HOME/.dotnet" || true
                        export PATH="$PATH:$HOME/.dotnet"
                    fi

                    if command -v dotnet >/dev/null 2>&1; then
                        echo "[fix] dotnet installed: $(dotnet --version)"
                    else
                        echo "[fix] dotnet is still unavailable after fallback attempts"
                    fi
                fi
        """;
}

/// <summary>Java / JDK not installed on host.</summary>
internal sealed class JavaMissingRule : IFixRule
{
    public string Name      => "JavaMissing";
    public string Diagnosis => "Java not found — installing Java 21 JDK";

    public bool CanFix(string stdErr, string stdOut) =>
        stdErr.Contains("java: not found") ||
        stdErr.Contains("java: command not found") ||
        stdErr.Contains("JAVA_HOME is not set");

    public string GenerateFixScript(string stdErr, string stdOut) =>
        """
        log "📦 Installing Java 21 (temurin)..."
        sudo apt-get update -qq
        sudo apt-get install -y wget apt-transport-https
        wget -qO - https://packages.adoptium.net/artifactory/api/gpg/key/public | sudo apt-key add -
        echo "deb https://packages.adoptium.net/artifactory/deb $(. /etc/os-release && echo $VERSION_CODENAME) main" | sudo tee /etc/apt/sources.list.d/adoptium.list
        sudo apt-get update -qq && sudo apt-get install -y temurin-21-jdk
        java -version
        export JAVA_HOME=$(java -XshowSettings:properties -version 2>&1 | grep 'java.home' | awk '{print $3}')
        """;
}

/// <summary>npm cache is corrupted.</summary>
internal sealed class NpmCacheCorruptionRule : IFixRule
{
    public string Name      => "NpmCacheCorruption";
    public string Diagnosis => "npm cache corrupted — clearing cache before retry";

    public bool CanFix(string stdErr, string stdOut) =>
        (stdErr.Contains("npm ERR!") || stdErr.Contains("npm WARN")) &&
        (stdErr.Contains("EINTEGRITY") || stdErr.Contains("invalid package") || stdErr.Contains("ERESOLVE"));

    public string GenerateFixScript(string stdErr, string stdOut) =>
        """
        log "🧹 Clearing npm cache..."
        npm cache clean --force 2>/dev/null || true
        rm -rf node_modules package-lock.json 2>/dev/null || true
        """;
}

/// <summary>Dockerfile path not found (relative path issue).</summary>
internal sealed class DockerfileNotFoundRule : IFixRule
{
    public string Name      => "DockerfileNotFound";
    public string Diagnosis => "Custom Dockerfile path not found — verify DockerfilePath in project settings";

    public bool CanFix(string stdErr, string stdOut) =>
        Regex.IsMatch(stdErr, @"unable to prepare context.*dockerfile.*not found|no such file.*Dockerfile", RegexOptions.IgnoreCase);

    public string GenerateFixScript(string stdErr, string stdOut) =>
        """
        log "🔍 Dockerfile not found at configured path — falling back to auto-generation..."
        # Remove the -f flag to use auto-generated Dockerfile
        """;
}

// ── String extensions ──────────────────────────────────────────────────────────

internal static class StringExtensions
{
    internal static string IfEmpty(this string s, string fallback) =>
        string.IsNullOrEmpty(s) ? fallback : s;
}
