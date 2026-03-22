/**
 * Comprehensive mock data for all DeployFlow features.
 * Used when NEXT_PUBLIC_USE_MOCK_API=true or when the backend is unreachable.
 */

import type {
  Alert,
  Deployment,
  Server,
  Project,
  Service,
  Database,
  Pipeline,
  Domain,
  TeamMember,
  SshKey,
  CostRecord,
  AuditLog,
  NotificationConfig,
  PaginatedResponse,
} from "@/types";

// ─── Local types (avoid circular deps) ──────────────────────────────────────

interface DashboardStats {
  totalProjects: number;
  activeDeployments: number;
  totalServers: number;
  onlineServers: number;
  failedDeploymentsToday: number;
  successfulDeploymentsToday: number;
  avgDeploymentDuration: number;
  totalDatabases: number;
  activeAlerts: number;
  monthlyCost: number;
}

interface Container {
  id: string;
  name: string;
  image: string;
  status: string;
  startedAt: string;
  cpuPercent: number;
  memoryBytes: number;
  memoryLimitBytes: number;
  projectId?: string;
  serviceId?: string;
}

interface Volume {
  id: string;
  name: string;
  status: string;
  driver: string;
  mountPath?: string;
  sizeBytes: number;
  projectId?: string;
  createdAt: string;
}

const now = Date.now();

// ─── Dashboard Stats ────────────────────────────────────────────────────────

export const mockDashboardStats: DashboardStats = {
  totalProjects: 12,
  activeDeployments: 3,
  totalServers: 6,
  onlineServers: 5,
  failedDeploymentsToday: 1,
  successfulDeploymentsToday: 14,
  avgDeploymentDuration: 187,
  totalDatabases: 8,
  activeAlerts: 2,
  monthlyCost: 847.5,
};

// ─── Projects ───────────────────────────────────────────────────────────────

export const mockProjects: PaginatedResponse<Project> = {
  data: [
    {
      id: "proj-1", name: "api-gateway", description: "Main API gateway handling all public traffic",
      status: "active", repositoryUrl: "https://github.com/acme/api-gateway", repositoryBranch: "main",
      environmentId: "env-prod", serverId: "srv-1", tags: ["production", "api", "core"],
      createdAt: "2024-01-15T08:00:00Z", updatedAt: "2026-03-19T09:55:00Z",
      lastDeployedAt: new Date(now - 1200000).toISOString(), deploymentCount: 142,
      activeDeploymentId: "dep-1",
      settings: { buildCommand: "npm run build", installCommand: "npm ci", startCommand: "npm start",
        outputDirectory: "dist", autoDeployEnabled: true, branchDeployEnabled: true,
        previewDeployEnabled: false, port: 3000, healthCheckPath: "/health", healthCheckTimeout: 30 },
    },
    {
      id: "proj-2", name: "frontend-web", description: "Customer-facing Next.js web application",
      status: "active", repositoryUrl: "https://github.com/acme/frontend-web", repositoryBranch: "main",
      environmentId: "env-prod", serverId: "srv-1", tags: ["production", "frontend"],
      createdAt: "2024-01-20T10:00:00Z", updatedAt: "2026-03-19T08:30:00Z",
      lastDeployedAt: new Date(now - 300000).toISOString(), deploymentCount: 89,
      settings: { buildCommand: "next build", installCommand: "npm ci", startCommand: "next start",
        outputDirectory: ".next", autoDeployEnabled: true, branchDeployEnabled: true,
        previewDeployEnabled: true, port: 3000, healthCheckPath: "/" },
    },
    {
      id: "proj-3", name: "worker-service", description: "Background job processor and queue consumer",
      status: "active", repositoryUrl: "https://github.com/acme/worker-service", repositoryBranch: "main",
      environmentId: "env-prod", serverId: "srv-1", tags: ["production", "backend", "workers"],
      createdAt: "2024-02-01T08:00:00Z", updatedAt: "2026-03-19T09:35:00Z",
      lastDeployedAt: new Date(now - 7200000).toISOString(), deploymentCount: 56,
      settings: { buildCommand: "npm run build", installCommand: "npm ci", startCommand: "npm run worker",
        autoDeployEnabled: true, branchDeployEnabled: false, previewDeployEnabled: false },
    },
    {
      id: "proj-4", name: "auth-service", description: "Authentication & authorization microservice",
      status: "active", repositoryUrl: "https://github.com/acme/auth-service", repositoryBranch: "main",
      environmentId: "env-prod", serverId: "srv-2", tags: ["production", "auth", "security"],
      createdAt: "2024-01-25T08:00:00Z", updatedAt: "2026-03-18T15:00:00Z",
      lastDeployedAt: new Date(now - 18000000).toISOString(), deploymentCount: 38,
      settings: { buildCommand: "cargo build --release", installCommand: "", startCommand: "./auth-server",
        dockerfilePath: "Dockerfile", autoDeployEnabled: true, branchDeployEnabled: false, previewDeployEnabled: false,
        port: 8080, healthCheckPath: "/healthz" },
    },
    {
      id: "proj-5", name: "notification-hub", description: "Multi-channel notification service",
      status: "active", repositoryUrl: "https://github.com/acme/notification-hub", repositoryBranch: "main",
      environmentId: "env-staging", serverId: "srv-3", tags: ["staging", "notifications"],
      createdAt: "2024-03-01T10:00:00Z", updatedAt: "2026-03-17T12:00:00Z",
      lastDeployedAt: new Date(now - 86400000).toISOString(), deploymentCount: 24,
      settings: { buildCommand: "go build -o server .", installCommand: "", startCommand: "./server",
        dockerfilePath: "Dockerfile", autoDeployEnabled: false, branchDeployEnabled: false,
        previewDeployEnabled: false, port: 9090, healthCheckPath: "/ping" },
    },
    {
      id: "proj-6", name: "admin-panel", description: "Internal administration dashboard",
      status: "active", repositoryUrl: "https://github.com/acme/admin-panel", repositoryBranch: "develop",
      environmentId: "env-staging", serverId: "srv-3", tags: ["staging", "admin", "internal"],
      createdAt: "2024-04-10T08:00:00Z", updatedAt: "2026-03-16T09:00:00Z",
      deploymentCount: 67,
      settings: { buildCommand: "npm run build", installCommand: "npm ci", startCommand: "npm start",
        autoDeployEnabled: true, branchDeployEnabled: true, previewDeployEnabled: true, port: 3001 },
    },
    {
      id: "proj-7", name: "payment-processor", description: "Stripe & PayPal payment integration",
      status: "active", repositoryUrl: "https://github.com/acme/payments", repositoryBranch: "main",
      environmentId: "env-prod", serverId: "srv-2", tags: ["production", "payments", "critical"],
      createdAt: "2024-02-15T10:00:00Z", updatedAt: "2026-03-18T11:00:00Z",
      lastDeployedAt: new Date(now - 43200000).toISOString(), deploymentCount: 31,
      settings: { buildCommand: "npm run build", installCommand: "npm ci", startCommand: "npm start",
        autoDeployEnabled: false, branchDeployEnabled: false, previewDeployEnabled: false,
        port: 4000, healthCheckPath: "/health" },
    },
    {
      id: "proj-8", name: "data-pipeline", description: "ETL data processing pipeline",
      status: "archived", repositoryUrl: "https://github.com/acme/data-pipeline",
      tags: ["archived", "data"],
      createdAt: "2024-01-10T08:00:00Z", updatedAt: "2025-12-01T10:00:00Z",
      deploymentCount: 15,
      settings: { buildCommand: "python -m build", autoDeployEnabled: false,
        branchDeployEnabled: false, previewDeployEnabled: false },
    },
  ],
  total: 12, page: 1, pageSize: 20, totalPages: 1,
};

// ─── Servers ────────────────────────────────────────────────────────────────

export const mockServers: Server[] = [
  {
    id: "srv-1", name: "prod-us-east-1", hostname: "prod-us-east-1.internal",
    ipAddress: "10.0.1.10", port: 2222, status: "online", provider: "aws", region: "us-east-1",
    os: "Ubuntu 22.04", arch: "x64", cpu: 8, memoryGB: 32, diskGB: 200,
    tags: ["production", "web"], isSwarmManager: true, isSwarmWorker: false,
    dockerVersion: "24.0.7", kubernetesEnabled: false,
    createdAt: "2024-01-15T08:00:00Z", lastHealthCheckAt: new Date().toISOString(),
    metrics: { cpuUsagePercent: 42, memoryUsagePercent: 61, diskUsagePercent: 38,
      networkInMbps: 12.4, networkOutMbps: 8.7, uptimeSeconds: 864000,
      loadAverage: [1.2, 1.5, 1.8], timestamp: new Date().toISOString() },
    sshKeyId: "key-1",
  },
  {
    id: "srv-2", name: "prod-eu-west-1", hostname: "prod-eu-west-1.internal",
    ipAddress: "10.0.2.10", port: 2222, status: "online", provider: "hetzner", region: "eu-west-1",
    os: "Debian 12", arch: "x64", cpu: 4, memoryGB: 16, diskGB: 100,
    tags: ["production", "eu"], isSwarmManager: false, isSwarmWorker: true,
    dockerVersion: "24.0.7", kubernetesEnabled: false,
    createdAt: "2024-02-10T10:00:00Z", lastHealthCheckAt: new Date().toISOString(),
    metrics: { cpuUsagePercent: 28, memoryUsagePercent: 47, diskUsagePercent: 55,
      networkInMbps: 6.1, networkOutMbps: 3.2, uptimeSeconds: 432000,
      loadAverage: [0.8, 0.9, 1.0], timestamp: new Date().toISOString() },
    sshKeyId: "key-1",
  },
  {
    id: "srv-3", name: "staging-us-east-1", hostname: "staging-us-east-1.internal",
    ipAddress: "10.0.3.10", port: 2222, status: "online", provider: "digitalocean", region: "nyc3",
    os: "Ubuntu 22.04", arch: "x64", cpu: 4, memoryGB: 8, diskGB: 80,
    tags: ["staging"], isSwarmManager: false, isSwarmWorker: false,
    dockerVersion: "24.0.5", kubernetesEnabled: false,
    createdAt: "2024-03-01T09:00:00Z", lastHealthCheckAt: new Date().toISOString(),
    metrics: { cpuUsagePercent: 15, memoryUsagePercent: 33, diskUsagePercent: 22,
      networkInMbps: 2.3, networkOutMbps: 1.1, uptimeSeconds: 172800,
      loadAverage: [0.3, 0.4, 0.5], timestamp: new Date().toISOString() },
    sshKeyId: "key-2",
  },
  {
    id: "srv-4", name: "db-prod-primary", hostname: "db-prod-primary.internal",
    ipAddress: "10.0.4.10", port: 2222, status: "online", provider: "aws", region: "us-east-1",
    os: "Ubuntu 22.04", arch: "x64", cpu: 16, memoryGB: 64, diskGB: 1000,
    tags: ["production", "database"], isSwarmManager: false, isSwarmWorker: false,
    dockerVersion: "24.0.7", kubernetesEnabled: false,
    createdAt: "2024-01-20T08:00:00Z", lastHealthCheckAt: new Date().toISOString(),
    metrics: { cpuUsagePercent: 75, memoryUsagePercent: 82, diskUsagePercent: 71,
      networkInMbps: 45.2, networkOutMbps: 38.9, uptimeSeconds: 2592000,
      loadAverage: [3.2, 3.0, 2.8], timestamp: new Date().toISOString() },
    sshKeyId: "key-1",
  },
  {
    id: "srv-5", name: "worker-us-east-1", hostname: "worker-us-east-1.internal",
    ipAddress: "10.0.5.10", port: 2222, status: "maintenance", provider: "aws", region: "us-east-1",
    os: "Ubuntu 22.04", arch: "x64", cpu: 8, memoryGB: 32, diskGB: 200,
    tags: ["workers", "batch"], isSwarmManager: false, isSwarmWorker: true,
    dockerVersion: "24.0.7", kubernetesEnabled: false,
    createdAt: "2024-02-05T08:00:00Z",
    lastHealthCheckAt: new Date(now - 3600000).toISOString(), sshKeyId: "key-1",
  },
  {
    id: "srv-6", name: "dev-sandbox", hostname: "dev-sandbox.internal",
    ipAddress: "10.0.6.10", port: 2222, status: "offline", provider: "custom",
    os: "Ubuntu 20.04", arch: "x64", cpu: 2, memoryGB: 4, diskGB: 40,
    tags: ["dev"], isSwarmManager: false, isSwarmWorker: false,
    dockerVersion: "23.0.1", kubernetesEnabled: false,
    createdAt: "2024-03-15T12:00:00Z",
    lastHealthCheckAt: new Date(now - 86400000).toISOString(), sshKeyId: "key-2",
  },
];

// ─── Deployments ────────────────────────────────────────────────────────────

export const mockDeployments: PaginatedResponse<Deployment> = {
  data: [
    {
      id: "dep-1", projectId: "proj-1", projectName: "api-gateway", status: "healthy",
      trigger: "git_push", commitSha: "a3f8b21", commitMessage: "feat: add rate limiting middleware",
      commitAuthor: "alice@example.com", branch: "main", environmentId: "env-prod", serverId: "srv-1",
      imageTag: "api-gateway:a3f8b21",
      createdAt: new Date(now - 1200000).toISOString(), startedAt: new Date(now - 1190000).toISOString(),
      finishedAt: new Date(now - 1000000).toISOString(), duration: 190,
      url: "https://api.example.com", isRollback: false,
    },
    {
      id: "dep-2", projectId: "proj-2", projectName: "frontend-web", status: "building",
      trigger: "git_push", commitSha: "c7d4e92", commitMessage: "chore: update dependencies",
      commitAuthor: "bob@example.com", branch: "main", environmentId: "env-prod", serverId: "srv-1",
      createdAt: new Date(now - 300000).toISOString(), startedAt: new Date(now - 290000).toISOString(),
      isRollback: false,
    },
    {
      id: "dep-3", projectId: "proj-3", projectName: "worker-service", status: "failed",
      trigger: "manual", commitSha: "e5f1b34", commitMessage: "fix: memory leak in job processor",
      commitAuthor: "carol@example.com", branch: "hotfix/memory-leak", environmentId: "env-prod",
      serverId: "srv-1",
      createdAt: new Date(now - 7200000).toISOString(), startedAt: new Date(now - 7190000).toISOString(),
      finishedAt: new Date(now - 7000000).toISOString(), duration: 190, isRollback: false,
    },
    {
      id: "dep-4", projectId: "proj-4", projectName: "auth-service", status: "healthy",
      trigger: "api", commitSha: "b2a9c56", commitMessage: "refactor: migrate to JWT RS256",
      commitAuthor: "dave@example.com", branch: "main", environmentId: "env-prod", serverId: "srv-2",
      imageTag: "auth-service:b2a9c56",
      createdAt: new Date(now - 18000000).toISOString(), startedAt: new Date(now - 17990000).toISOString(),
      finishedAt: new Date(now - 17800000).toISOString(), duration: 200,
      url: "https://auth.example.com", isRollback: false,
    },
    {
      id: "dep-5", projectId: "proj-5", projectName: "notification-hub", status: "healthy",
      trigger: "schedule", commitSha: "f9e3d78", commitMessage: "ci: weekly scheduled deploy",
      commitAuthor: "system", branch: "main", environmentId: "env-staging", serverId: "srv-3",
      imageTag: "notification-hub:f9e3d78",
      createdAt: new Date(now - 86400000).toISOString(), startedAt: new Date(now - 86390000).toISOString(),
      finishedAt: new Date(now - 86200000).toISOString(), duration: 190,
      url: "https://staging-notify.example.com", isRollback: false,
    },
  ],
  total: 47, page: 1, pageSize: 5, totalPages: 10,
};

// ─── Alerts ─────────────────────────────────────────────────────────────────

export const mockAlerts: Alert[] = [
  {
    id: "alert-1", name: "High CPU on db-prod-primary",
    description: "CPU usage exceeded 75% for more than 10 minutes.",
    severity: "warning", status: "active", source: "prometheus",
    resourceId: "srv-4", resourceType: "server",
    triggeredAt: new Date(now - 600000).toISOString(),
    conditions: [{ metric: "cpu_usage_percent", operator: "gt", threshold: 75, duration: 600 }],
  },
  {
    id: "alert-2", name: "worker-service deployment failed",
    description: "Deployment dep-3 failed to start after 3 retries.",
    severity: "critical", status: "active", source: "deployflow",
    resourceId: "dep-3", resourceType: "deployment",
    triggeredAt: new Date(now - 7000000).toISOString(),
    conditions: [{ metric: "deployment_status", operator: "eq", threshold: 0 }],
  },
  {
    id: "alert-3", name: "SSL certificate expiring",
    description: "Certificate for api.example.com expires in 14 days.",
    severity: "info", status: "acknowledged", source: "cert-manager",
    resourceType: "domain", triggeredAt: new Date(now - 172800000).toISOString(),
    acknowledgedBy: "alice@example.com",
    conditions: [{ metric: "cert_days_remaining", operator: "lt", threshold: 30 }],
  },
  {
    id: "alert-4", name: "Disk usage above 70%",
    description: "db-prod-primary disk usage is at 71%.",
    severity: "warning", status: "active", source: "prometheus",
    resourceId: "srv-4", resourceType: "server",
    triggeredAt: new Date(now - 3600000).toISOString(),
    conditions: [{ metric: "disk_usage_percent", operator: "gt", threshold: 70, duration: 300 }],
  },
];

// ─── Services ───────────────────────────────────────────────────────────────

export const mockServices: Service[] = [
  {
    id: "svc-1", projectId: "proj-1", name: "api-gateway", type: "api", status: "running",
    image: "acme/api-gateway", tag: "latest",
    ports: [{ hostPort: 3000, containerPort: 3000, protocol: "tcp" }],
    envVars: [], volumes: [],
    resources: { cpuLimit: "2", memoryLimit: "4Gi", cpuRequest: "0.5", memoryRequest: "1Gi" },
    healthCheck: { path: "/health", port: 3000, interval: 15, timeout: 5, retries: 3, startPeriod: 30 },
    replicas: 2, domainId: "dom-1",
    createdAt: "2024-01-15T08:00:00Z", updatedAt: "2026-03-19T09:55:00Z",
  },
  {
    id: "svc-2", projectId: "proj-2", name: "frontend-web", type: "web", status: "running",
    image: "acme/frontend-web", tag: "latest",
    ports: [{ hostPort: 3001, containerPort: 3000, protocol: "tcp" }],
    envVars: [], volumes: [],
    resources: { cpuLimit: "1", memoryLimit: "2Gi" },
    healthCheck: { path: "/", port: 3000, interval: 15, timeout: 5, retries: 3, startPeriod: 20 },
    replicas: 2, domainId: "dom-2",
    createdAt: "2024-01-20T10:00:00Z", updatedAt: "2026-03-19T08:30:00Z",
  },
  {
    id: "svc-3", projectId: "proj-3", name: "worker-service", type: "worker", status: "error",
    image: "acme/worker-service", tag: "latest",
    ports: [], envVars: [], volumes: [],
    resources: { cpuLimit: "4", memoryLimit: "8Gi" }, replicas: 3,
    createdAt: "2024-02-01T08:00:00Z", updatedAt: "2026-03-19T09:35:00Z",
  },
  {
    id: "svc-4", projectId: "proj-4", name: "auth-service", type: "api", status: "running",
    image: "acme/auth-service", tag: "v2.1.0",
    ports: [{ hostPort: 8080, containerPort: 8080, protocol: "tcp" }],
    envVars: [], volumes: [],
    resources: { cpuLimit: "1", memoryLimit: "2Gi" },
    healthCheck: { path: "/healthz", port: 8080, interval: 10, timeout: 3, retries: 5, startPeriod: 15 },
    replicas: 2,
    createdAt: "2024-01-25T08:00:00Z", updatedAt: "2026-03-18T15:00:00Z",
  },
  {
    id: "svc-5", projectId: "proj-1", name: "redis-cache", type: "cache", status: "running",
    image: "redis", tag: "7-alpine",
    ports: [{ hostPort: 6379, containerPort: 6379, protocol: "tcp" }],
    envVars: [], volumes: [{ containerPath: "/data", mode: "rw" }],
    resources: { cpuLimit: "0.5", memoryLimit: "1Gi" }, replicas: 1,
    createdAt: "2024-01-16T08:00:00Z", updatedAt: "2024-01-16T08:00:00Z",
  },
  {
    id: "svc-6", projectId: "proj-7", name: "payment-processor", type: "api", status: "running",
    image: "acme/payments", tag: "v1.8.3",
    ports: [{ hostPort: 4000, containerPort: 4000, protocol: "tcp" }],
    envVars: [], volumes: [],
    resources: { cpuLimit: "2", memoryLimit: "4Gi" },
    healthCheck: { path: "/health", port: 4000, interval: 10, timeout: 5, retries: 3, startPeriod: 20 },
    replicas: 2,
    createdAt: "2024-02-15T10:00:00Z", updatedAt: "2026-03-18T11:00:00Z",
  },
];

// ─── Databases ──────────────────────────────────────────────────────────────

export const mockDatabases: Database[] = [
  {
    id: "db-1", name: "postgres-prod", type: "postgresql", version: "16.2", status: "running",
    serverId: "srv-4", host: "10.0.4.10", port: 5432, databaseName: "deployflow_prod",
    username: "deployflow", storageGB: 250, backupEnabled: true,
    lastBackupAt: new Date(now - 3600000).toISOString(), createdAt: "2024-01-20T08:00:00Z",
    connectionString: "postgresql://***:***@10.0.4.10:5432/deployflow_prod",
    tags: ["production", "primary"],
  },
  {
    id: "db-2", name: "postgres-replica", type: "postgresql", version: "16.2", status: "running",
    serverId: "srv-4", host: "10.0.4.11", port: 5432, databaseName: "deployflow_prod",
    username: "deployflow_ro", storageGB: 250, backupEnabled: false,
    createdAt: "2024-01-21T08:00:00Z", tags: ["production", "replica"],
  },
  {
    id: "db-3", name: "redis-prod", type: "redis", version: "7.2", status: "running",
    serverId: "srv-1", host: "10.0.1.10", port: 6379, databaseName: "0",
    username: "default", storageGB: 4, backupEnabled: true,
    lastBackupAt: new Date(now - 7200000).toISOString(), createdAt: "2024-01-16T08:00:00Z",
    tags: ["production", "cache"],
  },
  {
    id: "db-4", name: "mongo-logs", type: "mongodb", version: "7.0", status: "running",
    serverId: "srv-4", host: "10.0.4.10", port: 27017, databaseName: "logs",
    username: "logwriter", storageGB: 500, backupEnabled: true,
    lastBackupAt: new Date(now - 14400000).toISOString(), createdAt: "2024-02-01T10:00:00Z",
    tags: ["production", "logs"],
  },
  {
    id: "db-5", name: "postgres-staging", type: "postgresql", version: "16.2", status: "running",
    serverId: "srv-3", host: "10.0.3.10", port: 5432, databaseName: "deployflow_staging",
    username: "deployflow", storageGB: 50, backupEnabled: false,
    createdAt: "2024-03-01T09:00:00Z", tags: ["staging"],
  },
  {
    id: "db-6", name: "mysql-legacy", type: "mysql", version: "8.0", status: "stopped",
    serverId: "srv-6", host: "10.0.6.10", port: 3306, databaseName: "legacy_app",
    username: "root", storageGB: 30, backupEnabled: false,
    createdAt: "2024-01-10T08:00:00Z", tags: ["legacy", "deprecated"],
  },
  {
    id: "db-7", name: "redis-sessions", type: "redis", version: "7.2", status: "running",
    serverId: "srv-2", host: "10.0.2.10", port: 6380, databaseName: "0",
    username: "default", storageGB: 2, backupEnabled: false,
    createdAt: "2024-02-10T10:00:00Z", tags: ["production", "sessions"],
  },
  {
    id: "db-8", name: "mariadb-analytics", type: "mariadb", version: "11.2", status: "running",
    serverId: "srv-4", host: "10.0.4.10", port: 3307, databaseName: "analytics",
    username: "analytics_rw", storageGB: 120, backupEnabled: true,
    lastBackupAt: new Date(now - 28800000).toISOString(), createdAt: "2024-03-10T08:00:00Z",
    tags: ["production", "analytics"],
  },
  {
    id: "db-9", name: "oracle-erp", type: "oracle", version: "19c", status: "running",
    serverId: "srv-1", host: "10.0.1.20", port: 1521, databaseName: "ORCL",
    username: "erp_owner", storageGB: 500, backupEnabled: true,
    lastBackupAt: new Date(now - 14400000).toISOString(), createdAt: "2024-01-05T08:00:00Z",
    connectionString: "oracle://erp_owner:***@10.0.1.20:1521/ORCL",
    tags: ["production", "erp", "oracle"],
  },
];

// ─── Pipelines ──────────────────────────────────────────────────────────────

export const mockPipelines: Pipeline[] = [
  {
    id: "pipe-1", projectId: "proj-1", name: "API Gateway CI/CD",
    description: "Build, test, and deploy the API gateway", status: "success",
    trigger: { type: "push", branches: ["main", "develop"] },
    stages: [
      { id: "s1", name: "Install", order: 1, runParallel: false,
        steps: [{ id: "st1", name: "npm ci", type: "command", status: "success", command: "npm ci", exitCode: 0 }] },
      { id: "s2", name: "Test", order: 2, runParallel: true,
        steps: [
          { id: "st2", name: "Unit Tests", type: "test", status: "success", command: "npm test", exitCode: 0 },
          { id: "st3", name: "Lint", type: "command", status: "success", command: "npm run lint", exitCode: 0 },
        ] },
      { id: "s3", name: "Build", order: 3, runParallel: false,
        steps: [{ id: "st4", name: "Docker Build", type: "docker", status: "success", image: "acme/api-gateway:latest" }] },
      { id: "s4", name: "Deploy", order: 4, runParallel: false,
        steps: [{ id: "st5", name: "Deploy to Prod", type: "deploy", status: "success" }] },
    ],
    lastRunAt: new Date(now - 1200000).toISOString(), lastRunDuration: 245,
    createdAt: "2024-01-15T08:00:00Z", updatedAt: "2026-03-19T09:55:00Z", isEnabled: true,
  },
  {
    id: "pipe-2", projectId: "proj-2", name: "Frontend Build Pipeline",
    description: "Build and deploy the frontend web app", status: "running",
    trigger: { type: "push", branches: ["main"] },
    stages: [
      { id: "s5", name: "Install", order: 1, runParallel: false,
        steps: [{ id: "st6", name: "npm ci", type: "command", status: "success", command: "npm ci" }] },
      { id: "s6", name: "Build", order: 2, runParallel: false,
        steps: [{ id: "st7", name: "next build", type: "command", status: "running", command: "next build" }] },
    ],
    lastRunAt: new Date(now - 300000).toISOString(),
    createdAt: "2024-01-20T10:00:00Z", updatedAt: "2026-03-19T08:30:00Z", isEnabled: true,
  },
  {
    id: "pipe-3", projectId: "proj-3", name: "Worker Service Pipeline",
    description: "Worker service CI pipeline", status: "failed",
    trigger: { type: "manual" },
    stages: [
      { id: "s7", name: "Build & Test", order: 1, runParallel: false,
        steps: [
          { id: "st8", name: "Build", type: "command", status: "success", command: "npm run build" },
          { id: "st9", name: "Test", type: "test", status: "failed", command: "npm test", exitCode: 1 },
        ] },
    ],
    lastRunAt: new Date(now - 7200000).toISOString(), lastRunDuration: 120,
    createdAt: "2024-02-01T08:00:00Z", updatedAt: "2026-03-19T09:35:00Z", isEnabled: true,
  },
  {
    id: "pipe-4", projectId: "proj-5", name: "Nightly Build",
    description: "Scheduled nightly build and deploy to staging", status: "success",
    trigger: { type: "schedule", schedule: "0 2 * * *" },
    stages: [
      { id: "s8", name: "Full CI", order: 1, runParallel: false,
        steps: [
          { id: "st10", name: "Build", type: "command", status: "success", command: "go build" },
          { id: "st11", name: "Test", type: "test", status: "success", command: "go test ./..." },
          { id: "st12", name: "Deploy", type: "deploy", status: "success" },
        ] },
    ],
    lastRunAt: new Date(now - 86400000).toISOString(), lastRunDuration: 180,
    createdAt: "2024-03-01T10:00:00Z", updatedAt: "2026-03-18T02:00:00Z", isEnabled: true,
  },
];

// ─── Domains ────────────────────────────────────────────────────────────────

export const mockDomains: Domain[] = [
  {
    id: "dom-1", name: "api.example.com", status: "active", serviceId: "svc-1",
    isWildcard: false, sslEnabled: true, sslExpiresAt: new Date(now + 1209600000).toISOString(),
    sslProvider: "letsencrypt", dnsVerified: true, redirectWww: false,
    createdAt: "2024-01-15T08:00:00Z",
  },
  {
    id: "dom-2", name: "app.example.com", status: "active", serviceId: "svc-2",
    isWildcard: false, sslEnabled: true, sslExpiresAt: new Date(now + 5184000000).toISOString(),
    sslProvider: "letsencrypt", dnsVerified: true, redirectWww: true,
    createdAt: "2024-01-20T10:00:00Z",
  },
  {
    id: "dom-3", name: "auth.example.com", status: "active", serviceId: "svc-4",
    isWildcard: false, sslEnabled: true, sslExpiresAt: new Date(now + 7776000000).toISOString(),
    sslProvider: "cloudflare", dnsVerified: true, redirectWww: false,
    createdAt: "2024-01-25T08:00:00Z",
  },
  {
    id: "dom-4", name: "*.staging.example.com", status: "active",
    isWildcard: true, sslEnabled: true, sslExpiresAt: new Date(now + 2592000000).toISOString(),
    sslProvider: "letsencrypt", dnsVerified: true, redirectWww: false,
    createdAt: "2024-03-01T09:00:00Z",
  },
  {
    id: "dom-5", name: "payments.example.com", status: "pending", serviceId: "svc-6",
    isWildcard: false, sslEnabled: false, sslProvider: "letsencrypt",
    dnsVerified: false, redirectWww: false, createdAt: "2026-03-18T15:00:00Z",
  },
];

// ─── Containers ─────────────────────────────────────────────────────────────

export const mockContainers: Container[] = [
  { id: "ctn-1", name: "api-gateway-1", image: "acme/api-gateway:a3f8b21", status: "running",
    startedAt: new Date(now - 1000000).toISOString(), cpuPercent: 12.5,
    memoryBytes: 524288000, memoryLimitBytes: 4294967296, projectId: "proj-1", serviceId: "svc-1" },
  { id: "ctn-2", name: "api-gateway-2", image: "acme/api-gateway:a3f8b21", status: "running",
    startedAt: new Date(now - 1000000).toISOString(), cpuPercent: 8.3,
    memoryBytes: 498073600, memoryLimitBytes: 4294967296, projectId: "proj-1", serviceId: "svc-1" },
  { id: "ctn-3", name: "frontend-web-1", image: "acme/frontend-web:c7d4e92", status: "running",
    startedAt: new Date(now - 290000).toISOString(), cpuPercent: 5.1,
    memoryBytes: 262144000, memoryLimitBytes: 2147483648, projectId: "proj-2", serviceId: "svc-2" },
  { id: "ctn-4", name: "auth-service-1", image: "acme/auth-service:b2a9c56", status: "running",
    startedAt: new Date(now - 17800000).toISOString(), cpuPercent: 3.2,
    memoryBytes: 157286400, memoryLimitBytes: 2147483648, projectId: "proj-4", serviceId: "svc-4" },
  { id: "ctn-5", name: "redis-cache", image: "redis:7-alpine", status: "running",
    startedAt: new Date(now - 864000000).toISOString(), cpuPercent: 0.8,
    memoryBytes: 67108864, memoryLimitBytes: 1073741824, projectId: "proj-1", serviceId: "svc-5" },
  { id: "ctn-6", name: "worker-service-1", image: "acme/worker-service:e5f1b34", status: "exited",
    startedAt: new Date(now - 7000000).toISOString(), cpuPercent: 0,
    memoryBytes: 0, memoryLimitBytes: 8589934592, projectId: "proj-3", serviceId: "svc-3" },
  { id: "ctn-7", name: "payment-proc-1", image: "acme/payments:v1.8.3", status: "running",
    startedAt: new Date(now - 43200000).toISOString(), cpuPercent: 6.7,
    memoryBytes: 419430400, memoryLimitBytes: 4294967296, projectId: "proj-7", serviceId: "svc-6" },
  { id: "ctn-8", name: "postgres-prod", image: "postgres:16.2", status: "running",
    startedAt: new Date(now - 2592000000).toISOString(), cpuPercent: 22.4,
    memoryBytes: 8589934592, memoryLimitBytes: 17179869184 },
];

// ─── Volumes ────────────────────────────────────────────────────────────────

export const mockVolumes: Volume[] = [
  { id: "vol-1", name: "postgres-data", status: "in-use", driver: "local",
    mountPath: "/var/lib/postgresql/data", sizeBytes: 268435456000, createdAt: "2024-01-20T08:00:00Z" },
  { id: "vol-2", name: "redis-data", status: "in-use", driver: "local",
    mountPath: "/data", sizeBytes: 2147483648, projectId: "proj-1", createdAt: "2024-01-16T08:00:00Z" },
  { id: "vol-3", name: "mongo-data", status: "in-use", driver: "local",
    mountPath: "/data/db", sizeBytes: 536870912000, createdAt: "2024-02-01T10:00:00Z" },
  { id: "vol-4", name: "uploads-storage", status: "in-use", driver: "local",
    mountPath: "/app/uploads", sizeBytes: 10737418240, projectId: "proj-2", createdAt: "2024-01-20T10:00:00Z" },
  { id: "vol-5", name: "backup-staging", status: "available", driver: "local",
    mountPath: "/backups", sizeBytes: 53687091200, createdAt: "2024-03-01T09:00:00Z" },
  { id: "vol-6", name: "legacy-mysql-data", status: "available", driver: "local",
    mountPath: "/var/lib/mysql", sizeBytes: 32212254720, createdAt: "2024-01-10T08:00:00Z" },
];

// ─── Team Members ───────────────────────────────────────────────────────────

export const mockTeamMembers: TeamMember[] = [
  {
    id: "tm-1", userId: "u-1", role: "admin", joinedAt: "2024-01-01T00:00:00Z",
    user: { id: "u-1", name: "Alice Chen", email: "alice@acme.com", role: "admin",
      tenantId: "t-1", isActive: true, createdAt: "2024-01-01T00:00:00Z",
      lastLoginAt: new Date(now - 1800000).toISOString(), twoFactorEnabled: true },
    permissions: [{ resource: "*", actions: ["*"] }],
  },
  {
    id: "tm-2", userId: "u-2", role: "devops", joinedAt: "2024-01-15T00:00:00Z",
    user: { id: "u-2", name: "Bob Martinez", email: "bob@acme.com", role: "devops",
      tenantId: "t-1", isActive: true, createdAt: "2024-01-15T00:00:00Z",
      lastLoginAt: new Date(now - 3600000).toISOString(), twoFactorEnabled: true },
    permissions: [{ resource: "projects", actions: ["read", "write", "deploy"] },
      { resource: "servers", actions: ["read", "write"] }],
  },
  {
    id: "tm-3", userId: "u-3", role: "developer", joinedAt: "2024-02-01T00:00:00Z",
    user: { id: "u-3", name: "Carol Kim", email: "carol@acme.com", role: "developer",
      tenantId: "t-1", isActive: true, createdAt: "2024-02-01T00:00:00Z",
      lastLoginAt: new Date(now - 7200000).toISOString(), twoFactorEnabled: false },
    permissions: [{ resource: "projects", actions: ["read", "write"] },
      { resource: "deployments", actions: ["read", "create"] }],
  },
  {
    id: "tm-4", userId: "u-4", role: "developer", joinedAt: "2024-02-15T00:00:00Z",
    user: { id: "u-4", name: "Dave Patel", email: "dave@acme.com", role: "developer",
      tenantId: "t-1", isActive: true, createdAt: "2024-02-15T00:00:00Z",
      lastLoginAt: new Date(now - 86400000).toISOString(), twoFactorEnabled: true },
    permissions: [{ resource: "projects", actions: ["read", "write"] }],
  },
  {
    id: "tm-5", userId: "u-5", role: "viewer", joinedAt: "2024-03-01T00:00:00Z",
    user: { id: "u-5", name: "Eve Nguyen", email: "eve@acme.com", role: "viewer",
      tenantId: "t-1", isActive: true, createdAt: "2024-03-01T00:00:00Z",
      twoFactorEnabled: false },
    permissions: [{ resource: "*", actions: ["read"] }],
  },
];

// ─── SSH Keys ───────────────────────────────────────────────────────────────

export const mockSshKeys: SshKey[] = [
  {
    id: "key-1", name: "Production Servers Key",
    publicKey: "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIPr...production@acme",
    fingerprint: "SHA256:xH7k8Qp+V2mN9bR3cW1jX5yT8uI0oP/lK4nM6hG2sA",
    createdAt: "2024-01-10T08:00:00Z", lastUsedAt: new Date().toISOString(),
  },
  {
    id: "key-2", name: "Staging / Dev Key",
    publicKey: "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIKm...staging@acme",
    fingerprint: "SHA256:aB3cD4eF5gH6iJ7kL8mN9oP0qR1sT2uV3wX4yZ5aB6",
    createdAt: "2024-02-01T08:00:00Z", lastUsedAt: new Date(now - 86400000).toISOString(),
  },
];

// ─── Cost Records ───────────────────────────────────────────────────────────

export const mockCosts: CostRecord[] = [
  {
    id: "cost-1", period: "2026-03", serverId: "srv-1", provider: "aws",
    totalCost: 285.40, currency: "USD",
    breakdown: [
      { category: "compute", amount: 195.00, details: "t3.xlarge on-demand" },
      { category: "storage", amount: 46.00, details: "200GB gp3 EBS" },
      { category: "network", amount: 44.40, details: "Data transfer out" },
    ],
  },
  {
    id: "cost-2", period: "2026-03", serverId: "srv-2", provider: "hetzner",
    totalCost: 42.50, currency: "USD",
    breakdown: [
      { category: "compute", amount: 32.50, details: "CX31 cloud server" },
      { category: "storage", amount: 5.00, details: "100GB cloud volume" },
      { category: "network", amount: 5.00, details: "Bandwidth overage" },
    ],
  },
  {
    id: "cost-3", period: "2026-03", serverId: "srv-3", provider: "digitalocean",
    totalCost: 48.00, currency: "USD",
    breakdown: [
      { category: "compute", amount: 40.00, details: "s-4vcpu-8gb droplet" },
      { category: "storage", amount: 8.00, details: "80GB block storage" },
    ],
  },
  {
    id: "cost-4", period: "2026-03", serverId: "srv-4", provider: "aws",
    totalCost: 420.60, currency: "USD",
    breakdown: [
      { category: "compute", amount: 180.00, details: "r6g.xlarge on-demand" },
      { category: "database", amount: 155.00, details: "RDS PostgreSQL" },
      { category: "storage", amount: 85.60, details: "1TB gp3 EBS + snapshots" },
    ],
  },
  {
    id: "cost-5", period: "2026-03", serverId: "srv-5", provider: "aws",
    totalCost: 51.00, currency: "USD",
    breakdown: [
      { category: "compute", amount: 45.00, details: "t3.xlarge (stopped 50%)" },
      { category: "storage", amount: 6.00, details: "200GB EBS" },
    ],
  },
];

// ─── Audit Logs ─────────────────────────────────────────────────────────────

export const mockAuditLogs: PaginatedResponse<AuditLog> = {
  data: [
    { id: "log-1", userId: "u-1", userName: "Alice Chen", action: "deployment.created",
      resourceType: "deployment", resourceId: "dep-1", resourceName: "api-gateway",
      ipAddress: "192.168.1.10", createdAt: new Date(now - 1200000).toISOString() },
    { id: "log-2", userId: "u-2", userName: "Bob Martinez", action: "deployment.created",
      resourceType: "deployment", resourceId: "dep-2", resourceName: "frontend-web",
      ipAddress: "192.168.1.11", createdAt: new Date(now - 300000).toISOString() },
    { id: "log-3", userId: "u-3", userName: "Carol Kim", action: "deployment.created",
      resourceType: "deployment", resourceId: "dep-3", resourceName: "worker-service",
      ipAddress: "192.168.1.12", createdAt: new Date(now - 7200000).toISOString() },
    { id: "log-4", userId: "u-1", userName: "Alice Chen", action: "server.updated",
      resourceType: "server", resourceId: "srv-5", resourceName: "worker-us-east-1",
      ipAddress: "192.168.1.10", metadata: { field: "status", oldValue: "online", newValue: "maintenance" },
      createdAt: new Date(now - 10800000).toISOString() },
    { id: "log-5", userId: "u-2", userName: "Bob Martinez", action: "database.backup.triggered",
      resourceType: "database", resourceId: "db-1", resourceName: "postgres-prod",
      ipAddress: "192.168.1.11", createdAt: new Date(now - 14400000).toISOString() },
    { id: "log-6", userId: "u-1", userName: "Alice Chen", action: "team.member.invited",
      resourceType: "team", resourceId: "u-5", resourceName: "Eve Nguyen",
      ipAddress: "192.168.1.10", createdAt: new Date(now - 86400000).toISOString() },
  ],
  total: 156, page: 1, pageSize: 20, totalPages: 8,
};

// ─── Notification Configs ───────────────────────────────────────────────────

export const mockNotifications: NotificationConfig[] = [
  {
    id: "notif-1", channel: "slack", name: "Slack #deployments",
    isEnabled: true, config: { webhookUrl: "https://hooks.slack.com/services/T.../B.../xxx" },
    events: ["deployment.success", "deployment.failed"], createdAt: "2024-01-15T08:00:00Z",
  },
  {
    id: "notif-2", channel: "email", name: "Team Email",
    isEnabled: true, config: { to: "team@acme.com" },
    events: ["alert.critical", "deployment.failed"], createdAt: "2024-01-15T08:00:00Z",
  },
  {
    id: "notif-3", channel: "discord", name: "Discord DevOps",
    isEnabled: false, config: { webhookUrl: "https://discord.com/api/webhooks/..." },
    events: ["deployment.success", "deployment.failed", "alert.critical"],
    createdAt: "2024-03-01T10:00:00Z",
  },
];

// ─── Mock Resolver ──────────────────────────────────────────────────────────

export function getMockResponse(url: string): unknown {
  // Dashboard stats
  if (url.includes("/stats/dashboard"))    return mockDashboardStats;

  // Core resources
  if (url.includes("/projects"))           return mockProjects;
  if (url.includes("/deployments"))        return mockDeployments;
  if (url.includes("/servers"))            return mockServers;
  if (url.includes("/services"))           return mockServices;
  if (url.includes("/databases"))          return mockDatabases;
  if (url.includes("/pipelines"))          return mockPipelines;
  if (url.includes("/alerts"))             return mockAlerts;
  if (url.includes("/domains"))            return mockDomains;
  if (url.includes("/containers"))         return mockContainers;
  if (url.includes("/volumes"))            return mockVolumes;

  // Management
  if (url.includes("/team"))               return mockTeamMembers;
  if (url.includes("/ssh-keys"))           return mockSshKeys;
  if (url.includes("/costs"))              return mockCosts;
  if (url.includes("/audit-logs"))         return mockAuditLogs;
  if (url.includes("/notifications"))      return mockNotifications;

  // Fallback
  return [];
}
