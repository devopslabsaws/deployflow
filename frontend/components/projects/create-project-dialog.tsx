"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Loader2, GitBranch, Github, Globe, Server, RefreshCw, Wand2, CheckCircle2 } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useCreateProject, useServers } from "@/hooks/use-api";
import type { DetectedStackDto } from "@/hooks/use-api";
import { apiClient } from "@/lib/api-client";
import { toast } from "sonner";

async function fetchRepoBranches(repoUrl: string): Promise<string[]> {
  const githubMatch = repoUrl.match(/github\.com\/([^/]+)\/([^/?.#]+)/);
  const gitlabMatch = repoUrl.match(/gitlab\.com\/([^/]+(?:\/[^/]+)*)\/([^/?.#]+)/);

  if (githubMatch) {
    const [, owner, repo] = githubMatch;
    const res = await fetch(`https://api.github.com/repos/${owner}/${repo.replace(/\.git$/, "")}/branches?per_page=100`);
    if (!res.ok) throw new Error("Could not fetch branches — repository may be private or not found.");
    const data = await res.json();
    return (data as any[]).map((b) => b.name as string);
  }

  if (gitlabMatch) {
    const [, namespace, repo] = gitlabMatch;
    const encoded = encodeURIComponent(`${namespace}/${repo.replace(/\.git$/, "")}`);
    const res = await fetch(`https://gitlab.com/api/v4/projects/${encoded}/repository/branches?per_page=100`);
    if (!res.ok) throw new Error("Could not fetch branches — repository may be private or not found.");
    const data = await res.json();
    return (data as any[]).map((b) => b.name as string);
  }

  throw new Error("Unsupported URL. Only GitHub and GitLab are supported.");
}

const schema = z.object({
  name: z
    .string()
    .min(2, "Must be at least 2 chars")
    .max(50, "Too long")
    .regex(/^[a-zA-Z0-9-_]+$/, "Alphanumeric, hyphens and underscores only"),
  description: z.string().max(200).optional(),
  repositoryUrl: z.string().url("Must be a valid URL").optional().or(z.literal("")),
  repositoryBranch: z.string().default("main"),
  buildCommand: z.string().optional(),
  startCommand: z.string().optional(),
  port: z.coerce.number().min(1).max(65535).optional(),
  autoDeployEnabled: z.boolean().default(true),
  dockerfilePath: z.string().optional(),
});

type FormData = z.infer<typeof schema>;

interface CreateProjectDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function CreateProjectDialog({ open, onOpenChange }: CreateProjectDialogProps) {
  const create = useCreateProject();
  const { data: serversRaw } = useServers();
  const servers = serversRaw ?? [];
  const [activeTab, setActiveTab] = useState("general");
  const [selectedServerId, setSelectedServerId] = useState<string>("none");
  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({ resolver: zodResolver(schema), defaultValues: { autoDeployEnabled: true, repositoryBranch: "main" } });

  const autoDeployEnabled = watch("autoDeployEnabled");
  const repositoryUrl = watch("repositoryUrl");

  const [branches, setBranches] = useState<string[]>([]);
  const [fetchingBranches, setFetchingBranches] = useState(false);
  const [detectingStack, setDetectingStack] = useState(false);
  const [detectedStack, setDetectedStack] = useState<DetectedStackDto | null>(null);

  async function handleDetectStack() {
    if (!repositoryUrl) return;
    setDetectingStack(true);
    setDetectedStack(null);
    try {
      const branch = watch("repositoryBranch") || "main";
      const result = await apiClient.post<DetectedStackDto>(
        "/stack-detection/detect-from-url",
        { repositoryUrl, branch }
      );
      setDetectedStack(result);
      if (result.buildCommand) setValue("buildCommand", result.buildCommand);
      if (result.startCommand) setValue("startCommand", result.startCommand);
      if (result.defaultPort) setValue("port", result.defaultPort);
      toast.success(`Detected: ${result.framework}`, {
        description: result.explanation,
      });
    } catch (e: any) {
      toast.error("Stack detection failed", {
        description: e?.response?.data?.error ?? e?.message ?? "Unknown error",
      });
    } finally {
      setDetectingStack(false);
    }
  }

  async function handleFetchBranches() {
    if (!repositoryUrl) return;
    setFetchingBranches(true);
    try {
      const result = await fetchRepoBranches(repositoryUrl);
      setBranches(result);
      if (result.length > 0 && !result.includes(watch("repositoryBranch") ?? "main")) {
        setValue("repositoryBranch", result[0]);
      }
      toast.success(`Fetched ${result.length} branch${result.length !== 1 ? "es" : ""}`);
    } catch (e: any) {
      toast.error("Failed to fetch branches", { description: e.message });
    } finally {
      setFetchingBranches(false);
    }
  }

  const onSubmit = async (data: FormData) => {
    try {
      await create.mutateAsync({
        name: data.name,
        description: data.description ?? "",
        repositoryUrl: data.repositoryUrl || undefined,
        branch: data.repositoryBranch || "main",
        buildCommand: data.buildCommand ?? "",
        startCommand: data.startCommand ?? "",
        dockerfilePath: data.dockerfilePath,
        framework: "",
        autoDeploy: data.autoDeployEnabled,
        tags: [],
        assignedServerId: selectedServerId !== "none" ? selectedServerId : undefined,
      } as any);
      toast.success(`Project "${data.name}" created!`);
      reset();
      setActiveTab("general");
      setSelectedServerId("none");
      onOpenChange(false);
    } catch (e: any) {
      toast.error("Failed to create project", { description: e.message });
    }
  };

  const onValidationError = (errs: typeof errors) => {
    const generalFields = ["name", "description"] as const;
    const repoFields = ["repositoryUrl", "repositoryBranch"] as const;
    const buildFields = ["buildCommand", "startCommand", "dockerfilePath", "port"] as const;
    if (generalFields.some(f => errs[f])) {
      setActiveTab("general");
    } else if (repoFields.some(f => errs[f])) {
      setActiveTab("repository");
    } else if (buildFields.some(f => errs[f])) {
      setActiveTab("build");
    }
    toast.error("Please fix the validation errors before submitting.");
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Create New Project</DialogTitle>
          <DialogDescription>
            Configure your project and deployment settings.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit, onValidationError)} className="space-y-4">
          <Tabs value={activeTab} onValueChange={setActiveTab}>
            <TabsList className="w-full">
              <TabsTrigger value="general" className="flex-1">General</TabsTrigger>
              <TabsTrigger value="repository" className="flex-1">Repository</TabsTrigger>
              <TabsTrigger value="build" className="flex-1">Build</TabsTrigger>
            </TabsList>

            <TabsContent value="general" className="space-y-4 mt-4">
              <div className="space-y-2">
                <Label htmlFor="name">Project Name *</Label>
                <Input id="name" placeholder="my-awesome-app" {...register("name")} />
                {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  placeholder="Brief description of your project..."
                  rows={3}
                  {...register("description")}
                />
              </div>
              <div className="space-y-2">
                <Label>Deploy Server</Label>
                <Select value={selectedServerId} onValueChange={setSelectedServerId}>
                  <SelectTrigger>
                    <div className="flex items-center gap-2">
                      <Server className="h-4 w-4 text-muted-foreground" />
                      <SelectValue placeholder="Select a server..." />
                    </div>
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No server (assign later)</SelectItem>
                    {servers.map((s) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name} — {s.ipAddress ?? ""}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">A server must be assigned before deploying.</p>
              </div>
            </TabsContent>

            <TabsContent value="repository" className="space-y-4 mt-4">
              <div className="space-y-2">
                <Label htmlFor="repoUrl">Repository URL</Label>
                <div className="flex gap-2">
                  <div className="relative flex-1">
                    <Github className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                    <Input
                      id="repoUrl"
                      placeholder="https://github.com/owner/repo"
                      className="pl-9"
                      {...register("repositoryUrl")}
                    />
                  </div>
                  <Button
                    type="button"
                    variant="outline"
                    size="icon"
                    disabled={!repositoryUrl || detectingStack}
                    onClick={handleDetectStack}
                    title="Auto-detect tech stack"
                  >
                    {detectingStack ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Wand2 className="h-4 w-4" />
                    )}
                  </Button>
                </div>
                <p className="text-xs text-muted-foreground">
                  Formats: <code className="bg-muted px-1 rounded font-mono text-[10px]">https://github.com/owner/repo</code> · <code className="bg-muted px-1 rounded font-mono text-[10px]">https://gitlab.com/owner/repo.git</code>
                </p>
                {errors.repositoryUrl && (
                  <p className="text-xs text-destructive">{errors.repositoryUrl.message}</p>
                )}
                {detectedStack && (
                  <div className="flex items-center gap-2 rounded-md border border-green-500/30 bg-green-500/10 px-3 py-1.5">
                    <CheckCircle2 className="h-4 w-4 text-green-500 shrink-0" />
                    <span className="text-sm font-medium text-green-600 dark:text-green-400">
                      Detected: {detectedStack.framework}
                    </span>
                    <span className="text-xs text-muted-foreground ml-1">
                      — port {detectedStack.defaultPort}
                    </span>
                  </div>
                )}
              </div>
              <div className="space-y-2">
                <Label htmlFor="branch">Default Branch</Label>
                <div className="flex gap-2">
                  {branches.length > 0 ? (
                    <Select
                      value={watch("repositoryBranch") ?? "main"}
                      onValueChange={(v) => setValue("repositoryBranch", v)}
                    >
                      <SelectTrigger className="flex-1">
                        <GitBranch className="h-4 w-4 text-muted-foreground mr-2" />
                        <SelectValue placeholder="Select branch..." />
                      </SelectTrigger>
                      <SelectContent>
                        {branches.map((b) => (
                          <SelectItem key={b} value={b}>{b}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  ) : (
                    <div className="relative flex-1">
                      <GitBranch className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                      <Input
                        id="branch"
                        placeholder="main"
                        className="pl-9"
                        {...register("repositoryBranch")}
                      />
                    </div>
                  )}
                  <Button
                    type="button"
                    variant="outline"
                    size="icon"
                    disabled={!repositoryUrl || fetchingBranches}
                    onClick={handleFetchBranches}
                    title="Fetch branches from repository"
                  >
                    {fetchingBranches ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <RefreshCw className="h-4 w-4" />
                    )}
                  </Button>
                </div>
              </div>
              <div className="flex items-center justify-between rounded-lg border p-3">
                <div>
                  <p className="text-sm font-medium">Auto Deploy</p>
                  <p className="text-xs text-muted-foreground">Deploy on every commit push</p>
                </div>
                <Switch
                  checked={autoDeployEnabled}
                  onCheckedChange={(v) => setValue("autoDeployEnabled", v)}
                />
              </div>
            </TabsContent>

            <TabsContent value="build" className="space-y-4 mt-4">
              <div className="space-y-2">
                <Label htmlFor="buildCommand">Build Command</Label>
                <Input id="buildCommand" placeholder="npm run build" {...register("buildCommand")} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="startCommand">Start Command</Label>
                <Input id="startCommand" placeholder="npm start" {...register("startCommand")} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="dockerfilePath">Dockerfile Path</Label>
                <Input id="dockerfilePath" placeholder="./Dockerfile" {...register("dockerfilePath")} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="port">Port</Label>
                <Input
                  id="port"
                  type="number"
                  placeholder="3000"
                  {...register("port")}
                />
              </div>
            </TabsContent>
          </Tabs>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting || create.isPending}>
              {(isSubmitting || create.isPending) && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Create Project
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
