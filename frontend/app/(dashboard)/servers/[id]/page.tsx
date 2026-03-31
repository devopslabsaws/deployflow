"use client";

import Link from "next/link";
import { useState } from "react";
import {
  ArrowLeft, Server, Cpu, MemoryStick, HardDrive, Activity,
  Terminal, Settings, Wifi, WifiOff, RefreshCw, Package, Cloud, Loader2,
  AlertTriangle, ScrollText,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useServer, useServerMetrics, useServerContainers, useServerLogs, queryKeys } from "@/hooks/use-api";
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
  const { data: containers, isLoading: containersLoading, isError: containersError, refetch: refetchContainers } =
    useServerContainers(id, activeTab === "containers");

  const { data: logLines, isLoading: logsLoading, isFetching: logsFetching, isError: logsError, refetch: refetchLogs, dataUpdatedAt: logsUpdatedAt } =
    useServerLogs(id, activeTab === "logs");

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
      if (!res.ok) {
        setTestResult("fail");
      } else {
        // Backend returns HTTP 200 with { data: true/false } —
        // true = SSH reachable, false = server offline/unreachable.
        // Must check the payload, not just the HTTP status code.
        const json = await res.json().catch(() => ({ data: false }));
        setTestResult(json?.data === true ? "ok" : "fail");
      }
      // Refresh server card so status dot updates immediately
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

  const cfg = statusConfig[
    testResult === "ok" ? "online" :
    testResult === "fail" ? "error" :
    server.status
  ] ?? statusConfig.offline;
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
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 w-7 p-0"
                  onClick={() => refetchContainers()}
                  disabled={containersLoading}
                >
                  <RefreshCw className={cn("w-3.5 h-3.5", containersLoading && "animate-spin")} />
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              {containersLoading ? (
                <div className="space-y-2">
                  {[...Array(3)].map((_, i) => <Skeleton key={i} className="h-10 w-full" />)}
                </div>
              ) : containersError ? (
                /* SSH exec failed — server may actually be offline */
                <div className="py-10 text-center space-y-3">
                  <AlertTriangle className="w-9 h-9 text-destructive/50 mx-auto" />
                  <div className="space-y-1">
                    <p className="font-medium text-sm text-destructive">Could not connect to server</p>
                    <p className="text-xs text-muted-foreground">
                      The SSH exec command failed. The server may be stopped or unreachable.
                      Check the server is running and click <strong>Test Connection</strong> above.
                    </p>
                  </div>
                  <Button
                    size="sm"
                    variant="outline"
                    className="gap-1.5 text-xs h-7"
                    onClick={() => refetchContainers()}
                  >
                    <RefreshCw className="h-3 w-3" /> Retry
                  </Button>
                </div>
              ) : !containers?.length ? (
                <div className="py-10 text-center">
                  <Package className="w-10 h-10 text-muted-foreground/30 mx-auto mb-3" />
                  <p className="font-medium text-sm">No running containers</p>
                  <p className="text-xs text-muted-foreground mt-1">
                    No Docker containers are currently running on this server.
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
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between flex-wrap gap-2">
                <div className="flex items-center gap-2">
                  <CardTitle className="text-sm">Server Logs</CardTitle>
                  {/* Live poll indicator */}
                  {activeTab === "logs" && !logsError && (
                    <Badge
                      variant="outline"
                      className={cn(
                        "gap-1 text-[10px] h-5",
                        logsFetching
                          ? "border-blue-500/40 text-blue-400"
                          : "border-emerald-500/40 text-emerald-500"
                      )}
                    >
                      <div className={cn(
                        "h-1.5 w-1.5 rounded-full",
                        logsFetching ? "bg-blue-400 animate-pulse" : "bg-emerald-500 animate-pulse"
                      )} />
                      {logsFetching ? "Fetching…" : "Live (10s)"}
                    </Badge>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  {logsUpdatedAt > 0 && (
                    <span className="text-[10px] text-muted-foreground">
                      Updated {formatRelativeTime(new Date(logsUpdatedAt).toISOString())}
                    </span>
                  )}
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 w-7 p-0"
                    onClick={() => refetchLogs()}
                    disabled={logsLoading || logsFetching}
                  >
                    <RefreshCw className={cn("w-3.5 h-3.5", (logsLoading || logsFetching) && "animate-spin")} />
                  </Button>
                </div>
              </div>
            </CardHeader>
            <CardContent className="p-0">
              {logsLoading ? (
                <div className="p-4 space-y-1.5">
                  {[...Array(8)].map((_, i) => (
                    <Skeleton key={i} className={cn("h-4", i % 3 === 0 ? "w-full" : i % 2 === 0 ? "w-3/4" : "w-5/6")} />
                  ))}
                </div>
              ) : logsError ? (
                <div className="py-12 text-center space-y-3 px-4">
                  <AlertTriangle className="w-9 h-9 text-destructive/50 mx-auto" />
                  <div className="space-y-1">
                    <p className="font-medium text-sm text-destructive">Cannot fetch server logs</p>
                    <p className="text-xs text-muted-foreground">
                      The SSH connection failed. Ensure the server is running and SSH access is configured.
                    </p>
                  </div>
                  <Button size="sm" variant="outline" className="gap-1.5 text-xs h-7" onClick={() => refetchLogs()}>
                    <RefreshCw className="h-3 w-3" /> Retry
                  </Button>
                </div>
              ) : !logLines?.length ? (
                <div className="py-12 text-center">
                  <ScrollText className="w-9 h-9 text-muted-foreground/30 mx-auto mb-3" />
                  <p className="text-sm text-muted-foreground">No log output received.</p>
                </div>
              ) : (
                <ScrollArea className="h-[480px] rounded-b-lg bg-[#0d1117] font-mono text-xs">
                  <div className="p-4 space-y-0.5">
                    {logLines.map((line, i) => {
                      const isSectionHeader = line.message.startsWith("---");
                      const isError = /error|failed|crit/i.test(line.message);
                      const isWarn  = /warn|notice/i.test(line.message);
                      return (
                        <div
                          key={i}
                          className={cn(
                            "flex gap-3 py-0.5 leading-relaxed",
                            isSectionHeader ? "text-cyan-400 font-semibold mt-3 first:mt-0" :
                            isError  ? "text-red-400" :
                            isWarn   ? "text-yellow-400" :
                            "text-zinc-300"
                          )}
                        >
                          {!isSectionHeader && (
                            <span className="text-zinc-600 shrink-0 select-none w-5 text-right tabular-nums">
                              {i + 1}
                            </span>
                          )}
                          {line.timestamp && !isSectionHeader && (
                            <span className="text-zinc-600 shrink-0 select-none w-[76px] tabular-nums truncate">
                              {line.timestamp.replace("T", " ").replace(/\+.*$/, "").slice(0, 19)}
                            </span>
                          )}
                          <span className={cn("flex-1 whitespace-pre-wrap break-all", isSectionHeader && "col-span-3")}>
                            {line.message}
                          </span>
                        </div>
                      );
                    })}
                    {/* Live indicator at end */}
                    <div className="flex items-center gap-1.5 pt-2 mt-1 border-t border-zinc-800">
                      <div className="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse" />
                      <span className="text-[10px] text-zinc-600">Auto-refreshes every 10s</span>
                    </div>
                  </div>
                </ScrollArea>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
