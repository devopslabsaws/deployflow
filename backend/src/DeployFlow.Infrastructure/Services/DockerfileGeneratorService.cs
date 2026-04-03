using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using System.Text;

namespace DeployFlow.Infrastructure.Services;

/// <summary>
/// Generates a bash script segment that:
///  1. Detects the project framework on the remote server by examining files.
///  2. Writes an optimised multi-stage Dockerfile via heredoc.
///  3. Builds the Docker image using BuildKit layer caching (--cache-from).
///
/// All detection and Dockerfile writing runs *on the remote server*, so the
/// backend never requires local filesystem access.
///
/// New stacks: add a <see cref="StackDefinition"/> to <see cref="Stacks"/>.
/// </summary>
public class DockerfileGeneratorService : IDockerfileGeneratorService
{
    // â”€â”€ Ordered list of stacks â€” first match wins â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static readonly IReadOnlyList<StackDefinition> Stacks = new List<StackDefinition>
    {
        // .NET â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "dotnet",
            condition: "ls *.csproj >/dev/null 2>&1 || ls *.sln >/dev/null 2>&1",
            port:      8080,
            logLabel:  ".NET",
            preamble:  "_ASSEMBLY=$(basename \"$(ls *.csproj 2>/dev/null | head -1)\" .csproj)",
            heredocFence: "DOCKERFILE_END",  // unquoted: shell expands $_ASSEMBLY inside
            dockerfile: new[]
            {
                "# Auto-generated .NET multi-stage Dockerfile",
                "FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build",
                "WORKDIR /src",
                "COPY *.csproj* *.sln* ./",
                "RUN dotnet restore || true",
                "COPY . .",
                "RUN dotnet publish -c Release -o /app/publish",
                "FROM mcr.microsoft.com/dotnet/aspnet:8.0",
                "WORKDIR /app",
                "COPY --from=build /app/publish .",
                "EXPOSE 8080",
                "ENV ASPNETCORE_URLS=http://+:8080 ASPNETCORE_ENVIRONMENT=Production",
                "ENTRYPOINT [\"sh\",\"-c\",\"dotnet ${_ASSEMBLY}.dll\"]",
            }),

        // Next.js â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "nextjs",
            condition: "[ -f package.json ] && grep -q '\"next\"' package.json 2>/dev/null",
            port:      3000,
            logLabel:  "Next.js",
            dockerfile: new[]
            {
                "# Auto-generated Next.js multi-stage Dockerfile",
                "FROM node:20-alpine AS deps",
                "RUN apk add --no-cache libc6-compat",
                "WORKDIR /app",
                "COPY package*.json yarn.lock* pnpm-lock.yaml* ./",
                "RUN if [ -f yarn.lock ]; then yarn --frozen-lockfile; elif [ -f pnpm-lock.yaml ]; then corepack enable pnpm && pnpm i --frozen-lockfile; else npm ci; fi",
                "FROM node:20-alpine AS builder",
                "WORKDIR /app",
                "COPY --from=deps /app/node_modules ./node_modules",
                "COPY . .",
                "ENV NEXT_TELEMETRY_DISABLED=1",
                "RUN npm run build",
                "FROM node:20-alpine AS runner",
                "WORKDIR /app",
                "ENV NODE_ENV=production NEXT_TELEMETRY_DISABLED=1 PORT=3000",
                "RUN addgroup --system --gid 1001 nodejs && adduser --system --uid 1001 nextjs",
                "COPY --from=builder /app/public ./public",
                "COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./",
                "COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static",
                "USER nextjs",
                "EXPOSE 3000",
                "CMD [\"node\",\"server.js\"]",
            }),

        // Nuxt 3 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "nuxt",
            condition: "[ -f package.json ] && grep -q '\"nuxt\"' package.json 2>/dev/null",
            port:      3000,
            logLabel:  "Nuxt 3",
            dockerfile: new[]
            {
                "FROM node:20-alpine AS builder",
                "WORKDIR /app", "COPY package*.json ./", "RUN npm ci",
                "COPY . .", "RUN npm run build",
                "FROM node:20-alpine", "WORKDIR /app",
                "COPY --from=builder /app/.output ./.output",
                "EXPOSE 3000",
                "ENV NUXT_HOST=0.0.0.0 NUXT_PORT=3000",
                "CMD [\"node\",\".output/server/index.mjs\"]",
            }),

        // NestJS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "nestjs",
            condition: "[ -f package.json ] && grep -q '@nestjs/core' package.json 2>/dev/null",
            port:      3000,
            logLabel:  "NestJS",
            dockerfile: new[]
            {
                "FROM node:20-alpine AS builder",
                "WORKDIR /app", "COPY package*.json ./", "RUN npm ci",
                "COPY . .", "RUN npm run build",
                "FROM node:20-alpine", "WORKDIR /app", "ENV NODE_ENV=production",
                "COPY --from=builder /app/dist ./dist",
                "COPY package*.json ./", "RUN npm ci --only=production",
                "EXPOSE 3000", "ENV PORT=3000", "CMD [\"node\",\"dist/main\"]",
            }),

        // Angular â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "angular",
            condition: "[ -f package.json ] && grep -q '\"@angular/core\"' package.json 2>/dev/null",
            port:      80,
            logLabel:  "Angular",
            dockerfile: new[]
            {
                "FROM node:20-alpine AS builder",
                "WORKDIR /app", "COPY package*.json ./", "RUN npm ci",
                "COPY . .", "RUN npm run build -- --configuration production",
                "FROM nginx:alpine",
                "COPY --from=builder /app/dist/browser /usr/share/nginx/html",
                "EXPOSE 80", "CMD [\"nginx\",\"-g\",\"daemon off;\"]",
            }),

        // React (CRA / Vite) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "react",
            condition: "[ -f package.json ] && grep -q '\"react\"' package.json 2>/dev/null && ! grep -q '@nestjs' package.json 2>/dev/null",
            port:      80,
            logLabel:  "React",
            dockerfile: new[]
            {
                "FROM node:20-alpine AS builder",
                "WORKDIR /app", "COPY package*.json ./", "RUN npm ci",
                "COPY . .", "RUN npm run build",
                "FROM nginx:alpine",
                "RUN mkdir -p /usr/share/nginx/html",
                "COPY --from=builder /app/build /usr/share/nginx/html 2>/dev/null || COPY --from=builder /app/dist /usr/share/nginx/html",
                "EXPOSE 80", "CMD [\"nginx\",\"-g\",\"daemon off;\"]",
            }),

        // Vite / Vue 3 / Svelte â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "vite",
            condition: "[ -f vite.config.ts ] || [ -f vite.config.js ]",
            port:      80,
            logLabel:  "Vite",
            dockerfile: new[]
            {
                "FROM node:20-alpine AS builder",
                "WORKDIR /app", "COPY package*.json ./", "RUN npm ci",
                "COPY . .", "RUN npm run build",
                "FROM nginx:alpine",
                "COPY --from=builder /app/dist /usr/share/nginx/html",
                "EXPOSE 80", "CMD [\"nginx\",\"-g\",\"daemon off;\"]",
            }),

        // Generic Node.js â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "node",
            condition: "[ -f package.json ]",
            port:      3000,
            logLabel:  "Node.js",
            dockerfile: new[]
            {
                "FROM node:20-alpine",
                "WORKDIR /app", "COPY package*.json ./",
                "RUN npm ci --only=production",
                "COPY . .",
                "EXPOSE 3000", "ENV PORT=3000 NODE_ENV=production",
                "CMD [\"sh\",\"-c\",\"npm start 2>/dev/null || node index.js 2>/dev/null || node server.js 2>/dev/null || node src/index.js\"]",
            }),

        // FastAPI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "fastapi",
            condition: "([ -f requirements.txt ] && grep -qi 'fastapi\\|uvicorn' requirements.txt 2>/dev/null) || ([ -f pyproject.toml ] && grep -qi 'fastapi' pyproject.toml 2>/dev/null)",
            port:      8000,
            logLabel:  "FastAPI",
            dockerfile: new[]
            {
                "FROM python:3.12-slim AS builder",
                "WORKDIR /app",
                "COPY requirements*.txt pyproject.toml* ./",
                "RUN pip install --no-cache-dir --upgrade pip && ([ -f requirements.txt ] && pip install --no-cache-dir -r requirements.txt || pip install --no-cache-dir .)",
                "FROM python:3.12-slim", "WORKDIR /app",
                "COPY --from=builder /usr/local/lib/python3.12/site-packages /usr/local/lib/python3.12/site-packages",
                "COPY --from=builder /usr/local/bin /usr/local/bin",
                "COPY . .",
                "EXPOSE 8000",
                "CMD [\"uvicorn\",\"main:app\",\"--host\",\"0.0.0.0\",\"--port\",\"8000\",\"--workers\",\"2\"]",
            }),

        // Django â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "django",
            condition: "[ -f requirements.txt ] && grep -qi 'django' requirements.txt 2>/dev/null",
            port:      8000,
            logLabel:  "Django",
            dockerfile: new[]
            {
                "FROM python:3.12-slim",
                "WORKDIR /app", "COPY requirements*.txt ./",
                "RUN pip install --no-cache-dir -r requirements.txt gunicorn",
                "COPY . .",
                "RUN python manage.py collectstatic --noinput 2>/dev/null || true",
                "EXPOSE 8000",
                "CMD [\"gunicorn\",\"wsgi:application\",\"--bind\",\"0.0.0.0:8000\",\"--workers\",\"2\",\"--timeout\",\"120\"]",
            }),

        // Flask â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "flask",
            condition: "[ -f requirements.txt ] && grep -qi 'flask' requirements.txt 2>/dev/null",
            port:      5000,
            logLabel:  "Flask",
            dockerfile: new[]
            {
                "FROM python:3.12-slim",
                "WORKDIR /app", "COPY requirements*.txt ./",
                "RUN pip install --no-cache-dir -r requirements.txt gunicorn",
                "COPY . .",
                "EXPOSE 5000", "ENV FLASK_ENV=production",
                "CMD [\"gunicorn\",\"app:app\",\"--bind\",\"0.0.0.0:5000\",\"--workers\",\"2\"]",
            }),

        // Generic Python â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "python",
            condition: "[ -f requirements.txt ] || [ -f pyproject.toml ] || [ -f setup.py ]",
            port:      8000,
            logLabel:  "Python",
            dockerfile: new[]
            {
                "FROM python:3.12-slim", "WORKDIR /app",
                "COPY requirements*.txt pyproject.toml* setup.py* ./",
                "RUN pip install --no-cache-dir -r requirements.txt 2>/dev/null || pip install --no-cache-dir . 2>/dev/null || true",
                "COPY . .",
                "EXPOSE 8000", "CMD [\"python\",\"main.py\"]",
            }),

        // Go â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "go",
            condition: "[ -f go.mod ]",
            port:      8080,
            logLabel:  "Go",
            dockerfile: new[]
            {
                "FROM golang:1.22-alpine AS builder",
                "RUN apk add --no-cache git ca-certificates tzdata",
                "WORKDIR /app", "COPY go.mod go.sum ./", "RUN go mod download",
                "COPY . .",
                "RUN CGO_ENABLED=0 GOOS=linux go build -ldflags='-w -s' -a -o app .",
                "FROM alpine:3.19",
                "RUN apk --no-cache add ca-certificates tzdata",
                "WORKDIR /root/", "COPY --from=builder /app/app .",
                "EXPOSE 8080", "CMD [\"./app\"]",
            }),

        // PHP / Laravel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "php",
            condition: "[ -f composer.json ]",
            port:      80,
            logLabel:  "PHP/Laravel",
            dockerfile: new[]
            {
                "FROM php:8.3-fpm-alpine AS deps",
                "RUN apk add --no-cache unzip git curl",
                "RUN docker-php-ext-install pdo pdo_mysql opcache",
                "COPY --from=composer:2 /usr/bin/composer /usr/bin/composer",
                "WORKDIR /var/www/html", "COPY composer*.json ./",
                "RUN composer install --no-dev --optimize-autoloader --no-interaction",
                "COPY . .",
                "RUN chown -R www-data:www-data storage bootstrap/cache 2>/dev/null || true",
                "FROM php:8.3-apache",
                "RUN docker-php-ext-install pdo pdo_mysql opcache",
                "COPY --from=deps /var/www/html /var/www/html",
                "RUN sed -i 's|/var/www/html|/var/www/html/public|g' /etc/apache2/sites-enabled/000-default.conf 2>/dev/null || true && a2enmod rewrite 2>/dev/null || true",
                "EXPOSE 80", "CMD [\"apache2-foreground\"]",
            }),

        // Ruby / Rails â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "ruby",
            condition: "[ -f Gemfile ]",
            port:      3000,
            logLabel:  "Ruby/Rails",
            dockerfile: new[]
            {
                "FROM ruby:3.3-slim AS builder",
                "RUN apt-get update && apt-get install -y build-essential libpq-dev nodejs curl && rm -rf /var/lib/apt/lists/*",
                "WORKDIR /app", "COPY Gemfile Gemfile.lock ./",
                "RUN bundle config set without 'development test' && bundle install --jobs 4",
                "COPY . .",
                "RUN bundle exec rails assets:precompile 2>/dev/null || true",
                "FROM ruby:3.3-slim",
                "RUN apt-get update && apt-get install -y libpq-dev && rm -rf /var/lib/apt/lists/*",
                "WORKDIR /app",
                "COPY --from=builder /usr/local/bundle /usr/local/bundle",
                "COPY --from=builder /app .",
                "EXPOSE 3000",
                "ENV RAILS_ENV=production RAILS_SERVE_STATIC_FILES=true",
                "CMD [\"bundle\",\"exec\",\"rails\",\"server\",\"-b\",\"0.0.0.0\",\"-p\",\"3000\"]",
            }),

        // Java / Spring Boot â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "java",
            condition: "[ -f pom.xml ] || [ -f build.gradle ] || [ -f build.gradle.kts ]",
            port:      8080,
            logLabel:  "Java/Spring",
            dockerfile: new[]
            {
                "FROM eclipse-temurin:21-jdk-alpine AS builder",
                "WORKDIR /app", "COPY . .",
                "RUN if [ -f mvnw ]; then chmod +x mvnw && ./mvnw package -DskipTests -q; elif [ -f pom.xml ]; then mvn package -DskipTests -q 2>/dev/null || true; elif [ -f gradlew ]; then chmod +x gradlew && ./gradlew bootJar -q; else gradle bootJar -q; fi",
                "FROM eclipse-temurin:21-jre-alpine",
                "WORKDIR /app",
                "RUN find /app/target -name '*.jar' 2>/dev/null | head -1 | xargs -I{} cp {} app.jar 2>/dev/null || find /app/build/libs -name '*.jar' 2>/dev/null | head -1 | xargs -I{} cp {} app.jar",
                "EXPOSE 8080", "ENV JAVA_OPTS=\"-Xmx512m -Xms128m\"",
                "CMD [\"sh\",\"-c\",\"java $JAVA_OPTS -jar app.jar\"]",
            }),

        // Static HTML â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "static",
            condition: "[ -f index.html ]",
            port:      80,
            logLabel:  "Static HTML",
            dockerfile: new[]
            {
                "FROM nginx:alpine",
                "COPY . /usr/share/nginx/html",
                "EXPOSE 80", "CMD [\"nginx\",\"-g\",\"daemon off;\"]",
            }),

        // Generic fallback â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        new(
            name:      "generic",
            condition: null,   // null = "else" branch â€” always matches
            port:      3000,
            logLabel:  "generic app",
            dockerfile: new[]
            {
                "FROM node:20-alpine", "WORKDIR /app", "COPY . .",
                "RUN [ -f package.json ] && npm install --production 2>/dev/null || true",
                "EXPOSE 3000", "ENV PORT=3000",
                "CMD [\"sh\",\"-c\",\"npm start 2>/dev/null || node index.js 2>/dev/null || node server.js 2>/dev/null || echo 'No start command found. Configure StartCommand in project settings.'\"]",
            }),
    };

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <inheritdoc />
    public string GenerateAutoDetectScript(Project project)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# â”€â”€ Auto-detect stack & generate Dockerfile â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€");
        sb.AppendLine("if [ ! -f Dockerfile ]; then");
        sb.AppendLine("  log \"ðŸ” No Dockerfile found â€” auto-detecting project type...\"");
        sb.AppendLine();

        bool first = true;
        foreach (var stack in Stacks)
        {
            if (stack.condition is null)
            {
                sb.AppendLine("  else");
            }
            else
            {
                string kw = first ? "if" : "elif";
                sb.AppendLine($"  {kw} {stack.condition}; then");
            }

            sb.AppendLine($"    log \"ðŸ” {stack.logLabel} project detected\"");
            sb.AppendLine($"    PORT={stack.port}");

            if (!string.IsNullOrWhiteSpace(stack.preamble))
                sb.AppendLine($"    {stack.preamble}");

            // Write Dockerfile via heredoc.
            sb.AppendLine($"    cat > Dockerfile << {stack.heredocFence}");
            foreach (var line in stack.dockerfile)
                sb.AppendLine(line);
            // Closing marker â€” strip surrounding single-quotes from fence if quoted.
            sb.AppendLine(stack.heredocFence.Trim('\''));
            sb.AppendLine();

            if (stack.condition is null)
                sb.AppendLine("  fi"); // closes the entire if/elif/else chain

            first = false;
        }

        sb.AppendLine("  log \"âœ… Dockerfile generated\"");
        sb.AppendLine("fi");
        sb.AppendLine();

        if (project.Port.HasValue)
        {
            sb.AppendLine($"PORT={project.Port.Value}  # project port override");
            sb.AppendLine();
        }

        // Build with BuildKit inline cache for layer re-use on successive runs.
        sb.AppendLine("log \"ðŸ³ Building Docker image: $IMAGE_TAG (BuildKit + layer cache)...\"");
        sb.AppendLine("DOCKER_BUILDKIT=1 docker build \\");
        sb.AppendLine("  --cache-from \"$IMAGE_TAG\" \\");
        sb.AppendLine("  --build-arg BUILDKIT_INLINE_CACHE=1 \\");
        sb.AppendLine("  -t \"$IMAGE_TAG\" \\");
        sb.AppendLine("  . 2>&1 | tail -40 || fail \"Docker build failed â€” see logs above\"");
        sb.AppendLine("log \"âœ… Docker image built\"");

        return sb.ToString();
    }
}

/// <summary>
/// Immutable descriptor for one tech-stack auto-detection + Dockerfile template.
/// </summary>
internal sealed record StackDefinition(
    string   name,
    string?  condition,          // bash conditional; null = else/fallback
    int      port,
    string[] dockerfile,
    string   logLabel,
    string?  preamble     = null,
    string   heredocFence = "'DOCKERFILE_END'");

