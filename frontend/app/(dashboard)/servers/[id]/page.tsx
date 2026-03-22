"use client";

import Link from "next/link";
import { useState } from "react";
import {
  ArrowLeft, Server, Cpu, MemoryStick, HardDrive, Activity,
  Terminal, Settings, Wifi, WifiOff, RefreshCw, Package, Cloud, Loader2,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useServer, useServerMetrics, useServerContainers, queryKeys } from "@/hooks/use-api";
import { useQueryClient } from "@tanstack/react-query";
import { cn } from "@/lib/utils";
import { formatRelativeTime } from "@/lib/utils";

const statusConfig: Record<string, { label: string; color: string; bg: string }> = {
  online:       { label: "Online",       color: "text-emerald-500", bg: "bg-emerald-500" },
  offline:      { label: "Offline",      color: "text-red-500",    bg: "bg-red-500" },
  provisioning: { label: "Provisioning", color: "text-amber-500",  bg: "bg-amber-500 animate-pulse" },
  maintenance:  { label: "Maintenance",  color: "text-blue-500",   bg: "bg-blue-500" },
  error:        { label: "Error",        color: "text-red-500",    bg: "bg-red-500 animate-pulse" },
};

export default function ServerDetailPage({ params }: { params: { id: string } }) {
  const { id } = params;
  const { data: server, isLoading } = useServer(id);
  const { data: metrics } = useServerMetrics(id);
  const queryClient = useQueryClient();
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<"ok" | "fail" | null>(null);
  const [activeTab, setActiveTab] = useState("overview");
  const { data: containers, isLoading: containersLoading, refetch: refetchContainers } =
    useServerContainers(id, activeTab === "containers" && server?.status === "online");

  function getToken() {
    try {
      const raw = localStorage.getItem("deployflow-auth");
      if (raw) return JSON.parse(raw)?.state?.accessToken ?? "";
    } catch {}
    return "";
  }

  const testConnection = async () => {
    setTesting(true);
    setTestResult(null);
    try {
      const res = await fetch(`/proxy/servers/${id}/test`, {
        method: "POST",
        headers: { Authorization: `Bearer ${getToken()}` },
      });
      setTestResult(res.ok ? "ok" : "fail");
      // Refresh server status (Provisioning → Online if SSH succeeded)
      queryClient.invalidateQueries({ queryKey: queryKeys.servers.detail(id) });
    } catch {
      setTestResult("fail");
    } finally {
      setTesting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-4 max-w-4xl">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!server) {
    return (
      <div className="flex flex-col items-center justify-center py-20 text-center">
        <Server className="w-12 h-12 text-muted-foreground/30 mb-4" />
        <p className="text-lg font-semibold">Server not found</p>
        <Link href="/servers"><Button variant="outline" className="mt-4">Back to Servers</Button></Link>
      </div>
    );
  }

  const cfg = statusConfig[server.status] ?? statusConfig.offline;
  const liveMetrics = metrics ?? {
    cpuUsagePercent: server.metrics?.cpuUsagePercent ?? 0,
    memoryUsagePercent: server.metrics?.memoryUsagePercent ?? 0,
    diskUsagePercent: server.metrics?.diskUsagePercent ?? 0,
  };

  return (
    <div className="space-y-6 max-w-5xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link href="/servers">
            <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
              <ArrowLeft className="w-4 h-4" />
            </Button>
          </Link>
          <div className="w-10 h-10 rounded-xl bg-muted flex items-center justify-center">
            <Server className="w-5 h-5 text-muted-foreground" />
          </div>
          <div>
            <h1 className="text-xl font-bold">{server.name}</h1>
            <p className="text-sm text-muted-foreground">{server.ipAddress}</p>
          </div>
          <div className="flex items-center gap-1.5 ml-2">
            <div className={cn("w-2 h-2 rounded-full", cfg.bg)} />
            <span className={cn("text-sm font-medium", cfg.color)}>{cfg.label}</span>
          </div>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={testConnection}
            disabled={testing}
            className={cn(
              testResult === "ok" && "border-emerald-500/50 text-emerald-500",
              testResult === "fail" && "border-red-500/50 text-red-500",
            )}
          >
            {testing
              ? <><Loader2 className="w-4 h-4 mr-1.5 animate-spin" />Testing…</>
              : testResult === "ok"
              ? <><Wifi className="w-4 h-4 mr-1.5" />Connected</>
              : testResult === "fail"
              ? <><WifiOff className="w-4 h-4 mr-1.5" />Unreachable</>
              : <><RefreshCw className="w-4 h-4 mr-1.5" />Test Connection</>
            }
          </Button>
          <Link href={`/servers/${id}/terminal`}>
            <Button variant="outline" size="sm"><Terminal className="w-4 h-4 mr-1.5" />Terminal</Button>
          </Link>
          <Link href={`/servers/${id}/settings`}>
            <Button variant="outline" size="sm"><Settings className="w-4 h-4 mr-1.5" />Settings</Button>
          </Link>
        </div>
      </div>

      <Tabs defaultValue="overview" onValueChange={setActiveTab}>
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="containers">Containers</TabsTrigger>
          <TabsTrigger value="logs">Logs</TabsTrigger>
        </TabsList>

        {/* ── Overview ── */}
        <TabsContent value="overview" className="space-y-4 mt-4">
          {/* Specs row */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {[
              { label: "vCPUs",   value: server.cpu ?? "—",                  icon: Cpu },
              { label: "Memory",  value: `${server.memoryGB ?? "—"} GB`,    icon: MemoryStick },
              { label: "Storage", value: `${server.diskGB ?? "—"} GB`,      icon: HardDrive },
              { label: "Provider", value: server.provider?.toUpperCase() ?? "—", icon: Cloud },
            ].map(({ label, value, icon: Icon }) => (
              <Card key={label} className="glass-card">
                <CardContent className="pt-4 pb-3">
                  <div className="flex items-center gap-2 mb-1">
                    <Icon className="w-4 h-4 text-muted-foreground" />
                    <span className="text-xs text-muted-foreground">{label}</span>
                  </div>
                  <p className="text-lg font-bold">{value}</p>
                </CardContent>
              </Card>
            ))}
          </div>

          {/* Live metrics */}
          <Card className="glass-card">
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between">
                <CardTitle className="text-sm">Live Metrics</CardTitle>
                <RefreshCw className="w-3.5 h-3.5 text-muted-foreground" />
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              {(["CPU", "Memory", "Disk"] as const).map((label, i) => {
                const vals = [liveMetrics.cpuUsagePercent, liveMetrics.memoryUsagePercent, liveMetrics.diskUsagePercent];
                const v = Math.round(vals[i] ?? 0);
                const color = v > 90 ? "bg-red-500" : v > 70 ? "bg-amber-500" : "bg-emerald-500";
                return (
                  <div key={label} className="space-y-1">
                    <div className="flex justify-between text-xs">
                      <span className="text-muted-foreground">{label}</span>
                      <span className="font-medium">{v}%</span>
                    </div>
                    <div className="h-2 bg-muted rounded-full overflow-hidden">
                      <div className={cn("h-full rounded-full transition-all", color)} style={{ width: `${v}%` }} />
                    </div>
                  </div>
                );
              })}
            </CardContent>
          </Card>

          {/* Info */}
          <Card className="glass-card">
            <CardHeader className="pb-3"><CardTitle className="text-sm">Server Info</CardTitle></CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 gap-y-3 text-sm">
                {[
                  ["IP Address",       server.ipAddress],
                  ["SSH Port",         server.port ?? 22],
                  ["Region",           server.region ?? "—"],
                  ["OS",               server.os ?? "—"],
                  ["Docker",           server.dockerVersion ?? "—"],
                  ["Last Health Check", server.lastHealthCheckAt ? formatRelativeTime(server.lastHealthCheckAt) : "—"],
                ].map(([k, v]) => (
                  <div key={k as string}>
                    <p className="text-xs text-muted-foreground">{k}</p>
                    <p className="font-medium mt-0.5">{v as string}</p>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Containers ── */}
        <TabsContent value="containers" className="mt-4">
          <Card className="glass-card">
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between">
                <CardTitle className="text-sm">Running Containers</CardTitle>
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0" onClick={() => refetchContainers()}>
                  <RefreshCw className={cn("w-3.5 h-3.5", containersLoading && "animate-spin")} />
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              {containersLoading ? (
                <div className="space-y-2">
                  {[...Array(3)].map((_, i) => <Skeleton key={i} className="h-10 w-full" />)}
                </div>
              ) : !containers?.length ? (
                <div className="py-10 text-center">
                  <Package className="w-10 h-10 text-muted-foreground/30 mx-auto mb-3" />
                  <p className="font-medium text-sm">No running containers</p>
                  <p className="text-xs text-muted-foreground mt-1">
                    {server?.status !== "online" ? "Server must be online to fetch containers." : "No containers are currently running on this server."}
                  </p>
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-xs">
                    <thead>
                      <tr className="border-b border-border/50 text-muted-foreground">
                        <th className="pb-2 text-left font-medium">Name</th>
                        <th className="pb-2 text-left font-medium">Image</th>
                        <th className="pb-2 text-left font-medium">Status</th>
                        <th className="pb-2 text-left font-medium">Ports</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-border/30">
                      {containers.map((c) => (
                        <tr key={c.id} className="hover:bg-muted/30 transition-colors">
                          <td className="py-2.5 pr-4 font-mono font-medium">{c.name}</td>
                          <td className="py-2.5 pr-4 text-muted-foreground">{c.image}</td>
                          <td className="py-2.5 pr-4">
                            <Badge
                              variant="outline"
                              className={cn(
                                "text-[10px] h-5",
                                c.status.toLowerCase().includes("up") ? "border-emerald-500/50 text-emerald-500" : "border-amber-500/50 text-amber-500"
                              )}
                            >
                              {c.status}
                            </Badge>
                          </td>
                          <td className="py-2.5 font-mono text-muted-foreground">
                            {c.ports || "—"}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Logs ── */}
        <TabsContent value="logs" className="mt-4">
          <Card className="glass-card">
            <CardContent className="py-12 text-center">
              <Activity className="w-10 h-10 text-muted-foreground/30 mx-auto mb-3" />
              <p className="font-medium">Server logs</p>
              <p className="text-sm text-muted-foreground mt-1">Live log streaming is available when the server is online.</p>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
