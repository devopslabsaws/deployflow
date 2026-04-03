"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  ArrowLeft, Waypoints, Play, Square, RefreshCw, Clock,
  User, Terminal, Wand2, AlertCircle, Zap, ChevronRight,
  CheckCircle2, XCircle, Loader2, Radio, Brain,
} from "lucide-react";
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
import { apiClient } from "@/lib/api-client";
import { cn, formatDuration, formatRelativeTime } from "@/lib/utils";

const runStatusConfig: Record<string, { label: string; cls: string; icon: React.ComponentType<any> }> = {
  queued:    { label: "Queued",    cls: "text-amber-500 border-amber-500/30 bg-amber-500/10",   icon: Clock },
  running:   { label: "Running",   cls: "text-blue-500 border-blue-500/30 bg-blue-500/10",      icon: Loader2 },
  success:   { label: "Success",   cls: "text-emerald-500 border-emerald-500/30 bg-emerald-500/10", icon: CheckCircle2 },
  failed:    { label: "Failed",    cls: "text-destructive border-destructive/30 bg-destructive/10", icon: XCircle },
  cancelled: { label: "Cancelled", cls: "text-muted-foreground border-border bg-muted/40",      icon: Square },
};

const logLevelColor: Record<string, string> = {
  info:    "text-zinc-300",
  warn:    "text-amber-400",
  warning: "text-amber-400",
  error:   "text-red-400",
  debug:   "text-zinc-500",
  success: "text-emerald-400",
};

export default function PipelineDetailsPage() {
  const params = useParams<{ id: string }>();
  const pipelineId = params?.id;

  const { data: pipeline, isLoading: pipelineLoading, refetch: refetchPipeline } = usePipeline(pipelineId);
  const { data: runs = [], isLoading: runsLoading, refetch: refetchRuns } = usePipelineRuns(pipelineId);

  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const activeRunId = selectedRunId ?? runs[0]?.id ?? "";

  const selectedRun = useMemo(() => runs.find((r) => r.id === activeRunId), [runs, activeRunId]);
  const isLive = selectedRun?.status === "running" || selectedRun?.status === "queued";

  const { data: logsPage, isLoading: logsLoading, dataUpdatedAt } = usePipelineRunLogs(activeRunId, 1, 500, isLive);

  const startRun = useStartPipelineRun();
  const cancelRun = useCancelPipelineRun();
  const [simulating, setSimulating] = useState(false);
  const [scaffolding, setScaffolding] = useState(false);
  const [dagData, setDagData] = useState<any>(null);
  const [analyzing, setAnalyzing] = useState(false);
  const [insights, setInsights] = useState<any[]>([]);
  const [selfHealYaml, setSelfHealYaml] = useState("");

  // Auto-select first run
  useEffect(() => {
    if (!selectedRunId && runs.length > 0) setSelectedRunId(runs[0].id);
  }, [runs, selectedRunId]);

  // Load DAG on mount
  useEffect(() => {
    if (!pipelineId) return;
    apiClient.get(`/pipelines/${pipelineId}/dag`).then((d: any) => setDagData(d)).catch(() => {});
  }, [pipelineId]);

  // Clear insights when selected run changes
  useEffect(() => { setInsights([]); setSelfHealYaml(""); }, [activeRunId]);

  // Auto-scroll log terminal on new data
  const logEndRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [dataUpdatedAt]);

  const handleSimulate = async () => {
    if (!pipelineId) return;
    setSimulating(true);
    try {
      const result: any = await apiClient.post(`/pipelines/${pipelineId}/runs/simulate`, {});
      toast.success(`Simulated — ${result.logCount} log lines in ${result.durationSeconds}s`);
      await Promise.all([refetchPipeline(), refetchRuns()]);
      if (result.runId) setSelectedRunId(result.runId);
    } catch (e: any) {
      toast.error("Simulation failed", { description: e.message });
    } finally {
      setSimulating(false);
    }
  };

  const handleScaffold = async () => {
    if (!pipelineId || !pipeline) return;
    setScaffolding(true);
    try {
      await apiClient.post(`/pipelines/${pipelineId}/scaffold`, { projectName: pipeline.name });
      toast.success(`Stages auto-generated for "${pipeline.name}"`);
      await refetchPipeline();
    } catch (e: any) {
      toast.error("Scaffold failed", { description: e.message });
    } finally {
      setScaffolding(false);
    }
  };

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
      await Promise.all([refetchPipeline(), refetchRuns()]);
    } catch (e: any) {
      toast.error("Failed to cancel run", { description: e.message });
    }
  };

  const handleAnalyzeFailure = async () => {
    if (!activeRunId || !pipelineId) return;
    setAnalyzing(true);
    try {
      const result: any = await apiClient.post(`/pipelines/${pipelineId}/runs/${activeRunId}/analyze-failure`, {});
      setInsights(result.insights ?? []);
      setSelfHealYaml(result.selfHealYaml ?? "");
      toast.success(`AI found ${result.insights?.length ?? 0} insights`);
    } catch (e: any) {
      toast.error("Analysis failed", { description: e.message });
    } finally {
      setAnalyzing(false);
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

  const busy = simulating || scaffolding || startRun.isPending;
  const logs = logsPage?.data ?? [];

  // Group logs by stage for the stage progress visualization
  const stageNames = Array.from(new Set(logs.map((l) => l.stageName).filter(Boolean)));

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/pipelines"><ArrowLeft className="h-4 w-4" /></Link>
          </Button>
          <div>
            <h1 className="text-2xl font-bold tracking-tight">{pipeline.name}</h1>
            <p className="text-sm text-muted-foreground">
              {pipeline.stages.length} stages
              {pipeline.description ? ` — ${pipeline.description}` : ""}
            </p>
          </div>
        </div>
        <div className="flex gap-2 flex-wrap justify-end">
          <Button variant="outline" size="sm" onClick={() => Promise.all([refetchPipeline(), refetchRuns()])}>
            <RefreshCw className="mr-1.5 h-4 w-4" />Refresh
          </Button>
          <Button variant="outline" size="sm" asChild>
            <Link href={`/pipelines/${pipelineId}/builder`}>
              <Waypoints className="mr-1.5 h-4 w-4" />Edit Stages
            </Link>
          </Button>
          <Button
            variant="outline" size="sm"
            onClick={handleScaffold}
            disabled={busy}
            title="Auto-detect project type and generate stages"
          >
            <Wand2 className="mr-1.5 h-4 w-4" />{scaffolding ? "Generating…" : "Auto-Scaffold"}
          </Button>
          <Button
            variant="outline" size="sm"
            onClick={handleSimulate}
            disabled={busy}
            title="Simulate a full run with logs"
          >
            <Zap className="mr-1.5 h-4 w-4" />{simulating ? "Simulating…" : "Simulate Run"}
          </Button>
          <Button size="sm" onClick={handleStartRun} disabled={busy || pipeline.status === "running"}>
            <Play className="mr-1.5 h-4 w-4" />Run Pipeline
          </Button>
        </div>
      </div>

      {/* No stages warning */}
      {pipeline.stages.length === 0 && (
        <div className="flex items-center gap-3 rounded-xl border border-amber-500/30 bg-amber-500/5 px-4 py-3">
          <AlertCircle className="w-5 h-5 text-amber-500 shrink-0" />
          <div className="flex-1">
            <p className="text-sm font-medium text-amber-600 dark:text-amber-400">No stages configured</p>
            <p className="text-xs text-amber-600/80 dark:text-amber-400/70">Auto-generate a CI/CD workflow or add stages manually.</p>
          </div>
          <Button
            variant="outline" size="sm"
            onClick={handleScaffold}
            disabled={scaffolding}
            className="border-amber-500/40 text-amber-600 dark:text-amber-400 hover:bg-amber-500/10 shrink-0"
          >
            <Wand2 className="mr-1.5 h-4 w-4" />
            {scaffolding ? "Generating…" : "Auto-Generate Stages"}
          </Button>
        </div>
      )}

      {/* Stage pipeline visualization */}
      {pipeline.stages.length > 0 && (
        <Card className="glass-card">
          <CardContent className="pt-4 pb-3">
            <div className="flex items-center gap-1.5 overflow-x-auto">
              {pipeline.stages
                .slice()
                .sort((a, b) => a.order - b.order)
                .map((stage, i) => {
                  const hasLogs = stageNames.includes(stage.name);
                  const isCurrentStage = isLive && stageNames[stageNames.length - 1] === stage.name;
                  return (
                    <div key={stage.id} className="flex items-center gap-1.5 shrink-0">
                      {i > 0 && <ChevronRight className="w-3 h-3 text-muted-foreground/40" />}
                      <div className={cn(
                        "flex items-center gap-1.5 px-2.5 py-1.5 rounded-md border text-xs font-medium transition-all",
                        isCurrentStage
                          ? "border-blue-500/50 bg-blue-500/10 text-blue-400"
                          : hasLogs
                          ? "border-emerald-500/30 bg-emerald-500/5 text-emerald-500"
                          : "border-border/50 bg-muted/40 text-muted-foreground"
                      )}>
                        {isCurrentStage && <Loader2 className="w-3 h-3 animate-spin" />}
                        {!isCurrentStage && hasLogs && <CheckCircle2 className="w-3 h-3" />}
                        <span>{stage.name}</span>
                        <span className="opacity-60">({stage.steps.length})</span>
                      </div>
                    </div>
                  );
                })}
            </div>
          </CardContent>
        </Card>
      )}

      {/* DAG Execution Plan — only shown when pipeline has parallel levels */}
      {dagData && dagData.levelCount > 1 && (
        <Card className="glass-card">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-wide flex items-center gap-2">
              <Waypoints className="h-3.5 w-3.5" />DAG Execution Plan
              <Badge variant="outline" className="text-[10px] ml-1">
                {dagData.levelCount} levels · ×{dagData.estimatedParallelism} parallelism
              </Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="pt-0">
            <div className="flex items-start gap-6 overflow-x-auto pb-1">
              {dagData.levels?.map((level: any) => (
                <div key={level.level} className="flex flex-col gap-2 shrink-0">
                  <div className="text-[10px] text-center text-muted-foreground uppercase tracking-widest">
                    Level {level.level}
                  </div>
                  <div className="flex flex-col gap-1.5">
                    {level.stages?.map((stage: any) => (
                      <div key={stage.name} className={cn(
                        "px-3 py-1.5 rounded-md border text-xs font-medium text-center min-w-[90px]",
                        level.stages.length > 1
                          ? "border-blue-500/30 bg-blue-500/5 text-blue-400"
                          : "border-border/50 bg-muted/30 text-muted-foreground"
                      )}>
                        {stage.name}
                        <div className="text-[10px] opacity-60 mt-0.5">{stage.stepCount} step{stage.stepCount !== 1 ? "s" : ""}</div>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Run history + log viewer side by side on large screens */}
      <div className="grid grid-cols-1 xl:grid-cols-[320px,1fr] gap-4">
        {/* Run history */}
        <Card className="glass-card">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-wide">Run History</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 pt-0">
            {runsLoading ? (
              <Skeleton className="h-24 w-full" />
            ) : runs.length === 0 ? (
              <div className="py-8 text-center">
                <p className="text-sm text-muted-foreground mb-3">No runs yet.</p>
                <Button size="sm" variant="outline" onClick={handleSimulate} disabled={simulating}>
                  <Zap className="mr-1.5 h-3.5 w-3.5" />Simulate First Run
                </Button>
              </div>
            ) : (
              runs.map((run) => {
                const cfg = runStatusConfig[run.status] ?? runStatusConfig.queued;
                const RunIcon = cfg.icon;
                const duration = run.completedAt
                  ? formatDuration((new Date(run.completedAt).getTime() - new Date(run.startedAt).getTime()) / 1000)
                  : "in progress";
                return (
                  <button
                    key={run.id}
                    type="button"
                    onClick={() => setSelectedRunId(run.id)}
                    className={cn(
                      "w-full rounded-lg border p-3 text-left transition-all",
                      activeRunId === run.id
                        ? "border-primary bg-primary/5"
                        : "border-border/50 hover:border-border hover:bg-muted/20"
                    )}
                  >
                    <div className="flex items-center justify-between gap-2 mb-1.5">
                      <div className="flex items-center gap-2">
                        <RunIcon className={cn("h-3.5 w-3.5", cfg.cls.split(" ")[0], run.status === "running" && "animate-spin")} />
                        <span className={cn("text-xs font-medium", cfg.cls.split(" ")[0])}>{cfg.label}</span>
                      </div>
                      <span className="text-[10px] text-muted-foreground">{formatRelativeTime(run.startedAt)}</span>
                    </div>
                    <div className="flex items-center gap-3 text-[10px] text-muted-foreground">
                      <span className="flex items-center gap-0.5"><Clock className="h-2.5 w-2.5" />{duration}</span>
                      <span>{run.stageCount}s/{run.stepCount}st</span>
                      {run.triggeredBy && <span className="flex items-center gap-0.5"><User className="h-2.5 w-2.5" />{run.triggeredBy}</span>}
                    </div>
                  </button>
                );
              })
            )}

            {selectedRun && (selectedRun.status === "running" || selectedRun.status === "queued") && (
              <Button
                variant="destructive" size="sm" className="w-full mt-1"
                onClick={handleCancelRun}
                disabled={cancelRun.isPending}
              >
                <Square className="mr-1.5 h-3.5 w-3.5" />Cancel Run
              </Button>
            )}
          </CardContent>
        </Card>

        {/* Log terminal */}
        <Card className="glass-card">
          <CardHeader className="pb-2">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-wide flex items-center gap-2">
                <Terminal className="h-3.5 w-3.5" />
                {activeRunId ? `Logs — ${logs.length} lines` : "Logs"}
              </CardTitle>
              {isLive && (
                <div className="flex items-center gap-1.5 text-xs text-blue-400">
                  <Radio className="h-3 w-3 animate-pulse" />
                  <span className="font-medium">LIVE</span>
                </div>
              )}
            </div>
          </CardHeader>
          <CardContent className="pt-0">
            {!activeRunId ? (
              <div className="py-12 text-center text-sm text-muted-foreground">
                Select a run from the history to view logs.
              </div>
            ) : logsLoading ? (
              <div className="space-y-1.5">
                {[...Array(8)].map((_, i) => <Skeleton key={i} className="h-4 w-full opacity-50" style={{ width: `${60 + Math.random() * 35}%` }} />)}
              </div>
            ) : logs.length === 0 ? (
              <div className="py-12 text-center text-sm text-muted-foreground">
                No logs yet — they will appear here automatically.
              </div>
            ) : (
              <div className="max-h-[520px] overflow-auto rounded-md border border-zinc-800 bg-[#0d1117] p-3 font-mono text-xs leading-relaxed">
                {logs.map((log, idx) => {
                  const level = (log.level ?? "info").toLowerCase();
                  const msgColor = logLevelColor[level] ?? "text-zinc-300";
                  // Show stage separator when stage changes
                  const prevLog = idx > 0 ? logs[idx - 1] : null;
                  const stageChanged = log.stageName && prevLog?.stageName !== log.stageName;
                  return (
                    <div key={log.id}>
                      {stageChanged && (
                        <div className="flex items-center gap-2 my-2 text-zinc-600">
                          <div className="flex-1 border-t border-zinc-800" />
                          <span className="px-2 py-0.5 rounded bg-zinc-800/60 text-[10px] text-zinc-400 uppercase tracking-widest">
                            {log.stageName}
                          </span>
                          <div className="flex-1 border-t border-zinc-800" />
                        </div>
                      )}
                      <div className="flex items-start gap-2 hover:bg-zinc-800/30 rounded px-1 py-0.5">
                        <span className="w-18 shrink-0 text-zinc-600 text-[10px] mt-0.5 tabular-nums">
                          {new Date(log.timestamp).toLocaleTimeString()}
                        </span>
                        <span className={cn(
                          "w-12 shrink-0 text-[10px] mt-0.5 uppercase font-semibold",
                          level === "error" ? "text-red-400" : level === "warn" || level === "warning" ? "text-amber-400" : "text-zinc-600"
                        )}>
                          {level.slice(0, 4)}
                        </span>
                        {log.stepName && (
                          <span className="w-24 shrink-0 text-zinc-500 text-[10px] mt-0.5 truncate">{log.stepName}</span>
                        )}
                        <span className={cn("flex-1 break-all", msgColor)}>{log.message}</span>
                      </div>
                    </div>
                  );
                })}
                {isLive && (
                  <div className="flex items-center gap-1.5 text-blue-500 mt-2 px-1">
                    <span className="w-1.5 h-1.5 rounded-full bg-blue-500 animate-pulse" />
                    <span className="text-[10px]">Streaming…</span>
                  </div>
                )}
                <div ref={logEndRef} />
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* AI Failure Insights — only shown when a failed run is selected */}
      {selectedRun?.status === "failed" && (
        <Card className="glass-card border-amber-500/20">
          <CardHeader className="pb-2">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-wide flex items-center gap-2">
                <Brain className="h-3.5 w-3.5 text-amber-500" />AI Failure Insights
              </CardTitle>
              <Button size="sm" variant="outline" onClick={handleAnalyzeFailure} disabled={analyzing} className="text-xs h-7">
                {analyzing ? <Loader2 className="mr-1.5 h-3 w-3 animate-spin" /> : <Zap className="mr-1.5 h-3 w-3" />}
                {analyzing ? "Analyzing…" : "Analyze Failure"}
              </Button>
            </div>
          </CardHeader>
          {insights.length > 0 && (
            <CardContent className="pt-0 space-y-2">
              {insights.map((insight: any, i: number) => (
                <div key={i} className="rounded-md border border-amber-500/20 bg-amber-500/5 p-3">
                  <div className="flex items-center gap-2 mb-1.5">
                    <Badge variant="outline" className="text-[10px] border-amber-500/30 text-amber-500 capitalize">
                      {insight.category}
                    </Badge>
                    <span className="text-[10px] text-muted-foreground">
                      {Math.round((insight.confidence ?? 0) * 100)}% confidence
                    </span>
                  </div>
                  <p className="text-xs font-medium mb-1">{insight.message}</p>
                  {insight.suggestions?.length > 0 && (
                    <ul className="text-xs text-muted-foreground space-y-0.5 mt-1.5">
                      {insight.suggestions.map((s: string, j: number) => (
                        <li key={j} className="flex items-start gap-1.5">
                          <ChevronRight className="h-3 w-3 mt-0.5 text-amber-500 shrink-0" />{s}
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              ))}
              {selfHealYaml && (
                <div className="rounded-md border border-emerald-500/20 bg-emerald-500/5 p-3">
                  <p className="text-xs font-medium text-emerald-500 mb-2">Self-Heal YAML Snippet</p>
                  <pre className="text-[10px] text-zinc-300 overflow-auto max-h-36 font-mono leading-relaxed">{selfHealYaml}</pre>
                </div>
              )}
            </CardContent>
          )}
        </Card>
      )}
    </div>
  );
}

