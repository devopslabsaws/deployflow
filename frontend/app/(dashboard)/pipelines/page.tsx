"use client";

import { useMemo, useState } from "react";
import { motion } from "framer-motion";
import {
  Plus,
  GitBranch,
  Play,
  Pause,
  RefreshCw,
  MoreVertical,
  CheckCircle2,
  XCircle,
  Clock,
  Layers,
  Trash2,
  Settings,
  ChevronRight,
  Timer,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { usePipelines, useStartPipelineRun } from "@/hooks/use-api";
import { formatRelativeTime, formatDuration, cn } from "@/lib/utils";
import { toast } from "sonner";
import Link from "next/link";
import type { Pipeline, PipelineStatus } from "@/types";

const statusConfig: Record<PipelineStatus, { label: string; color: string; icon: React.ComponentType<any> }> = {
  idle: { label: "Idle", color: "text-muted-foreground", icon: Clock },
  running: { label: "Running", color: "text-blue-500", icon: RefreshCw },
  success: { label: "Success", color: "text-success", icon: CheckCircle2 },
  failed: { label: "Failed", color: "text-destructive", icon: XCircle },
  cancelled: { label: "Cancelled", color: "text-muted-foreground", icon: XCircle },
};

export default function PipelinesPage() {
  const { data: pipelines, isLoading, refetch } = usePipelines();
  const startPipelineRun = useStartPipelineRun();
  const [pendingRunIds, setPendingRunIds] = useState<string[]>([]);

  const pendingRunSet = useMemo(() => new Set(pendingRunIds), [pendingRunIds]);

  const handleTrigger = async (id: string, name: string) => {
    try {
      setPendingRunIds((prev) => (prev.includes(id) ? prev : [...prev, id]));
      await startPipelineRun.mutateAsync(id);
      toast.success(`Pipeline "${name}" run started.`);
    } catch (e: any) {
      toast.error("Failed to start pipeline run", { description: e.message });
    } finally {
      setPendingRunIds((prev) => prev.filter((x) => x !== id));
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Pipelines</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            CI/CD automation pipelines
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={() => refetch()} className="gap-1.5">
            <RefreshCw className="w-4 h-4" />
            Refresh
          </Button>
          <Button asChild>
            <Link href="/pipelines/new">
              <Plus className="w-4 h-4 mr-1.5" />
              New Pipeline
            </Link>
          </Button>
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-4">
          {[...Array(3)].map((_, i) => <Skeleton key={i} className="h-40 rounded-xl" />)}
        </div>
      ) : !pipelines?.length ? (
        <EmptyPipelinesState />
      ) : (
        <div className="space-y-4">
          {pipelines.map((pipeline) => (
            <PipelineCard
              key={pipeline.id}
              pipeline={pipeline}
              onTrigger={() => handleTrigger(pipeline.id, pipeline.name)}
              runPending={pendingRunSet.has(pipeline.id)}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function PipelineCard({ pipeline, onTrigger, runPending }: { pipeline: Pipeline; onTrigger: () => void; runPending?: boolean }) {
  const effectiveStatus = runPending ? "running" : pipeline.status;
  const cfg = statusConfig[effectiveStatus as PipelineStatus];
  const StatusIcon = cfg.icon;
  const totalSteps = pipeline.stages.reduce((a, s) => a + s.steps.length, 0);

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
                  {pipeline.description || `${pipeline.stages.length} stages • ${totalSteps} steps`}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-3">
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
                  <DropdownMenuItem onClick={onTrigger} disabled={effectiveStatus === "running" || !!runPending}>
                    <Play className="mr-2 w-4 h-4" />Run Now
                  </DropdownMenuItem>
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
                  <DropdownMenuItem className="text-destructive">
                    <Trash2 className="mr-2 w-4 h-4" />Delete
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </div>
        </CardHeader>

        <CardContent className="space-y-4">
          {/* Stage visualization */}
          <div className="flex items-center gap-1.5 overflow-x-auto pb-1">
            {pipeline.stages.map((stage, i) => (
              <div key={stage.id} className="flex items-center gap-1.5 shrink-0">
                {i > 0 && <ChevronRight className="w-3 h-3 text-muted-foreground/50" />}
                <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-muted/60 border border-border/50 text-xs">
                  <span className="font-medium">{stage.name}</span>
                  <span className="text-muted-foreground">({stage.steps.length})</span>
                </div>
              </div>
            ))}
          </div>

          {/* Footer */}
          <div className="flex items-center justify-between text-xs text-muted-foreground pt-2 border-t border-border/50">
            <div className="flex items-center gap-4">
              <span className="flex items-center gap-1">
                <Clock className="w-3 h-3" />
                {pipeline.trigger.type === "push"
                  ? `On push to ${pipeline.trigger.branches?.join(", ")}`
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
            <div className="flex items-center gap-3">
              {pipeline.lastRunAt && <span>{formatRelativeTime(pipeline.lastRunAt)}</span>}
              <Badge
                variant="outline"
                className={pipeline.isEnabled ? "text-success border-success/30 bg-success/10" : "text-muted-foreground"}
              >
                {pipeline.isEnabled ? "Enabled" : "Disabled"}
              </Badge>
              <Button size="sm" variant="outline" className="h-6 text-xs" onClick={onTrigger} disabled={effectiveStatus === "running" || !!runPending}>
                <Play className="w-3 h-3 mr-1" />{runPending ? "Starting..." : "Run"}
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
