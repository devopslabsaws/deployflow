"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Cpu, MemoryStick, ArrowUp, ArrowDown,
  Plus, Trash2, Zap, ChevronDown, Activity, RefreshCw, Moon, Power,
} from "lucide-react";
import { useAuthStore } from "@/store/auth-store";
import { useProjects } from "@/hooks/use-api";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { apiClient } from "@/lib/api-client";
import { cn, formatRelativeTime } from "@/lib/utils";
import { toast } from "sonner";

interface ScalingPolicy {
  id: string;
  name: string;
  containerName: string;
  projectId: string;
  horizontalEnabled: boolean;
  minReplicas: number;
  maxReplicas: number;
  currentReplicas: number;
  cpuScaleUpThreshold: number;
  cpuScaleDownThreshold: number;
  memoryScaleUpThreshold: number;
  memoryScaleDownThreshold: number;
  scaleCooldownSeconds: number;
  verticalEnabled: boolean;
  cpuLimit?: string;
  memoryLimit?: string;
  scaleToZeroEnabled: boolean;
  scaleToZeroAfterMinutes: number;
  isScaledToZero: boolean;
  isActive: boolean;
  lastScaledAt?: string;
  lastScalingDirection: "Up" | "Down" | "None";
  createdAt: string;
}

interface ScalingEvent {
  id: string;
  direction: "Up" | "Down" | "None";
  trigger: string;
  fromReplicas: number;
  toReplicas: number;
  cpuPercentAtTime: number;
  memoryPercentAtTime: number;
  notes?: string;
  succeeded: boolean;
  createdAt: string;
}

const DEFAULT_FORM = {
  name: "", containerName: "",
  horizontalEnabled: true, minReplicas: 1, maxReplicas: 5, currentReplicas: 1,
  cpuScaleUpThreshold: 80, cpuScaleDownThreshold: 30,
  memoryScaleUpThreshold: 85, memoryScaleDownThreshold: 40,
  scaleCooldownSeconds: 120,
  verticalEnabled: false, cpuLimit: "", memoryLimit: "",
  scaleToZeroEnabled: false, scaleToZeroAfterMinutes: 30,
  isActive: true,
  projectId: "",
};

export default function AutoScalingPage() {
  const qc = useQueryClient();
  const user = useAuthStore(s => s.user);
  const { data: projects } = useProjects();
  const [open, setOpen] = useState(false);
  const [scaleOpen, setScaleOpen] = useState(false);
  const [selectedPolicy, setSelectedPolicy] = useState<ScalingPolicy | null>(null);
  const [scaleTarget, setScaleTarget] = useState(1);
  const [form, setForm] = useState(DEFAULT_FORM);

  const { data: policies, isLoading } = useQuery<ScalingPolicy[]>({
    queryKey: ["scaling", "policies"],
    queryFn: () => apiClient.get("/scaling/policies"),
    staleTime: 30_000,
  });

  const { data: events } = useQuery<ScalingEvent[]>({
    queryKey: ["scaling", "events"],
    queryFn: () => apiClient.get("/scaling/events?limit=30"),
    staleTime: 15_000,
    refetchInterval: 30_000,
  });

  const createMutation = useMutation({
    mutationFn: (data: any) => apiClient.post("/scaling/policies", data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["scaling"] }); setOpen(false); setForm(DEFAULT_FORM); toast.success("Scaling policy created."); },
    onError: (e: any) => toast.error(e.message),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => apiClient.delete(`/scaling/policies/${id}`),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["scaling"] }); toast.success("Policy deleted."); },
  });

  const scaleMutation = useMutation({
    mutationFn: ({ id, replicas }: { id: string; replicas: number }) =>
      apiClient.post(`/scaling/policies/${id}/scale`, { replicas, reason: "Manual scale from UI" }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["scaling"] });
      setScaleOpen(false);
      toast.success("Scale command sent.");
    },
    onError: (e: any) => toast.error(e.message),
  });

  const toggleActiveMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      apiClient.patch(`/scaling/policies/${id}`, { isActive }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["scaling"] }); toast.success("Policy updated."); },
    onError: (e: any) => toast.error(e.message),
  });

  const f = (key: keyof typeof form, val: any) => setForm(p => ({ ...p, [key]: val }));

  const handleSubmit = () => {
    createMutation.mutate({
      tenantId: user?.tenantId ?? "",
      ...form,
    });
  };

  const directionIcon = (d: string) =>
    d === "Up" ? <ArrowUp className="h-3.5 w-3.5 text-emerald-400" /> :
    d === "Down" ? <ArrowDown className="h-3.5 w-3.5 text-orange-400" /> :
    <span className="h-3.5 w-3.5" />;

  return (
    <div className="mx-auto max-w-[1400px] space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
            <Zap className="h-6 w-6 text-amber-400" />
            Auto-Scaling
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Automatically scale replicas and resource limits based on CPU / RAM thresholds.
            Scale-to-Zero saves costs on idle dev environments.
          </p>
        </div>
        <Button className="gap-2 shrink-0" onClick={() => setOpen(true)}>
          <Plus className="h-4 w-4" /> New Policy
        </Button>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        {[
          { label: "Active Policies", value: policies?.filter(p => p.isActive).length ?? 0, icon: Activity, color: "text-blue-400" },
          { label: "Scaled-to-Zero", value: policies?.filter(p => p.isScaledToZero).length ?? 0, icon: Moon, color: "text-violet-400" },
          { label: "Scale-Up Events", value: events?.filter(e => e.direction === "Up").length ?? 0, icon: ArrowUp, color: "text-emerald-400" },
          { label: "Scale-Down Events", value: events?.filter(e => e.direction === "Down").length ?? 0, icon: ArrowDown, color: "text-orange-400" },
        ].map(s => (
          <Card key={s.label}>
            <CardContent className="flex items-center gap-3 p-4">
              <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-muted/60">
                <s.icon className={cn("h-4 w-4", s.color)} />
              </div>
              <div>
                <p className="text-xl font-bold">{s.value}</p>
                <p className="text-xs text-muted-foreground">{s.label}</p>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        {/* Policies list */}
        <div className="lg:col-span-2 space-y-3">
          <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide">Policies</h2>
          {isLoading && Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-28 w-full rounded-xl" />)}
          {policies?.length === 0 && (
            <Card>
              <CardContent className="flex flex-col items-center justify-center py-12 text-center">
                <Zap className="h-10 w-10 text-muted-foreground/30 mb-3" />
                <p className="text-sm text-muted-foreground">No scaling policies yet.</p>
                <Button size="sm" variant="outline" className="mt-3 gap-1.5" onClick={() => setOpen(true)}>
                  <Plus className="h-3.5 w-3.5" /> Create Policy
                </Button>
              </CardContent>
            </Card>
          )}
          {policies?.map(p => (
            <Card key={p.id} className={cn("transition-all hover:border-border/60", !p.isActive && "opacity-60")}>
              <CardContent className="p-4 space-y-3">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="font-semibold text-sm">{p.name}</span>
                      <Badge variant="outline" className="text-[10px] font-mono">{p.containerName}</Badge>
                      {p.isScaledToZero && <Badge variant="secondary" className="text-[10px] text-violet-400 border-violet-400/30 bg-violet-400/10"><Moon className="h-3 w-3 mr-1" />Scaled to Zero</Badge>}
                      {!p.isActive && <Badge variant="outline" className="text-[10px]">Inactive</Badge>}
                    </div>
                    {p.lastScaledAt && (
                      <p className="text-xs text-muted-foreground mt-0.5">
                        Last scaled {formatRelativeTime(p.lastScaledAt)} · {directionIcon(p.lastScalingDirection)} {p.lastScalingDirection}
                      </p>
                    )}
                  </div>
                  <div className="flex gap-1.5 shrink-0">
                    <Button size="sm" variant="outline" className="h-7 text-xs gap-1" onClick={() => { setSelectedPolicy(p); setScaleTarget(p.currentReplicas); setScaleOpen(true); }}>
                      <RefreshCw className="h-3 w-3" /> Scale
                    </Button>
                    <Button size="sm" variant="outline" className={cn("h-7 w-7 p-0", p.isActive ? "text-emerald-400 hover:text-emerald-300" : "text-muted-foreground hover:text-foreground")} title={p.isActive ? "Deactivate policy" : "Activate policy"} onClick={() => toggleActiveMutation.mutate({ id: p.id, isActive: !p.isActive })}>
                      <Power className="h-3.5 w-3.5" />
                    </Button>
                    <Button size="sm" variant="ghost" className="h-7 w-7 p-0 text-destructive hover:text-destructive" onClick={() => deleteMutation.mutate(p.id)}>
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                </div>

                {/* Metrics row */}
                <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 text-xs">
                  <div className="rounded-lg bg-muted/40 px-3 py-2">
                    <p className="text-muted-foreground">Replicas</p>
                    <p className="font-mono font-bold text-base">{p.currentReplicas}<span className="text-muted-foreground font-normal text-xs">/{p.maxReplicas}</span></p>
                  </div>
                  <div className="rounded-lg bg-muted/40 px-3 py-2">
                    <p className="text-muted-foreground">CPU Threshold</p>
                    <p className="font-mono font-bold">{p.cpuScaleDownThreshold}%–{p.cpuScaleUpThreshold}%</p>
                  </div>
                  <div className="rounded-lg bg-muted/40 px-3 py-2">
                    <p className="text-muted-foreground">RAM Threshold</p>
                    <p className="font-mono font-bold">{p.memoryScaleDownThreshold}%–{p.memoryScaleUpThreshold}%</p>
                  </div>
                  <div className="rounded-lg bg-muted/40 px-3 py-2">
                    <p className="text-muted-foreground">Cooldown</p>
                    <p className="font-mono font-bold">{p.scaleCooldownSeconds}s</p>
                  </div>
                </div>

                <div className="flex gap-2 text-[11px] text-muted-foreground flex-wrap">
                  {p.horizontalEnabled && <span className="flex items-center gap-1 rounded-full bg-emerald-500/10 text-emerald-400 px-2 py-0.5 border border-emerald-500/20">↔ Horizontal</span>}
                  {p.verticalEnabled && <span className="flex items-center gap-1 rounded-full bg-blue-500/10 text-blue-400 px-2 py-0.5 border border-blue-500/20">↕ Vertical</span>}
                  {p.scaleToZeroEnabled && <span className="flex items-center gap-1 rounded-full bg-violet-500/10 text-violet-400 px-2 py-0.5 border border-violet-500/20"><Moon className="h-3 w-3" /> Scale-to-Zero@{p.scaleToZeroAfterMinutes}m</span>}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>

        {/* Event history */}
        <div className="space-y-3">
          <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide">Scaling History</h2>
          <Card>
            <CardContent className="p-0 divide-y divide-border/40">
              {(!events || events.length === 0) && (
                <p className="text-xs text-muted-foreground p-4 text-center">No events yet.</p>
              )}
              {events?.slice(0, 20).map(e => (
                <div key={e.id} className="flex items-start gap-3 p-3">
                  <div className={cn("mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full",
                    e.direction === "Up" ? "bg-emerald-500/10" : e.direction === "Down" ? "bg-orange-500/10" : "bg-muted/40"
                  )}>
                    {directionIcon(e.direction)}
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="text-xs font-medium">{e.fromReplicas} → {e.toReplicas} replicas</p>
                    <p className="text-[10px] text-muted-foreground">{e.trigger} · CPU {e.cpuPercentAtTime.toFixed(0)}%</p>
                    <p className="text-[10px] text-muted-foreground/60">{formatRelativeTime(e.createdAt)}</p>
                  </div>
                  {!e.succeeded && <Badge variant="destructive" className="text-[10px]">Failed</Badge>}
                </div>
              ))}
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Manual scale dialog */}
      <Dialog open={scaleOpen} onOpenChange={setScaleOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>Manual Scale — {selectedPolicy?.name}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Replicas: {scaleTarget}</Label>
              <input
                type="range"
                min={0}
                max={selectedPolicy?.maxReplicas ?? 10}
                step={1}
                value={scaleTarget}
                onChange={e => setScaleTarget(Number(e.target.value))}
                className="mt-3 w-full accent-primary"
              />
              <div className="flex justify-between text-[10px] text-muted-foreground mt-1">
                <span>0 (scale-to-zero)</span>
                <span>{selectedPolicy?.maxReplicas ?? 10} (max)</span>
              </div>
            </div>
            {scaleTarget === 0 && (
              <p className="text-xs text-amber-400 bg-amber-500/10 border border-amber-500/20 rounded-lg px-3 py-2">
                Setting replicas to 0 will stop all containers for this service.
              </p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setScaleOpen(false)}>Cancel</Button>
            <Button onClick={() => selectedPolicy && scaleMutation.mutate({ id: selectedPolicy.id, replicas: scaleTarget })}
              disabled={scaleMutation.isPending}>
              Apply
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Create policy dialog */}
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>New Scaling Policy</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="grid grid-cols-2 gap-3">
              <div className="col-span-2 space-y-1.5">
                <Label>Project</Label>
                <select value={form.projectId} onChange={e => f("projectId", e.target.value)} className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm">
                  <option value="">Select a project…</option>
                  {projects?.data.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                </select>
              </div>
              <div className="col-span-2 space-y-1.5">
                <Label>Policy Name</Label>
                <Input value={form.name} onChange={e => f("name", e.target.value)} placeholder="my-api autoscaler" />
              </div>
              <div className="col-span-2 space-y-1.5">
                <Label>Container / Service Name</Label>
                <Input value={form.containerName} onChange={e => f("containerName", e.target.value)} placeholder="my-api" />
              </div>
            </div>

            <div className="space-y-3 rounded-xl border border-border/50 p-4">
              <div className="flex items-center justify-between">
                <p className="text-sm font-semibold">Horizontal Scaling (Replicas)</p>
                <Switch checked={form.horizontalEnabled} onCheckedChange={v => f("horizontalEnabled", v)} />
              </div>
              {form.horizontalEnabled && (
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1.5">
                    <Label>Min Replicas</Label>
                    <Input type="number" min={1} value={form.minReplicas} onChange={e => f("minReplicas", +e.target.value)} />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Max Replicas</Label>
                    <Input type="number" min={1} value={form.maxReplicas} onChange={e => f("maxReplicas", +e.target.value)} />
                  </div>
                  <div className="space-y-1.5">
                    <Label>CPU Scale-Up % (↑ at)</Label>
                    <Input type="number" min={1} max={100} value={form.cpuScaleUpThreshold} onChange={e => f("cpuScaleUpThreshold", +e.target.value)} />
                  </div>
                  <div className="space-y-1.5">
                    <Label>CPU Scale-Down % (↓ at)</Label>
                    <Input type="number" min={1} max={100} value={form.cpuScaleDownThreshold} onChange={e => f("cpuScaleDownThreshold", +e.target.value)} />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Cooldown (seconds)</Label>
                    <Input type="number" min={10} value={form.scaleCooldownSeconds} onChange={e => f("scaleCooldownSeconds", +e.target.value)} />
                  </div>
                </div>
              )}
            </div>

            <div className="space-y-3 rounded-xl border border-border/50 p-4">
              <div className="flex items-center justify-between">
                <p className="text-sm font-semibold">Vertical Scaling (Resource Limits)</p>
                <Switch checked={form.verticalEnabled} onCheckedChange={v => f("verticalEnabled", v)} />
              </div>
              {form.verticalEnabled && (
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1.5">
                    <Label>CPU Limit</Label>
                    <Input value={form.cpuLimit} onChange={e => f("cpuLimit", e.target.value)} placeholder="1.5" />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Memory Limit</Label>
                    <Input value={form.memoryLimit} onChange={e => f("memoryLimit", e.target.value)} placeholder="512m" />
                  </div>
                </div>
              )}
            </div>

            <div className="space-y-3 rounded-xl border border-border/50 p-4">
              <div className="flex items-center justify-between">
                <p className="text-sm font-semibold">Scale-to-Zero (Dev Environments)</p>
                <Switch checked={form.scaleToZeroEnabled} onCheckedChange={v => f("scaleToZeroEnabled", v)} />
              </div>
              {form.scaleToZeroEnabled && (
                <div className="space-y-1.5">
                  <Label>Idle timeout (minutes)</Label>
                  <Input type="number" min={5} value={form.scaleToZeroAfterMinutes} onChange={e => f("scaleToZeroAfterMinutes", +e.target.value)} />
                  <p className="text-xs text-muted-foreground">Container will be stopped after this many minutes of no requests.</p>
                </div>
              )}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button onClick={handleSubmit} disabled={createMutation.isPending || !form.name || !form.containerName || !form.projectId}>
              Create Policy
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
