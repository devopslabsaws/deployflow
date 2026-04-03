"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Activity, AlertTriangle, CheckCircle2, XCircle, Loader2,
  Plus, Trash2, RefreshCw, ChevronRight, Clock, Filter,
  Bug, Zap, Eye, FileText,
} from "lucide-react";
import { BarChart3, ShieldCheck } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { apiClient } from "@/lib/api-client";
import { cn } from "@/lib/utils";
import { toast } from "sonner";

interface OtelTrace {
  id: string;
  traceId: string;
  spanId: string;
  parentSpanId?: string;
  serviceName: string;
  operationName: string;
  startTime: string;
  endTime?: string;
  durationMs: number;
  statusCode: string;
  statusMessage?: string;
  spanCount: number;
  errorSpanCount: number;
  traceAttributes?: string;
  createdAt: string;
}

interface TraceSummary {
  totalTraces: number;
  errorTraces: number;
  averageDurationMs: number;
  p99DurationMs: number;
  servicesTracked: number;
}

interface LogRule {
  id: string;
  name: string;
  pattern: string;
  action: string;
  severity: string;
  isEnabled: boolean;
  matchCount: number;
  lastMatchedAt?: string;
  createdAt: string;
}

interface CrashReport {
  id: string;
  serviceName: string;
  crashMessage: string;
  stackTrace?: string;
  rcaSummary?: string;
  triggeringRuleId?: string;
  isResolved: boolean;
  occurrenceCount: number;
  firstSeenAt: string;
  lastSeenAt: string;
  resolvedAt?: string;
}

interface ProjectSloDto {
  id: string;
  projectId: string;
  projectName: string;
  uptimeTargetPercent: number;
  p95LatencyMs: number;
  errorRateBudgetPercent: number;
  windowDays: number;
  isEnabled: boolean;
  currentUptimePercent?: number;
  errorBudgetRemainingPercent?: number;
  lastEvaluatedAt?: string;
  isBreaching: boolean;
  errorBudgetBurnRate?: number;
}

interface SloEventCorrelation {
  deployedAt: string;
  projectName: string;
  branch: string;
  commitSha: string;
  status: string;
}

interface SloReportDto {
  projectId: string;
  projectName: string;
  uptimeTargetPercent: number;
  windowDays: number;
  currentUptimePercent?: number;
  errorBudgetRemainingPercent?: number;
  isBreaching: boolean;
  recentDeployEvents: SloEventCorrelation[];
  generatedAt: string;
}

function DurationBadge({ ms }: { ms: number }) {
  const color = ms > 2000 ? "text-destructive" : ms > 500 ? "text-amber-400" : "text-emerald-400";
  return <span className={cn("font-mono text-xs tabular-nums", color)}>{ms < 1000 ? `${ms}ms` : `${(ms / 1000).toFixed(2)}s`}</span>;
}

function StatusBadge({ status }: { status: string }) {
  const ok = status === "OK" || status === "STATUS_CODE_OK";
  return (
    <Badge className={cn("gap-1 text-[10px]", ok ? "bg-emerald-500/10 text-emerald-400 border-emerald-500/30" : "bg-destructive/10 text-destructive border-destructive/30")}>
      {ok ? <CheckCircle2 className="h-3 w-3" /> : <XCircle className="h-3 w-3" />}
      {ok ? "OK" : status}
    </Badge>
  );
}

export default function ObservabilityPage() {
  const qc = useQueryClient();
  const [ruleOpen, setRuleOpen] = useState(false);
  const [selectedTrace, setSelectedTrace] = useState<OtelTrace | null>(null);
  const [ruleForm, setRuleForm] = useState({ name: "", pattern: "", action: "Alert", severity: "Warning", isEnabled: true });
  const [serviceFilter, setServiceFilter] = useState<string>("");
  const [selectedSlo, setSelectedSlo] = useState<ProjectSloDto | null>(null);
  const [sloReport, setSloReport] = useState<SloReportDto | null>(null);
  const [sloReportLoading, setSloReportLoading] = useState(false);

  /* ── Queries ── */
    const { data: slos, isLoading: slosLoading } = useQuery<ProjectSloDto[]>({
      queryKey: ["slos"],
      queryFn: () => apiClient.get("/slos"),
      staleTime: 30_000,
    });

  const { data: traces, isLoading: tracesLoading } = useQuery<OtelTrace[]>({
    queryKey: ["traces"],
    queryFn: () => apiClient.get("/observability/traces"),
    staleTime: 10_000,
    refetchInterval: 15_000,
  });

  const { data: summary } = useQuery<TraceSummary>({
    queryKey: ["traces", "summary"],
    queryFn: () => apiClient.get("/observability/traces/summary"),
    staleTime: 15_000,
  });

  const { data: rules, isLoading: rulesLoading } = useQuery<LogRule[]>({
    queryKey: ["observability", "rules"],
    queryFn: () => apiClient.get("/observability/rules"),
    staleTime: 30_000,
  });

  const { data: crashes, isLoading: crashesLoading } = useQuery<CrashReport[]>({
    queryKey: ["observability", "crashes"],
    queryFn: () => apiClient.get("/observability/crashes"),
    staleTime: 15_000,
  });

  /* ── Mutations ── */
  const createRuleMutation = useMutation({
    mutationFn: (d: any) => apiClient.post("/observability/rules", d),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["observability", "rules"] }); setRuleOpen(false); toast.success("Rule created."); },
    onError: (e: any) => toast.error(e.message),
  });

  const deleteRuleMutation = useMutation({
    mutationFn: (id: string) => apiClient.delete(`/observability/rules/${id}`),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["observability", "rules"] }); toast.success("Rule deleted."); },
  });

  const resolveCrashMutation = useMutation({
    mutationFn: (id: string) => apiClient.post(`/observability/crashes/${id}/resolve`, {}),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["observability", "crashes"] }); toast.success("Crash report resolved."); },
  });

  const filteredTraces = traces?.filter(t => !serviceFilter || t.serviceName.toLowerCase().includes(serviceFilter.toLowerCase())) ?? [];
  const services = Array.from(new Set(traces?.map(t => t.serviceName) ?? []));
  const errorRate = summary && summary.totalTraces > 0 ? ((summary.errorTraces / summary.totalTraces) * 100).toFixed(1) : "0.0";

  async function loadSloReport(slo: ProjectSloDto) {
    setSelectedSlo(slo);
    setSloReportLoading(true);
    setSloReport(null);
    try {
      const report = await apiClient.get(`/slos/${slo.projectId}/report`);
      setSloReport(report as SloReportDto);
    } catch { setSloReport(null); }
    finally { setSloReportLoading(false); }
  }

  return (
    <div className="mx-auto max-w-[1400px] space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
            <Activity className="h-6 w-6 text-blue-400" />
            Observability & Tracing
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            OpenTelemetry traces, log aggregation rules, and crash reports across all services.
          </p>
        </div>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-5">
        {[
          { label: "Total Traces", value: summary?.totalTraces?.toLocaleString() ?? "—", color: "text-foreground" },
          { label: "Error Traces", value: summary?.errorTraces?.toLocaleString() ?? "—", color: "text-destructive" },
          { label: "Error Rate", value: `${errorRate}%`, color: parseFloat(errorRate) > 5 ? "text-destructive" : "text-emerald-400" },
          { label: "Avg Duration", value: summary ? (summary.averageDurationMs < 1000 ? `${Math.round(summary.averageDurationMs)}ms` : `${(summary.averageDurationMs / 1000).toFixed(2)}s`) : "—", color: "text-foreground" },
          { label: "Services", value: summary?.servicesTracked?.toString() ?? "—", color: "text-blue-400" },
        ].map(s => (
          <Card key={s.label}>
            <CardContent className="p-4">
              <p className={cn("text-2xl font-bold tabular-nums", s.color)}>{s.value}</p>
              <p className="text-xs text-muted-foreground mt-0.5">{s.label}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Tabs */}
      <Tabs defaultValue="traces">
        <TabsList className="h-9">
          <TabsTrigger value="traces" className="gap-1.5 text-xs">
            <Activity className="h-3.5 w-3.5" /> Traces
          </TabsTrigger>
          <TabsTrigger value="rules" className="gap-1.5 text-xs">
            <Filter className="h-3.5 w-3.5" /> Aggregation Rules
          </TabsTrigger>
          <TabsTrigger value="crashes" className="gap-1.5 text-xs">
            <Bug className="h-3.5 w-3.5" /> Crash Reports
          </TabsTrigger>
          <TabsTrigger value="slos" className="gap-1.5 text-xs">
            <ShieldCheck className="h-3.5 w-3.5" /> SLO Dashboard
          </TabsTrigger>
        </TabsList>

        {/* ─── TRACES ─── */}
        <TabsContent value="traces" className="mt-4 space-y-3">
          <div className="flex gap-2 items-center">
            <div className="relative flex-1 max-w-xs">
              <Input
                placeholder="Filter by service…"
                value={serviceFilter}
                onChange={e => setServiceFilter(e.target.value)}
                className="h-8 pl-8 text-sm"
              />
              <Filter className="absolute left-2 top-2 h-4 w-4 text-muted-foreground" />
            </div>
            <Badge variant="outline" className="text-xs font-normal">{filteredTraces.length} traces</Badge>
          </div>

          {tracesLoading && <div className="space-y-2">{Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-14 rounded-lg" />)}</div>}

          {filteredTraces.length === 0 && !tracesLoading && (
            <Card><CardContent className="py-12 text-center text-sm text-muted-foreground">No traces found. Start by ingesting OTel spans from your services.</CardContent></Card>
          )}

          <div className="rounded-xl border overflow-hidden">
            {filteredTraces.map((trace, i) => (
              <button key={trace.id} onClick={() => setSelectedTrace(trace)}
                className={cn("w-full flex items-center gap-3 px-4 py-3 text-left hover:bg-accent/50 transition-colors",
                  i !== filteredTraces.length - 1 && "border-b border-border/50")}>
                <StatusBadge status={trace.statusCode} />
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium truncate">{trace.operationName}</p>
                  <p className="text-xs text-muted-foreground font-mono">{trace.serviceName} · {trace.traceId.slice(0, 16)}…</p>
                </div>
                <div className="text-right shrink-0">
                  <DurationBadge ms={trace.durationMs} />
                  <p className="text-[10px] text-muted-foreground mt-0.5">{trace.spanCount} spans{trace.errorSpanCount > 0 && <span className="text-destructive"> · {trace.errorSpanCount} err</span>}</p>
                </div>
                <ChevronRight className="h-4 w-4 text-muted-foreground/50 shrink-0" />
              </button>
            ))}
          </div>
        </TabsContent>

        {/* ─── RULES ─── */}
        <TabsContent value="rules" className="mt-4 space-y-3">
          <div className="flex justify-end">
            <Button size="sm" className="gap-1.5 h-8 text-xs" onClick={() => setRuleOpen(true)}>
              <Plus className="h-3.5 w-3.5" /> New Rule
            </Button>
          </div>

          {rulesLoading && <div className="space-y-2">{Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-16 rounded-xl" />)}</div>}

          {rules?.length === 0 && !rulesLoading && (
            <Card><CardContent className="py-12 text-center text-sm text-muted-foreground">No aggregation rules. Create one to monitor log patterns and trigger alerts.</CardContent></Card>
          )}

          <div className="space-y-2">
            {rules?.map(rule => {
              const severityColor: Record<string, string> = { Critical: "text-destructive", Error: "text-red-400", Warning: "text-amber-400", Info: "text-blue-400" };
              return (
                <Card key={rule.id}>
                  <CardContent className="flex items-center gap-4 px-5 py-3">
                    <div className="flex-1 min-w-0 space-y-0.5">
                      <div className="flex items-center gap-2">
                        <p className="text-sm font-semibold">{rule.name}</p>
                        <Badge variant="outline" className={cn("text-[10px]", rule.isEnabled ? severityColor[rule.severity] : "text-muted-foreground")}>
                          {rule.severity}
                        </Badge>
                        <Badge variant="outline" className="text-[10px]">{rule.action}</Badge>
                        {!rule.isEnabled && <Badge variant="secondary" className="text-[10px]">Disabled</Badge>}
                      </div>
                      <p className="text-xs font-mono text-muted-foreground truncate">{rule.pattern}</p>
                    </div>
                    <div className="text-right shrink-0">
                      <p className="text-xs text-muted-foreground">{rule.matchCount} matches</p>
                      {rule.lastMatchedAt && <p className="text-[10px] text-muted-foreground">{new Date(rule.lastMatchedAt).toLocaleString()}</p>}
                    </div>
                    <Button size="icon" variant="ghost" className="h-7 w-7 text-muted-foreground hover:text-destructive" onClick={() => deleteRuleMutation.mutate(rule.id)}>
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </CardContent>
                </Card>
              );
            })}
          </div>
        </TabsContent>

          {/* ─── SLO DASHBOARD ─── */}
          <TabsContent value="slos" className="mt-4 space-y-4">
            <div className="flex items-center justify-between">
              <p className="text-sm text-muted-foreground">
                Service Level Objectives track uptime targets and error budgets per project.
              </p>
              <Badge variant="outline" className="text-xs font-normal">
                {slos?.filter(s => s.isBreaching).length ?? 0} breaching
              </Badge>
            </div>

            {slosLoading && (
              <div className="space-y-2">{Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-20 rounded-xl" />)}</div>
            )}

            {(slos?.length ?? 0) === 0 && !slosLoading && (
              <Card><CardContent className="py-12 text-center text-sm text-muted-foreground">
                No SLOs configured. Use the API to set uptime targets per project.
              </CardContent></Card>
            )}

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {(slos ?? []).map(slo => {
                const uptimePct = slo.currentUptimePercent;
                const budgetPct = slo.errorBudgetRemainingPercent;
                const breachBorder = slo.isBreaching ? "border-destructive/50" : "border-emerald-500/30";

                return (
                  <Card key={slo.id} className={cn("cursor-pointer transition-colors hover:bg-accent/30", breachBorder)}
                    onClick={() => loadSloReport(slo)}>
                    <CardContent className="p-4 space-y-3">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2">
                          {slo.isBreaching
                            ? <XCircle className="h-4 w-4 text-destructive" />
                            : <CheckCircle2 className="h-4 w-4 text-emerald-400" />}
                          <p className="font-semibold text-sm">{slo.projectName}</p>
                        </div>
                        <div className="flex items-center gap-1.5">
                          <Badge variant={slo.isBreaching ? "destructive" : "outline"} className="text-[10px]">
                            {slo.isBreaching ? "Breaching" : "Healthy"}
                          </Badge>
                          {!slo.isEnabled && <Badge variant="secondary" className="text-[10px]">Disabled</Badge>}
                        </div>
                      </div>

                      <div className="grid grid-cols-3 gap-3 text-center">
                        <div>
                          <p className={cn("text-xl font-bold tabular-nums", slo.isBreaching ? "text-destructive" : "text-emerald-400")}>
                            {uptimePct != null ? `${uptimePct.toFixed(2)}%` : "—"}
                          </p>
                          <p className="text-[10px] text-muted-foreground">Uptime</p>
                          <p className="text-[10px] text-muted-foreground">Target: {slo.uptimeTargetPercent}%</p>
                        </div>
                        <div>
                          <p className={cn("text-xl font-bold tabular-nums",
                            (budgetPct ?? 100) < 20 ? "text-destructive" : (budgetPct ?? 100) < 50 ? "text-amber-400" : "text-foreground")}>
                            {budgetPct != null ? `${budgetPct.toFixed(1)}%` : "—"}
                          </p>
                          <p className="text-[10px] text-muted-foreground">Error Budget</p>
                          <p className="text-[10px] text-muted-foreground">Remaining</p>
                        </div>
                        <div>
                          <p className="text-xl font-bold tabular-nums text-foreground">
                            {slo.errorBudgetBurnRate != null ? `${slo.errorBudgetBurnRate.toFixed(1)}x` : "—"}
                          </p>
                          <p className="text-[10px] text-muted-foreground">Burn Rate</p>
                          <p className="text-[10px] text-muted-foreground">{slo.windowDays}d window</p>
                        </div>
                      </div>

                      {slo.lastEvaluatedAt && (
                        <p className="text-[10px] text-muted-foreground">
                          Last evaluated: {new Date(slo.lastEvaluatedAt).toLocaleString()}
                        </p>
                      )}
                    </CardContent>
                  </Card>
                );
              })}
            </div>

            {selectedSlo && (
              <Card className="mt-2">
                <CardHeader className="pb-2">
                  <CardTitle className="text-base flex items-center gap-2">
                    <BarChart3 className="h-4 w-4 text-blue-400" />
                    Deploy Correlation — {selectedSlo.projectName}
                  </CardTitle>
                  <CardDescription className="text-xs">
                    Recent deployments and their impact on service reliability.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {sloReportLoading && <Skeleton className="h-24 rounded-lg" />}
                  {!sloReportLoading && (sloReport?.recentDeployEvents?.length ?? 0) === 0 && (
                    <p className="text-sm text-muted-foreground text-center py-4">No recent deployments in this window.</p>
                  )}
                  {!sloReportLoading && (sloReport?.recentDeployEvents ?? []).length > 0 && (
                    <div className="rounded-xl border overflow-hidden">
                      {sloReport!.recentDeployEvents.map((ev, i) => (
                        <div key={i} className={cn("flex items-center gap-3 px-4 py-2.5 text-sm",
                          i !== sloReport!.recentDeployEvents.length - 1 && "border-b border-border/50")}>
                          <div className={cn("h-2 w-2 rounded-full shrink-0",
                            ev.status === "Succeeded" ? "bg-emerald-400" : ev.status === "Failed" ? "bg-destructive" : "bg-amber-400")} />
                          <div className="flex-1 min-w-0">
                            <span className="font-mono text-xs text-muted-foreground">{ev.commitSha}</span>
                            <span className="mx-2 text-muted-foreground/40">·</span>
                            <span className="text-xs">{ev.branch}</span>
                          </div>
                          <Badge variant="outline" className={cn("text-[10px]",
                            ev.status === "Succeeded" ? "text-emerald-400" : ev.status === "Failed" ? "text-destructive" : "text-amber-400")}>
                            {ev.status}
                          </Badge>
                          <span className="text-[10px] text-muted-foreground shrink-0">
                            {new Date(ev.deployedAt).toLocaleDateString()}
                          </span>
                        </div>
                      ))}
                    </div>
                  )}
                </CardContent>
              </Card>
            )}
          </TabsContent>

        {/* ─── CRASHES ─── */}
        <TabsContent value="crashes" className="mt-4 space-y-3">
          {crashesLoading && <div className="space-y-2">{Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-24 rounded-xl" />)}</div>}

          {crashes?.length === 0 && !crashesLoading && (
            <Card><CardContent className="py-12 text-center text-sm text-muted-foreground">No crash reports. All services appear healthy.</CardContent></Card>
          )}

          <div className="space-y-3">
            {crashes?.map(crash => (
              <Card key={crash.id} className={cn(crash.isResolved ? "opacity-60" : "")}>
                <CardContent className="px-5 py-4 space-y-2">
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="text-xs font-mono font-semibold text-muted-foreground">{crash.serviceName}</span>
                        <Badge className={cn("text-[10px]", crash.isResolved ? "bg-muted text-muted-foreground" : "bg-destructive/10 text-destructive border-destructive/30")}>
                          {crash.isResolved ? "Resolved" : "Active"}
                        </Badge>
                        <span className="text-[10px] text-muted-foreground">{crash.occurrenceCount}x</span>
                      </div>
                      <p className="text-sm font-medium mt-1 line-clamp-2">{crash.crashMessage}</p>
                    </div>
                    {!crash.isResolved && (
                      <Button size="sm" variant="outline" className="h-7 gap-1 text-xs shrink-0" onClick={() => resolveCrashMutation.mutate(crash.id)}>
                        <CheckCircle2 className="h-3 w-3" /> Resolve
                      </Button>
                    )}
                  </div>

                  {crash.rcaSummary && (
                    <div className="flex items-start gap-1.5 rounded-lg bg-muted/40 px-3 py-2">
                      <Zap className="h-3.5 w-3.5 text-amber-400 mt-0.5 shrink-0" />
                      <p className="text-xs text-foreground/80"><span className="font-semibold text-amber-400">RCA: </span>{crash.rcaSummary}</p>
                    </div>
                  )}

                  <div className="flex gap-3 text-[10px] text-muted-foreground">
                    <span>First seen: {new Date(crash.firstSeenAt).toLocaleString()}</span>
                    <span>·</span>
                    <span>Last seen: {new Date(crash.lastSeenAt).toLocaleString()}</span>
                    {crash.resolvedAt && <><span>·</span><span>Resolved: {new Date(crash.resolvedAt).toLocaleString()}</span></>}
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>
      </Tabs>

      {/* Trace detail dialog */}
      <Dialog open={!!selectedTrace} onOpenChange={() => setSelectedTrace(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader><DialogTitle>Trace Detail</DialogTitle></DialogHeader>
          {selectedTrace && (
            <div className="space-y-3 text-sm">
              <div className="grid grid-cols-2 gap-x-4 gap-y-2">
                {[
                  ["Operation", selectedTrace.operationName],
                  ["Service", selectedTrace.serviceName],
                  ["Trace ID", selectedTrace.traceId],
                  ["Span ID", selectedTrace.spanId],
                  ["Duration", `${selectedTrace.durationMs}ms`],
                  ["Status", selectedTrace.statusCode],
                  ["Total Spans", selectedTrace.spanCount],
                  ["Error Spans", selectedTrace.errorSpanCount],
                ].map(([k, v]) => (
                  <div key={k as string}>
                    <p className="text-[10px] text-muted-foreground uppercase tracking-wide">{k}</p>
                    <p className="font-mono text-xs truncate">{v}</p>
                  </div>
                ))}
              </div>
              {selectedTrace.statusMessage && (
                <div>
                  <p className="text-[10px] text-muted-foreground uppercase tracking-wide mb-1">Message</p>
                  <p className="text-xs rounded bg-muted/50 px-2 py-1">{selectedTrace.statusMessage}</p>
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* New rule dialog */}
      <Dialog open={ruleOpen} onOpenChange={setRuleOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader><DialogTitle>New Aggregation Rule</DialogTitle></DialogHeader>
          <div className="space-y-3 py-2">
            <div className="space-y-1.5">
              <Label>Name</Label>
              <Input value={ruleForm.name} onChange={e => setRuleForm(p => ({ ...p, name: e.target.value }))} placeholder="OOM Crash Detector" />
            </div>
            <div className="space-y-1.5">
              <Label>Pattern (regex)</Label>
              <Input value={ruleForm.pattern} onChange={e => setRuleForm(p => ({ ...p, pattern: e.target.value }))} placeholder="OutOfMemoryException|ENOMEM" className="font-mono text-xs" />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Action</Label>
                <Select value={ruleForm.action} onValueChange={v => setRuleForm(p => ({ ...p, action: v }))}>
                  <SelectTrigger className="h-8 text-xs"><SelectValue /></SelectTrigger>
                  <SelectContent>{["Alert", "CrashReport", "Ignore", "Escalate"].map(a => <SelectItem key={a} value={a} className="text-xs">{a}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label>Severity</Label>
                <Select value={ruleForm.severity} onValueChange={v => setRuleForm(p => ({ ...p, severity: v }))}>
                  <SelectTrigger className="h-8 text-xs"><SelectValue /></SelectTrigger>
                  <SelectContent>{["Info", "Warning", "Error", "Critical"].map(s => <SelectItem key={s} value={s} className="text-xs">{s}</SelectItem>)}</SelectContent>
                </Select>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Switch checked={ruleForm.isEnabled} onCheckedChange={v => setRuleForm(p => ({ ...p, isEnabled: v }))} />
              <Label className="text-xs">Enable immediately</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRuleOpen(false)}>Cancel</Button>
            <Button onClick={() => createRuleMutation.mutate(ruleForm)} disabled={createRuleMutation.isPending || !ruleForm.name || !ruleForm.pattern}>
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
