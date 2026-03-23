"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Activity,
  AlertTriangle,
  RefreshCw,
  Clock,
  Cpu,
  MemoryStick,
  HardDrive,
  Network,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  AreaChart,
  Area,
  CartesianGrid,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  LineChart,
  Line,
} from "recharts";
import { useServers, useAlerts, useMonitoringSummary, useMonitoringTimeSeries, useMonitoringNetwork } from "@/hooks/use-api";
import { cn } from "@/lib/utils";

const severityConfig = {
  info: "bg-info/10 text-info border-info/30",
  warning: "bg-warning/10 text-warning border-warning/30",
  critical: "bg-destructive/10 text-destructive border-destructive/30",
};

export default function MonitoringPage() {
  const [selectedServer, setSelectedServer] = useState<string>("all");
  const [timeRange, setTimeRange] = useState("1h");

  const serverId = selectedServer === "all" ? undefined : selectedServer;

  const { data: servers, refetch: refetchServers } = useServers();
  const { data: alerts, refetch: refetchAlerts } = useAlerts();
  const { data: summary, refetch: refetchSummary } = useMonitoringSummary(serverId, timeRange);
  const { data: cpuSeries, refetch: refetchCpu } = useMonitoringTimeSeries("cpu", serverId, timeRange);
  const { data: memSeries, refetch: refetchMem } = useMonitoringTimeSeries("memory", serverId, timeRange);
  const { data: diskSeries, refetch: refetchDisk } = useMonitoringTimeSeries("disk", serverId, timeRange);
  const { data: netSeries, refetch: refetchNet } = useMonitoringNetwork(serverId, timeRange);

  const activeAlerts = alerts?.filter((a) => a.status === "active") ?? [];

  const cpuData = (cpuSeries ?? []).map((p) => ({
    time: new Date(p.timestamp).toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit", hour12: false }),
    value: p.value,
  }));
  const memData = (memSeries ?? []).map((p) => ({
    time: new Date(p.timestamp).toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit", hour12: false }),
    value: p.value,
  }));
  const diskData = (diskSeries ?? []).map((p) => ({
    time: new Date(p.timestamp).toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit", hour12: false }),
    value: p.value,
  }));
  const networkData = (netSeries ?? []).map((p) => ({
    time: new Date(p.timestamp).toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit", hour12: false }),
    in: p.inbound,
    out: p.outbound,
  }));

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Monitoring</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            Real-time infrastructure metrics & alerting
          </p>
        </div>
        <div className="flex gap-2">
          <Select value={selectedServer} onValueChange={setSelectedServer}>
            <SelectTrigger className="w-44">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Servers</SelectItem>
              {servers?.map((s) => (
                <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={timeRange} onValueChange={setTimeRange}>
            <SelectTrigger className="w-24">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="15m">15m</SelectItem>
              <SelectItem value="1h">1h</SelectItem>
              <SelectItem value="6h">6h</SelectItem>
              <SelectItem value="24h">24h</SelectItem>
            </SelectContent>
          </Select>
          <Button
            variant="outline"
            size="sm"
            className="gap-1.5"
            onClick={() => {
              refetchServers();
              refetchAlerts();
              refetchSummary();
              refetchCpu();
              refetchMem();
              refetchDisk();
              refetchNet();
            }}
          >
            <RefreshCw className="w-3.5 h-3.5" />Refresh
          </Button>
        </div>
      </div>

      {/* Alert Summary */}
      {activeAlerts.length > 0 && (
        <div className="flex items-center gap-3 p-3 rounded-lg bg-warning/10 border border-warning/30">
          <AlertTriangle className="w-5 h-5 text-warning shrink-0" />
          <p className="text-sm">
            <span className="font-semibold">{activeAlerts.length} active alert{activeAlerts.length > 1 ? "s" : ""}</span>
            {" — "}
            {activeAlerts.filter(a => a.severity === "critical").length} critical,{" "}
            {activeAlerts.filter(a => a.severity === "warning").length} warning
          </p>
          <Button variant="outline" size="sm" className="ml-auto h-7 text-xs" asChild>
            <a href="/alerts">View All</a>
          </Button>
        </div>
      )}

      {/* Quick Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        {[
          {
            label: "Avg CPU",
            value: `${(summary?.avgCpu ?? 0).toFixed(1)}%`,
            icon: Cpu,
            color: "text-blue-500",
            bg: "bg-blue-500/10",
            trend: `${summary?.sampleCount ?? 0} samples`,
            positive: true,
          },
          {
            label: "Avg Memory",
            value: `${(summary?.avgMemory ?? 0).toFixed(1)}%`,
            icon: MemoryStick,
            color: "text-purple-500",
            bg: "bg-purple-500/10",
            trend: `${summary?.sampleCount ?? 0} samples`,
            positive: true,
          },
          {
            label: "Avg Disk",
            value: `${(summary?.avgDisk ?? 0).toFixed(1)}%`,
            icon: HardDrive,
            color: "text-orange-500",
            bg: "bg-orange-500/10",
            trend: `${summary?.sampleCount ?? 0} samples`,
            positive: true,
          },
          {
            label: "Network In",
            value: `${(summary?.avgNetworkInMbps ?? 0).toFixed(1)} MB/s`,
            icon: Network,
            color: "text-success",
            bg: "bg-success/10",
            trend: `${(summary?.avgNetworkOutMbps ?? 0).toFixed(1)} MB/s out`,
            positive: true,
          },
        ].map((stat) => (
          <Card key={stat.label} className="glass-card">
            <CardContent className="p-4">
              <div className="flex items-center gap-2 mb-3">
                <div className={cn("w-8 h-8 rounded-lg flex items-center justify-center", stat.bg)}>
                  <stat.icon className={cn("w-4 h-4", stat.color)} />
                </div>
              </div>
              <p className="text-xl font-bold">{stat.value}</p>
              <div className="flex items-center justify-between mt-1">
                <p className="text-xs text-muted-foreground">{stat.label}</p>
                <span className={cn("text-xs font-medium", stat.positive ? "text-success" : "text-destructive")}>
                  {stat.trend}
                </span>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Charts */}
      <Tabs defaultValue="cpu">
        <TabsList>
          <TabsTrigger value="cpu" className="gap-1.5">
            <Cpu className="w-3.5 h-3.5" />CPU
          </TabsTrigger>
          <TabsTrigger value="memory" className="gap-1.5">
            <MemoryStick className="w-3.5 h-3.5" />Memory
          </TabsTrigger>
          <TabsTrigger value="disk" className="gap-1.5">
            <HardDrive className="w-3.5 h-3.5" />Disk
          </TabsTrigger>
          <TabsTrigger value="network" className="gap-1.5">
            <Network className="w-3.5 h-3.5" />Network
          </TabsTrigger>
        </TabsList>

        <div className="mt-4">
          <TabsContent value="cpu">
            <MetricChart
              title="CPU Usage"
              description="Percentage of CPU utilized over time"
              data={cpuData}
              dataKey="value"
              unit="%"
              color="hsl(var(--primary))"
            />
          </TabsContent>
          <TabsContent value="memory">
            <MetricChart
              title="Memory Usage"
              description="RAM utilization over time"
              data={memData}
              dataKey="value"
              unit="%"
              color="hsl(262, 80%, 65%)"
            />
          </TabsContent>
          <TabsContent value="disk">
            <MetricChart
              title="Disk Usage"
              description="Storage utilization over time"
              data={diskData}
              dataKey="value"
              unit="%"
              color="hsl(var(--warning))"
            />
          </TabsContent>
          <TabsContent value="network">
            <Card className="glass-card">
              <CardHeader>
                <CardTitle className="text-base">Network I/O</CardTitle>
                <CardDescription>Inbound and outbound traffic in MB/s</CardDescription>
              </CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={280}>
                  <LineChart data={networkData}>
                    <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                    <XAxis dataKey="time" tick={{ fontSize: 11, fill: "hsl(var(--muted-foreground))" }} axisLine={false} tickLine={false} />
                    <YAxis tick={{ fontSize: 11, fill: "hsl(var(--muted-foreground))" }} axisLine={false} tickLine={false} width={45} tickFormatter={(v) => `${v}MB`} />
                    <Tooltip
                      contentStyle={{ backgroundColor: "hsl(var(--card))", border: "1px solid hsl(var(--border))", borderRadius: "8px", fontSize: 12 }}
                      formatter={(v: number) => [`${v.toFixed(1)} MB/s`]}
                    />
                    <Line type="monotone" dataKey="in" stroke="hsl(var(--success))" strokeWidth={2} dot={false} name="Inbound" />
                    <Line type="monotone" dataKey="out" stroke="hsl(var(--primary))" strokeWidth={2} dot={false} name="Outbound" />
                  </LineChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          </TabsContent>
        </div>
      </Tabs>

      {/* Active Alerts Table */}
      {activeAlerts.length > 0 && (
        <Card className="glass-card">
          <CardHeader className="pb-3">
            <CardTitle className="text-base flex items-center gap-2">
              <AlertTriangle className="w-4 h-4 text-warning" />
              Active Alerts
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {activeAlerts.map((alert) => (
              <div
                key={alert.id}
                className={cn(
                  "flex items-center justify-between p-3 rounded-lg border text-sm",
                  severityConfig[alert.severity]
                )}
              >
                <div>
                  <p className="font-medium">{alert.name}</p>
                  {alert.description && (
                    <p className="text-xs opacity-80 mt-0.5">{alert.description}</p>
                  )}
                </div>
                <div className="flex items-center gap-2 text-xs">
                  <Clock className="w-3 h-3" />
                  <span>
                    {new Date(alert.triggeredAt).toLocaleTimeString()}
                  </span>
                  <Badge variant="outline" className="capitalize text-[10px]">
                    {alert.severity}
                  </Badge>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function MetricChart({
  title,
  description,
  data,
  dataKey,
  unit,
  color,
}: {
  title: string;
  description: string;
  data: any[];
  dataKey: string;
  unit: string;
  color: string;
}) {
  return (
    <Card className="glass-card">
      <CardHeader>
        <CardTitle className="text-base">{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        <ResponsiveContainer width="100%" height={280}>
          <AreaChart data={data}>
            <defs>
              <linearGradient id={`gradient-${dataKey}`} x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor={color} stopOpacity={0.3} />
                <stop offset="95%" stopColor={color} stopOpacity={0} />
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" vertical={false} />
            <XAxis
              dataKey="time"
              tick={{ fontSize: 11, fill: "hsl(var(--muted-foreground))" }}
              axisLine={false}
              tickLine={false}
            />
            <YAxis
              tick={{ fontSize: 11, fill: "hsl(var(--muted-foreground))" }}
              axisLine={false}
              tickLine={false}
              width={40}
              domain={[0, 100]}
              tickFormatter={(v) => `${v}${unit}`}
            />
            <Tooltip
              contentStyle={{
                backgroundColor: "hsl(var(--card))",
                border: "1px solid hsl(var(--border))",
                borderRadius: "8px",
                fontSize: 12,
              }}
              formatter={(v: number) => [`${v.toFixed(1)}${unit}`, title]}
            />
            <Area
              type="monotone"
              dataKey={dataKey}
              stroke={color}
              strokeWidth={2}
              fill={`url(#gradient-${dataKey})`}
            />
          </AreaChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  );
}
