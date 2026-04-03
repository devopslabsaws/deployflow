"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  Activity,
  ArrowLeft,
  Bot,
  BrainCircuit,
  CheckCircle2,
  Copy,
  GitPullRequest,
  Loader2,
  Radar,
  Send,
  ShieldAlert,
  ShieldCheck,
  Sparkles,
  TriangleAlert,
  Workflow,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Skeleton } from "@/components/ui/skeleton";
import {
  useAiChat,
  useAiIncidentCommander,
  useAiOpsMemory,
  useAiPipelineArchitecture,
  useAiPolicySimulation,
  useAiPreviewQa,
  useAiRiskAssessment,
  useCreatePolicyTemplate,
  useDeployment,
  useProjects,
  type AiActionPlan,
  type AiPolicyDraft,
} from "@/hooks/use-api";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
}

function SeverityBadge({ value }: { value?: string | null }) {
  const tone = (value ?? "low").toLowerCase();
  const klass = tone === "critical"
    ? "bg-red-500/10 text-red-300 border-red-500/30"
    : tone === "high"
      ? "bg-orange-500/10 text-orange-300 border-orange-500/30"
      : tone === "medium"
        ? "bg-amber-500/10 text-amber-300 border-amber-500/30"
        : tone === "healthy" || tone === "ok"
          ? "bg-emerald-500/10 text-emerald-300 border-emerald-500/30"
          : "bg-sky-500/10 text-sky-300 border-sky-500/30";

  return <Badge variant="outline" className={klass}>{value ?? "n/a"}</Badge>;
}

function MetricCard({ label, value, hint }: { label: string; value: string; hint: string }) {
  return (
    <Card className="border-white/10 bg-white/[0.03] backdrop-blur-sm">
      <CardContent className="p-4">
        <p className="text-[11px] uppercase tracking-[0.18em] text-zinc-400">{label}</p>
        <p className="mt-2 text-2xl font-semibold text-zinc-50">{value}</p>
        <p className="mt-1 text-xs text-zinc-400">{hint}</p>
      </CardContent>
    </Card>
  );
}

function ProjectSelect({
  value,
  onChange,
  projects,
}: {
  value: string;
  onChange: (value: string) => void;
  projects: Array<{ id: string; name: string }>;
}) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="h-10 rounded-lg border border-border/60 bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-amber-500/30"
    >
      {projects.map((project) => (
        <option key={project.id} value={project.id}>{project.name}</option>
      ))}
    </select>
  );
}

export default function AIAssistantPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const fromDeploymentId = searchParams.get("id") ?? "";
  const fromContext = searchParams.get("context");

  const { data: deployment } = useDeployment(fromDeploymentId);
  const { data: projectResponse } = useProjects();
  const projects = projectResponse?.data ?? [];

  const [selectedProjectId, setSelectedProjectId] = useState("");
  const [incidentDeploymentId, setIncidentDeploymentId] = useState(fromDeploymentId);
  const [riskBranch, setRiskBranch] = useState("main");
  const [riskEnvironment, setRiskEnvironment] = useState("production");
  const [policyPrompt, setPolicyPrompt] = useState("All production deploys after 6 PM need 2 approvals unless branch starts with hotfix/");
  const [previewBranch, setPreviewBranch] = useState("feature/mission-control");
  const [previewTitle, setPreviewTitle] = useState("Improve deployment AI mission control");
  const [previewSummary, setPreviewSummary] = useState("Adds incident timeline, deploy risk scoring, policy simulation, preview QA, and ops memory views.");
  const [chatInput, setChatInput] = useState("");
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([
    {
      id: "welcome",
      role: "assistant",
      content: "AI Mission Control is online. Ask for deployment guidance, incident RCA, rollout advice, or pipeline architecture.",
    },
  ]);

  const incident = useAiIncidentCommander(incidentDeploymentId, !!incidentDeploymentId);
  const riskAssessment = useAiRiskAssessment();
  const policySimulation = useAiPolicySimulation();
  const previewQa = useAiPreviewQa();
  const pipelineArchitecture = useAiPipelineArchitecture(selectedProjectId, !!selectedProjectId);
  const opsMemory = useAiOpsMemory(selectedProjectId || undefined);
  const aiChat = useAiChat();
  const createPolicyTemplate = useCreatePolicyTemplate();

  useEffect(() => {
    if (!selectedProjectId && deployment?.projectId) {
      setSelectedProjectId(deployment.projectId);
      setRiskBranch(deployment.branch || "main");
    }
  }, [deployment, selectedProjectId]);

  useEffect(() => {
    if (!selectedProjectId && projects.length > 0) {
      setSelectedProjectId(projects[0].id);
    }
  }, [projects, selectedProjectId]);

  async function handleRunRiskTwin() {
    if (!selectedProjectId) return;
    try {
      await riskAssessment.mutateAsync({
        projectId: selectedProjectId,
        branch: riskBranch,
        environmentSlug: riskEnvironment,
      });
    } catch (error: any) {
      toast.error("Risk twin failed", { description: error?.message });
    }
  }

  async function handleSimulatePolicy() {
    try {
      await policySimulation.mutateAsync({ prompt: policyPrompt });
    } catch (error: any) {
      toast.error("Policy simulation failed", { description: error?.message });
    }
  }

  async function handleCreateDraft(draft: AiPolicyDraft) {
    try {
      await createPolicyTemplate.mutateAsync({
        ...draft,
        requiredApproverRole: draft.requiredApproverRole ?? undefined,
        autoApprovePattern: draft.autoApprovePattern ?? undefined,
        allowedHoursUtc: draft.allowedHoursUtc ?? undefined,
        projectId: selectedProjectId || undefined,
      });
      toast.success("Policy template created from AI draft");
    } catch (error: any) {
      toast.error("Failed to create policy template", { description: error?.message });
    }
  }

  async function handleGeneratePreviewQa() {
    if (!selectedProjectId) return;
    try {
      await previewQa.mutateAsync({
        projectId: selectedProjectId,
        branch: previewBranch,
        prTitle: previewTitle,
        changeSummary: previewSummary,
      });
    } catch (error: any) {
      toast.error("Preview QA generation failed", { description: error?.message });
    }
  }

  async function handleSendChat() {
    const text = chatInput.trim();
    if (!text) return;

    const userMessage: ChatMessage = {
      id: `user-${crypto.randomUUID()}`,
      role: "user",
      content: text,
    };
    setChatMessages((prev) => [...prev, userMessage]);
    setChatInput("");

    try {
      const result = await aiChat.mutateAsync({
        message: text,
        deploymentId: incidentDeploymentId || undefined,
      });
      setChatMessages((prev) => [
        ...prev,
        {
          id: `assistant-${crypto.randomUUID()}`,
          role: "assistant",
          content: result.reply,
        },
      ]);
    } catch (error: any) {
      toast.error("AI chat failed", { description: error?.message });
    }
  }

  async function handleCopy(text: string, label: string) {
    await navigator.clipboard.writeText(text);
    toast.success(`${label} copied`);
  }

  const riskData = riskAssessment.data;
  const policyData = policySimulation.data;
  const previewData = previewQa.data;
  const incidentData = incident.data;
  const opsMemoryItems = opsMemory.data?.items ?? [];

  return (
    <div className="mx-auto max-w-[1500px] space-y-6 pb-10">
      <section className="relative overflow-hidden rounded-[28px] border border-white/10 bg-[radial-gradient(circle_at_top_left,_rgba(245,158,11,0.22),_transparent_28%),radial-gradient(circle_at_top_right,_rgba(34,211,238,0.18),_transparent_30%),linear-gradient(180deg,_rgba(24,24,27,0.98),_rgba(9,9,11,0.98))] p-6 text-zinc-50 shadow-2xl shadow-black/20">
        <div className="absolute inset-y-0 right-0 w-[34%] bg-[linear-gradient(135deg,transparent,rgba(255,255,255,0.04))]" />
        <div className="relative flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
          <div className="space-y-4">
            <div className="flex items-center gap-3">
              {(fromDeploymentId || fromContext) && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-9 w-9 rounded-full border border-white/10 bg-white/5 p-0 text-zinc-200 hover:bg-white/10"
                  onClick={() => fromDeploymentId ? router.push(`/deployments/${fromDeploymentId}`) : router.back()}
                >
                  <ArrowLeft className="h-4 w-4" />
                </Button>
              )}
              <div className="flex h-12 w-12 items-center justify-center rounded-2xl border border-amber-300/20 bg-amber-300/10">
                <BrainCircuit className="h-6 w-6 text-amber-300" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h1 className="text-2xl font-semibold tracking-tight">AI Mission Control</h1>
                  <Badge variant="outline" className="border-cyan-300/20 bg-cyan-300/10 text-cyan-200">
                    <Sparkles className="mr-1 h-3 w-3" /> Sprint Workflows
                  </Badge>
                </div>
                <p className="mt-1 max-w-2xl text-sm text-zinc-300">
                  Incident commander, deploy risk twin, policy authoring, preview QA, pipeline architecture, and ops memory in one operational surface.
                </p>
              </div>
            </div>

            <div className="flex flex-wrap gap-2 text-xs text-zinc-300">
              <Badge variant="outline" className="border-white/10 bg-white/5 text-zinc-300">Route: /ai-assistant</Badge>
              {incidentDeploymentId && <Badge variant="outline" className="border-red-400/20 bg-red-400/10 text-red-200">Deployment context attached</Badge>}
              {selectedProjectId && <Badge variant="outline" className="border-amber-400/20 bg-amber-400/10 text-amber-100">Project selected</Badge>}
            </div>
          </div>

          <div className="grid w-full gap-3 sm:grid-cols-2 xl:w-[760px] xl:grid-cols-4">
            <MetricCard
              label="Incident"
              value={incidentData ? `${incidentData.confidence}%` : "Idle"}
              hint={incidentData ? `${incidentData.severity} confidence` : "Attach deployment context"}
            />
            <MetricCard
              label="Risk Twin"
              value={riskData ? `${riskData.score}` : "--"}
              hint={riskData ? `${riskData.verdict} verdict` : "Run against a project"}
            />
            <MetricCard
              label="Policy Impact"
              value={policyData ? `${policyData.impactedProjectCount}` : "--"}
              hint={policyData ? "projects matched" : "Simulate from plain English"}
            />
            <MetricCard
              label="Ops Memory"
              value={`${opsMemoryItems.length}`}
              hint={opsMemory.data?.summary ?? "Recent recurring failure patterns"}
            />
          </div>
        </div>
      </section>

      <Tabs defaultValue="sprint1" className="space-y-6">
        <TabsList className="h-auto flex-wrap gap-2 rounded-2xl border border-border/60 bg-background/80 p-2">
          <TabsTrigger value="sprint1">Sprint 1: Incident Commander</TabsTrigger>
          <TabsTrigger value="sprint2">Sprint 2: Risk + Pipeline</TabsTrigger>
          <TabsTrigger value="sprint3">Sprint 3: Policy Agent</TabsTrigger>
          <TabsTrigger value="sprint4">Sprint 4: Preview QA</TabsTrigger>
          <TabsTrigger value="sprint5">Sprint 5: Ops Memory</TabsTrigger>
          <TabsTrigger value="assistant">Assistant Chat</TabsTrigger>
        </TabsList>

        <TabsContent value="sprint1" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <ShieldAlert className="h-4 w-4 text-red-400" /> Incident Commander
              </CardTitle>
              <CardDescription>
                Build a coordinated incident view from deployment state, first error log, alerts, and SLO drift.
              </CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-3 lg:flex-row">
              <Input
                value={incidentDeploymentId}
                onChange={(e) => setIncidentDeploymentId(e.target.value)}
                placeholder="Paste deployment ID to activate incident commander"
              />
              <Button onClick={() => incident.refetch()} disabled={!incidentDeploymentId || incident.isFetching} className="gap-2">
                {incident.isFetching ? <Loader2 className="h-4 w-4 animate-spin" /> : <Radar className="h-4 w-4" />}
                Refresh Incident View
              </Button>
            </CardContent>
          </Card>

          {incident.isLoading && (
            <div className="grid gap-4 lg:grid-cols-[1.4fr_1fr]">
              <Skeleton className="h-72 rounded-2xl" />
              <Skeleton className="h-72 rounded-2xl" />
            </div>
          )}

          {incidentData && (
            <div className="grid gap-6 xl:grid-cols-[1.4fr_1fr]">
              <div className="space-y-6">
                <Card className="border-border/60 bg-background/90">
                  <CardHeader>
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <CardTitle className="text-lg">{incidentData.projectName}</CardTitle>
                        <CardDescription>{incidentData.summary}</CardDescription>
                      </div>
                      <SeverityBadge value={incidentData.severity} />
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div className="grid gap-4 md:grid-cols-3">
                      <div className="rounded-2xl border border-border/60 bg-muted/30 p-4">
                        <p className="text-[11px] uppercase tracking-[0.16em] text-muted-foreground">Confidence</p>
                        <p className="mt-2 text-3xl font-semibold">{incidentData.confidence}%</p>
                      </div>
                      <div className="rounded-2xl border border-border/60 bg-muted/30 p-4">
                        <p className="text-[11px] uppercase tracking-[0.16em] text-muted-foreground">Blast Radius</p>
                        <p className="mt-2 text-3xl font-semibold capitalize">{incidentData.blastRadius}</p>
                      </div>
                      <div className="rounded-2xl border border-border/60 bg-muted/30 p-4">
                        <p className="text-[11px] uppercase tracking-[0.16em] text-muted-foreground">SLO</p>
                        <p className="mt-2 text-3xl font-semibold capitalize">{incidentData.sloStatus ?? "n/a"}</p>
                      </div>
                    </div>

                    <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-4">
                      <p className="text-xs font-medium uppercase tracking-[0.16em] text-red-300">Likely Cause</p>
                      <p className="mt-2 text-sm leading-6 text-zinc-200">{incidentData.likelyCause}</p>
                    </div>

                    <div className="rounded-2xl border border-cyan-500/20 bg-cyan-500/5 p-4">
                      <p className="text-xs font-medium uppercase tracking-[0.16em] text-cyan-300">Recommended Decision</p>
                      <p className="mt-2 text-sm leading-6 text-zinc-200">{incidentData.recommendedDecision}</p>
                    </div>

                    <div className="grid gap-4 lg:grid-cols-2">
                      <Card className="border-border/60 bg-muted/20">
                        <CardHeader className="pb-3">
                          <CardTitle className="text-sm">Active Signals</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                          {incidentData.activeSignals.map((signal) => (
                            <div key={signal} className="rounded-xl border border-border/60 bg-background/70 px-3 py-2 text-sm">
                              {signal}
                            </div>
                          ))}
                        </CardContent>
                      </Card>

                      <Card className="border-border/60 bg-muted/20">
                        <CardHeader className="pb-3">
                          <CardTitle className="text-sm">Self-Heal / Action Plan</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                          {incidentData.actionPlan.map((action) => (
                            <div key={`${action.label}-${action.actionType}`} className="rounded-xl border border-border/60 bg-background/70 p-3">
                              <div className="flex items-start justify-between gap-3">
                                <div>
                                  <p className="text-sm font-medium">{action.label}</p>
                                  <p className="mt-1 text-xs text-muted-foreground">{action.detail}</p>
                                </div>
                                <Badge variant="outline" className={cn(
                                  action.riskLevel === "high" ? "border-red-500/30 text-red-300" : "border-amber-500/30 text-amber-300"
                                )}>
                                  {action.riskLevel}
                                </Badge>
                              </div>
                              {action.command && (
                                <button
                                  className="mt-2 w-full rounded-lg border border-dashed border-border/60 bg-black/[0.14] px-3 py-2 text-left font-mono text-xs text-zinc-300 transition-colors hover:bg-black/[0.22]"
                                  onClick={() => handleCopy(action.command || "", action.label)}
                                >
                                  {action.command}
                                </button>
                              )}
                            </div>
                          ))}
                        </CardContent>
                      </Card>
                    </div>
                  </CardContent>
                </Card>
              </div>

              <Card className="border-border/60 bg-background/90">
                <CardHeader>
                  <CardTitle className="text-base">Incident Timeline</CardTitle>
                  <CardDescription>Ordered signals and milestones collected around the deploy event.</CardDescription>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3">
                    {incidentData.timeline.map((event, index) => (
                      <div key={`${event.title}-${index}`} className="relative rounded-2xl border border-border/60 bg-muted/20 p-4">
                        <div className="mb-2 flex items-center justify-between gap-3">
                          <p className="font-medium">{event.title}</p>
                          <Badge variant="outline" className="capitalize">{event.tone}</Badge>
                        </div>
                        <p className="text-sm text-muted-foreground">{event.detail}</p>
                        <p className="mt-2 text-[11px] uppercase tracking-[0.16em] text-muted-foreground">
                          {new Date(event.at).toLocaleString()}
                        </p>
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>
            </div>
          )}
        </TabsContent>

        <TabsContent value="sprint2" className="space-y-6">
          <div className="grid gap-6 xl:grid-cols-[1.05fr_1fr]">
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <Radar className="h-4 w-4 text-amber-400" /> Pre-Deploy Risk Twin
                </CardTitle>
                <CardDescription>
                  Score rollout safety using failure history, alerts, SLO state, governance, and health coverage.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {projects.length > 0 && (
                  <div className="grid gap-3 md:grid-cols-3">
                    <ProjectSelect value={selectedProjectId} onChange={setSelectedProjectId} projects={projects.map((p) => ({ id: p.id, name: p.name }))} />
                    <Input value={riskBranch} onChange={(e) => setRiskBranch(e.target.value)} placeholder="Branch" />
                    <Input value={riskEnvironment} onChange={(e) => setRiskEnvironment(e.target.value)} placeholder="Environment slug" />
                  </div>
                )}
                <Button onClick={handleRunRiskTwin} disabled={!selectedProjectId || riskAssessment.isPending} className="gap-2">
                  {riskAssessment.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <ShieldCheck className="h-4 w-4" />}
                  Run Risk Twin
                </Button>

                {riskData && (
                  <div className="space-y-4 rounded-2xl border border-border/60 bg-muted/20 p-4">
                    <div className="flex flex-wrap items-center gap-3">
                      <p className="text-4xl font-semibold">{riskData.score}</p>
                      <SeverityBadge value={riskData.level} />
                      <Badge variant="outline" className="capitalize">{riskData.verdict}</Badge>
                      <Badge variant="outline">{riskData.suggestedRollout}</Badge>
                    </div>
                    <p className="text-sm text-muted-foreground">{riskData.summary}</p>
                    <div className="space-y-2">
                      {riskData.factors.map((factor) => (
                        <div key={`${factor.name}-${factor.detail}`} className="rounded-xl border border-border/60 bg-background/80 p-3">
                          <div className="flex items-center justify-between gap-3">
                            <p className="font-medium">{factor.name}</p>
                            <Badge variant="outline">{factor.impact > 0 ? `+${factor.impact}` : `${factor.impact}`}</Badge>
                          </div>
                          <p className="mt-1 text-xs text-muted-foreground">{factor.detail}</p>
                        </div>
                      ))}
                    </div>
                    <div className="space-y-2">
                      {riskData.recommendedActions.map((action) => (
                        <div key={action} className="rounded-lg bg-background/60 px-3 py-2 text-sm">{action}</div>
                      ))}
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <Workflow className="h-4 w-4 text-cyan-400" /> Pipeline Architect Agent
                </CardTitle>
                <CardDescription>
                  Recommends rollout blueprints using current project posture instead of generic CI/CD boilerplate.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {!selectedProjectId && <p className="text-sm text-muted-foreground">Select a project to generate pipeline blueprints.</p>}
                {pipelineArchitecture.isLoading && <Skeleton className="h-64 rounded-2xl" />}
                {pipelineArchitecture.data && (
                  <>
                    <div className="rounded-2xl border border-cyan-500/20 bg-cyan-500/5 p-4">
                      <p className="text-sm font-medium">{pipelineArchitecture.data.projectName}</p>
                      <p className="mt-1 text-sm text-muted-foreground">{pipelineArchitecture.data.recommendation}</p>
                    </div>
                    <div className="space-y-3">
                      {pipelineArchitecture.data.blueprints.map((blueprint) => (
                        <div key={blueprint.name} className="rounded-2xl border border-border/60 bg-muted/20 p-4">
                          <div className="mb-2 flex items-center justify-between gap-3">
                            <p className="font-medium">{blueprint.name}</p>
                            <Badge variant="outline">{blueprint.rolloutMode}</Badge>
                          </div>
                          <p className="text-sm text-muted-foreground">{blueprint.summary}</p>
                          <div className="mt-3 flex flex-wrap gap-2">
                            {blueprint.stages.map((stage) => (
                              <Badge key={stage} variant="secondary">{stage}</Badge>
                            ))}
                          </div>
                          <div className="mt-3 grid gap-3 md:grid-cols-2">
                            <div>
                              <p className="text-xs uppercase tracking-[0.16em] text-muted-foreground">Benefits</p>
                              <div className="mt-2 space-y-2">
                                {blueprint.benefits.map((item) => <div key={item} className="text-sm">{item}</div>)}
                              </div>
                            </div>
                            <div>
                              <p className="text-xs uppercase tracking-[0.16em] text-muted-foreground">Tradeoffs</p>
                              <div className="mt-2 space-y-2">
                                {blueprint.tradeoffs.map((item) => <div key={item} className="text-sm text-muted-foreground">{item}</div>)}
                              </div>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  </>
                )}
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="sprint3" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <ShieldCheck className="h-4 w-4 text-emerald-400" /> Policy Authoring Agent
              </CardTitle>
              <CardDescription>
                Convert plain-English deployment governance into a policy draft, then save it as a real approval template.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <textarea
                value={policyPrompt}
                onChange={(e) => setPolicyPrompt(e.target.value)}
                rows={5}
                className="w-full rounded-2xl border border-border/60 bg-background px-4 py-3 text-sm outline-none focus:ring-2 focus:ring-emerald-500/30"
                placeholder="Describe your deployment approval policy in plain English"
              />
              <div className="flex gap-3">
                <Button onClick={handleSimulatePolicy} disabled={policySimulation.isPending || !policyPrompt.trim()} className="gap-2">
                  {policySimulation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />}
                  Simulate Policy
                </Button>
                {selectedProjectId && <Badge variant="outline">Project-scoped save enabled</Badge>}
              </div>

              {policyData && (
                <div className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
                  <div className="space-y-4 rounded-2xl border border-border/60 bg-muted/20 p-4">
                    <div>
                      <p className="text-xs uppercase tracking-[0.16em] text-muted-foreground">Parsed Intent</p>
                      <p className="mt-2 text-sm leading-6">{policyData.parsedIntent}</p>
                    </div>
                    <div>
                      <p className="text-xs uppercase tracking-[0.16em] text-muted-foreground">Impact</p>
                      <p className="mt-2 text-3xl font-semibold">{policyData.impactedProjectCount}</p>
                      <p className="text-xs text-muted-foreground">projects matched the current simulation</p>
                    </div>
                    <div className="space-y-2">
                      {policyData.warnings.map((warning) => (
                        <div key={warning} className="rounded-xl border border-amber-500/20 bg-amber-500/5 px-3 py-2 text-sm text-amber-100">
                          {warning}
                        </div>
                      ))}
                    </div>
                  </div>
                  <div className="space-y-3">
                    {policyData.drafts.map((draft) => (
                      <div key={draft.name} className="rounded-2xl border border-border/60 bg-background/80 p-4">
                        <div className="flex items-start justify-between gap-4">
                          <div>
                            <p className="font-medium">{draft.name}</p>
                            <p className="mt-1 text-sm text-muted-foreground">{draft.description}</p>
                          </div>
                          <Button size="sm" onClick={() => handleCreateDraft(draft)} disabled={createPolicyTemplate.isPending}>
                            Save Draft
                          </Button>
                        </div>
                        <div className="mt-3 flex flex-wrap gap-2">
                          <Badge variant="secondary">{draft.appliesTo}</Badge>
                          <Badge variant="secondary">{draft.requiredApprovals} approval(s)</Badge>
                          {draft.requiredApproverRole && <Badge variant="secondary">role: {draft.requiredApproverRole}</Badge>}
                          {draft.allowedHoursUtc && <Badge variant="secondary">{draft.allowedHoursUtc} UTC</Badge>}
                          {draft.autoApprovePattern && <Badge variant="secondary">{draft.autoApprovePattern}</Badge>}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="sprint4" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <GitPullRequest className="h-4 w-4 text-violet-400" /> PR-to-Preview QA Agent
              </CardTitle>
              <CardDescription>
                Generate a concrete QA checklist and PR-ready comment body for preview or ephemeral environments.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-3 lg:grid-cols-2 xl:grid-cols-4">
                {projects.length > 0 && (
                  <ProjectSelect value={selectedProjectId} onChange={setSelectedProjectId} projects={projects.map((p) => ({ id: p.id, name: p.name }))} />
                )}
                <Input value={previewBranch} onChange={(e) => setPreviewBranch(e.target.value)} placeholder="Branch" />
                <Input value={previewTitle} onChange={(e) => setPreviewTitle(e.target.value)} placeholder="PR title" />
                <Button onClick={handleGeneratePreviewQa} disabled={previewQa.isPending || !selectedProjectId} className="gap-2">
                  {previewQa.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Activity className="h-4 w-4" />}
                  Generate QA Plan
                </Button>
              </div>
              <textarea
                value={previewSummary}
                onChange={(e) => setPreviewSummary(e.target.value)}
                rows={4}
                className="w-full rounded-2xl border border-border/60 bg-background px-4 py-3 text-sm outline-none focus:ring-2 focus:ring-violet-500/30"
                placeholder="Change summary"
              />

              {previewData && (
                <div className="grid gap-6 xl:grid-cols-[1.1fr_1fr]">
                  <div className="space-y-3">
                    <div className="rounded-2xl border border-violet-500/20 bg-violet-500/5 p-4">
                      <p className="font-medium">{previewData.projectName}</p>
                      <p className="mt-1 text-sm text-muted-foreground">{previewData.previewStrategy}</p>
                      <div className="mt-3 flex flex-wrap gap-2">
                        {previewData.focusAreas.map((focus) => <Badge key={focus} variant="secondary">{focus}</Badge>)}
                      </div>
                    </div>
                    {previewData.checks.map((check) => (
                      <div key={`${check.area}-${check.step}`} className="rounded-2xl border border-border/60 bg-muted/20 p-4">
                        <div className="flex items-center justify-between gap-3">
                          <p className="font-medium">{check.area}</p>
                          <Badge variant="outline" className="uppercase">{check.priority}</Badge>
                        </div>
                        <p className="mt-2 text-sm">{check.step}</p>
                        <p className="mt-2 text-xs text-muted-foreground">{check.rationale}</p>
                      </div>
                    ))}
                  </div>
                  <Card className="border-border/60 bg-background/90">
                    <CardHeader>
                      <CardTitle className="text-base">PR Comment Body</CardTitle>
                      <CardDescription>Copy this directly into GitHub or GitLab review comments.</CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      <button
                        onClick={() => handleCopy(previewData.commentBody, "PR comment")}
                        className="flex w-full items-center justify-between rounded-xl border border-dashed border-border/60 bg-black/[0.12] px-3 py-2 text-left text-xs font-medium text-zinc-300 transition-colors hover:bg-black/[0.18]"
                      >
                        Copy comment body
                        <Copy className="h-3.5 w-3.5" />
                      </button>
                      <pre className="overflow-x-auto whitespace-pre-wrap rounded-2xl border border-border/60 bg-muted/20 p-4 text-xs leading-6 text-zinc-200">
                        {previewData.commentBody}
                      </pre>
                    </CardContent>
                  </Card>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="sprint5" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <Bot className="h-4 w-4 text-cyan-400" /> Ops Memory Graph
              </CardTitle>
              <CardDescription>
                Surface recurring failure patterns so the agent improves future guidance instead of starting from zero every time.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="rounded-2xl border border-cyan-500/20 bg-cyan-500/5 p-4 text-sm text-zinc-200">
                {opsMemory.data?.summary ?? "Loading recent patterns..."}
              </div>
              {opsMemory.isLoading && <Skeleton className="h-72 rounded-2xl" />}
              <div className="grid gap-3 xl:grid-cols-2">
                {opsMemoryItems.map((item) => (
                  <div key={`${item.projectId}-${item.pattern}`} className="rounded-2xl border border-border/60 bg-muted/20 p-4">
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <p className="font-medium">{item.projectName}</p>
                        <p className="mt-1 text-sm text-muted-foreground">{item.pattern}</p>
                      </div>
                      <SeverityBadge value={item.severity} />
                    </div>
                    <div className="mt-3 flex flex-wrap gap-2">
                      <Badge variant="secondary">{item.occurrences} occurrences</Badge>
                      <Badge variant="secondary">Last seen {new Date(item.lastSeenAt).toLocaleDateString()}</Badge>
                    </div>
                    <p className="mt-3 text-sm">{item.recommendedFocus}</p>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="assistant" className="space-y-6">
          <Card className="overflow-hidden">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <Bot className="h-4 w-4 text-amber-400" /> Assistant Chat
              </CardTitle>
              <CardDescription>
                Conversational guidance with deployment context attached when available.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <ScrollArea className="h-[420px] rounded-2xl border border-border/60 bg-muted/20 p-4">
                <div className="space-y-3">
                  {chatMessages.map((message) => (
                    <div key={message.id} className={cn("flex", message.role === "user" ? "justify-end" : "justify-start")}>
                      <div className={cn(
                        "max-w-[82%] rounded-2xl px-4 py-3 text-sm leading-6",
                        message.role === "user"
                          ? "bg-amber-500 text-black"
                          : "border border-border/60 bg-background text-foreground"
                      )}>
                        {message.content}
                      </div>
                    </div>
                  ))}
                  {aiChat.isPending && (
                    <div className="flex justify-start">
                      <div className="rounded-2xl border border-border/60 bg-background px-4 py-3 text-sm text-muted-foreground">
                        <Loader2 className="h-4 w-4 animate-spin" />
                      </div>
                    </div>
                  )}
                </div>
              </ScrollArea>
              <div className="flex gap-3">
                <Input
                  value={chatInput}
                  onChange={(e) => setChatInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" && !e.shiftKey) {
                      e.preventDefault();
                      handleSendChat();
                    }
                  }}
                  placeholder="Ask about rollout strategy, RCA, rollback, or infra posture"
                  disabled={aiChat.isPending}
                />
                <Button onClick={handleSendChat} disabled={aiChat.isPending || !chatInput.trim()} className="gap-2">
                  {aiChat.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
                  Send
                </Button>
              </div>
              <div className="flex flex-wrap gap-2">
                {[
                  "Why did this deployment fail?",
                  "What rollout mode should I use for production?",
                  "How should I reduce deployment risk this sprint?",
                ].map((prompt) => (
                  <Button key={prompt} variant="outline" size="sm" onClick={() => setChatInput(prompt)}>
                    {prompt}
                  </Button>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {!projects.length && (
        <Card className="border-amber-500/20 bg-amber-500/5">
          <CardContent className="flex items-start gap-3 p-4 text-sm text-amber-100">
            <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0" />
            Create at least one project to activate the risk twin, policy agent, preview QA, and pipeline architect workflows.
          </CardContent>
        </Card>
      )}

      {incidentData && (
        <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
          <button className="inline-flex items-center gap-1 rounded-full border border-border/60 px-3 py-1.5 hover:bg-muted/40" onClick={() => handleCopy(incidentData.summary, "Incident summary")}>
            <Copy className="h-3 w-3" /> Copy summary
          </button>
          <button className="inline-flex items-center gap-1 rounded-full border border-border/60 px-3 py-1.5 hover:bg-muted/40" onClick={() => router.push(`/deployments/${incidentData.deploymentId}`)}>
            <CheckCircle2 className="h-3 w-3" /> Open deployment
          </button>
        </div>
      )}
    </div>
  );
}
