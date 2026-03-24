"use client";

import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  Layers2,
  Plus,
  Trash2,
  Rocket,
  Square,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  Clock,
  MoreVertical,
  FileCode2,
  Server,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  useComposeStacks,
  useCreateComposeStack,
  useDeployComposeStack,
  useDeleteComposeStack,
  type ComposeStackDto,
} from "@/hooks/use-api";
import { useProjects, useServers } from "@/hooks/use-api";
import { formatRelativeTime, cn } from "@/lib/utils";
import { toast } from "sonner";

const statusConfig: Record<
  ComposeStackDto["status"],
  { icon: React.ElementType; color: string; label: string }
> = {
  running: { icon: CheckCircle2, color: "text-green-500", label: "Running" },
  stopped: { icon: Square, color: "text-muted-foreground", label: "Stopped" },
  starting: { icon: RefreshCw, color: "text-blue-500", label: "Starting" },
  failed: { icon: AlertTriangle, color: "text-destructive", label: "Failed" },
  removing: { icon: RefreshCw, color: "text-orange-400", label: "Removing" },
};

const STARTER_YAML = `version: "3.8"
services:
  app:
    image: nginx:alpine
    ports:
      - "80:80"
    restart: unless-stopped

  db:
    image: postgres:15-alpine
    environment:
      POSTGRES_PASSWORD: changeme
    volumes:
      - db_data:/var/lib/postgresql/data

volumes:
  db_data:
`;

export default function ComposePage() {
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<ComposeStackDto | null>(null);
  const [yamlPreview, setYamlPreview] = useState<ComposeStackDto | null>(null);
  const [form, setForm] = useState({
    name: "",
    projectId: "",
    serverId: "",
    composeYaml: STARTER_YAML,
    environmentName: "production",
  });

  const { data: stacks = [], isLoading } = useComposeStacks();
  const { data: projects } = useProjects();
  const projectList = projects?.data ?? [];
  const { data: servers = [] } = useServers();
  const createStack = useCreateComposeStack();
  const deployStack = useDeployComposeStack();
  const deleteStack = useDeleteComposeStack();

  async function handleCreate() {
    if (!form.name.trim() || !form.projectId) return;
    try {
      await createStack.mutateAsync({
        name: form.name.trim(),
        projectId: form.projectId,
        serverId: form.serverId || undefined,
        composeYaml: form.composeYaml,
        environmentName: form.environmentName,
      });
      toast.success("Compose stack created.");
      setCreateOpen(false);
      setForm({
        name: "",
        projectId: "",
        serverId: "",
        composeYaml: STARTER_YAML,
        environmentName: "production",
      });
    } catch {
      toast.error("Failed to create compose stack.");
    }
  }

  async function handleDeploy(stack: ComposeStackDto) {
    try {
      await deployStack.mutateAsync(stack.id);
      toast.success(`"${stack.name}" deployment started.`);
    } catch {
      toast.error("Deployment failed.");
    }
  }

  async function handleDelete() {
    if (!deleteTarget) return;
    try {
      await deleteStack.mutateAsync(deleteTarget.id);
      toast.success(`"${deleteTarget.name}" deleted.`);
      setDeleteTarget(null);
    } catch {
      toast.error("Failed to delete compose stack.");
    }
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <Layers2 className="h-6 w-6 text-primary" />
            Docker Compose
          </h1>
          <p className="text-muted-foreground text-sm mt-1">
            Manage multi-container applications with Compose stacks
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)} className="gap-2">
          <Plus className="h-4 w-4" />
          New Stack
        </Button>
      </div>

      {/* Stacks */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-44 rounded-xl" />
          ))}
        </div>
      ) : stacks.length === 0 ? (
        <div className="text-center py-24 text-muted-foreground">
          <Layers2 className="h-12 w-12 mx-auto mb-4 opacity-20" />
          <p className="font-medium">No compose stacks yet</p>
          <p className="text-sm mt-1">
            Create your first stack to run multi-service apps.
          </p>
          <Button
            className="mt-4 gap-2"
            onClick={() => setCreateOpen(true)}
          >
            <Plus className="h-4 w-4" />
            Create Stack
          </Button>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <AnimatePresence>
            {stacks.map((stack, idx) => {
              const status = statusConfig[stack.status] ?? statusConfig.stopped;
              const StatusIcon = status.icon;
              return (
                <motion.div
                  key={stack.id}
                  initial={{ opacity: 0, y: 12 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, scale: 0.95 }}
                  transition={{ delay: idx * 0.05 }}
                >
                  <Card className="hover:border-primary/30 transition-colors">
                    <CardHeader className="pb-2">
                      <div className="flex items-start justify-between">
                        <div className="flex items-center gap-2 min-w-0">
                          <Layers2 className="h-5 w-5 text-primary shrink-0" />
                          <CardTitle className="text-base truncate">
                            {stack.name}
                          </CardTitle>
                        </div>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon" className="h-7 w-7">
                              <MoreVertical className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem
                              onClick={() => setYamlPreview(stack)}
                              className="gap-2"
                            >
                              <FileCode2 className="h-4 w-4" />
                              View YAML
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              onClick={() => handleDeploy(stack)}
                              className="gap-2"
                            >
                              <Rocket className="h-4 w-4" />
                              Deploy
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              onClick={() => setDeleteTarget(stack)}
                              className="gap-2 text-destructive focus:text-destructive"
                            >
                              <Trash2 className="h-4 w-4" />
                              Delete
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </div>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      {/* Status */}
                      <div className="flex items-center gap-2">
                        <StatusIcon
                          className={cn(
                            "h-4 w-4",
                            status.color,
                            stack.status === "starting" && "animate-spin"
                          )}
                        />
                        <span className={cn("text-sm font-medium", status.color)}>
                          {status.label}
                        </span>
                        <Badge variant="outline" className="ml-auto text-xs">
                          {stack.serviceCount} service{stack.serviceCount !== 1 && "s"}
                        </Badge>
                      </div>

                      {/* Meta */}
                      <div className="text-xs text-muted-foreground space-y-1">
                        {stack.environmentName && (
                          <div className="flex items-center gap-1.5">
                            <Server className="h-3 w-3" />
                            <span className="capitalize">{stack.environmentName}</span>
                          </div>
                        )}
                        {stack.lastDeployedAt && (
                          <div className="flex items-center gap-1.5">
                            <Clock className="h-3 w-3" />
                            <span>
                              Last deployed {formatRelativeTime(stack.lastDeployedAt)}
                            </span>
                          </div>
                        )}
                        {stack.lastError && (
                          <div className="flex items-center gap-1.5 text-destructive">
                            <AlertTriangle className="h-3 w-3" />
                            <span className="truncate">{stack.lastError}</span>
                          </div>
                        )}
                      </div>

                      {/* Deploy button */}
                      <Button
                        size="sm"
                        className="w-full gap-1.5"
                        variant={stack.status === "running" ? "outline" : "default"}
                        onClick={() => handleDeploy(stack)}
                        disabled={
                          stack.status === "starting" ||
                          stack.status === "removing" ||
                          deployStack.isPending
                        }
                      >
                        <Rocket className="h-3.5 w-3.5" />
                        {stack.status === "running" ? "Re-deploy" : "Deploy"}
                      </Button>
                    </CardContent>
                  </Card>
                </motion.div>
              );
            })}
          </AnimatePresence>
        </div>
      )}

      {/* Create Dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>New Compose Stack</DialogTitle>
            <DialogDescription>
              Define a Docker Compose YAML and deploy multiple containers together.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Stack Name</Label>
                <Input
                  placeholder="my-app-stack"
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                />
              </div>
              <div>
                <Label>Environment</Label>
                <Select
                  value={form.environmentName}
                  onValueChange={(v) => setForm((f) => ({ ...f, environmentName: v }))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {["development", "qa", "staging", "production"].map((e) => (
                      <SelectItem key={e} value={e}>
                        {e.charAt(0).toUpperCase() + e.slice(1)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Project</Label>
                <Select
                  value={form.projectId}
                  onValueChange={(v) => setForm((f) => ({ ...f, projectId: v }))}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select project" />
                  </SelectTrigger>
                  <SelectContent>
                    {projectList.map((p: { id: string; name: string }) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label>Server (optional)</Label>
                <Select
                  value={form.serverId}
                  onValueChange={(v) => setForm((f) => ({ ...f, serverId: v }))}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Auto-assign" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="">Auto-assign</SelectItem>
                    {servers.map((s: { id: string; name: string }) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div>
              <Label>docker-compose.yml</Label>
              <Textarea
                className="font-mono text-xs min-h-[300px]"
                value={form.composeYaml}
                onChange={(e) =>
                  setForm((f) => ({ ...f, composeYaml: e.target.value }))
                }
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={handleCreate}
              disabled={!form.name.trim() || !form.projectId || createStack.isPending}
              className="gap-2"
            >
              <Layers2 className="h-4 w-4" />
              {createStack.isPending ? "Creating..." : "Create Stack"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* YAML Preview Dialog */}
      <Dialog open={!!yamlPreview} onOpenChange={() => setYamlPreview(null)}>
        <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{yamlPreview?.name} — Compose YAML</DialogTitle>
          </DialogHeader>
          <pre className="font-mono text-xs bg-muted rounded-lg p-4 overflow-x-auto whitespace-pre">
            {yamlPreview?.composeYaml}
          </pre>
        </DialogContent>
      </Dialog>

      {/* Delete Confirm */}
      <AlertDialog
        open={!!deleteTarget}
        onOpenChange={(o) => !o && setDeleteTarget(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              Delete &quot;{deleteTarget?.name}&quot;?
            </AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently remove the compose stack definition. Running
              containers will not be stopped — stop them manually first.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
