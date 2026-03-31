"use client";

import React, { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  CheckCircle2,
  GitBranch,
  Rocket,
  Settings2,
  Server,
  Zap,
  ChevronRight,
  ChevronLeft,
  Code2,
  Loader2,
  Download,
  Upload,
  ExternalLink,
  AlertCircle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  useDetectStack,
  useApplyStack,
  useCreateDeployment,
  type DetectedStackDto,
} from "@/hooks/use-api";
import { useProjects, useServers } from "@/hooks/use-api";
import { useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api-client";
import type { Deployment, DeploymentStatus } from "@/types";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

const steps = [
  { id: 1, label: "Connect Repo", icon: GitBranch },
  { id: 2, label: "Detect Stack", icon: Code2 },
  { id: 3, label: "Environment", icon: Settings2 },
  { id: 4, label: "Choose Server", icon: Server },
  { id: 5, label: "Deploy!", icon: Rocket },
];

const FRAMEWORK_COLORS: Record<string, string> = {
  Nextjs: "bg-black text-white",
  React: "bg-blue-500 text-white",
  Vue: "bg-green-500 text-white",
  DotnetWebApi: "bg-purple-600 text-white",
  FastApi: "bg-teal-500 text-white",
  Django: "bg-green-700 text-white",
  Go: "bg-cyan-500 text-white",
  Static: "bg-gray-500 text-white",
  Nodejs: "bg-green-600 text-white",
  Express: "bg-gray-700 text-white",
  Nestjs: "bg-red-600 text-white",
};

/** Parse owner/repo from a GitHub or GitLab URL */
function parseGitRepo(url: string): { host: "github" | "gitlab" | null; owner: string; repo: string } | null {
  try {
    const u = new URL(url.replace(/\.git$/, ""));
    const parts = u.pathname.replace(/^\//, "").split("/");
    if (parts.length < 2) return null;
    const [owner, repo] = parts;
    if (u.hostname === "github.com") return { host: "github", owner, repo };
    if (u.hostname === "gitlab.com") return { host: "gitlab", owner, repo };
    return null;
  } catch {
    return null;
  }
}

export default function OnboardingPage() {
  const [currentStep, setCurrentStep] = useState(1);
  const [maxVisitedStep, setMaxVisitedStep] = useState(1);
  const [repoUrl, setRepoUrl] = useState("");
  const [fileList, setFileList] = useState("");
  const [packageJson, setPackageJson] = useState("");
  const [detectedStack, setDetectedStack] = useState<DetectedStackDto | null>(null);
  const [selectedProjectId, setSelectedProjectId] = useState("");
  const [selectedServerId, setSelectedServerId] = useState("");
  const [envVars, setEnvVars] = useState<Record<string, string>>({});
  const [fetching, setFetching] = useState(false);
  const [deploymentId, setDeploymentId] = useState<string | null>(null);
  const [deploymentData, setDeploymentData] = useState<Deployment | null>(null);

  const detectStack = useDetectStack();
  const createDeployment = useCreateDeployment();
  const queryClient = useQueryClient();
  const { data: projects } = useProjects();
  const { data: servers } = useServers();

  function goToStep(step: number) {
    setCurrentStep(step);
    setMaxVisitedStep((prev) => Math.max(prev, step));
  }

  /** Fetch file list from GitHub/GitLab API and auto-fill the textarea */
  async function handleFetchFiles() {
    const parsed = parseGitRepo(repoUrl);
    if (!parsed) {
      toast.error("Only github.com and gitlab.com URLs are supported for auto-fetch.");
      return;
    }
    setFetching(true);
    try {
      if (parsed.host === "github") {
        const res = await fetch(`https://api.github.com/repos/${parsed.owner}/${parsed.repo}/contents/`, {
          headers: { Accept: "application/vnd.github+json" },
        });
        if (!res.ok) throw new Error(res.status === 404 ? "Repository not found or private." : `GitHub API error ${res.status}`);
        const items: { name: string; type: string }[] = await res.json();
        const names = items.map((i) => i.type === "dir" ? `${i.name}/` : i.name);
        setFileList(names.join("\n"));

        // Try to fetch package.json content for better detection
        const pkgRes = await fetch(`https://raw.githubusercontent.com/${parsed.owner}/${parsed.repo}/HEAD/package.json`);
        if (pkgRes.ok) setPackageJson(await pkgRes.text());

        toast.success(`Fetched ${names.length} files from GitHub`);
      } else {
        // GitLab
        const encodedPath = encodeURIComponent(`${parsed.owner}/${parsed.repo}`);
        const res = await fetch(`https://gitlab.com/api/v4/projects/${encodedPath}/repository/tree`);
        if (!res.ok) throw new Error("GitLab API error — repository may be private.");
        const items: { name: string; type: string }[] = await res.json();
        setFileList(items.map((i) => i.type === "tree" ? `${i.name}/` : i.name).join("\n"));
        toast.success(`Fetched ${items.length} files from GitLab`);
      }
    } catch (err: any) {
      toast.error(err.message ?? "Failed to fetch repository contents");
    } finally {
      setFetching(false);
    }
  }

  function handleDetect() {
    const fileNames = fileList
      .split("\n")
      .map((f) => f.trim())
      .filter(Boolean);
    if (!fileNames.length) {
      toast.error("Enter at least one filename");
      return;
    }
    detectStack.mutate(
      { fileNames, packageJsonContent: packageJson || undefined },
      {
        onSuccess: (stack) => {
          setDetectedStack(stack);
          // Pre-populate suggested env vars
          const initial: Record<string, string> = {};
          (stack.suggestedEnvVars ?? []).forEach((k) => {
            initial[k] = "";
          });
          setEnvVars(initial);
          goToStep(3);
          toast.success(`Detected: ${stack.framework}`);
        },
        onError: () => toast.error("Detection failed"),
      }
    );
  }

  const applyStack = useApplyStack(selectedProjectId);
  function handleApply() {
    if (!selectedProjectId || !detectedStack) return;
    applyStack.mutate(
      { framework: detectedStack.framework },
      {
        onSuccess: () => {
          toast.success("Stack settings applied to project");
          createDeployment.mutate(
            { projectId: selectedProjectId, trigger: "manual" },
            {
              onSuccess: (deployment) => {
                setDeploymentId((deployment as any).id ?? (deployment as any).deploymentId ?? null);
                goToStep(5);
                toast.success("Deployment triggered!");
              },
              onError: () => {
                goToStep(5);
                toast.warning("Stack applied — but deployment could not be triggered automatically.");
              },
            }
          );
        },
        onError: () => toast.error("Failed to apply stack"),
      }
    );
  }

  // Poll deployment status on step 5 until a terminal state is reached
  const TERMINAL: DeploymentStatus[] = ["running", "healthy", "unhealthy", "failed", "cancelled", "stopped", "rolled_back"];
  React.useEffect(() => {
    if (!deploymentId || currentStep !== 5) return;
    let cancelled = false;
    const poll = async () => {
      try {
        const dto = await apiClient.get<any>(`/deployments/${deploymentId}`);
        if (!cancelled) setDeploymentData(dto as Deployment);
        if (TERMINAL.includes((dto.status as string).replace(/([a-z])([A-Z])/g, "$1_$2").toLowerCase() as DeploymentStatus)) {
          clearInterval(handle);
        }
      } catch { /* ignore */ }
    };
    poll();
    const handle = setInterval(poll, 3000);
    return () => { cancelled = true; clearInterval(handle); };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [deploymentId, currentStep]);

  const progressPercent = ((currentStep - 1) / (steps.length - 1)) * 100;

  return (
    <div className="max-w-3xl mx-auto py-10 px-4 space-y-8">
      {/* Header */}
      <div className="text-center space-y-2">
        <h1 className="text-3xl font-bold">Welcome to DeployFlow</h1>
        <p className="text-muted-foreground">
          Let&apos;s get your first project deployed in minutes.
        </p>
      </div>

      {/* Step Indicator */}
      <div className="relative flex items-center justify-between px-2">
        <div className="absolute left-0 right-0 top-1/2 h-0.5 bg-muted -translate-y-1/2" />
        <div
          className="absolute left-0 top-1/2 h-0.5 bg-primary -translate-y-1/2 transition-all duration-500"
          style={{ width: `${progressPercent}%` }}
        />
        {steps.map((step) => {
          const Icon = step.icon;
          const done = currentStep > step.id;
          const active = currentStep === step.id;
          const visited = step.id <= maxVisitedStep;
          return (
            <div key={step.id} className="relative z-10 flex flex-col items-center gap-1">
              <button
                onClick={() => visited && setCurrentStep(step.id)}
                className={cn(
                  "w-10 h-10 rounded-full flex items-center justify-center border-2 transition-all",
                  done && "bg-primary border-primary text-primary-foreground cursor-pointer",
                  active && "bg-background border-primary text-primary",
                  !done && !active && visited && "bg-primary/10 border-primary/40 text-primary cursor-pointer",
                  !visited && "bg-muted border-muted-foreground/30 text-muted-foreground cursor-default"
                )}
              >
                {done ? <CheckCircle2 className="w-5 h-5" /> : <Icon className="w-4 h-4" />}
              </button>
              <span
                className={cn(
                  "text-xs font-medium hidden sm:block",
                  active ? "text-primary" : "text-muted-foreground"
                )}
              >
                {step.label}
              </span>
            </div>
          );
        })}
      </div>

      {/* Step Content */}
      <AnimatePresence mode="wait">
        <motion.div
          key={currentStep}
          initial={{ opacity: 0, x: 30 }}
          animate={{ opacity: 1, x: 0 }}
          exit={{ opacity: 0, x: -30 }}
          transition={{ duration: 0.2 }}
        >
          {/* Step 1 – Connect Repo */}
          {currentStep === 1 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <GitBranch className="w-5 h-5" /> Connect your repository
                </CardTitle>
                <CardDescription>Enter your git repository URL to get started.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="repo">Repository URL</Label>
                  <Input
                    id="repo"
                    placeholder="https://github.com/org/repo"
                    value={repoUrl}
                    onChange={(e) => setRepoUrl(e.target.value)}
                  />
                </div>
                <Button
                  className="w-full"
                  disabled={!repoUrl}
                  onClick={async () => {
                    goToStep(2);
                    // Auto-fetch if it's a public GitHub/GitLab repo
                    if (parseGitRepo(repoUrl)) {
                      await handleFetchFiles();
                    }
                  }}
                >
                  Continue <ChevronRight className="ml-2 w-4 h-4" />
                </Button>
              </CardContent>
            </Card>
          )}

          {/* Step 2 – Detect Stack */}
          {currentStep === 2 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Code2 className="w-5 h-5" /> Auto-detect your stack
                </CardTitle>
                <CardDescription>
                  {parseGitRepo(repoUrl)
                    ? "Files were auto-fetched from your repository. Click Detect Stack to identify your framework."
                    : "List the files in your project root (one per line). DeployFlow will auto-generate a production Dockerfile."}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <Label>Root files (one per line)</Label>
                    {parseGitRepo(repoUrl) && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="h-7 text-xs gap-1"
                        disabled={fetching}
                        onClick={handleFetchFiles}
                      >
                        {fetching ? <Loader2 className="h-3 w-3 animate-spin" /> : <Download className="h-3 w-3" />}
                        Re-fetch files
                      </Button>
                    )}
                  </div>
                  <Textarea
                    placeholder={"package.json\nnext.config.js\ntsconfig.json\npublic/"}
                    rows={5}
                    value={fileList}
                    onChange={(e) => setFileList(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>package.json contents (optional)</Label>
                  <Textarea
                    placeholder='{"dependencies":{"next":"14.0.0"}}'
                    rows={3}
                    value={packageJson}
                    onChange={(e) => setPackageJson(e.target.value)}
                    className="font-mono text-xs"
                  />
                </div>
                <div className="flex gap-2">
                  <Button variant="outline" onClick={() => setCurrentStep(1)}>
                    <ChevronLeft className="mr-2 w-4 h-4" /> Back
                  </Button>
                  <Button
                    className="flex-1"
                    onClick={handleDetect}
                    disabled={detectStack.isPending}
                  >
                    {detectStack.isPending ? (
                      <Loader2 className="mr-2 w-4 h-4 animate-spin" />
                    ) : (
                      <Zap className="mr-2 w-4 h-4" />
                    )}
                    Detect Stack
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Step 3 – Environment Variables */}
          {currentStep === 3 && detectedStack && (
            <Card>
              <CardHeader>
                <div className="flex items-start justify-between">
                  <div>
                    <CardTitle className="flex items-center gap-2">
                      <Settings2 className="w-5 h-5" /> Configure environment
                    </CardTitle>
                    <CardDescription className="mt-1">{detectedStack.explanation}</CardDescription>
                  </div>
                  <Badge
                    className={FRAMEWORK_COLORS[detectedStack.framework] ?? "bg-muted text-foreground"}
                  >
                    {detectedStack.framework}
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                {detectedStack.suggestedEnvVars?.length > 0 && (
                  <div className="space-y-3">
                    <p className="text-sm font-medium text-muted-foreground">
                      Suggested environment variables
                    </p>
                    {detectedStack.suggestedEnvVars.map((key) => (
                      <div key={key} className="flex items-center gap-2">
                        <Label className="w-56 shrink-0 font-mono text-xs">{key}</Label>
                        <Input
                          placeholder="value"
                          value={envVars[key] ?? ""}
                          onChange={(e) =>
                            setEnvVars((prev) => ({ ...prev, [key]: e.target.value }))
                          }
                        />
                      </div>
                    ))}
                  </div>
                )}
                <details className="text-xs">
                  <summary className="cursor-pointer text-muted-foreground hover:text-foreground">
                    View generated Dockerfile
                  </summary>
                  <pre className="mt-2 p-3 bg-muted rounded text-xs overflow-auto max-h-64 whitespace-pre-wrap">
                    {detectedStack.dockerfileContent}
                  </pre>
                </details>
                <div className="flex gap-2">
                  <Button variant="outline" onClick={() => setCurrentStep(2)}>
                    <ChevronLeft className="mr-2 w-4 h-4" /> Back
                  </Button>
                  <Button className="flex-1" onClick={() => goToStep(4)}>
                    Continue <ChevronRight className="ml-2 w-4 h-4" />
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Step 4 – Choose Server and Link Project */}
          {currentStep === 4 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Server className="w-5 h-5" /> Link to project &amp; server
                </CardTitle>
                <CardDescription>
                  Select an existing project to apply the detected stack settings.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label>Project</Label>
                  <div className="grid gap-2">
                    {(projects?.data ?? []).slice(0, 8).map((p) => (
                      <button
                        key={p.id}
                        onClick={() => setSelectedProjectId(p.id)}
                        className={cn(
                          "text-left px-4 py-3 rounded-lg border transition-all text-sm",
                          selectedProjectId === p.id
                            ? "border-primary bg-primary/5"
                            : "border-border hover:border-primary/50"
                        )}
                      >
                        <span className="font-medium">{p.name}</span>
                        {p.repositoryUrl && (
                          <span className="ml-2 text-muted-foreground text-xs truncate">
                            {p.repositoryUrl}
                          </span>
                        )}
                      </button>
                    ))}
                  </div>
                </div>
                {(servers ?? []).length > 0 && (
                  <div className="space-y-2">
                    <Label>Server <span className="text-muted-foreground font-normal">(optional — uses project default if not selected)</span></Label>
                    <div className="grid gap-2">
                      {(servers ?? []).slice(0, 6).map((s: any) => (
                        <button
                          key={s.id}
                          onClick={() => setSelectedServerId(selectedServerId === s.id ? "" : s.id)}
                          className={cn(
                            "text-left px-4 py-3 rounded-lg border transition-all text-sm flex items-center gap-3",
                            selectedServerId === s.id
                              ? "border-primary bg-primary/5"
                              : "border-border hover:border-primary/50"
                          )}
                        >
                          <Server className="w-4 h-4 shrink-0 text-muted-foreground" />
                          <div>
                            <span className="font-medium">{s.name}</span>
                            {s.ipAddress && (
                              <span className="ml-2 text-muted-foreground text-xs">{s.ipAddress}</span>
                            )}
                          </div>
                        </button>
                      ))}
                    </div>
                  </div>
                )}
                <div className="flex gap-2">
                  <Button variant="outline" onClick={() => setCurrentStep(3)}>
                    <ChevronLeft className="mr-2 w-4 h-4" /> Back
                  </Button>
                  <Button
                    className="flex-1"
                    disabled={!selectedProjectId || applyStack.isPending || createDeployment.isPending}
                    onClick={handleApply}
                  >
                    {(applyStack.isPending || createDeployment.isPending) ? (
                      <Loader2 className="mr-2 w-4 h-4 animate-spin" />
                    ) : (
                      <Upload className="mr-2 w-4 h-4" />
                    )}
                    Apply &amp; Deploy
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Step 5 – Deployment Status */}
          {currentStep === 5 && (
            <Card className="text-center">
              <CardContent className="py-12 space-y-5">
                {(() => {
                  const status = (deploymentData as any)?.status as string | undefined;
                  const url = (deploymentData as any)?.url as string | undefined;
                  const isTerminal = status && ["running", "healthy", "unhealthy", "failed", "cancelled", "stopped", "rolled_back"].includes(status);
                  const isFailed = status && ["failed", "cancelled", "stopped", "rolled_back", "unhealthy"].includes(status);
                  const isSuccess = status && ["running", "healthy"].includes(status);

                  return (
                    <>
                      <div className="flex justify-center">
                        <div className={cn(
                          "w-20 h-20 rounded-full flex items-center justify-center",
                          isSuccess ? "bg-green-100 dark:bg-green-900/30" :
                          isFailed ? "bg-red-100 dark:bg-red-900/30" :
                          "bg-blue-100 dark:bg-blue-900/30 animate-pulse"
                        )}>
                          {isSuccess ? (
                            <Rocket className="w-10 h-10 text-green-600 dark:text-green-400" />
                          ) : isFailed ? (
                            <AlertCircle className="w-10 h-10 text-red-500" />
                          ) : (
                            <Loader2 className="w-10 h-10 text-blue-500 animate-spin" />
                          )}
                        </div>
                      </div>

                      <h2 className="text-2xl font-bold">
                        {isSuccess ? "You're all set!" : isFailed ? "Deployment failed" : deploymentId ? "Deploying…" : "Deployment triggered!"}
                      </h2>

                      <p className="text-muted-foreground max-w-sm mx-auto">
                        {isSuccess
                          ? "Your app is live and running."
                          : isFailed
                          ? "The deployment encountered an error. Check the logs for details."
                          : deploymentId
                          ? "Building and deploying your app. This usually takes 1–3 minutes."
                          : "Stack settings applied. Head to your project to monitor the deployment."}
                      </p>

                      {status && (
                        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full text-xs font-medium bg-muted text-muted-foreground">
                          {!isTerminal && <Loader2 className="w-3 h-3 animate-spin" />}
                          {isSuccess && <CheckCircle2 className="w-3 h-3 text-green-500" />}
                          {isFailed && <AlertCircle className="w-3 h-3 text-red-500" />}
                          Status: <span className="capitalize">{status.replace(/_/g, " ")}</span>
                        </div>
                      )}

                      {url && (
                        <div className="pt-2">
                          <a
                            href={url}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="inline-flex items-center gap-2 px-4 py-2 rounded-lg bg-green-600 hover:bg-green-700 text-white text-sm font-medium transition-colors"
                          >
                            <ExternalLink className="w-4 h-4" />
                            Open your app
                          </a>
                          <p className="text-xs text-muted-foreground mt-2">{url}</p>
                        </div>
                      )}

                      <div className="flex justify-center gap-3 pt-2">
                        <Button variant="outline" onClick={() => setCurrentStep(1)}>
                          Start over
                        </Button>
                        {deploymentId ? (
                          <Button asChild>
                            <a href={`/deployments/${deploymentId}`}>View Deployment</a>
                          </Button>
                        ) : (
                          <Button asChild>
                            <a href="/projects">Go to Projects</a>
                          </Button>
                        )}
                      </div>
                    </>
                  );
                })()}
              </CardContent>
            </Card>
          )}
        </motion.div>
      </AnimatePresence>
    </div>
  );
}
