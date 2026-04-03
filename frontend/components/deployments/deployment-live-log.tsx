"use client";

import { useEffect, useRef, useState, useCallback, useMemo } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { motion, AnimatePresence } from "framer-motion";
import {
  Terminal, CheckCircle2, XCircle, Loader2, Wifi, WifiOff,
  Clock, Server, ChevronDown, AlertTriangle, Bot, RefreshCw,
  ServerCrash, GitBranch, Package, Layers, Play, Activity, Copy,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/store/auth-store";
import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api-client";
import { useRunnerStatus } from "@/hooks/use-api";
import { cn } from "@/lib/utils";
import Link from "next/link";
import { toast } from "sonner";

// SignalR is loaded lazily so SSR does not break
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type HubConnection = any;
// eslint-disable-next-line @typescript-eslint/no-require-imports, @typescript-eslint/no-explicit-any
const getSignalR = (): any => {
  try { return require("@microsoft/signalr"); }
  catch { return null; }
};

interface LogLine {
  id: string;
  message: string;
  stream?: string;
  timestamp: string;
}

interface DeploymentLiveLogProps {
  deploymentId: string;
  initialStatus?: string;
  /** ISO date string — when the deployment was created; used for the elapsed timer */
  startedAt?: string;
  /** ISO date string — when the deployment finished; freezes the timer */
  finishedAt?: string;
}

const PHASES = [
  { key: "queued",    label: "Queued",    desc: "Waiting for a runner to pick up this deployment\u2026" },
  { key: "building",  label: "Building",  desc: "Cloning repo \u00b7 detecting stack \u00b7 building Docker image\u2026" },
  { key: "deploying", label: "Deploying", desc: "Starting container \u00b7 running health checks\u2026" },
  { key: "live",      label: "Live",      desc: "Deployment succeeded \u2014 your app is running!" },
];

function phaseIndex(status: string): number {
  if (status === "healthy" || status === "running") return 3;
  if (status === "deploying") return 2;
  if (status === "building") return 1;
  // failed/cancelled: keep the last known phase as the stuck point (not reset to 0)
  return 0;
}

function lastActivePhase(status: string): number {
  if (status === "failed" || status === "cancelled") return 1; // died during or before building
  return phaseIndex(status);
}

function lineClass(stream?: string, msg?: string): string {
  if (stream === "stderr") return "text-red-400";
  if (msg?.startsWith("\u2501\u2501\u2501")) return "text-cyan-400 font-semibold";
  if (msg?.match(/^[\u2705\u2713]/)) return "text-emerald-400";
  if (msg?.match(/^[\u274c\u2717]/)) return "text-red-400";
  if (msg?.match(/^[\u26a0\ufe0f]/)) return "text-yellow-400";
  if (msg?.startsWith("\uD83D\uDD17")) return "text-blue-400";
  if (msg?.match(/^[\uD83D\uDCE6\uD83D\uDCE5\u25B6\uFE0F]/)) return "text-violet-400";
  return "text-zinc-300";
}

// ── Pipeline steps matching BuildService.cs bash script log output ────────────
interface PipelineStep {
  id: string; label: string; desc: string; estSec: number;
  startPattern: RegExp; donePattern: RegExp; failPattern: RegExp | null;
}
type StepStatus = "pending" | "active" | "done" | "failed";
interface StepState { id: string; status: StepStatus; }

const PIPELINE_STEPS: PipelineStep[] = [
  { id: "ssh",     label: "SSH Connection",  desc: "Testing connectivity to remote server",   estSec: 5,   startPattern: /checking connectivity/i,         donePattern: /server reachable/i,                         failPattern: /server unreachable|offline or unreachable|connection timed out|connection refused|no route to host|host is unreachable|ssh.*connect.*failed/i },
  { id: "docker",  label: "Docker Check",    desc: "Verifying Docker on remote server",       estSec: 10,  startPattern: /step 1\/6.*docker check/i,        donePattern: /docker.*installed|step 2/i,                 failPattern: null },
  { id: "source",  label: "Git Clone",       desc: "Fetching source code from repository",    estSec: 30,  startPattern: /step 2\/6.*fetching source/i,     donePattern: /source ready/i,                             failPattern: /clone.*fail|repository.*not found/i },
  { id: "build",   label: "Docker Build",    desc: "Detecting stack \u00b7 building image",   estSec: 120, startPattern: /step 3\/6/i,                      donePattern: /step 4\/6|successfully built/i,             failPattern: /docker build failed|build.*fail/i },
  { id: "replace", label: "Rolling Replace", desc: "Stopping previous container",             estSec: 5,   startPattern: /step 4\/6.*rolling replace/i,     donePattern: /step 5\/6/i,                                failPattern: null },
  { id: "start",   label: "Start Container", desc: "Starting new container",                  estSec: 10,  startPattern: /step 5\/6.*starting container/i,  donePattern: /step 6\/6/i,                                failPattern: /failed to start container/i },
  { id: "health",  label: "Health Check",    desc: "Waiting for container to be healthy",     estSec: 30,  startPattern: /step 6\/6.*health check/i,        donePattern: /container is running|deployment complete/i, failPattern: /container crashed|container exited/i },
];

function deriveStepStates(lines: LogLine[]): StepState[] {
  return PIPELINE_STEPS.map((step) => {
    const hasFail  = step.failPattern  && lines.some(l => step.failPattern!.test(l.message));
    const hasDone  = lines.some(l => step.donePattern.test(l.message));
    const hasStart = lines.some(l => step.startPattern.test(l.message));
    if (hasFail)  return { id: step.id, status: "failed" };
    if (hasDone)  return { id: step.id, status: "done" };
    if (hasStart) return { id: step.id, status: "active" };
    return { id: step.id, status: "pending" };
  });
}

function fmtSec(s: number): string {
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  const r = s % 60;
  return r > 0 ? `${m}m ${r}s` : `${m}m`;
}

function getStepIcon(id: string) {
  if (id === "ssh")     return Wifi;
  if (id === "docker")  return Package;
  if (id === "source")  return GitBranch;
  if (id === "build")   return Layers;
  if (id === "replace") return RefreshCw;
  if (id === "start")   return Play;
  if (id === "health")  return Activity;
  return Server;
}

// ── Full 13-step deployment workflow — shown in terminal when status = "queued" ─
const WORKFLOW_OVERVIEW: Array<{
  step: number; where: string; what: string; done?: boolean; waiting?: boolean;
}> = [
  { step: 1,  where: "Frontend",             what: "Deployment record created  ·  POST /api/deployments",       done: true },
  { step: 2,  where: "Runner  ·  5 s poll",  what: "Queue pick-up by DeploymentRunnerService",                   waiting: true },
  { step: 3,  where: "BuildService",         what: "TCP / SSH pre-flight ping — fail-fast if VM is off" },
  { step: 4,  where: "BuildService",         what: "Generate self-contained deployment bash script" },
  { step: 5,  where: "SshService",           what: "SSH execute script on remote server, stream logs live" },
  { step: 6,  where: "Remote server",        what: "git clone --depth=1   or   git pull (re-deploy)" },
  { step: 7,  where: "Remote server",        what: "Auto-detect stack  (Next.js · .NET · Node · Python …)" },
  { step: 8,  where: "Remote server",        what: "Write multi-stage Dockerfile via heredoc" },
  { step: 9,  where: "Remote server",        what: "docker build with BuildKit layer cache" },
  { step: 10, where: "Remote server",        what: "Rolling deploy — stop old container, run new one" },
  { step: 11, where: "Remote server",        what: "Health check  (18 × 5 s retries  ·  container status)" },
  { step: 12, where: "BuildService",         what: "Parse  DEPLOYFLOW_URL=http://IP:PORT  from stdout" },
  { step: 13, where: "SignalR broadcaster",  what: "Push final status + live URL to frontend" },
];

function QueuedWorkflowPreview({
  isStuck,
  runnerOnline,
  lastPollSeconds,
}: {
  isStuck: boolean;
  runnerOnline: boolean | null;
  lastPollSeconds: number | null;
}) {
  return (
    <div className="w-full space-y-4 py-3 text-left">
      {/* Status line */}
      <div className="flex items-center gap-2.5">
        <div className="h-2 w-2 rounded-full bg-blue-500 animate-pulse shrink-0" />
        <span className="text-blue-400 text-xs font-semibold tracking-wide">QUEUED</span>
        <span className="text-zinc-600 text-xs">— waiting for a runner to pick up this deployment&hellip;</span>
      </div>

      {/* Runner health pill */}
      {runnerOnline !== null && (
        <div className={cn(
          "inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-medium border",
          runnerOnline
            ? "border-emerald-500/30 bg-emerald-950/40 text-emerald-400"
            : "border-red-500/30 bg-red-950/40 text-red-400"
        )}>
          <div className={cn(
            "h-1.5 w-1.5 rounded-full",
            runnerOnline ? "bg-emerald-400 animate-pulse" : "bg-red-500"
          )} />
          {runnerOnline
            ? `Runner online · last poll ${lastPollSeconds != null ? `${Math.round(lastPollSeconds)}s` : '—'} ago`
            : "Runner offline or unresponsive"}
        </div>
      )}

      {/* Stuck runner warning */}
      {isStuck && (
        <div className="flex items-start gap-2.5 rounded-md border border-amber-500/30 bg-amber-500/8 px-3 py-2.5">
          <AlertTriangle className="h-3.5 w-3.5 text-amber-400 mt-0.5 shrink-0" />
          <div className="space-y-1">
            <p className="text-xs font-semibold text-amber-400">Runner hasn&apos;t picked up yet</p>
            <p className="text-[11px] leading-relaxed text-zinc-500">
              The runner polls every 5 s. If it has been longer than expected the backend API may
              be stopped or the runner loop threw an unhandled exception — check the API server
              terminal for errors.
            </p>
          </div>
        </div>
      )}

      {/* Workflow steps table */}
      <div>
        <p className="text-[10px] font-semibold uppercase tracking-widest text-zinc-700 pb-2">
          Deployment Workflow — 13 steps
        </p>
        <div className="space-y-0.5">
          {WORKFLOW_OVERVIEW.map((s) => (
            <div
              key={s.step}
              className={cn(
                "grid items-start gap-x-3 rounded px-2 py-1.5 text-[11px] leading-snug",
                "grid-cols-[18px_1fr_auto]",
                s.done    ? "text-emerald-400" :
                s.waiting ? "border border-blue-500/20 bg-blue-950/50 text-blue-300" :
                "text-zinc-700"
              )}
            >
              {/* Step icon / number */}
              <div className="flex items-center justify-center mt-0.5">
                {s.done    ? <CheckCircle2 className="h-3.5 w-3.5 text-emerald-500" /> :
                 s.waiting ? <Loader2 className="h-3.5 w-3.5 text-blue-400 animate-spin" /> :
                 <span className="font-mono text-[10px] text-zinc-800">{s.step}</span>}
              </div>
              {/* Description */}
              <span>{s.what}</span>
              {/* Where */}
              <span className={cn(
                "whitespace-nowrap font-mono text-[10px]",
                s.done ? "text-zinc-600" : s.waiting ? "text-zinc-500" : "text-zinc-800"
              )}>{s.where}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

import { BACKEND_BASE_URL } from "@/lib/api-client";

const SIGNALR_URL = `${BACKEND_BASE_URL}/hubs/logs`;
const SERVER_LABEL = BACKEND_BASE_URL.replace(/^https?:\/\//, "");

function ElapsedTimer({ from, to }: { from: number; to?: number }) {
  const fixed = to !== undefined ? Math.floor((to - from) / 1000) : undefined;
  const [elapsed, setElapsed] = useState(() => fixed ?? Math.floor((Date.now() - from) / 1000));
  useEffect(() => {
    if (fixed !== undefined) { setElapsed(fixed); return; }
    // Initialise immediately so we don't show 0s on remount
    setElapsed(Math.floor((Date.now() - from) / 1000));
    const t = setInterval(() => setElapsed(Math.floor((Date.now() - from) / 1000)), 1000);
    return () => clearInterval(t);
  }, [from, fixed]);
  const m = Math.floor(elapsed / 60);
  const s = elapsed % 60;
  return <>{m > 0 ? `${m}m ` : ""}{s < 10 ? `0${s}` : s}s</>;
}

function WorkflowProgressPanel({ stepStates, isFailed, onStepClick }: {
  stepStates: StepState[];
  isFailed: boolean;
  onStepClick: (stepId: string) => void;
}) {
  const hasProgress = stepStates.some(s => s.status !== "pending");
  if (!hasProgress) return null;

  const doneCount   = stepStates.filter(s => s.status === "done").length;
  const failIndex   = stepStates.findIndex(s => s.status === "failed");
  const progressPct = isFailed
    ? Math.round(((failIndex >= 0 ? failIndex : doneCount) / PIPELINE_STEPS.length) * 100)
    : Math.round((doneCount / PIPELINE_STEPS.length) * 100);

  const estRemaining = stepStates.reduce((acc, state, i) => {
    if (state.status === "pending") return acc + PIPELINE_STEPS[i].estSec;
    if (state.status === "active")  return acc + Math.floor(PIPELINE_STEPS[i].estSec * 0.6);
    return acc;
  }, 0);

  const activeIdx = stepStates.findIndex(s => s.status === "active");

  return (
    <div className="rounded-xl border border-border/40 bg-muted/5 p-4 space-y-3">
      {/* Header */}
      <div className="flex items-center justify-between text-xs">
        <span className="font-semibold text-foreground">Pipeline Steps</span>
        <span className="text-muted-foreground">
          {progressPct === 100
            ? "\u2705 All steps complete"
            : isFailed
              ? "\u274c Pipeline stopped \u2014 see logs"
              : `\u2248 ${fmtSec(estRemaining)} est. remaining`}
        </span>
      </div>

      {/* Progress bar */}
      <div className="h-1.5 rounded-full bg-border/50 overflow-hidden">
        <div
          className={cn(
            "h-full rounded-full transition-all duration-700",
            isFailed ? "bg-destructive" : progressPct === 100 ? "bg-emerald-500" : "bg-blue-500"
          )}
          style={{ width: `${progressPct}%` }}
        />
      </div>

      {/* Step badges — each is a button that scrolls to that step in the log */}
      <div className="flex flex-wrap gap-1.5">
        {PIPELINE_STEPS.map((step, i) => {
          const state = stepStates[i];
          const Icon  = getStepIcon(step.id);
          const isClickable = state.status !== "pending";
          return (
            <button
              key={step.id}
              type="button"
              title={isClickable ? `Jump to ${step.label} in logs` : step.desc}
              disabled={!isClickable}
              onClick={() => isClickable && onStepClick(step.id)}
              className={cn(
                "flex items-center gap-1 rounded-full px-2.5 py-0.5 text-[11px] font-medium border transition-all",
                "disabled:cursor-default",
                state.status === "done"   ? "border-emerald-500/40 bg-emerald-500/10 text-emerald-400 cursor-pointer hover:bg-emerald-500/20 hover:border-emerald-500/60" :
                state.status === "active" ? "border-blue-500/40 bg-blue-500/10 text-blue-400 cursor-pointer hover:bg-blue-500/20" :
                state.status === "failed" ? "border-destructive/40 bg-destructive/10 text-destructive cursor-pointer hover:bg-destructive/20" :
                "border-border/40 bg-muted/20 text-muted-foreground/60"
              )}
            >
              {state.status === "done"   ? <CheckCircle2 className="h-3 w-3" /> :
               state.status === "active" ? <Loader2 className="h-3 w-3 animate-spin" /> :
               state.status === "failed" ? <XCircle className="h-3 w-3" /> :
               <Icon className="h-3 w-3" />}
              {step.label}
            </button>
          );
        })}
      </div>

      {/* Active step hint */}
      {!isFailed && progressPct < 100 && (
        <p className="text-[11px] text-muted-foreground">
          {activeIdx >= 0
            ? `\u25B6\uFE0F  ${PIPELINE_STEPS[activeIdx].desc}\u2026`
            : "Waiting for next step\u2026"}
        </p>
      )}
      {/* Discoverability hint — only shown when at least one step is done */}
      {stepStates.some(s => s.status !== "pending") && (
        <p className="text-[10px] text-muted-foreground/50">
          Click any completed step badge to jump to its log section.
        </p>
      )}
    </div>
  );
}

export function DeploymentLiveLog({ deploymentId, initialStatus, startedAt, finishedAt }: DeploymentLiveLogProps) {
  const token = useAuthStore((s) => s.accessToken);
  const queryClient = useQueryClient();
  const [lines, setLines] = useState<LogLine[]>([]);
  const [connected, setConnected] = useState(false);
  const [status, setStatus] = useState(initialStatus ?? "queued");
  const [backendOffline, setBackendOffline] = useState(false);
  const [apiErrorMsg, setApiErrorMsg] = useState<string | null>(null);
  const [rerunLoading, setRerunLoading] = useState(false);
  const [autoScroll, setAutoScroll] = useState(true);
  // Refs for each pipeline step's first matching log line — used for step-click scrolling
  const stepLineRefs = useRef<Record<string, HTMLDivElement | null>>({});
  const scrollAreaRef = useRef<HTMLDivElement | null>(null);

  // ── Sync internal status from parent prop ─────────────────────────────────
  // The parent page polls the deployment and passes updated status via initialStatus.
  // We only accept the parent's value if it represents a more-advanced state (higher phase),
  // or the known terminal states — this prevents backward flips during polling races.
  useEffect(() => {
    if (!initialStatus || initialStatus === status) return;
    const parentPhase = phaseIndex(initialStatus);
    const localPhase  = phaseIndex(status);
    const parentIsTerminal = ["healthy", "running", "failed", "cancelled"].includes(initialStatus);
    // Accept if parent is ahead, or if parent is terminal and we're still active
    if (parentPhase > localPhase || (parentIsTerminal && !["healthy", "running", "failed", "cancelled"].includes(status))) {
      setStatus(initialStatus);
    }
  }, [initialStatus]); // eslint-disable-line react-hooks/exhaustive-deps

  // Use deployment's actual creation time as timer origin so navigation away+back
  // doesn't reset the counter to 0.
  const timerOrigin = useMemo(() => {
    if (startedAt) {
      const parsed = new Date(startedAt).getTime();
      if (!isNaN(parsed)) return parsed;
    }
    return Date.now();
  }, [startedAt]);

  // Elapsed in seconds — only used for the "stuck queued" warning
  const elapsed = Math.floor((Date.now() - timerOrigin) / 1000);
  // Show diagnostic "stuck" warning after 45 s with no runner pickup
  const stuckWarning = status === "queued" && lines.length === 0 && elapsed > 45;
  const bottomRef = useRef<HTMLDivElement>(null);
  const connectionRef = useRef<HubConnection | null>(null);
  const seenIds = useRef(new Set<string>());
  // Tracks whether component is still mounted — prevents setState after teardown
  const mountedRef = useRef(true);
  // Always-current status ref so queryFn closures don't capture stale state
  const statusRef = useRef(status);
  useEffect(() => { statusRef.current = status; }, [status]);

  const isActive   = ["queued", "building", "deploying"].includes(status);
  const isTerminal = ["healthy", "running", "failed", "cancelled"].includes(status);
  const isFailed   = status === "failed" || status === "cancelled";

  // Poll runner health every 10s while deployment is queued
  const { data: runnerStatus } = useRunnerStatus(status === "queued");
  const runnerOnline = runnerStatus ? runnerStatus.isRunning : null;
  const runnerLastPollSec = runnerStatus?.secondsSinceLastPoll ?? null;

  // Detect server-offline error from log content — covers SSH/network failure patterns
  const serverOffline = lines.some(
    (l) =>
      l.stream === "stderr" &&
      /offline or unreachable|connection timed out|connection refused|no route to host|host is unreachable|network is unreachable|ssh.*connect.*failed|port 22.*failed|name or service not known/i.test(l.message)
  );

  // Classify the type of SSH failure for a more precise error message
  const sshErrorLine = lines.find(
    (l) =>
      l.stream === "stderr" &&
      /offline or unreachable|connection timed out|connection refused|no route to host|host is unreachable|network is unreachable|ssh.*connect.*failed|port 22.*failed|name or service not known/i.test(l.message)
  );
  const sshErrorDetail = sshErrorLine
    ? /connection timed out/i.test(sshErrorLine.message)
      ? "SSH connection timed out"
      : /connection refused/i.test(sshErrorLine.message)
      ? "SSH port 22 refused the connection"
      : /no route to host|host is unreachable|network is unreachable/i.test(sshErrorLine.message)
      ? "No network route to host"
      : /name or service not known/i.test(sshErrorLine.message)
      ? "Hostname could not be resolved"
      : "SSH connection failed"
    : null;

  // Extract the server IP mentioned in connectivity-check log lines
  const serverIpLine = lines.find((l) => /checking connectivity to/i.test(l.message));
  const serverIpMatch = serverIpLine?.message.match(/(\d{1,3}(?:\.\d{1,3}){3}(?::\d+)?)/);
  const remoteServerIp = serverIpMatch ? serverIpMatch[1] : null;

  // Scroll to the first log line that matches a given pipeline step
  const handleStepClick = useCallback((stepId: string) => {
    const stepEl = stepLineRefs.current[stepId];
    if (!stepEl) return;
    // Scroll the ScrollArea viewport to the step's first log line
    const viewport = scrollAreaRef.current?.querySelector("[data-radix-scroll-area-viewport]") as HTMLElement | null;
    if (viewport) {
      const offsetTop = stepEl.offsetTop - 60; // leave a small margin at top
      viewport.scrollTo({ top: Math.max(0, offsetTop), behavior: "smooth" });
      setAutoScroll(false);
      // Briefly highlight the line
      stepEl.classList.add("ring-1", "ring-blue-500/60", "rounded");
      setTimeout(() => stepEl.classList.remove("ring-1", "ring-blue-500/60", "rounded"), 1500);
    }
  }, []);

  // ── Poll deployment status via REST every 5s while active ──────────────────
  useQuery({
    queryKey: ["dep-live-status", deploymentId],
    queryFn: async () => {
      const d = await apiClient.get<any>(`/deployments/${deploymentId}`);
      if (!mountedRef.current) return d;
      setBackendOffline(false);
      setApiErrorMsg(null);
      // Backend serialises enums as PascalCase (e.g. "Healthy"); normalise to lowercase so
      // phaseIndex() and isTerminal comparisons (which use lowercase) work correctly.
      const rawApiStatus: string | undefined = d?.status;
      const normalizedStatus = typeof rawApiStatus === "string" ? rawApiStatus.toLowerCase() : rawApiStatus;
      if (normalizedStatus && normalizedStatus !== statusRef.current) {
        setStatus(normalizedStatus);
        // Immediately refresh the parent useDeployment query so the page header badge updates
        queryClient.invalidateQueries({ queryKey: ["deployments", "detail", deploymentId] });
      }
      return d;
    },
    enabled: !!deploymentId,
    // Use a function-form so React Query re-evaluates on every refetch cycle
    // based on the latest fetched data, not the React state closure
    refetchInterval: (query: any) => {
      const rawStatus: string = (query.state.data as any)?.status ?? statusRef.current;
      // Normalise PascalCase enum values ("Healthy", "Failed") to lowercase before comparing
      const latestStatus = typeof rawStatus === "string" ? rawStatus.toLowerCase() : rawStatus;
      return ["healthy", "running", "failed", "cancelled"].includes(latestStatus) ? false : 5_000;
    },
    staleTime: 0,
    retry: 1,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    onError: (err: any) => {
      if (!mountedRef.current) return;
      const msg: string = err?.message ?? "Unknown error";
      // Network-level failure = backend unreachable
      const isNetwork = msg.includes("Unable to reach") || msg.includes("ECONNABORTED") || msg.includes("Network Error");
      setBackendOffline(isNetwork);
      setApiErrorMsg(
        isNetwork
          ? `Backend API is unreachable (${SERVER_LABEL}). The API server may be stopped.`
          : `API error: ${msg}`
      );
    },
  } as any);

  // ── Poll deployment logs via REST every 4s as fallback ─────────────────────
  useQuery({
    queryKey: ["dep-live-logs", deploymentId],
    queryFn: async () => {
      // Use the deployment-specific logs endpoint which filters by deployment ID.
      // The global /api/logs endpoint does NOT accept a deploymentId filter and would
      // return unrelated tenant-wide logs (or miss old logs beyond the page limit).
      const res = await apiClient.get<any>(`/deployments/${deploymentId}/logs`, {
        params: { pageSize: 500, page: 1 },
      });
      if (!mountedRef.current) return res;
      setBackendOffline(false);
      setApiErrorMsg(null);
      const items: any[] = Array.isArray(res) ? res : (res?.data ?? []);
      const newLines: LogLine[] = items.map((l: any) => ({
        id: l.id ?? `rest-${l.timestamp ?? l.createdAt}-${l.message?.slice(0, 20)}`,
        message: l.message ?? "",
        stream: l.stream ?? l.level ?? "stdout",
        timestamp: l.timestamp ?? l.createdAt ?? new Date().toISOString(),
      }));
      setLines((prev) => {
        const merged = [...prev];
        for (const line of newLines) {
          if (!seenIds.current.has(line.id)) {
            seenIds.current.add(line.id);
            merged.push(line);
          }
        }
        return merged.sort((a, b) => a.timestamp.localeCompare(b.timestamp));
      });
      return res;
    },
    enabled: !!deploymentId,
    refetchInterval: isTerminal ? false : 4_000,
    staleTime: 0,
    retry: 1,
  });

  // ── SignalR live stream ─────────────────────────────────────────────────────
  useEffect(() => {
    mountedRef.current = true;
    if (!deploymentId) return;
    const signalR = getSignalR();
    if (!signalR) return;

    // Do NOT use skipNegotiation — let SignalR negotiate the best transport
    // (WebSockets → ServerSentEvents → LongPolling). skipNegotiation=true with
    // WebSockets-only causes an AbortError when the component remounts during
    // Next.js Fast Refresh before the handshake completes.
    const connection: HubConnection = new signalR.HubConnectionBuilder()
      .withUrl(SIGNALR_URL, {
        accessTokenFactory: () => token ?? "",
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      // Use an explicit null logger instead of LogLevel.None — when SignalR is
      // loaded via dynamic require(), the enum value may not resolve correctly.
      // A { log: () => {} } object is guaranteed to suppress every internal
      // SignalR console message (including "stopped during negotiation").
      // REST polling covers any transient connection hiccup.
      .configureLogging({ log: () => {} })
      .build();

    connection.on("log", (data: { message: string; stream?: string; timestamp: string }) => {
      if (!mountedRef.current) return;
      const id = `sig-${data.timestamp}-${Math.random()}`;
      if (!seenIds.current.has(id)) {
        seenIds.current.add(id);
        setLines((prev) => [...prev, { id, ...data }]);
      }
    });

    connection.on("statusChanged", (data: { status: string }) => {
      if (mountedRef.current) {
        setStatus(data.status);
        // Also push the update into the parent page's React Query cache so the
        // header badge refreshes without waiting for the next 5s REST poll.
        queryClient.invalidateQueries({ queryKey: ["deployments", "detail", deploymentId] });
      }
    });

    connection.onreconnecting(() => { if (mountedRef.current) setConnected(false); });
    connection.onreconnected(async () => {
      if (!mountedRef.current) return;
      setConnected(true);
      try { await connection.invoke("SubscribeToDeployment", deploymentId); } catch { /* ignore */ }
    });
    connection.onclose(() => { if (mountedRef.current) setConnected(false); });

    connection
      .start()
      .then(async () => {
        if (!mountedRef.current) { connection.stop().catch(() => {}); return; }
        setConnected(true);
        try { await connection.invoke("SubscribeToDeployment", deploymentId); } catch { /* ignore */ }
      })
      .catch((err: unknown) => {
        // Suppress AbortError — it is caused by component unmounting before
        // SignalR finishes the HTTP negotiate handshake; REST polling covers us.
        const msg = err instanceof Error ? err.message : String(err);
        // Suppress noise: connection stopped before negotiate completes (Fast Refresh / unmount race)
        const isSignalRNoise = msg.includes("stop()") || msg.includes("AbortError") || msg.includes("negotiation");
        if (!isSignalRNoise) {
          console.warn("SignalR failed — REST polling active:", msg);
        }
        if (mountedRef.current) setConnected(false);
      });

    connectionRef.current = connection;

    return () => {
      mountedRef.current = false;
      connection.stop().catch(() => {});
      connectionRef.current = null;
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [deploymentId, token]);

  // Auto-scroll when new lines arrive
  useEffect(() => {
    if (autoScroll) bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [lines, autoScroll]);

  // Reset step-line refs when deployment changes so stale refs don't point to old DOM nodes
  useEffect(() => {
    stepLineRefs.current = {};
  }, [deploymentId]);

  const currentPhase = phaseIndex(status);
  const aiUrl = `/ai-assistant?context=deployment&id=${deploymentId}`;
  const stepStates   = deriveStepStates(lines);

  const fullLogText = useMemo(() => {
    if (lines.length === 0) return "";
    return lines
      .map((line, i) => {
        const ts = new Date(line.timestamp).toISOString();
        const stream = line.stream ?? "stdout";
        return `${i + 1}. [${ts}] [${stream}] ${line.message}`;
      })
      .join("\n");
  }, [lines]);

  const dotnetHostCommandIssue = useMemo(() => {
    const text = lines.map(l => l.message).join("\n");
    return /\.net project detected|dotnet: command not found|dotnet_install:|dotnetsdkmissing|custom install command/i.test(text);
  }, [lines]);

  const recommendedNextSteps = useMemo(() => {
    return [
      "Restart or redeploy the DeployFlow backend service so the latest pipeline fixes are active.",
      "Re-run the same deployment.",
      "In Project Settings, keep InstallCommand and BuildCommand empty for dockerized .NET apps unless host execution is explicitly required.",
      "If stack detection is used, apply detection again so sanitized command values are persisted.",
    ];
  }, []);

  const copyEntireLog = useCallback(async () => {
    if (!fullLogText) {
      toast.info("No logs to copy yet");
      return;
    }
    try {
      await navigator.clipboard.writeText(fullLogText);
      toast.success(`Copied ${lines.length} log lines`);
    } catch {
      toast.error("Failed to copy logs");
    }
  }, [fullLogText, lines.length]);

  const copyNextSteps = useCallback(async () => {
    const payload = ["Next Steps:", ...recommendedNextSteps.map((s, i) => `${i + 1}. ${s}`)].join("\n");
    try {
      await navigator.clipboard.writeText(payload);
      toast.success("Next steps copied");
    } catch {
      toast.error("Failed to copy next steps");
    }
  }, [recommendedNextSteps]);

  const rerunFailedDeployment = useCallback(async () => {
    try {
      setRerunLoading(true);
      const res = await apiClient.post<any>(`/deployments/${deploymentId}/rerun`);
      const newId = res?.id ?? res?.value?.id;

      toast.success("Re-run started", {
        description: newId ? `Deployment ${newId} queued.` : "New deployment queued.",
      });

      if (newId) {
        window.location.href = `/logs?deploymentId=${newId}`;
      } else {
        window.location.href = "/deployments";
      }
    } catch (e: any) {
      toast.error("Failed to re-run deployment", {
        description: e?.message ?? "Please trigger it manually from Deployments page.",
      });
    } finally {
      setRerunLoading(false);
    }
  }, [deploymentId]);

  const downloadIncidentReport = useCallback(async () => {
    try {
      const storedToken = useAuthStore.getState().accessToken;
      const res = await fetch(`${BACKEND_BASE_URL}/api/deployments/${deploymentId}/incident-report`, {
        headers: { Authorization: `Bearer ${storedToken}` },
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error((err as any)?.error ?? `HTTP ${res.status}`);
      }
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `incident-${deploymentId}.json`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      toast.success("Incident report downloaded");
    } catch (e: any) {
      toast.error("Failed to download incident report", { description: e?.message });
    }
  }, [deploymentId]);

  return (
    <div className="space-y-3">

      {/* ── API / backend offline banner ── */}
      {backendOffline && (
        <div className="flex items-start gap-3 rounded-lg border border-destructive/40 bg-destructive/8 px-4 py-3.5">
          <ServerCrash className="h-4 w-4 shrink-0 text-destructive mt-0.5" />
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold text-destructive">Backend API is unreachable</p>
            <p className="text-xs text-muted-foreground mt-0.5">
              Cannot connect to <span className="font-mono">{SERVER_LABEL}</span>.
              The API server may be stopped or your network may be blocking the connection.
            </p>
          </div>
          <Button size="sm" variant="outline" className="gap-1.5 shrink-0 h-7 text-xs" asChild>
            <Link href={aiUrl}><Bot className="h-3 w-3" />Ask AI</Link>
          </Button>
        </div>
      )}

      {/* ── Non-network API error ── */}
      {!backendOffline && apiErrorMsg && (
        <div className="flex items-start gap-3 rounded-lg border border-amber-500/40 bg-amber-500/8 px-4 py-3.5">
          <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500 mt-0.5" />
          <p className="flex-1 text-xs text-amber-500">{apiErrorMsg}</p>
        </div>
      )}

      {/* ── Deploy server offline banner (from log content) ── */}
      {serverOffline && (
        <div className="rounded-lg border border-destructive/50 bg-destructive/8 divide-y divide-destructive/20 overflow-hidden">
          {/* Header */}
          <div className="flex items-start gap-3 px-4 py-3">
            <WifiOff className="h-4 w-4 shrink-0 text-destructive mt-0.5" />
            <div className="flex-1 min-w-0">
              <p className="text-sm font-semibold text-destructive">Remote server is unreachable</p>
              <p className="text-xs text-muted-foreground mt-0.5">
                {sshErrorDetail && <span className="text-foreground font-medium">{sshErrorDetail}. </span>}
                {remoteServerIp
                  ? <>Could not reach <span className="font-mono text-foreground">{remoteServerIp}</span> via SSH.</>  
                  : "Could not establish an SSH connection to the deploy target."}
              </p>
            </div>
            <Button size="sm" variant="outline" className="gap-1.5 shrink-0 h-7 text-xs border-destructive/30" asChild>
              <Link href={aiUrl}><Bot className="h-3 w-3" />Ask AI</Link>
            </Button>
          </div>
          {/* Action steps */}
          <div className="px-4 py-3 space-y-2">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Resolution steps</p>
            <ol className="text-xs text-muted-foreground space-y-1.5 list-none">
              <li className="flex items-start gap-2"><span className="flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-border/60 text-[10px] font-bold mt-0.5">1</span><span>Go to your cloud provider <strong className="text-foreground">(Azure / AWS / GCP)</strong> and start the VM if it is stopped.</span></li>
              <li className="flex items-start gap-2"><span className="flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-border/60 text-[10px] font-bold mt-0.5">2</span><span>Confirm <strong className="text-foreground">port 22</strong> is open in the firewall / security group / NSG rules.</span></li>
              <li className="flex items-start gap-2"><span className="flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-border/60 text-[10px] font-bold mt-0.5">3</span><span>Once the server is reachable, use <strong className="text-foreground">Re-deploy</strong> to retry the deployment.</span></li>
            </ol>
          </div>
          {/* Action buttons */}
          <div className="flex items-center gap-2 px-4 py-2.5 bg-destructive/5">
            <Button size="sm" variant="outline" className="gap-1.5 h-7 text-xs" asChild>
              <Link href="/servers">View Servers</Link>
            </Button>
            <Link
              href={`/deployments?project=${deploymentId}`}
              className="ml-auto text-xs text-primary hover:underline"
            >
              Re-deploy →
            </Link>
          </div>
        </div>
      )}

      {/* ── Phase progress tracker ── */}
      <div className="rounded-xl border border-border/40 bg-muted/10 p-4 space-y-3">
        <div className="flex items-center">
          {PHASES.map((phase, i) => {
            // For failed/cancelled: detect which phase the failure happened in from logs,
            // otherwise fall back to the last status-reported phase
            const failedPhase = isFailed
              ? (stepStates.some(s => s.status === "failed" && ["ssh"].includes(s.id))
                  ? 1   // SSH failed → failed during Building
                  : stepStates.some(s => s.status === "failed" && ["docker","source","build"].includes(s.id))
                  ? 1   // Build steps failed
                  : stepStates.some(s => s.status === "failed" && ["replace","start","health"].includes(s.id))
                  ? 2   // Deploying steps failed
                  : lastActivePhase(status))
              : -1;

            const done    = !isFailed && currentPhase > i;
            const active  = !isFailed && currentPhase === i;
            const failed  = isFailed && i === failedPhase;
            const skiped  = isFailed && i > failedPhase;
            return (
              <div key={phase.key} className="flex items-center flex-1 min-w-0">
                <div className="flex flex-col items-center gap-1">
                  <div className={cn(
                    "flex h-7 w-7 items-center justify-center rounded-full border-2 transition-all",
                    done   ? "border-emerald-500 bg-emerald-500 text-white" :
                    active ? "border-blue-500 bg-blue-500/20 text-blue-400" :
                    failed ? "border-destructive bg-destructive/20 text-destructive" :
                    skiped ? "border-border/30 bg-background/30 text-muted-foreground/30" :
                    "border-border bg-background text-muted-foreground"
                  )}>
                    {done   ? <CheckCircle2 className="h-3.5 w-3.5" /> :
                     active ? <Loader2 className="h-3 w-3 animate-spin" /> :
                     failed ? <XCircle className="h-3.5 w-3.5" /> :
                     <span className="text-[10px] font-bold">{i + 1}</span>}
                  </div>
                  <span className={cn(
                    "text-[10px] font-medium whitespace-nowrap",
                    done   ? "text-emerald-500" :
                    active ? "text-blue-400" :
                    failed ? "text-destructive" :
                    skiped ? "text-muted-foreground/30" :
                    "text-muted-foreground"
                  )}>{phase.label}</span>
                </div>
                {i < PHASES.length - 1 && (
                  <div className={cn(
                    "h-0.5 flex-1 mx-2 rounded transition-all",
                    done ? "bg-emerald-500" :
                    (isFailed && i < failedPhase) ? "bg-emerald-500/50" :
                    "bg-border/50"
                  )} />
                )}
              </div>
            );
          })}
        </div>
        <p className="text-xs text-muted-foreground text-center">
          {isFailed
            ? "\u274c Deployment failed \u2014 see logs below for the error details"
            : PHASES[Math.min(currentPhase, 3)]?.desc}
        </p>
      </div>

      {/* ── Detailed pipeline workflow steps ── */}
      {(status !== "queued" || lines.length > 0) && (
        <WorkflowProgressPanel stepStates={stepStates} isFailed={isFailed} onStepClick={handleStepClick} />
      )}

      {/* ── Status / connection bar ── */}
      <div className="flex items-center justify-between flex-wrap gap-2">
        <div className="flex items-center gap-2 flex-wrap">
          <Badge
            variant="outline"
            className={cn("gap-1.5 text-xs",
              connected
                ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-500"
                : backendOffline
                  ? "border-destructive/30 bg-destructive/10 text-destructive"
                  : "border-amber-500/30 bg-amber-500/10 text-amber-500"
            )}
          >
            {connected
              ? <Wifi className="h-3 w-3" />
              : backendOffline
                ? <ServerCrash className="h-3 w-3" />
                : <WifiOff className="h-3 w-3" />}
            {connected ? "Live stream" : backendOffline ? "API offline" : "Polling (4s)"}
          </Badge>
          <Badge variant="outline" className="gap-1 text-xs text-muted-foreground border-border/50">
            <Server className="h-3 w-3" />
            {SERVER_LABEL}
          </Badge>
          <Badge variant="outline" className="gap-1 text-xs text-muted-foreground border-border/50">
            <Clock className="h-3 w-3" />
            <ElapsedTimer
              from={timerOrigin}
              to={isTerminal && finishedAt ? new Date(finishedAt).getTime() : undefined}
            />
          </Badge>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="text-xs text-muted-foreground">{lines.length} lines</span>
          <Button variant="ghost" size="sm" className="h-6 px-2 text-xs gap-1"
            onClick={copyEntireLog}>
            <Copy className="h-3 w-3" />Copy Entire Log
          </Button>
          <Button variant="ghost" size="sm" className="h-6 px-2 text-xs"
            onClick={() => { setLines([]); seenIds.current.clear(); }}>Clear</Button>
          <Button
            variant={autoScroll ? "secondary" : "ghost"}
            size="sm" className="h-6 px-2 text-xs gap-1"
            onClick={() => setAutoScroll((v) => !v)}
          >
            <ChevronDown className="h-3 w-3" />Auto-scroll
          </Button>
        </div>
      </div>

      {/* ── Log terminal ── */}
      <ScrollArea
        ref={scrollAreaRef}
        className="h-[420px] rounded-lg border border-border/40 bg-[#0d1117] font-mono text-xs"
        onScrollCapture={(e) => {
          const el = e.currentTarget.querySelector("[data-radix-scroll-area-viewport]") as HTMLElement;
          if (!el) return;
          const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 40;
          setAutoScroll(atBottom);
        }}
      >
        <div className="p-4 space-y-0.5">
          {lines.length === 0 ? (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              className={status === "queued" && !backendOffline
                ? "w-full py-4"
                : "flex flex-col items-center justify-center py-12 text-center space-y-4"}
            >
              {backendOffline ? (
                <>
                  <ServerCrash className="h-10 w-10 text-destructive/40" />
                  <div className="space-y-1">
                    <p className="text-sm text-zinc-400 font-medium">Backend API is offline</p>
                    <p className="text-xs text-zinc-600">
                      Cannot reach <span className="text-zinc-500 font-mono">{SERVER_LABEL}</span>.
                      Check the API server is running.
                    </p>
                  </div>
                  <div className="flex gap-2">
                    <Button size="sm" variant="outline" className="gap-1.5 h-7 text-xs border-zinc-700 text-zinc-400 hover:text-zinc-200"
                      onClick={() => window.location.reload()}>
                      <RefreshCw className="h-3 w-3" />Retry
                    </Button>
                    <Button size="sm" variant="outline" className="gap-1.5 h-7 text-xs border-zinc-700 text-zinc-400 hover:text-zinc-200" asChild>
                      <Link href={aiUrl}><Bot className="h-3 w-3" />Ask AI Assistant</Link>
                    </Button>
                  </div>
                </>
              ) : status === "queued" ? (
                <QueuedWorkflowPreview
                  isStuck={stuckWarning}
                  runnerOnline={runnerOnline}
                  lastPollSeconds={runnerLastPollSec}
                />
              ) : (
                <>
                  <div className="relative">
                    <Terminal className="h-10 w-10 text-zinc-700" />
                    {isActive && (
                      <div className="absolute -top-1 -right-1 h-3 w-3 rounded-full bg-blue-500 animate-pulse" />
                    )}
                  </div>
                  <div className="space-y-1">
                    <p className="text-sm text-zinc-500 font-medium">
                      {status === "queued"    ? "Deployment is queued \u2014 waiting for a runner\u2026" :
                       status === "building"  ? "Build in progress \u2014 logs streaming shortly\u2026" :
                       status === "deploying" ? "Container starting \u2014 waiting for output\u2026" :
                       status === "failed"    ? "Deployment failed \u2014 no log output was captured." :
                       status === "cancelled" ? "Deployment was cancelled before it started." :
                       "Waiting for deployment logs\u2026"}
                    </p>
                    <p className="text-xs text-zinc-600">
                      {status === "failed"
                        ? "The deploy server may be offline or the SSH key may be incorrect."
                        : connected
                          ? "Connected to live stream \u00b7 logs will appear in real-time"
                          : "Polling server every 4s \u00b7 logs will appear here"}
                    </p>
                  </div>
                  {(status === "failed" || (!isActive && lines.length === 0)) && (
                    <Button size="sm" variant="outline" className="gap-1.5 h-7 text-xs border-zinc-700 text-zinc-400 hover:text-zinc-200" asChild>
                      <Link href={aiUrl}><Bot className="h-3 w-3" />Troubleshoot with AI Assistant</Link>
                    </Button>
                  )}
                  {isActive && (
                    <div className="flex gap-1.5 pt-1">
                      {[0, 1, 2].map((i) => (
                        <div key={i}
                          className="h-1.5 w-1.5 rounded-full bg-zinc-600 animate-bounce"
                          style={{ animationDelay: `${i * 150}ms` }}
                        />
                      ))}
                    </div>
                  )}
                </>
              )}
            </motion.div>
          ) : (
            <AnimatePresence initial={false}>
              {lines.map((line, i) => {
                // Register the first log line that matches each pipeline step's startPattern
                // so clicking a step badge scrolls to it.
                const matchedStepId = PIPELINE_STEPS.find(
                  (step, si) =>
                    stepStates[si]?.status !== "pending" &&
                    step.startPattern.test(line.message) &&
                    !stepLineRefs.current[step.id]
                )?.id ?? null;

                return (
                  <motion.div
                    key={line.id}
                    ref={(el) => {
                      if (matchedStepId && el) stepLineRefs.current[matchedStepId] = el;
                    }}
                    initial={{ opacity: 0, x: -4 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ duration: 0.08 }}
                    className={cn("flex gap-3 py-0.5 leading-relaxed transition-all", lineClass(line.stream, line.message))}
                  >
                    <span className="text-zinc-600 select-none w-5 text-right shrink-0 tabular-nums">
                      {i + 1}
                    </span>
                    <span className="text-zinc-600 shrink-0 select-none w-20 tabular-nums">
                      {new Date(line.timestamp).toLocaleTimeString("en-US", {
                        hour12: false, hour: "2-digit", minute: "2-digit", second: "2-digit",
                      })}
                    </span>
                    <span className="flex-1 whitespace-pre-wrap break-all">{line.message}</span>
                  </motion.div>
                );
              })}
            </AnimatePresence>
          )}
          {/* AI Assistant CTA when failed and logs exist */}
          {isFailed && lines.length > 0 && (
            <div className="mt-4 flex items-center justify-between rounded-lg border border-zinc-800 bg-zinc-900/60 px-4 py-3">
              <div className="flex items-center gap-2">
                <Bot className="h-4 w-4 text-primary" />
                <p className="text-xs text-zinc-400">
                  Having trouble? Let the AI Assistant analyse these logs and suggest a fix.
                </p>
              </div>
              <Button size="sm" variant="secondary" className="gap-1.5 h-7 text-xs shrink-0 ml-4" asChild>
                <Link href={aiUrl}>Troubleshoot</Link>
              </Button>
            </div>
          )}
          <div ref={bottomRef} />
        </div>
      </ScrollArea>

      {(isFailed || dotnetHostCommandIssue) && lines.length > 0 && (
        <div className="rounded-lg border border-amber-500/30 bg-amber-500/5 p-4 space-y-3">
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-sm font-semibold text-amber-400">Recommended Next Steps</p>
              <p className="text-xs text-muted-foreground mt-1">
                Use these actions for .NET-on-Linux deployment pipeline failures.
              </p>
            </div>
            <Button size="sm" variant="outline" className="h-7 text-xs gap-1.5" onClick={copyNextSteps}>
              <Copy className="h-3 w-3" />Copy Steps
            </Button>
          </div>
          <ol className="space-y-1.5 text-xs text-muted-foreground">
            {recommendedNextSteps.map((step, idx) => (
              <li key={idx}>{idx + 1}. {step}</li>
            ))}
          </ol>
          <div className="flex items-center gap-2">
            <Button size="sm" variant="outline" className="h-7 text-xs" asChild>
              <Link href="/projects">Open Project Settings</Link>
            </Button>
            <Button size="sm" variant="outline" className="h-7 text-xs" onClick={rerunFailedDeployment} disabled={rerunLoading}>
              {rerunLoading ? "Re-running…" : "Re-run Deployment"}
            </Button>
            {isFailed && (
              <Button size="sm" variant="outline" className="h-7 text-xs gap-1" onClick={downloadIncidentReport}>
                Export Incident
              </Button>
            )}
          </div>
        </div>
      )}
    </div>
  );
}