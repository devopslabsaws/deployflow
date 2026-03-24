using DeployFlow.Application.Common;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.StackDetection;

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public enum DetectedFramework
{
    // JavaScript / TypeScript
    Nextjs, React, Vue, Nuxt, Angular, SvelteKit, Remix, Astro,
    // Backend JS
    Nodejs, Express, Nestjs,
    // .NET
    DotnetWebApi, DotnetBlazor, DotnetMvc, Dotnet,
    // Python
    FastApi, Django, Flask, Python,
    // Go
    Go,
    // PHP
    Laravel, Php,
    // Ruby
    Rails, Ruby,
    // Static
    Static,
    Unknown
}

public record DetectedStack(
    DetectedFramework Framework,
    string Language,
    string DockerfileContent,
    string BuildCommand,
    string StartCommand,
    string InstallCommand,
    int DefaultPort,
    string[] SuggestedEnvVars,
    string Explanation
);

// ─── Detect Stack Query ───────────────────────────────────────────────────────

/// <summary>
/// Analyzes file manifest + optional content hints from a repository
/// and returns a complete deployment scaffold.
/// </summary>
public record DetectStackQuery(
    /// <summary>File names present in the repo root (not full paths needed)</summary>
    List<string> FileNames,
    string? PackageJsonContent = null,
    string? RequirementsTxtContent = null,
    string? ComposerJsonContent = null,
    string? GemfileContent = null,
    Guid? ProjectId = null
) : IRequest<Result<DetectedStack>>;

public class DetectStackQueryHandler : IRequestHandler<DetectStackQuery, Result<DetectedStack>>
{
    public Task<Result<DetectedStack>> Handle(DetectStackQuery request, CancellationToken ct)
    {
        var files = request.FileNames.Select(f => f.ToLowerInvariant()).ToHashSet();
        var pkg = request.PackageJsonContent ?? "";
        var reqs = request.RequirementsTxtContent ?? "";
        var composer = request.ComposerJsonContent ?? "";
        var gemfile = request.GemfileContent ?? "";

        var stack = DetectStack(files, pkg, reqs, composer, gemfile);
        return Task.FromResult(Result<DetectedStack>.Success(stack));
    }

    private static DetectedStack DetectStack(
        HashSet<string> files, string pkg, string reqs, string composer, string gemfile)
    {
        // ── .NET ──────────────────────────────────────────────────────────────
        if (files.Any(f => f.EndsWith(".csproj") || f.EndsWith(".sln")))
        {
            var isBlazor = pkg.Contains("blazor", StringComparison.OrdinalIgnoreCase);
            return new DetectedStack(
                isBlazor ? DetectedFramework.DotnetBlazor : DetectedFramework.DotnetWebApi,
                "csharp",
                Dockerfiles.DotnetApiDockerfile(),
                "dotnet publish -c Release -o out",
                "dotnet out/*.dll",
                "dotnet restore",
                8080,
                ["ASPNETCORE_ENVIRONMENT", "ConnectionStrings__DefaultConnection", "JWT__Secret"],
                "Detected .NET project. Multi-stage build with sdk + aspnet runtime images."
            );
        }

        // ── Node/JS — detect specific framework ──────────────────────────────
        if (files.Contains("package.json") && !string.IsNullOrEmpty(pkg))
        {
            if (pkg.Contains("\"next\""))
                return new DetectedStack(
                    DetectedFramework.Nextjs, "typescript",
                    Dockerfiles.NextjsDockerfile(),
                    "npm run build", "npm start", "npm ci",
                    3000,
                    ["NEXT_PUBLIC_API_URL", "NEXTAUTH_SECRET", "NEXTAUTH_URL", "DATABASE_URL"],
                    "Detected Next.js app. Standalone output mode with multi-stage build.");

            if (pkg.Contains("\"nuxt\""))
                return new DetectedStack(
                    DetectedFramework.Nuxt, "typescript",
                    Dockerfiles.NuxtDockerfile(),
                    "npm run build", "node .output/server/index.mjs", "npm ci",
                    3000,
                    ["NUXT_PUBLIC_API_BASE", "NUXT_SECRET"],
                    "Detected Nuxt 3 app.");

            if (pkg.Contains("\"@angular/core\""))
                return new DetectedStack(
                    DetectedFramework.Angular, "typescript",
                    Dockerfiles.StaticNginxDockerfile("dist/browser"),
                    "npm run build -- --configuration production",
                    "nginx -g 'daemon off;'", "npm ci",
                    80,
                    ["API_URL"],
                    "Detected Angular app. SPA build served by Nginx.");

            if (pkg.Contains("\"svelte\"") || pkg.Contains("\"@sveltejs/kit\""))
                return new DetectedStack(
                    DetectedFramework.SvelteKit, "typescript",
                    Dockerfiles.NodeDockerfile(),
                    "npm run build", "node build", "npm ci",
                    3000,
                    ["PUBLIC_API_URL"],
                    "Detected SvelteKit app.");

            if (pkg.Contains("\"@nestjs/core\""))
                return new DetectedStack(
                    DetectedFramework.Nestjs, "typescript",
                    Dockerfiles.NodeDockerfile(),
                    "npm run build", "node dist/main", "npm ci",
                    3000,
                    ["DATABASE_URL", "JWT_SECRET", "PORT"],
                    "Detected NestJS app.");

            if (pkg.Contains("\"express\""))
                return new DetectedStack(
                    DetectedFramework.Express, "javascript",
                    Dockerfiles.NodeDockerfile(),
                    "", "node index.js", "npm ci",
                    3000,
                    ["PORT", "DATABASE_URL", "JWT_SECRET"],
                    "Detected Express.js app.");

            if (pkg.Contains("\"react\""))
                return new DetectedStack(
                    DetectedFramework.React, "typescript",
                    Dockerfiles.StaticNginxDockerfile("build"),
                    "npm run build", "nginx -g 'daemon off;'", "npm ci",
                    80,
                    ["REACT_APP_API_URL"],
                    "Detected React SPA. Production build served by Nginx.");

            // Generic Node
            return new DetectedStack(
                DetectedFramework.Nodejs, "javascript",
                Dockerfiles.NodeDockerfile(),
                "", "node index.js", "npm ci",
                3000,
                ["PORT", "NODE_ENV"],
                "Detected Node.js app.");
        }

        // ── Python ────────────────────────────────────────────────────────────
        if (files.Contains("requirements.txt") || files.Contains("pipfile") || files.Contains("pyproject.toml"))
        {
            if (reqs.Contains("fastapi") || reqs.Contains("uvicorn"))
                return new DetectedStack(
                    DetectedFramework.FastApi, "python",
                    Dockerfiles.PythonDockerfile("uvicorn main:app --host 0.0.0.0 --port 8000"),
                    "", "uvicorn main:app --host 0.0.0.0 --port 8000", "pip install -r requirements.txt",
                    8000,
                    ["DATABASE_URL", "SECRET_KEY", "CORS_ORIGINS"],
                    "Detected FastAPI app.");

            if (reqs.Contains("django"))
                return new DetectedStack(
                    DetectedFramework.Django, "python",
                    Dockerfiles.PythonDockerfile("gunicorn config.wsgi:application --bind 0.0.0.0:8000"),
                    "python manage.py collectstatic --noinput",
                    "gunicorn config.wsgi:application --bind 0.0.0.0:8000",
                    "pip install -r requirements.txt",
                    8000,
                    ["SECRET_KEY", "DATABASE_URL", "ALLOWED_HOSTS", "DJANGO_SETTINGS_MODULE"],
                    "Detected Django app.");

            if (reqs.Contains("flask"))
                return new DetectedStack(
                    DetectedFramework.Flask, "python",
                    Dockerfiles.PythonDockerfile("gunicorn app:app --bind 0.0.0.0:5000"),
                    "", "gunicorn app:app --bind 0.0.0.0:5000", "pip install -r requirements.txt",
                    5000,
                    ["FLASK_SECRET_KEY", "DATABASE_URL"],
                    "Detected Flask app.");

            return new DetectedStack(
                DetectedFramework.Python, "python",
                Dockerfiles.PythonDockerfile("python main.py"),
                "", "python main.py", "pip install -r requirements.txt",
                8000,
                ["PORT"],
                "Detected Python app.");
        }

        // ── Go ────────────────────────────────────────────────────────────────
        if (files.Contains("go.mod"))
            return new DetectedStack(
                DetectedFramework.Go, "go",
                Dockerfiles.GoDockerfile(),
                "go build -o app .", "./app", "",
                8080,
                ["PORT", "DATABASE_URL"],
                "Detected Go app. Multi-stage build with alpine runtime.");

        // ── PHP / Laravel ─────────────────────────────────────────────────────
        if (files.Contains("composer.json"))
        {
            if (composer.Contains("laravel/framework"))
                return new DetectedStack(
                    DetectedFramework.Laravel, "php",
                    Dockerfiles.PhpDockerfile(),
                    "php artisan optimize", "php-fpm", "composer install --no-dev",
                    9000,
                    ["APP_KEY", "APP_ENV", "DB_CONNECTION", "DB_HOST", "DB_PASSWORD"],
                    "Detected Laravel app.");

            return new DetectedStack(
                DetectedFramework.Php, "php",
                Dockerfiles.PhpDockerfile(),
                "", "php-fpm", "composer install --no-dev",
                9000,
                ["APP_ENV"],
                "Detected PHP app.");
        }

        // ── Ruby / Rails ──────────────────────────────────────────────────────
        if (files.Contains("gemfile") || files.Contains("gemfile.lock"))
        {
            if (gemfile.Contains("rails"))
                return new DetectedStack(
                    DetectedFramework.Rails, "ruby",
                    Dockerfiles.RubyDockerfile(),
                    "bundle exec rails assets:precompile",
                    "bundle exec rails server -b 0.0.0.0",
                    "bundle install",
                    3000,
                    ["RAILS_ENV", "SECRET_KEY_BASE", "DATABASE_URL"],
                    "Detected Ruby on Rails app.");

            return new DetectedStack(
                DetectedFramework.Ruby, "ruby",
                Dockerfiles.RubyDockerfile(),
                "", "bundle exec ruby app.rb", "bundle install",
                4567,
                ["RACK_ENV"],
                "Detected Ruby app.");
        }

        // ── Static HTML ───────────────────────────────────────────────────────
        if (files.Contains("index.html"))
            return new DetectedStack(
                DetectedFramework.Static, "html",
                Dockerfiles.StaticNginxDockerfile("."),
                "", "nginx -g 'daemon off;'", "",
                80,
                [],
                "Detected static HTML site. Served by Nginx.");

        return new DetectedStack(
            DetectedFramework.Unknown, "unknown",
            Dockerfiles.NodeDockerfile(),
            "", "", "",
            3000,
            ["PORT"],
            "Could not auto-detect stack. Generic Node.js Dockerfile generated as a starting point.");
    }
}

// ─── Apply Detected Stack to Project ─────────────────────────────────────────

public record ApplyStackDetectionCommand(
    Guid ProjectId,
    DetectedFramework Framework,
    string? BuildCommandOverride,
    string? StartCommandOverride,
    string? InstallCommandOverride,
    int? PortOverride
) : IRequest<Result<bool>>;

public class ApplyStackDetectionCommandHandler : IRequestHandler<ApplyStackDetectionCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ApplyStackDetectionCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(ApplyStackDetectionCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<bool>.Failure("Project not found.", "404");

        // Store detection results: framework, build/start/install commands, port
        project.Update(
            buildCommand: request.BuildCommandOverride,
            startCommand: request.StartCommandOverride,
            installCommand: request.InstallCommandOverride,
            framework: request.Framework.ToString().ToLowerInvariant(),
            port: request.PortOverride);

        await _uow.Projects.UpdateAsync(project, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─── Dockerfile Templates ─────────────────────────────────────────────────────

internal static class Dockerfiles
{
    public static string NextjsDockerfile() => """
        # ── Stage 1: Dependencies ──────────────────────────────────────────────
        FROM node:20-alpine AS deps
        RUN apk add --no-cache libc6-compat
        WORKDIR /app
        COPY package*.json ./
        RUN npm ci

        # ── Stage 2: Build ─────────────────────────────────────────────────────
        FROM node:20-alpine AS builder
        WORKDIR /app
        COPY --from=deps /app/node_modules ./node_modules
        COPY . .
        ENV NEXT_TELEMETRY_DISABLED=1
        RUN npm run build

        # ── Stage 3: Runner ────────────────────────────────────────────────────
        FROM node:20-alpine AS runner
        WORKDIR /app
        ENV NODE_ENV=production
        ENV NEXT_TELEMETRY_DISABLED=1
        RUN addgroup --system --gid 1001 nodejs
        RUN adduser --system --uid 1001 nextjs
        COPY --from=builder /app/public ./public
        COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
        COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static
        USER nextjs
        EXPOSE 3000
        ENV PORT=3000
        CMD ["node", "server.js"]
        """;

    public static string NodeDockerfile() => """
        FROM node:20-alpine AS builder
        WORKDIR /app
        COPY package*.json ./
        RUN npm ci --only=production
        COPY . .

        FROM node:20-alpine
        WORKDIR /app
        RUN addgroup -S appgroup && adduser -S appuser -G appgroup
        COPY --from=builder /app .
        USER appuser
        EXPOSE 3000
        ENV PORT=3000
        CMD ["node", "index.js"]
        """;

    public static string DotnetApiDockerfile() => """
        # ── Stage 1: Build ─────────────────────────────────────────────────────
        FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
        WORKDIR /src
        COPY *.sln .
        COPY **/*.csproj ./
        RUN dotnet restore
        COPY . .
        RUN dotnet publish -c Release -o /app/publish

        # ── Stage 2: Runtime ───────────────────────────────────────────────────
        FROM mcr.microsoft.com/dotnet/aspnet:8.0
        WORKDIR /app
        RUN addgroup --system --gid 1001 dotnet
        RUN adduser --system --uid 1001 --ingroup dotnet appuser
        COPY --from=build /app/publish .
        USER appuser
        EXPOSE 8080
        ENV ASPNETCORE_URLS=http://+:8080
        ENTRYPOINT ["dotnet", "App.dll"]
        """;

    public static string PythonDockerfile(string cmd) => $"""
        FROM python:3.12-slim
        WORKDIR /app
        RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
        COPY requirements.txt .
        RUN pip install --no-cache-dir -r requirements.txt
        COPY . .
        RUN chown -R appuser:appgroup /app
        USER appuser
        EXPOSE 8000
        CMD ["{cmd.Split(' ')[0]}", {string.Join(", ", cmd.Split(' ').Skip(1).Select(a => $"\"{a}\""))}]
        """;

    public static string GoDockerfile() => """
        # ── Stage 1: Build ─────────────────────────────────────────────────────
        FROM golang:1.22-alpine AS builder
        RUN apk add --no-cache git ca-certificates
        WORKDIR /app
        COPY go.mod go.sum ./
        RUN go mod download
        COPY . .
        RUN CGO_ENABLED=0 GOOS=linux go build -a -installsuffix cgo -o app .

        # ── Stage 2: Runtime ───────────────────────────────────────────────────
        FROM alpine:3.19
        RUN apk --no-cache add ca-certificates tzdata
        WORKDIR /root/
        COPY --from=builder /app/app .
        EXPOSE 8080
        CMD ["./app"]
        """;

    public static string PhpDockerfile() => """
        FROM php:8.3-fpm-alpine
        WORKDIR /var/www/html
        RUN apk add --no-cache nginx supervisor
        RUN docker-php-ext-install pdo pdo_mysql
        COPY --from=composer:latest /usr/bin/composer /usr/bin/composer
        COPY composer*.json ./
        RUN composer install --no-dev --optimize-autoloader
        COPY . .
        RUN chown -R www-data:www-data /var/www/html
        EXPOSE 9000
        CMD ["php-fpm"]
        """;

    public static string NuxtDockerfile() => """
        FROM node:20-alpine AS builder
        WORKDIR /app
        COPY package*.json ./
        RUN npm ci
        COPY . .
        RUN npm run build

        FROM node:20-alpine
        WORKDIR /app
        COPY --from=builder /app/.output ./.output
        EXPOSE 3000
        ENV NUXT_HOST=0.0.0.0 NUXT_PORT=3000
        CMD ["node", ".output/server/index.mjs"]
        """;

    public static string RubyDockerfile() => """
        FROM ruby:3.3-slim
        RUN apt-get update && apt-get install -y build-essential libpq-dev nodejs
        WORKDIR /app
        COPY Gemfile Gemfile.lock ./
        RUN bundle install --without development test
        COPY . .
        RUN adduser --disabled-password appuser && chown -R appuser /app
        USER appuser
        EXPOSE 3000
        CMD ["bundle", "exec", "rails", "server", "-b", "0.0.0.0"]
        """;

    public static string StaticNginxDockerfile(string buildDir) => $"""
        FROM node:20-alpine AS builder
        WORKDIR /app
        COPY package*.json ./
        RUN npm ci
        COPY . .
        RUN npm run build

        FROM nginx:alpine
        COPY --from=builder /app/{buildDir} /usr/share/nginx/html
        COPY nginx.conf /etc/nginx/conf.d/default.conf 2>/dev/null || true
        EXPOSE 80
        CMD ["nginx", "-g", "daemon off;"]
        """;
}
