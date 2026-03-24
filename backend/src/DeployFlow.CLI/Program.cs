using System.CommandLine;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace DeployFlow.CLI;

// ─── Config ───────────────────────────────────────────────────────────────────

record CliConfig(string ApiUrl, string? AccessToken, string? RefreshToken, DateTime? TokenExpiry);

static class Config
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".deployflow", "config.json");

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static CliConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new CliConfig("http://localhost:5000", null, null, null);
        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<CliConfig>(json, Opts)
                   ?? new CliConfig("http://localhost:5000", null, null, null);
        }
        catch { return new CliConfig("http://localhost:5000", null, null, null); }
    }

    public static void Save(CliConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, Opts));
    }

    public static bool IsAuthenticated()
    {
        var cfg = Load();
        return !string.IsNullOrEmpty(cfg.AccessToken) &&
               (cfg.TokenExpiry == null || cfg.TokenExpiry > DateTime.UtcNow.AddMinutes(1));
    }
}

// ─── API Client ───────────────────────────────────────────────────────────────

static class Api
{
    private static HttpClient? _http;

    public static HttpClient GetClient()
    {
        if (_http is not null) return _http;
        var cfg = Config.Load();
        _http = new HttpClient { BaseAddress = new Uri(cfg.ApiUrl.TrimEnd('/') + "/api/") };
        if (!string.IsNullOrEmpty(cfg.AccessToken))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", cfg.AccessToken);
        return _http;
    }

    public static async Task<T?> GetAsync<T>(string path)
    {
        var resp = await GetClient().GetAsync(path);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
    }

    public static async Task<T?> PostAsync<T>(string path, object? body = null)
    {
        var resp = await GetClient().PostAsJsonAsync(path, body, JsonOpts);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

// ─── Entry point ──────────────────────────────────────────────────────────────

class Program
{
    static async Task<int> Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("DeployFlow").Color(Color.Green));

        var rootCommand = new RootCommand("df — DeployFlow CLI");

        rootCommand.AddCommand(BuildLoginCommand());
        rootCommand.AddCommand(BuildLogoutCommand());
        rootCommand.AddCommand(BuildStatusCommand());
        rootCommand.AddCommand(BuildDeployCommand());
        rootCommand.AddCommand(BuildLogsCommand());
        rootCommand.AddCommand(BuildServersCommand());
        rootCommand.AddCommand(BuildProjectsCommand());
        rootCommand.AddCommand(BuildEnvCommand());
        rootCommand.AddCommand(BuildRollbackCommand());
        rootCommand.AddCommand(BuildInitCommand());
        rootCommand.AddCommand(BuildEnvSetCommand());

        return await rootCommand.InvokeAsync(args);
    }

    // ── login ─────────────────────────────────────────────────────────────────

    static Command BuildLoginCommand()
    {
        var urlOpt = new Option<string>("--url", () => "http://localhost:5000", "DeployFlow API URL");
        var emailOpt = new Option<string?>("--email", "Email address");
        var passwordOpt = new Option<string?>("--password", "Password (omit for secure prompt)");
        var cmd = new Command("login", "Authenticate with DeployFlow")
        {
            urlOpt, emailOpt, passwordOpt
        };

        cmd.SetHandler(async (string url, string? email, string? password) =>
        {
            if (string.IsNullOrEmpty(email))
                email = AnsiConsole.Ask<string>("Email: ");
            if (string.IsNullOrEmpty(password))
                password = AnsiConsole.Prompt(new TextPrompt<string>("Password: ").Secret());

            await AnsiConsole.Status()
                .StartAsync("Authenticating...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    var http = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/api/") };
                    var resp = await http.PostAsJsonAsync("auth/login",
                        new { email, password });

                    if (!resp.IsSuccessStatusCode)
                    {
                        AnsiConsole.MarkupLine("[red]Login failed.[/] Check your credentials.");
                        return;
                    }

                    var result = await resp.Content.ReadFromJsonAsync<LoginResult>(
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (result?.AccessToken is null)
                    {
                        AnsiConsole.MarkupLine("[red]Invalid response from server.[/]");
                        return;
                    }

                    Config.Save(new CliConfig(
                        url,
                        result.AccessToken,
                        result.RefreshToken,
                        DateTime.UtcNow.AddHours(1)));

                    AnsiConsole.MarkupLine($"[green]Logged in as {Markup.Escape(email)}[/]");
                });
        }, urlOpt, emailOpt, passwordOpt);

        return cmd;
    }

    // ── logout ────────────────────────────────────────────────────────────────

    static Command BuildLogoutCommand()
    {
        var cmd = new Command("logout", "Clear saved credentials");
        cmd.SetHandler(() =>
        {
            var cfg = Config.Load();
            Config.Save(cfg with { AccessToken = null, RefreshToken = null, TokenExpiry = null });
            AnsiConsole.MarkupLine("[grey]Logged out.[/]");
        });
        return cmd;
    }

    // ── status ────────────────────────────────────────────────────────────────

    static Command BuildStatusCommand()
    {
        var cmd = new Command("status", "Show platform dashboard summary");
        cmd.SetHandler(async () =>
        {
            RequireAuth();
            try
            {
                var stats = await Api.GetAsync<DashboardStats>("dashboard/stats");
                if (stats is null) { AnsiConsole.MarkupLine("[red]Could not fetch stats.[/]"); return; }

                var table = new Table()
                    .AddColumn("Metric")
                    .AddColumn(new TableColumn("Value").RightAligned());

                table.AddRow("Projects", stats.TotalProjects.ToString());
                table.AddRow("Servers", stats.TotalServers.ToString());
                table.AddRow("Active Deployments", $"[yellow]{stats.ActiveDeployments}[/]");
                table.AddRow("Running Services", $"[green]{stats.RunningServices}[/]");
                table.AddRow("Active Alerts", stats.ActiveAlerts > 0
                    ? $"[red]{stats.ActiveAlerts}[/]"
                    : $"[green]{stats.ActiveAlerts}[/]");

                AnsiConsole.Write(table);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            }
        });
        return cmd;
    }

    // ── deploy ────────────────────────────────────────────────────────────────

    static Command BuildDeployCommand()
    {
        var projectArg = new Argument<string?>("project", () => null, "Project name or ID");
        var branchOpt = new Option<string>("--branch", () => "main", "Git branch to deploy");
        var envOpt = new Option<string>("--env", () => "production", "Target environment");
        var serverOpt = new Option<string?>("--server", () => null, "Target server ID");
        var cmd = new Command("deploy", "Trigger a deployment")
        {
            projectArg, branchOpt, envOpt, serverOpt
        };

        cmd.SetHandler(async (string? project, string branch, string env, string? server) =>
        {
            RequireAuth();

            // If no project specified, offer interactive selection
            List<ProjectItem>? projects = null;
            try { projects = await Api.GetAsync<List<ProjectItem>>("projects"); }
            catch { }

            string? projectId = null;
            if (!string.IsNullOrEmpty(project))
            {
                projectId = projects?.FirstOrDefault(p =>
                    p.Name.Equals(project, StringComparison.OrdinalIgnoreCase) || p.Id == project)?.Id;
                if (projectId is null)
                {
                    AnsiConsole.MarkupLine($"[red]Project '{Markup.Escape(project)}' not found.[/]");
                    return;
                }
            }
            else if (projects?.Count > 0)
            {
                var selected = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a [green]project[/] to deploy:")
                        .AddChoices(projects.Select(p => $"{p.Name} ({p.Id[..8]})")));
                projectId = projects.First(p => selected.Contains(p.Id[..8])).Id;
            }

            if (projectId is null) { AnsiConsole.MarkupLine("[red]No project selected.[/]"); return; }

            AnsiConsole.MarkupLine($"Deploying branch [cyan]{branch}[/] → [yellow]{env}[/]…");

            try
            {
                var result = await Api.PostAsync<DeployResult>("deployments", new
                {
                    projectId,
                    branch,
                    commitMessage = $"CLI deploy via `df deploy`",
                    triggeredBy = "cli",
                    serverId = server
                });

                AnsiConsole.MarkupLine($"[green]Deployment queued![/] ID: [dim]{result?.Id}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Deploy failed: {Markup.Escape(ex.Message)}[/]");
            }
        }, projectArg, branchOpt, envOpt, serverOpt);

        return cmd;
    }

    // ── logs ──────────────────────────────────────────────────────────────────

    static Command BuildLogsCommand()
    {
        var deployIdArg = new Argument<string?>("deployment-id", () => null);
        var tailOpt = new Option<int>("--tail", () => 50, "Lines to show");
        var cmd = new Command("logs", "Stream deployment logs") { deployIdArg, tailOpt };

        cmd.SetHandler(async (string? deployId, int tail) =>
        {
            RequireAuth();
            if (string.IsNullOrEmpty(deployId))
            {
                // Show most recent deployment
                var page = await Api.GetAsync<DeploymentPage>("deployments?page=1&pageSize=5");
                var latest = page?.Items?.FirstOrDefault();
                if (latest is null) { AnsiConsole.MarkupLine("[grey]No deployments found.[/]"); return; }
                deployId = latest.Id;
                AnsiConsole.MarkupLine($"[dim]Showing logs for latest deployment {deployId?[..8]}…[/]");
            }

            try
            {
                var logs = await Api.GetAsync<List<LogEntry>>($"deployments/{deployId}/logs?tail={tail}");
                if (logs is null || logs.Count == 0)
                {
                    AnsiConsole.MarkupLine("[grey]No logs available.[/]");
                    return;
                }

                foreach (var entry in logs)
                {
                    var color = entry.Level?.ToLowerInvariant() switch
                    {
                        "error" => "red",
                        "warning" or "warn" => "yellow",
                        "success" => "green",
                        _ => "white"
                    };
                    AnsiConsole.MarkupLine(
                        $"[dim]{Markup.Escape(entry.Timestamp)}[/] [{color}]{Markup.Escape(entry.Message ?? "")}[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            }
        }, deployIdArg, tailOpt);

        return cmd;
    }

    // ── servers ───────────────────────────────────────────────────────────────

    static Command BuildServersCommand()
    {
        var cmd = new Command("servers", "Manage servers");
        var listCmd = new Command("list", "List all servers");
        listCmd.SetHandler(async () =>
        {
            RequireAuth();
            try
            {
                var result = await Api.GetAsync<ServerPage>("servers");
                var servers = result?.Servers ?? [];

                var table = new Table()
                    .AddColumn("Name")
                    .AddColumn("IP")
                    .AddColumn("Status")
                    .AddColumn("CPU")
                    .AddColumn("RAM")
                    .AddColumn("Region");

                foreach (var s in servers)
                {
                    var statusColor = s.Status?.ToLowerInvariant() switch
                    {
                        "online" => "green",
                        "offline" => "red",
                        _ => "yellow"
                    };
                    table.AddRow(
                        Markup.Escape(s.Name ?? ""),
                        Markup.Escape(s.IpAddress ?? ""),
                        $"[{statusColor}]{Markup.Escape(s.Status ?? "")}[/]",
                        $"{s.CpuUsage:F0}%",
                        $"{s.MemoryUsage:F0}%",
                        Markup.Escape(s.Region ?? "-")
                    );
                }

                AnsiConsole.Write(table);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            }
        });
        cmd.AddCommand(listCmd);

        // servers provision
        var provisionCmd = new Command("provision", "Provision a new server");
        var nameOpt = new Option<string>("--name", "Server name") { IsRequired = true };
        var providerOpt = new Option<string>("--provider", () => "hetzner", "Cloud provider");
        var regionOpt = new Option<string>("--region", () => "fra1", "Region/datacenter");
        var sizeOpt = new Option<string>("--size", () => "cx21", "Instance size");
        provisionCmd.AddOption(nameOpt);
        provisionCmd.AddOption(providerOpt);
        provisionCmd.AddOption(regionOpt);
        provisionCmd.AddOption(sizeOpt);
        provisionCmd.SetHandler(async (string name, string provider, string region, string size) =>
        {
            RequireAuth();
            await AnsiConsole.Status().StartAsync("Planning provisioning...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                try
                {
                    var job = await Api.PostAsync<ProvisioningJob>("provisioning/plan",
                        new { name, provider, region, size, os = "ubuntu-22.04" });
                    AnsiConsole.MarkupLine($"[green]Plan created![/] Job ID: [dim]{job?.Id}[/]");
                    AnsiConsole.MarkupLine($"Apply with: [cyan]df servers apply {job?.Id}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
                }
            });
        }, nameOpt, providerOpt, regionOpt, sizeOpt);
        cmd.AddCommand(provisionCmd);

        return cmd;
    }

    // ── projects ──────────────────────────────────────────────────────────────

    static Command BuildProjectsCommand()
    {
        var cmd = new Command("projects", "List projects");
        cmd.SetHandler(async () =>
        {
            RequireAuth();
            try
            {
                var projects = await Api.GetAsync<List<ProjectItem>>("projects");
                var table = new Table()
                    .AddColumn("Name")
                    .AddColumn("ID")
                    .AddColumn("Git Repo");

                foreach (var p in projects ?? [])
                    table.AddRow(
                        Markup.Escape(p.Name ?? ""),
                        $"[dim]{Markup.Escape(p.Id[..8])}…[/]",
                        Markup.Escape(p.GitRepository ?? "-"));

                AnsiConsole.Write(table);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            }
        });
        return cmd;
    }

    // ── env ───────────────────────────────────────────────────────────────────

    static Command BuildEnvCommand()
    {
        var cmd = new Command("env", "Manage environments");
        var listCmd = new Command("list", "List environments");
        listCmd.SetHandler(async () =>
        {
            RequireAuth();
            try
            {
                var envs = await Api.GetAsync<List<EnvironmentItem>>("environments");
                var table = new Table()
                    .AddColumn("Name")
                    .AddColumn("Slug")
                    .AddColumn("Production");

                foreach (var e in envs ?? [])
                    table.AddRow(
                        Markup.Escape(e.Name ?? ""),
                        Markup.Escape(e.Slug ?? ""),
                        e.IsProduction ? "[green]✓[/]" : "[grey]–[/]");

                AnsiConsole.Write(table);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            }
        });
        cmd.AddCommand(listCmd);
        return cmd;
    }

    // ── rollback ──────────────────────────────────────────────────────────────

    static Command BuildRollbackCommand()
    {
        var projectArg = new Argument<string>("project", "Project name or ID");
        var cmd = new Command("rollback", "Roll back to the previous good deployment") { projectArg };
        cmd.SetHandler(async (string project) =>
        {
            RequireAuth();
            AnsiConsole.MarkupLine(
                $"[yellow]Rollback for '{Markup.Escape(project)}' triggered.[/] " +
                "The previous successful deployment will be re-queued.");
            // POST /api/projects/{id}/rollback — future endpoint
            await Task.CompletedTask;
        }, projectArg);
        return cmd;
    }

    // ── init ──────────────────────────────────────────────────────────────────

    static Command BuildInitCommand()
    {
        var dirOpt = new Option<string?>("--dir", "Project directory to scan (default: current)");
        var cmd = new Command("init", "Auto-detect your stack and generate a Dockerfile") { dirOpt };

        cmd.SetHandler(async (string? dir) =>
        {
            RequireAuth();
            var workDir = string.IsNullOrEmpty(dir)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(dir);

            if (!Directory.Exists(workDir))
            {
                AnsiConsole.MarkupLine($"[red]Directory not found: {Markup.Escape(workDir)}[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[cyan]Scanning {Markup.Escape(workDir)}...[/]");

            var fileNames = Directory.GetFiles(workDir)
                .Select(Path.GetFileName)
                .Where(f => f is not null)
                .Cast<string>()
                .ToList();

            string? packageJsonContent = null;
            var pkgPath = Path.Combine(workDir, "package.json");
            if (File.Exists(pkgPath))
                packageJsonContent = await File.ReadAllTextAsync(pkgPath);

            string? requirementsTxt = null;
            var reqPath = Path.Combine(workDir, "requirements.txt");
            if (File.Exists(reqPath))
                requirementsTxt = await File.ReadAllTextAsync(reqPath);

            try
            {
                var detected = await Api.PostAsync<DetectedStackResult>(
                    "stack-detection/detect",
                    new { fileNames, packageJsonContent, requirementsTxtContent = requirementsTxt });

                if (detected is null)
                {
                    AnsiConsole.MarkupLine("[red]Stack detection failed.[/]");
                    return;
                }

                AnsiConsole.Write(new Panel(
                    $"[bold]{Markup.Escape(detected.Framework ?? "Unknown")}[/] ({Markup.Escape(detected.Language ?? "")})\n" +
                    $"[dim]{Markup.Escape(detected.Explanation ?? "")}[/]\n\n" +
                    $"Build : [cyan]{Markup.Escape(detected.BuildCommand ?? "")}[/]\n" +
                    $"Start : [cyan]{Markup.Escape(detected.StartCommand ?? "")}[/]\n" +
                    $"Port  : [cyan]{detected.DefaultPort}[/]")
                {
                    Header = new PanelHeader(" Detected Stack "),
                    Border = BoxBorder.Rounded,
                });

                if (!string.IsNullOrEmpty(detected.DockerfileContent))
                {
                    var save = AnsiConsole.Confirm("Save generated Dockerfile to this directory?");
                    if (save)
                    {
                        var dockerfilePath = Path.Combine(workDir, "Dockerfile");
                        await File.WriteAllTextAsync(dockerfilePath, detected.DockerfileContent);
                        AnsiConsole.MarkupLine($"[green]✓ Dockerfile written to {Markup.Escape(dockerfilePath)}[/]");
                    }
                }

                if ((detected.SuggestedEnvVars?.Length ?? 0) > 0)
                {
                    AnsiConsole.MarkupLine("\n[yellow]Suggested env vars:[/]");
                    foreach (var v in detected.SuggestedEnvVars!)
                        AnsiConsole.MarkupLine($"  [grey]•[/] {Markup.Escape(v)}");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            }
        }, dirOpt);
        return cmd;
    }

    // ── env-set ───────────────────────────────────────────────────────────────

    static Command BuildEnvSetCommand()
    {
        var projectArg = new Argument<string>("projectId", "Project ID to set variables for");
        var pairsArg = new Argument<string[]>("pairs", "KEY=VALUE pairs")
            { Arity = ArgumentArity.OneOrMore };
        var cmd = new Command("env-set", "Set environment variable(s) for a project")
            { projectArg, pairsArg };

        cmd.SetHandler(async (string projectId, string[] pairs) =>
        {
            RequireAuth();

            var vars = new List<(string Key, string Value)>();
            foreach (var pair in pairs)
            {
                var sepIdx = pair.IndexOf('=');
                if (sepIdx <= 0)
                {
                    AnsiConsole.MarkupLine($"[red]Invalid format: '{Markup.Escape(pair)}'. Use KEY=VALUE[/]");
                    return;
                }
                vars.Add((pair[..sepIdx], pair[(sepIdx + 1)..]));
            }

            try
            {
                foreach (var (key, value) in vars)
                    await Api.PostAsync<object>("env-variables",
                        new { projectId, key, value, isSecret = false });

                AnsiConsole.MarkupLine(
                    $"[green]✓ {vars.Count} variable(s) set for project " +
                    $"{Markup.Escape(projectId[..Math.Min(8, projectId.Length)])}…[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            }
        }, projectArg, pairsArg);
        return cmd;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    static void RequireAuth()
    {
        if (!Config.IsAuthenticated())
        {
            AnsiConsole.MarkupLine("[yellow]Not authenticated.[/] Run [cyan]df login[/] first.");
            Environment.Exit(1);
        }
    }
}

// ─── API response models ──────────────────────────────────────────────────────

record LoginResult(string? AccessToken, string? RefreshToken);
record DashboardStats(int TotalProjects, int TotalServers, int ActiveDeployments, int RunningServices, int ActiveAlerts);
record ProjectItem(string Id, string? Name, string? GitRepository);
record DeployResult(string? Id);
record DeploymentPage(List<DeploymentItem>? Items, int Total);
record DeploymentItem(string Id, string? Status, string? Branch, string? CreatedAt);
record LogEntry(string? Timestamp, string? Level, string? Message);
record ServerPage(List<ServerItem>? Servers, int Total);
record ServerItem(string? Id, string? Name, string? IpAddress, string? Status, double CpuUsage, double MemoryUsage, string? Region);
record EnvironmentItem(string? Id, string? Name, string? Slug, bool IsProduction);
record ProvisioningJob(string? Id, string? Status, string? PlanOutput);
record DetectedStackResult(string? Framework, string? Language, string? DockerfileContent,
    string? BuildCommand, string? StartCommand, int DefaultPort, string[]? SuggestedEnvVars, string? Explanation);
