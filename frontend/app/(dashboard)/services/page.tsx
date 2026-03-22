"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Boxes, Plus, Play, Square, RefreshCw, Trash2, MoreVertical,
  Settings, Activity, Loader2,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  useServices, useStartService, useStopService, useRestartService,
  useDeleteService, useCreateService, useProjects,
} from "@/hooks/use-api";
import { formatDistanceToNow } from "date-fns";
import Link from "next/link";
import { toast } from "sonner";

const statusConfig: Record<string, { color: string; label: string }> = {
  running:    { color: "text-emerald-500", label: "Running" },
  stopped:    { color: "text-muted-foreground", label: "Stopped" },
  starting:   { color: "text-blue-500", label: "Starting" },
  restarting: { color: "text-amber-500", label: "Restarting" },
  error:      { color: "text-destructive", label: "Error" },
};

export default function ServicesPage() {
  const { data: services, isLoading, refetch } = useServices();
  const startService   = useStartService();
  const stopService    = useStopService();
  const restartService = useRestartService();
  const deleteService  = useDeleteService();

  const [createOpen, setCreateOpen]     = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);

  const handleStart = async (id: string) => {
    try {
      await startService.mutateAsync(id);
      toast.success("Service started.");
    } catch (e: any) {
      toast.error("Failed to start service", { description: e.message });
    }
  };

  const handleStop = async (id: string) => {
    try {
      await stopService.mutateAsync(id);
      toast.success("Service stopped.");
    } catch (e: any) {
      toast.error("Failed to stop service", { description: e.message });
    }
  };

  const handleRestart = async (id: string) => {
    try {
      await restartService.mutateAsync(id);
      toast.success("Service restarted.");
    } catch (e: any) {
      toast.error("Failed to restart service", { description: e.message });
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteService.mutateAsync(deleteTarget.id);
      toast.success(`${deleteTarget.name} deleted.`);
    } catch (e: any) {
      toast.error("Failed to delete service", { description: e.message });
    } finally {
      setDeleteTarget(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Services</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {services?.filter((s) => s.status?.toLowerCase() === "running").length ?? 0} running · {services?.length ?? 0} total
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={() => refetch()} className="gap-1.5">
            <RefreshCw className="h-4 w-4" />
            Refresh
          </Button>
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="h-4 w-4 mr-2" />
            New Service
          </Button>
        </div>
      </div>

      <div className="grid gap-4">
        {isLoading
          ? Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-28 w-full rounded-lg" />)
          : !services?.length
          ? (
            <Card>
              <CardContent className="py-16 text-center">
                <Boxes className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                <p className="text-lg font-medium">No services yet</p>
                <p className="text-sm text-muted-foreground mb-4">
                  Services are individual deployable components of your applications.
                </p>
                <Button onClick={() => setCreateOpen(true)}>
                  <Plus className="h-4 w-4 mr-2" />Create Service
                </Button>
              </CardContent>
            </Card>
          )
          : services.map((service) => {
            const statusKey = service.status?.toLowerCase() ?? "";
            const statusCfg = statusConfig[statusKey] ?? { color: "text-muted-foreground", label: service.status };
            const isRunning = statusKey === "running";
            const isBusy = startService.isPending || stopService.isPending || restartService.isPending;

            return (
              <motion.div key={service.id} initial={{ opacity: 0, y: 4 }} animate={{ opacity: 1, y: 0 }}>
                <Card className="hover:shadow-md transition-shadow">
                  <CardContent className="py-4 flex items-center gap-4">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="font-semibold">{service.name}</span>
                        <Badge variant="outline" className="text-xs">
                          <Activity className={`h-3 w-3 mr-1 ${statusCfg.color}`} />
                          {statusCfg.label}
                        </Badge>
                        <Badge variant="secondary" className="text-xs capitalize">
                          {service.type}
                        </Badge>
                      </div>
                      {(service as any).ports?.[0] && (
                        <p className="text-xs text-muted-foreground font-mono mt-1">
                          :{(service as any).ports[0].hostPort} → :{(service as any).ports[0].containerPort}
                        </p>
                      )}
                      {service.updatedAt && (
                        <p className="text-xs text-muted-foreground mt-0.5">
                          Last updated {formatDistanceToNow(new Date(service.updatedAt), { addSuffix: true })}
                        </p>
                      )}
                    </div>
                    <div className="flex items-center gap-2">
                      <Button variant="outline" size="sm" asChild>
                        <Link href={`/services/${service.id}`}>
                          <Settings className="h-4 w-4 mr-1.5" />Manage
                        </Link>
                      </Button>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" className="h-8 w-8" disabled={isBusy}>
                            <MoreVertical className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          {!isRunning && (
                            <DropdownMenuItem onClick={() => handleStart(service.id)}>
                              <Play className="h-4 w-4 mr-2" />Start
                            </DropdownMenuItem>
                          )}
                          {isRunning && (
                            <DropdownMenuItem onClick={() => handleStop(service.id)}>
                              <Square className="h-4 w-4 mr-2" />Stop
                            </DropdownMenuItem>
                          )}
                          <DropdownMenuItem onClick={() => handleRestart(service.id)}>
                            <RefreshCw className="h-4 w-4 mr-2" />Restart
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem
                            className="text-destructive"
                            onClick={() => setDeleteTarget({ id: service.id, name: service.name })}
                          >
                            <Trash2 className="h-4 w-4 mr-2" />Delete
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                  </CardContent>
                </Card>
              </motion.div>
            );
          })}
      </div>

      <CreateServiceDialog open={createOpen} onOpenChange={setCreateOpen} />

      <AlertDialog open={!!deleteTarget} onOpenChange={(open) => !open && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete {deleteTarget?.name}?</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete the service. This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={handleDelete}
              disabled={deleteService.isPending}
            >
              {deleteService.isPending && <Loader2 className="h-4 w-4 animate-spin mr-2" />}
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function CreateServiceDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (v: boolean) => void }) {
  const [name, setName]           = useState("");
  const [type, setType]           = useState("web");
  const [image, setImage]         = useState("");
  const [projectId, setProjectId] = useState("");
  const create = useCreateService();
  const { data: projectsData } = useProjects();
  const projects = (projectsData as any)?.data ?? [];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) { toast.error("Name is required."); return; }
    if (!projectId)   { toast.error("Project is required."); return; }
    try {
      await create.mutateAsync({ projectId, name: name.trim(), type, image: image || undefined });
      toast.success("Service created!");
      setName(""); setType("web"); setImage(""); setProjectId("");
      onOpenChange(false);
    } catch (err: any) {
      toast.error("Failed to create service", { description: err.message });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Create Service</DialogTitle>
          <DialogDescription>Add a new service to manage and deploy.</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 py-2">
          <div className="space-y-1.5">
            <Label htmlFor="svc-name" className="text-xs">Name *</Label>
            <Input id="svc-name" placeholder="my-api" className="h-8 text-sm"
              value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="svc-project" className="text-xs">Project *</Label>
            <Select value={projectId} onValueChange={setProjectId}>
              <SelectTrigger id="svc-project" className="h-8 text-sm">
                <SelectValue placeholder="Select project" />
              </SelectTrigger>
              <SelectContent>
                {projects.map((p: any) => (
                  <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="svc-type" className="text-xs">Type</Label>
            <Select value={type} onValueChange={setType}>
              <SelectTrigger id="svc-type" className="h-8 text-sm">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {["web", "api", "worker", "cron", "database", "cache", "queue", "storage"].map((t) => (
                  <SelectItem key={t} value={t} className="capitalize">{t}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="svc-image" className="text-xs">Docker Image</Label>
            <Input id="svc-image" placeholder="nginx:latest" className="h-8 text-sm font-mono"
              value={image} onChange={(e) => setImage(e.target.value)} />
          </div>
          <DialogFooter className="pt-2">
            <Button type="button" variant="outline" size="sm" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" size="sm" disabled={create.isPending}>
              {create.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin mr-2" />}
              Create
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
