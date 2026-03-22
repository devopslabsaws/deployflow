"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Bell, BellOff, CheckCircle, AlertTriangle, AlertCircle,
  Clock, Shield, Server, Filter, MoreVertical, RefreshCw,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { useAlerts, useAcknowledgeAlert, useResolveAlert } from "@/hooks/use-api";
import { toast } from "sonner";
import { formatDistanceToNow } from "date-fns";
import type { Alert } from "@/types";

const severityConfig = {
  critical: { icon: AlertCircle, color: "text-destructive", badge: "destructive" as const, label: "Critical" },
  warning: { icon: AlertTriangle, color: "text-warning", badge: "warning" as const, label: "Warning" },
  info: { icon: Bell, color: "text-info", badge: "secondary" as const, label: "Info" },
};

const statusConfig = {
  active: { label: "Active", color: "text-destructive" },
  acknowledged: { label: "Acknowledged", color: "text-warning" },
  resolved: { label: "Resolved", color: "text-success" },
};

export default function AlertsPage() {
  const [severity, setSeverity] = useState<string>("all");
  const [acknowledged, setAcknowledged] = useState<string>("all");
  const { data: alertsRaw, isLoading, refetch } = useAlerts();
  const alerts = alertsRaw?.filter((a) => {
    if (severity !== "all" && a.severity !== severity) return false;
    if (acknowledged === "acknowledged" && a.status !== "acknowledged") return false;
    if (acknowledged === "unacknowledged" && a.status === "acknowledged") return false;
    return true;
  });
  const acknowledge = useAcknowledgeAlert();
  const resolve = useResolveAlert();

  const handleAcknowledge = async (id: string) => {
    try {
      await acknowledge.mutateAsync(id);
      toast.success("Alert acknowledged.");
    } catch (e: any) {
      toast.error("Failed to acknowledge alert", { description: e.message });
    }
  };

  const handleResolve = async (id: string) => {
    try {
      await resolve.mutateAsync(id);
      toast.success("Alert resolved.");
    } catch (e: any) {
      toast.error("Failed to resolve alert", { description: e.message });
    }
  };

  const activeCount = alerts?.filter((a) => a.status === "active").length ?? 0;
  const criticalCount = alerts?.filter((a) => a.severity === "critical" && a.status === "active").length ?? 0;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Alerts</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {activeCount} active · {criticalCount} critical
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => refetch()} className="gap-1.5">
          <RefreshCw className="h-4 w-4" />
          Refresh
        </Button>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-3 gap-4">
        {[
          { label: "Active", value: alerts?.filter(a => a.status === "active").length ?? 0, icon: Bell, color: "text-destructive" },
          { label: "Acknowledged", value: alerts?.filter(a => a.status === "acknowledged").length ?? 0, icon: Clock, color: "text-warning" },
          { label: "Resolved (Today)", value: alerts?.filter(a => a.status === "resolved").length ?? 0, icon: CheckCircle, color: "text-success" },
        ].map(({ label, value, icon: Icon, color }) => (
          <Card key={label}>
            <CardContent className="pt-6">
              <div className="flex items-center gap-3">
                <Icon className={`h-8 w-8 ${color}`} />
                <div>
                  <p className="text-2xl font-bold">{value}</p>
                  <p className="text-sm text-muted-foreground">{label}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Filters */}
      <div className="flex gap-3">
        <Select value={severity} onValueChange={setSeverity}>
          <SelectTrigger className="w-40">
            <Filter className="h-4 w-4 mr-2" />
            <SelectValue placeholder="Severity" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Severities</SelectItem>
            <SelectItem value="critical">Critical</SelectItem>
            <SelectItem value="warning">Warning</SelectItem>
            <SelectItem value="info">Info</SelectItem>
          </SelectContent>
        </Select>
        <Select value={acknowledged} onValueChange={setAcknowledged}>
          <SelectTrigger className="w-48">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Status</SelectItem>
            <SelectItem value="unacknowledged">Unacknowledged</SelectItem>
            <SelectItem value="acknowledged">Acknowledged</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Alert list */}
      <div className="space-y-3">
        {isLoading
          ? Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-24 w-full rounded-lg" />
            ))
          : alerts?.length === 0
          ? (
            <Card>
              <CardContent className="py-16 text-center">
                <Shield className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                <p className="text-lg font-medium">No alerts</p>
                <p className="text-sm text-muted-foreground">Everything looks healthy!</p>
              </CardContent>
            </Card>
          )
          : alerts?.map((alert) => {
            const sev = severityConfig[alert.severity as keyof typeof severityConfig] ?? severityConfig.info;
            const SevIcon = sev.icon;
            return (
              <motion.div
                key={alert.id}
                initial={{ opacity: 0, y: 4 }}
                animate={{ opacity: 1, y: 0 }}
              >
                <Card className={alert.status === "resolved" ? "opacity-60" : ""}>
                  <CardContent className="py-4 flex items-start gap-4">
                    <SevIcon className={`h-5 w-5 mt-0.5 flex-shrink-0 ${sev.color}`} />
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="font-medium">{alert.name}</span>
                        <Badge variant={sev.badge}>{sev.label}</Badge>
                        <Badge variant="outline">{statusConfig[alert.status as keyof typeof statusConfig]?.label ?? alert.status}</Badge>
                      </div>
                      {alert.conditions?.length > 0 && (
                        <p className="text-sm text-muted-foreground mt-1">
                          Condition: {alert.conditions[0].metric} {alert.conditions[0].operator} {alert.conditions[0].threshold}
                        </p>
                      )}
                      <p className="text-xs text-muted-foreground mt-1">
                        Triggered {alert.triggeredAt ? formatDistanceToNow(new Date(alert.triggeredAt), { addSuffix: true }) : "—"}
                      </p>
                    </div>
                    {alert.status === "active" && (
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" className="h-8 w-8">
                            <MoreVertical className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => handleAcknowledge(alert.id)}>
                            <Clock className="h-4 w-4 mr-2" />
                            Acknowledge
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={() => handleResolve(alert.id)}>
                            <CheckCircle className="h-4 w-4 mr-2" />
                            Resolve
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    )}
                  </CardContent>
                </Card>
              </motion.div>
            );
          })}
      </div>
    </div>
  );
}
