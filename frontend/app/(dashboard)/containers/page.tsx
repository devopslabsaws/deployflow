"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Container, Play, Square, RefreshCw, Trash2, MoreVertical,
  Activity, Search, ScrollText,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useContainers, useContainerLogs } from "@/hooks/use-api";
import { toast } from "sonner";
import { useStartContainer, useStopContainer, useRestartContainer, useRemoveContainer } from "@/hooks/use-api";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

const statusColors: Record<string, string> = {
  up: "text-success",
  running: "text-success",
  stopped: "text-muted-foreground",
  exited: "text-destructive",
  paused: "text-warning",
  restarting: "text-info",
};

function getStatusColor(status: string): string {
  const lower = status.toLowerCase();
  if (lower.startsWith("up")) return statusColors.up;
  if (lower === "exited") return statusColors.exited;
  if (lower === "paused") return statusColors.paused;
  return statusColors.stopped;
}

export default function ContainersPage() {
  const [search, setSearch] = useState("");
  const [deleteConfirm, setDeleteConfirm] = useState<{ serverId: string; id: string; name: string } | null>(null);
  const [logTarget, setLogTarget] = useState<{ serverId: string; id: string; name: string } | null>(null);
  const { data: containers, isLoading, refetch } = useContainers();

  const startMutation = useStartContainer();
  const stopMutation = useStopContainer();
  const restartMutation = useRestartContainer();
  const removeMutation = useRemoveContainer();

  const handleStartContainer = (serverId: string, containerId: string) => {
    startMutation.mutate(
      { serverId, containerId },
      {
        onSuccess: () => {
          toast.success("Container started successfully");
          refetch();
        },
        onError: (error) => {
          toast.error("Failed to start container", {
            description: error instanceof Error ? error.message : "Unknown error",
          });
        },
      }
    );
  };

  const handleStopContainer = (serverId: string, containerId: string) => {
    stopMutation.mutate(
      { serverId, containerId },
      {
        onSuccess: () => {
          toast.success("Container stopped successfully");
          refetch();
        },
        onError: (error) => {
          toast.error("Failed to stop container", {
            description: error instanceof Error ? error.message : "Unknown error",
          });
        },
      }
    );
  };

  const handleRestartContainer = (serverId: string, containerId: string) => {
    restartMutation.mutate(
      { serverId, containerId },
      {
        onSuccess: () => {
          toast.success("Container restarted successfully");
          refetch();
        },
        onError: (error) => {
          toast.error("Failed to restart container", {
            description: error instanceof Error ? error.message : "Unknown error",
          });
        },
      }
    );
  };

  const handleRemoveContainer = (serverId: string, containerId: string, name: string) => {
    setDeleteConfirm({ serverId, id: containerId, name });
  };

  const confirmRemoveContainer = () => {
    if (!deleteConfirm) return;
    removeMutation.mutate(
      { serverId: deleteConfirm.serverId, containerId: deleteConfirm.id },
      {
        onSuccess: () => {
          toast.success("Container removed successfully");
          setDeleteConfirm(null);
          refetch();
        },
        onError: (error) => {
          toast.error("Failed to remove container", {
            description: error instanceof Error ? error.message : "Unknown error",
          });
          setDeleteConfirm(null);
        },
      }
    );
  };

  const filtered = containers?.filter(
    (c) => c.name.toLowerCase().includes(search.toLowerCase()) ||
      c.image.toLowerCase().includes(search.toLowerCase())
  );

  const runningCount = containers?.filter((c) => c.status.toLowerCase().startsWith("up")).length ?? 0;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Containers</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {runningCount} running of {containers?.length ?? 0} total
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => refetch()}>
          <RefreshCw className="h-4 w-4 mr-2" />
          Refresh
        </Button>
      </div>

      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Search containers..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="pl-9"
        />
      </div>

      <div className="space-y-3">
        {isLoading
          ? Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-28 w-full rounded-lg" />)
          : !filtered?.length
          ? (
            <Card>
              <CardContent className="py-16 text-center">
                <Container className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                <p className="text-lg font-medium">No containers found</p>
                <p className="text-sm text-muted-foreground">
                  {containers === undefined
                    ? "Add a server and bring it online to see containers."
                    : "No containers match your search."}
                </p>
              </CardContent>
            </Card>
          )
          : filtered?.map((container) => (
            <motion.div key={`${container.serverId}-${container.id}`} initial={{ opacity: 0, y: 4 }} animate={{ opacity: 1, y: 0 }}>
              <Card>
                <CardContent className="py-4">
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex items-start gap-3 min-w-0">
                      <Container className="h-5 w-5 text-muted-foreground mt-0.5 flex-shrink-0" />
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="font-medium truncate">{container.name}</span>
                          <Badge
                            variant="outline"
                            className={`text-xs ${getStatusColor(container.status)}`}
                          >
                            <Activity className="h-3 w-3 mr-1" />
                            {container.status}
                          </Badge>
                          <span className="text-xs text-muted-foreground">
                            via {container.serverName}
                          </span>
                        </div>
                        <p className="text-xs text-muted-foreground font-mono mt-0.5 truncate">
                          {container.image}
                        </p>
                        {container.ports && (
                          <p className="text-xs text-muted-foreground font-mono mt-0.5 truncate">
                            {container.ports}
                          </p>
                        )}
                      </div>
                    </div>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" className="h-8 w-8 flex-shrink-0">
                          <MoreVertical className="h-4 w-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        {!container.status.toLowerCase().startsWith("up") && (
                          <DropdownMenuItem
                            onClick={() => handleStartContainer(container.serverId, container.id)}
                            disabled={startMutation.isPending}
                          >
                            <Play className="h-4 w-4 mr-2" />Start
                          </DropdownMenuItem>
                        )}
                        {container.status.toLowerCase().startsWith("up") && (
                          <DropdownMenuItem
                            onClick={() => handleStopContainer(container.serverId, container.id)}
                            disabled={stopMutation.isPending}
                          >
                            <Square className="h-4 w-4 mr-2" />Stop
                          </DropdownMenuItem>
                        )}
                        <DropdownMenuItem
                          onClick={() => handleRestartContainer(container.serverId, container.id)}
                          disabled={restartMutation.isPending}
                        >
                          <RefreshCw className="h-4 w-4 mr-2" />Restart
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          onClick={() => setLogTarget({ serverId: container.serverId, id: container.id, name: container.name })}
                        >
                          <ScrollText className="h-4 w-4 mr-2" />View Logs
                        </DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem
                          className="text-destructive"
                          onClick={() => handleRemoveContainer(container.serverId, container.id, container.name)}
                          disabled={removeMutation.isPending}
                        >
                          <Trash2 className="h-4 w-4 mr-2" />Remove
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                </CardContent>
              </Card>
            </motion.div>
          ))}
      </div>

      <AlertDialog open={!!deleteConfirm} onOpenChange={(open) => !open && setDeleteConfirm(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove Container</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to remove the container <strong>{deleteConfirm?.name}</strong>? This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <div className="flex gap-2 justify-end">
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={confirmRemoveContainer} disabled={removeMutation.isPending} className="bg-destructive text-destructive-foreground hover:bg-destructive/90">
              {removeMutation.isPending ? "Removing..." : "Remove"}
            </AlertDialogAction>
          </div>
        </AlertDialogContent>
      </AlertDialog>

      {/* Container Log Drawer */}
      <ContainerLogDialog target={logTarget} onClose={() => setLogTarget(null)} />
    </div>
  );
}

function ContainerLogDialog({
  target,
  onClose,
}: {
  target: { serverId: string; id: string; name: string } | null;
  onClose: () => void;
}) {
  const { data, isLoading, refetch } = useContainerLogs(
    target?.serverId ?? "",
    target?.id ?? "",
    !!target
  );

  return (
    <Dialog open={!!target} onOpenChange={(open) => { if (!open) onClose(); }}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 font-mono text-sm">
            <ScrollText className="h-4 w-4" />
            {target?.name} - Logs
          </DialogTitle>
        </DialogHeader>
        <div className="flex justify-end">
          <button
            onClick={() => refetch()}
            className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1"
          >
            <RefreshCw className="h-3 w-3" />Refresh
          </button>
        </div>
        <div className="rounded-xl bg-[#0d1117] border border-border/50 overflow-y-auto max-h-96 p-4 font-mono text-xs">
          {isLoading && <p className="text-muted-foreground">Loading logs...</p>}
          {!isLoading && !data?.logs && <p className="text-muted-foreground">No logs available.</p>}
          {data?.logs && data.logs.split("\n").map((line, i) => (
            <div key={i} className="py-0.5 whitespace-pre-wrap break-all text-[#c9d1d9]">{line}</div>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  );
}
