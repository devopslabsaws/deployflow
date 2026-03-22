"use client";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  CheckCircle2,
  XCircle,
  Server,
  GitBranch,
  AlertTriangle,
  User,
  Database,
} from "lucide-react";
import { cn, formatRelativeTime } from "@/lib/utils";

interface ActivityItem {
  id: string;
  type: "deployment_success" | "deployment_failure" | "server_added" | "pipeline_run" | "alert" | "user_action" | "db";
  title: string;
  description: string;
  timestamp: string;
}

const mockActivity: ActivityItem[] = [
  { id: "1", type: "deployment_success", title: "my-api deployed", description: "main → production", timestamp: "2026-03-19T09:55:00Z" },
  { id: "2", type: "alert", title: "High CPU usage", description: "server-us-east-1 > 90%", timestamp: "2026-03-19T09:48:00Z" },
  { id: "3", type: "deployment_failure", title: "frontend-app failed", description: "Build error: npm ci", timestamp: "2026-03-19T09:35:00Z" },
  { id: "4", type: "server_added", title: "Server added", description: "server-eu-west-1", timestamp: "2026-03-19T09:15:00Z" },
  { id: "5", type: "pipeline_run", title: "CI Pipeline ran", description: "my-api • 18 steps", timestamp: "2026-03-19T09:00:00Z" },
  { id: "6", type: "user_action", title: "Team invite sent", description: "alice@company.com", timestamp: "2026-03-19T08:30:00Z" },
  { id: "7", type: "deployment_success", title: "worker-service deployed", description: "v2.3.1 → staging", timestamp: "2026-03-19T08:00:00Z" },
  { id: "8", type: "db", title: "DB Backup completed", description: "postgres-prod", timestamp: "2026-03-19T07:00:00Z" },
];

const activityConfig = {
  deployment_success: { icon: CheckCircle2, iconClass: "text-success", bgClass: "bg-success/10" },
  deployment_failure: { icon: XCircle, iconClass: "text-destructive", bgClass: "bg-destructive/10" },
  server_added: { icon: Server, iconClass: "text-info", bgClass: "bg-info/10" },
  pipeline_run: { icon: GitBranch, iconClass: "text-primary", bgClass: "bg-primary/10" },
  alert: { icon: AlertTriangle, iconClass: "text-warning", bgClass: "bg-warning/10" },
  user_action: { icon: User, iconClass: "text-muted-foreground", bgClass: "bg-muted" },
  db: { icon: Database, iconClass: "text-purple-500", bgClass: "bg-purple-500/10" },
};

export function ActivityFeed() {
  return (
    <Card className="glass-card flex h-full flex-col">
      <CardHeader className="shrink-0 space-y-0 pb-3">
        <CardTitle className="text-sm font-semibold">Activity Feed</CardTitle>
      </CardHeader>
      <CardContent className="flex-1 overflow-hidden p-0">
        <ScrollArea className="h-[340px] px-4 pb-4">
          <div className="space-y-0.5">
            {mockActivity.map((item) => {
              const cfg = activityConfig[item.type];
              return (
                <div key={item.id} className="flex items-start gap-3 rounded-lg px-2 py-2 transition-colors hover:bg-muted/30">
                  <div className={cn("mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full", cfg.bgClass)}>
                    <cfg.icon className={cn("h-3.5 w-3.5", cfg.iconClass)} />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="text-[13px] font-medium leading-snug">{item.title}</p>
                    <p className="text-xs text-muted-foreground/80">{item.description}</p>
                    <p className="mt-0.5 text-[10px] text-muted-foreground/40">
                      {formatRelativeTime(item.timestamp)}
                    </p>
                  </div>
                </div>
              );
            })}
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  );
}
