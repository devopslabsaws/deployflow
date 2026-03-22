"use client";

import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Server as ServerIcon, ArrowUpRight } from "lucide-react";
import type { Server } from "@/types";
import { cn } from "@/lib/utils";
import Link from "next/link";
import { Button } from "@/components/ui/button";

interface ServerHealthGridProps {
  servers: Server[];
}

const statusConfig = {
  online: { color: "text-success", dot: "bg-success", label: "Online" },
  offline: { color: "text-destructive", dot: "bg-destructive", label: "Offline" },
  provisioning: { color: "text-warning", dot: "bg-warning animate-pulse", label: "Provisioning" },
  maintenance: { color: "text-info", dot: "bg-info", label: "Maintenance" },
  error: { color: "text-destructive", dot: "bg-destructive animate-pulse", label: "Error" },
};

export function ServerHealthGrid({ servers }: ServerHealthGridProps) {
  return (
    <Card className="glass-card flex flex-col">
      <CardHeader className="flex shrink-0 flex-row items-center justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-sm font-semibold">Server Health</CardTitle>
          <CardDescription className="mt-0.5 text-xs">Infrastructure status</CardDescription>
        </div>
        <Button variant="ghost" size="sm" className="h-7 gap-1 text-xs text-muted-foreground" asChild>
          <Link href="/servers">
            View all <ArrowUpRight className="h-3 w-3" />
          </Link>
        </Button>
      </CardHeader>
      <CardContent className="flex-1 pt-0">
        {servers.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-10 text-center">
            <ServerIcon className="mb-3 h-9 w-9 text-muted-foreground/25" />
            <p className="text-sm text-muted-foreground">No servers configured</p>
            <Link href="/servers/add" className="mt-2 text-xs text-primary hover:underline">
              Add your first server
            </Link>
          </div>
        ) : (
          <div className="space-y-0.5">
            {servers.map((server) => {
              const cfg = statusConfig[server.status] ?? statusConfig.offline;
              return (
                <Link
                  key={server.id}
                  href={`/servers/${server.id}`}
                  className="flex items-center gap-3 rounded-lg px-2.5 py-2 transition-colors hover:bg-muted/40"
                >
                  <div className={cn("h-2 w-2 shrink-0 rounded-full", cfg.dot)} />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{server.name}</p>
                    <p className="truncate text-xs text-muted-foreground">{server.ipAddress}</p>
                  </div>
                  <div className="flex shrink-0 items-center gap-4">
                    {server.metrics && (
                      <>
                        <MetricBar label="CPU" value={server.metrics.cpuUsagePercent} />
                        <MetricBar label="MEM" value={server.metrics.memoryUsagePercent} />
                      </>
                    )}
                    <span className={cn("w-20 text-right text-xs font-medium", cfg.color)}>{cfg.label}</span>
                  </div>
                </Link>
              );
            })}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function MetricBar({ label, value }: { label: string; value: number }) {
  const color =
    value > 90 ? "bg-destructive" : value > 70 ? "bg-warning" : "bg-success";
  return (
    <div className="flex flex-col items-center gap-0.5 w-12">
      <div className="w-full h-1.5 bg-muted rounded-full overflow-hidden">
        <div
          className={cn("h-full rounded-full transition-all", color)}
          style={{ width: `${Math.min(100, value)}%` }}
        />
      </div>
      <span className="text-[9px] text-muted-foreground">{label} {Math.round(value)}%</span>
    </div>
  );
}
