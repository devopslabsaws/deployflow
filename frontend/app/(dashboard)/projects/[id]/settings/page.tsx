"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import {
  ArrowLeft, Loader2, Trash2, GitBranch, Globe, Terminal,
  Code2, Settings, AlertTriangle, Sparkles, ChevronDown, ChevronUp, RefreshCw,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Separator } from "@/components/ui/separator";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useProject, useDeleteProject, useDetectStack, useApplyStack, type DetectedStackDto } from "@/hooks/use-api";
import { apiClient } from "@/lib/api-client";
import { Badge } from "@/components/ui/badge";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "@/hooks/use-api";
import { toast } from "sonner";
import { ConfirmActionDialog } from "@/components/ui/confirm-action-dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

async function fetchRepoBranches(repoUrl: string): Promise<string[]> {
  const githubMatch = repoUrl.match(/github\.com\/([^/]+)\/([^/?.#]+)/);
  const gitlabMatch = repoUrl.match(/gitlab\.com\/([^/]+(?:\/[^/]+)*)\/([^/?.#]+)/);

  if (githubMatch) {
    const [, owner, repo] = githubMatch;
    const res = await fetch(`https://api.github.com/repos/${owner}/${repo.replace(/\.git$/, "")}/branches?per_page=100`);
    if (!res.ok) throw new Error("Could not fetch branches \u2014 repository may be private or not found.");
    const data = await res.json();
    return (data as any[]).map((b) => b.name as string);
  }

  if (gitlabMatch) {
    const [, namespace, repo] = gitlabMatch;
    const encoded = encodeURIComponent(`${namespace}/${repo.replace(/\.git$/, "")}`);
    const res = await fetch(`https://gitlab.com/api/v4/projects/${encoded}/repository/branches?per_page=100`);
    if (!res.ok) throw new Error("Could not fetch branches \u2014 repository may be private or not found.");
    const data = await res.json();
    return (data as any[]).map((b) => b.name as string);
  }

  throw new Error("Unsupported URL. Only GitHub and GitLab are supported.");
}

const schema = z.object({
  name: z.string().min(2).max(100).regex(/^[a-zA-Z0-9\s\-_.]+$/, "Name contains invalid characters"),
  description: z.string().max(500).optional(),
  repositoryUrl: z.string().url("Must be a valid URL").optional().or(z.literal("")),
  branch: z.string().min(1).default("main"),
  buildCommand: z.string().optional(),
  startCommand: z.string().optional(),
  installCommand: z.string().optional(),
  dockerfilePath: z.string().optional(),
  customDomain: z.string().optional(),
  autoDeploy: z.boolean().default(true),
});

type FormData = z.infer<typeof schema>;

export default function ProjectSettingsPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const qc = useQueryClient();

  const { data: project, isLoading } = useProject(id);
  const p = project as any;
  const [deleteOpen, setDeleteOpen] = useState(false);

  // Stack detection state
  const [fileNames, setFileNames] = useState("");
  const [packageJson, setPackageJson] = useState("");
  const [detectedStack, setDetectedStack] = useState<DetectedStackDto | null>(null);
  const [showDockerfile, setShowDockerfile] = useState(false);
  const detectStack = useDetectStack();
  const applyStack = useApplyStack(id);

  const deleteProject = useDeleteProject();

  const updateProject = useMutation({
    mutationFn: (data: FormData) =>
      apiClient.put(`/projects/${id}`, {
        name: data.name,
        description: data.description ?? "",
        repositoryUrl: data.repositoryUrl || null,
        branch: data.branch,
        buildCommand: data.buildCommand ?? "",
        startCommand: data.startCommand ?? "",
        installCommand: data.installCommand ?? "",
        dockerfilePath: data.dockerfilePath ?? "",
        customDomain: data.customDomain || null,
        autoDeploy: data.autoDeploy,
      }),
    onSuccess: () => {
      toast.success("Project settings saved.");
      qc.invalidateQueries({ queryKey: queryKeys.projects.detail(id) });
      qc.invalidateQueries({ queryKey: queryKeys.projects.all });
    },
    onError: (e: any) =>
      toast.error("Failed to save settings", { description: e.message }),
  });

  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isDirty, isSubmitting },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { autoDeploy: true, branch: "main" },
  });

  const autoDeploy = watch("autoDeploy");
  const repositoryUrl = watch("repositoryUrl");

  const [branches, setBranches] = useState<string[]>([]);
  const [fetchingBranches, setFetchingBranches] = useState(false);

  async function handleFetchBranches() {
    if (!repositoryUrl) return;
    setFetchingBranches(true);
    try {
      const result = await fetchRepoBranches(repositoryUrl);
      setBranches(result);
      const currentBranch = watch("branch");
      if (result.length > 0 && !result.includes(currentBranch ?? "main")) {
        setValue("branch", result[0], { shouldDirty: true });
      }
      toast.success(`Fetched ${result.length} branch${result.length !== 1 ? "es" : ""}`);
    } catch (e: any) {
      toast.error("Failed to fetch branches", { description: e.message });
    } finally {
      setFetchingBranches(false);
    }
  }

  // Populate form once project loads
  useEffect(() => {
    if (p) {
      reset({
        name: p.name ?? "",
        description: p.description ?? "",
        repositoryUrl: p.repositoryUrl ?? "",
        branch: p.branch ?? "main",
        buildCommand: p.buildCommand ?? "",
        startCommand: p.startCommand ?? "",
        installCommand: p.installCommand ?? "",
        dockerfilePath: p.dockerfilePath ?? "",
        customDomain: p.customDomain ?? "",
        autoDeploy: p.autoDeploy ?? true,
      });
    }
  }, [p?.id]); // eslint-disable-line

  const handleDelete = async () => {
    try {
      await deleteProject.mutateAsync(id);
      toast.success("Project deleted.");
      router.push("/projects");
    } catch (e: any) {
      toast.error("Failed to delete project", { description: e.message });
    }
  };

  const onSubmit = (data: FormData) => updateProject.mutate(data);

  if (isLoading) {
    return (
      <div className="mx-auto max-w-2xl space-y-6">
        <Skeleton className="h-10 w-48" />
        <Skeleton className="h-64 rounded-xl" />
        <Skeleton className="h-48 rounded-xl" />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">

      {/* ── Header ── */}
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" asChild className="h-8 w-8 p-0">
          <Link href={`/projects/${id}`}><ArrowLeft className="h-4 w-4" /></Link>
        </Button>
        <div>
          <h1 className="text-lg font-bold">Project Settings</h1>
          <p className="text-xs text-muted-foreground">{p?.name}</p>
        </div>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">

        {/* ── General ── */}
        <Card className="glass-card">
          <CardHeader className="pb-3 pt-4 px-4">
            <CardTitle className="text-sm flex items-center gap-2">
              <Settings className="h-3.5 w-3.5" />General
            </CardTitle>
          </CardHeader>
          <CardContent className="px-4 pb-4 space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="name" className="text-xs">Project Name</Label>
              <Input id="name" className="h-8 text-sm" {...register("name")} />
              {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="description" className="text-xs">Description</Label>
              <Textarea
                id="description"
                rows={2}
                className="text-sm resize-none"
                placeholder="Brief description..."
                {...register("description")}
              />
            </div>
          </CardContent>
        </Card>

        {/* ── Repository ── */}
        <Card className="glass-card">
          <CardHeader className="pb-3 pt-4 px-4">
            <CardTitle className="text-sm flex items-center gap-2">
              <GitBranch className="h-3.5 w-3.5" />Repository
            </CardTitle>
          </CardHeader>
          <CardContent className="px-4 pb-4 space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="repositoryUrl" className="text-xs">Repository URL</Label>
              <Input
                id="repositoryUrl"
                className="h-8 text-sm font-mono"
                placeholder="https://github.com/owner/repo"
                {...register("repositoryUrl")}
              />
              {errors.repositoryUrl && (
                <p className="text-xs text-destructive">{errors.repositoryUrl.message}</p>
              )}
              <p className="text-xs text-muted-foreground">
                Formats: <code className="bg-muted px-1 rounded font-mono text-[10px]">https://github.com/owner/repo</code> · <code className="bg-muted px-1 rounded font-mono text-[10px]">https://gitlab.com/owner/repo.git</code>
              </p>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="branch" className="text-xs">Default Branch</Label>
              <div className="flex gap-2">
                {branches.length > 0 ? (
                  <Select
                    value={watch("branch") ?? "main"}
                    onValueChange={(v) => setValue("branch", v, { shouldDirty: true })}
                  >
                    <SelectTrigger className="flex-1 h-8 text-sm font-mono">
                      <SelectValue placeholder="Select branch..." />
                    </SelectTrigger>
                    <SelectContent>
                      {branches.map((b) => (
                        <SelectItem key={b} value={b} className="text-sm font-mono">{b}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <Input
                    id="branch"
                    className="h-8 text-sm font-mono flex-1"
                    placeholder="main"
                    {...register("branch")}
                  />
                )}
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  className="h-8 w-8 shrink-0"
                  disabled={!repositoryUrl || fetchingBranches}
                  onClick={handleFetchBranches}
                  title="Fetch branches from repository"
                >
                  {fetchingBranches ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <RefreshCw className="h-3.5 w-3.5" />
                  )}
                </Button>
              </div>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="customDomain" className="text-xs">Custom Domain</Label>
              <Input
                id="customDomain"
                className="h-8 text-sm font-mono"
                placeholder="app.yourdomain.com"
                {...register("customDomain")}
              />
            </div>
            <div className="flex items-center justify-between rounded-lg border border-border/60 p-3">
              <div>
                <p className="text-sm font-medium">Auto Deploy</p>
                <p className="text-xs text-muted-foreground">Deploy on every commit push</p>
              </div>
              <Switch
                checked={autoDeploy}
                onCheckedChange={(v) => setValue("autoDeploy", v, { shouldDirty: true })}
              />
            </div>
          </CardContent>
        </Card>

        {/* ── Build ── */}
        <Card className="glass-card">
          <CardHeader className="pb-3 pt-4 px-4">
            <CardTitle className="text-sm flex items-center gap-2">
              <Terminal className="h-3.5 w-3.5" />Build & Deploy
            </CardTitle>
          </CardHeader>
          <CardContent className="px-4 pb-4 space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="installCommand" className="text-xs">Install Command</Label>
              <Input
                id="installCommand"
                className="h-8 text-sm font-mono"
                placeholder="npm install"
                {...register("installCommand")}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="buildCommand" className="text-xs">Build Command</Label>
              <Input
                id="buildCommand"
                className="h-8 text-sm font-mono"
                placeholder="npm run build"
                {...register("buildCommand")}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="startCommand" className="text-xs">Start Command</Label>
              <Input
                id="startCommand"
                className="h-8 text-sm font-mono"
                placeholder="npm start"
                {...register("startCommand")}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="dockerfilePath" className="text-xs">Dockerfile Path</Label>
              <Input
                id="dockerfilePath"
                className="h-8 text-sm font-mono"
                placeholder="./Dockerfile"
                {...register("dockerfilePath")}
              />
            </div>
          </CardContent>
        </Card>

        {/* ── Auto-Detect Stack ── */}
        <Card className="glass-card border-primary/20">
          <CardHeader className="pb-3 pt-4 px-4">
            <CardTitle className="text-sm flex items-center gap-2">
              <Sparkles className="h-3.5 w-3.5 text-primary" />Auto-Detect Stack
            </CardTitle>
            <CardDescription className="text-xs">
              Paste your repository file list and optionally package.json to auto-detect framework,
              generate a Dockerfile, and pre-fill build commands.
            </CardDescription>
          </CardHeader>
          <CardContent className="px-4 pb-4 space-y-3">
            <div className="space-y-1.5">
              <Label className="text-xs">Repository Files (one per line)</Label>
              <Textarea
                rows={4}
                className="text-xs font-mono resize-none"
                placeholder={"package.json\ntsconfig.json\nnext.config.mjs\nDockerfile"}
                value={fileNames}
                onChange={(e) => setFileNames(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">
                package.json contents{" "}
                <span className="text-muted-foreground font-normal">(optional)</span>
              </Label>
              <Textarea
                rows={3}
                className="text-xs font-mono resize-none"
                placeholder='{"dependencies": {"next": "14.0.0"}}'
                value={packageJson}
                onChange={(e) => setPackageJson(e.target.value)}
              />
            </div>
            <Button
              type="button"
              size="sm"
              variant="outline"
              className="h-8 gap-1.5 text-xs"
              disabled={!fileNames.trim() || detectStack.isPending}
              onClick={() => {
                const files = fileNames.split("\n").map((f) => f.trim()).filter(Boolean);
                detectStack.mutate(
                  { fileNames: files, packageJsonContent: packageJson || undefined },
                  {
                    onSuccess: (data) => {
                      setDetectedStack(data as DetectedStackDto);
                      toast.success(`Detected: ${(data as any)?.framework}`);
                    },
                    onError: () => toast.error("Detection failed"),
                  }
                );
              }}
            >
              {detectStack.isPending ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : (
                <Sparkles className="h-3.5 w-3.5" />
              )}
              Detect Stack
            </Button>

            {detectedStack && (
              <div className="rounded-lg border border-primary/30 bg-primary/5 p-3 space-y-3">
                <div className="flex items-center justify-between">
                  <div className="space-y-1">
                    <Badge className="text-xs">{detectedStack.framework}</Badge>
                    <p className="text-xs text-muted-foreground">{detectedStack.explanation}</p>
                  </div>
                  <Button
                    type="button"
                    size="sm"
                    className="h-7 text-xs gap-1"
                    disabled={applyStack.isPending}
                    onClick={() => {
                      applyStack.mutate(
                        {
                          framework: detectedStack.framework,
                          buildCommandOverride: detectedStack.buildCommand || undefined,
                          startCommandOverride: detectedStack.startCommand || undefined,
                          installCommandOverride: detectedStack.installCommand || undefined,
                          portOverride: detectedStack.defaultPort || undefined,
                        },
                        {
                          onSuccess: () => {
                            toast.success("Stack applied — build commands updated");
                            setValue("buildCommand", detectedStack.buildCommand, { shouldDirty: true });
                            setValue("startCommand", detectedStack.startCommand, { shouldDirty: true });
                            setValue("installCommand", detectedStack.installCommand, { shouldDirty: true });
                          },
                          onError: () => toast.error("Failed to apply"),
                        }
                      );
                    }}
                  >
                    {applyStack.isPending ? (
                      <Loader2 className="h-3 w-3 animate-spin" />
                    ) : null}
                    Apply
                  </Button>
                </div>
                {detectedStack.suggestedEnvVars?.length > 0 && (
                  <div>
                    <p className="text-xs font-medium mb-1">Suggested env vars:</p>
                    <div className="flex flex-wrap gap-1">
                      {detectedStack.suggestedEnvVars.map((v) => (
                        <Badge key={v} variant="secondary" className="text-xs font-mono px-1.5 py-0">
                          {v}
                        </Badge>
                      ))}
                    </div>
                  </div>
                )}
                <div>
                  <button
                    type="button"
                    className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
                    onClick={() => setShowDockerfile((v) => !v)}
                  >
                    {showDockerfile ? (
                      <ChevronUp className="h-3 w-3" />
                    ) : (
                      <ChevronDown className="h-3 w-3" />
                    )}
                    {showDockerfile ? "Hide" : "Show"} generated Dockerfile
                  </button>
                  {showDockerfile && (
                    <pre className="mt-2 text-xs font-mono bg-muted rounded p-2 overflow-auto max-h-48 whitespace-pre">
                      {detectedStack.dockerfileContent}
                    </pre>
                  )}
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        {/* ── Save button ── */}
        <div className="flex justify-end">
          <Button
            type="submit"
            size="sm"
            className="h-8 gap-1.5 text-xs"
            disabled={!isDirty || isSubmitting || updateProject.isPending}
          >
            {(isSubmitting || updateProject.isPending) && (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            )}
            Save Changes
          </Button>
        </div>
      </form>

      {/* ── Danger Zone ── */}
      <Card className="border-destructive/40">
        <CardHeader className="pb-3 pt-4 px-4">
          <CardTitle className="text-sm text-destructive flex items-center gap-2">
            <AlertTriangle className="h-3.5 w-3.5" />Danger Zone
          </CardTitle>
          <CardDescription className="text-xs">
            These actions are irreversible.
          </CardDescription>
        </CardHeader>
        <CardContent className="px-4 pb-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium">Delete Project</p>
              <p className="text-xs text-muted-foreground">
                Archives this project and removes all related resources.
              </p>
            </div>
            <Button
              variant="destructive"
              size="sm"
              className="h-8 gap-1.5 text-xs"
              onClick={() => setDeleteOpen(true)}
              disabled={deleteProject.isPending}
            >
              {deleteProject.isPending
                ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
                : <Trash2 className="h-3.5 w-3.5" />}
              Delete
            </Button>
          </div>
        </CardContent>
      </Card>

      <ConfirmActionDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete Project"
        description={p?.name
          ? `Delete project \"${p.name}\"? This action cannot be undone.`
          : "Delete this project? This action cannot be undone."}
        confirmLabel="Delete Project"
        requireText={p?.name}
        isConfirming={deleteProject.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}
