"use client";

import Link from "next/link";
import { ArrowLeft, Settings, Play, Square, RefreshCw, Loader2, Activity, Box, Cpu, MemoryStick } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api-client";
import { useStartService, useStopService, useRestartService } from "@/hooks/use-api";
import { toast } from "sonner";
import type { Service } from "@/types";

const statusColors: Record<string, string> = {
  running:  "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400",
  stopped:  "bg-slate-100 text-slate-600 dark:bg-slate-500/15 dark:text-slate-400",
  starting: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400",
  error:    "bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-400",
};

export default function ServiceDetailPage({ params }: { params: { id: string } }) {
  const { id } = params;

  const { data: service, isLoading } = useQuery({
    queryKey: ["services", "detail", id],
    queryFn: () => apiClient.get<Service>(`/services/${id}`),
    enabled: !!id,
  });

  const start   = useStartService();
  const stop    = useStopService();
  const restart = useRestartService();

  const handleStart = async () => {
    try { await start.mutateAsync(id); toast.success("Service starting…"); }
    catch (e: any) { toast.error("Failed to start service", { description: e.message }); }
  };

  const handleStop = async () => {
    try { await stop.mutateAsync(id); toast.success("Service stopped."); }
    catch (e: any) { toast.error("Failed to stop service", { description: e.message }); }
  };

  const handleRestart = async () => {
    try { await restart.mutateAsync(id); toast.success("Service restarting…"); }
    catch (e: any) { toast.error("Failed to restart service", { description: e.message }); }
  };

  if (isLoading) {
    return (
      <div className="space-y-4 max-w-4xl">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  if (!service) {
    return (
      <div className="text-center py-20">
        <p className="text-muted-foreground">Service not found.</p>
        <Button variant="outline" size="sm" className="mt-4" asChild>
          <Link href="/services">Back to Services</Link>
        </Button>
      </div>
    );
  }

  const statusCls = statusColors[service.status?.toLowerCase()] ?? statusColors.stopped;

  return (
    <div className="space-y-6 max-w-4xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" asChild>
            <Link href="/services"><ArrowLeft className="h-4 w-4 mr-1" />Back</Link>
          </Button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-bold">{service.name}</h1>
              <Badge className={statusCls}>{service.status}</Badge>
            </div>
            <p className="text-xs text-muted-foreground mt-0.5">{service.type} · {service.image}:{service.tag ?? "latest"}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={handleStart} disabled={start.isPending || service.status === "running"}>
            {start.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Play className="h-3.5 w-3.5" />}
          </Button>
          <Button variant="outline" size="sm" onClick={handleStop} disabled={stop.isPending || service.status === "stopped"}>
            {stop.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Square className="h-3.5 w-3.5" />}
          </Button>
          <Button variant="outline" size="sm" onClick={handleRestart} disabled={restart.isPending}>
            {restart.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <RefreshCw className="h-3.5 w-3.5" />}
          </Button>
        </div>
      </div>

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview"><Activity className="h-3.5 w-3.5 mr-1.5" />Overview</TabsTrigger>
          <TabsTrigger value="settings"><Settings className="h-3.5 w-3.5 mr-1.5" />Settings</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="mt-4 space-y-4">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {[
              { label: "Replicas", value: service.replicas ?? 1, icon: Box },
              { label: "Status",   value: service.status,        icon: Activity },
              { label: "CPU Limit",    value: service.resources?.cpuLimit ?? "–",    icon: Cpu },
              { label: "Memory Limit", value: service.resources?.memoryLimit ?? "–", icon: MemoryStick },
            ].map(({ label, value, icon: Icon }) => (
              <Card key={label}>
                <CardContent className="pt-4 pb-3 flex items-start gap-3">
                  <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary/10">
                    <Icon className="h-4 w-4 text-primary" />
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">{label}</p>
                    <p className="text-sm font-semibold capitalize">{String(value)}</p>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>

          {service.containerId && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm">Container</CardTitle>
              </CardHeader>
              <CardContent>
                <code className="text-xs font-mono text-muted-foreground">{service.containerId}</code>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="settings" className="mt-4 space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Service Configuration</CardTitle>
              <CardDescription className="text-xs">Update image, replicas and resource limits</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label className="text-xs">Image</Label>
                  <Input defaultValue={service.image ?? ""} placeholder="nginx" />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs">Tag</Label>
                  <Input defaultValue={service.tag ?? "latest"} placeholder="latest" />
                </div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-1.5">
                  <Label className="text-xs">Replicas</Label>
                  <Input type="number" defaultValue={service.replicas ?? 1} min={1} />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs">CPU Limit</Label>
                  <Input defaultValue={service.resources?.cpuLimit ?? ""} placeholder="0.5" />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs">Memory Limit</Label>
                  <Input defaultValue={service.resources?.memoryLimit ?? ""} placeholder="512m" />
                </div>
              </div>
              <Button size="sm" onClick={() => toast.info("Settings update requires backend implementation.")}>
                Save Changes
              </Button>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
