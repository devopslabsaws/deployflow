import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api-client";

// Disable polling in mock mode — mock data never changes
const MOCK = process.env.NEXT_PUBLIC_USE_MOCK_API === "true";
import type {
  Project,
  Deployment,
  Server,
  Service,
  Database,
  Pipeline,
  Alert,
  Domain,
  EnvVariable,
  AuditLog,
  ServerMetrics,
  MetricSeries,
  PaginatedResponse,
  NotificationConfig,
  SshKey,
  TeamMember,
  CostRecord,
  S3Destination,
  BackupPolicy,
  RestoreJob,
  ServiceScalingPolicy,
} from "@/types";

// ─── Local Types ──────────────────────────────────────────────────────────────

export interface Container {
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

export interface Volume {
  id: string;
  name: string;
  status: string;
  driver: string;
  mountPath?: string;
  sizeBytes: number;
  projectId?: string;
  createdAt: string;
}

export interface LogLine {
  id: string;
  timestamp: string;
  level: "info" | "warn" | "error" | "debug";
  message: string;
  service: string;
}

export interface LogsResponse {
  items: LogLine[];
  nextCursor: number | null;
  hasMore: boolean;
}

export interface MonitoringSummary {
  avgCpu: number;
  avgMemory: number;
  avgDisk: number;
  avgNetworkInMbps: number;
  avgNetworkOutMbps: number;
  sampleCount: number;
}

export interface MonitoringPoint {
  timestamp: string;
  value: number;
}

export interface MonitoringNetworkPoint {
  timestamp: string;
  inbound: number;
  outbound: number;
}

export interface PipelineRun {
  id: string;
  pipelineId: string;
  status: "queued" | "running" | "success" | "failed" | "cancelled";
  startedAt: string;
  completedAt?: string | null;
  stageCount: number;
  stepCount: number;
  triggeredBy?: string | null;
  errorMessage?: string | null;
}

export interface PipelineRunLog {
  id: string;
  timestamp: string;
  level: string;
  stageName: string;
  stepName?: string | null;
  message: string;
  sequence: number;
}

function normalizeService(dto: any): Service {
  return {
    ...dto,
    status: dto.status?.toLowerCase() as Service["status"],
    type: dto.type?.toLowerCase() as Service["type"],
    ports: dto.ports ?? [],
    envVars: dto.envVars ?? [],
    volumes: dto.volumes ?? [],
    resources: {
      cpuLimit: dto.resources?.cpuLimit ?? dto.cpuLimit ?? undefined,
      memoryLimit: dto.resources?.memoryLimit ?? dto.memoryLimit ?? undefined,
      cpuRequest: dto.resources?.cpuRequest ?? dto.cpuRequest ?? undefined,
      memoryRequest: dto.resources?.memoryRequest ?? dto.memoryRequest ?? undefined,
    },
    replicas: dto.replicas ?? 1,
    minReplicas: dto.minReplicas ?? dto.replicas ?? 1,
    maxReplicas: dto.maxReplicas ?? dto.replicas ?? 1,
    cpuTargetPercentage: dto.cpuTargetPercentage ?? undefined,
    memoryTargetPercentage: dto.memoryTargetPercentage ?? undefined,
    lastScalingAction: dto.lastScalingAction ?? undefined,
    lastScalingReason: dto.lastScalingReason ?? undefined,
    lastScaledAt: dto.lastScaledAt ?? undefined,
  } as Service;
}

// ─── Query Keys ───────────────────────────────────────────────────────────────

export const queryKeys = {
  projects: {
    all: ["projects"] as const,
    list: (params?: any) => ["projects", "list", params] as const,
    detail: (id: string) => ["projects", "detail", id] as const,
  },
  deployments: {
    all: ["deployments"] as const,
    list: (params?: any) => ["deployments", "list", params] as const,
    detail: (id: string) => ["deployments", "detail", id] as const,
    byProject: (projectId: string) => ["deployments", "project", projectId] as const,
  },
  servers: {
    all: ["servers"] as const,
    list: () => ["servers", "list"] as const,
    detail: (id: string) => ["servers", "detail", id] as const,
    metrics: (id: string) => ["servers", "metrics", id] as const,
  },
  services: {
    all: ["services"] as const,
    list: (projectId?: string) => ["services", "list", projectId] as const,
    detail: (id: string) => ["services", "detail", id] as const,
  },
  databases: {
    all: ["databases"] as const,
    list: () => ["databases", "list"] as const,
    detail: (id: string) => ["databases", "detail", id] as const,
  },
  pipelines: {
    all: ["pipelines"] as const,
    list: (projectId?: string) => ["pipelines", "list", projectId] as const,
    detail: (id: string) => ["pipelines", "detail", id] as const,
    runs: (pipelineId: string) => ["pipelines", "runs", pipelineId] as const,
  },
  pipelineRuns: {
    detail: (runId: string) => ["pipeline-runs", "detail", runId] as const,
    logs: (runId: string, page: number, pageSize: number) => ["pipeline-runs", "logs", runId, page, pageSize] as const,
  },
  alerts: {
    all: ["alerts"] as const,
    list: () => ["alerts", "list"] as const,
  },
  domains: {
    all: ["domains"] as const,
    list: () => ["domains", "list"] as const,
    detail: (id: string) => ["domains", "detail", id] as const,
  },
  envVars: (projectId: string) => ["envvars", projectId] as const,
  auditLogs: (params?: any) => ["audit-logs", params] as const,
  metrics: (resourceId: string, type: string) => ["metrics", resourceId, type] as const,
  notifications: () => ["notifications"] as const,
  sshKeys: () => ["ssh-keys"] as const,
  team: () => ["team"] as const,
  costs: (params?: any) => ["costs", params] as const,
  costDashboard: (period: string) => ["costs", "dashboard", period] as const,
  stats: () => ["stats"] as const,
};

// ─── Projects ─────────────────────────────────────────────────────────────────

// Backend ProjectDto uses different field names; normalize to frontend Project type
function normalizeProject(dto: any): Project {
  return {
    ...dto,
    status: (dto.status as string)?.toLowerCase() as Project["status"],
    repositoryBranch: dto.branch ?? dto.repositoryBranch,
    deploymentCount: dto.totalDeployments ?? dto.deploymentCount ?? 0,
    serverId: dto.assignedServerId ?? dto.serverId,
    lastDeployedAt: dto.lastDeployedAt,
    lastDeploymentStatus: dto.lastDeploymentStatus?.toLowerCase(),
    settings: {
      buildCommand: dto.buildCommand,
      installCommand: dto.installCommand,
      startCommand: dto.startCommand,
      dockerfilePath: dto.dockerfilePath,
      autoDeployEnabled: dto.autoDeploy ?? dto.settings?.autoDeployEnabled ?? false,
      branchDeployEnabled: dto.settings?.branchDeployEnabled ?? false,
      previewDeployEnabled: dto.settings?.previewDeployEnabled ?? false,
    },
    tags: dto.tags ?? [],
  };
}

export function useProjects(params?: { status?: string; search?: string; page?: number }) {
  return useQuery({
    queryKey: queryKeys.projects.list(params),
    queryFn: async () => {
      const result = await apiClient.get<any>("/projects", { params });
      const items: any[] = Array.isArray(result) ? result : (result?.data ?? []);
      return {
        ...(Array.isArray(result) ? { total: items.length, page: 1, pageSize: items.length, totalPages: 1 } : result),
        data: items.map(normalizeProject),
      } as PaginatedResponse<Project>;
    },
    staleTime: 30_000,
  });
}

export function useProject(id: string) {
  return useQuery({
    queryKey: queryKeys.projects.detail(id),
    queryFn: async () => {
      const dto = await apiClient.get<any>(`/projects/${id}`);
      return normalizeProject(dto) as Project;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useCreateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<Project>) => apiClient.post<Project>("/projects", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.projects.all }),
  });
}

export function useUpdateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...data }: { id: string } & Partial<Project>) =>
      apiClient.put<Project>(`/projects/${id}`, data),
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.projects.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.projects.detail(id) });
    },
  });
}

export function useDeleteProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.delete(`/projects/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.projects.all }),
  });
}

// ─── Deployments ──────────────────────────────────────────────────────────────

// Backend returns PascalCase trigger ("GitPush") → frontend uses snake_case ("git_push")
const triggerMap: Record<string, string> = {
  GitPush: "git_push", Manual: "manual", Api: "api",
  Schedule: "schedule", Rollback: "rollback",
};
// Backend DeploymentStatus enum → lowercase frontend values ("RolledBack" → "rolled_back")
// Parse .NET TimeSpan string ("HH:MM:SS.fffffff" or "D.HH:MM:SS") to seconds
function parseTimeSpanToSeconds(ts: any): number | null {
  if (!ts) return null;
  if (typeof ts === 'number') return ts;
  const m = String(ts).match(/^(-?)(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})/);
  if (!m) return null;
  const [, neg, days, h, min, sec] = m;
  const total = (parseInt(days || '0') * 86400) + (parseInt(h) * 3600) + (parseInt(min) * 60) + parseInt(sec);
  return neg === '-' ? -total : total;
}

function normalizeDeployment(dto: any): Deployment {
  return {
    ...dto,
    status: dto.status?.replace(/([a-z])([A-Z])/g, "$1_$2").toLowerCase() as Deployment["status"],
    trigger: (triggerMap[dto.trigger] ?? dto.trigger?.toLowerCase()) as Deployment["trigger"],
    duration: parseTimeSpanToSeconds(dto.duration),
  };
}

export function useDeployments(params?: {
  projectId?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: queryKeys.deployments.list(params),
    queryFn: async () => {
      const result = await apiClient.get<any>("/deployments", { params });
      const items: any[] = Array.isArray(result) ? result : (result?.data ?? []);
      return {
        ...(Array.isArray(result) ? { total: items.length, page: 1, pageSize: items.length, totalPages: 1 } : result),
        data: items.map(normalizeDeployment),
      } as PaginatedResponse<Deployment>;
    },
    staleTime: 30_000,
  });
}

export function useDeployment(id: string) {
  return useQuery({
    queryKey: queryKeys.deployments.detail(id),
    queryFn: async () => {
      const dto = await apiClient.get<any>(`/deployments/${id}`);
      return normalizeDeployment(dto) as Deployment;
    },
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useCreateDeployment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { projectId: string; branch?: string; trigger?: string }) =>
      apiClient.post<Deployment>("/deployments", data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.deployments.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.projects.all });
    },
  });
}

export function useRollbackDeployment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ deploymentId }: { projectId: string; deploymentId: string }) =>
      apiClient.post<Deployment>(`/deployments/${deploymentId}/rollback`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.deployments.all }),
  });
}

export function useCancelDeployment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.post(`/deployments/${id}/cancel`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.deployments.all }),
  });
}

// ─── Servers ──────────────────────────────────────────────────────────────────

export function useServers() {
  return useQuery({
    queryKey: queryKeys.servers.list(),
    queryFn: async () => {
      const result = await apiClient.get<any>("/servers");
      const items: any[] = Array.isArray(result) ? result : ((result as any).data ?? []);
      return items.map((s: any): Server => ({
        ...s,
        status: s.status?.toLowerCase() as Server["status"],
        provider: s.provider?.toLowerCase() as Server["provider"],
        // backend uses cpuCores/memoryGb/diskGb; frontend type uses cpu/memoryGB/diskGB
        cpu: s.cpu ?? s.cpuCores ?? 0,
        memoryGB: s.memoryGB ?? s.memoryGb ?? 0,
        diskGB: s.diskGB ?? s.diskGb ?? 0,
        port: s.port ?? s.sshPort ?? 22,
        // Wrap usage percentages into metrics object so ServerCard renders the bars
        metrics: {
          cpuUsagePercent: s.cpuUsagePercent ?? 0,
          memoryUsagePercent: s.memoryUsagePercent ?? 0,
          diskUsagePercent: s.diskUsagePercent ?? 0,
          activeContainers: s.activeContainers ?? 0,
          networkInMbps: 0,
          networkOutMbps: 0,
          uptimeSeconds: 0,
          loadAverage: [],
          timestamp: s.lastHealthCheckAt ?? new Date().toISOString(),
        },
      }));
    },
    staleTime: 60_000,
  });
}

export function useServer(id: string) {
  return useQuery({
    queryKey: queryKeys.servers.detail(id),
    queryFn: async () => {
      const s = await apiClient.get<any>(`/servers/${id}`);
      return {
        ...s,
        status: s.status?.toLowerCase() as Server["status"],
        provider: s.provider?.toLowerCase() as Server["provider"],
        cpu: s.cpu ?? s.cpuCores ?? 0,
        memoryGB: s.memoryGB ?? s.memoryGb ?? 0,
        diskGB: s.diskGB ?? s.diskGb ?? 0,
        port: s.port ?? s.sshPort ?? 22,
        os: s.os ?? undefined,
        dockerVersion: s.dockerVersion ?? undefined,
        // Nest the stored usage percentages so server detail page fallback works
        metrics: {
          cpuUsagePercent: s.cpuUsagePercent ?? 0,
          memoryUsagePercent: s.memoryUsagePercent ?? 0,
          diskUsagePercent: s.diskUsagePercent ?? 0,
          activeContainers: s.activeContainers ?? 0,
        },
      } as Server;
    },
    enabled: !!id,
    staleTime: 60_000,
  });
}

export function useServerMetrics(id: string) {
  return useQuery({
    queryKey: queryKeys.servers.metrics(id),
    queryFn: async () => {
      // Backend returns List<ServerMetricsDto> with raw byte values — use the latest entry.
      const result = await apiClient.get<any>(`/servers/${id}/metrics`);
      const items: any[] = Array.isArray(result) ? result : [];
      if (items.length === 0) return null;
      const latest = items[items.length - 1];
      return {
        cpuUsagePercent: latest.cpuUsagePercent ?? 0,
        memoryUsagePercent: latest.memoryTotalBytes > 0
          ? (latest.memoryUsageBytes / latest.memoryTotalBytes) * 100
          : 0,
        diskUsagePercent: latest.diskTotalBytes > 0
          ? (latest.diskUsageBytes / latest.diskTotalBytes) * 100
          : 0,
        activeContainers: latest.activeContainers ?? 0,
        networkInMbps: latest.networkInMbps ?? 0,
        networkOutMbps: latest.networkOutMbps ?? 0,
        uptimeSeconds: latest.uptimeSeconds ?? 0,
        loadAverage: latest.loadAverage ?? [],
        timestamp: latest.timestamp ?? new Date().toISOString(),
      } as ServerMetrics;
    },
    enabled: !!id,
    staleTime: 10_000,
    refetchInterval: MOCK ? false : 10000, // 10s live refresh
  });
}

export function useCreateServer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<Server>) => apiClient.post<Server>("/servers", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.servers.all }),
  });
}

export function useRemoveServer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.delete(`/servers/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.servers.all }),
  });
}

export interface ContainerInfo {
  id: string;
  name: string;
  image: string;
  status: string;
  ports: string;
}

export function useServerContainers(serverId: string, enabled = true) {
  return useQuery({
    queryKey: [...queryKeys.servers.detail(serverId), "containers"],
    queryFn: async (): Promise<ContainerInfo[]> => {
      const result = await apiClient.post<{ stdOut: string; stdErr: string; exitCode: number; success: boolean }>(
        `/servers/${serverId}/exec`,
        { command: "docker ps --format '{{.ID}}|{{.Names}}|{{.Image}}|{{.Status}}|{{.Ports}}'" }
      );
      if (!result.stdOut?.trim()) return [];
      return result.stdOut
        .trim()
        .split("\n")
        .filter(Boolean)
        .map((line) => {
          const [id = "", name = "", image = "", status = "", ports = ""] = line.split("|");
          return { id: id.substring(0, 12), name, image, status, ports };
        });
    },
    enabled: !!serverId && enabled,
    staleTime: 15_000,
    refetchInterval: enabled ? 15_000 : false,
  });
}

// ─── Services ─────────────────────────────────────────────────────────────────

export function useServices(projectId?: string) {
  return useQuery({
    queryKey: queryKeys.services.list(projectId),
    queryFn: async () => {
      const result = await apiClient.get<any[]>("/services", { params: projectId ? { projectId } : {} });
      return (result ?? []).map(normalizeService);
    },
  });
}

export function useService(id: string) {
  return useQuery({
    queryKey: queryKeys.services.detail(id),
    queryFn: async () => normalizeService(await apiClient.get<any>(`/services/${id}`)),
    enabled: !!id,
  });
}

export function useCreateService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { projectId: string; name: string; type: string; image?: string; tag?: string }) =>
      apiClient.post<Service>("/services", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.services.all }),
  });
}

export function useDeleteService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.delete(`/services/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.services.all }),
  });
}

export function useStartService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.post(`/services/${id}/start`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.services.all }),
  });
}

export function useStopService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.post(`/services/${id}/stop`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.services.all }),
  });
}

export function useRestartService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.post(`/services/${id}/restart`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.services.all }),
  });
}

export function useServiceScalingPolicy(id: string) {
  return useQuery({
    queryKey: ["services", "scaling-policy", id],
    queryFn: () => apiClient.get<ServiceScalingPolicy>(`/services/${id}/scaling-policy`),
    enabled: !!id,
  });
}

export function useUpdateServiceScalingPolicy() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      id,
      ...data
    }: {
      id: string;
      minReplicas: number;
      maxReplicas: number;
      cpuTargetPercentage?: number;
      memoryTargetPercentage?: number;
      triggerReason?: string;
      lastScalingAction?: string;
    }) => apiClient.put<ServiceScalingPolicy>(`/services/${id}/scaling-policy`, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.services.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.services.detail(variables.id) });
      queryClient.invalidateQueries({ queryKey: ["services", "scaling-policy", variables.id] });
    },
  });
}

// ─── Databases ────────────────────────────────────────────────────────────────

export function useDatabases() {
  return useQuery({
    queryKey: queryKeys.databases.list(),
    queryFn: async () => {
      const result = await apiClient.get<any>("/databases");
      const items: any[] = Array.isArray(result) ? result : ((result as any).data ?? []);
      return items.map((d: any): Database => ({
        ...d,
        status: d.status?.toLowerCase() as Database["status"],
        type: d.engine?.toLowerCase() as Database["type"] ?? d.type?.toLowerCase(),
      }));
    },
    staleTime: 60_000,
  });
}

export function useDatabase(id: string) {
  return useQuery({
    queryKey: queryKeys.databases.detail(id),
    queryFn: () => apiClient.get<Database>(`/databases/${id}`),
    enabled: !!id,
    staleTime: 60_000,
  });
}

export function useCreateDatabase() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<Database>) => apiClient.post<Database>("/databases", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.databases.all }),
  });
}

export function useTriggerBackup() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.post(`/databases/${id}/backup`, {}),
    onSuccess: (_, id) =>
      queryClient.invalidateQueries({ queryKey: queryKeys.databases.detail(id) }),
  });
}

export function useDeleteDatabase() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.delete(`/databases/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.databases.all }),
  });
}

// ─── Pipelines ────────────────────────────────────────────────────────────────

function normalizePipeline(dto: any): Pipeline {
  const triggerRaw = (dto.trigger ?? "manual") as string;
  const triggerType = triggerRaw.toLowerCase() as Pipeline["trigger"]["type"];

  return {
    ...dto,
    status: dto.status?.toLowerCase() as Pipeline["status"],
    trigger: {
      type: (triggerType === "pullrequest" ? "pr" : triggerType) as Pipeline["trigger"]["type"],
      schedule: dto.cronExpression ?? undefined,
    },
    stages: (dto.stages ?? []).map((s: any) => ({
      ...s,
      steps: s.steps ?? [],
      runParallel: s.runParallel ?? false,
    })),
    lastRunDuration: typeof dto.lastDuration === "number" ? dto.lastDuration : undefined,
    isEnabled: dto.isEnabled ?? true,
  } as Pipeline;
}

export function usePipelines(projectId?: string) {
  return useQuery({
    queryKey: queryKeys.pipelines.list(projectId),
    queryFn: async () => {
      const result = await apiClient.get<any>("/pipelines", { params: projectId ? { projectId } : {} });
      const items: any[] = Array.isArray(result) ? result : ((result as any).data ?? []);
      return items.map(normalizePipeline);
    },
    staleTime: 60_000,
  });
}

export function usePipeline(id: string) {
  return useQuery({
    queryKey: queryKeys.pipelines.detail(id),
    queryFn: async () => normalizePipeline(await apiClient.get<any>(`/pipelines/${id}`)),
    enabled: !!id,
    staleTime: 60_000,
  });
}

export function useTriggerPipeline() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.post(`/pipelines/${id}/trigger`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.pipelines.all }),
  });
}

export function useUpdatePipeline() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...data }: { id: string; name: string; description?: string; trigger: string; cronExpression?: string; isEnabled: boolean }) =>
      apiClient.put(`/pipelines/${id}`, data),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.pipelines.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.pipelines.detail(vars.id) });
    },
  });
}

export function useStartPipelineRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (pipelineId: string) => apiClient.post<PipelineRun>(`/pipelines/${pipelineId}/runs`, {}),
    onSuccess: (run) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.pipelines.all });
      if (run?.pipelineId) {
        queryClient.invalidateQueries({ queryKey: queryKeys.pipelines.runs(run.pipelineId) });
      }
    },
  });
}

export function usePipelineRuns(pipelineId: string) {
  return useQuery({
    queryKey: queryKeys.pipelines.runs(pipelineId),
    queryFn: () => apiClient.get<PipelineRun[]>(`/pipelines/${pipelineId}/runs`),
    enabled: !!pipelineId,
    staleTime: 10_000,
    refetchInterval: MOCK ? false : 10000,
  });
}

export function usePipelineRun(runId: string) {
  return useQuery({
    queryKey: queryKeys.pipelineRuns.detail(runId),
    queryFn: () => apiClient.get<PipelineRun>(`/pipeline-runs/${runId}`),
    enabled: !!runId,
    staleTime: 10_000,
    refetchInterval: MOCK ? false : 10000,
  });
}

export function usePipelineRunLogs(runId: string, page = 1, pageSize = 200) {
  return useQuery({
    queryKey: queryKeys.pipelineRuns.logs(runId, page, pageSize),
    queryFn: async () => {
      const res = await apiClient.get<any>(`/pipeline-runs/${runId}/logs`, { params: { page, pageSize } });
      return {
        data: (res?.data ?? []) as PipelineRunLog[],
        total: res?.total ?? 0,
        page: res?.page ?? page,
        pageSize: res?.pageSize ?? pageSize,
        totalPages: res?.totalPages ?? 1,
      };
    },
    enabled: !!runId,
    staleTime: 5_000,
    refetchInterval: MOCK ? false : 5000,
  });
}

export function useCancelPipelineRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (runId: string) => apiClient.post<PipelineRun>(`/pipeline-runs/${runId}/cancel`, {}),
    onSuccess: (run) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.pipelineRuns.detail(run.id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.pipelineRuns.logs(run.id, 1, 200) });
      queryClient.invalidateQueries({ queryKey: queryKeys.pipelines.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.pipelines.runs(run.pipelineId) });
    },
  });
}

// ─── Alerts ───────────────────────────────────────────────────────────────────

export function useAlerts() {
  return useQuery({
    queryKey: queryKeys.alerts.list(),
    queryFn: async () => {
      const result = await apiClient.get<any>("/alerts");
      const items: any[] = Array.isArray(result) ? result : ((result as any).data ?? []);
      return items.map((a: any): Alert => ({
        ...a,
        status: a.status?.toLowerCase() as Alert["status"],
        severity: a.severity?.toLowerCase() as Alert["severity"],
        conditions: a.conditions ?? [],
      }));
    },
    staleTime: 30_000,
    refetchInterval: MOCK ? false : 30000,
  });
}

export function useAcknowledgeAlert() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.post(`/alerts/${id}/acknowledge`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.alerts.all }),
  });
}

// ─── Domains ──────────────────────────────────────────────────────────────────

export function useDomains() {
  return useQuery({
    queryKey: queryKeys.domains.list(),
    queryFn: async () => {
      const result = await apiClient.get<PaginatedResponse<Domain> | Domain[]>("/domains");
      return Array.isArray(result) ? result : ((result as PaginatedResponse<Domain>).data ?? []);
    },
    staleTime: 60_000,
  });
}

export function useCreateDomain() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<Domain>) => apiClient.post<Domain>("/domains", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.domains.all }),
  });
}

// ─── Environment Variables ────────────────────────────────────────────────────

export function useEnvVars(projectId: string) {
  return useQuery({
    queryKey: queryKeys.envVars(projectId),
    queryFn: () => apiClient.get<EnvVariable[]>(`/projects/${projectId}/env`),
    enabled: !!projectId,
    staleTime: 60_000,
  });
}

export function useUpsertEnvVar(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<EnvVariable>) =>
      apiClient.post<EnvVariable>(`/projects/${projectId}/env`, data),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: queryKeys.envVars(projectId) }),
  });
}

// ─── Metrics ──────────────────────────────────────────────────────────────────

export function useServerMetricHistory(
  serverId: string,
  metric: string,
  timeRange = "1h"
) {
  return useQuery({
    queryKey: queryKeys.metrics(serverId, `${metric}-${timeRange}`),
    queryFn: () =>
      apiClient.get<MetricSeries[]>(`/servers/${serverId}/metrics/history`, {
        params: { metric, timeRange },
      }),
    enabled: !!serverId,
    staleTime: 10_000,
    refetchInterval: MOCK ? false : 30000,
  });
}

// ─── Team ─────────────────────────────────────────────────────────────────────

export function useTeamMembers() {
  return useQuery({
    queryKey: queryKeys.team(),
    queryFn: async () => {
      const data = await apiClient.get<any[]>("/team");
      // Backend returns flat TeamMemberDto; normalize to nested TeamMember shape
      return (data ?? []).map((m: any): TeamMember => ({
        id: m.id,
        userId: m.id,
        user: {
          id: m.id,
          name: m.name ?? "",
          email: m.email ?? "",
          avatarUrl: m.avatarUrl || undefined,
          role: m.role,
          tenantId: "",
          isActive: m.status === "active",
          createdAt: m.createdAt,
          lastLoginAt: m.joinedAt || undefined,
          twoFactorEnabled: false,
        },
        role: m.role,
        joinedAt: m.joinedAt ?? m.createdAt,
        permissions: [],
      }));
    },
    staleTime: 5 * 60_000,
  });
}

export function useInviteTeamMember() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { email: string; role: string }) =>
      apiClient.post("/team/invite", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.team() }),
  });
}

// ─── SSH Keys ─────────────────────────────────────────────────────────────────

export function useSshKeys() {
  return useQuery({
    queryKey: queryKeys.sshKeys(),
    queryFn: () => apiClient.get<SshKey[]>("/ssh-keys"),
    staleTime: 5 * 60_000,
  });
}

export function useCreateSshKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { name: string; privateKey: string; passphrase?: string }) =>
      apiClient.post<SshKey>("/ssh-keys", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.sshKeys() }),
  });
}

// ─── Notifications ────────────────────────────────────────────────────────────

export function useNotificationConfigs() {
  return useQuery({
    queryKey: queryKeys.notifications(),
    queryFn: () => apiClient.get<NotificationConfig[]>("/notifications/config"),
    staleTime: 5 * 60_000,
  });
}

// ─── Audit Logs ───────────────────────────────────────────────────────────────

export function useAuditLogs(params?: { page?: number; pageSize?: number; resourceType?: string }) {
  return useQuery({
    queryKey: queryKeys.auditLogs(params),
    queryFn: () =>
      apiClient.get<PaginatedResponse<AuditLog>>("/audit-logs", { params }),
    staleTime: 60_000,
  });
}

// ─── Cost Monitoring ──────────────────────────────────────────────────────────

export interface CostTrendPoint {
  date: string;
  amount: number;
  forecast?: number;
}

export interface CostByCategory {
  category: string;
  amount: number;
  percent: number;
}

export interface CostAnomaly {
  date: string;
  resourceType: string;
  amount: number;
  expectedAmount: number;
  zScore: number;
}

export interface CostOptimizationTip {
  title: string;
  description: string;
  estimatedSavings: number;
}

export interface CostDashboard {
  totalPeriodCost: number;
  lineItemCount: number;
  currentMonthCost: number;
  forecastMonthCost: number;
  changePercent: number;
  trend: CostTrendPoint[];
  byCategory: CostByCategory[];
  anomalies: CostAnomaly[];
  optimizationTips: CostOptimizationTip[];
}

export function useCostRecords(params?: { period?: string; projectId?: string }) {
  return useQuery({
    queryKey: queryKeys.costs(params),
    // Map backend CostRecordDto fields to the shape the old costs page expected
    queryFn: async () => {
      const items = await apiClient.get<any[]>("/costs", { params });
      return (items ?? []).map((r: any): CostRecord => ({
        ...r,
        provider: r.provider ?? r.resourceType ?? "server",
        totalCost: r.totalCost ?? r.amount ?? 0,
      }));
    },
    staleTime: 5 * 60_000,
  });
}

export function useCostDashboard(period = "3m") {
  return useQuery({
    queryKey: [...queryKeys.costs({ period }), "dashboard"],
    queryFn: () => apiClient.get<CostDashboard>("/costs/dashboard", { params: { period } }),
    staleTime: 5 * 60_000,
  });
}

// ─── Dashboard Stats ──────────────────────────────────────────────────────────

export interface DashboardStats {
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

export function useDashboardStats() {
  return useQuery({
    queryKey: queryKeys.stats(),
    queryFn: () => apiClient.get<DashboardStats>("/stats/dashboard"),
    staleTime: 30_000,
    refetchInterval: MOCK ? false : 60000,
  });
}

// ─── Missing hooks (aliases + new) ───────────────────────────────────────────

export function useResolveAlert() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.post(`/alerts/${id}/resolve`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.alerts.all }),
  });
}

export interface AggregatedContainer extends ContainerInfo {
  serverId: string;
  serverName: string;
}

export function useContainers(projectId?: string) {
  // Fetch all online servers and aggregate docker ps from each
  const { data: servers } = useServers();
  const onlineServers = servers?.filter((s) => s.status === "online") ?? [];

  return useQuery({
    queryKey: ["containers", "aggregated", projectId, onlineServers.map((s) => s.id).join(",")],
    queryFn: async (): Promise<AggregatedContainer[]> => {
      if (!onlineServers.length) return [];
      const results = await Promise.allSettled(
        onlineServers.map(async (server) => {
          const result = await apiClient.post<{ stdOut: string; stdErr: string; exitCode: number; success: boolean }>(
            `/servers/${server.id}/exec`,
            { command: "docker ps --format '{{.ID}}|{{.Names}}|{{.Image}}|{{.Status}}|{{.Ports}}|{{.RunningFor}}'" }
          );
          if (!result.stdOut?.trim()) return [] as AggregatedContainer[];
          return result.stdOut
            .trim()
            .split("\n")
            .filter(Boolean)
            .map((line): AggregatedContainer => {
              const [id = "", name = "", image = "", status = "", ports = "", since = ""] = line.split("|");
              return {
                id: id.substring(0, 12),
                name,
                image,
                status,
                ports,
                serverId: server.id,
                serverName: server.name,
              };
            });
        })
      );
      return results
        .filter((r): r is PromiseFulfilledResult<AggregatedContainer[]> => r.status === "fulfilled")
        .flatMap((r) => r.value);
    },
    enabled: onlineServers.length > 0,
    staleTime: 30_000,
    refetchInterval: 30_000,
  });
}

export function useAddDomain() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<Domain>) => apiClient.post<Domain>("/domains", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.domains.all }),
  });
}

export function useDeleteDomain() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.delete(`/domains/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.domains.all }),
  });
}

export function useInviteMember() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { email: string; name: string; role: string }) =>
      apiClient.post("/team/invite", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["team"] }),
  });
}

export function useUpdateMemberRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: string }) =>
      apiClient.put(`/team/${userId}/role`, { role }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["team"] }),
  });
}

export function useRemoveMember() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => apiClient.delete(`/team/${userId}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["team"] }),
  });
}

export function useDeleteSshKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.delete(`/ssh-keys/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.sshKeys() }),
  });
}

export function useVolumes(projectId?: string) {
  return useQuery({
    queryKey: ["volumes", projectId],
    queryFn: () =>
      apiClient.get<Volume[]>("/volumes", projectId ? { params: { projectId } } : undefined),
    staleTime: 60_000,
  });
}

export function useCreateVolume() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { name: string; mountPath?: string; driver?: string; projectId?: string }) =>
      apiClient.post("/volumes", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["volumes"] }),
  });
}

export function useDeleteVolume() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.delete(`/volumes/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["volumes"] }),
  });
}

export function useAttachVolume() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { id: string; projectId?: string; serviceId?: string; mountPath?: string }) =>
      apiClient.post(`/volumes/${data.id}/attach`, {
        projectId: data.projectId,
        serviceId: data.serviceId,
        mountPath: data.mountPath,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["volumes"] }),
  });
}

export function useDetachVolume() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.post(`/volumes/${id}/detach`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["volumes"] }),
  });
}

export function useLogs(params?: {
  level?: string;
  service?: string;
  search?: string;
  cursor?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: ["logs", params],
    queryFn: () => apiClient.get<LogsResponse>("/logs", { params }),
    staleTime: 5_000,
  });
}

export function useMonitoringSummary(serverId?: string, range: string = "1h") {
  return useQuery({
    queryKey: ["monitoring", "summary", serverId, range],
    queryFn: () => apiClient.get<MonitoringSummary>("/monitoring/summary", { params: { serverId, range } }),
    staleTime: 10_000,
  });
}

export function useMonitoringTimeSeries(metric: string, serverId?: string, range: string = "1h") {
  return useQuery({
    queryKey: ["monitoring", "timeseries", metric, serverId, range],
    queryFn: () => apiClient.get<MonitoringPoint[]>("/monitoring/timeseries", { params: { metric, serverId, range } }),
    staleTime: 10_000,
  });
}

export function useMonitoringNetwork(serverId?: string, range: string = "1h") {
  return useQuery({
    queryKey: ["monitoring", "network", serverId, range],
    queryFn: () => apiClient.get<MonitoringNetworkPoint[]>("/monitoring/network", { params: { serverId, range } }),
    staleTime: 10_000,
  });
}

// ─── Integrations ──────────────────────────────────────────────────────────────

export interface IntegrationStatus {
  name: string;
  isConnected: boolean;
  username?: string;
  connectedAt?: string;
}

export function useDockerHubStatus() {
  return useQuery({
    queryKey: ["integrations", "docker-hub"],
    queryFn: () => apiClient.get<IntegrationStatus>("/integrations/docker-hub"),
  });
}

export function useConnectDockerHub() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { username: string; accessToken: string }) =>
      apiClient.post<IntegrationStatus>("/integrations/docker-hub", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["integrations", "docker-hub"] }),
  });
}

export function useDisconnectDockerHub() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => apiClient.delete("/integrations/docker-hub"),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["integrations", "docker-hub"] }),
  });
}

export function useVerifyDockerHub() {
  return useMutation({
    mutationFn: (data: { username: string; accessToken: string }) =>
      apiClient.post<IntegrationStatus>("/integrations/docker-hub/verify", data),
  });
}

export function useConnectSlack() {
  return useMutation({
    mutationFn: (data: { webhookUrl: string }) =>
      apiClient.post<IntegrationStatus>("/integrations/slack", data),
  });
}

export function useConnectAws() {
  return useMutation({
    mutationFn: (data: { accessKeyId: string; secretAccessKey: string; region: string }) =>
      apiClient.post<IntegrationStatus>("/integrations/aws", data),
  });
}

export function useConnectGrafana() {
  return useMutation({
    mutationFn: (data: { url: string; apiToken: string }) =>
      apiClient.post<IntegrationStatus>("/integrations/grafana", data),
  });
}

export interface EmailNotificationConfig {
  emailEnabled: boolean;
  deploymentSuccess: boolean;
  deploymentFailure: boolean;
  email?: string;
}

export function useEmailNotificationConfig() {
  return useQuery({
    queryKey: ["notifications", "email-config"],
    queryFn: () => apiClient.get<EmailNotificationConfig>("/notifications/config"),
  });
}

export function useSaveEmailNotificationConfig() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { emailEnabled: boolean; deploymentSuccess: boolean; deploymentFailure: boolean }) =>
      apiClient.post("/notifications/config", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["notifications", "email-config"] }),
  });
}

// ─── API Keys ──────────────────────────────────────────────────────────────────

export interface ApiKey {
  id: string;
  name: string;
  keyPrefix: string;
  permissions: string;
  createdAt: string;
  lastUsed?: string;
  isEnabled: boolean;
}

export interface CreateApiKeyResult {
  key: ApiKey;
  fullKey: string;
}

export function useApiKeys() {
  return useQuery({
    queryKey: ["api-keys"],
    queryFn: () => apiClient.get<ApiKey[]>("/api-keys"),
  });
}

export function useCreateApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { name: string; permissions?: string }) =>
      apiClient.post<CreateApiKeyResult>("/api-keys", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["api-keys"] }),
  });
}

export function useDeleteApiKey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.delete(`/api-keys/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["api-keys"] }),
  });
}

// ─── S3 Destinations ──────────────────────────────────────────────────────────

export function useS3Destinations() {
  return useQuery({
    queryKey: ["s3-destinations"],
    queryFn: () => apiClient.get<S3Destination[]>("/databases/s3-destinations"),
  });
}

export function useS3Destination(id: string) {
  return useQuery({
    queryKey: ["s3-destinations", id],
    queryFn: () => apiClient.get<S3Destination>(`/databases/s3-destinations/${id}`),
    enabled: !!id,
  });
}

export function useCreateS3Destination() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: {
      name: string;
      description?: string;
      endpoint: string;
      bucketName: string;
      region?: string;
      accessKeyId: string;
      secretAccessKey: string;
      isDefault: boolean;
    }) => apiClient.post<S3Destination>("/databases/s3-destinations", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["s3-destinations"] }),
  });
}

export function useUpdateS3Destination() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: {
      id: string;
      name: string;
      description?: string;
      endpoint: string;
      bucketName: string;
      region?: string;
      accessKeyId?: string;
      secretAccessKey?: string;
      isDefault: boolean;
    }) =>
      apiClient.put<S3Destination>(
        `/databases/s3-destinations/${data.id}`,
        data
      ),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["s3-destinations"] });
    },
  });
}

export function useDeleteS3Destination() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) =>
      apiClient.delete<void>(`/databases/s3-destinations/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["s3-destinations"] }),
  });
}

export function useTestS3Destination() {
  return useMutation({
    mutationFn: (data: {
      endpoint: string;
      bucketName: string;
      region?: string;
      accessKeyId: string;
      secretAccessKey: string;
    }) =>
      apiClient.post<{ isConnected: boolean; message: string }>(
        "/databases/s3-destinations/test",
        data
      ),
  });
}

// ─── Backup Policy ────────────────────────────────────────────────────────────

export function useBackupPolicy(databaseId: string) {
  return useQuery({
    queryKey: ["backup-policy", databaseId],
    queryFn: () =>
      apiClient.get<BackupPolicy>(`/databases/${databaseId}/backup-policy`),
    enabled: !!databaseId,
  });
}

export function useUpdateBackupPolicy() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: {
      databaseId: string;
      isEnabled: boolean;
      cronExpression: string;
      retentionDays: number;
      s3DestinationId?: string;
      storageLocation: "local" | "s3";
    }) =>
      apiClient.put<BackupPolicy>(
        `/databases/${data.databaseId}/backup-policy`,
        data
      ),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["backup-policy", variables.databaseId] });
    },
  });
}

// ─── Restore Jobs ────────────────────────────────────────────────────────────

export function useRestoreJobs(databaseId: string) {
  return useQuery({
    queryKey: ["restore-jobs", databaseId],
    queryFn: () =>
      apiClient.get<RestoreJob[]>(`/databases/${databaseId}/restore/jobs`),
    enabled: !!databaseId,
  });
}

export function useRestoreJob(databaseId: string, jobId: string) {
  return useQuery({
    queryKey: ["restore-jobs", databaseId, jobId],
    queryFn: () =>
      apiClient.get<RestoreJob>(`/databases/${databaseId}/restore/jobs/${jobId}`),
    enabled: !!databaseId && !!jobId,
  });
}

export function useCreateRestoreJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: {
      databaseId: string;
      backupId: string;
      targetDatabaseName: string;
    }) =>
      apiClient.post<RestoreJob>(`/databases/${data.databaseId}/restore/jobs`, {
        backupId: data.backupId,
        targetDatabaseName: data.targetDatabaseName,
      }),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["restore-jobs", variables.databaseId] });
    },
  });
}

// ─── Container Actions ───────────────────────────────────────────────────────────

export function useStartContainer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { serverId: string; containerId: string }) =>
      apiClient.post(`/servers/${data.serverId}/containers/${data.containerId}/start`, {}),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["containers", variables.serverId] });
    },
  });
}

export function useStopContainer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { serverId: string; containerId: string }) =>
      apiClient.post(`/servers/${data.serverId}/containers/${data.containerId}/stop`, {}),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["containers", variables.serverId] });
    },
  });
}

export function useRestartContainer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { serverId: string; containerId: string }) =>
      apiClient.post(`/servers/${data.serverId}/containers/${data.containerId}/restart`, {}),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["containers", variables.serverId] });
    },
  });
}

export function useRemoveContainer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { serverId: string; containerId: string }) =>
      apiClient.delete(`/servers/${data.serverId}/containers/${data.containerId}`),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["containers", variables.serverId] });
    },
  });
}

export function useContainerLogs(serverId: string, containerId: string, enabled: boolean = true) {
  return useQuery({
    queryKey: ["container-logs", serverId, containerId],
    queryFn: () =>
      apiClient.get<{ logs: string }>(`/servers/${serverId}/containers/${containerId}/logs`),
    enabled: enabled && !!serverId && !!containerId,
    staleTime: 0,
  });
}
