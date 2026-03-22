"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import Link from "next/link";
import {
  ArrowLeft, Loader2, Trash2, GitBranch, Globe, Terminal,
  Code2, Settings, AlertTriangle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Separator } from "@/components/ui/separator";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useProject, useDeleteProject } from "@/hooks/use-api";
import { apiClient } from "@/lib/api-client";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "@/hooks/use-api";
import { toast } from "sonner";

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
    if (!confirm(`Delete project "${p?.name}"? This cannot be undone.`)) return;
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
                placeholder="https://github.com/user/repo"
                {...register("repositoryUrl")}
              />
              {errors.repositoryUrl && (
                <p className="text-xs text-destructive">{errors.repositoryUrl.message}</p>
              )}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="branch" className="text-xs">Default Branch</Label>
              <Input
                id="branch"
                className="h-8 text-sm font-mono"
                placeholder="main"
                {...register("branch")}
              />
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
              onClick={handleDelete}
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
    </div>
  );
}
