"use client";

import React, { useState, useEffect, useRef } from "react";
import * as PopoverPrimitive from "@radix-ui/react-popover";
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
  Sparkles,
  ChevronsUpDown,
  Search,
  RefreshCw,
  Info,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import {
  useDetectStack,
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

/** Fetch public branch list from GitHub or GitLab */
async function fetchRepoBranches(repoUrl: string): Promise<string[]> {
  const githubMatch = repoUrl.match(/github\.com\/([^/]+)\/([^/?.#]+)/);
  const gitlabMatch = repoUrl.match(/gitlab\.com\/([^/]+(?:\/[^/]+)*)\/([^/?.#]+)/);

  if (githubMatch) {
    const [, owner, repo] = githubMatch;
    const res = await fetch(
      `https://api.github.com/repos/${owner}/${repo.replace(/\.git$/, "")}/branches?per_page=100`,
      { headers: { Accept: "application/vnd.github+json" } },
    );
    if (!res.ok) throw new Error("Could not fetch branches — repository may be private.");
    const data = await res.json();
    return (data as any[]).map((b) => b.name as string);
  }

  if (gitlabMatch) {
    const [, namespace, repo] = gitlabMatch;
    const encoded = encodeURIComponent(`${namespace}/${repo.replace(/\.git$/, "")}`);
    const res = await fetch(`https://gitlab.com/api/v4/projects/${encoded}/repository/branches?per_page=100`);
    if (!res.ok) throw new Error("Could not fetch branches — repository may be private.");
    const data = await res.json();
    return (data as any[]).map((b) => b.name as string);
  }

  return [];
}

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

/** Derive a project name from a repo URL, e.g. https://github.com/org/my-app.git → "my-app" */
function getRepoName(url: string): string {
  try {
    const clean = url.replace(/\.git$/, "").replace(/\/$/, "");
    const parts = new URL(clean).pathname.split("/").filter(Boolean);
    return parts[parts.length - 1] ?? "new-project";
  } catch {
    return "new-project";
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
  const [autoDetectedProjectId, setAutoDetectedProjectId] = useState<string | null>(null);
  const [branch, setBranch] = useState("main");
  const [projectName, setProjectName] = useState("");
  const [branches, setBranches] = useState<string[]>([]);
  const [fetchingBranches, setFetchingBranches] = useState(false);
  const branchFetchTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [repoUrlError, setRepoUrlError] = useState("");
  const [envVars, setEnvVars] = useState<Record<string, string>>({});
  const [fetching, setFetching] = useState(false);
  const [deploymentId, setDeploymentId] = useState<string | null>(null);
  const [deploymentData, setDeploymentData] = useState<Deployment | null>(null);
  const [isAutoCreating, setIsAutoCreating] = useState(false);
  const [deploymentEnvironments, setDeploymentEnvironments] = useState([
    { environmentName: "Dev", branch: "dev", autoDeploy: true, requiresApproval: false, isDefault: false, order: 1 },
    { environmentName: "QA", branch: "staging", autoDeploy: true, requiresApproval: false, isDefault: false, order: 2 },
    { environmentName: "Prod", branch: "main", autoDeploy: true, requiresApproval: true, isDefault: true, order: 3 },
  ]);

  const detectStack = useDetectStack();
  const createDeployment = useCreateDeployment();
  const queryClient = useQueryClient();
  const { data: projects } = useProjects();
  const { data: servers } = useServers();

  // Auto-fetch branches when a valid GitHub/GitLab URL is entered (debounced)
  useEffect(() => {
    if (branchFetchTimerRef.current) clearTimeout(branchFetchTimerRef.current);
    if (!repoUrl || !parseGitRepo(repoUrl)) {
      setBranches([]);
      return;
    }
    setBranches([]);
    branchFetchTimerRef.current = setTimeout(async () => {
      setFetchingBranches(true);
      try {
        const result = await fetchRepoBranches(repoUrl);
        setBranches(result);
        if (result.length > 0) {
          const defaultBranch = result.includes("main") ? "main" : result[0];
          setBranch(defaultBranch);
        }
      } catch {
        setBranches([]);
      } finally {
        setFetchingBranches(false);
      }
    }, 800);
    return () => { if (branchFetchTimerRef.current) clearTimeout(branchFetchTimerRef.current); };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [repoUrl]);

  // Auto-detect project from repo URL
  useEffect(() => {
    if (!repoUrl || !projects?.data?.length) return;
    const normalize = (u: string) => u.replace(/\.git$/, "").replace(/\/$/, "").toLowerCase();
    const normalizedRepo = normalize(repoUrl);
    const match = projects.data.find(
      (p) => p.repositoryUrl && normalize(p.repositoryUrl) === normalizedRepo
    );
    if (match) {
      setAutoDetectedProjectId(match.id);
      setSelectedProjectId(match.id);
    } else {
      setAutoDetectedProjectId(null);
    }
  }, [repoUrl, projects?.data]);

  useEffect(() => {
    if (!projectName && repoUrl) {
      setProjectName(getRepoName(repoUrl));
    }
  }, [projectName, repoUrl]);

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

  async function handleApplyWithCreate() {
    if (!detectedStack) return;
    let projectId = selectedProjectId;
    setIsAutoCreating(true);
    try {
      // Auto-create project from repo URL if no project is selected
      if (!projectId) {
        if (!repoUrl) { toast.error("No repository URL — go back to step 1."); return; }
        const finalProjectName = projectName.trim() || getRepoName(repoUrl);
        const created = await apiClient.post<{ id: string }>("/projects", {
          name: finalProjectName,
          description: `Auto-created via onboarding from ${repoUrl}`,
          repositoryUrl: repoUrl,
          branch: branch,
          buildCommand: detectedStack.buildCommand,
          startCommand: detectedStack.startCommand,
          installCommand: detectedStack.installCommand || null,
          dockerfilePath: null,
          framework: detectedStack.framework,
          customDomain: null,
          autoDeploy: true,
          tags: [],
          assignedServerId: selectedServerId || null,
        });
        projectId = (created as any).id;
        setSelectedProjectId(projectId!);
        await queryClient.invalidateQueries({ queryKey: ["projects"] });
        toast.success(`Project "${finalProjectName}" created!`);
      }

      // Persist the selected branch/server even when the deployment-environments API is unavailable.
      if (selectedServerId || branch) {
        await apiClient.put(`/projects/${projectId}`, {
          branch,
          assignedServerId: selectedServerId || null,
        });
      }

      // Save environment-to-branch routing for multi-branch deployments
      try {
        await apiClient.put(`/projects/${projectId}/deployment-environments/bulk`, {
          items: deploymentEnvironments
            .filter((x) => x.environmentName.trim() && x.branch.trim())
            .map((x, idx) => ({
              environmentName: x.environmentName.trim(),
              branch: x.branch.trim(),
              autoDeploy: x.autoDeploy,
              requiresApproval: x.requiresApproval,
              isDefault: x.isDefault,
              order: idx + 1,
            })),
        });
      } catch (e: any) {
        if (!String(e?.message ?? "").includes("404")) {
          throw e;
        }

        toast.warning("Branch routing API is unavailable on the current backend. Continuing with project defaults.");
      }

      // Apply detected stack settings to the project
      await apiClient.post(`/stack-detection/apply/${projectId}`, { framework: detectedStack.framework });

      // Auto-create and scaffold a smart default pipeline based on detected stack
      const pipeline = await apiClient.post<any>("/pipelines", {
        name: `${(projectName.trim() || getRepoName(repoUrl))} default pipeline`,
        description: "Auto-generated during onboarding",
        projectId,
        trigger: "push",
        cronExpression: null,
      });

      await apiClient.post(`/pipelines/${pipeline.id}/scaffold`, {
        projectName: projectName.trim() || getRepoName(repoUrl),
        repoFiles: fileList.split("\n").map((x) => x.trim()).filter(Boolean),
        packageJsonContent: packageJson || undefined,
        branch,
      });

      // Initialize project webhooks so branch-based auto deploy can be wired immediately.
      await apiClient.get(`/projects/${projectId}/webhooks`);

      toast.success("Stack settings applied.");
    } catch (e: any) {
      toast.error(e?.message ?? "Operation failed");
      setIsAutoCreating(false);
      return;
    }
    setIsAutoCreating(false);
    // Trigger the deployment
    createDeployment.mutate(
      { projectId: projectId!, trigger: "manual" },
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
                  <div className="flex items-center gap-1.5">
                    <Label htmlFor="repo">Repository URL</Label>
                    <TooltipProvider delayDuration={200}>
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <Info className="h-3.5 w-3.5 text-muted-foreground cursor-help" />
                        </TooltipTrigger>
                        <TooltipContent className="max-w-xs space-y-1">
                          <p className="font-semibold">Supported formats</p>
                          <p className="font-mono text-[11px]">https://github.com/owner/repo</p>
                          <p className="font-mono text-[11px]">https://github.com/owner/repo.git</p>
                          <p className="font-mono text-[11px]">https://gitlab.com/owner/repo</p>
                          <p className="mt-1 text-[11px] text-primary-foreground/70">Private repos require manual file list</p>
                        </TooltipContent>
                      </Tooltip>
                    </TooltipProvider>
                  </div>
                  <Input
                    id="repo"
                    placeholder="https://github.com/owner/repo"
                    value={repoUrl}
                    onChange={(e) => {
                      setRepoUrl(e.target.value);
                      setRepoUrlError("");
                    }}
                    className={repoUrlError ? "border-destructive" : ""}
                  />
                  {repoUrlError && (
                    <p className="text-xs text-destructive">{repoUrlError}</p>
                  )}
                  {repoUrl && !repoUrlError && (
                    <p className="text-xs text-muted-foreground">
                      Project name: <span className="font-semibold text-foreground">{getRepoName(repoUrl)}</span>
                      {!parseGitRepo(repoUrl) && (
                        <span className="ml-1 text-amber-500">(manual file list required — private or non-GitHub URL)</span>
                      )}
                    </p>
                  )}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="projectName">Project Name (optional)</Label>
                  <Input
                    id="projectName"
                    placeholder="my-app"
                    value={projectName}
                    onChange={(e) => setProjectName(e.target.value)}
                  />
                  <p className="text-xs text-muted-foreground">You can override the auto-detected name from repository URL.</p>
                </div>
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <Label htmlFor="branch">Branch</Label>
                    {fetchingBranches && (
                      <span className="flex items-center gap-1 text-xs text-muted-foreground">
                        <Loader2 className="h-3 w-3 animate-spin" />Fetching branches…
                      </span>
                    )}
                    {!fetchingBranches && branches.length > 0 && (
                      <span className="text-xs text-muted-foreground">{branches.length} branch{branches.length !== 1 ? "es" : ""} found</span>
                    )}
                  </div>
                  {branches.length > 0 ? (
                    <div className="flex gap-2">
                      <Select value={branch} onValueChange={setBranch}>
                        <SelectTrigger className="flex-1">
                          <GitBranch className="h-4 w-4 mr-2 text-muted-foreground shrink-0" />
                          <SelectValue placeholder="Select a branch…" />
                        </SelectTrigger>
                        <SelectContent className="max-h-56">
                          {branches.map((b) => (
                            <SelectItem key={b} value={b}>
                              <span className="flex items-center gap-2">
                                <GitBranch className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />{b}
                              </span>
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <Button
                        type="button" variant="outline" size="icon"
                        disabled={fetchingBranches}
                        onClick={async () => {
                          setFetchingBranches(true);
                          try { setBranches(await fetchRepoBranches(repoUrl)); } catch { /* ignore */ }
                          finally { setFetchingBranches(false); }
                        }}
                        title="Refresh branch list"
                      >
                        {fetchingBranches ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
                      </Button>
                    </div>
                  ) : (
                    <div className="relative">
                      <GitBranch className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                      <Input
                        id="branch"
                        placeholder="main"
                        value={branch}
                        onChange={(e) => setBranch(e.target.value || "main")}
                        className="pl-9"
                      />
                    </div>
                  )}
                  {!branches.length && !fetchingBranches && parseGitRepo(repoUrl) && (
                    <p className="text-xs text-muted-foreground">Enter a branch name or wait for auto-detection from public repos.</p>
                  )}
                </div>
                <Button
                  className="w-full"
                  disabled={!repoUrl || fetching}
                  onClick={async () => {
                    // Validate URL format
                    if (repoUrl && !repoUrl.startsWith("http")) {
                      setRepoUrlError("Please enter a full HTTPS URL (e.g. https://github.com/org/repo)");
                      return;
                    }
                    goToStep(2);
                    if (parseGitRepo(repoUrl)) {
                      await handleFetchFiles();
                    }
                  }}
                >
                  {fetching ? (
                    <><Loader2 className="mr-2 w-4 h-4 animate-spin" />Fetching files…</>
                  ) : (
                    <>Continue <ChevronRight className="ml-2 w-4 h-4" /></>
                  )}
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
                    className={FRAMEWORK_COLORS[detectedStack.framework === "Unknown" ? "Nodejs" : detectedStack.framework] ?? "bg-muted text-foreground"}
                  >
                    {detectedStack.framework === "Unknown" ? "Nodejs" : detectedStack.framework}
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-3 rounded-lg border border-primary/20 bg-primary/5 p-3">
                  <p className="text-sm font-medium">Multi-Branch Deployment Environments</p>
                  <p className="text-xs text-muted-foreground">Map each environment to a branch. Webhooks will auto-deploy matching branches.</p>
                  {deploymentEnvironments.map((row, idx) => (
                    <div key={idx} className="grid grid-cols-12 gap-2 items-center">
                      <Input
                        className="col-span-3"
                        placeholder="Environment"
                        value={row.environmentName}
                        onChange={(e) => setDeploymentEnvironments((prev) => prev.map((r, i) => i === idx ? { ...r, environmentName: e.target.value } : r))}
                      />
                      <Input
                        className="col-span-3"
                        placeholder="branch"
                        value={row.branch}
                        onChange={(e) => setDeploymentEnvironments((prev) => prev.map((r, i) => i === idx ? { ...r, branch: e.target.value } : r))}
                      />
                      <label className="col-span-2 text-xs flex items-center gap-1.5">
                        <input
                          type="checkbox"
                          checked={row.autoDeploy}
                          onChange={(e) => setDeploymentEnvironments((prev) => prev.map((r, i) => i === idx ? { ...r, autoDeploy: e.target.checked } : r))}
                        /> Auto
                      </label>
                      <label className="col-span-2 text-xs flex items-center gap-1.5">
                        <input
                          type="checkbox"
                          checked={row.requiresApproval}
                          onChange={(e) => setDeploymentEnvironments((prev) => prev.map((r, i) => i === idx ? { ...r, requiresApproval: e.target.checked } : r))}
                        /> Approval
                      </label>
                      <label className="col-span-2 text-xs flex items-center gap-1.5">
                        <input
                          type="radio"
                          name="default-env"
                          checked={row.isDefault}
                          onChange={() => setDeploymentEnvironments((prev) => prev.map((r, i) => ({ ...r, isDefault: i === idx })))}
                        /> Default
                      </label>
                    </div>
                  ))}
                </div>
                {detectedStack.suggestedEnvVars?.length > 0 && (
                  <div className="space-y-3">
                    <p className="text-sm font-medium text-muted-foreground">
                      Suggested environment variables
                    </p>
                    {detectedStack.suggestedEnvVars.map((key) => (
                      <div key={key} className="space-y-1.5">
                        <Label className="font-mono text-xs break-all">{key}</Label>
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
                  {autoDetectedProjectId
                    ? "Repository matched an existing project — ready to deploy."
                    : selectedProjectId
                    ? "Stack settings will be applied to the selected project."
                    : `No matching project found. A new project \"${getRepoName(repoUrl)}\" will be created automatically.`}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <Label>Project</Label>
                    {autoDetectedProjectId && (
                      <span className="flex items-center gap-1 text-xs text-emerald-600 dark:text-emerald-400 font-medium">
                        <Sparkles className="w-3 h-3" /> Auto-detected from repository URL
                      </span>
                    )}
                  </div>
                  <SearchableSelect
                    items={(projects?.data ?? []).map((p) => ({
                      id: p.id,
                      label: p.name,
                      subtitle: p.repositoryUrl ?? undefined,
                      highlighted: p.id === autoDetectedProjectId,
                    }))}
                    value={selectedProjectId}
                    onSelect={setSelectedProjectId}
                    placeholder="Search and select a project…"
                    emptyMessage="No projects found"
                    icon={<GitBranch className="w-4 h-4" />}
                  />
                </div>

                {/* Auto-create banner — shown when repo URL is set but no project matched */}
                {!selectedProjectId && repoUrl && (
                  <div className="flex items-start gap-2.5 rounded-lg border border-blue-500/30 bg-blue-500/8 px-3.5 py-3">
                    <Sparkles className="w-4 h-4 text-blue-400 shrink-0 mt-0.5" />
                    <div className="space-y-0.5">
                      <p className="text-sm font-medium text-blue-300">Auto-create project</p>
                      <p className="text-xs text-blue-400/80">
                        A new project{" "}
                        <span className="font-mono font-semibold">&quot;{getRepoName(repoUrl)}&quot;</span>{" "}
                        will be created with the{detectedStack ? ` ${detectedStack.framework}` : ""} stack settings
                        from <span className="font-mono">{repoUrl}</span>, then deployed automatically.
                      </p>
                    </div>
                  </div>
                )}

                <div className="space-y-2">
                  <Label>Server <span className="text-muted-foreground font-normal">(optional — uses project default if not selected)</span></Label>
                  <SearchableSelect
                    items={(servers ?? []).map((s: any) => ({
                      id: s.id,
                      label: s.name,
                      subtitle: s.ipAddress ?? undefined,
                    }))}
                    value={selectedServerId}
                    onSelect={(id) => setSelectedServerId(selectedServerId === id ? "" : id)}
                    placeholder="Search and select a server… (optional)"
                    emptyMessage="No servers found"
                    icon={<Server className="w-4 h-4" />}
                    clearable
                    onClear={() => setSelectedServerId("")}
                  />
                </div>
                <div className="flex gap-2">
                  <Button variant="outline" onClick={() => setCurrentStep(3)}>
                    <ChevronLeft className="mr-2 w-4 h-4" /> Back
                  </Button>
                  <Button
                    className="flex-1"
                    disabled={isAutoCreating || createDeployment.isPending || (!selectedProjectId && !repoUrl)}
                    onClick={handleApplyWithCreate}
                  >
                    {(isAutoCreating || createDeployment.isPending) ? (
                      <Loader2 className="mr-2 w-4 h-4 animate-spin" />
                    ) : (
                      <Upload className="mr-2 w-4 h-4" />
                    )}
                    {selectedProjectId ? "Apply & Deploy" : "Create project & Deploy"}
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

// ---------------------------------------------------------------------------
// SearchableSelect – virtualized combobox using Radix Popover + cmdk Command
// Handles 100+ items with type-ahead search and lazy-rendered list
// ---------------------------------------------------------------------------
type SelectItem = {
  id: string;
  label: string;
  subtitle?: string;
  highlighted?: boolean;
};

function SearchableSelect({
  items,
  value,
  onSelect,
  placeholder,
  emptyMessage,
  icon,
  clearable,
  onClear,
}: {
  items: SelectItem[];
  value: string;
  onSelect: (id: string) => void;
  placeholder: string;
  emptyMessage: string;
  icon?: React.ReactNode;
  clearable?: boolean;
  onClear?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);

  const selected = items.find((i) => i.id === value);

  const filtered = React.useMemo(() => {
    if (!query) return items;
    const q = query.toLowerCase();
    return items.filter(
      (i) =>
        i.label.toLowerCase().includes(q) ||
        (i.subtitle ?? "").toLowerCase().includes(q)
    );
  }, [items, query]);

  // Focus search input when popover opens
  useEffect(() => {
    if (open) {
      setTimeout(() => inputRef.current?.focus(), 50);
    } else {
      setQuery("");
    }
  }, [open]);

  return (
    <PopoverPrimitive.Root open={open} onOpenChange={setOpen}>
      <PopoverPrimitive.Trigger asChild>
        <button
          type="button"
          className={cn(
            "w-full flex items-center justify-between gap-2 px-3 py-2.5 rounded-lg border text-sm transition-all",
            "bg-background hover:border-primary/60 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2",
            open ? "border-primary ring-2 ring-ring ring-offset-2" : "border-border"
          )}
          aria-expanded={open}
        >
          <div className="flex items-center gap-2 min-w-0">
            {icon && <span className="text-muted-foreground shrink-0">{icon}</span>}
            {selected ? (
              <div className="flex items-center gap-2 min-w-0">
                <span className="font-medium truncate">{selected.label}</span>
                {selected.highlighted && (
                  <Badge className="h-4 px-1 text-[10px] bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/30">
                    <Sparkles className="w-2.5 h-2.5 mr-0.5" /> Auto
                  </Badge>
                )}
                {selected.subtitle && (
                  <span className="text-muted-foreground text-xs truncate hidden sm:block">
                    {selected.subtitle}
                  </span>
                )}
              </div>
            ) : (
              <span className="text-muted-foreground">{placeholder}</span>
            )}
          </div>
          <ChevronsUpDown className="w-4 h-4 text-muted-foreground shrink-0" />
        </button>
      </PopoverPrimitive.Trigger>

      <PopoverPrimitive.Portal>
        <PopoverPrimitive.Content
          sideOffset={4}
          align="start"
          className={cn(
            "z-50 w-[var(--radix-popover-trigger-width)] min-w-[220px]",
            "rounded-lg border bg-popover shadow-md",
            "data-[state=open]:animate-in data-[state=closed]:animate-out",
            "data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0",
            "data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95"
          )}
        >
          {/* Search */}
          <div className="flex items-center border-b px-3 py-2 gap-2">
            <Search className="w-4 h-4 text-muted-foreground shrink-0" />
            <input
              ref={inputRef}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Search…"
              className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
            />
            {query && (
              <button
                onClick={() => setQuery("")}
                className="text-muted-foreground hover:text-foreground text-xs px-1"
              >
                ✕
              </button>
            )}
          </div>

          {/* List */}
          <div className="max-h-[280px] overflow-y-auto overscroll-contain py-1">
            {filtered.length === 0 ? (
              <p className="py-6 text-center text-sm text-muted-foreground">{emptyMessage}</p>
            ) : (
              filtered.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => {
                    onSelect(item.id);
                    setOpen(false);
                  }}
                  className={cn(
                    "w-full flex items-center justify-between gap-2 px-3 py-2 text-sm transition-colors text-left",
                    "hover:bg-accent hover:text-accent-foreground",
                    value === item.id && "bg-primary/5 text-primary font-medium"
                  )}
                >
                  <div className="flex items-center gap-2 min-w-0">
                    <div className="min-w-0">
                      <div className="flex items-center gap-1.5">
                        <span className="truncate">{item.label}</span>
                        {item.highlighted && (
                          <Sparkles className="w-3 h-3 text-emerald-500 shrink-0" />
                        )}
                      </div>
                      {item.subtitle && (
                        <p className="text-xs text-muted-foreground truncate">{item.subtitle}</p>
                      )}
                    </div>
                  </div>
                  {value === item.id && (
                    <CheckCircle2 className="w-4 h-4 text-primary shrink-0" />
                  )}
                </button>
              ))
            )}
          </div>

          {/* Clear option */}
          {clearable && value && (
            <div className="border-t py-1">
              <button
                type="button"
                onClick={() => {
                  onClear?.();
                  setOpen(false);
                }}
                className="w-full px-3 py-2 text-xs text-muted-foreground hover:text-foreground hover:bg-accent text-left transition-colors"
              >
                Clear selection
              </button>
            </div>
          )}
        </PopoverPrimitive.Content>
      </PopoverPrimitive.Portal>
    </PopoverPrimitive.Root>
  );
}
