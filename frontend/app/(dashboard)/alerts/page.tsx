"use client";

import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  Bell, BellOff, CheckCircle, AlertTriangle, AlertCircle,
  Clock, Shield, Filter, MoreVertical, RefreshCw, Plus, FlaskConical, Trash2, Edit,
  ChevronDown, ChevronUp, Lightbulb, TrendingUp, Activity, Server, Cpu,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import {
  useAlerts,
  useAcknowledgeAlert,
  useResolveAlert,
  useAlertRules,
  useCreateAlertRule,
  useUpdateAlertRule,
  useDeleteAlertRule,
  useTestAlertRule,
} from "@/hooks/use-api";
import { toast } from "sonner";
import { formatDistanceToNow } from "date-fns";
import type { Alert, AlertRule } from "@/types";
import { cn } from "@/lib/utils";

const severityConfig = {
  critical: { icon: AlertCircle, color: "text-destructive", badge: "destructive" as const, label: "Critical" },
  warning: { icon: AlertTriangle, color: "text-warning", badge: "warning" as const, label: "Warning" },
  info: { icon: Bell, color: "text-info", badge: "secondary" as const, label: "Info" },
};

const statusConfig = {
  active: { label: "Active", color: "text-destructive" },
  acknowledged: { label: "Acknowledged", color: "text-warning" },
  resolved: { label: "Resolved", color: "text-success" },
};

interface RootCauseInfo { cause: string; impact: string; actions: string[]; }

function getRootCause(alert: Alert): RootCauseInfo {
  const metric = alert.conditions?.[0]?.metric ?? alert.name ?? "";
  const threshold = alert.conditions?.[0]?.threshold;
  const m = metric.toLowerCase();

  if (m.includes("cpu")) {
    return {
      cause: `CPU usage breached ${threshold ?? "the configured"} threshold. This is usually caused by a sudden traffic spike, a runaway process, or an inefficient code path inside a container.`,
      impact: "High CPU contention degrades request latency for all services running on this server. Sustained overload triggers OOM kills and cascading failures.",
      actions: [
        "SSH into the server: run `top` or `docker stats` to identify the hot container.",
        "Restart the offending container if it is stuck in a busy loop.",
        "Scale out horizontally: add replicas or migrate to a larger instance.",
        "Review recent deployments for CPU-intensive code changes.",
        "Increase CPU limits in Service → Resources or enable Auto-scaling.",
      ],
    };
  }
  if (m.includes("memory") || m.includes("mem")) {
    return {
      cause: `Memory usage exceeded ${threshold ?? "the configured"} threshold. Common causes: memory leak in application code, missing heap limits, or holding large in-memory caches without eviction.`,
      impact: "Container will be OOM-killed by the kernel, causing service interruptions. Swap pressure slows all processes on the host.",
      actions: [
        "Run `docker stats` to pin down which container is consuming memory.",
        "Set `--memory` limits in docker run / compose to prevent OOM cascade.",
        "Profile heap usage: use Node --inspect, dotnet-dump, or Java heap dump.",
        "Reduce cache TTL or enable eviction policies (Redis maxmemory-policy).",
        "Deploy a patched version that fixes the leak if one is identified.",
      ],
    };
  }
  if (m.includes("disk") || m.includes("storage")) {
    return {
      cause: `Disk usage is above ${threshold ?? "safe"} capacity. Logs, Docker images, or database files are the most common culprits.`,
      impact: "When disk is full, databases crash, log writes fail, and container image pulls are blocked — causing deploy failures.",
      actions: [
        "Run `df -h` and `du -sh /*` on the server to locate large directories.",
        "Prune stale Docker images: `docker image prune -af`.",
        "Enable log rotation in /etc/logrotate.d or via Docker log driver settings.",
        "Move database data volume to a larger attached disk.",
        "Set up automated disk-full alerts below 80% to get ahead next time.",
      ],
    };
  }
  if (m.includes("response") || m.includes("latency") || m.includes("time")) {
    return {
      cause: `Response time exceeded ${threshold ?? "acceptable"} ms. Root causes typically include slow database queries, downstream HTTP timeouts, or resource starvation.`,
      impact: "User-facing requests are slow or timing out. SLA thresholds may be breached.",
      actions: [
        "Check slow query logs in your database dashboard.",
        "Enable distributed tracing (e.g. OpenTelemetry) to identify the slow span.",
        "Add a circuit breaker for slow downstream dependencies.",
        "Scale database replicas or add a caching layer (Redis).",
        "Profile the highest-traffic API endpoints for N+1 query patterns.",
      ],
    };
  }
  if (m.includes("error") || m.includes("5xx") || m.includes("fail")) {
    return {
      cause: `Error rate breached ${threshold ?? "normal"} levels. This indicates application exceptions or infrastructure faults.`,
      impact: "Users are receiving error responses. Revenue impact is likely if the service is customer-facing.",
      actions: [
        "Open the Logs tab for the affected deployment and filter by `stderr`.",
        "Use the AI Debug tab to run automated root-cause analysis on the error.",
        "Check recent deployments — if error rate spiked after a deploy, rollback immediately.",
        "Verify external dependencies (databases, third-party APIs) are responding.",
        "Set up structured logging with error tracking (Sentry / Datadog).",
      ],
    };
  }
  // Generic fallback
  return {
    cause: `Alert for metric "${metric}" triggered${threshold != null ? ` at value ${threshold}` : ""}. Open your monitoring dashboard to inspect the exact time series.`,
    impact: "Investigate the affected resource immediately to prevent escalation.",
    actions: [
      "Check the Monitoring page for resource utilisation graphs.",
      "Review recent deployments or configuration changes around the trigger time.",
      "Acknowledge this alert after investigating so the team knows it is being handled.",
      "Add a more specific alert rule with a tighter threshold to catch this earlier.",
    ],
  };
}

export default function AlertsPage() {
  const [severity, setSeverity] = useState<string>("all");
  const [acknowledged, setAcknowledged] = useState<string>("all");
  const [expandedAlertId, setExpandedAlertId] = useState<string | null>(null);
  const [ruleDialogOpen, setRuleDialogOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<AlertRule | null>(null);
  const [ruleErrors, setRuleErrors] = useState<Partial<Record<"name" | "metric" | "threshold" | "windowMinutes" | "cooldownMinutes", string>>>({});
  const [ruleForm, setRuleForm] = useState<{
    name: string;
    metric: string;
    operator: AlertRule["operator"];
    threshold: number;
    windowMinutes: number;
    severity: AlertRule["severity"];
    isEnabled: boolean;
    cooldownMinutes: number;
    description: string;
  }>({
    name: "",
    metric: "cpu.usage",
    operator: ">",
    threshold: 80,
    windowMinutes: 5,
    severity: "warning",
    isEnabled: true,
    cooldownMinutes: 10,
    description: "",
  });
  const { data: alertsRaw, isLoading, refetch } = useAlerts();
  const { data: rules, isLoading: rulesLoading } = useAlertRules();
  const alerts = alertsRaw?.filter((a) => {
    if (severity !== "all" && a.severity !== severity) return false;
    if (acknowledged === "acknowledged" && a.status !== "acknowledged") return false;
    if (acknowledged === "unacknowledged" && a.status === "acknowledged") return false;
    return true;
  });
  const acknowledge = useAcknowledgeAlert();
  const resolve = useResolveAlert();
  const createRule = useCreateAlertRule();
  const updateRule = useUpdateAlertRule();
  const deleteRule = useDeleteAlertRule();
  const testRule = useTestAlertRule();

  const handleAcknowledge = async (id: string) => {
    try {
      await acknowledge.mutateAsync(id);
      toast.success("Alert acknowledged.");
    } catch (e: any) {
      toast.error("Failed to acknowledge alert", { description: e.message });
    }
  };

  const handleResolve = async (id: string) => {
    try {
      await resolve.mutateAsync(id);
      toast.success("Alert resolved.");
    } catch (e: any) {
      toast.error("Failed to resolve alert", { description: e.message });
    }
  };

  const openCreateRule = () => {
    setEditingRule(null);
    setRuleForm({
      name: "",
      metric: "cpu.usage",
      operator: ">",
      threshold: 80,
      windowMinutes: 5,
      severity: "warning",
      isEnabled: true,
      cooldownMinutes: 10,
      description: "",
    });
    setRuleDialogOpen(true);
  };

  const openEditRule = (rule: AlertRule) => {
    setEditingRule(rule);
    setRuleForm({
      name: rule.name,
      metric: rule.metric,
      operator: rule.operator,
      threshold: rule.threshold,
      windowMinutes: rule.windowMinutes,
      severity: rule.severity,
      isEnabled: rule.isEnabled,
      cooldownMinutes: rule.cooldownMinutes,
      description: rule.description ?? "",
    });
    setRuleDialogOpen(true);
  };

  const handleSaveRule = async () => {
    const nextErrors: Partial<Record<"name" | "metric" | "threshold" | "windowMinutes" | "cooldownMinutes", string>> = {};
    if (!ruleForm.name.trim()) nextErrors.name = "Rule name is required.";
    if (!ruleForm.metric.trim()) nextErrors.metric = "Metric is required.";
    if (!Number.isFinite(ruleForm.threshold)) nextErrors.threshold = "Threshold must be a valid number.";
    if (ruleForm.windowMinutes < 1 || ruleForm.windowMinutes > 1440) nextErrors.windowMinutes = "Window must be between 1 and 1440 minutes.";
    if (ruleForm.cooldownMinutes < 0 || ruleForm.cooldownMinutes > 1440) nextErrors.cooldownMinutes = "Cooldown must be between 0 and 1440 minutes.";

    if (Object.keys(nextErrors).length > 0) {
      setRuleErrors(nextErrors);
      toast.error("Please fix the highlighted fields.");
      return;
    }

    setRuleErrors({});

    try {
      if (editingRule) {
        await updateRule.mutateAsync({ id: editingRule.id, ...ruleForm });
        toast.success("Alert rule updated.");
      } else {
        await createRule.mutateAsync(ruleForm);
        toast.success("Alert rule created.");
      }
      setRuleDialogOpen(false);
    } catch (e: any) {
      toast.error("Failed to save alert rule", { description: e.message });
    }
  };

  const handleDeleteRule = async (id: string) => {
    try {
      await deleteRule.mutateAsync(id);
      toast.success("Alert rule deleted.");
    } catch (e: any) {
      toast.error("Failed to delete alert rule", { description: e.message });
    }
  };

  const handleTestRule = async (id: string, name: string) => {
    try {
      const result = await testRule.mutateAsync({ id });
      toast.success(`Rule test completed: ${name}`, { description: result.message });
      if (result.triggered) {
        refetch();
      }
    } catch (e: any) {
      toast.error("Rule test failed", { description: e.message });
    }
  };

  const activeCount = alerts?.filter((a) => a.status === "active").length ?? 0;
  const criticalCount = alerts?.filter((a) => a.severity === "critical" && a.status === "active").length ?? 0;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Alerts</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {activeCount} active · {criticalCount} critical
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => refetch()} className="gap-1.5">
          <RefreshCw className="h-4 w-4" />
          Refresh
        </Button>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-3 gap-4">
        {[
          { label: "Active", value: alerts?.filter(a => a.status === "active").length ?? 0, icon: Bell, color: "text-destructive" },
          { label: "Acknowledged", value: alerts?.filter(a => a.status === "acknowledged").length ?? 0, icon: Clock, color: "text-warning" },
          { label: "Resolved (Today)", value: alerts?.filter(a => a.status === "resolved").length ?? 0, icon: CheckCircle, color: "text-success" },
        ].map(({ label, value, icon: Icon, color }) => (
          <Card key={label}>
            <CardContent className="pt-6">
              <div className="flex items-center gap-3">
                <Icon className={`h-8 w-8 ${color}`} />
                <div>
                  <p className="text-2xl font-bold">{value}</p>
                  <p className="text-sm text-muted-foreground">{label}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="text-base">Alert Rules</CardTitle>
          <Button size="sm" onClick={openCreateRule}>
            <Plus className="h-4 w-4 mr-1.5" />New Rule
          </Button>
        </CardHeader>
        <CardContent className="space-y-3">
          {rulesLoading
            ? Array.from({ length: 2 }).map((_, i) => <Skeleton key={i} className="h-16 w-full rounded-lg" />)
            : !rules?.length
            ? <p className="text-sm text-muted-foreground">No alert rules configured yet.</p>
            : rules.map((rule) => (
              <div key={rule.id} className="flex items-center justify-between rounded-lg border p-3">
                <div>
                  <p className="text-sm font-medium">{rule.name}</p>
                  <p className="text-xs text-muted-foreground">
                    {rule.metric} {rule.operator} {rule.threshold} · {rule.windowMinutes}m window · {rule.severity}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant={rule.isEnabled ? "secondary" : "outline"}>{rule.isEnabled ? "Enabled" : "Disabled"}</Badge>
                  <Button variant="outline" size="sm" onClick={() => handleTestRule(rule.id, rule.name)}>
                    <FlaskConical className="h-3.5 w-3.5 mr-1" />Test
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => openEditRule(rule)}>
                    <Edit className="h-3.5 w-3.5" />
                  </Button>
                  <Button variant="ghost" size="sm" className="text-destructive" onClick={() => handleDeleteRule(rule.id)}>
                    <Trash2 className="h-3.5 w-3.5" />
                  </Button>
                </div>
              </div>
            ))}
        </CardContent>
      </Card>

      {/* Filters */}
      <div className="flex gap-3">
        <Select value={severity} onValueChange={setSeverity}>
          <SelectTrigger className="w-40">
            <Filter className="h-4 w-4 mr-2" />
            <SelectValue placeholder="Severity" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Severities</SelectItem>
            <SelectItem value="critical">Critical</SelectItem>
            <SelectItem value="warning">Warning</SelectItem>
            <SelectItem value="info">Info</SelectItem>
          </SelectContent>
        </Select>
        <Select value={acknowledged} onValueChange={setAcknowledged}>
          <SelectTrigger className="w-48">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Status</SelectItem>
            <SelectItem value="unacknowledged">Unacknowledged</SelectItem>
            <SelectItem value="acknowledged">Acknowledged</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Alert list */}
      <div className="space-y-2">
        {isLoading
          ? Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-20 w-full rounded-lg" />
            ))
          : alerts?.length === 0
          ? (
            <Card>
              <CardContent className="py-16 text-center">
                <Shield className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                <p className="text-lg font-medium">No alerts</p>
                <p className="text-sm text-muted-foreground">Everything looks healthy!</p>
              </CardContent>
            </Card>
          )
          : alerts?.map((alert) => {
            const sev = severityConfig[alert.severity as keyof typeof severityConfig] ?? severityConfig.info;
            const SevIcon = sev.icon;
            const isExpanded = expandedAlertId === alert.id;
            const rootCause = getRootCause(alert);
            return (
              <motion.div
                key={alert.id}
                initial={{ opacity: 0, y: 4 }}
                animate={{ opacity: 1, y: 0 }}
              >
                <Card className={cn(
                  "transition-all",
                  alert.status === "resolved" ? "opacity-60" : "",
                  isExpanded ? "border-primary/40 shadow-sm" : ""
                )}>
                  {/* Clickable header row */}
                  <button
                    type="button"
                    className="w-full text-left"
                    onClick={() => setExpandedAlertId(isExpanded ? null : alert.id)}
                  >
                    <CardContent className="py-4 flex items-start gap-4">
                      <SevIcon className={`h-5 w-5 mt-0.5 flex-shrink-0 ${sev.color}`} />
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="font-medium">{alert.name}</span>
                          <Badge variant={sev.badge}>{sev.label}</Badge>
                          <Badge variant="outline">{statusConfig[alert.status as keyof typeof statusConfig]?.label ?? alert.status}</Badge>
                        </div>
                        {alert.conditions?.length > 0 && (
                          <p className="text-sm text-muted-foreground mt-1">
                            Condition: {alert.conditions[0].metric} {alert.conditions[0].operator} {alert.conditions[0].threshold}
                          </p>
                        )}
                        <p className="text-xs text-muted-foreground mt-1">
                          Triggered {alert.triggeredAt ? formatDistanceToNow(new Date(alert.triggeredAt), { addSuffix: true }) : "—"}
                        </p>
                      </div>
                      <div className="flex items-center gap-1 shrink-0">
                        {isExpanded
                          ? <ChevronUp className="h-4 w-4 text-muted-foreground" />
                          : <ChevronDown className="h-4 w-4 text-muted-foreground" />}
                        {alert.status === "active" && (
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" className="h-8 w-8" onClick={(e) => e.stopPropagation()}>
                                <MoreVertical className="h-4 w-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onClick={(e) => { e.stopPropagation(); handleAcknowledge(alert.id); }}>
                                <Clock className="h-4 w-4 mr-2" />Acknowledge
                              </DropdownMenuItem>
                              <DropdownMenuItem onClick={(e) => { e.stopPropagation(); handleResolve(alert.id); }}>
                                <CheckCircle className="h-4 w-4 mr-2" />Resolve
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        )}
                      </div>
                    </CardContent>
                  </button>

                  {/* Expandable root-cause panel */}
                  <AnimatePresence>
                    {isExpanded && (
                      <motion.div
                        initial={{ height: 0, opacity: 0 }}
                        animate={{ height: "auto", opacity: 1 }}
                        exit={{ height: 0, opacity: 0 }}
                        transition={{ duration: 0.18 }}
                        className="overflow-hidden"
                      >
                        <div className="border-t border-border/60 mx-4 mb-4 pt-4 space-y-4">
                          {/* Root cause */}
                          <div className="flex items-start gap-3 rounded-lg bg-destructive/5 border border-destructive/20 px-4 py-3">
                            <AlertCircle className="h-4 w-4 text-destructive mt-0.5 shrink-0" />
                            <div>
                              <p className="text-sm font-semibold text-destructive">Root Cause</p>
                              <p className="text-sm text-muted-foreground mt-0.5">{rootCause.cause}</p>
                            </div>
                          </div>

                          {/* Impact */}
                          <div className="flex items-start gap-3 rounded-lg bg-amber-500/5 border border-amber-500/20 px-4 py-3">
                            <TrendingUp className="h-4 w-4 text-amber-500 mt-0.5 shrink-0" />
                            <div>
                              <p className="text-sm font-semibold text-amber-500">Impact</p>
                              <p className="text-sm text-muted-foreground mt-0.5">{rootCause.impact}</p>
                            </div>
                          </div>

                          {/* Fix suggestions */}
                          <div>
                            <p className="text-xs font-semibold text-muted-foreground uppercase tracking-widest mb-2 flex items-center gap-1.5">
                              <Lightbulb className="h-3.5 w-3.5" />Recommended Actions
                            </p>
                            <ol className="space-y-1.5 ml-1">
                              {rootCause.actions.map((action, i) => (
                                <li key={i} className="flex items-start gap-2 text-sm">
                                  <span className="flex h-5 w-5 items-center justify-center rounded-full bg-primary/10 text-primary text-[10px] font-bold shrink-0 mt-0.5">{i + 1}</span>
                                  <span className="text-muted-foreground">{action}</span>
                                </li>
                              ))}
                            </ol>
                          </div>

                          {/* Quick actions */}
                          {alert.status === "active" && (
                            <div className="flex gap-2 pt-1">
                              <Button size="sm" variant="outline" className="h-7 text-xs gap-1.5"
                                onClick={() => handleAcknowledge(alert.id)}>
                                <Clock className="h-3.5 w-3.5" />Acknowledge
                              </Button>
                              <Button size="sm" variant="outline" className="h-7 text-xs gap-1.5 text-success border-success/30 hover:bg-success/10"
                                onClick={() => handleResolve(alert.id)}>
                                <CheckCircle className="h-3.5 w-3.5" />Mark Resolved
                              </Button>
                            </div>
                          )}
                        </div>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </Card>
              </motion.div>
            );
          })}
      </div>

      <Dialog open={ruleDialogOpen} onOpenChange={setRuleDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editingRule ? "Edit Alert Rule" : "Create Alert Rule"}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <div className="space-y-1.5">
              <Label>Rule Name</Label>
              <Input
                value={ruleForm.name}
                onChange={(e) => {
                  const value = e.target.value;
                  setRuleForm((s) => ({ ...s, name: value }));
                  setRuleErrors((prev) => ({ ...prev, name: value.trim() ? undefined : prev.name }));
                }}
                className={ruleErrors.name ? "border-destructive" : ""}
              />
              {ruleErrors.name && <p className="text-xs text-destructive">{ruleErrors.name}</p>}
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Metric</Label>
                <Input
                  value={ruleForm.metric}
                  onChange={(e) => {
                    const value = e.target.value;
                    setRuleForm((s) => ({ ...s, metric: value }));
                    setRuleErrors((prev) => ({ ...prev, metric: value.trim() ? undefined : prev.metric }));
                  }}
                  className={ruleErrors.metric ? "border-destructive" : ""}
                />
                {ruleErrors.metric
                  ? <p className="text-xs text-destructive">{ruleErrors.metric}</p>
                  : <p className="text-xs text-muted-foreground">Examples: cpu.usage, memory.usage, response.time</p>}
              </div>
              <div className="space-y-1.5">
                <Label>Operator</Label>
                <Select value={ruleForm.operator} onValueChange={(v) => setRuleForm((s) => ({ ...s, operator: v as AlertRule["operator"] }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value=">">&gt;</SelectItem>
                    <SelectItem value=">=">&gt;=</SelectItem>
                    <SelectItem value="<">&lt;</SelectItem>
                    <SelectItem value="<=">&lt;=</SelectItem>
                    <SelectItem value="=">=</SelectItem>
                    <SelectItem value="!=">!=</SelectItem>
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">Comparison operator used when evaluating the metric.</p>
              </div>
            </div>
            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-1.5">
                <Label>Threshold</Label>
                <Input
                  type="number"
                  value={ruleForm.threshold}
                  onChange={(e) => {
                    const value = Number(e.target.value);
                    setRuleForm((s) => ({ ...s, threshold: value }));
                    setRuleErrors((prev) => ({ ...prev, threshold: Number.isFinite(value) ? undefined : prev.threshold }));
                  }}
                  className={ruleErrors.threshold ? "border-destructive" : ""}
                />
                {ruleErrors.threshold && <p className="text-xs text-destructive">{ruleErrors.threshold}</p>}
              </div>
              <div className="space-y-1.5">
                <Label>Window (min)</Label>
                <Input
                  type="number"
                  value={ruleForm.windowMinutes}
                  onChange={(e) => {
                    const value = Number(e.target.value);
                    setRuleForm((s) => ({ ...s, windowMinutes: value }));
                    setRuleErrors((prev) => ({ ...prev, windowMinutes: value >= 1 && value <= 1440 ? undefined : prev.windowMinutes }));
                  }}
                  className={ruleErrors.windowMinutes ? "border-destructive" : ""}
                />
                {ruleErrors.windowMinutes && <p className="text-xs text-destructive">{ruleErrors.windowMinutes}</p>}
              </div>
              <div className="space-y-1.5">
                <Label>Cooldown (min)</Label>
                <Input
                  type="number"
                  value={ruleForm.cooldownMinutes}
                  onChange={(e) => {
                    const value = Number(e.target.value);
                    setRuleForm((s) => ({ ...s, cooldownMinutes: value }));
                    setRuleErrors((prev) => ({ ...prev, cooldownMinutes: value >= 0 && value <= 1440 ? undefined : prev.cooldownMinutes }));
                  }}
                  className={ruleErrors.cooldownMinutes ? "border-destructive" : ""}
                />
                {ruleErrors.cooldownMinutes && <p className="text-xs text-destructive">{ruleErrors.cooldownMinutes}</p>}
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>Severity</Label>
              <Select value={ruleForm.severity} onValueChange={(v) => setRuleForm((s) => ({ ...s, severity: v as AlertRule["severity"] }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="critical">Critical</SelectItem>
                  <SelectItem value="warning">Warning</SelectItem>
                  <SelectItem value="info">Info</SelectItem>
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">Critical triggers immediate action, warning is elevated, info is low urgency.</p>
            </div>
            <div className="space-y-1.5">
              <Label>Description</Label>
              <Input value={ruleForm.description} onChange={(e) => setRuleForm((s) => ({ ...s, description: e.target.value }))} />
            </div>
            <div className="flex items-center justify-between rounded-md border p-3">
              <Label>Rule Enabled</Label>
              <Switch checked={ruleForm.isEnabled} onCheckedChange={(v) => setRuleForm((s) => ({ ...s, isEnabled: v }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRuleDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleSaveRule} disabled={createRule.isPending || updateRule.isPending}>
              {editingRule ? "Save Changes" : "Create Rule"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
