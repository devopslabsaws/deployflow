using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace DeployFlow.Infrastructure.Services;

/// <summary>
/// Implements the full build + deploy pipeline for a single <see cref="BuildRequest"/>:
///   clone → auto-detect stack → generate Dockerfile → docker build → run container → health-check
///
/// Retries up to <see cref="MaxAttempts"/> times, using <see cref="ISmartFixEngine"/> to
/// prepend a targeted fix script on each failure.
/// </summary>
public class BuildService : IBuildService
{
    private const string FixHookMarker = "# __DEPLOYFLOW_FIX_HOOK__";

    private readonly ISshService                    _ssh;
    private readonly IDockerfileGeneratorService    _dockerfileGenerator;
    private readonly ISmartFixEngine                _fixEngine;
    private readonly ILogger<BuildService>          _logger;
    private readonly ProxyRoutingOptions            _proxyRouting;

    private const int       MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    public BuildService(
        ISshService                 ssh,
        IDockerfileGeneratorService dockerfileGenerator,
        ISmartFixEngine             fixEngine,
        ILogger<BuildService>       logger,
        IOptions<ProxyRoutingOptions> proxyRouting)
    {
        _ssh                  = ssh;
        _dockerfileGenerator  = dockerfileGenerator;
        _fixEngine            = fixEngine;
        _logger               = logger;
        _proxyRouting         = proxyRouting.Value;
    }

    /// <inheritdoc />
    public async Task<BuildResult> BuildAndDeployAsync(
        BuildRequest          request,
        Func<string, string?, Task> logCallback,
        CancellationToken     ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var (deployment, project, server, sshPrivateKey, envVars) = request;

        var envDict = envVars.ToDictionary(e => e.Key, e => e.Value ?? "");
        var immutableOnly = deployment.Metadata.TryGetValue("immutableOnly", out var immutableValue)
            && bool.TryParse(immutableValue, out var immutableParsed)
            && immutableParsed;
        var imageTag = immutableOnly && !string.IsNullOrWhiteSpace(deployment.ImageTag)
            ? deployment.ImageTag!
            : $"deployflow/{project.Slug}:latest";

        await logCallback("🚀 Building deploy pipeline script...", null);

        // ── Pre-flight: verify server is reachable before spending time on script ──
        await logCallback($"🔌 Checking connectivity to {server.IpAddress}:{server.SshPort}...", null);
        var isReachable = await _ssh.TestConnectionAsync(
            server.IpAddress, server.SshPort, server.SshUser, sshPrivateKey, ct);

        if (!isReachable)
        {
            var offlineMsg =
                $"Deploy server is offline or unreachable.\n" +
                $"  Host : {server.IpAddress} (SSH port {server.SshPort})\n\n" +
                $"  Possible causes:\n" +
                $"  \u2022 VM is stopped or deallocated in your cloud provider (Azure/AWS/GCP).\n" +
                $"  \u2022 SSH port {server.SshPort} is blocked by a firewall or security group.\n" +
                $"  \u2022 The SSH private key stored in DeployFlow does not match the server.\n\n" +
                $"  Fix: Start the VM in your cloud console, ensure port {server.SshPort} is open,\n" +
                $"       then re-trigger this deployment.";

            await logCallback($"\u274c Server unreachable — deployment cannot start.\n{offlineMsg}", "stderr");
            stopwatch.Stop();
            return new BuildResult(false, null, null,
                $"Server offline: {server.IpAddress}:{server.SshPort} — {offlineMsg}",
                stopwatch.Elapsed);
        }

        await logCallback($"\u2705 Server reachable ({server.IpAddress}:{server.SshPort}) — building pipeline...", null);

        var script = BuildDeployScript(deployment, project, envDict, server.IpAddress);

        await logCallback("▶️  Executing deploy pipeline on server...", null);

        SshCommandResult? result = null;
        string? lastError = null;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await logCallback($"⏳ Retry {attempt}/{MaxAttempts} — waiting {RetryDelay.TotalSeconds}s...", null);
                await Task.Delay(RetryDelay, ct);
                await logCallback($"🔄 Attempt {attempt} of {MaxAttempts}...", null);
            }

        // Stream stdout lines in real-time via the log callback
            // Capture the DEPLOYFLOW_URL line from the stream
            string? capturedUrl = null;
            result = await _ssh.ExecuteCommandStreamingAsync(
                server.IpAddress, server.SshPort, server.SshUser, sshPrivateKey, script,
                async line =>
                {
                    if (line.StartsWith("DEPLOYFLOW_URL=", StringComparison.Ordinal))
                    {
                        capturedUrl = line["DEPLOYFLOW_URL=".Length..].Trim();
                        // Don't log the raw URL sentinel — log it nicely instead
                        return;
                    }
                    await logCallback(line, "stdout");
                },
                ct);

            if (result.Success && result.ExitCode == 0)
            {
                var publicUrl = capturedUrl
                    ?? ExtractDeploymentUrl(result.StdErr)
                    ?? DeploymentPublicUrlResolver.ResolvePublicUrl(project, _proxyRouting, server.IpAddress, project.Port);

                _logger.LogInformation(
                    "BuildService: deployment {Id} succeeded on attempt {Attempt}. URL={Url}",
                    deployment.Id, attempt, publicUrl);

                stopwatch.Stop();
                return new BuildResult(true, imageTag, publicUrl, null, stopwatch.Elapsed);
            }

            var errSnippet = TrimLog(result.StdErr, 800);
            lastError = errSnippet;
            if (!string.IsNullOrWhiteSpace(errSnippet))
                await logCallback($"⚠️ Attempt {attempt} failed (exit {result.ExitCode}):\n{errSnippet}", "stderr");
            else
                await logCallback($"⚠️ Attempt {attempt} failed (exit {result.ExitCode})", "stderr");

            if (attempt < MaxAttempts)
            {
                var fix = _fixEngine.Analyze(result.StdErr, result.StdOut);
                if (fix.ShouldRetry && fix.FixScript is not null)
                {
                    script = InjectFixScript(script, fix.FixScript);
                    await logCallback($"🩹 Auto-fix applied [{fix.RuleName}]: {fix.Diagnosis}", null);
                    _logger.LogInformation("BuildService: auto-fix {Rule} applied for {Id}", fix.RuleName, deployment.Id);
                }
            }
        }

        stopwatch.Stop();
        var finalError = $"Deploy failed after {MaxAttempts} attempts.\n\n{lastError}";
        _logger.LogWarning("BuildService: deployment {Id} failed after {Attempts} attempts", deployment.Id, MaxAttempts);
        return new BuildResult(false, null, null, finalError, stopwatch.Elapsed);
    }

    // ── Script builder ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a self-contained bash deploy script to run on the target server.
    /// Pipeline: env-vars → ensure-docker → git-clone/update → Dockerfile → docker-build → run → health-check.
    /// </summary>
    private string BuildDeployScript(
        Deployment                 deployment,
        Project                    project,
        Dictionary<string, string> envVars,
        string                     serverIp)
    {
        var sb     = new StringBuilder();
        var slug   = project.Slug;
        var branch = project.RepositoryBranch ?? "main";
        var immutableOnly = deployment.Metadata.TryGetValue("immutableOnly", out var immutableValue)
            && bool.TryParse(immutableValue, out var immutableParsed)
            && immutableParsed;
        var safeRollout = deployment.Metadata.TryGetValue("safeRollout", out var safeRolloutValue)
            && bool.TryParse(safeRolloutValue, out var safeRolloutParsed)
            && safeRolloutParsed;
        var imageTag = immutableOnly && !string.IsNullOrWhiteSpace(deployment.ImageTag)
            ? deployment.ImageTag!
            : $"deployflow/{slug}:latest";
        var proxyHost = DeploymentPublicUrlResolver.ResolveProxyHost(project, _proxyRouting);
        var proxyUrl = !string.IsNullOrWhiteSpace(proxyHost)
            ? $"{(string.Equals(_proxyRouting.Scheme, "http", StringComparison.OrdinalIgnoreCase) ? "http" : "https")}://{proxyHost}"
            : null;
        var routerName = DeploymentPublicUrlResolver.GetRouterName(project);
        var serviceName = DeploymentPublicUrlResolver.GetServiceName(project);

        // ── Inject git token into clone URL ───────────────────────────────────
        var gitToken = envVars.GetValueOrDefault("GIT_ACCESS_TOKEN")
                    ?? envVars.GetValueOrDefault("GITHUB_TOKEN")
                    ?? envVars.GetValueOrDefault("GITLAB_TOKEN");

        var cloneUrl = project.RepositoryUrl ?? "";
        if (!string.IsNullOrEmpty(cloneUrl) && !string.IsNullOrEmpty(gitToken))
        {
            if (cloneUrl.Contains("github.com"))
                cloneUrl = cloneUrl.Replace("https://", $"https://{gitToken}:x-oauth-basic@");
            else if (cloneUrl.Contains("gitlab.com"))
                cloneUrl = cloneUrl.Replace("https://", $"https://oauth2:{gitToken}@");
            else
                cloneUrl = cloneUrl.Replace("https://", $"https://{gitToken}@");
        }

        // ── Script header ─────────────────────────────────────────────────────
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("set -e");
        // pipefail is bash-specific; only set it when running under bash
        sb.AppendLine("(set -o pipefail 2>/dev/null) && set -o pipefail || true");
        sb.AppendLine();
        sb.AppendLine($"CONTAINER_NAME=\"{slug}\"");
        sb.AppendLine($"IMAGE_TAG=\"{imageTag}\"");
        sb.AppendLine($"SAFE_ROLLOUT=\"{(safeRollout ? "true" : "false")}\"");
        sb.AppendLine("PREVIOUS_CONTAINER=\"\"");
        sb.AppendLine($"APP_DIR=\"$HOME/deployflow/{slug}\"");
        sb.AppendLine(project.Port.HasValue ? $"DEFAULT_INTERNAL_PORT={project.Port.Value}" : "DEFAULT_INTERNAL_PORT=3000");
        sb.AppendLine("HOST_PORT=\"\"");
        sb.AppendLine($"PROXY_HOST=\"{EscapeForBash(proxyHost ?? string.Empty)}\"");
        sb.AppendLine();
        sb.AppendLine("log()  { echo \"[$(date -u +%Y-%m-%dT%H:%M:%SZ)] $*\"; }");
        sb.AppendLine("fail() { log \"❌ FATAL: $*\"; exit 1; }");
        sb.AppendLine("port_in_use() {");
        sb.AppendLine("  local candidate=\"$1\"");
        sb.AppendLine("  if command -v ss >/dev/null 2>&1; then");
        sb.AppendLine("    ss -ltn \"( sport = :$candidate )\" 2>/dev/null | tail -n +2 | grep -q .");
        sb.AppendLine("    return $?");
        sb.AppendLine("  fi");
        sb.AppendLine("  if command -v netstat >/dev/null 2>&1; then");
        sb.AppendLine("    netstat -ltn 2>/dev/null | awk '{print $4}' | grep -Eq \"(^|[:.])${candidate}$\"");
        sb.AppendLine("    return $?");
        sb.AppendLine("  fi");
        sb.AppendLine("  return 1");
        sb.AppendLine("}");
        sb.AppendLine("allocate_host_port() {");
        sb.AppendLine("  local preferred=\"${1:-3000}\"");
        sb.AppendLine("  local range_start=3000");
        sb.AppendLine("  local range_end=3999");
        sb.AppendLine("  if [ \"$preferred\" -ge \"$range_start\" ] && [ \"$preferred\" -le \"$range_end\" ] && ! port_in_use \"$preferred\"; then");
        sb.AppendLine("    echo \"$preferred\"");
        sb.AppendLine("    return 0");
        sb.AppendLine("  fi");
        sb.AppendLine("  local candidate");
        sb.AppendLine("  for candidate in $(seq \"$range_start\" \"$range_end\"); do");
        sb.AppendLine("    if ! port_in_use \"$candidate\"; then");
        sb.AppendLine("      echo \"$candidate\"");
        sb.AppendLine("      return 0");
        sb.AppendLine("    fi");
        sb.AppendLine("  done");
        sb.AppendLine("  return 1");
        sb.AppendLine("}");
        sb.AppendLine(FixHookMarker);
        sb.AppendLine();

        // ── Proactive Docker ensure ───────────────────────────────────────────
        sb.AppendLine("# ── Ensure Docker ───────────────────────────────────────────────────────");
        sb.AppendLine("log \"━━━ Step 1/6: Docker Check ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\"");
        sb.AppendLine("if ! command -v docker >/dev/null 2>&1; then");
        sb.AppendLine("  log \"🐳 Docker not found — installing via get.docker.com (this may take 2-3 min)...\"");
        sb.AppendLine("  curl -fsSL https://get.docker.com | sh");
        sb.AppendLine("  sudo systemctl start docker 2>/dev/null || sudo service docker start 2>/dev/null || true");
        sb.AppendLine("  sudo chmod 666 /var/run/docker.sock 2>/dev/null || true");
        sb.AppendLine("  log \"✅ Docker installed: $(docker --version)\"");
        sb.AppendLine("else");
        sb.AppendLine("  log \"✅ Docker already installed: $(docker --version)\"");
        sb.AppendLine("fi");
        sb.AppendLine("if [ -S /var/run/docker.sock ] && ! docker info >/dev/null 2>&1; then");
        sb.AppendLine("  log \"🔑 Fixing Docker socket permissions...\"");
        sb.AppendLine("  sudo chmod 666 /var/run/docker.sock 2>/dev/null || true");
        sb.AppendLine("fi");
        sb.AppendLine();

        sb.AppendLine("mkdir -p \"$APP_DIR\"");
        sb.AppendLine("cd \"$APP_DIR\"");
        sb.AppendLine();

        // ── Environment variables ─────────────────────────────────────────────
        sb.AppendLine("# ── Environment Variables ───────────────────────────────────────────────");
        foreach (var (key, value) in envVars)
        {
            if (key is "GIT_ACCESS_TOKEN" or "GITHUB_TOKEN" or "GITLAB_TOKEN") continue;
            var safe = value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("$", "\\$")
                .Replace("`", "\\`");
            sb.AppendLine($"export {key}=\"{safe}\"");
        }
        sb.AppendLine("PORT=\"${PORT:-$DEFAULT_INTERNAL_PORT}\"");
        sb.AppendLine("HOST_PORT=\"$(allocate_host_port \"$PORT\")\" || fail \"No free host port available in range 3000-3999\"");
        sb.AppendLine("log \"🧭 Internal app port: $PORT | selected external host port: $HOST_PORT\"");
        sb.AppendLine();

        // ── Git clone / update ────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(cloneUrl))
        {
            sb.AppendLine("# ── Source Code ─────────────────────────────────────────────────────────");
            sb.AppendLine($"log \"━━━ Step 2/6: Fetching Source Code (branch: {branch}) ━━━━━━━━━━━━━\"");
            sb.AppendLine("if [ -d .git ]; then");
            sb.AppendLine("  log \"📂 Updating existing repository...\"");
            sb.AppendLine("  git fetch origin 2>&1 | tail -5");
            sb.AppendLine($"  git checkout {branch} 2>/dev/null || git checkout -b {branch} --track origin/{branch} 2>/dev/null || true");
            sb.AppendLine($"  git reset --hard origin/{branch} 2>&1 | tail -3");
            sb.AppendLine("  git clean -fdx 2>&1 | tail -3");
            sb.AppendLine("else");
            sb.AppendLine("  log \"📥 Cloning repository (shallow clone, 1 commit)...\"");
            sb.AppendLine($"  git clone --depth=1 --branch {branch} \"{cloneUrl}\" . 2>&1 | tail -5");
            sb.AppendLine("fi");
            sb.AppendLine("log \"✅ Source ready — commit: $(git rev-parse --short HEAD 2>/dev/null || echo unknown) | $(git log -1 --format='%s' 2>/dev/null || echo '')\"");
            sb.AppendLine();
        }

        // ── Dockerfile: explicit path OR auto-generate ────────────────────────
        if (!string.IsNullOrEmpty(project.DockerfilePath))
        {
            sb.AppendLine($"log \"━━━ Step 3/6: Build Docker Image ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\"");
            sb.AppendLine($"log \"🐳 Building from explicit Dockerfile: {project.DockerfilePath}\"");
            sb.AppendLine($"DOCKER_BUILDKIT=1 docker build \\");
            sb.AppendLine($"  --cache-from \"$IMAGE_TAG\" \\");
            sb.AppendLine($"  --build-arg BUILDKIT_INLINE_CACHE=1 \\");
            sb.AppendLine($"  -f \"{project.DockerfilePath}\" \\");
            sb.AppendLine("  -t \"$IMAGE_TAG\" \\");
            sb.AppendLine("  . 2>&1 | tail -40 || fail \"Docker build failed\"");
        }
        else
        {
            // Delegate to DockerfileGeneratorService for the if/elif/else block
            sb.AppendLine($"log \"━━━ Step 3/6: Detect Stack & Build Docker Image ━━━━━━━━━━━━━━━━━\"");
            sb.Append(_dockerfileGenerator.GenerateAutoDetectScript(project));
        }
        sb.AppendLine();

        // ── Optional custom install / build commands ──────────────────────────
        if (ShouldRunHostCustomCommand(project, project.InstallCommand))
        {
            sb.AppendLine("# ── Custom Install ──────────────────────────────────────────────────────");
            sb.AppendLine("log \"📦 Running custom install command...\"");
            sb.AppendLine(project.InstallCommand);
            sb.AppendLine("log \"✅ Install command completed\"");
            sb.AppendLine();
        }
        else if (!string.IsNullOrWhiteSpace(project.InstallCommand))
        {
            sb.AppendLine("# ── Custom Install (Skipped) ────────────────────────────────────────────");
            sb.AppendLine("log \"⏭️ Skipping host install command for dockerized .NET stack (runs inside Docker build).\"");
            sb.AppendLine();
        }

        if (ShouldRunHostCustomCommand(project, project.BuildCommand))
        {
            sb.AppendLine("# ── Custom Build ────────────────────────────────────────────────────────");
            sb.AppendLine("log \"🔨 Running custom build command...\"");
            sb.AppendLine(project.BuildCommand);
            sb.AppendLine("log \"✅ Build command completed\"");
            sb.AppendLine();
        }
        else if (!string.IsNullOrWhiteSpace(project.BuildCommand))
        {
            sb.AppendLine("# ── Custom Build (Skipped) ──────────────────────────────────────────────");
            sb.AppendLine("log \"⏭️ Skipping host build command for dockerized .NET stack (runs inside Docker build).\"");
            sb.AppendLine();
        }

        // ── Rolling replace: stop/remove old container ────────────────────────
        sb.AppendLine("# ── Rolling Replace ─────────────────────────────────────────────────────");
        sb.AppendLine("log \"━━━ Step 4/6: Rolling Replace ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\"");
        sb.AppendLine("if [ \"$SAFE_ROLLOUT\" = \"true\" ]; then");
        sb.AppendLine("  log \"🛡️ Safe rollout enabled — preserving previous container for quick rollback.\"");
        sb.AppendLine("  if docker ps -a --format '{{.Names}}' | grep -Fxq \"$CONTAINER_NAME\"; then");
        sb.AppendLine("    PREVIOUS_CONTAINER=\"${CONTAINER_NAME}_prev_$(date +%s)\"");
        sb.AppendLine("    docker stop \"$CONTAINER_NAME\" 2>/dev/null || true");
        sb.AppendLine("    docker rename \"$CONTAINER_NAME\" \"$PREVIOUS_CONTAINER\" 2>/dev/null || docker rm \"$CONTAINER_NAME\" 2>/dev/null || true");
        sb.AppendLine("    log \"📦 Previous container preserved as $PREVIOUS_CONTAINER\"");
        sb.AppendLine("  else");
        sb.AppendLine("    log \"ℹ️ No previous container found.\"");
        sb.AppendLine("  fi");
        sb.AppendLine("else");
        sb.AppendLine("  log \"🛑 Stopping previous deployment (if any)...\"");
        sb.AppendLine("  docker stop \"$CONTAINER_NAME\" 2>/dev/null || true");
        sb.AppendLine("  docker rm   \"$CONTAINER_NAME\" 2>/dev/null || true");
        sb.AppendLine("fi");
        sb.AppendLine();

        // ── Run new container ─────────────────────────────────────────────────
        sb.AppendLine("log \"━━━ Step 5/6: Starting Container ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\"");
        sb.AppendLine("log \"▶️  Starting container '$CONTAINER_NAME' with host port $HOST_PORT -> container port $PORT...\"");
        sb.AppendLine("start_container() {");
        sb.Append("docker run -d");
        sb.Append(" \\\n  --name \"$CONTAINER_NAME\"");
        sb.Append(" \\\n  --restart unless-stopped");
        sb.Append(" \\\n  -p \"$HOST_PORT:$PORT\"");

        if (!string.IsNullOrWhiteSpace(proxyHost))
        {
            if (!string.IsNullOrWhiteSpace(_proxyRouting.DockerNetwork))
                sb.Append($" \\\n+  --network \"{EscapeForBash(_proxyRouting.DockerNetwork)}\"");

            sb.Append(" \\\n+  --label \"traefik.enable=true\"");
            sb.Append($" \\\n+  --label \"traefik.http.routers.{routerName}.rule=Host(`{proxyHost}`)\"");
            sb.Append($" \\\n+  --label \"traefik.http.routers.{routerName}.service={serviceName}\"");
            sb.Append(" \\\n+  --label \"traefik.http.services." + serviceName + ".loadbalancer.server.port=$PORT\"");

            if (!string.IsNullOrWhiteSpace(_proxyRouting.EntryPoints))
                sb.Append($" \\\n+  --label \"traefik.http.routers.{routerName}.entrypoints={EscapeForBash(_proxyRouting.EntryPoints)}\"");

            if (_proxyRouting.TlsEnabled)
            {
                sb.Append($" \\\n+  --label \"traefik.http.routers.{routerName}.tls=true\"");
                if (!string.IsNullOrWhiteSpace(_proxyRouting.CertResolver))
                    sb.Append($" \\\n+  --label \"traefik.http.routers.{routerName}.tls.certresolver={EscapeForBash(_proxyRouting.CertResolver)}\"");
            }

            if (!string.IsNullOrWhiteSpace(_proxyRouting.DockerNetwork))
                sb.Append($" \\\n+  --label \"traefik.docker.network={EscapeForBash(_proxyRouting.DockerNetwork)}\"");
        }

        foreach (var (key, value) in envVars)
        {
            if (key is "GIT_ACCESS_TOKEN" or "GITHUB_TOKEN" or "GITLAB_TOKEN") continue;
            var safe = value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("$", "\\$")
                .Replace("`", "\\`");
            sb.Append($" \\\n  -e \"{key}={safe}\"");
        }

        sb.AppendLine(" \\\n  \"$IMAGE_TAG\"");
        sb.AppendLine("}");
        sb.AppendLine("for _run_try in 1 2 3; do");
        sb.AppendLine("  set +e");
        sb.AppendLine("  _run_output=$(start_container 2>&1)");
        sb.AppendLine("  _run_exit=$?");
        sb.AppendLine("  set -e");
        sb.AppendLine("  if [ \"$_run_exit\" -eq 0 ]; then");
        sb.AppendLine("    log \"✅ Container started on external port $HOST_PORT\"");
        sb.AppendLine("    break");
        sb.AppendLine("  fi");
        sb.AppendLine("  echo \"$_run_output\" >&2");
        sb.AppendLine("  if echo \"$_run_output\" | grep -Eiq \"port is already allocated|address already in use\"; then");
        sb.AppendLine("    log \"⚠️ Host port $HOST_PORT became busy during deploy attempt $_run_try/3 — selecting another port...\"");
        sb.AppendLine("    HOST_PORT=\"$(allocate_host_port \"$((HOST_PORT + 1))\")\" || fail \"No alternate host port available in range 3000-3999\"");
        sb.AppendLine("    continue");
        sb.AppendLine("  fi");
        sb.AppendLine("  fail \"Failed to start container\"");
        sb.AppendLine("done");
        sb.AppendLine("[ \"$_run_exit\" -eq 0 ] || fail \"Failed to start container after 3 attempts\"");
        sb.AppendLine();

        // ── Health check ──────────────────────────────────────────────────────
        sb.AppendLine("# ── Health Check ────────────────────────────────────────────────────────");
        sb.AppendLine("log \"━━━ Step 6/6: Health Check ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\"");
        sb.AppendLine("log \"🏥 Waiting for container to become healthy (up to 90s)...\"");
        sb.AppendLine("for _hc in $(seq 1 18); do");
        sb.AppendLine("  _STATUS=$(docker inspect --format='{{.State.Status}}' \"$CONTAINER_NAME\" 2>/dev/null || echo missing)");
        sb.AppendLine("  if [ \"$_STATUS\" = running ]; then");
        sb.AppendLine("    log \"✅ Container is running — image: $IMAGE_TAG, internal port: $PORT, external port: $HOST_PORT\"");
        sb.AppendLine("    break");
        sb.AppendLine("  elif [ \"$_STATUS\" = exited ] || [ \"$_STATUS\" = dead ]; then");
        sb.AppendLine("    log \"❌ Container crashed immediately. Last 40 log lines:\"");
        sb.AppendLine("    docker logs \"$CONTAINER_NAME\" --tail 40 2>&1 || true");
        sb.AppendLine("    if [ \"$SAFE_ROLLOUT\" = \"true\" ] && [ -n \"$PREVIOUS_CONTAINER\" ]; then");
        sb.AppendLine("      log \"↩️ Restoring previous container: $PREVIOUS_CONTAINER\"");
        sb.AppendLine("      docker rm \"$CONTAINER_NAME\" 2>/dev/null || true");
        sb.AppendLine("      docker rename \"$PREVIOUS_CONTAINER\" \"$CONTAINER_NAME\" 2>/dev/null || true");
        sb.AppendLine("      docker start \"$CONTAINER_NAME\" 2>/dev/null || true");
        sb.AppendLine("    fi");
        sb.AppendLine("    fail \"Container exited immediately after start\"");
        sb.AppendLine("  fi");
        sb.AppendLine("  log \"⏳ Container status: $_STATUS — waiting (attempt $_hc/18)...\"");
        sb.AppendLine("  sleep 5");
        sb.AppendLine("done");
        sb.AppendLine();

        // ── Cleanup dangling images ───────────────────────────────────────────
        sb.AppendLine("docker image prune -f --filter 'dangling=true' 2>/dev/null || true");
        sb.AppendLine();

        // ── Emit URL for the runner to capture ────────────────────────────────
        sb.AppendLine("log \"🌐 Deployment complete!\"");
        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            sb.AppendLine($"DEPLOY_URL=\"{proxyUrl}\"");
            sb.AppendLine($"log \"🔗 App URL: {proxyUrl}\"");
            sb.AppendLine($"echo \"DEPLOYFLOW_URL={proxyUrl}\"");
        }
        else
        {
            sb.AppendLine($"DEPLOY_URL=\"http://{serverIp}:$HOST_PORT\"");
            sb.AppendLine($"log \"🔗 App URL: http://{serverIp}:$HOST_PORT\"");
            sb.AppendLine($"echo \"DEPLOYFLOW_URL=http://{serverIp}:$HOST_PORT\"");
        }

        // Normalize Windows CRLF → LF so bash on Linux does not see \r as part
        // of each command/option (causes "set: invalid option" and "$'\r': command not found").
        return sb.ToString().Replace("\r\n", "\n").Replace("\r", "\n");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string? ExtractDeploymentUrl(string? stdout)
    {
        if (!string.IsNullOrEmpty(stdout))
        {
            var m = Regex.Match(stdout, @"DEPLOYFLOW_URL=(https?://\S+)");
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    private static string EscapeForBash(string value)
        => value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("$", "\\$")
            .Replace("`", "\\`");

    private static string InjectFixScript(string script, string fixScript)
    {
        var normalizedFix = fixScript.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        if (script.Contains(FixHookMarker, StringComparison.Ordinal))
        {
            return script.Replace(
                FixHookMarker,
                $"{FixHookMarker}\n{normalizedFix}\n",
                StringComparison.Ordinal);
        }

        // Fallback for older scripts without marker.
        return normalizedFix + "\n" + script;
    }

    private static bool ShouldRunHostCustomCommand(Project project, string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        if (!string.IsNullOrEmpty(project.DockerfilePath)) return false;

        var framework = project.Framework ?? string.Empty;
        var isDotNetProject = framework.Contains("dotnet", StringComparison.OrdinalIgnoreCase)
            || framework.Contains("asp.net", StringComparison.OrdinalIgnoreCase)
            || command.Contains("dotnet", StringComparison.OrdinalIgnoreCase);

        return !isDotNetProject;
    }

    private static string TrimLog(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "\n… (truncated)";
}
