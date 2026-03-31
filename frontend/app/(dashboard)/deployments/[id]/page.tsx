"use client";

import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { motion } from "framer-motion";
import {
  ArrowLeft, Rocket, Clock, GitCommit, Terminal,
  CheckCircle2, XCircle, AlertTriangle, Loader2,
  ShieldCheck, ShieldX, GitBranch, TrendingUp,
  Brain, Lightbulb, Wrench, MessageSquare, Send,
  ChevronRight, ExternalLink, RotateCcw, X,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import {
  useDeployment, useApproveDeployment, useRejectDeployment,
  useStartCanary, usePromoteCanary, useAbortCanary,
  useAiAnalyzeDeployment, useAiChat, useDeploymentErrors,
  useCancelDeployment, useCreateDeployment,
  type ErrorSuggestionDto,
} from "@/hooks/use-api";
import { formatRelativeTime, formatDuration, formatDate } from "@/lib/utils";
import { DeploymentLiveLog } from "@/components/deployments/deployment-live-log";
import Link from "next/link";
import { toast } from "sonner";

const statusConfig: Record<string, { icon: React.ElementType; color: string; label: string }> = {
  running: { icon: Loader2, color: "text-blue-500", label: "Running" },
  healthy: { icon: CheckCircle2, color: "text-success", label: "Healthy" },
  failed: { icon: XCircle, color: "text-destructive", label: "Failed" },
  building: { icon: Loader2, color: "text-warning", label: "Building" },
  deploying: { icon: Loader2, color: "text-info", label: "Deploying" },
  queued: { icon: Clock, color: "text-muted-foreground", label: "Queued" },
  cancelled: { icon: AlertTriangle, color: "text-muted-foreground", label: "Cancelled" },
  stopped: { icon: AlertTriangle, color: "text-muted-foreground", label: "Stopped" },
};

export default function DeploymentDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;
  const { data: deployment, isLoading } = useDeployment(id);

  const [approvalNotes, setApprovalNotes] = useState("");
  const [canaryTraffic, setCanaryTraffic] = useState(10);
  const [canaryStep, setCanaryStep] = useState(10);
  const [activeTab, setActiveTab] = useState("overview");

  // AI Debug state
  const [aiEnabled, setAiEnabled] = useState(false);
  const [chatInput, setChatInput] = useState("");
  const [chatHistory, setChatHistory] = useState<{ role: "user" | "assistant"; content: string }[]>([]);

  const approve = useApproveDeployment();
  const reject = useRejectDeployment();
  const cancel = useCancelDeployment();
  const redeploy = useCreateDeployment();
  const startCanary = useStartCanary();
  const promoteCanary = usePromoteCanary();
  const abortCanary = useAbortCanary();
  const { data: aiAnalysis, isLoading: aiLoading } = useAiAnalyzeDeployment(id, aiEnabled);
  const aiChat = useAiChat();
  const { data: smartErrors } = useDeploymentErrors(
    deployment?.status === "failed" ? id : null
  );

  const status = deployment
    ? (statusConfig[deployment.status] ?? { icon: AlertTriangle, color: "text-muted-foreground", label: deployment.status })
    : null;

  const isPending = approve.isPending || reject.isPending;
  const isCanaryBusy = startCanary.isPending || promoteCanary.isPending || abortCanary.isPending;

  const handleApprove = async () => {
    await approve.mutateAsync({ id, notes: approvalNotes || undefined });
    toast.success("Deployment approved");
    setApprovalNotes("");
  };

  const handleReject = async () => {
    await reject.mutateAsync({ id, notes: approvalNotes || undefined });
    toast.success("Deployment rejected");
    setApprovalNotes("");
  };

  const handleSendChat = async (message: string) => {
    if (!message.trim()) return;
    const userMsg = message.trim();
    setChatHistory(h => [...h, { role: "user", content: userMsg }]);
    setChatInput("");
    try {
      const res = await aiChat.mutateAsync({ message: userMsg, deploymentId: id });
      setChatHistory(h => [...h, { role: "assistant", content: res.reply }]);
    } catch {
      setChatHistory(h => [...h, { role: "assistant", content: "Sorry, I couldn't process that request." }]);
    }
  };

  const handleStartCanary = async () => {
    await startCanary.mutateAsync({ id, trafficPercent: canaryTraffic, stepDurationMinutes: canaryStep });
    toast.success(`Canary started at ${canaryTraffic}% traffic`);
  };

  const handleCancel = async () => {
    try {
      await cancel.mutateAsync(id);
      toast.success("Deployment cancelled");
    } catch (e: any) {
      toast.error("Failed to cancel", { description: e?.message });
    }
  };

  const handleRedeploy = async () => {
    if (!deployment) return;
    try {
      await redeploy.mutateAsync({
        projectId: deployment.projectId,
        branch: deployment.branch ?? undefined,
        trigger: "manual",
      });
      toast.success("New deployment queued — check the Deployments list");
    } catch (e: any) {
      toast.error("Failed to re-deploy", { description: e?.message });
    }
  };

  const isStuckQueued = deployment?.status === "queued";
  const isCancellable = ["queued", "building", "deploying"].includes(deployment?.status ?? "");

  return (
    <div className="space-y-6 max-w-4xl mx-auto">
      <div className="flex items-center justify-between gap-3">
        <Button variant="ghost" size="sm" onClick={() => router.back()}>
          <ArrowLeft className="h-4 w-4 mr-1.5" />Back
        </Button>
        {/* Cancel / Re-deploy actions — shown for active or stuck deployments */}
        {deployment && (
          <div className="flex items-center gap-2">
            {isCancellable && (
              <Button
                variant="outline"
                size="sm"
                className="gap-1.5 text-destructive border-destructive/40 hover:bg-destructive/10"
                onClick={handleCancel}
                disabled={cancel.isPending}
              >
                {cancel.isPending
                  ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  : <X className="h-3.5 w-3.5" />}
                Cancel
              </Button>
            )}
            {(deployment.status === "failed" || deployment.status === "cancelled") && (
              <Button
                variant="outline"
                size="sm"
                className="gap-1.5"
                onClick={handleRedeploy}
                disabled={redeploy.isPending}
              >
                {redeploy.isPending
                  ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  : <RotateCcw className="h-3.5 w-3.5" />}
                Re-deploy
              </Button>
            )}
          </div>
        )}
      </div>

      {/* Stuck-at-Queued diagnostic banner */}
      {isStuckQueued && (
        <div className="flex items-start gap-3 rounded-lg border border-amber-500/40 bg-amber-500/8 px-4 py-3.5">
          <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500 mt-0.5" />
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold text-amber-500">Deployment is waiting for a runner</p>
            <p className="text-xs text-muted-foreground mt-0.5">
              The backend runner polls every 5 seconds. If it stays queued, check that the API server is
              running and the project has a server with an SSH key assigned.
              You can cancel this deployment and trigger a new one if needed.
            </p>
          </div>
          <Button
            variant="outline"
            size="sm"
            className="gap-1.5 shrink-0 h-7 text-xs"
            onClick={handleCancel}
            disabled={cancel.isPending}
          >
            {cancel.isPending ? <Loader2 className="h-3 w-3 animate-spin" /> : <X className="h-3 w-3" />}
            Cancel
          </Button>
        </div>
      )}

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-48" />
        </div>
      ) : deployment ? (
        <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} className="space-y-1">
          <div className="flex items-center gap-3 flex-wrap">
            <h1 className="text-2xl font-bold">{deployment.projectName}</h1>
            {status && (
              <Badge variant="outline" className={`gap-1 ${status.color}`}>
                <status.icon className={`h-3.5 w-3.5 ${["running","building","deploying"].includes(deployment.status) ? "animate-spin" : ""}`} />
                {status.label}
              </Badge>
            )}
            {deployment.approvalStatus && deployment.approvalStatus !== "NotRequired" && (
              <Badge variant="outline" className={
                deployment.approvalStatus === "Approved" ? "gap-1 text-green-500 border-green-500/30" :
                deployment.approvalStatus === "Rejected" ? "gap-1 text-destructive border-destructive/30" :
                "gap-1 text-yellow-500 border-yellow-500/30"
              }>
                {deployment.approvalStatus === "Approved" && <ShieldCheck className="h-3.5 w-3.5" />}
                {deployment.approvalStatus === "Rejected" && <ShieldX className="h-3.5 w-3.5" />}
                {deployment.approvalStatus === "Pending" && <Clock className="h-3.5 w-3.5" />}
                {deployment.approvalStatus}
              </Badge>
            )}
            {deployment.canaryStatus && deployment.canaryStatus !== "None" && (
              <Badge variant="outline" className={
                deployment.canaryStatus === "Running" ? "gap-1 text-blue-500 border-blue-500/30" :
                deployment.canaryStatus === "Promoted" ? "gap-1 text-green-500 border-green-500/30" :
                "gap-1 text-muted-foreground"
              }>
                <GitBranch className="h-3.5 w-3.5" />
                Canary {deployment.canaryStatus}
                {deployment.canaryStatus === "Running" && ` (${deployment.canaryTrafficPercent}%)`}
              </Badge>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            Deployment {deployment.id.slice(0, 8)}
            {" · "}
            <span title={formatDate(deployment.createdAt, "PPpp")} className="cursor-help border-b border-dashed border-muted-foreground/50">
              {formatRelativeTime(deployment.createdAt)}
            </span>
            {" · "}
            <span className="text-xs">{formatDate(deployment.createdAt, "MMM d, yyyy HH:mm")}</span>
          </p>
        </motion.div>
      ) : (
        <div className="text-center py-16">
          <p className="text-muted-foreground">Deployment not found.</p>
          <Button variant="outline" size="sm" className="mt-4" asChild>
            <Link href="/deployments">View all deployments</Link>
          </Button>
        </div>
      )}

      {deployment && (
        <Tabs value={activeTab} onValueChange={setActiveTab}>
          <TabsList>
            <TabsTrigger value="overview">Overview</TabsTrigger>
            <TabsTrigger value="logs">Logs</TabsTrigger>
            {deployment.approvalStatus === "Pending" && (
              <TabsTrigger value="approval">
                <span className="flex items-center gap-1">
                  <span className="relative flex h-2 w-2">
                    <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-yellow-400 opacity-75" />
                    <span className="relative inline-flex rounded-full h-2 w-2 bg-yellow-500" />
                  </span>
                  Approval
                </span>
              </TabsTrigger>
            )}
            <TabsTrigger value="ai-debug" className="gap-1.5">
              <Brain className="h-3.5 w-3.5" />
              AI Debug
            </TabsTrigger>
            {(deployment.status === "healthy" || deployment.status === "running" || deployment.canaryStatus === "Running") && (
              <TabsTrigger value="canary">Canary</TabsTrigger>
            )}
          </TabsList>

          <TabsContent value="overview" className="mt-4 space-y-4">
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              {[
                { label: "Status", value: status?.label ?? "–" },
                { label: "Branch", value: deployment.branch ?? "–" },
                { label: "Duration", value: deployment.duration ? formatDuration(deployment.duration) : "–" },
                { label: "Trigger", value: deployment.trigger },
              ].map(({ label, value }) => (
                <Card key={label}>
                  <CardContent className="pt-4 pb-3">
                    <p className="text-xs text-muted-foreground mb-1">{label}</p>
                    <p className="font-medium text-sm">{value}</p>
                  </CardContent>
                </Card>
              ))}
            </div>

            {/* ── App URL banner ── */}
            {deployment.url && (
              <Card className="border-emerald-500/30 bg-emerald-500/5">
                <CardContent className="py-3 flex items-center justify-between gap-4">
                  <div className="min-w-0">
                    <p className="text-xs text-muted-foreground mb-0.5">Live App URL</p>
                    <p className="text-sm font-mono truncate text-emerald-600 dark:text-emerald-400">{deployment.url}</p>
                  </div>
                  <a
                    href={deployment.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="shrink-0"
                  >
                    <Button size="sm" className="gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white">
                      <ExternalLink className="h-3.5 w-3.5" />
                      Open App
                    </Button>
                  </a>
                </CardContent>
              </Card>
            )}
            {deployment.errorMessage && (
              <Card className="border-destructive/40 bg-destructive/5">
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm text-destructive flex items-center gap-2">
                    Error Details
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-sm text-destructive/90 font-mono whitespace-pre-wrap">{deployment.errorMessage}</p>
                </CardContent>
              </Card>
            )}
            {deployment.commitMessage && (
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm flex items-center gap-2">
                    <GitCommit className="h-4 w-4" /> Commit
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-sm">{deployment.commitMessage}</p>
                  {deployment.commitSha && (
                    <p className="text-xs text-muted-foreground font-mono mt-1">{deployment.commitSha.slice(0,12)}</p>
                  )}
                </CardContent>
              </Card>
            )}
          </TabsContent>

          {/* Logs tab — forceMount keeps DeploymentLiveLog mounted so logs/timer preserve when switching tabs */}
          <TabsContent value="logs" forceMount className={activeTab !== "logs" ? "hidden" : "mt-4"}>
            <Card>
              <CardHeader className="flex flex-row items-center justify-between pb-3">
                <CardTitle className="text-sm flex items-center gap-2">
                  <Terminal className="h-4 w-4" /> Build Logs
                </CardTitle>
              </CardHeader>
              <CardContent>
                <DeploymentLiveLog
                  deploymentId={id}
                  initialStatus={deployment?.status}
                  startedAt={deployment?.createdAt}
                />
              </CardContent>
            </Card>
          </TabsContent>

          {/* AI Debug Tab */}
          <TabsContent value="ai-debug" className="mt-4 space-y-4">
            {/* Smart Debug — auto error analysis for failed deployments */}
            {smartErrors && smartErrors.length > 0 && (
              <Card className="border-destructive/30 bg-destructive/5">
                <CardHeader className="pb-3">
                  <CardTitle className="text-sm flex items-center gap-2 text-destructive">
                    <AlertTriangle className="h-4 w-4" />
                    Smart Debug — {smartErrors.length} issue{smartErrors.length !== 1 ? "s" : ""} detected
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-2">
                  {(smartErrors as ErrorSuggestionDto[]).map((e, i) => {
                    const severityColor =
                      e.severity === "critical" ? "border-red-400 text-red-600" :
                      e.severity === "high"     ? "border-orange-400 text-orange-600" :
                      e.severity === "medium"   ? "border-yellow-400 text-yellow-600" :
                      "border-blue-400 text-blue-600";
                    return (
                      <div key={i} className="rounded-md border border-border/40 bg-background p-3 space-y-1.5">
                        <div className="flex items-center justify-between gap-2">
                          <p className="text-sm font-medium">{e.title}</p>
                          <Badge variant="outline" className={`text-xs capitalize shrink-0 ${severityColor}`}>
                            {e.severity}
                          </Badge>
                        </div>
                        <p className="text-xs text-muted-foreground">{e.description}</p>
                        <div className="rounded bg-emerald-500/10 border border-emerald-500/20 px-2.5 py-1.5">
                          <p className="text-xs font-medium text-emerald-700 dark:text-emerald-400 flex items-start gap-1.5">
                            <Lightbulb className="h-3 w-3 mt-0.5 shrink-0" />
                            {e.fix}
                          </p>
                        </div>
                        <Badge variant="secondary" className="text-[10px] px-1.5 py-0">{e.category}</Badge>
                      </div>
                    );
                  })}
                </CardContent>
              </Card>
            )}
            {!aiEnabled ? (
              <Card>
                <CardContent className="pt-8 pb-8 flex flex-col items-center text-center gap-4">
                  <div className="h-14 w-14 rounded-2xl bg-violet-500/10 flex items-center justify-center">
                    <Brain className="h-7 w-7 text-violet-500" />
                  </div>
                  <div>
                    <h3 className="font-semibold text-base mb-1">AI-Powered Deployment Analysis</h3>
                    <p className="text-sm text-muted-foreground max-w-sm">
                      Run an automated analysis to detect root causes, identify patterns, and get actionable fix suggestions.
                    </p>
                  </div>
                  <Button
                    onClick={() => setAiEnabled(true)}
                    className="gap-2 bg-violet-600 hover:bg-violet-700"
                  >
                    <Brain className="h-4 w-4" />
                    Analyze Deployment
                  </Button>
                </CardContent>
              </Card>
            ) : aiLoading ? (
              <Card>
                <CardContent className="pt-8 pb-8 flex flex-col items-center gap-3">
                  <Loader2 className="h-8 w-8 animate-spin text-violet-500" />
                  <p className="text-sm text-muted-foreground">Analyzing deployment logs…</p>
                </CardContent>
              </Card>
            ) : aiAnalysis ? (
              <>
                {/* Severity + Diagnosis */}
                <Card className={{
                  critical: "border-red-500/40 bg-red-50/30",
                  high:     "border-orange-500/40 bg-orange-50/30",
                  medium:   "border-yellow-500/40 bg-yellow-50/30",
                  low:      "border-blue-500/40 bg-blue-50/30",
                  ok:       "border-emerald-500/40 bg-emerald-50/30",
                }[aiAnalysis.severity] ?? ""}
                >
                  <CardHeader className="pb-3">
                    <CardTitle className="text-sm flex items-center gap-2">
                      <Brain className="h-4 w-4 text-violet-500" />
                      Diagnosis
                      <Badge variant="outline" className={`ml-auto capitalize ${
                        aiAnalysis.severity === "critical" ? "border-red-400 text-red-600" :
                        aiAnalysis.severity === "high"     ? "border-orange-400 text-orange-600" :
                        aiAnalysis.severity === "medium"   ? "border-yellow-400 text-yellow-600" :
                        aiAnalysis.severity === "ok"       ? "border-emerald-400 text-emerald-600" :
                        "border-blue-400 text-blue-600"
                      }`}>
                        {aiAnalysis.severity}
                      </Badge>
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    <p className="text-sm">{aiAnalysis.diagnosis}</p>
                    <div className="rounded-md bg-muted/50 p-3">
                      <p className="text-xs text-muted-foreground font-medium mb-1">Root Cause</p>
                      <p className="text-sm">{aiAnalysis.rootCause}</p>
                    </div>
                  </CardContent>
                </Card>

                {/* Suggestions */}
                {aiAnalysis.suggestions.length > 0 && (
                  <Card>
                    <CardHeader className="pb-3">
                      <CardTitle className="text-sm flex items-center gap-2">
                        <Lightbulb className="h-4 w-4 text-amber-500" />
                        Suggestions
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-2">
                      {aiAnalysis.suggestions.map((s, i) => (
                        <div key={i} className="flex gap-3 p-3 rounded-md bg-muted/40 border border-border/40">
                          <ChevronRight className="h-4 w-4 text-muted-foreground mt-0.5 shrink-0" />
                          <div>
                            <p className="text-sm font-medium">{s.title}</p>
                            <p className="text-xs text-muted-foreground mt-0.5">{s.detail}</p>
                            <Badge variant="secondary" className="mt-1.5 text-[10px] px-1.5 py-0">{s.category}</Badge>
                          </div>
                        </div>
                      ))}
                    </CardContent>
                  </Card>
                )}

                {/* Auto-fixes */}
                {aiAnalysis.autoFixes.length > 0 && (
                  <Card>
                    <CardHeader className="pb-3">
                      <CardTitle className="text-sm flex items-center gap-2">
                        <Wrench className="h-4 w-4 text-blue-500" />
                        Quick Actions
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="flex flex-wrap gap-2">
                      {aiAnalysis.autoFixes.map(fix => (
                        <Button
                          key={fix.id}
                          variant={fix.destructive ? "destructive" : "outline"}
                          size="sm"
                          className="gap-1.5"
                          onClick={() => toast.info(`Running: ${fix.label}`)}
                        >
                          <Wrench className="h-3.5 w-3.5" />
                          {fix.label}
                        </Button>
                      ))}
                    </CardContent>
                  </Card>
                )}

                {/* AI Chat */}
                <Card>
                  <CardHeader className="pb-3">
                    <CardTitle className="text-sm flex items-center gap-2">
                      <MessageSquare className="h-4 w-4 text-violet-500" />
                      Ask AI
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    {chatHistory.length > 0 && (
                      <div className="space-y-2 max-h-64 overflow-y-auto">
                        {chatHistory.map((m, i) => (
                          <div key={i} className={`flex ${ m.role === "user" ? "justify-end" : "justify-start" }`}>
                            <div className={`max-w-[80%] rounded-lg px-3 py-2 text-sm ${
                              m.role === "user"
                                ? "bg-primary text-primary-foreground"
                                : "bg-muted text-foreground"
                            }`}>
                              {m.content}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                    <div className="flex gap-2">
                      <Input
                        placeholder="Ask about this deployment…"
                        value={chatInput}
                        onChange={e => setChatInput(e.target.value)}
                        onKeyDown={e => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); handleSendChat(chatInput); } }}
                        disabled={aiChat.isPending}
                      />
                      <Button
                        size="sm"
                        className="shrink-0"
                        onClick={() => handleSendChat(chatInput)}
                        disabled={!chatInput.trim() || aiChat.isPending}
                      >
                        {aiChat.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
                      </Button>
                    </div>
                    <div className="flex flex-wrap gap-1.5">
                      {["Why did this fail?", "How to rollback?", "What is canary?"].map(q => (
                        <Button
                          key={q}
                          variant="outline"
                          size="sm"
                          className="text-xs h-7"
                          onClick={() => handleSendChat(q)}
                          disabled={aiChat.isPending}
                        >
                          {q}
                        </Button>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              </>
            ) : null}
          </TabsContent>

          {deployment.approvalStatus === "Pending" && (
            <TabsContent value="approval" className="mt-4">
              <Card>
                <CardHeader className="pb-3">
                  <CardTitle className="text-base flex items-center gap-2">
                    <ShieldCheck className="h-5 w-5 text-yellow-500" />
                    Deployment Approval Required
                  </CardTitle>
                  <p className="text-sm text-muted-foreground">
                    This deployment is awaiting approval before it can proceed.
                  </p>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="space-y-1.5">
                    <Label htmlFor="approval-notes" className="text-xs">Notes (optional)</Label>
                    <Textarea
                      id="approval-notes"
                      placeholder="Add approval or rejection notes…"
                      value={approvalNotes}
                      onChange={e => setApprovalNotes(e.target.value)}
                      rows={3}
                    />
                  </div>
                  <div className="flex gap-3">
                    <Button
                      className="flex-1 bg-green-600 hover:bg-green-700 text-white"
                      disabled={isPending}
                      onClick={handleApprove}
                    >
                      {approve.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <ShieldCheck className="h-4 w-4 mr-1.5" />}
                      Approve
                    </Button>
                    <Button
                      variant="destructive"
                      className="flex-1"
                      disabled={isPending}
                      onClick={handleReject}
                    >
                      {reject.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <ShieldX className="h-4 w-4 mr-1.5" />}
                      Reject
                    </Button>
                  </div>
                </CardContent>
              </Card>
            </TabsContent>
          )}

          {(deployment.status === "healthy" || deployment.status === "running" || deployment.canaryStatus === "Running") && (
            <TabsContent value="canary" className="mt-4 space-y-4">
              {deployment.canaryStatus === "Running" ? (
                <Card>
                  <CardHeader className="pb-3">
                    <CardTitle className="text-base flex items-center gap-2">
                      <GitBranch className="h-5 w-5 text-blue-500" />
                      Canary Release Active
                    </CardTitle>
                    <p className="text-sm text-muted-foreground">
                      {deployment.canaryTrafficPercent}% of traffic is routed to this canary deployment.
                    </p>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    {/* Traffic split visualization */}
                    <div className="space-y-1">
                      <div className="flex justify-between text-xs text-muted-foreground">
                        <span>Canary</span>
                        <span>Baseline</span>
                      </div>
                      <div className="h-3 rounded-full overflow-hidden bg-muted flex">
                        <div
                          className="bg-blue-500 transition-all duration-500"
                          style={{ width: `${deployment.canaryTrafficPercent ?? 0}%` }}
                        />
                        <div
                          className="bg-muted-foreground/20"
                          style={{ width: `${100 - (deployment.canaryTrafficPercent ?? 0)}%` }}
                        />
                      </div>
                      <div className="flex justify-between text-xs font-medium">
                        <span className="text-blue-500">{deployment.canaryTrafficPercent}%</span>
                        <span className="text-muted-foreground">{100 - (deployment.canaryTrafficPercent ?? 0)}%</span>
                      </div>
                    </div>
                    <div className="flex gap-3 pt-2">
                      <Button
                        className="flex-1 bg-green-600 hover:bg-green-700 text-white"
                        disabled={isCanaryBusy}
                        onClick={() => {
                          promoteCanary.mutateAsync(id).then(() => toast.success("Canary promoted to 100%!"));
                        }}
                      >
                        {promoteCanary.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <TrendingUp className="h-4 w-4 mr-1.5" />}
                        Promote to 100%
                      </Button>
                      <Button
                        variant="destructive"
                        className="flex-1"
                        disabled={isCanaryBusy}
                        onClick={() => {
                          abortCanary.mutateAsync(id).then(() => toast.success("Canary aborted — traffic restored to baseline"));
                        }}
                      >
                        {abortCanary.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <XCircle className="h-4 w-4 mr-1.5" />}
                        Abort Canary
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              ) : (
                <Card>
                  <CardHeader className="pb-3">
                    <CardTitle className="text-base flex items-center gap-2">
                      <GitBranch className="h-5 w-5" />
                      Start Canary Release
                    </CardTitle>
                    <p className="text-sm text-muted-foreground">
                      Route a portion of traffic to this deployment while the baseline remains active.
                    </p>
                  </CardHeader>
                  <CardContent className="space-y-5">
                    <div className="space-y-2">
                      <Label className="text-xs">
                        Initial traffic split — <span className="font-semibold text-foreground">{canaryTraffic}%</span> to new version
                      </Label>
                      <input
                        type="range"
                        min={1}
                        max={50}
                        step={1}
                        value={canaryTraffic}
                        onChange={e => setCanaryTraffic(Number(e.target.value))}
                        className="w-full accent-primary"
                      />
                      <div className="flex justify-between text-xs text-muted-foreground">
                        <span>1%</span>
                        <span>50%</span>
                      </div>
                    </div>
                    <div className="space-y-1.5">
                      <Label htmlFor="canary-step" className="text-xs">Step duration (minutes)</Label>
                      <Input
                        id="canary-step"
                        type="number"
                        min={1}
                        max={120}
                        value={canaryStep}
                        onChange={e => setCanaryStep(Number(e.target.value))}
                        className="w-32"
                      />
                    </div>
                    <Button
                      disabled={isCanaryBusy}
                      onClick={handleStartCanary}
                    >
                      {startCanary.isPending ? <Loader2 className="h-4 w-4 mr-1.5 animate-spin" /> : <GitBranch className="h-4 w-4 mr-1.5" />}
                      Start Canary at {canaryTraffic}%
                    </Button>
                  </CardContent>
                </Card>
              )}
            </TabsContent>
          )}
        </Tabs>
      )}
    </div>
  );
}