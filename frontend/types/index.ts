// ============================================================
// Core Types
// ============================================================

export type ID = string;

export interface PaginatedResponse<T> {
  data: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ApiResponse<T> {
  data: T;
  message?: string;
  success: boolean;
}

export interface ApiError {
  message: string;
  code?: string;
  details?: Record<string, string[]>;
}

// ============================================================
// User & Auth
// ============================================================

export type UserRole = "owner" | "admin" | "devops" | "developer" | "viewer";

export interface User {
  id: ID;
  name: string;
  email: string;
  avatarUrl?: string;
  role: UserRole;
  tenantId: ID;
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string;
  twoFactorEnabled: boolean;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  name: string;
  email: string;
  password: string;
}

// ============================================================
// Project
// ============================================================

export type ProjectStatus = "active" | "archived" | "suspended";

export interface Project {
  id: ID;
  name: string;
  description?: string;
  status: ProjectStatus;
  repositoryUrl?: string;
  repositoryBranch?: string;
  environmentId?: ID;
  serverId?: ID;
  teamId?: ID;
  tags: string[];
  createdAt: string;
  updatedAt: string;
  lastDeployedAt?: string;
  deploymentCount: number;
  activeDeploymentId?: ID;
  settings: ProjectSettings;
}

export interface ProjectSettings {
  buildCommand?: string;
  installCommand?: string;
  startCommand?: string;
  outputDirectory?: string;
  rootDirectory?: string;
  autoDeployEnabled: boolean;
  branchDeployEnabled: boolean;
  previewDeployEnabled: boolean;
  dockerfilePath?: string;
  port?: number;
  healthCheckPath?: string;
  healthCheckTimeout?: number;
}

// ============================================================
// Deployment
// ============================================================

export type DeploymentStatus =
  | "queued"
  | "building"
  | "deploying"
  | "running"
  | "healthy"
  | "unhealthy"
  | "failed"
  | "cancelled"
  | "stopped"
  | "rolled_back";

export type DeploymentTrigger = "git_push" | "manual" | "api" | "schedule" | "rollback";

export interface Deployment {
  id: ID;
  projectId: ID;
  projectName: string;
  status: DeploymentStatus;
  trigger: DeploymentTrigger;
  commitSha?: string;
  commitMessage?: string;
  commitAuthor?: string;
  branch?: string;
  environmentId: ID;
  serverId: ID;
  containerIds?: string[];
  imageTag?: string;
  createdAt: string;
  startedAt?: string;
  finishedAt?: string;
  duration?: number;
  url?: string;
  previousDeploymentId?: string;
  isRollback: boolean;
  metadata?: Record<string, any>;
  errorMessage?: string;
}

// ============================================================
// Server
// ============================================================

export type ServerStatus = "online" | "offline" | "provisioning" | "maintenance" | "error";
export type ServerProvider = "custom" | "aws" | "azure" | "gcp" | "digitalocean" | "hetzner" | "vultr";

export interface Server {
  id: ID;
  name: string;
  hostname: string;
  ipAddress: string;
  port: number;
  status: ServerStatus;
  provider: ServerProvider;
  region?: string;
  os?: string;
  arch?: string;
  cpu: number;
  memoryGB: number;
  diskGB: number;
  tags: string[];
  isSwarmManager: boolean;
  isSwarmWorker: boolean;
  dockerVersion?: string;
  kubernetesEnabled: boolean;
  createdAt: string;
  lastHealthCheckAt?: string;
  metrics?: ServerMetrics;
  sshKeyId?: ID;
}

export interface ServerMetrics {
  cpuUsagePercent: number;
  memoryUsagePercent: number;
  diskUsagePercent: number;
  networkInMbps: number;
  networkOutMbps: number;
  uptimeSeconds: number;
  loadAverage: number[];
  timestamp: string;
}

// ============================================================
// Service
// ============================================================

export type ServiceType =
  | "web"
  | "api"
  | "worker"
  | "cron"
  | "database"
  | "cache"
  | "queue"
  | "storage";

export type ServiceStatus = "running" | "stopped" | "starting" | "restarting" | "error";

export interface Service {
  id: ID;
  projectId: ID;
  name: string;
  type: ServiceType;
  status: ServiceStatus;
  image?: string;
  tag?: string;
  ports: ServicePort[];
  envVars: EnvVariable[];
  volumes: ServiceVolume[];
  resources: ResourceLimits;
  healthCheck?: HealthCheck;
  replicas: number;
  minReplicas?: number;
  maxReplicas?: number;
  cpuTargetPercentage?: number;
  memoryTargetPercentage?: number;
  lastScalingAction?: string;
  lastScalingReason?: string;
  lastScaledAt?: string;
  domainId?: ID;
  createdAt: string;
  updatedAt: string;
  containerId?: string;
}

export interface ServiceScalingPolicy {
  serviceId: ID;
  currentReplicas: number;
  minReplicas: number;
  maxReplicas: number;
  cpuTargetPercentage?: number;
  memoryTargetPercentage?: number;
  lastScalingAction?: string;
  lastScalingReason?: string;
  lastScaledAt?: string;
}

export interface ServicePort {
  hostPort: number;
  containerPort: number;
  protocol: "tcp" | "udp";
}

export interface ServiceVolume {
  hostPath?: string;
  containerPath: string;
  volumeId?: ID;
  mode: "rw" | "ro";
}

export interface ResourceLimits {
  cpuLimit?: string;
  memoryLimit?: string;
  cpuRequest?: string;
  memoryRequest?: string;
}

export interface HealthCheck {
  path?: string;
  port?: number;
  interval: number;
  timeout: number;
  retries: number;
  startPeriod: number;
}

// ============================================================
// Environment Variables
// ============================================================

export type EnvVariableType = "plaintext" | "secret" | "file";

export interface EnvVariable {
  id: ID;
  key: string;
  value?: string; // masked if secret
  type: EnvVariableType;
  serviceId?: ID;
  projectId?: ID;
  isShared: boolean;
  createdAt: string;
}

// ============================================================
// Database
// ============================================================

export type DatabaseType = "postgresql" | "mysql" | "mariadb" | "mongodb" | "redis" | "mssql" | "oracle";
export type DatabaseStatus = "running" | "stopped" | "creating" | "restoring" | "error";

export interface Database {
  id: ID;
  name: string;
  type: DatabaseType;
  version: string;
  status: DatabaseStatus;
  serverId: ID;
  host?: string;
  port?: number;
  databaseName: string;
  username: string;
  storageGB: number;
  backupEnabled: boolean;
  lastBackupAt?: string;
  createdAt: string;
  connectionString?: string; // masked
  tags: string[];
}

export interface DatabaseBackup {
  id: ID;
  databaseId: ID;
  status: "pending" | "running" | "completed" | "failed";
  sizeBytes?: number;
  storageProvider: "local" | "s3" | "gcs" | "r2";
  createdAt: string;
  expiresAt?: string;
}

// ============================================================
// Pipeline
// ============================================================

export type PipelineStatus = "idle" | "running" | "success" | "failed" | "cancelled";
export type StepStatus = "pending" | "running" | "success" | "failed" | "skipped";

export interface Pipeline {
  id: ID;
  projectId: ID;
  name: string;
  description?: string;
  status: PipelineStatus;
  trigger: {
    type: "manual" | "push" | "pr" | "tag" | "schedule";
    branches?: string[];
    schedule?: string;
  };
  stages: PipelineStage[];
  lastRunAt?: string;
  lastRunDuration?: number;
  createdAt: string;
  updatedAt: string;
  isEnabled: boolean;
}

export interface PipelineStage {
  id: ID;
  name: string;
  order: number;
  steps: PipelineStep[];
  dependsOn?: ID[];
  runParallel: boolean;
}

export interface PipelineStep {
  id: ID;
  name: string;
  type: "command" | "docker" | "deploy" | "test" | "notify" | "approval";
  status?: StepStatus;
  command?: string;
  image?: string;
  envVars?: Record<string, string>;
  timeout?: number;
  artifacts?: string[];
  startedAt?: string;
  finishedAt?: string;
  logs?: string;
  exitCode?: number;
}

// ============================================================
// Monitoring
// ============================================================

export interface MetricDataPoint {
  timestamp: string;
  value: number;
}

export interface MetricSeries {
  name: string;
  data: MetricDataPoint[];
  unit?: string;
  color?: string;
}

export interface Alert {
  id: ID;
  name: string;
  description?: string;
  severity: "info" | "warning" | "critical";
  status: "active" | "resolved" | "acknowledged";
  source: string;
  resourceId?: ID;
  resourceType?: string;
  triggeredAt: string;
  resolvedAt?: string;
  acknowledgedBy?: string;
  conditions: AlertCondition[];
}

export interface AlertCondition {
  metric: string;
  operator: "gt" | "lt" | "gte" | "lte" | "eq";
  threshold: number;
  duration?: number;
}

// ============================================================
// Domain & SSL
// ============================================================

export type DomainStatus = "pending" | "active" | "error" | "expired";

export interface Domain {
  id: ID;
  name: string;
  status: DomainStatus;
  serviceId?: ID;
  isWildcard: boolean;
  sslEnabled: boolean;
  sslExpiresAt?: string;
  sslProvider: "letsencrypt" | "custom" | "cloudflare";
  dnsVerified: boolean;
  redirectWww: boolean;
  createdAt: string;
}

export interface DomainDnsCheckResult {
  domainId: ID;
  domainName: string;
  isValid: boolean;
  status: "verified" | "pending";
  message: string;
  expectedTarget: string;
  resolvedAddresses: string[];
  checkedAt: string;
}

export interface DomainSslActionResult {
  domainId: ID;
  domainName: string;
  sslEnabled: boolean;
  sslExpiresAt?: string;
  status: DomainStatus | string;
  message: string;
}

export interface AlertRule {
  id: ID;
  name: string;
  metric: string;
  operator: ">" | ">=" | "<" | "<=" | "=" | "!=";
  threshold: number;
  windowMinutes: number;
  severity: "critical" | "warning" | "info";
  isEnabled: boolean;
  cooldownMinutes: number;
  lastTriggeredAt?: string;
  description?: string;
  createdAt: string;
  updatedAt: string;
}

export interface AlertRuleTestResult {
  ruleId: ID;
  triggered: boolean;
  sampleValue: number;
  message: string;
  alertId?: ID;
}

// ============================================================
// Team & RBAC
// ============================================================

export interface TeamMember {
  id: ID;
  userId: ID;
  user: User;
  role: UserRole;
  joinedAt: string;
  permissions: Permission[];
}

export interface TeamInvitation {
  id: ID;
  email: string;
  name: string;
  role: UserRole | string;
  status: "pending" | "expired";
  expiresAt: string;
  lastSentAt: string;
  resendCount: number;
  invitedByName: string;
  createdAt: string;
}

export interface InvitationPreview {
  email: string;
  name: string;
  role: UserRole | string;
  expiresAt: string;
}

export interface Permission {
  resource: string;
  actions: string[];
}

export interface PermissionGrant {
  id: ID;
  userId: ID;
  resourceType: string;
  resourceId: ID;
  actions: string[];
}

export interface PermissionPreview {
  userId: ID;
  resourceType: string;
  resourceId: ID;
  effectiveActions: string[];
}

// ============================================================
// Notifications
// ============================================================

export type NotificationChannel =
  | "email"
  | "slack"
  | "webhook"
  | "discord"
  | "telegram"
  | "msteams"
  | "github"
  | "gitlab"
  | "cloudflare";

export interface NotificationConfig {
  id: ID;
  channel: NotificationChannel;
  name: string;
  isEnabled: boolean;
  config: Record<string, string>;
  events: string[];
  createdAt: string;
}

// ============================================================
// Cost Monitoring
// ============================================================

export interface CostRecord {
  id: ID;
  period: string;
  serverId?: ID;
  projectId?: ID;
  provider: ServerProvider;
  totalCost: number;
  currency: string;
  breakdown: CostBreakdown[];
}

export interface CostBreakdown {
  category: "compute" | "storage" | "network" | "database" | "other";
  amount: number;
  details?: string;
}

// ============================================================
// SSH Keys
// ============================================================

export interface SshKey {
  id: ID;
  name: string;
  publicKey: string;
  fingerprint: string;
  createdAt: string;
  lastUsedAt?: string;
}

// ============================================================
// Audit Log
// ============================================================

export interface AuditLog {
  id: ID;
  userId: ID;
  userName: string;
  action: string;
  resourceType: string;
  resourceId: ID;
  resourceName: string;
  ipAddress: string;
  userAgent?: string;
  metadata?: Record<string, any>;
  createdAt: string;
}

// ============================================================
// S3 Destination
// ============================================================

export type S3DestinationStatus = "unconfigured" | "active" | "error";

export interface S3Destination {
  id: ID;
  name: string;
  description?: string;
  endpoint: string;
  bucketName: string;
  region?: string;
  isDefault: boolean;
  status: S3DestinationStatus;
  lastTestedAt?: string;
  createdAt: string;
  updatedAt: string;
}

// ============================================================
// Backup Policy
// ============================================================

export interface BackupPolicy {
  id: ID;
  databaseInstanceId: ID;
  isEnabled: boolean;
  cronExpression: string;
  retentionDays: number;
  s3DestinationId?: ID;
  storageLocation: "local" | "s3";
  lastRunAt?: string;
  nextRunAt?: string;
  errorMessage?: string;
  createdAt: string;
  updatedAt: string;
}

// ============================================================
// Restore Job
// ============================================================

export type RestoreJobStatus = "pending" | "running" | "success" | "failed" | "cancelled";

export interface RestoreJob {
  id: ID;
  databaseInstanceId: ID;
  backupId: ID;
  targetDatabaseName: string;
  status: RestoreJobStatus;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
  progressPercent?: number;
  createdAt: string;
  updatedAt: string;
}
