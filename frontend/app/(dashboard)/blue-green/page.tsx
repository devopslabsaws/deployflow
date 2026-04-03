"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  GitCompare, CheckCircle2, XCircle, ArrowRightLeft, RotateCcw,
  Plus, Activity, Clock, Zap, Server, ChevronRight, Loader2,
} from "lucide-react";
import { useAuthStore } from "@/store/auth-store";
import { useProjects } from "@/hooks/use-api";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { ScrollArea } from "@/components/ui/scroll-area";
import { apiClient } from "@/lib/api-client";
import { cn, formatRelativeTime } from "@/lib/utils";
import { toast } from "sonner";

interface BlueGreen {
  id: string;
  projectId: string;
  status: "Idle" | "Provisioning" | "HealthChecking" | "Switching" | "Live" | "RollingBack" | "Failed";
  blueContainerName?: string;
  bluePort: number;
  blueImageTag?: string;
  blueCommitSha?: string;
  greenContainerName?: string;
  greenPort: number;
  greenImageTag?: string;
  greenCommitSha?: string;
  activeSlot: "blue" | "green";
  healthCheckPath?: string;
  healthCheckRetries: number;
  autoPromote: boolean;
  greenTrafficPercent: number;
  switchStartedAt?: string;
  switchedAt?: string;
  lastError?: string;
  updatedAt: string;
}

interface BgLog {
  id: string;
  message: string;
  level: string;
  createdAt: string;
}

const STATUS_CONFIG: Record<BlueGreen["status"], { label: string; color: string; icon: React.ReactNode }> = {
  Idle:           { label: "Idle",            color: "bg-muted/60 text-muted-foreground border-border/40",     icon: <Activity className="h-3 w-3" /> },
  Provisioning:   { label: "Provisioning",    color: "bg-blue-500/10 text-blue-400 border-blue-500/30",       icon: <Loader2 className="h-3 w-3 animate-spin" /> },
  HealthChecking: { label: "Health Check",    color: "bg-amber-500/10 text-amber-400 border-amber-500/30",    icon: <Activity className="h-3 w-3 animate-pulse" /> },
  Switching:      { label: "Switching",       color: "bg-violet-500/10 text-violet-400 border-violet-500/30", icon: <ArrowRightLeft className="h-3 w-3 animate-pulse" /> },
  Live:           { label: "Live",            color: "bg-emerald-500/10 text-emerald-400 border-emerald-500/30", icon: <CheckCircle2 className="h-3 w-3" /> },
  RollingBack:    { label: "Rolling Back",    color: "bg-orange-500/10 text-orange-400 border-orange-500/30", icon: <RotateCcw className="h-3 w-3 animate-spin" /> },
  Failed:         { label: "Failed",          color: "bg-destructive/10 text-destructive border-destructive/30", icon: <XCircle className="h-3 w-3" /> },
};

export default function BlueGreenPage() {
  const qc = useQueryClient();
  const user = useAuthStore(s => s.user);
  const { data: projects } = useProjects();
  const [createOpen, setCreateOpen] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState({ projectId: "", bluePort: 3000, greenPort: 3001, healthCheckPath: "/health", healthCheckRetries: 5, healthCheckIntervalSeconds: 10, autoPromote: true, traefikRouterName: "" });
  const [provisionForm, setProvisionForm] = useState({ imageTag: "", commitSha: "", containerBaseName: "" });
  const [provisionOpen, setProvisionOpen] = useState(false);

  const { data: deployments, isLoading } = useQuery<BlueGreen[]>({
    queryKey: ["blue-green"],
    queryFn: () => apiClient.get("/blue-green"),
    staleTime: 15_000,
    refetchInterval: 10_000,
  });

  const { data: logs } = useQuery<{ blueGreen: BlueGreen; logs: BgLog[] }>({
    queryKey: ["blue-green", selectedId],
    queryFn: () => apiClient.get(`/blue-green/${selectedId}`),
    enabled: !!selectedId,
    staleTime: 5_000,
    refetchInterval: 5_000,
  });

  const createMutation = useMutation({
    mutationFn: (d: any) => apiClient.post("/blue-green", d),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["blue-green"] }); setCreateOpen(false); toast.success("Blue/Green workflow created."); },
    onError: (e: any) => toast.error(e.message),
  });

  const provisionMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: any }) => apiClient.post(`/blue-green/${id}/provision-green`, data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["blue-green"] }); setProvisionOpen(false); toast.success("Green slot provisioning started."); },
    onError: (e: any) => toast.error(e.message),
  });

  const promoteMutation = useMutation({
    mutationFn: (id: string) => apiClient.post(`/blue-green/${id}/promote`, {}),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["blue-green"] }); toast.success("Traffic promoted to green slot!"); },
    onError: (e: any) => toast.error(e.message),
  });

  const rollbackMutation = useMutation({
    mutationFn: (id: string) => apiClient.post(`/blue-green/${id}/rollback`, {}),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["blue-green"] }); toast.success("Rolled back to previous slot."); },
    onError: (e: any) => toast.error(e.message),
  });

  const selected = deployments?.find(d => d.id === selectedId) ?? logs?.blueGreen ?? null;

  return (
    <div className="mx-auto max-w-[1400px] space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
            <GitCompare className="h-6 w-6 text-blue-400" />
            Blue/Green Deployments
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Deploy new versions to the shadow (green) slot, health-check them, then instantly cut traffic — zero downtime.
          </p>
        </div>
        <Button className="gap-2 shrink-0" onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4" /> New Workflow
        </Button>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        {/* Workflow list */}
        <div className="space-y-3">
          <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide">Workflows</h2>
          {isLoading && Array.from({ length: 2 }).map((_, i) => <Skeleton key={i} className="h-36 w-full rounded-xl" />)}
          {deployments?.length === 0 && (
            <Card>
              <CardContent className="flex flex-col items-center justify-center py-10 text-center">
                <GitCompare className="h-9 w-9 text-muted-foreground/25 mb-3" />
                <p className="text-sm text-muted-foreground">No workflows yet.</p>
                <Button size="sm" variant="outline" className="mt-3 gap-1.5" onClick={() => setCreateOpen(true)}>
                  <Plus className="h-3.5 w-3.5" /> Create
                </Button>
              </CardContent>
            </Card>
          )}
          {deployments?.map(d => {
            const cfg = STATUS_CONFIG[d.status];
            return (
              <button key={d.id} type="button" className={cn("w-full text-left rounded-xl border bg-card p-4 space-y-3 transition-all hover:border-border/60 cursor-pointer", selectedId === d.id && "ring-1 ring-primary border-primary/40")} onClick={() => setSelectedId(d.id)}>
                <div className="flex items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <Badge className={cn("gap-1 text-[11px]", cfg.color)}>{cfg.icon}{cfg.label}</Badge>
                  </div>
                  <span className="text-[10px] text-muted-foreground">{formatRelativeTime(d.updatedAt)}</span>
                </div>
                {/* Blue/Green slot row */}
                <div className="flex items-center gap-2 text-xs">
                  <div className={cn("flex-1 rounded-lg px-2 py-1.5 text-center font-semibold border", d.activeSlot === "blue" ? "bg-blue-500/15 border-blue-500/40 text-blue-300" : "bg-muted/30 border-border/30 text-muted-foreground")}>
                    🔵 Blue :{d.bluePort}
                    {d.activeSlot === "blue" && <span className="ml-1 text-[9px] bg-blue-500/20 text-blue-400 px-1 rounded">LIVE</span>}
                  </div>
                  <ArrowRightLeft className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
                  <div className={cn("flex-1 rounded-lg px-2 py-1.5 text-center font-semibold border", d.activeSlot === "green" ? "bg-emerald-500/15 border-emerald-500/40 text-emerald-300" : "bg-muted/30 border-border/30 text-muted-foreground")}>
                    🟢 Green :{d.greenPort}
                    {d.activeSlot === "green" && <span className="ml-1 text-[9px] bg-emerald-500/20 text-emerald-400 px-1 rounded">LIVE</span>}
                  </div>
                </div>
                {d.lastError && <p className="text-[11px] text-destructive">{d.lastError}</p>}
              </button>
            );
          })}
        </div>

        {/* Detail panel */}
        <div className="lg:col-span-2 space-y-4">
          {!selectedId && (
            <Card>
              <CardContent className="flex flex-col items-center justify-center py-20 text-muted-foreground/40">
                <GitCompare className="h-10 w-10 mb-2" />
                <p className="text-sm">Select a workflow to view details.</p>
              </CardContent>
            </Card>
          )}
          {selected && (
            <>
              {/* Action buttons */}
              <div className="flex flex-wrap gap-2">
                <Button size="sm" variant="outline" className="gap-1.5" onClick={() => { setProvisionOpen(true); }}>
                  <Zap className="h-3.5 w-3.5 text-amber-400" /> Deploy Green Slot
                </Button>
                <Button size="sm" variant="outline" className="gap-1.5 border-emerald-500/30 text-emerald-400 hover:bg-emerald-500/10"
                  disabled={selected.status !== "HealthChecking" || promoteMutation.isPending}
                  onClick={() => promoteMutation.mutate(selected.id)}>
                  <ArrowRightLeft className="h-3.5 w-3.5" /> Promote to Live
                </Button>
                <Button size="sm" variant="outline" className="gap-1.5 border-orange-500/30 text-orange-400 hover:bg-orange-500/10"
                  disabled={rollbackMutation.isPending}
                  onClick={() => rollbackMutation.mutate(selected.id)}>
                  <RotateCcw className="h-3.5 w-3.5" /> Rollback
                </Button>
              </div>

              {/* Status overview */}
              <Card>
                <CardHeader className="pb-3">
                  <CardTitle className="text-sm">Workflow Overview</CardTitle>
                </CardHeader>
                <CardContent className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                  {[
                    { label: "Status", value: STATUS_CONFIG[selected.status].label },
                    { label: "Health Path", value: selected.healthCheckPath },
                    { label: "Retries", value: selected.healthCheckRetries },
                    { label: "Auto-Promote", value: selected.autoPromote ? "Yes" : "No" },
                    { label: "Blue Image", value: selected.blueImageTag ?? "—" },
                    { label: "Green Image", value: selected.greenImageTag ?? "—" },
                    { label: "Switched At", value: selected.switchedAt ? formatRelativeTime(selected.switchedAt) : "—" },
                    { label: "Green Traffic", value: `${selected.greenTrafficPercent}%` },
                  ].map(item => (
                    <div key={item.label} className="space-y-0.5">
                      <p className="text-[10px] text-muted-foreground uppercase">{item.label}</p>
                      <p className="text-sm font-mono font-semibold truncate">{item.value}</p>
                    </div>
                  ))}
                </CardContent>
              </Card>

              {/* Switch log */}
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm">Switch Log</CardTitle>
                  <CardDescription className="text-xs">Recent events for this workflow</CardDescription>
                </CardHeader>
                <CardContent className="p-0">
                  <ScrollArea className="h-56">
                    {(!logs?.logs || logs.logs.length === 0) && <p className="p-4 text-xs text-muted-foreground text-center">No log entries yet.</p>}
                    {logs?.logs.map(l => (
                      <div key={l.id} className={cn("flex gap-3 px-4 py-2 text-xs border-b border-border/20 last:border-0", l.level === "error" ? "text-destructive" : l.level === "warn" ? "text-amber-400" : "text-zinc-300")}>
                        <span className="shrink-0 text-muted-foreground font-mono">{new Date(l.createdAt).toLocaleTimeString()}</span>
                        <span>{l.message}</span>
                      </div>
                    ))}
                  </ScrollArea>
                </CardContent>
              </Card>
            </>
          )}
        </div>
      </div>

      {/* Create dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>New Blue/Green Workflow</DialogTitle></DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Project</Label>
              <select value={form.projectId} onChange={e => setForm(p => ({ ...p, projectId: e.target.value }))} className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm">
                <option value="">Select a project…</option>
                {projects?.data.map(pr => <option key={pr.id} value={pr.id}>{pr.name}</option>)}
              </select>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Blue Port (live)</Label>
                <Input type="number" value={form.bluePort} onChange={e => setForm(p => ({ ...p, bluePort: +e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Green Port (shadow)</Label>
                <Input type="number" value={form.greenPort} onChange={e => setForm(p => ({ ...p, greenPort: +e.target.value }))} />
              </div>
              <div className="col-span-2 space-y-1.5">
                <Label>Health Check Path</Label>
                <Input value={form.healthCheckPath} onChange={e => setForm(p => ({ ...p, healthCheckPath: e.target.value }))} placeholder="/health" />
              </div>
              <div className="space-y-1.5">
                <Label>Health Retries</Label>
                <Input type="number" min={1} value={form.healthCheckRetries} onChange={e => setForm(p => ({ ...p, healthCheckRetries: +e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Check Interval (s)</Label>
                <Input type="number" min={1} value={form.healthCheckIntervalSeconds} onChange={e => setForm(p => ({ ...p, healthCheckIntervalSeconds: +e.target.value }))} />
              </div>
            </div>
            <div className="flex items-center justify-between rounded-lg border px-4 py-3">
              <p className="text-sm font-medium">Auto-promote on health pass</p>
              <Switch checked={form.autoPromote} onCheckedChange={v => setForm(p => ({ ...p, autoPromote: v }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button onClick={() => createMutation.mutate({ tenantId: user?.tenantId ?? "", ...form })} disabled={createMutation.isPending || !form.projectId}>
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Provision green dialog */}
      <Dialog open={provisionOpen} onOpenChange={setProvisionOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Deploy to Green Slot</DialogTitle></DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Image Tag</Label>
              <Input value={provisionForm.imageTag} onChange={e => setProvisionForm(p => ({ ...p, imageTag: e.target.value }))} placeholder="my-app:v1.2.3" />
            </div>
            <div className="space-y-1.5">
              <Label>Commit SHA</Label>
              <Input value={provisionForm.commitSha} onChange={e => setProvisionForm(p => ({ ...p, commitSha: e.target.value }))} placeholder="abc1234" />
            </div>
            <div className="space-y-1.5">
              <Label>Container Base Name</Label>
              <Input value={provisionForm.containerBaseName} onChange={e => setProvisionForm(p => ({ ...p, containerBaseName: e.target.value }))} placeholder="my-app" />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setProvisionOpen(false)}>Cancel</Button>
            <Button onClick={() => selectedId && provisionMutation.mutate({ id: selectedId, data: provisionForm })}
              disabled={provisionMutation.isPending || !provisionForm.imageTag}>
              Deploy Green
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
