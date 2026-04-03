"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  GitPullRequest, Clock, CheckCircle2, XCircle, Trash2,
  Plus, ExternalLink, RefreshCw, Timer, Loader2, Moon,
  ChevronRight, AlertTriangle, Play,
} from "lucide-react";
import { useAuthStore } from "@/store/auth-store";
import { useProjects } from "@/hooks/use-api";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Progress } from "@/components/ui/progress";
import { apiClient } from "@/lib/api-client";
import { cn, formatRelativeTime } from "@/lib/utils";
import { toast } from "sonner";
import Link from "next/link";

type EphemeralStatus = "Pending" | "Provisioning" | "Running" | "Sleeping" | "Destroying" | "Destroyed" | "Failed";

interface EphemeralEnv {
  id: string;
  projectId: string;
  prNumber: string;
  prTitle: string;
  prUrl: string;
  branch: string;
  commitSha: string;
  authorName?: string;
  authorAvatarUrl?: string;
  status: EphemeralStatus;
  previewUrl?: string;
  assignedPort: number;
  ttlHours: number;
  expiresAt?: string;
  isExpired: boolean;
  provisionedAt?: string;
  lastActivityAt?: string;
  destroyedAt?: string;
  lastError?: string;
  createdAt: string;
}

interface Stats {
  total: number;
  running: number;
  pending: number;
  expired: number;
  destroyed: number;
}

const STATUS_CONFIG: Record<EphemeralStatus, { label: string; color: string; icon: React.ReactNode }> = {
  Pending:      { label: "Pending",      color: "bg-blue-500/10 text-blue-400 border-blue-500/30",       icon: <Clock className="h-3 w-3" /> },
  Provisioning: { label: "Provisioning", color: "bg-violet-500/10 text-violet-400 border-violet-500/30", icon: <Loader2 className="h-3 w-3 animate-spin" /> },
  Running:      { label: "Running",      color: "bg-emerald-500/10 text-emerald-400 border-emerald-500/30", icon: <CheckCircle2 className="h-3 w-3" /> },
  Sleeping:     { label: "Sleeping",     color: "bg-muted/60 text-muted-foreground border-border/40",    icon: <Moon className="h-3 w-3" /> },
  Destroying:   { label: "Destroying",   color: "bg-orange-500/10 text-orange-400 border-orange-500/30", icon: <Loader2 className="h-3 w-3 animate-spin" /> },
  Destroyed:    { label: "Destroyed",    color: "bg-muted/40 text-muted-foreground/50 border-border/20", icon: <Trash2 className="h-3 w-3" /> },
  Failed:       { label: "Failed",       color: "bg-destructive/10 text-destructive border-destructive/30", icon: <XCircle className="h-3 w-3" /> },
};

function ttlPercent(env: EphemeralEnv): number {
  if (!env.expiresAt || !env.createdAt) return 0;
  const total = new Date(env.expiresAt).getTime() - new Date(env.createdAt).getTime();
  const elapsed = Date.now() - new Date(env.createdAt).getTime();
  return Math.min(100, Math.round((elapsed / total) * 100));
}

function ttlRemaining(env: EphemeralEnv): string {
  if (!env.expiresAt) return "—";
  const diff = new Date(env.expiresAt).getTime() - Date.now();
  if (diff <= 0) return "Expired";
  const h = Math.floor(diff / 3_600_000);
  const m = Math.floor((diff % 3_600_000) / 60_000);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

export default function EphemeralEnvironmentsPage() {
  const qc = useQueryClient();
  const user = useAuthStore(s => s.user);
  const { data: projects } = useProjects();
  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState({ projectId: "", prNumber: "", prTitle: "", prUrl: "", branch: "", commitSha: "", authorName: "", ttlHours: 24, assignedPort: 4000 });

  const { data: envs, isLoading } = useQuery<EphemeralEnv[]>({
    queryKey: ["ephemeral"],
    queryFn: () => apiClient.get("/ephemeral"),
    staleTime: 15_000,
    refetchInterval: 20_000,
  });

  const { data: stats } = useQuery<Stats>({
    queryKey: ["ephemeral", "stats"],
    queryFn: () => apiClient.get("/ephemeral/stats"),
    staleTime: 20_000,
  });

  const createMutation = useMutation({
    mutationFn: (d: any) => apiClient.post("/ephemeral", d),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["ephemeral"] }); setCreateOpen(false); toast.success("Ephemeral environment queued for provisioning."); },
    onError: (e: any) => toast.error(e.message),
  });

  const destroyMutation = useMutation({
    mutationFn: (id: string) => apiClient.delete(`/ephemeral/${id}`),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["ephemeral"] }); toast.success("Environment destroyed."); },
  });

  const extendMutation = useMutation({
    mutationFn: (id: string) => apiClient.post(`/ephemeral/${id}/extend`, { additionalHours: 24 }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["ephemeral"] }); toast.success("TTL extended by 24 hours."); },
  });

  const activeEnvs = envs?.filter(e => e.status !== "Destroyed") ?? [];

  return (
    <div className="mx-auto max-w-[1400px] space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
            <GitPullRequest className="h-6 w-6 text-violet-400" />
            Ephemeral Preview Environments
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Temporary environments spun up per Pull Request — auto-destroyed after the TTL expires or PR is merged.
          </p>
        </div>
        <Button className="gap-2 shrink-0" onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4" /> New Environment
        </Button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-5">
        {[
          { label: "Total", value: stats?.total ?? 0, color: "text-foreground" },
          { label: "Running", value: stats?.running ?? 0, color: "text-emerald-400" },
          { label: "Pending", value: stats?.pending ?? 0, color: "text-blue-400" },
          { label: "Expired", value: stats?.expired ?? 0, color: "text-amber-400" },
          { label: "Destroyed", value: stats?.destroyed ?? 0, color: "text-muted-foreground" },
        ].map(s => (
          <Card key={s.label}>
            <CardContent className="p-4">
              <p className={cn("text-2xl font-bold", s.color)}>{s.value}</p>
              <p className="text-xs text-muted-foreground mt-0.5">{s.label}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Environment list */}
      {isLoading && <div className="space-y-3">{Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-36 w-full rounded-xl" />)}</div>}

      {activeEnvs.length === 0 && !isLoading && (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-16 text-center">
            <GitPullRequest className="h-12 w-12 text-muted-foreground/25 mb-3" />
            <p className="text-sm text-muted-foreground mb-1">No active preview environments.</p>
            <p className="text-xs text-muted-foreground/60 max-w-xs">Open a PR to automatically trigger a preview environment, or create one manually.</p>
            <Button size="sm" variant="outline" className="mt-4 gap-1.5" onClick={() => setCreateOpen(true)}>
              <Plus className="h-3.5 w-3.5" /> Create Manually
            </Button>
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {activeEnvs.map(env => {
          const cfg = STATUS_CONFIG[env.status];
          const pct = ttlPercent(env);
          const remaining = ttlRemaining(env);
          return (
            <Card key={env.id} className={cn("flex flex-col gap-0 overflow-hidden", env.isExpired && "border-amber-500/30")}>
              <CardHeader className="px-4 pt-4 pb-3">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <span className="text-lg">
                      {env.authorName ? env.authorName.charAt(0).toUpperCase() : "?"}
                    </span>
                    <div>
                      <Badge className={cn("gap-1 text-[11px]", cfg.color)}>{cfg.icon}{cfg.label}</Badge>
                    </div>
                  </div>
                  <a href={env.prUrl} target="_blank" rel="noreferrer" className="text-xs text-primary hover:underline flex items-center gap-0.5">
                    PR #{env.prNumber} <ExternalLink className="h-3 w-3" />
                  </a>
                </div>
                <p className="text-sm font-semibold mt-2 line-clamp-1">{env.prTitle}</p>
                <p className="text-xs text-muted-foreground font-mono">{env.branch} · {env.commitSha.slice(0, 7)}</p>
              </CardHeader>

              <CardContent className="px-4 pb-4 space-y-3 flex-1">
                {/* Preview URL */}
                {env.previewUrl && (
                  <a href={env.previewUrl} target="_blank" rel="noreferrer"
                    className="flex items-center gap-1.5 text-xs text-primary hover:underline font-mono">
                    <ExternalLink className="h-3 w-3 shrink-0" />
                    {env.previewUrl}
                  </a>
                )}

                {/* TTL bar */}
                <div>
                  <div className="flex justify-between text-[10px] text-muted-foreground mb-1">
                    <span className="flex items-center gap-1"><Timer className="h-3 w-3" /> TTL: {env.ttlHours}h</span>
                    <span className={cn(env.isExpired ? "text-amber-400 font-semibold" : "")}>{remaining}</span>
                  </div>
                  <Progress
                    value={pct}
                    className={cn("h-1.5", pct > 80 ? "[&>div]:bg-amber-400" : pct > 60 ? "[&>div]:bg-yellow-400" : "[&>div]:bg-emerald-400")}
                  />
                </div>

                {env.lastError && (
                  <div className="flex items-start gap-1.5 rounded-lg bg-destructive/8 border border-destructive/30 px-2.5 py-1.5">
                    <AlertTriangle className="h-3.5 w-3.5 shrink-0 text-destructive mt-0.5" />
                    <p className="text-[11px] text-destructive">{env.lastError}</p>
                  </div>
                )}

                {/* Actions */}
                <div className="flex gap-1.5 pt-1">
                  <Button size="sm" variant="outline" className="flex-1 h-7 text-xs gap-1" onClick={() => extendMutation.mutate(env.id)} disabled={extendMutation.isPending}>
                    <RefreshCw className="h-3 w-3" /> +24h
                  </Button>
                  <Button size="sm" variant="outline" className="flex-1 h-7 text-xs gap-1 text-destructive hover:text-destructive border-destructive/30 hover:bg-destructive/10"
                    onClick={() => destroyMutation.mutate(env.id)} disabled={destroyMutation.isPending}>
                    <Trash2 className="h-3 w-3" /> Destroy
                  </Button>
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>

      {/* Create dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>New Ephemeral Environment</DialogTitle></DialogHeader>
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
                <Label>PR Number</Label>
                <Input value={form.prNumber} onChange={e => setForm(p => ({ ...p, prNumber: e.target.value }))} placeholder="42" />
              </div>
              <div className="space-y-1.5">
                <Label>Branch</Label>
                <Input value={form.branch} onChange={e => setForm(p => ({ ...p, branch: e.target.value }))} placeholder="feature/my-feature" />
                <p className="text-xs text-muted-foreground">e.g. <code className="bg-muted px-1 rounded font-mono text-[10px]">feature/login</code> or <code className="bg-muted px-1 rounded font-mono text-[10px]">fix/bug-123</code></p>
              </div>
              <div className="col-span-2 space-y-1.5">
                <Label>PR Title</Label>
                <Input value={form.prTitle} onChange={e => setForm(p => ({ ...p, prTitle: e.target.value }))} placeholder="Add user authentication" />
              </div>
              <div className="col-span-2 space-y-1.5">
                <Label>PR URL</Label>
                <Input value={form.prUrl} onChange={e => setForm(p => ({ ...p, prUrl: e.target.value }))} placeholder="https://github.com/org/repo/pull/42" />
                <p className="text-xs text-muted-foreground">Format: <code className="bg-muted px-1 rounded font-mono text-[10px]">https://github.com/owner/repo/pull/{'{number}'}</code></p>
              </div>
              <div className="space-y-1.5">
                <Label>Commit SHA</Label>
                <Input value={form.commitSha} onChange={e => setForm(p => ({ ...p, commitSha: e.target.value }))} placeholder="abc1234" />
              </div>
              <div className="space-y-1.5">
                <Label>Assigned Port</Label>
                <Input type="number" value={form.assignedPort} onChange={e => setForm(p => ({ ...p, assignedPort: +e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>TTL (hours)</Label>
                <Input type="number" min={1} value={form.ttlHours} onChange={e => setForm(p => ({ ...p, ttlHours: +e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <Label>Author Name (optional)</Label>
                <Input value={form.authorName} onChange={e => setForm(p => ({ ...p, authorName: e.target.value }))} />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button onClick={() => createMutation.mutate({ tenantId: user?.tenantId ?? "", ...form })}
              disabled={createMutation.isPending || !form.prNumber || !form.branch || !form.commitSha || !form.projectId}>
              Provision
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
