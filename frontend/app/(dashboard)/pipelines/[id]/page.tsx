"use client";

import { useEffect, useMemo, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Layers, Play, Square, RefreshCw, Clock, User, TerminalSquare } from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  useCancelPipelineRun,
  usePipeline,
  usePipelineRunLogs,
  usePipelineRuns,
  useStartPipelineRun,
} from "@/hooks/use-api";
import { formatDuration, formatRelativeTime } from "@/lib/utils";

const runStatusClass: Record<string, string> = {
  queued: "text-amber-500 border-amber-500/30 bg-amber-500/10",
  running: "text-blue-500 border-blue-500/30 bg-blue-500/10",
  success: "text-emerald-500 border-emerald-500/30 bg-emerald-500/10",
  failed: "text-destructive border-destructive/30 bg-destructive/10",
  cancelled: "text-muted-foreground border-border bg-muted/40",
};

export default function PipelineDetailsPage() {
  const params = useParams<{ id: string }>();
  const pipelineId = params?.id;

  const { data: pipeline, isLoading: pipelineLoading, refetch: refetchPipeline } = usePipeline(pipelineId);
  const { data: runs = [], isLoading: runsLoading, refetch: refetchRuns } = usePipelineRuns(pipelineId);

  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const activeRunId = selectedRunId ?? runs[0]?.id ?? "";

  const { data: logsPage, isLoading: logsLoading, refetch: refetchLogs } = usePipelineRunLogs(activeRunId, 1, 200);
  const startRun = useStartPipelineRun();
  const cancelRun = useCancelPipelineRun();

  useEffect(() => {
    if (!selectedRunId && runs.length > 0) {
      setSelectedRunId(runs[0].id);
    }
  }, [runs, selectedRunId]);

  const selectedRun = useMemo(() => runs.find((r) => r.id === activeRunId), [runs, activeRunId]);

  const handleStartRun = async () => {
    if (!pipelineId) return;
    try {
      const run = await startRun.mutateAsync(pipelineId);
      toast.success("Pipeline run started.");
      setSelectedRunId(run.id);
      await Promise.all([refetchPipeline(), refetchRuns()]);
    } catch (e: any) {
      toast.error("Failed to start pipeline run", { description: e.message });
    }
  };

  const handleCancelRun = async () => {
    if (!selectedRun) return;
    try {
      await cancelRun.mutateAsync(selectedRun.id);
      toast.success("Run cancelled.");
      await Promise.all([refetchPipeline(), refetchRuns(), refetchLogs()]);
    } catch (e: any) {
      toast.error("Failed to cancel run", { description: e.message });
    }
  };

  if (pipelineLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-56 w-full" />
      </div>
    );
  }

  if (!pipeline) {
    return (
      <div className="space-y-4">
        <p className="text-sm text-muted-foreground">Pipeline not found.</p>
        <Button asChild variant="outline" size="sm">
          <Link href="/pipelines">Back to pipelines</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/pipelines">
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-bold tracking-tight">{pipeline.name}</h1>
            <p className="text-sm text-muted-foreground">{pipeline.description || "Pipeline execution details"}</p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={() => Promise.all([refetchPipeline(), refetchRuns(), refetchLogs()])}>
            <RefreshCw className="mr-1.5 h-4 w-4" />Refresh
          </Button>
          <Button variant="outline" size="sm" asChild>
            <Link href={`/pipelines/${pipelineId}/builder`}>
              <Layers className="mr-1.5 h-4 w-4" />Edit Stages
            </Link>
          </Button>
          <Button size="sm" onClick={handleStartRun} disabled={startRun.isPending || pipeline.status === "running"}>
            <Play className="mr-1.5 h-4 w-4" />Run Pipeline
          </Button>
        </div>
      </div>

      <Card className="glass-card">
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Run History</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {runsLoading ? (
            <Skeleton className="h-24 w-full" />
          ) : runs.length === 0 ? (
            <p className="text-sm text-muted-foreground">No runs yet. Trigger the first run to initialize execution history.</p>
          ) : (
            <div className="space-y-2">
              {runs.map((run) => (
                <button
                  key={run.id}
                  type="button"
                  onClick={() => setSelectedRunId(run.id)}
                  className={`w-full rounded-lg border p-3 text-left transition ${activeRunId === run.id ? "border-primary bg-primary/5" : "border-border/50 hover:border-border"}`}
                >
                  <div className="flex items-center justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <Badge variant="outline" className={runStatusClass[run.status] || runStatusClass.queued}>
                        {run.status}
                      </Badge>
                      <span className="text-sm text-muted-foreground">{formatRelativeTime(run.startedAt)}</span>
                    </div>
                    <div className="flex items-center gap-4 text-xs text-muted-foreground">
                      <span className="flex items-center gap-1"><Clock className="h-3 w-3" />{run.completedAt ? formatDuration((new Date(run.completedAt).getTime() - new Date(run.startedAt).getTime()) / 1000) : "in progress"}</span>
                      <span>{run.stageCount} stages / {run.stepCount} steps</span>
                    </div>
                  </div>
                </button>
              ))}
            </div>
          )}

          {selectedRun && (
            <div className="flex items-center justify-between rounded-lg border border-border/60 p-3">
              <div className="text-sm text-muted-foreground flex items-center gap-2">
                <User className="h-4 w-4" />
                Triggered by: {selectedRun.triggeredBy || "system"}
              </div>
              <Button
                variant="destructive"
                size="sm"
                onClick={handleCancelRun}
                disabled={cancelRun.isPending || selectedRun.status !== "running"}
              >
                <Square className="mr-1.5 h-4 w-4" />Cancel Run
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      <Card className="glass-card">
        <CardHeader className="pb-3">
          <CardTitle className="text-base flex items-center gap-2">
            <TerminalSquare className="h-4 w-4" />Stage and Step Logs
          </CardTitle>
        </CardHeader>
        <CardContent>
          {!activeRunId ? (
            <p className="text-sm text-muted-foreground">Select a run to inspect logs.</p>
          ) : logsLoading ? (
            <Skeleton className="h-56 w-full" />
          ) : (logsPage?.data?.length ?? 0) === 0 ? (
            <p className="text-sm text-muted-foreground">No logs recorded for this run yet.</p>
          ) : (
            <div className="max-h-[460px] overflow-auto rounded-md border border-border/60 bg-[#0d1117] p-3 font-mono text-xs">
              {logsPage?.data?.map((log) => (
                <div key={log.id} className="mb-1.5 flex items-start gap-2 text-zinc-300">
                  <span className="w-20 shrink-0 text-zinc-500">{new Date(log.timestamp).toLocaleTimeString()}</span>
                  <span className="w-16 shrink-0 text-zinc-400">{log.level}</span>
                  <span className="w-28 shrink-0 text-zinc-400">{log.stageName}</span>
                  <span className="w-28 shrink-0 text-zinc-500">{log.stepName ?? "-"}</span>
                  <span className="text-zinc-200">{log.message}</span>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
