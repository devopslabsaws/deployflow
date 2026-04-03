"use client";

import { useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { motion } from "framer-motion";
import {
  Plus, GitBranch, Play, Pause, RefreshCw, MoreVertical,
  CheckCircle2, XCircle, Clock, Layers, Trash2, Settings,
  ChevronRight, Timer, Zap, Wand2, AlertCircle, AlertTriangle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem,
  DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription,
} from "@/components/ui/dialog";
import { usePipelines, useStartPipelineRun, useDeletePipeline, useUpdatePipeline } from "@/hooks/use-api";
import { formatRelativeTime, formatDuration, cn } from "@/lib/utils";
import { apiClient } from "@/lib/api-client";
import { toast } from "sonner";
import Link from "next/link";
import type { Pipeline, PipelineStatus } from "@/types";

const statusConfig: Record<PipelineStatus, { label: string; color: string; icon: React.ComponentType<any> }> = {
  idle:      { label: "Idle",      color: "text-muted-foreground", icon: Clock },
  running:   { label: "Running",   color: "text-blue-500",         icon: RefreshCw },
  success:   { label: "Success",   color: "text-emerald-500",      icon: CheckCircle2 },
  failed:    { label: "Failed",    color: "text-destructive",      icon: XCircle },
  cancelled: { label: "Cancelled", color: "text-muted-foreground", icon: XCircle },
};

export default function PipelinesPage() {
  const qc = useQueryClient();
  const { data: pipelines, isLoading, refetch } = usePipelines();
  const startPipelineRun = useStartPipelineRun();
  const deletePipeline = useDeletePipeline();
  const updatePipeline = useUpdatePipeline();
  const [pendingRunIds, setPendingRunIds] = useState<string[]>([]);
  const [scaffoldingIds, setScaffoldingIds] = useState<string[]>([]);
  const [simulatingIds, setSimulatingIds] = useState<string[]>([]);
  const [deletingPipeline, setDeletingPipeline] = useState<Pipeline | null>(null);
  const [deleteInProgress, setDeleteInProgress] = useState(false);

  const pendingRunSet   = useMemo(() => new Set(pendingRunIds),   [pendingRunIds]);
  const scaffoldingSet  = useMemo(() => new Set(scaffoldingIds),  [scaffoldingIds]);
  const simulatingSet   = useMemo(() => new Set(simulatingIds),   [simulatingIds]);

  const handleTrigger = async (id: string, name: string) => {
    try {
      setPendingRunIds((prev) => [...prev, id]);
      await startPipelineRun.mutateAsync(id);
      toast.success(`Pipeline "${name}" run started.`);
      refetch();
    } catch (e: any) {
      toast.error("Failed to start pipeline run", { description: e.message });
    } finally {
      setPendingRunIds((prev) => prev.filter((x) => x !== id));
    }
  };

  // Auto-scaffold: detect project type and fill stages/steps
  const handleScaffold = async (id: string, name: string) => {
    try {
      setScaffoldingIds((prev) => [...prev, id]);
      await apiClient.post(`/pipelines/${id}/scaffold`, { projectName: name });
      toast.success(`Stages auto-generated for "${name}".`);
      refetch();
    } catch (e: any) {
      toast.error("Scaffold failed", { description: e.message });
    } finally {
      setScaffoldingIds((prev) => prev.filter((x) => x !== id));
    }
  };

  // Trigger a test run so users see pipeline logs immediately
  const handleSimulate = async (id: string, name: string) => {
    try {
      setSimulatingIds((prev) => [...prev, id]);
      await apiClient.post(`/pipelines/${id}/runs/simulate`, {});
      toast.success(`Test run started for "${name}".`);
      refetch();
    } catch (e: any) {
      toast.error("Failed to start test run", { description: e.message });
    } finally {
      setSimulatingIds((prev) => prev.filter((x) => x !== id));
    }
  };

  const handleDelete = (pipeline: Pipeline) => {
    setDeletingPipeline(pipeline);
  };

  const confirmDelete = async () => {
    if (!deletingPipeline) return;
    setDeleteInProgress(true);
    try {
      await deletePipeline.mutateAsync(deletingPipeline.id);
      toast.success(`Pipeline "${deletingPipeline.name}" deleted.`);
      setDeletingPipeline(null);
    } catch (e: any) {
      toast.error("Failed to delete pipeline", { description: e.message });
    } finally {
      setDeleteInProgress(false);
    }
  };

  const handleToggleEnabled = async (pipeline: Pipeline) => {
    try {
      await updatePipeline.mutateAsync({
        id: pipeline.id, name: pipeline.name,
        description: pipeline.description,
        trigger: pipeline.trigger.type,
        cronExpression: pipeline.trigger.schedule,
        isEnabled: !pipeline.isEnabled,
      });
      toast.success(`Pipeline ${pipeline.isEnabled ? "disabled" : "enabled"}.`);
    } catch (e: any) {
      toast.error("Failed to update pipeline", { description: e.message });
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Pipelines</h1>
          <p className="text-muted-foreground text-sm mt-0.5">CI/CD automation pipelines</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={() => refetch()} className="gap-1.5">
            <RefreshCw className="w-4 h-4" /> Refresh
          </Button>
          <Button asChild>
            <Link href="/pipelines/new">
              <Plus className="w-4 h-4 mr-1.5" /> New Pipeline
            </Link>
          </Button>
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-4">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-40 rounded-xl" />)}</div>
      ) : !pipelines?.length ? (
        <EmptyPipelinesState />
      ) : (
        <div className="space-y-4">
          {pipelines.map((pipeline) => (
            <PipelineCard
              key={pipeline.id}
              pipeline={pipeline}
              onTrigger={() => handleTrigger(pipeline.id, pipeline.name)}
              onScaffold={() => handleScaffold(pipeline.id, pipeline.name)}
              onSimulate={() => handleSimulate(pipeline.id, pipeline.name)}
              onDelete={() => handleDelete(pipeline)}
              onToggleEnabled={() => handleToggleEnabled(pipeline)}
              runPending={pendingRunSet.has(pipeline.id)}
              scaffolding={scaffoldingSet.has(pipeline.id)}
              simulating={simulatingSet.has(pipeline.id)}
            />
          ))}
        </div>
      )}
      <DeletePipelineModal
        pipeline={deletingPipeline}
        isDeleting={deleteInProgress}
        onCancel={() => setDeletingPipeline(null)}
        onConfirm={confirmDelete}
      />
    </div>
  );
}


function DeletePipelineModal({
  pipeline, isDeleting, onCancel, onConfirm,
}: {
  pipeline: Pipeline | null;
  isDeleting: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const hasStages = (pipeline?.stages.length ?? 0) > 0;
  const totalSteps = pipeline?.stages.reduce((a, s) => a + s.steps.length, 0) ?? 0;
  return (
    <Dialog open={!!pipeline} onOpenChange={(open) => { if (!open && !isDeleting) onCancel(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <div className="flex items-center gap-3 mb-1">
            <div className="w-10 h-10 rounded-full bg-destructive/10 flex items-center justify-center shrink-0">
              <AlertTriangle className="w-5 h-5 text-destructive" />
            </div>
            <DialogTitle className="text-xl">Delete Pipeline</DialogTitle>
          </div>
          <DialogDescription asChild>
            <div className="space-y-3 pt-1">
              <div className="rounded-lg border border-border bg-muted/40 px-4 py-3 space-y-1">
                <p className="font-semibold text-foreground">{pipeline?.name}</p>
                {pipeline?.description && (
                  <p className="text-xs text-muted-foreground">{pipeline.description}</p>
                )}
                <div className="flex items-center gap-3 text-xs text-muted-foreground mt-1">
                  {hasStages ? (
                    <span>{pipeline!.stages.length} stage{pipeline!.stages.length !== 1 ? "s" : ""} · {totalSteps} step{totalSteps !== 1 ? "s" : ""}</span>
                  ) : (
                    <span>No stages configured</span>
                  )}
                  <span>·</span>
                  <span className="capitalize">{pipeline?.trigger.type ?? "manual"} trigger</span>
                </div>
              </div>
              <div className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2.5">
                <AlertCircle className="w-4 h-4 text-destructive shrink-0 mt-0.5" />
                <p className="text-xs text-destructive">
                  This action is <strong>permanent and cannot be undone</strong>. All associated run history, logs, and configuration will be permanently removed.
                </p>
              </div>
            </div>
          </DialogDescription>
        </DialogHeader>
        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={onCancel} disabled={isDeleting}>
            Cancel
          </Button>
          <Button
            variant="destructive"
            onClick={onConfirm}
            disabled={isDeleting}
            className="gap-2"
          >
            {isDeleting ? (
              <><RefreshCw className="w-4 h-4 animate-spin" /> Deleting…</>
            ) : (
              <><Trash2 className="w-4 h-4" /> Delete Pipeline</>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}


function PipelineCard({
  pipeline, onTrigger, onScaffold, onSimulate,
  onDelete, onToggleEnabled,
  runPending, scaffolding, simulating,
}: {
  pipeline: Pipeline;
  onTrigger: () => void;
  onScaffold: () => void;
  onSimulate: () => void;
  onDelete: () => void;
  onToggleEnabled: () => void;
  runPending?: boolean;
  scaffolding?: boolean;
  simulating?: boolean;
}) {
  const busy = runPending || scaffolding || simulating;
  const effectiveStatus = runPending ? "running" : pipeline.status;
  const cfg = statusConfig[effectiveStatus as PipelineStatus];
  const StatusIcon = cfg.icon;
  const hasStages = pipeline.stages.length > 0;
  const totalSteps = pipeline.stages.reduce((a, s) => a + s.steps.length, 0);

  const placeholderStages = [
    { name: "Setup",   desc: "git clone · install deps"  },
    { name: "Build",   desc: "compile · bundle"          },
    { name: "Test",    desc: "unit & integration tests"  },
    { name: "Package", desc: "docker build · tag image"  },
    { name: "Deploy",  desc: "rolling replace · start"   },
    { name: "Verify",  desc: "health check · smoke test" },
  ];

  return (
    <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}>
      <Card className="glass-card hover:border-border/80 transition-all">
        <CardHeader className="pb-3">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className={cn(
                "w-9 h-9 rounded-xl flex items-center justify-center",
                effectiveStatus === "running" ? "bg-blue-500/10" : "bg-muted"
              )}>
                <GitBranch className={cn("w-5 h-5", effectiveStatus === "running" ? "text-blue-500" : "text-muted-foreground")} />
              </div>
              <div>
                <Link href={`/pipelines/${pipeline.id}`} className="font-semibold text-sm hover:text-primary transition-colors">
                  {pipeline.name}
                </Link>
                <p className="text-xs text-muted-foreground">
                  {pipeline.description || (hasStages ? `${pipeline.stages.length} stages • ${totalSteps} steps` : "No stages configured")}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <div className={cn("flex items-center gap-1.5 text-sm", cfg.color)}>
                <StatusIcon className={cn("w-4 h-4", effectiveStatus === "running" && "animate-spin")} />
                <span className="font-medium">{cfg.label}</span>
              </div>

              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="ghost" size="sm" className="h-8 w-8 px-0">
                    <MoreVertical className="w-4 h-4" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onClick={onTrigger} disabled={!!busy}>
                    <Play className="mr-2 w-4 h-4" />Run Now
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={onSimulate} disabled={!!busy}>
                    <Zap className="mr-2 w-4 h-4" />Simulate Run
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={onScaffold} disabled={!!busy}>
                    <Wand2 className="mr-2 w-4 h-4" />Auto-Generate Stages
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem asChild>
                    <Link href={`/pipelines/${pipeline.id}`}>
                      <Layers className="mr-2 w-4 h-4" />View Details
                    </Link>
                  </DropdownMenuItem>
                  <DropdownMenuItem asChild>
                    <Link href={`/pipelines/${pipeline.id}/builder`}>
                      <GitBranch className="mr-2 w-4 h-4" />Edit Stages
                    </Link>
                  </DropdownMenuItem>
                  <DropdownMenuItem asChild>
                    <Link href={`/pipelines/${pipeline.id}/edit`}>
                      <Settings className="mr-2 w-4 h-4" />Edit
                    </Link>
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={onDelete} className="text-destructive">
                    <Trash2 className="mr-2 w-4 h-4" />Delete
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </div>
        </CardHeader>

        <CardContent className="space-y-3">
          {/* Auto-scaffold banner for empty pipelines */}
          {!hasStages && (
            <div className="flex items-center gap-3 rounded-lg border border-amber-500/30 bg-amber-500/5 px-3 py-2.5">
              <AlertCircle className="w-4 h-4 text-amber-500 shrink-0" />
              <p className="text-xs text-amber-600 dark:text-amber-400 flex-1">
                Pipeline has no stages. Auto-generate a CI/CD workflow or edit stages manually.
              </p>
              <Button
                size="sm"
                variant="outline"
                className="h-6 text-xs border-amber-500/40 text-amber-600 dark:text-amber-400 hover:bg-amber-500/10 shrink-0"
                onClick={onScaffold}
                disabled={!!scaffolding}
              >
                <Wand2 className="w-3 h-3 mr-1" />
                {scaffolding ? "Generating…" : "Auto-generate"}
              </Button>
            </div>
          )}

          {/* Stage pills */}
          <div className="flex items-center gap-1.5 overflow-x-auto pb-1">
            {hasStages
              ? pipeline.stages.map((stage, i) => (
                  <div key={stage.id} className="flex items-center gap-1.5 shrink-0">
                    {i > 0 && <ChevronRight className="w-3 h-3 text-muted-foreground/50" />}
                    <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-muted/60 border border-border/50 text-xs">
                      <span className="font-medium">{stage.name}</span>
                      <span className="text-muted-foreground">({stage.steps.length})</span>
                    </div>
                  </div>
                ))
              : placeholderStages.map((stage, i) => (
                  <div key={stage.name} className="flex items-center gap-1.5 shrink-0 opacity-40">
                    {i > 0 && <ChevronRight className="w-3 h-3 text-muted-foreground/50" />}
                    <div
                      className="px-2.5 py-1 rounded-md border border-dashed border-border/60 text-xs text-muted-foreground"
                      title={`Auto-generated: ${stage.desc}`}
                    >
                      {stage.name}
                    </div>
                  </div>
                ))
            }
          </div>
          {!hasStages && (
            <p className="text-[10px] text-muted-foreground/60 -mt-0.5">
              Auto-generated stages — click <strong>Auto-Generate</strong> to scaffold a real CI/CD workflow for this project.
            </p>
          )}

          {/* Footer */}
          <div className="flex items-center justify-between text-xs text-muted-foreground pt-2 border-t border-border/50">
            <div className="flex items-center gap-4">
              <span className="flex items-center gap-1">
                <Clock className="w-3 h-3" />
                {pipeline.trigger.type === "push"
                  ? `On push`
                  : pipeline.trigger.type === "schedule"
                  ? `Schedule: ${pipeline.trigger.schedule}`
                  : "Manual"}
              </span>
              {pipeline.lastRunDuration && (
                <span className="flex items-center gap-1">
                  <Timer className="w-3 h-3" />
                  Last: {formatDuration(pipeline.lastRunDuration)}
                </span>
              )}
            </div>
            <div className="flex items-center gap-2">
              {pipeline.lastRunAt && <span>{formatRelativeTime(pipeline.lastRunAt)}</span>}
              <Badge
                variant="outline"
                className={cn(
                  "cursor-pointer select-none",
                  pipeline.isEnabled
                    ? "text-success border-success/30 bg-success/10 hover:bg-success/20"
                    : "text-muted-foreground hover:bg-muted"
                )}
                onClick={onToggleEnabled}
                title={pipeline.isEnabled ? "Click to disable" : "Click to enable"}
              >
                {pipeline.isEnabled ? "Enabled" : "Disabled"}
              </Badge>
              <Button
                size="sm" variant="outline" className="h-6 text-xs gap-1"
                onClick={onSimulate} disabled={!!busy}
                title="Simulate full run with logs"
              >
                <Zap className="w-3 h-3" />{simulating ? "Running…" : "Simulate"}
              </Button>
              <Button
                size="sm" variant="default" className="h-6 text-xs gap-1"
                onClick={onTrigger} disabled={!!busy}
              >
                <Play className="w-3 h-3" />{runPending ? "Starting…" : "Run"}
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </motion.div>
  );
}

function EmptyPipelinesState() {
  return (
    <div className="flex flex-col items-center justify-center py-20 text-center">
      <div className="w-16 h-16 rounded-2xl bg-muted flex items-center justify-center mb-4">
        <GitBranch className="w-8 h-8 text-muted-foreground" />
      </div>
      <h3 className="text-lg font-semibold mb-2">No pipelines</h3>
      <p className="text-muted-foreground text-sm max-w-sm mb-6">
        Build automated CI/CD pipelines with visual stage configuration.
      </p>
      <Button asChild>
        <Link href="/pipelines/new">
          <Plus className="w-4 h-4 mr-1.5" />
          Create Pipeline
        </Link>
      </Button>
    </div>
  );
}
