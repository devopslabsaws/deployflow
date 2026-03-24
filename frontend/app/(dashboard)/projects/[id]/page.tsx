"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft, Rocket, Settings, GitBranch, ExternalLink,
  CheckCircle2, XCircle, AlertTriangle, Clock, RefreshCw,
  Globe, Code2, Server, Calendar, Activity, StopCircle, Tag,
  Terminal, Play, Loader2, Webhook, CalendarClock, KeyRound,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { useProject, useDeployments, useCancelDeployment, queryKeys } from "@/hooks/use-api";
import { useActionFeedback } from "@/hooks/use-action-feedback";
import { apiClient } from "@/lib/api-client";
import { formatRelativeTime, cn } from "@/lib/utils";
import { toast } from "sonner";

export default function ProjectDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const qc = useQueryClient();

  const { data: project, isLoading: projectLoading } = useProject(id);
  const { data: deploymentsData, isLoading: deploymentsLoading } = useDeployments({ projectId: id });

  // Project response from backend has: branch, totalDeployments, autoDeploy, buildCommand etc at top level
  const p = project as any;
  const deployments: any[] = (deploymentsData as any)?.data ?? [];

  const cancelDeploy = useCancelDeployment();

  const triggerDeploy = useMutation({
    mutationFn: () =>
      apiClient.post("/deployments", { projectId: id, trigger: "manual" }),
    onSuccess: () => {
      toast.success("Deployment triggered!");
      qc.invalidateQueries({ queryKey: queryKeys.deployments.all });
      qc.invalidateQueries({ queryKey: queryKeys.projects.detail(id) });
    },
    onError: (e: any) =>
      toast.error("Failed to trigger deployment", { description: e.message }),
  });

  const deployFeedback = useActionFeedback(
    () => triggerDeploy.mutateAsync(),
    { successDuration: 3000 },
  );

  if (projectLoading) {
    return (
      <div className="mx-auto max-w-[1400px] space-y-6">
        <Skeleton className="h-10 w-64" />
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          {[...Array(4)].map((_, i) => <Skeleton key={i} className="h-20 rounded-xl" />)}
        </div>
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          <Skeleton className="h-64 rounded-xl" />
          <Skeleton className="lg:col-span-2 h-64 rounded-xl" />
        </div>
      </div>
    );
  }

  if (!project) {
    return (
      <div className="flex flex-col items-center justify-center py-20">
        <XCircle className="mb-3 h-10 w-10 text-muted-foreground/40" />
        <p className="text-sm text-muted-foreground">Project not found.</p>
        <Button asChild className="mt-4 h-8 text-xs" variant="outline" size="sm">
          <Link href="/projects">Back to Projects</Link>
        </Button>
      </div>
    );
  }

  const statusStyle: Record<string, string> = {
    active:    "text-emerald-500 border-emerald-500/30 bg-emerald-500/10",
    archived:  "text-muted-foreground border-border bg-muted/50",
    suspended: "text-amber-500 border-amber-500/30 bg-amber-500/10",
  };

  return (
    <div className="mx-auto max-w-[1400px] space-y-6">

      {/* ── Header ──────────────────────────────────────────────── */}
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" asChild className="h-8 w-8 p-0 shrink-0">
          <Link href="/projects"><ArrowLeft className="h-4 w-4" /></Link>
        </Button>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <h1 className="text-lg font-bold truncate">{p?.name}</h1>
            <Badge variant="outline" className={cn("text-[10px] px-1.5 py-0 h-[18px]", statusStyle[p?.status] ?? "")}>
              {p?.status}
            </Badge>
            {p?.autoDeploy && (
              <Badge variant="outline" className="text-[10px] px-1.5 py-0 h-[18px] border-primary/20 bg-primary/5 text-primary">
                auto-deploy
              </Badge>
            )}
          </div>
          {p?.description && (
            <p className="text-xs text-muted-foreground mt-0.5 truncate">{p.description}</p>
          )}
        </div>
        <div className="flex items-center gap-2 shrink-0">
          {p?.repositoryUrl && (
            <Button size="sm" variant="ghost" className="h-8 w-8 p-0" asChild>
              <a href={p.repositoryUrl} target="_blank" rel="noopener noreferrer">
                <ExternalLink className="h-3.5 w-3.5" />
              </a>
            </Button>
          )}
          <Button size="sm" variant="outline" className="h-8 gap-1.5 text-xs" asChild>
            <Link href={`/projects/${id}/settings`}>
              <Settings className="h-3.5 w-3.5" />Settings
            </Link>
          </Button>
          <Button size="sm" variant="outline" className="h-8 gap-1.5 text-xs" asChild>
            <Link href={`/projects/${id}/webhooks`}>
              <Webhook className="h-3.5 w-3.5" />Webhooks
            </Link>
          </Button>
          <Button size="sm" variant="outline" className="h-8 gap-1.5 text-xs" asChild>
            <Link href={`/projects/${id}/env-variables`}>
              <KeyRound className="h-3.5 w-3.5" />Env Vars
            </Link>
          </Button>
          <Button size="sm" variant="outline" className="h-8 gap-1.5 text-xs" asChild>
            <Link href={`/projects/${id}/scheduled-tasks`}>
              <CalendarClock className="h-3.5 w-3.5" />Tasks
            </Link>
          </Button>
          <Button
            size="sm"
            className={cn(
              "h-8 gap-1.5 text-xs transition-all",
              deployFeedback.state === "success" && "bg-emerald-600 hover:bg-emerald-500 text-white",
              deployFeedback.state === "error" && "bg-destructive hover:bg-destructive/90 text-white",
            )}
            onClick={deployFeedback.run}
            disabled={deployFeedback.state === "loading" || p?.status !== "active"}
          >
            {deployFeedback.state === "loading" && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
            {deployFeedback.state === "success" && <CheckCircle2 className="h-3.5 w-3.5" />}
            {deployFeedback.state === "error" && <XCircle className="h-3.5 w-3.5" />}
            {deployFeedback.state === "idle" && <Rocket className="h-3.5 w-3.5" />}
            {deployFeedback.state === "loading" && "Deploying…"}
            {deployFeedback.state === "success" && "Deployed!"}
            {deployFeedback.state === "error" && "Failed"}
            {deployFeedback.state === "idle" && "Deploy Now"}
          </Button>
        </div>
      </div>

      {/* ── Stats Row ────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard
          icon={Activity}
          label="Last Status"
          value={p?.lastDeploymentStatus ?? "—"}
          valueClass={deployStatusClass(p?.lastDeploymentStatus)}
        />
        <StatCard
          icon={GitBranch}
          label="Branch"
          value={p?.branch ?? "—"}
          mono
        />
        <StatCard
          icon={Rocket}
          label="Total Deployments"
          value={String(p?.totalDeployments ?? 0)}
        />
        <StatCard
          icon={Calendar}
          label="Last Deployed"
          value={p?.lastDeployedAt ? formatRelativeTime(p.lastDeployedAt) : "Never"}
        />
      </div>

      {/* ── Main Content ─────────────────────────────────────────── */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">

        {/* ── Left: Details ─────────────────────── */}
        <div className="space-y-4">
          <Card className="glass-card">
            <CardHeader className="pb-3 pt-4 px-4">
              <CardTitle className="text-sm">Project Details</CardTitle>
            </CardHeader>
            <CardContent className="px-4 pb-4 space-y-3">
              <DetailRow icon={GitBranch} label="Repository">
                {p?.repositoryUrl ? (
                  <a
                    href={p.repositoryUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-primary hover:underline truncate max-w-[160px] font-mono text-xs"
                  >
                    {p.repositoryUrl.replace(/^https?:\/\/(www\.)?/, "")}
                  </a>
                ) : "—"}
              </DetailRow>

              <DetailRow icon={GitBranch} label="Branch">
                <span className="font-mono text-xs">{p?.branch ?? "—"}</span>
              </DetailRow>

              {p?.framework && (
                <DetailRow icon={Code2} label="Framework">
                  <span className="capitalize">{p.framework || "—"}</span>
                </DetailRow>
              )}

              {p?.customDomain && (
                <DetailRow icon={Globe} label="Domain">
                  <span className="font-mono text-xs">{p.customDomain}</span>
                </DetailRow>
              )}

              {p?.assignedServerName && (
                <DetailRow icon={Server} label="Server">
                  {p.assignedServerName}
                </DetailRow>
              )}

              <Separator className="my-1" />

              {p?.buildCommand && (
                <DetailRow icon={Terminal} label="Build">
                  <code className="text-[11px] bg-muted/50 px-1.5 py-0.5 rounded font-mono">
                    {p.buildCommand}
                  </code>
                </DetailRow>
              )}

              {p?.startCommand && (
                <DetailRow icon={Play} label="Start">
                  <code className="text-[11px] bg-muted/50 px-1.5 py-0.5 rounded font-mono">
                    {p.startCommand}
                  </code>
                </DetailRow>
              )}

              {p?.dockerfilePath && (
                <DetailRow icon={Code2} label="Dockerfile">
                  <code className="text-[11px] bg-muted/50 px-1.5 py-0.5 rounded font-mono">
                    {p.dockerfilePath}
                  </code>
                </DetailRow>
              )}

              {p?.tags?.length > 0 && (
                <>
                  <Separator className="my-1" />
                  <DetailRow icon={Tag} label="Tags">
                    <div className="flex flex-wrap gap-1">
                      {p.tags.map((t: string) => (
                        <Badge key={t} variant="secondary" className="text-[10px] px-1.5 py-0 h-[18px] font-normal">
                          {t}
                        </Badge>
                      ))}
                    </div>
                  </DetailRow>
                </>
              )}

              {p?.createdAt && (
                <>
                  <Separator className="my-1" />
                  <DetailRow icon={Calendar} label="Created">
                    {formatRelativeTime(p.createdAt)}
                  </DetailRow>
                </>
              )}
            </CardContent>
          </Card>
        </div>

        {/* ── Right: Deployments table ──────────── */}
        <div className="lg:col-span-2">
          <Card className="glass-card">
            <CardHeader className="pb-3 pt-4 px-4 flex flex-row items-center justify-between">
              <CardTitle className="text-sm">Deployments</CardTitle>
              <Button
                size="sm"
                variant="ghost"
                className="h-7 w-7 p-0"
                onClick={() => qc.invalidateQueries({ queryKey: queryKeys.deployments.all })}
              >
                <RefreshCw className="h-3.5 w-3.5" />
              </Button>
            </CardHeader>
            <CardContent className="p-0">
              {deploymentsLoading ? (
                <div className="space-y-px">
                  {[...Array(4)].map((_, i) => <Skeleton key={i} className="h-14 rounded-none first:rounded-t last:rounded-b" />)}
                </div>
              ) : deployments.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-14 text-center">
                  <Rocket className="mb-3 h-8 w-8 text-muted-foreground/30" />
                  <p className="text-sm font-medium">No deployments yet</p>
                  <p className="text-xs text-muted-foreground mt-0.5 mb-4">
                    Trigger your first deployment to get started.
                  </p>
                  <Button
                    size="sm"
                    className="h-8 gap-1.5 text-xs"
                    onClick={deployFeedback.run}
                    disabled={deployFeedback.state === "loading"}
                  >
                    <Rocket className="h-3 w-3" />Deploy Now
                  </Button>
                </div>
              ) : (
                <div className="divide-y divide-border/40">
                  {deployments.slice(0, 15).map((d: any) => (
                    <DeploymentRow
                      key={d.id}
                      deployment={d}
                      onCancel={() => cancelDeploy.mutate(d.id)}
                      cancelling={cancelDeploy.isPending}
                    />
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

/* ── Helper Components ──────────────────────────────────────────── */

function StatCard({
  icon: Icon, label, value, valueClass, mono,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
  valueClass?: string;
  mono?: boolean;
}) {
  return (
    <div className="flex items-center gap-3 rounded-xl border border-border/60 bg-card px-4 py-3 shadow-sm">
      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-muted/50">
        <Icon className="h-3.5 w-3.5 text-muted-foreground" />
      </div>
      <div className="min-w-0">
        <p className={cn("text-sm font-semibold truncate", valueClass, mono && "font-mono")}>
          {value}
        </p>
        <p className="text-[11px] text-muted-foreground">{label}</p>
      </div>
    </div>
  );
}

function DetailRow({
  icon: Icon, label, children,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-start gap-2 text-xs">
      <Icon className="h-3.5 w-3.5 mt-0.5 shrink-0 text-muted-foreground" />
      <span className="text-muted-foreground w-20 shrink-0">{label}</span>
      <span className="flex-1 min-w-0">{children}</span>
    </div>
  );
}

function DeploymentRow({
  deployment: d, onCancel, cancelling,
}: {
  deployment: any;
  onCancel: () => void;
  cancelling: boolean;
}) {
  const isRunning = d.status === "running" || d.status === "pending" || d.status === "building";

  return (
    <div className="flex items-center gap-3 px-4 py-3 hover:bg-muted/20 transition-colors">
      <DeployStatusIcon status={d.status} />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className={cn("text-xs font-medium", deployStatusClass(d.status))}>
            {d.status}
          </span>
          {d.commitSha && (
            <code className="text-[10px] text-muted-foreground bg-muted/50 px-1 rounded">
              {d.commitSha.slice(0, 7)}
            </code>
          )}
          <span className="text-[10px] text-muted-foreground/60 capitalize">
            via {d.trigger}
          </span>
        </div>
        {d.commitMessage && (
          <p className="text-[11px] text-muted-foreground truncate mt-0.5">{d.commitMessage}</p>
        )}
      </div>
      <div className="flex items-center gap-2 shrink-0">
        {d.startedAt && (
          <span className="text-[11px] text-muted-foreground hidden sm:block">
            {formatRelativeTime(d.startedAt)}
          </span>
        )}
        {isRunning && (
          <Button
            size="sm"
            variant="ghost"
            className="h-7 w-7 p-0 text-destructive hover:text-destructive"
            onClick={onCancel}
            disabled={cancelling}
          >
            <StopCircle className="h-3.5 w-3.5" />
          </Button>
        )}
      </div>
    </div>
  );
}

function DeployStatusIcon({ status }: { status: string }) {
  if (status === "succeeded" || status === "success")
    return <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-500" />;
  if (status === "failed")
    return <XCircle className="h-4 w-4 shrink-0 text-destructive" />;
  if (status === "running" || status === "building")
    return <RefreshCw className="h-4 w-4 shrink-0 text-blue-500 animate-spin" />;
  if (status === "queued" || status === "pending")
    return <Clock className="h-4 w-4 shrink-0 text-amber-500" />;
  if (status === "cancelled" || status === "stopped")
    return <StopCircle className="h-4 w-4 shrink-0 text-muted-foreground" />;
  if (status === "rolled_back")
    return <RefreshCw className="h-4 w-4 shrink-0 text-violet-500" />;
  return <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500" />;
}

function deployStatusClass(status?: string): string {
  if (!status) return "";
  if (status === "succeeded" || status === "success") return "text-emerald-500";
  if (status === "failed") return "text-destructive";
  if (status === "running" || status === "building") return "text-blue-500";
  if (status === "queued" || status === "pending") return "text-amber-500";
  if (status === "cancelled" || status === "stopped") return "text-muted-foreground";
  if (status === "rolled_back") return "text-violet-500";
  return "";
}
