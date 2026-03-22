"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Plus,
  Server,
  Cpu,
  MemoryStick,
  HardDrive,
  Wifi,
  WifiOff,
  RefreshCw,
  Settings,
  Terminal,
  Trash2,
  MoreVertical,
  Activity,
  Package,
  Cloud,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useServers, useRemoveServer } from "@/hooks/use-api";
import { cn } from "@/lib/utils";
import { toast } from "sonner";
import Link from "next/link";
import type { Server as ServerType } from "@/types";
import { AddServerDialog } from "@/components/servers/add-server-dialog";

const providerIcons: Record<string, string> = {
  aws: "AWS",
  azure: "Azure",
  gcp: "GCP",
  digitalocean: "DO",
  hetzner: "Hetzner",
  vultr: "Vultr",
  custom: "Custom",
};

const statusConfig = {
  online: { color: "text-success", bg: "bg-success", label: "Online" },
  offline: { color: "text-destructive", bg: "bg-destructive", label: "Offline" },
  provisioning: { color: "text-warning", bg: "bg-warning animate-pulse", label: "Provisioning" },
  maintenance: { color: "text-info", bg: "bg-info", label: "Maintenance" },
  error: { color: "text-destructive", bg: "bg-destructive animate-pulse", label: "Error" },
};

export default function ServersPage() {
  const [addOpen, setAddOpen] = useState(false);
  const { data: servers, isLoading, refetch } = useServers();
  const removeServer = useRemoveServer();

  const handleRemove = async (id: string, name: string) => {
    if (!confirm(`Remove server "${name}"? Active deployments may be affected.`)) return;
    try {
      await removeServer.mutateAsync(id);
      toast.success(`Server "${name}" removed.`);
    } catch (e: any) {
      toast.error("Failed to remove server", { description: e.message });
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Servers</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {servers?.filter((s) => s.status === "online").length ?? 0} online /{" "}
            {servers?.length ?? 0} total
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={() => refetch()} className="gap-1.5">
            <RefreshCw className="h-4 w-4" />
            Refresh
          </Button>
          <Button onClick={() => setAddOpen(true)}>
            <Plus className="w-4 h-4 mr-1.5" />
            Add Server
          </Button>
        </div>
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {[...Array(4)].map((_, i) => <Skeleton key={i} className="h-64 rounded-xl" />)}
        </div>
      ) : !servers?.length ? (
        <EmptyServersState onAdd={() => setAddOpen(true)} />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {servers.map((server) => (
            <ServerCard
              key={server.id}
              server={server}
              onRemove={() => handleRemove(server.id, server.name)}
            />
          ))}
        </div>
      )}

      <AddServerDialog open={addOpen} onOpenChange={setAddOpen} />
    </div>
  );
}

function ServerCard({ server, onRemove }: { server: ServerType; onRemove: () => void }) {
  const cfg = statusConfig[server.status] ?? statusConfig.offline;

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} className="group">
      <Card className="glass-card hover:border-border/80 transition-all hover:shadow-md">
        <CardHeader className="pb-3">
          <div className="flex items-start justify-between">
            <Link href={`/servers/${server.id}`} className="flex items-center gap-3 flex-1 min-w-0">
              <div className="w-10 h-10 rounded-xl bg-muted flex items-center justify-center shrink-0">
                <Server className="w-5 h-5 text-muted-foreground" />
              </div>
              <div className="min-w-0">
                <p className="font-semibold text-sm truncate">{server.name}</p>
                <p className="text-xs text-muted-foreground">{server.ipAddress}</p>
              </div>
            </Link>

            <div className="flex items-center gap-2">
              <div className="flex items-center gap-1.5">
                <div className={cn("w-2 h-2 rounded-full", cfg.bg)} />
                <span className={cn("text-xs font-medium", cfg.color)}>{cfg.label}</span>
              </div>
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="ghost" size="sm" className="h-7 w-7 px-0 opacity-0 group-hover:opacity-100">
                    <MoreVertical className="w-4 h-4" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem asChild>
                    <Link href={`/servers/${server.id}`}>
                      <Activity className="mr-2 w-4 h-4" />View Details
                    </Link>
                  </DropdownMenuItem>
                  <DropdownMenuItem asChild>
                    <Link href={`/servers/${server.id}/terminal`}>
                      <Terminal className="mr-2 w-4 h-4" />Terminal
                    </Link>
                  </DropdownMenuItem>
                  <DropdownMenuItem asChild>
                    <Link href={`/servers/${server.id}/settings`}>
                      <Settings className="mr-2 w-4 h-4" />Settings
                    </Link>
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem className="text-destructive" onClick={onRemove}>
                    <Trash2 className="mr-2 w-4 h-4" />Remove
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </div>
        </CardHeader>

        <CardContent className="space-y-4">
          {/* Info */}
          <div className="grid grid-cols-2 gap-2 text-xs">
            <div className="flex items-center gap-1.5 text-muted-foreground">
              <Cloud className="w-3.5 h-3.5 shrink-0" />
              <span>{providerIcons[server.provider] ?? server.provider}</span>
              {server.region && <span className="text-muted-foreground/60">• {server.region}</span>}
            </div>
            <div className="flex items-center gap-1.5 text-muted-foreground">
              <Package className="w-3.5 h-3.5 shrink-0" />
              <span>Docker {server.dockerVersion ?? "—"}</span>
            </div>
          </div>

          {/* Metrics */}
          {server.metrics ? (
            <div className="space-y-2.5">
              <MetricProgress
                label="CPU"
                value={server.metrics.cpuUsagePercent}
                icon={Cpu}
              />
              <MetricProgress
                label="Memory"
                value={server.metrics.memoryUsagePercent}
                icon={MemoryStick}
              />
              <MetricProgress
                label="Disk"
                value={server.metrics.diskUsagePercent}
                icon={HardDrive}
              />
            </div>
          ) : (
            <div className="text-center py-3 text-xs text-muted-foreground">
              No metrics available
            </div>
          )}

          {/* Specs */}
          <div className="grid grid-cols-3 gap-2 pt-2 border-t border-border/50 text-center">
            <SpecBadge label="vCPUs" value={server.cpu} />
            <SpecBadge label="RAM" value={`${server.memoryGB}GB`} />
            <SpecBadge label="Storage" value={`${server.diskGB}GB`} />
          </div>
        </CardContent>
      </Card>
    </motion.div>
  );
}

function MetricProgress({
  label,
  value,
  icon: Icon,
}: {
  label: string;
  value: number;
  icon: React.ComponentType<{ className?: string }>;
}) {
  const color =
    value > 90
      ? "bg-destructive"
      : value > 70
      ? "bg-warning"
      : "bg-success";

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-xs">
        <div className="flex items-center gap-1.5 text-muted-foreground">
          <Icon className="w-3.5 h-3.5" />
          <span>{label}</span>
        </div>
        <span
          className={cn(
            "font-medium",
            value > 90 ? "text-destructive" : value > 70 ? "text-warning" : "text-muted-foreground"
          )}
        >
          {Math.round(value)}%
        </span>
      </div>
      <div className="h-1.5 bg-muted rounded-full overflow-hidden">
        <div
          className={cn("h-full rounded-full transition-all", color)}
          style={{ width: `${Math.min(100, value)}%` }}
        />
      </div>
    </div>
  );
}

function SpecBadge({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="flex flex-col">
      <span className="font-semibold text-sm">{value}</span>
      <span className="text-[10px] text-muted-foreground">{label}</span>
    </div>
  );
}

function EmptyServersState({ onAdd }: { onAdd: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-20 text-center">
      <div className="w-16 h-16 rounded-2xl bg-muted flex items-center justify-center mb-4">
        <Server className="w-8 h-8 text-muted-foreground" />
      </div>
      <h3 className="text-lg font-semibold mb-2">No servers connected</h3>
      <p className="text-muted-foreground text-sm max-w-sm mb-6">
        Add a server via SSH to start deploying. Supports any Linux server with Docker installed.
      </p>
      <Button onClick={onAdd}>
        <Plus className="w-4 h-4 mr-1.5" />
        Add Server
      </Button>
    </div>
  );
}
