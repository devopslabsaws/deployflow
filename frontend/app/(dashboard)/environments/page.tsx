"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  GitBranch,
  Plus,
  Trash2,
  CheckCircle2,
  ShieldAlert,
  Shield,
  Code2,
  TestTube2,
  LayoutList,
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
import { Switch } from "@/components/ui/switch";
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
  useEnvironments,
  useCreateEnvironment,
  useDeleteEnvironment,
  type EnvironmentDto,
} from "@/hooks/use-api";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

const envConfig: Record<
  string,
  {
    icon: React.ElementType;
    color: string;
    bg: string;
    description: string;
  }
> = {
  dev: {
    icon: Code2,
    color: "text-blue-400",
    bg: "bg-blue-400/10 border-blue-400/20",
    description: "Local and day-to-day development work",
  },
  development: {
    icon: Code2,
    color: "text-blue-400",
    bg: "bg-blue-400/10 border-blue-400/20",
    description: "Local and day-to-day development work",
  },
  qa: {
    icon: TestTube2,
    color: "text-yellow-400",
    bg: "bg-yellow-400/10 border-yellow-400/20",
    description: "Quality assurance and automated testing",
  },
  staging: {
    icon: LayoutList,
    color: "text-orange-400",
    bg: "bg-orange-400/10 border-orange-400/20",
    description: "Pre-production validation environment",
  },
  production: {
    icon: ShieldAlert,
    color: "text-green-400",
    bg: "bg-green-400/10 border-green-400/20",
    description: "Live customer-facing environment",
  },
};

function getConfig(env: EnvironmentDto) {
  return (
    envConfig[env.slug] ||
    envConfig[env.name.toLowerCase()] || {
      icon: GitBranch,
      color: "text-muted-foreground",
      bg: "bg-muted/50",
      description: "Custom environment",
    }
  );
}

export default function EnvironmentsPage() {
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<EnvironmentDto | null>(null);
  const [form, setForm] = useState({
    name: "",
    slug: "",
    isProduction: false,
    order: 0,
  });

  const { data: environments = [], isLoading } = useEnvironments();
  const createEnv = useCreateEnvironment();
  const deleteEnv = useDeleteEnvironment();

  async function handleCreate() {
    if (!form.name.trim()) return;
    try {
      await createEnv.mutateAsync({
        name: form.name.trim(),
        slug: form.slug || form.name.toLowerCase().replace(/\s+/g, "-"),
        isProduction: form.isProduction,
        order: form.order || environments.length,
      });
      toast.success(`Environment "${form.name}" created.`);
      setCreateOpen(false);
      setForm({ name: "", slug: "", isProduction: false, order: 0 });
    } catch {
      toast.error("Failed to create environment.");
    }
  }

  async function handleDelete() {
    if (!deleteTarget) return;
    try {
      await deleteEnv.mutateAsync(deleteTarget.id);
      toast.success(`Environment "${deleteTarget.name}" deleted.`);
      setDeleteTarget(null);
    } catch (err: unknown) {
      const msg =
        err instanceof Error ? err.message : "Failed to delete environment.";
      toast.error(msg);
    }
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <GitBranch className="h-6 w-6 text-primary" />
            Environments
          </h1>
          <p className="text-muted-foreground text-sm mt-1">
            Manage deployment environments — Dev, QA, Staging, Production
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)} className="gap-2">
          <Plus className="h-4 w-4" />
          New Environment
        </Button>
      </div>

      {/* Environment pipeline visual */}
      <Card className="bg-muted/30">
        <CardContent className="p-4">
          <div className="flex items-center gap-2 overflow-x-auto pb-1">
            {isLoading
              ? Array.from({ length: 4 }).map((_, i) => (
                  <Skeleton key={i} className="h-8 w-24 rounded-full" />
                ))
              : environments
                  .slice()
                  .sort((a, b) => a.order - b.order)
                  .map((env, idx) => {
                    const cfg = getConfig(env);
                    return (
                      <div key={env.id} className="flex items-center gap-2 shrink-0">
                        {idx > 0 && (
                          <div className="w-6 h-px bg-border" />
                        )}
                        <div
                          className={cn(
                            "flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium border",
                            cfg.bg,
                            cfg.color
                          )}
                        >
                          <cfg.icon className="h-3.5 w-3.5" />
                          {env.name}
                        </div>
                      </div>
                    );
                  })}
            <div className="flex items-center gap-2 shrink-0 ml-auto text-xs text-muted-foreground italic">
              Deployment pipeline →
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Environment cards */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-48 rounded-xl" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {environments
            .slice()
            .sort((a, b) => a.order - b.order)
            .map((env, idx) => {
              const cfg = getConfig(env);
              const Icon = cfg.icon;
              return (
                <motion.div
                  key={env.id}
                  initial={{ opacity: 0, y: 16 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: idx * 0.06 }}
                >
                  <Card
                    className={cn(
                      "border transition-colors hover:border-primary/30",
                      env.isProduction && "border-green-400/30"
                    )}
                  >
                    <CardHeader className="pb-2">
                      <div className="flex items-start justify-between">
                        <div
                          className={cn(
                            "w-10 h-10 rounded-lg flex items-center justify-center",
                            cfg.bg
                          )}
                        >
                          <Icon className={cn("h-5 w-5", cfg.color)} />
                        </div>
                        {!env.isProduction && (
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-7 w-7 text-muted-foreground hover:text-destructive"
                            onClick={() => setDeleteTarget(env)}
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </Button>
                        )}
                      </div>
                      <CardTitle className="text-base mt-2">{env.name}</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      <p className="text-xs text-muted-foreground">
                        {cfg.description}
                      </p>
                      <div className="flex items-center gap-2 flex-wrap">
                        <Badge variant="outline" className="text-xs">
                          /{env.slug}
                        </Badge>
                        {env.isDefault && (
                          <Badge variant="secondary" className="text-xs gap-1">
                            <CheckCircle2 className="h-3 w-3" />
                            Default
                          </Badge>
                        )}
                        {env.isProduction && (
                          <Badge
                            variant="outline"
                            className="text-xs gap-1 text-green-400 border-green-400/30"
                          >
                            <Shield className="h-3 w-3" />
                            Production
                          </Badge>
                        )}
                      </div>
                    </CardContent>
                  </Card>
                </motion.div>
              );
            })}
        </div>
      )}

      {/* Create Dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Create Environment</DialogTitle>
            <DialogDescription>
              Add a new deployment environment to your pipeline.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label>Name</Label>
              <Input
                placeholder="e.g. Staging"
                value={form.name}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    name: e.target.value,
                    slug: e.target.value
                      .toLowerCase()
                      .replace(/\s+/g, "-")
                      .replace(/[^a-z0-9-]/g, ""),
                  }))
                }
              />
            </div>
            <div>
              <Label>Slug</Label>
              <Input
                placeholder="staging"
                value={form.slug}
                onChange={(e) =>
                  setForm((f) => ({ ...f, slug: e.target.value }))
                }
              />
            </div>
            <div>
              <Label>Sort Order</Label>
              <Input
                type="number"
                min={0}
                value={form.order}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    order: parseInt(e.target.value) || 0,
                  }))
                }
              />
            </div>
            <div className="flex items-center justify-between">
              <Label>Mark as Production</Label>
              <Switch
                checked={form.isProduction}
                onCheckedChange={(v) =>
                  setForm((f) => ({ ...f, isProduction: v }))
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
              disabled={!form.name.trim() || createEnv.isPending}
            >
              {createEnv.isPending ? "Creating..." : "Create Environment"}
            </Button>
          </DialogFooter>
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
              This will permanently remove the environment. Services and
              deployments using it will be unaffected but must be reassigned.
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
