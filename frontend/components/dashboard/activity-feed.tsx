"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Skeleton } from "@/components/ui/skeleton";
import {
  CheckCircle2,
  XCircle,
  Server,
  GitBranch,
  AlertTriangle,
  User,
  Database,
  Inbox,
} from "lucide-react";
import { useDeployments, useAlerts } from "@/hooks/use-api";
import { cn, formatRelativeTime } from "@/lib/utils";

interface ActivityItem {
  id: string;
  type: "deployment_success" | "deployment_failure" | "pipeline_run" | "alert" | "server_added" | "db";
  title: string;
  description: string;
  timestamp: string;
}

const activityConfig = {
  deployment_success: { icon: CheckCircle2, iconClass: "text-emerald-500", bgClass: "bg-emerald-500/10" },
  deployment_failure: { icon: XCircle,       iconClass: "text-destructive",  bgClass: "bg-destructive/10" },
  pipeline_run:       { icon: GitBranch,     iconClass: "text-primary",      bgClass: "bg-primary/10" },
  alert:              { icon: AlertTriangle, iconClass: "text-amber-500",    bgClass: "bg-amber-500/10" },
  server_added:       { icon: Server,        iconClass: "text-sky-400",      bgClass: "bg-sky-500/10" },
  db:                 { icon: Database,      iconClass: "text-purple-500",   bgClass: "bg-purple-500/10" },
};

export function ActivityFeed() {
  const { data: deployments, isLoading: dLoading } = useDeployments({ pageSize: 6 });
  const { data: alerts,      isLoading: aLoading  } = useAlerts();

  const isLoading = dLoading || aLoading;

  const items: ActivityItem[] = [];

  // Turn recent deployments into activity entries
  for (const d of deployments?.data ?? []) {
    const isSuccess = ["healthy", "succeeded", "success", "running"].includes(d.status ?? "");
    items.push({
      id: `d-${d.id}`,
      type: isSuccess ? "deployment_success" : "deployment_failure",
      title: isSuccess ? `${d.projectName ?? "Deployment"} deployed` : `${d.projectName ?? "Deployment"} failed`,
      description: `${d.branch ?? "main"} — ${(d.status ?? "unknown").replace(/_/g, " ")}`,
      timestamp: d.startedAt ?? d.createdAt ?? "",
    });
  }

  // Turn active/recent alerts into activity entries
  for (const a of (alerts ?? []).slice(0, 4)) {
    items.push({
      id: `a-${a.id}`,
      type: "alert",
      title: a.name ?? "Alert triggered",
      description: `${a.severity ?? "warning"} — ${a.source ?? ""}`.trim().replace(/—\s*$/, ""),
      timestamp: (a as any).triggeredAt ?? (a as any).createdAt ?? "",
    });
  }

  // Sort combined list by timestamp descending, take top 10
  const sorted = items
    .filter((i) => i.timestamp)
    .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
    .slice(0, 10);

  return (
    <Card className="glass-card flex h-full flex-col">
      <CardHeader className="shrink-0 space-y-0 pb-3">
        <CardTitle className="text-sm font-semibold">Activity Feed</CardTitle>
      </CardHeader>
      <CardContent className="flex-1 overflow-hidden p-0">
        <ScrollArea className="h-[340px] px-4 pb-4">
          {isLoading ? (
            <div className="space-y-3 pt-1">
              {Array.from({ length: 6 }).map((_, i) => (
                <div key={i} className="flex items-start gap-3">
                  <Skeleton className="h-7 w-7 rounded-full shrink-0" />
                  <div className="flex-1 space-y-1.5">
                    <Skeleton className="h-3 w-3/4" />
                    <Skeleton className="h-2.5 w-1/2" />
                  </div>
                </div>
              ))}
            </div>
          ) : sorted.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-48 gap-2">
              <Inbox className="h-8 w-8 text-muted-foreground/30" />
              <p className="text-xs text-muted-foreground">No recent activity</p>
            </div>
          ) : (
            <div className="space-y-0.5">
              {sorted.map((item) => {
                const cfg = activityConfig[item.type];
                return (
                  <div key={item.id} className="flex items-start gap-3 rounded-lg px-2 py-2 transition-colors hover:bg-muted/30">
                    <div className={cn("mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full", cfg.bgClass)}>
                      <cfg.icon className={cn("h-3.5 w-3.5", cfg.iconClass)} />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="text-[13px] font-medium leading-snug">{item.title}</p>
                      <p className="text-xs text-muted-foreground/80">{item.description}</p>
                      {item.timestamp && (
                        <p className="mt-0.5 text-[10px] text-muted-foreground/40">
                          {formatRelativeTime(item.timestamp)}
                        </p>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
