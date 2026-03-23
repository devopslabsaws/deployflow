"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Bell, BellOff, CheckCircle, AlertTriangle, AlertCircle,
  Clock, Shield, Server, Filter, MoreVertical, RefreshCw, Plus, FlaskConical, Trash2, Edit,
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

export default function AlertsPage() {
  const [severity, setSeverity] = useState<string>("all");
  const [acknowledged, setAcknowledged] = useState<string>("all");
  const [ruleDialogOpen, setRuleDialogOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<AlertRule | null>(null);
  const [ruleErrors, setRuleErrors] = useState<Partial<Record<"name" | "metric" | "threshold" | "windowMinutes" | "cooldownMinutes", string>>>({});
  const [ruleForm, setRuleForm] = useState({
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
      <div className="space-y-3">
        {isLoading
          ? Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-24 w-full rounded-lg" />
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
            return (
              <motion.div
                key={alert.id}
                initial={{ opacity: 0, y: 4 }}
                animate={{ opacity: 1, y: 0 }}
              >
                <Card className={alert.status === "resolved" ? "opacity-60" : ""}>
                  <CardContent className="py-4 flex items-start gap-4">
                    <SevIcon className={`h-5 w-5 mt-0.5 flex-shrink-0 ${sev.color}`} />
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
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
                    {alert.status === "active" && (
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" className="h-8 w-8">
                            <MoreVertical className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => handleAcknowledge(alert.id)}>
                            <Clock className="h-4 w-4 mr-2" />
                            Acknowledge
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={() => handleResolve(alert.id)}>
                            <CheckCircle className="h-4 w-4 mr-2" />
                            Resolve
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    )}
                  </CardContent>
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
                <Select value={ruleForm.operator} onValueChange={(v) => setRuleForm((s) => ({ ...s, operator: v }))}>
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
              <Select value={ruleForm.severity} onValueChange={(v) => setRuleForm((s) => ({ ...s, severity: v }))}>
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
