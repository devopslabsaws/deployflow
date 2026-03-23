"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Loader2, Save } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { usePipeline, useUpdatePipeline } from "@/hooks/use-api";

export default function EditPipelinePage() {
  const params = useParams<{ id: string }>();
  const pipelineId = params?.id;
  const router = useRouter();

  const { data: pipeline, isLoading } = usePipeline(pipelineId);
  const updatePipeline = useUpdatePipeline();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [trigger, setTrigger] = useState("manual");
  const [cronExpression, setCronExpression] = useState("");
  const [isEnabled, setIsEnabled] = useState(true);

  useEffect(() => {
    if (!pipeline) return;
    setName(pipeline.name);
    setDescription(pipeline.description || "");
    setTrigger(pipeline.trigger?.type || "manual");
    setCronExpression(pipeline.trigger?.schedule || "");
    setIsEnabled(!!pipeline.isEnabled);
  }, [pipeline]);

  const onSave = async () => {
    if (!pipelineId) return;
    if (!name.trim()) {
      toast.error("Pipeline name is required.");
      return;
    }

    if (trigger === "schedule" && !cronExpression.trim()) {
      toast.error("Cron expression is required for schedule trigger.");
      return;
    }

    try {
      await updatePipeline.mutateAsync({
        id: pipelineId,
        name: name.trim(),
        description: description.trim() || undefined,
        trigger,
        cronExpression: trigger === "schedule" ? cronExpression.trim() : undefined,
        isEnabled,
      });
      toast.success("Pipeline updated.");
      router.push(`/pipelines/${pipelineId}`);
    } catch (e: any) {
      toast.error("Failed to update pipeline", { description: e.message });
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-64 w-full" />
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

  return (
    <div className="space-y-6 max-w-2xl">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" asChild>
          <Link href={`/pipelines/${pipelineId}`}>
            <ArrowLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Edit Pipeline</h1>
          <p className="text-sm text-muted-foreground">Update trigger and execution settings</p>
        </div>
      </div>

      <Card className="glass-card">
        <CardHeader>
          <CardTitle>Pipeline Settings</CardTitle>
          <CardDescription>Control when the pipeline runs and whether it is active.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="pipeline-name">Name</Label>
            <Input id="pipeline-name" value={name} onChange={(e) => setName(e.target.value)} />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="pipeline-description">Description</Label>
            <Textarea
              id="pipeline-description"
              value={description}
              rows={3}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>

          <div className="space-y-1.5">
            <Label>Trigger</Label>
            <Select value={trigger} onValueChange={setTrigger}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="manual">Manual</SelectItem>
                <SelectItem value="push">On Push</SelectItem>
                <SelectItem value="schedule">Schedule</SelectItem>
                <SelectItem value="pr">Pull Request</SelectItem>
                <SelectItem value="tag">Tag</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {trigger === "schedule" && (
            <div className="space-y-1.5">
              <Label htmlFor="pipeline-cron">Cron Expression</Label>
              <Input
                id="pipeline-cron"
                value={cronExpression}
                placeholder="0 2 * * *"
                onChange={(e) => setCronExpression(e.target.value)}
              />
            </div>
          )}

          <div className="flex items-center justify-between rounded-md border border-border/60 p-3">
            <div>
              <p className="text-sm font-medium">Enabled</p>
              <p className="text-xs text-muted-foreground">Disabled pipelines cannot be run.</p>
            </div>
            <Switch checked={isEnabled} onCheckedChange={setIsEnabled} />
          </div>
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button variant="outline" asChild>
          <Link href={`/pipelines/${pipelineId}`}>Cancel</Link>
        </Button>
        <Button onClick={onSave} disabled={updatePipeline.isPending}>
          {updatePipeline.isPending ? (
            <><Loader2 className="mr-1.5 h-4 w-4 animate-spin" />Saving...</>
          ) : (
            <><Save className="mr-1.5 h-4 w-4" />Save Changes</>
          )}
        </Button>
      </div>
    </div>
  );
}
