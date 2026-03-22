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

// ─── Services ─────────────────────────────────────────────────────────────────

export function useServices(projectId?: string) {
  return useQuery({
    queryKey: queryKeys.services.list(projectId),
    queryFn: () =>
      apiClient.get<Service[]>("/services", { params: projectId ? { projectId } : {} }),
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

// ─── Pipelines ────────────────────────────────────────────────────────────────

export function usePipelines(projectId?: string) {
  return useQuery({
    queryKey: queryKeys.pipelines.list(projectId),
    queryFn: async () => {
      const result = await apiClient.get<any>("/pipelines", { params: projectId ? { projectId } : {} });
      const items: any[] = Array.isArray(result) ? result : ((result as any).data ?? []);
      return items.map((p: any): Pipeline => ({
        ...p,
        status: p.status?.toLowerCase() as Pipeline["status"],
        trigger: {
          ...p.trigger,
          type: p.trigger?.type?.toLowerCase() as Pipeline["trigger"]["type"],
        },
        stages: p.stages ?? [],
      }));
    },
    staleTime: 60_000,
  });
}

export function usePipeline(id: string) {
  return useQuery({
    queryKey: queryKeys.pipelines.detail(id),
    queryFn: () => apiClient.get<Pipeline>(`/pipelines/${id}`),
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

export function useCostRecords(params?: { period?: string; projectId?: string }) {
  return useQuery({
    queryKey: queryKeys.costs(params),
    queryFn: () => apiClient.get<CostRecord[]>("/costs", { params }),
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

export function useContainers(projectId?: string) {
  return useQuery({
    queryKey: ["containers", projectId],
    queryFn: () =>
      apiClient.get<Container[]>("/containers", projectId ? { params: { projectId } } : undefined),
    staleTime: 30_000,
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
