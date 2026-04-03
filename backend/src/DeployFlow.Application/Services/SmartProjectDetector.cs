using System.Text.Json;

namespace DeployFlow.Application.Services;

/// <summary>
/// Detects project type from a repository structure and produces a typed
/// <see cref="ProjectProfile"/> that the pipeline generator uses to scaffold stages and steps.
/// </summary>
public static class SmartProjectDetector
{
    // File indicators → runtime
    private static readonly (string file, string runtime, string framework)[] Signatures =
    [
        ("package.json",       "node",   ""),
        ("requirements.txt",   "python", ""),
        ("pyproject.toml",     "python", ""),
        ("Pipfile",            "python", ""),
        ("go.mod",             "go",     ""),
        ("Gemfile",            "ruby",   ""),
        ("composer.json",      "php",    ""),
        ("pom.xml",            "java",   "maven"),
        ("build.gradle",       "java",   "gradle"),
        ("Cargo.toml",         "rust",   ""),
        ("*.csproj",           "dotnet", ""),
        ("*.sln",              "dotnet", ""),
        ("Dockerfile",         "docker", ""),
    ];

    /// <summary>
    /// Build a <see cref="ProjectProfile"/> from the file list that a repository scanner provides.
    /// <paramref name="repoFiles"/> is a flat list of file paths (relative to repo root).
    /// </summary>
    public static ProjectProfile Detect(
        string projectName,
        IEnumerable<string> repoFiles,
        string? packageJsonContent = null)
    {
        var files = repoFiles.Select(f => f.ToLowerInvariant()).ToHashSet();

        // ── Dockerfile wins if present + no other clear runtime match ──────
        bool hasDockerfile = files.Contains("dockerfile");

        // ── Node.js ───────────────────────────────────────────────────────
        if (files.Contains("package.json"))
        {
            var pkg = ParsePackageJson(packageJsonContent);
            return BuildNodeProfile(projectName, pkg, hasDockerfile, files);
        }

        // ── Python ────────────────────────────────────────────────────────
        if (files.Contains("requirements.txt") || files.Contains("pyproject.toml") || files.Contains("pipfile"))
        {
            return BuildPythonProfile(projectName, files, hasDockerfile);
        }

        // ── Go ─────────────────────────────────────────────────────────────
        if (files.Contains("go.mod"))
            return BuildGoProfile(projectName, hasDockerfile);

        // ── Ruby ──────────────────────────────────────────────────────────
        if (files.Contains("gemfile"))
            return BuildRubyProfile(projectName, hasDockerfile);

        // ── PHP ───────────────────────────────────────────────────────────
        if (files.Contains("composer.json"))
            return BuildPhpProfile(projectName, hasDockerfile);

        // ── Java ──────────────────────────────────────────────────────────
        if (files.Contains("pom.xml"))
            return BuildJavaProfile(projectName, "maven", hasDockerfile);
        if (files.Contains("build.gradle") || files.Contains("build.gradle.kts"))
            return BuildJavaProfile(projectName, "gradle", hasDockerfile);

        // ── .NET ──────────────────────────────────────────────────────────
        if (files.Any(f => f.EndsWith(".csproj") || f.EndsWith(".sln")))
            return BuildDotnetProfile(projectName, hasDockerfile);

        // ── Dockerfile only ────────────────────────────────────────────────
        if (hasDockerfile)
            return BuildDockerProfile(projectName);

        // ── Static site fallback ───────────────────────────────────────────
        return BuildStaticProfile(projectName, files);
    }

    // ── Node.js ──────────────────────────────────────────────────────────────

    private static ProjectProfile BuildNodeProfile(
        string name, PackageJsonInfo pkg, bool hasDockerfile, HashSet<string> files)
    {
        string framework = DetectNodeFramework(pkg, files);
        int port = framework switch { "nextjs" => 3000, "angular" => 4200, "nuxt" => 3000, _ => 3000 };

        string buildCmd = pkg.Scripts.GetValueOrDefault("build") is { Length: > 0 } b ? $"npm run build" : "";
        string startCmd = framework switch
        {
            "nextjs"   => "npm start",
            "react"    => "npx serve -s build -l 3000",
            "vue"      => "npx serve -s dist -l 3000",
            "angular"  => "npx serve -s dist -l 4200",
            "nuxt"     => "npm start",
            _           => pkg.Scripts.ContainsKey("start") ? "npm start" : "node index.js",
        };
        bool hasTests = pkg.Scripts.ContainsKey("test");

        return new ProjectProfile
        {
            Name          = name,
            Runtime       = "node",
            Framework     = framework,
            Port          = port,
            BuildCommand  = buildCmd,
            StartCommand  = startCmd,
            InstallCommand = "npm ci --prefer-offline || npm install",
            TestCommand   = hasTests ? "npm test -- --passWithNoTests --watchAll=false" : null,
            HasDockerfile = hasDockerfile,
            HealthPath    = "/",
            ContainerName = ToSlug(name),
            ImageName     = $"{ToSlug(name)}:latest",
        };
    }

    private static string DetectNodeFramework(PackageJsonInfo pkg, HashSet<string> files)
    {
        if (pkg.Deps.ContainsKey("next"))                return "nextjs";
        if (pkg.Deps.ContainsKey("nuxt"))                return "nuxt";
        if (files.Contains("angular.json"))              return "angular";
        if (pkg.Deps.ContainsKey("@angular/core"))       return "angular";
        if (pkg.Deps.ContainsKey("vue") || pkg.Deps.ContainsKey("@vue/core")) return "vue";
        if (pkg.Deps.ContainsKey("gatsby"))              return "gatsby";
        if (pkg.Deps.ContainsKey("react"))               return "react";
        if (pkg.Deps.ContainsKey("express"))             return "express";
        if (pkg.Deps.ContainsKey("fastify"))             return "fastify";
        if (pkg.Deps.ContainsKey("koa"))                 return "koa";
        return "node";
    }

    // ── Python ───────────────────────────────────────────────────────────────

    private static ProjectProfile BuildPythonProfile(string name, HashSet<string> files, bool hasDockerfile)
    {
        string framework = "python";
        string startCmd  = "python app.py";
        int port         = 8000;

        if (files.Contains("manage.py"))        { framework = "django";   startCmd = "gunicorn wsgi:application --bind 0.0.0.0:8000"; }
        else if (files.Any(f => f.EndsWith("app.py") || f.EndsWith("main.py")))
        {
            // guess FastAPI / Flask
            framework = "flask";
            startCmd  = "gunicorn app:app --bind 0.0.0.0:8000 --workers 2";
        }

        return new ProjectProfile
        {
            Name           = name,
            Runtime        = "python",
            Framework      = framework,
            Port           = port,
            BuildCommand   = "",
            StartCommand   = startCmd,
            InstallCommand = "pip install -r requirements.txt",
            TestCommand    = files.Contains("pytest.ini") || files.Contains("setup.cfg")
                               ? "pytest --tb=short -q"
                               : null,
            HasDockerfile  = hasDockerfile,
            HealthPath     = "/health",
            ContainerName  = ToSlug(name),
            ImageName      = $"{ToSlug(name)}:latest",
        };
    }

    // ── Go ───────────────────────────────────────────────────────────────────

    private static ProjectProfile BuildGoProfile(string name, bool hasDockerfile) => new()
    {
        Name           = name,
        Runtime        = "go",
        Framework      = "go",
        Port           = 8080,
        InstallCommand = "go mod download",
        BuildCommand   = "go build -o app ./...",
        StartCommand   = "./app",
        TestCommand    = "go test ./... -v",
        HasDockerfile  = hasDockerfile,
        HealthPath     = "/health",
        ContainerName  = ToSlug(name),
        ImageName      = $"{ToSlug(name)}:latest",
    };

    // ── Ruby ─────────────────────────────────────────────────────────────────

    private static ProjectProfile BuildRubyProfile(string name, bool hasDockerfile) => new()
    {
        Name           = name,
        Runtime        = "ruby",
        Framework      = "rails",
        Port           = 3000,
        InstallCommand = "bundle install",
        BuildCommand   = "bundle exec rake assets:precompile",
        StartCommand   = "bundle exec rails server -b 0.0.0.0",
        TestCommand    = "bundle exec rspec",
        HasDockerfile  = hasDockerfile,
        HealthPath     = "/",
        ContainerName  = ToSlug(name),
        ImageName      = $"{ToSlug(name)}:latest",
    };

    // ── PHP ──────────────────────────────────────────────────────────────────

    private static ProjectProfile BuildPhpProfile(string name, bool hasDockerfile) => new()
    {
        Name           = name,
        Runtime        = "php",
        Framework      = "php",
        Port           = 8080,
        InstallCommand = "composer install --no-dev --optimize-autoloader",
        BuildCommand   = "",
        StartCommand   = "php -S 0.0.0.0:8080 -t public",
        HasDockerfile  = hasDockerfile,
        HealthPath     = "/",
        ContainerName  = ToSlug(name),
        ImageName      = $"{ToSlug(name)}:latest",
    };

    // ── Java ─────────────────────────────────────────────────────────────────

    private static ProjectProfile BuildJavaProfile(string name, string tool, bool hasDockerfile) => new()
    {
        Name           = name,
        Runtime        = "java",
        Framework      = "java",
        Port           = 8080,
        InstallCommand = tool == "maven" ? "mvn dependency:resolve" : "gradle dependencies",
        BuildCommand   = tool == "maven" ? "mvn package -DskipTests" : "gradle build",
        StartCommand   = "java -jar target/*.jar",
        TestCommand    = tool == "maven" ? "mvn test" : "gradle test",
        HasDockerfile  = hasDockerfile,
        HealthPath     = "/actuator/health",
        ContainerName  = ToSlug(name),
        ImageName      = $"{ToSlug(name)}:latest",
    };

    // ── .NET ─────────────────────────────────────────────────────────────────

    private static ProjectProfile BuildDotnetProfile(string name, bool hasDockerfile) => new()
    {
        Name           = name,
        Runtime        = "dotnet",
        Framework      = "aspnet",
        Port           = 8080,
        InstallCommand = "dotnet restore",
        BuildCommand   = "dotnet publish -c Release -o out",
        StartCommand   = "dotnet out/*.dll",
        TestCommand    = "dotnet test",
        HasDockerfile  = hasDockerfile,
        HealthPath     = "/health",
        ContainerName  = ToSlug(name),
        ImageName      = $"{ToSlug(name)}:latest",
    };

    // ── Docker-only ──────────────────────────────────────────────────────────

    private static ProjectProfile BuildDockerProfile(string name) => new()
    {
        Name           = name,
        Runtime        = "docker",
        Framework      = "docker",
        Port           = 80,
        InstallCommand = "",
        BuildCommand   = $"docker build -t {ToSlug(name)}:latest .",
        StartCommand   = $"docker run -d -p 80:80 --name {ToSlug(name)} {ToSlug(name)}:latest",
        HealthPath     = "/",
        ContainerName  = ToSlug(name),
        ImageName      = $"{ToSlug(name)}:latest",
        HasDockerfile  = true,
    };

    // ── Static site ──────────────────────────────────────────────────────────

    private static ProjectProfile BuildStaticProfile(string name, HashSet<string> files)
    {
        bool hasIndex = files.Contains("index.html");
        return new ProjectProfile
        {
            Name           = name,
            Runtime        = "static",
            Framework      = "static",
            Port           = 80,
            InstallCommand = "",
            BuildCommand   = "",
            StartCommand   = "npx serve -s . -l 80",
            HealthPath     = "/",
            ContainerName  = ToSlug(name),
            ImageName      = $"{ToSlug(name)}:latest",
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PackageJsonInfo ParsePackageJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root  = doc.RootElement;
            var deps  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var scripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (root.TryGetProperty("dependencies", out var d))
                foreach (var p in d.EnumerateObject()) deps[p.Name] = p.Value.GetString() ?? "";
            if (root.TryGetProperty("devDependencies", out var dd))
                foreach (var p in dd.EnumerateObject()) deps[p.Name] = p.Value.GetString() ?? "";
            if (root.TryGetProperty("scripts", out var s))
                foreach (var p in s.EnumerateObject()) scripts[p.Name] = p.Value.GetString() ?? "";

            return new() { Deps = deps, Scripts = scripts };
        }
        catch { return new(); }
    }

    private static string ToSlug(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

    private sealed class PackageJsonInfo
    {
        public Dictionary<string, string> Deps    { get; init; } = [];
        public Dictionary<string, string> Scripts { get; init; } = [];
    }
}

/// <summary>Describes a detected project&#39;s runtime characteristics.</summary>
public sealed record ProjectProfile
{
    public string  Name           { get; init; } = "";
    public string  Runtime        { get; init; } = "docker";
    public string  Framework      { get; init; } = "";
    public int     Port           { get; init; } = 3000;
    public string  InstallCommand { get; init; } = "";
    public string  BuildCommand   { get; init; } = "";
    public string  StartCommand   { get; init; } = "";
    public string? TestCommand    { get; init; }
    public bool    HasDockerfile  { get; init; }
    public string  HealthPath     { get; init; } = "/";
    public string  ContainerName  { get; init; } = "";
    public string  ImageName      { get; init; } = "";    /// <summary>Repository branch used for checkout. Defaults to 'main'.</summary>
    public string  Branch         { get; init; } = "main";}
