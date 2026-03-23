"use client";

import { useState, useEffect } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  ArrowLeft, CalendarClock, Plus, Trash2, Play, Loader2,
  CheckCircle2, XCircle, Clock, Terminal, ChevronDown, ChevronUp,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { Separator } from "@/components/ui/separator";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from "@/components/ui/dialog";
import { useProject } from "@/hooks/use-api";
import { apiClient } from "@/lib/api-client";
import { formatRelativeTime } from "@/lib/utils";
import { toast } from "sonner";
import { ConfirmActionDialog } from "@/components/ui/confirm-action-dialog";

interface ScheduledTask {
  id: string;
  name: string;
  command: string;
  frequency: string;
  timeoutSeconds: number;
  containerName?: string;
  isActive: boolean;
  lastRunAt?: string;
  lastRunStatus?: string;
  lastRunOutput?: string;
}

function StatusBadge({ status }: { status?: string }) {
  if (!status) return <Badge variant="outline" className="text-xs">Never run</Badge>;
  const map: Record<string, { variant: "default" | "destructive" | "secondary"; icon: React.ReactNode }> = {
    success: { variant: "default", icon: <CheckCircle2 className="w-3 h-3 mr-1" /> },
    failed: { variant: "destructive", icon: <XCircle className="w-3 h-3 mr-1" /> },
    timeout: { variant: "secondary", icon: <Clock className="w-3 h-3 mr-1" /> },
  };
  const cfg = map[status] ?? { variant: "secondary", icon: null };
  return (
    <Badge variant={cfg.variant} className="text-xs flex items-center gap-0.5">
      {cfg.icon}{status}
    </Badge>
  );
}

function TaskCard({
  task,
  onRun,
  onDelete,
  onToggle,
  running,
}: {
  task: ScheduledTask;
  onRun: (id: string) => void;
  onDelete: (id: string) => void;
  onToggle: (id: string, v: boolean) => void;
  running: boolean;
}) {
  const [open, setOpen] = useState(false);
  return (
    <div className="rounded-lg border bg-card/50 p-4 space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="space-y-0.5 min-w-0">
          <p className="text-sm font-medium truncate">{task.name}</p>
          <code className="text-xs text-muted-foreground">{task.command}</code>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <Switch
            checked={task.isActive}
            onCheckedChange={v => onToggle(task.id, v)}
          />
          <Button
            size="icon"
            variant="outline"
            className="h-7 w-7"
            disabled={running}
            onClick={() => onRun(task.id)}
            title="Run now"
          >
            {running ? <Loader2 className="w-3 h-3 animate-spin" /> : <Play className="w-3 h-3" />}
          </Button>
          <Button
            size="icon"
            variant="ghost"
            className="h-7 w-7 text-destructive hover:text-destructive"
            onClick={() => onDelete(task.id)}
          >
            <Trash2 className="w-3 h-3" />
          </Button>
        </div>
      </div>

      <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
        <span className="flex items-center gap-1">
          <Clock className="w-3 h-3" />
          <code>{task.frequency}</code>
        </span>
        {task.containerName && (
          <span className="flex items-center gap-1">
            <Terminal className="w-3 h-3" />
            <code>{task.containerName}</code>
          </span>
        )}
        <span>Timeout: {task.timeoutSeconds}s</span>
        {task.lastRunAt && (
          <span>Last run: {formatRelativeTime(task.lastRunAt)}</span>
        )}
      </div>

      <div className="flex items-center justify-between">
        <StatusBadge status={task.lastRunStatus} />
        {task.lastRunOutput && (
          <Button variant="ghost" size="sm" className="h-6 text-xs gap-1" onClick={() => setOpen(v => !v)}>
            {open ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
            Output
          </Button>
        )}
      </div>

      {open && task.lastRunOutput && (
        <pre className="text-xs bg-muted rounded p-3 overflow-x-auto whitespace-pre-wrap max-h-48 overflow-y-auto font-mono">
          {task.lastRunOutput}
        </pre>
      )}
    </div>
  );
}

const EMPTY_FORM = {
  name: "",
  command: "",
  frequency: "0 0 * * *",
  timeoutSeconds: 300,
  containerName: "",
};

export default function ScheduledTasksPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const { data: project } = useProject(id);
  const p = project as any;

  const [tasks, setTasks] = useState<ScheduledTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [runningId, setRunningId] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTaskId, setDeleteTaskId] = useState<string | null>(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [creating, setCreating] = useState(false);

  const load = () => {
    setLoading(true);
    apiClient.get(`/projects/${id}/scheduled-tasks`)
      .then((d: any) => setTasks(d.data ?? d ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, [id]);

  const handleRun = async (taskId: string) => {
    setRunningId(taskId);
    try {
      const result: any = await apiClient.post(
        `/projects/${id}/scheduled-tasks/${taskId}/run`, {}
      );
      toast.success("Task executed.", {
        description: result.output
          ? result.output.slice(0, 200) + (result.output.length > 200 ? "…" : "")
          : undefined,
      });
      load();
    } catch (e: any) {
      toast.error("Task failed", { description: e.message });
    } finally {
      setRunningId(null);
    }
  };

  const handleDelete = async (taskId: string) => {
    try {
      await apiClient.delete(`/projects/${id}/scheduled-tasks/${taskId}`);
      toast.success("Task deleted.");
      setTasks(t => t.filter(x => x.id !== taskId));
    } catch (e: any) {
      toast.error("Failed to delete", { description: e.message });
    }
  };

  const confirmDeleteTask = async () => {
    if (!deleteTaskId) return;
    await handleDelete(deleteTaskId);
  };

  const handleToggle = async (taskId: string, active: boolean) => {
    try {
      await apiClient.put(`/projects/${id}/scheduled-tasks/${taskId}`, { isActive: active });
      setTasks(t => t.map(x => x.id === taskId ? { ...x, isActive: active } : x));
    } catch (e: any) {
      toast.error("Failed to update", { description: e.message });
    }
  };

  const handleCreate = async () => {
    if (!form.name || !form.command || !form.frequency) {
      toast.error("Name, command, and frequency are required.");
      return;
    }
    setCreating(true);
    try {
      const created: any = await apiClient.post(`/projects/${id}/scheduled-tasks`, {
        name: form.name,
        command: form.command,
        frequency: form.frequency,
        timeoutSeconds: Number(form.timeoutSeconds) || 300,
        containerName: form.containerName || null,
      });
      setTasks(t => [...t, created]);
      setCreateOpen(false);
      setForm(EMPTY_FORM);
      toast.success("Scheduled task created!");
    } catch (e: any) {
      toast.error("Failed to create task", { description: e.message });
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="max-w-2xl space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Link href={`/projects/${id}`}>
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
            <ArrowLeft className="w-4 h-4" />
          </Button>
        </Link>
        <CalendarClock className="w-5 h-5 text-muted-foreground" />
        <div>
          <h1 className="text-xl font-bold">Scheduled Tasks</h1>
          <p className="text-sm text-muted-foreground">{p?.name}</p>
        </div>
        <Button size="sm" className="ml-auto" onClick={() => setCreateOpen(true)}>
          <Plus className="w-4 h-4 mr-1.5" />
          New Task
        </Button>
      </div>

      {/* Task list */}
      <Card className="glass-card">
        <CardHeader className="pb-3">
          <CardTitle className="text-sm">Tasks</CardTitle>
          <CardDescription>
            Scheduled tasks run commands in your container on a cron schedule
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {loading ? (
            [...Array(3)].map((_, i) => (
              <div key={i} className="h-20 rounded-lg bg-muted animate-pulse" />
            ))
          ) : tasks.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-12 text-muted-foreground gap-3">
              <CalendarClock className="w-10 h-10 opacity-30" />
              <p className="text-sm">No scheduled tasks yet</p>
              <Button variant="outline" size="sm" onClick={() => setCreateOpen(true)}>
                <Plus className="w-3.5 h-3.5 mr-1.5" />
                Create your first task
              </Button>
            </div>
          ) : (
            tasks.map(task => (
              <TaskCard
                key={task.id}
                task={task}
                running={runningId === task.id}
                onRun={handleRun}
                onDelete={setDeleteTaskId}
                onToggle={handleToggle}
              />
            ))
          )}
        </CardContent>
      </Card>

      {/* Create Dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <CalendarClock className="w-4 h-4" />
              New Scheduled Task
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="task-name" className="text-xs">Name *</Label>
              <Input
                id="task-name"
                placeholder="e.g. Daily backup"
                value={form.name}
                onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
                autoFocus
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="task-cmd" className="text-xs">Command *</Label>
              <Input
                id="task-cmd"
                placeholder="e.g. php artisan schedule:run"
                value={form.command}
                onChange={e => setForm(f => ({ ...f, command: e.target.value }))}
                className="font-mono text-xs"
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="task-freq" className="text-xs">Frequency (cron) *</Label>
                <Input
                  id="task-freq"
                  placeholder="0 0 * * *"
                  value={form.frequency}
                  onChange={e => setForm(f => ({ ...f, frequency: e.target.value }))}
                  className="font-mono text-xs"
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="task-timeout" className="text-xs">Timeout (seconds)</Label>
                <Input
                  id="task-timeout"
                  type="number"
                  min={1}
                  max={3600}
                  value={form.timeoutSeconds}
                  onChange={e => setForm(f => ({ ...f, timeoutSeconds: Number(e.target.value) }))}
                />
              </div>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="task-container" className="text-xs">
                Container name <span className="text-muted-foreground">(optional — runs inside container)</span>
              </Label>
              <Input
                id="task-container"
                placeholder="my-app-container"
                value={form.containerName}
                onChange={e => setForm(f => ({ ...f, containerName: e.target.value }))}
                className="font-mono text-xs"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button disabled={creating} onClick={handleCreate}>
              {creating && <Loader2 className="w-3.5 h-3.5 animate-spin mr-1.5" />}
              Create Task
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmActionDialog
        open={!!deleteTaskId}
        onOpenChange={(open) => { if (!open) setDeleteTaskId(null); }}
        title="Delete Scheduled Task"
        description="Delete this scheduled task? This cannot be undone."
        confirmLabel="Delete Task"
        onConfirm={confirmDeleteTask}
      />
    </div>
  );
}
