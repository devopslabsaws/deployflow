# DeployFlow — Complete User & Developer Documentation

> **Version:** Sprint 10 (March 2026) · **Stack:** ASP.NET Core 8 + Next.js 14 + Oracle 21c  
> **URLs (dev):** Backend `http://localhost:5000` · Frontend `http://localhost:3001`

---

## Table of Contents

1. [Introduction & Architecture](#1-introduction--architecture)
2. [Getting Started (Developer Setup)](#2-getting-started-developer-setup)
3. [Authentication & User Management](#3-authentication--user-management)
4. [Projects](#4-projects)
5. [Deployments](#5-deployments)
6. [Services](#6-services)
7. [Servers & Clusters](#7-servers--clusters)
8. [Databases](#8-databases)
9. [Pipelines](#9-pipelines)
10. [Monitoring & Alerts](#10-monitoring--alerts)
11. [Logs](#11-logs)
12. [Domains & SSL](#12-domains--ssl)
13. [Volumes](#13-volumes)
14. [Containers](#14-containers)
15. [Docker Compose Stacks](#15-docker-compose-stacks)
16. [Templates Gallery](#16-templates-gallery)
17. [Preview Environments](#17-preview-environments)
18. [Team Management & RBAC](#18-team-management--rbac)
19. [Security (2FA, SSH Keys, Audit Logs)](#19-security)
20. [Settings & Integrations](#20-settings--integrations)
21. [Cost Monitor](#21-cost-monitor)
22. [AI Assistant](#22-ai-assistant)
23. [EF Migrations Reference](#23-ef-migrations-reference)
24. [API Reference Summary](#24-api-reference-summary)

---

## 1. Introduction & Architecture

DeployFlow is a **self-hosted PaaS** (Platform as a Service) — similar to Coolify/Dokploy — that lets teams deploy applications from Git repositories to Linux servers via SSH, Docker, and Docker Compose.

### System Architecture

```
┌─────────────────────────────────────────────────┐
│                  Browser / Next.js 14            │
│   React Query ←→ REST API + SignalR WebSocket    │
└─────────────────────┬───────────────────────────┘
                      │ HTTP / WebSocket
┌─────────────────────▼───────────────────────────┐
│          ASP.NET Core 8 API (:5000)              │
│  ┌──────────────────────────────────────────┐   │
│  │  MediatR CQRS (Commands + Queries)       │   │
│  │  JWT Auth + Identity                     │   │
│  │  SignalR Hub (/hubs/logs)                │   │
│  │  Background Services:                    │   │
│  │    DeploymentRunnerService (poll 5s)     │   │
│  │    PipelineRunnerService                 │   │
│  │    ScheduledTaskRunnerService            │   │
│  └──────────────────────────────────────────┘   │
│  ┌────────────────┐ ┌──────────────────────┐   │
│  │  Oracle 21c EF │ │  SSH.NET → Linux VMs │   │
│  │  (43 tables)   │ │  (Docker commands)   │   │
│  └────────────────┘ └──────────────────────┘   │
└─────────────────────────────────────────────────┘
```

### Key Technologies

| Layer | Technology |
|-------|-----------|
| Frontend | Next.js 14, React 18, TailwindCSS, shadcn/ui, Framer Motion |
| State | Zustand (auth), TanStack Query v5 (server state) |
| Backend | ASP.NET Core 8, MediatR, AutoMapper, FluentValidation |
| Database | Oracle 21c via EF Core 8 (Oracle.EntityFrameworkCore) |
| Auth | ASP.NET Identity + JWT (access 60m, refresh 7d) |
| Real-time | SignalR WebSockets + SSE fallback |
| SSH | SSH.NET (Renci.SshNet) with streaming support |
| Email | SMTP via MailKit |

---

## 2. Getting Started (Developer Setup)

### Prerequisites

- .NET 9 SDK
- Node.js 20+
- Oracle 21c Express (or container)
- Docker Desktop

### Backend Setup

```bash
# 1. Restore packages
cd backend
dotnet restore DeployFlow.sln

# 2. Configure Oracle connection
# Edit backend/src/DeployFlow.API/appsettings.Development.json:
{
  "ConnectionStrings": {
    "DefaultConnection": "User Id=deployflow;Password=yourpass;Data Source=localhost:1521/XEPDB1"
  },
  "Jwt": { "Key": "your-32-char-secret-key", "Issuer": "DeployFlow" },
  "Encryption": { "Key": "your-32-char-encryption-key" }
}

# 3. Apply migrations (creates 43 tables)
cd backend/src/DeployFlow.API
dotnet ef database update

# 4. Run the API
dotnet run --urls http://localhost:5000
```

### Frontend Setup

```bash
cd frontend
npm install

# Create .env.local
echo "NEXT_PUBLIC_API_URL=http://localhost:5000" > .env.local

# Development (hot reload)
npm run dev -- --port 3001

# Production build
npm run build && npm start
```

### Default Admin Credentials

```
Email:    admin@deployflow.dev
Password: Admin123!
```

### Database Seeder

On first run, the API seeds:
- 1 default tenant (`default-tenant`)
- 1 admin user
- Sample app templates (WordPress, Ghost, Postgres, Redis, Grafana, etc.)
- 3 project environments (Development, Staging, Production)

---

## 3. Authentication & User Management

### Login Flow

```
POST /api/auth/login
Body: { "email": "admin@deployflow.dev", "password": "Admin123!" }

Response:
{
  "accessToken": "eyJ...",     ← JWT (60 min TTL)
  "refreshToken": "abc123",    ← Refresh (7 days)
  "expiresIn": 3600,
  "user": { "id": "...", "name": "Admin", "email": "...", "role": "Admin", "avatarUrl": null }
}
```

### Token Refresh

```
POST /api/auth/refresh
Body: { "refreshToken": "abc123" }
→ Returns new accessToken + refreshToken pair
```

### Registration

```
POST /api/auth/register
Body: { "fullName": "John Doe", "email": "john@example.com", "password": "Secure123!" }
```

### Profile Update

```
PUT /api/auth/profile
Body: { "fullName": "John Doe Updated" }

# Avatar upload (base64 CLOB stored in Oracle)
PUT /api/auth/avatar
Body: { "avatarBase64": "data:image/png;base64,..." }
```

### Two-Factor Authentication (2FA)

Navigate to **Settings → Security → Two-Factor Auth**:

1. Click **Enable 2FA** → QR code displayed
2. Scan with Google Authenticator / Authy
3. Enter 6-digit TOTP code to confirm
4. 2FA is now required on every login

**Sample TOTP endpoint:**
```
POST /api/auth/2fa/setup     → { qrCodeUri, manualEntryKey }
POST /api/auth/2fa/confirm   { "code": "123456" }
POST /api/auth/2fa/disable   { "code": "654321" }
```

---

## 4. Projects

Projects represent **applications** deployed from Git repositories.

### Create a Project

**UI:** Projects → New Project button

**Sample data:**
```json
{
  "name": "My Next.js App",
  "description": "E-commerce frontend",
  "repositoryUrl": "https://github.com/acme/nextjs-shop",
  "repositoryBranch": "main",
  "serverId": "srv-uuid-here",
  "framework": "nextjs",
  "port": 3000,
  "buildCommand": "npm run build",
  "installCommand": "npm ci",
  "startCommand": "npm start",
  "autoDeploy": true
}
```

**API:**
```
POST /api/projects
GET  /api/projects?page=1&pageSize=20&status=active&search=shop
GET  /api/projects/{id}
PUT  /api/projects/{id}
DELETE /api/projects/{id}
```

### Project Auto-Detection

DeployFlow auto-detects the stack (no Dockerfile needed):

| Detected Stack | Indicator File | Generated Dockerfile |
|----------------|---------------|---------------------|
| Node.js | `package.json` | Node 20 Alpine, `npm ci && npm run build` |
| Python | `requirements.txt` / `Pipfile` | Python 3.12 slim |
| Go | `go.mod` | Go 1.22 Alpine, multi-stage |
| PHP | `composer.json` | PHP 8.3 FPM + Nginx |
| Ruby | `Gemfile` | Ruby 3.3 Alpine |
| Java | `pom.xml` / `build.gradle` | OpenJDK 21 slim |
| Static | `index.html` | Nginx Alpine |

### Project Detail Page

`/projects/{id}` shows:
- **Stats row**: Last Status, Branch, Total Deployments, Last Deployed
- **Live app URL banner** (green) when latest deployment succeeded
- **Project Details card**: Repository, branch, framework, custom domain, server
- **Deployments list**: last 15 deployments with status, commit SHA, relative time, Open App links

### Deploy Now

Click **Deploy Now** → triggers build via `POST /api/projects/{id}/build` → automatically navigates to `/deployments/{newId}` showing live streaming logs.

### Project Settings

`/projects/{id}/settings`:
- General (name, description, status)
- Build (commands, Dockerfile path, output directory)
- Health Check (path, timeout)
- Tags

### Env Variables

`/projects/{id}/env-variables`:
```
POST /api/projects/{id}/env-variables
Body: { "key": "DATABASE_URL", "value": "postgres://...", "type": "Secret" }
```
Types: `PlainText` | `Secret` | `File`

### Scheduled Tasks

`/projects/{id}/scheduled-tasks` — run commands on a schedule inside the deployed container:
```json
{
  "name": "DB Cleanup",
  "command": "node scripts/cleanup.js",
  "frequency": "0 3 * * *",
  "containerName": "myapp",
  "timeoutSeconds": 300
}
```

### Webhooks

`/projects/{id}/webhooks` — receive push events from GitHub/GitLab/Bitbucket to trigger auto-deploys:
```
GET /api/projects/{id}/webhooks    → { token, gitHubSecret, gitLabSecret, ... }

# GitHub webhook URL example:
https://api.deployflow.dev/api/webhooks/github/{token}
```

---

## 5. Deployments

### Deployment States

```
Queued → Building → Deploying → Healthy
                             ↘ Failed
                  → Cancelled
```

### Trigger a Deployment

```bash
# Via API
POST /api/deployments
{
  "projectId": "proj-uuid",
  "branch": "main",
  "trigger": "manual"
}
# Returns: { "id": "dep-uuid", "status": "queued" }

# Via project build endpoint (also navigates to logs)
POST /api/projects/{id}/build
# Returns: { "deploymentId": "dep-uuid", "message": "Build queued" }
```

### Live Log Streaming

After clicking Deploy, the UI auto-navigates to `/deployments/{id}` → **Logs tab**.

The live log shows **6 phases**:
```
━━━ Step 1/6: Docker Check ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Docker already installed: Docker version 24.0.7

━━━ Step 2/6: Fetching Source Code (branch: main) ━━━━━━━━━━
📥 Cloning repository (shallow clone, 1 commit)...
✅ Source ready — commit: a1b2c3d | feat: add product page

━━━ Step 3/6: Detect Stack & Build Docker Image ━━━━━━━━━━━━
🔍 Detected: Node.js (package.json found)
📦 Generating Dockerfile for Node.js 20...
🐳 Building Docker image (layer caching enabled)...

━━━ Step 4/6: Rolling Replace ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🛑 Stopping previous deployment (if any)...

━━━ Step 5/6: Starting Container ━━━━━━━━━━━━━━━━━━━━━━━━━━
▶️  Starting container 'my-nextjs-app' on port 3000...

━━━ Step 6/6: Health Check ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🏥 Waiting for container to become healthy (up to 90s)...
✅ Container is running — image: deployflow/my-nextjs-app:latest, port: 3000
🔗 App URL: http://192.168.1.100:3000
```

### Smart Auto-Fix Engine

If a build fails, DeployFlow's SmartFixEngine:
1. Analyzes the error (OOM, missing deps, permission denied, etc.)
2. Prepends a fix script
3. Retries up to **3 times** with 30s delay

### Deployment Detail Page

`/deployments/{id}`:
- **Overview tab**: 4 stat cards (Status, Branch, Duration, Trigger) + green **Open App** URL banner
- **Logs tab**: Live SignalR-connected terminal with auto-scroll
- **Approval tab** (when `approvalStatus = "Pending"`): Approve / Reject with notes
- **AI Debug tab**: Automated error analysis + chat interface
- **Canary tab** (when status = "healthy"): Configure and manage canary releases

### Deployment Approval Workflow

```
Project setting: requiresApproval = true
→ Deployment created with approvalStatus = "Pending"
→ Notified approvers see pulsing "Approval" tab
→ Approver clicks Approve/Reject with optional notes
→ If Approved: deployment proceeds to build
→ If Rejected: deployment is cancelled
```

**API:**
```
POST /api/deployments/{id}/approve  { "notes": "Tested in staging" }
POST /api/deployments/{id}/reject   { "notes": "Needs more testing" }
```

### Canary Deployments

```
POST /api/deployments/{id}/canary/start
{ "trafficPercent": 10, "stepDurationMinutes": 5 }

# Gradually routes 10% → 25% → 50% → 100%
POST /api/deployments/{id}/canary/promote  → 100%
POST /api/deployments/{id}/canary/abort    → 0% (rollback)
```

### Rollback

```
POST /api/deployments/{deploymentId}/rollback
→ Creates a new deployment with isRollback=true from the previous image tag
```

---

## 6. Services

Services are **long-running Docker containers** within a project.

### Create a Service

```json
POST /api/services
{
  "projectId": "proj-uuid",
  "name": "api-service",
  "type": "web",
  "image": "nginx:alpine",
  "tag": "latest",
  "replicas": 2,
  "cpuLimit": "500m",
  "memoryLimit": "512Mi",
  "healthCheckPath": "/health",
  "healthCheckInterval": 30
}
```

### Service Types

| Type | Description |
|------|-------------|
| `web` | HTTP-serving container (gets domain routing) |
| `worker` | Background job processor |
| `cron` | Scheduled task container |
| `database` | Managed DB service |

### Autoscaling

```json
PUT /api/services/{id}/autoscale
{
  "minReplicas": 1,
  "maxReplicas": 10,
  "cpuTargetPercentage": 70,
  "memoryTargetPercentage": 80
}
```

### Service Actions

```
POST /api/services/{id}/start
POST /api/services/{id}/stop
POST /api/services/{id}/restart
```

---

## 7. Servers & Clusters

### Add a Server

**UI:** Servers → Add Server

```json
POST /api/servers
{
  "name": "prod-web-01",
  "ipAddress": "192.168.1.100",
  "sshPort": 22,
  "sshUser": "ubuntu",
  "sshKeyId": "key-uuid",
  "provider": "hetzner",
  "region": "fsn1",
  "cpuCount": 4,
  "memoryGb": 8,
  "diskGb": 80
}
```

### Server Health

The API polls server health every 60 seconds, collecting:
- CPU/Memory/Disk usage
- Active containers count
- Docker version
- Load average

```
GET /api/servers/{id}/health
→ { "status": "online", "cpuUsagePercent": 45.2, "memoryUsagePercent": 62.1, "diskUsagePercent": 38, "dockerVersion": "24.0.7" }
```

### Server Terminal

`/servers/{id}/terminal` — Live browser-based SSH terminal.

```
GET /api/servers/{id}/terminal/ws  ← WebSocket terminal proxy
```

### Server Provisioning Jobs

Provision new cloud servers from within DeployFlow:

```json
POST /api/servers/provision
{
  "name": "new-hetzner-server",
  "provider": "hetzner",
  "region": "fsn1",
  "size": "cx21",
  "os": "ubuntu-22.04",
  "sshKeyId": "key-uuid"
}
```

Status progression: `Pending → Planning → Applying → Completed`

### Clusters

Group multiple servers for load distribution:

```json
POST /api/clusters
{
  "name": "prod-cluster",
  "description": "Production web cluster",
  "strategy": "LeastLoaded",
  "nodeIds": ["srv-uuid-1", "srv-uuid-2", "srv-uuid-3"]
}
```

Strategies: `RoundRobin` | `LeastLoaded` | `Replicated`

---

## 8. Databases

### Supported Engines

| Engine | Default Port |
|--------|-------------|
| PostgreSQL | 5432 |
| MySQL | 3306 |
| MariaDB | 3306 |
| MongoDB | 27017 |
| Redis | 6379 |
| Microsoft SQL Server | 1433 |
| Oracle | 1521 |

### Create Database

```json
POST /api/databases
{
  "name": "prod-postgres",
  "engine": "PostgreSQL",
  "version": "16",
  "serverId": "srv-uuid",
  "databaseName": "myapp_prod",
  "username": "appuser",
  "password": "SecurePass123!",
  "storageGb": 20,
  "backupEnabled": true,
  "backupSchedule": "0 2 * * *",
  "backupRetentionDays": 7
}
```

### Backup Policy

```json
POST /api/databases/{id}/backup-policy
{
  "isEnabled": true,
  "cronExpression": "0 2 * * *",
  "retentionDays": 30,
  "s3DestinationId": "s3-dest-uuid"
}
```

### S3 Destinations (for backups)

```json
POST /api/s3-destinations
{
  "name": "AWS S3 Backups",
  "endpoint": "https://s3.amazonaws.com",
  "bucketName": "myapp-backups",
  "accessKeyId": "AKIAIOSFODNN7EXAMPLE",
  "secretAccessKey": "wJalrXUtnFEMI...",
  "region": "us-east-1"
}
```

### Restore a Backup

```json
POST /api/databases/{id}/restore
{
  "backupId": "bkp-uuid",
  "targetDatabaseName": "myapp_restore_test"
}
```

---

## 9. Pipelines

Pipelines are **multi-stage CI/CD workflows** with parallel execution support.

### Create Pipeline

```json
POST /api/pipelines
{
  "projectId": "proj-uuid",
  "name": "Full CI/CD Pipeline",
  "description": "Test → Build → Deploy to staging → Deploy to prod",
  "trigger": "OnPush",
  "cronExpression": null,
  "stages": [
    {
      "name": "Test",
      "order": 1,
      "runParallel": false,
      "steps": [
        { "name": "Install deps", "type": "Script", "command": "npm ci" },
        { "name": "Unit tests",   "type": "Script", "command": "npm test" },
        { "name": "Lint",         "type": "Script", "command": "npm run lint" }
      ]
    },
    {
      "name": "Build & Push",
      "order": 2,
      "runParallel": false,
      "steps": [
        { "name": "Docker build", "type": "DockerBuild", "image": "myapp:${COMMIT_SHA}" }
      ]
    },
    {
      "name": "Deploy Staging",
      "order": 3,
      "steps": [
        { "name": "Deploy", "type": "Deploy", "configJson": "{\"environment\":\"staging\"}" }
      ]
    }
  ]
}
```

### Pipeline Triggers

| Trigger | Description |
|---------|-------------|
| `OnPush` | Any git push |
| `OnTag` | Git tag created |
| `Manual` | Triggered manually |
| `Scheduled` | Cron expression |

### Pipeline Run History

```
GET /api/pipelines/{id}/runs?page=1&pageSize=20
→ [{ id, status, startedAt, completedAt, stageCount, stepCount, triggeredBy }]

GET /api/pipelines/{id}/runs/{runId}/logs
→ [{ timestamp, level, stageName, stepName, message }]
```

---

## 10. Monitoring & Alerts

### Server Metrics

Real-time metrics are collected every 60 seconds per server:

```
GET /api/servers/{id}/metrics?range=24h
→ [{
    timestamp: "2026-03-30T10:00:00Z",
    cpuUsagePercent: 42.3,
    memoryUsageBytes: 4294967296,
    memoryTotalBytes: 8589934592,
    networkRxBytes: 123456,
    networkTxBytes: 456789,
    activeContainers: 12,
    loadAverage1m: 1.23
  }]
```

### Alert Rules

Define threshold-based alerts:

```json
POST /api/alert-rules
{
  "name": "High CPU Alert",
  "metric": "cpu",
  "operator": "gt",
  "threshold": 85,
  "windowMinutes": 5,
  "severity": "critical",
  "cooldownMinutes": 15,
  "description": "Alert when CPU > 85% for 5 minutes"
}
```

**Metrics:** `cpu` | `memory` | `disk` | `network_rx` | `network_tx` | `containers`

**Operators:** `gt` | `lt` | `gte` | `lte` | `eq`

**Severities:** `info` | `warning` | `critical`

### Acknowledge Alert

```
POST /api/alerts/{id}/acknowledge
→ Sets status = "Acknowledged", records acknowledgedBy/At
```

### Recovery Rules

Auto-remediate incidents:

```json
POST /api/recovery-rules
{
  "name": "Auto-restart on crash",
  "trigger": "ContainerCrash",
  "action": "RestartContainer",
  "maxRetries": 3,
  "cooldownSeconds": 60,
  "targetProjectId": "proj-uuid"
}
```

**Triggers:** `ContainerCrash` | `ServerOffline` | `DeploymentFailed` | `HealthCheckFailed`

**Actions:** `RestartContainer` | `RedeployLastGood` | `AlertOnly` | `ScaleUp`

---

## 11. Logs

### Session Log Viewer (`/logs`)

- **Real-time SSE stream**: connects to `GET /api/logs/stream?access_token={jwt}`
- **Initial load**: last 200 log lines fetched via REST
- **Level filter**: All / Info / Warning / Error / Debug
- **"Load older"** button for pagination (cursor-based)
- **Auto-scroll** toggle

### Deployment Logs

```
GET /api/logs?deploymentId={id}&page=1&pageSize=200
→ [{ id, deploymentId, message, level, stream, timestamp }]
```

### Log Levels & Colors

| Level | Color | Icon |
|-------|-------|------|
| `info` | Gray | ℹ️ |
| `warn` | Amber | ⚠️ |
| `error` | Red | ❌ |
| `debug` | Blue | 🔍 |
| `stdout` | Green | — |
| `stderr` | Orange | — |

---

## 12. Domains & SSL

### Add Domain

```json
POST /api/domains
{
  "name": "app.example.com",
  "serviceId": "svc-uuid",
  "sslEnabled": true,
  "sslProvider": "letsencrypt",
  "redirectWww": true
}
```

Domain statuses: `Pending` → `Active` | `Error` | `Expired`

### Traefik Router (advanced)

```json
POST /api/domains/{id}/traefik-router
{
  "rule": "Host(`app.example.com`) && PathPrefix(`/api`)",
  "entrypoints": "websecure",
  "tlsEnabled": true,
  "certResolver": "letsencrypt",
  "priority": 10
}
```

### DNS Verification

```
POST /api/domains/{id}/verify
→ Checks DNS A/CNAME record points to server IP
```

---

## 13. Volumes

Docker named volumes for persistent storage:

```json
POST /api/volumes
{
  "name": "postgres-data",
  "serverId": "srv-uuid",
  "mountPath": "/var/lib/postgresql/data",
  "driver": "local"
}
```

Volume statuses: `Active` | `Inactive` | `Error`

```
GET  /api/volumes
DELETE /api/volumes/{id}  ← soft delete; actual Docker volume persists until pruned
```

---

## 14. Containers

Live view of running Docker containers across all servers:

```
GET /api/containers?serverId={id}
→ [{
    containerId: "sha256abc...",
    name: "myapp",
    image: "deployflow/myapp:latest",
    status: "running",
    cpuPercent: 12.4,
    memoryBytes: 268435456,
    ports: "[{\"host\":3000,\"container\":3000}]",
    startedAt: "2026-03-30T08:00:00Z"
  }]

POST /api/containers/{id}/stop
POST /api/containers/{id}/restart
DELETE /api/containers/{id}     ← remove container
```

---

## 15. Docker Compose Stacks

Deploy multi-service applications using Docker Compose:

### Create Stack

**UI:** Compose → New Stack

```json
POST /api/compose
{
  "name": "WordPress Stack",
  "serverId": "srv-uuid",
  "composeYaml": "version: '3.8'\nservices:\n  wordpress:\n    image: wordpress:latest\n    ports:\n      - '8080:80'\n    environment:\n      WORDPRESS_DB_HOST: db\n  db:\n    image: mysql:8\n    environment:\n      MYSQL_ROOT_PASSWORD: secret",
  "environmentName": "production"
}
```

### Stack Actions

```
POST /api/compose/{id}/deploy   ← docker compose up -d
POST /api/compose/{id}/stop     ← docker compose down
POST /api/compose/{id}/restart  ← docker compose restart
GET  /api/compose/{id}/logs     ← live YAML compose logs
```

Stack statuses: `Stopped` | `Starting` | `Running` | `Failed` | `Removing`

---

## 16. Templates Gallery

One-click deploy from a curated library of 20+ pre-configured apps.

### Available Templates (sample)

| Template | Image | Category | Default Port |
|----------|-------|----------|-------------|
| WordPress | wordpress:latest | CMS | 8080 |
| Ghost | ghost:latest | CMS | 2368 |
| PostgreSQL | postgres:16 | Database | 5432 |
| MySQL | mysql:8 | Database | 3306 |
| Redis | redis:7-alpine | Database | 6379 |
| Grafana | grafana/grafana | Monitoring | 3000 |
| Prometheus | prom/prometheus | Monitoring | 9090 |
| MinIO | minio/minio | Storage | 9000 |
| Gitea | gitea/gitea | DevTools | 3000 |
| Nextcloud | nextcloud:latest | Productivity | 8080 |
| Plausible | plausible/analytics | Analytics | 8000 |
| Uptime Kuma | louislam/uptime-kuma | Monitoring | 3001 |

### Deploy from Template

```
POST /api/templates/{slug}/deploy
{
  "projectId": "proj-uuid",
  "serverId": "srv-uuid",
  "envOverrides": {
    "WORDPRESS_DB_PASSWORD": "mysecretpass"
  }
}
```

---

## 17. Preview Environments

Automatically create ephemeral environments for pull requests:

### How it works

1. PR opened on GitHub/GitLab → webhook arrives at `/api/webhooks/github/{token}`
2. DeployFlow creates a `PreviewEnvironment` record
3. A new deployment is triggered on a dedicated subdomain: `pr-42.app.yourdomain.com`
4. PR merged/closed → environment automatically spun down

### Preview Environment API

```
GET  /api/preview-environments?projectId={id}
→ [{ id, projectId, prNumber, prTitle, branch, url, status, deploymentId }]

DELETE /api/preview-environments/{id}  ← manual teardown
```

### Statuses

`Active` | `Deploying` | `Stopped` | `Error`

---

## 18. Team Management & RBAC

### Team Roles

| Role | Can Do |
|------|--------|
| `Admin` | All operations including user management |
| `Developer` | Deploy, create projects, view logs |
| `Viewer` | Read-only access to all resources |
| `Ops` | Deploy + server management, no user management |

### Invite Team Member

**UI:** Team → Invite Member

```json
POST /api/team/invite
{
  "email": "developer@example.com",
  "name": "Jane Developer",
  "role": "Developer"
}
```

→ Email sent with invite link: `https://app.deployflow.dev/invite/{token}`

### Resend / Revoke Invitations

```
POST /api/team/invitations/{id}/resend
DELETE /api/team/invitations/{id}
```

### Resource-Level Permissions

Grant fine-grained access per resource:

```json
POST /api/resource-permissions
{
  "userId": "user-uuid",
  "resourceType": "Project",
  "resourceId": "proj-uuid",
  "actions": ["Read", "Deploy"]
}
```

**Actions (flags):** `Read` | `Deploy` | `Configure` | `Delete`

---

## 19. Security

### SSH Keys

```json
POST /api/ssh-keys
{
  "name": "prod-server-key",
  "publicKey": "ssh-rsa AAAAB3...",
  "privateKeyEncrypted": "..."  ← encrypted with AES-256
}
```

Keys are **encrypted at rest** using `IEncryptionService` (AES-256). The plaintext private key is only decrypted in memory during SSH session establishment.

### Audit Logs

All sensitive actions are logged:

```
GET /api/audit-logs?page=1&pageSize=50&userId=...&action=Deploy
→ [{
    userId, userName, action, resourceType, resourceId,
    ipAddress, userAgent, createdAt, metadataJson
  }]
```

**Logged actions:** Login, Logout, Deploy, Delete, SettingsChanged, UserInvited, PermissionChanged, SecretAccessed

### Outbound Webhooks Security

All outbound webhook deliveries include an HMAC-SHA256 signature:

```
Header: X-DeployFlow-Signature: sha256=abc123...
```

Consumers should verify: `HMAC-SHA256(secret, request_body)`

---

## 20. Settings & Integrations

### Profile Settings

`/settings → Profile tab`:
- Update display name
- Upload avatar (cropper dialog, stored as CLOB in Oracle)
- Change password

### Notification Channels

`/settings → Notifications tab`:

| Channel | Connect via |
|---------|-------------|
| Docker Hub | Username + Access Token |
| Slack | Incoming Webhook URL |
| Microsoft Teams | Webhook URL |
| GitHub | Personal Access Token |
| GitLab | PAT |
| AWS | Access Key ID + Secret |
| Grafana | API Token + Grafana URL |
| Cloudflare | API Token + Zone ID |

### Connect an Integration (example: Slack)

```json
POST /api/integrations/slack
{ "webhookUrl": "https://hooks.slack.com/services/T00/B00/xxx" }

GET /api/integrations/slack
→ { "isConnected": true, "username": "deployflow-chan", "connectedAt": "2026-03-01T..." }

DELETE /api/integrations/slack

POST /api/integrations/slack/test
→ Sends "🚀 DeployFlow test notification" to Slack
```

### SMTP Settings

```json
PUT /api/settings/smtp
{
  "host": "smtp.gmail.com",
  "port": 587,
  "username": "alerts@company.com",
  "password": "app-password",
  "fromAddress": "deployflow@company.com",
  "enableSsl": true
}
```

### SSO (OIDC)

```json
PUT /api/settings/sso
{
  "ssoEnabled": true,
  "ssoIssuer": "https://auth.company.com",
  "ssoClientId": "deployflow-client",
  "ssoClientSecret": "secret123"
}
```

---

## 21. Cost Monitor

Tracks resource costs per project/service/database:

```
GET /api/costs?period=2026-03&resourceType=compute
→ [{
    resourceType: "compute",
    resourceName: "prod-web-01",
    amount: 42.50,
    currency: "USD",
    period: "2026-03",
    recordedAt: "2026-03-30T..."
  }]
```

**Resource types:** `compute` | `memory` | `storage` | `database` | `container`

**Sample monthly breakdown:**

| Resource | Type | Cost |
|----------|------|------|
| prod-web-01 | compute | $42.50 |
| postgres-prod | database | $18.00 |
| s3-backups | storage | $3.20 |
| **Total** | | **$63.70** |

---

## 22. AI Assistant

### AI Debug (per deployment)

Navigate to `/deployments/{id} → AI Debug tab`:

1. Click **Analyze Deployment** → GPT-4o analyzes deployment logs
2. Response includes:
   - **Severity**: 🟢 ok | 🟡 medium | 🟠 high | 🔴 critical
   - **Diagnosis**: Human-readable root cause
   - **Root Cause**: Technical explanation
   - **Suggestions**: Actionable fix steps
   - **Quick Actions**: One-click auto-fixes

### AI Chat

```json
POST /api/ai/chat
{
  "message": "Why did my Node.js deployment fail with ENOMEM?",
  "deploymentId": "dep-uuid"
}
→ { "reply": "The build ran out of memory. Your server has 512MB RAM but Node.js build requires ~1GB..." }
```

### AI Analysis

```
GET /api/ai/analyze/{deploymentId}
→ {
    severity: "high",
    diagnosis: "npm install failed due to insufficient disk space",
    rootCause: "Server disk is 94% full (only 800MB free)",
    suggestions: [
      { title: "Clear Docker build cache", detail: "Run: docker builder prune -f", category: "System" }
    ],
    autoFixes: [
      { id: "prune-cache", label: "Clean Docker Cache", destructive: false }
    ]
  }
```

---

## 23. EF Migrations Reference

### Complete Migration History

| # | Migration | Date | What it adds |
|---|-----------|------|-------------|
| 1 | `InitialOracleSchema` | 2026-03-20 | All core tables: projects, deployments, servers, services, databases, pipelines, alerts, domains, volumes, etc. (28 tables) |
| 2 | `AddSsoConfig` | 2026-03-20 | SSO columns on tenants table |
| 3 | `AddCostRecordCategory` | 2026-03-21 | Category column on cost_records |
| 4 | `AddServiceAutoscalingAndBlueGreen` | 2026-03-23 | Autoscaling cols on services, backup_policies, restore_jobs, s3_destinations, pipeline_runs/logs, volumes, clusters, resource_permissions, scheduled_tasks |
| 5 | `AddTeamInvitationsAndExpandedPermissions` | 2026-03-23 | team_invitations table |
| 6 | `AddAlertRules` | 2026-03-23 | alert_rules table |
| 7 | `AddDeploymentApprovalAndCanary` | 2026-03-23 | Approval + canary columns on deployments |
| 8 | `AddSprint910Tables` | 2026-03-24 | app_templates, compose_stacks, outbound_webhook_configs, PreviewEnvironments, ProjectEnvironments, provisioning_jobs, recovery_rules, traefik_routers |
| 9 | `AvatarUrlClob` | 2026-03-29 | AvatarUrl column changed from NVARCHAR2(1000) to CLOB |

### EF Audit: Entity → Migration Coverage

| Entity | Tracked by EF | Migration | Notes |
|--------|--------------|-----------|-------|
| Tenant | ✅ | InitialOracleSchema | |
| ApplicationUser | ✅ | InitialOracleSchema + AvatarUrlClob | AvatarUrl is CLOB |
| Project | ✅ | InitialOracleSchema | |
| Deployment + DeploymentLog | ✅ | InitialOracleSchema + ApprovalAndCanary | |
| Server + ServerMetrics | ✅ | InitialOracleSchema | |
| Service | ✅ | InitialOracleSchema + Autoscaling | |
| DatabaseInstance + Backup | ✅ | InitialOracleSchema | |
| BackupPolicy + RestoreJob | ✅ | AddServiceAutoscalingAndBlueGreen | |
| S3Destination | ✅ | AddServiceAutoscalingAndBlueGreen | |
| Pipeline + stages + steps | ✅ | InitialOracleSchema | |
| PipelineRun + PipelineRunLog | ✅ | AddServiceAutoscalingAndBlueGreen | |
| Alert + AlertRule | ✅ | InitialOracleSchema + AddAlertRules | |
| Domain | ✅ | InitialOracleSchema | |
| EnvVariable | ✅ | InitialOracleSchema | |
| SshKey | ✅ | InitialOracleSchema | |
| AuditLog | ✅ | InitialOracleSchema | |
| CostRecord | ✅ | InitialOracleSchema + AddCostRecordCategory | |
| NotificationConfig | ✅ | InitialOracleSchema | |
| RefreshTokenRecord | ✅ | InitialOracleSchema | |
| ResourcePermission | ✅ | AddServiceAutoscalingAndBlueGreen | |
| Cluster | ✅ | AddServiceAutoscalingAndBlueGreen | |
| DeployWebhook | ✅ | AddServiceAutoscalingAndBlueGreen | |
| ScheduledTask | ✅ | AddServiceAutoscalingAndBlueGreen | |
| Volume | ✅ | AddServiceAutoscalingAndBlueGreen | |
| ProjectEnvironment | ✅ | AddSprint910Tables | |
| TeamInvitation | ✅ | AddTeamInvitationsAndExpandedPermissions | |
| ComposeStack | ✅ | AddSprint910Tables | |
| AppTemplate | ✅ | AddSprint910Tables | |
| TraefikRouter | ✅ | AddSprint910Tables | |
| ProvisioningJob | ✅ | AddSprint910Tables | |
| RecoveryRule | ✅ | AddSprint910Tables | |
| PreviewEnvironment | ✅ | AddSprint910Tables | |
| OutboundWebhookConfig | ✅ | AddSprint910Tables | |
| Container (domain entity) | ❌ | — | Live Docker state only, not DB-persisted |
| ScheduledDeploy (domain entity) | ❌ | — | Functionality merged into ScheduledTask |
| ProjectMember (domain entity) | ❌ | — | RBAC via Identity + ResourcePermission |

**✅ All 43 EF-tracked entities have complete, up-to-date migrations.**

---

## 24. API Reference Summary

### Base URL

```
http://localhost:5000/api
```

### Authentication Header

```
Authorization: Bearer {accessToken}
```

### Core Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/auth/login` | Login, get JWT |
| POST | `/auth/register` | Register new user |
| POST | `/auth/refresh` | Refresh access token |
| PUT | `/auth/profile` | Update profile |
| PUT | `/auth/avatar` | Upload avatar (base64) |
| GET | `/projects` | List projects |
| POST | `/projects` | Create project |
| GET | `/projects/{id}` | Get project detail |
| PUT | `/projects/{id}` | Update project |
| DELETE | `/projects/{id}` | Delete project |
| POST | `/projects/{id}/build` | Trigger build → returns deploymentId |
| GET | `/projects/{id}/detect-stack` | Detect framework |
| GET | `/deployments` | List deployments |
| POST | `/deployments` | Create deployment |
| GET | `/deployments/{id}` | Get deployment |
| POST | `/deployments/{id}/approve` | Approve deployment |
| POST | `/deployments/{id}/reject` | Reject deployment |
| POST | `/deployments/{id}/cancel` | Cancel deployment |
| POST | `/deployments/{id}/rollback` | Rollback |
| POST | `/deployments/{id}/canary/start` | Start canary |
| POST | `/deployments/{id}/canary/promote` | Promote canary |
| POST | `/deployments/{id}/canary/abort` | Abort canary |
| GET | `/servers` | List servers |
| POST | `/servers` | Add server |
| POST | `/servers/{id}/health-check` | Run health check |
| GET | `/servers/{id}/metrics` | Get server metrics |
| POST | `/servers/{id}/exec` | Execute SSH command |
| GET | `/logs` | Get deployment logs |
| GET | `/logs/stream` | SSE live log stream |
| GET | `/ai/analyze/{deploymentId}` | AI deployment analysis |
| POST | `/ai/chat` | AI chat |
| GET | `/integrations/{service}` | Get integration status |
| POST | `/integrations/{service}` | Connect integration |
| DELETE | `/integrations/{service}` | Disconnect integration |
| POST | `/integrations/{service}/test` | Test integration |

### Real-time (SignalR)

```
Hub URL: /hubs/logs
Auth: ?access_token={jwt}

Client-side:
  invoke("SubscribeToDeployment", deploymentId)
  invoke("UnsubscribeFromDeployment", deploymentId)

Events received:
  on("log", { message, stream, timestamp })
  on("statusChanged", { status })
```

---

## Appendix: Sample cURL Commands

```bash
# 1. Login
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@deployflow.dev","password":"Admin123!"}' \
  | jq -r '.accessToken')

# 2. List projects
curl -s http://localhost:5000/api/projects \
  -H "Authorization: Bearer $TOKEN" | jq '.data[].name'

# 3. Trigger deployment
curl -s -X POST http://localhost:5000/api/projects/PROJ-ID/build \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}' | jq '.deploymentId'

# 4. Get live logs (SSE)
curl -N "http://localhost:5000/api/logs/stream?access_token=$TOKEN"

# 5. Add server
curl -s -X POST http://localhost:5000/api/servers \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "my-server",
    "ipAddress": "192.168.1.100",
    "sshPort": 22,
    "sshUser": "ubuntu",
    "cpuCount": 4,
    "memoryGb": 8
  }'
```

---

*Documentation generated: March 30, 2026 · DeployFlow Sprint 10*
