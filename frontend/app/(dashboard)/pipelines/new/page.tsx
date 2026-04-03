"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowLeft, GitBranch, Plus, Trash2, Loader2, Wand2, Zap } from "lucide-react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { useProjects, useCreatePipeline } from "@/hooks/use-api";
import { apiClient } from "@/lib/api-client";
import { toast } from "sonner";

export default function NewPipelinePage() {
  const router = useRouter();
  const { data: projectsData } = useProjects();
  const projects = projectsData?.data ?? [];

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [projectId, setProjectId] = useState("");
  const [trigger, setTrigger] = useState("manual");
  const [cronExpression, setCronExpression] = useState("");
  const [branches, setBranches] = useState("main");

  const createPipeline = useCreatePipeline();
  const saving = createPipeline.isPending;
  const [scaffolding, setScaffolding] = useState(false);
  const [simulating, setSimulating] = useState(false);

  const handleCreate = async () => {
    if (!name.trim()) { toast.error("Pipeline name is required."); return; }
    if (!projectId) { toast.error("Please select a project."); return; }
    if (trigger === "schedule" && !cronExpression.trim()) {
      toast.error("Cron expression is required for scheduled pipelines.");
      return;
    }
    try {
      const created: any = await createPipeline.mutateAsync({
        name: name.trim(),
        description: description.trim() || null,
        projectId,
        trigger,
        cronExpression: trigger === "schedule" ? cronExpression.trim() : null,
      });

      if (!created?.id) {
        toast.success(`Pipeline "${name}" created!`);
        router.push("/pipelines");
        return;
      }

      const pipeId = created.id;

      // Step 1: Auto-scaffold stages
      try {
        setScaffolding(true);
        await apiClient.post(`/pipelines/${pipeId}/scaffold`, { projectName: name.trim() });
      } catch {
        // Non-fatal, continue
      } finally {
        setScaffolding(false);
      }

      // Step 2: Trigger a first test run so users see logs immediately
      try {
        setSimulating(true);
        await apiClient.post(`/pipelines/${pipeId}/runs/simulate`, {});
        toast.success(`Pipeline "${name}" created — stages generated and first run started!`);
      } catch {
        toast.success(`Pipeline "${name}" created with auto-generated stages!`);
      } finally {
        setSimulating(false);
      }

      router.push(`/pipelines/${pipeId}`);
    } catch (e: any) {
      toast.error("Failed to create pipeline", { description: e.message });
    }
  };

  return (
    <div className="space-y-6 max-w-2xl">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Link href="/pipelines">
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
            <ArrowLeft className="w-4 h-4" />
          </Button>
        </Link>
        <div className="w-9 h-9 rounded-xl bg-muted flex items-center justify-center">
          <GitBranch className="w-5 h-5 text-muted-foreground" />
        </div>
        <div>
          <h1 className="text-xl font-bold">New Pipeline</h1>
          <p className="text-sm text-muted-foreground">Create an automated CI/CD pipeline</p>
        </div>
      </div>

      {/* Basic Info */}
      <Card className="glass-card">
        <CardHeader>
          <CardTitle className="text-base">Pipeline Details</CardTitle>
          <CardDescription>Basic information about your pipeline</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="pipeline-name">Pipeline Name *</Label>
            <Input
              id="pipeline-name"
              placeholder="e.g. Production Deploy"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="pipeline-desc">Description</Label>
            <Textarea
              id="pipeline-desc"
              placeholder="What does this pipeline do?"
              rows={2}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label>Project *</Label>
            <Select value={projectId} onValueChange={setProjectId}>
              <SelectTrigger>
                <SelectValue placeholder="Select a project" />
              </SelectTrigger>
              <SelectContent>
                {projects?.map((p) => (
                  <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
                ))}
                {!projects?.length && (
                  <SelectItem value="_none" disabled>No projects available</SelectItem>
                )}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Trigger Config */}
      <Card className="glass-card">
        <CardHeader>
          <CardTitle className="text-base">Trigger</CardTitle>
          <CardDescription>When should this pipeline run?</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-1.5">
            <Label>Trigger Type</Label>
            <Select value={trigger} onValueChange={setTrigger}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="manual">Manual — run on demand</SelectItem>
                <SelectItem value="push">On Push — auto-deploy on git push</SelectItem>
                <SelectItem value="schedule">Schedule — cron expression</SelectItem>
                <SelectItem value="pr">Pull Request — run on PR open/update</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {trigger === "push" && (
            <div className="space-y-1.5">
              <Label>Branches (comma-separated)</Label>
              <Input
                placeholder="main, production"
                value={branches}
                onChange={(e) => setBranches(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">Pipeline triggers when commits are pushed to these branches.</p>
            </div>
          )}

          {trigger === "schedule" && (
            <div className="space-y-1.5">
              <Label>Cron Expression *</Label>
              <Input
                placeholder="0 2 * * *"
                value={cronExpression}
                onChange={(e) => setCronExpression(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                E.g. <code className="bg-muted px-1 rounded">0 2 * * *</code> = daily at 2 AM,{" "}
                <code className="bg-muted px-1 rounded">*/30 * * * *</code> = every 30 min
              </p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Submit */}
      <div className="flex justify-end gap-3">
        <Button variant="outline" asChild disabled={saving || scaffolding || simulating}>
          <Link href="/pipelines">Cancel</Link>
        </Button>
        <Button onClick={handleCreate} disabled={saving || scaffolding || simulating}>
          {saving
            ? <><Loader2 className="w-4 h-4 mr-1.5 animate-spin" />Creating…</>
            : scaffolding
            ? <><Wand2 className="w-4 h-4 mr-1.5 animate-pulse" />Generating Stages…</>
            : simulating
            ? <><Zap className="w-4 h-4 mr-1.5 animate-pulse" />Running First Build…</>
            : <><Plus className="w-4 h-4 mr-1.5" />Create Pipeline</>
          }
        </Button>
      </div>
    </div>
  );
}
