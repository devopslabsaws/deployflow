"use client";

import { useState } from "react";
import {
  TrendingUp,
  CheckCircle2,
  XCircle,
  Clock,
  Zap,
  BarChart3,
  AlertTriangle,
  Lightbulb,
  ChevronDown,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useDeploymentInsights, useDeploymentErrors, type ErrorSuggestionDto } from "@/hooks/use-api";
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Legend,
} from "recharts";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";

const SEVERITY_CONFIG: Record<string, { color: string; icon: React.ComponentType<{ className?: string }> }> = {
  error: { color: "text-red-500", icon: XCircle },
  warning: { color: "text-yellow-500", icon: AlertTriangle },
  info: { color: "text-blue-500", icon: Lightbulb },
};

const TRIGEER_COLORS = ["#6366f1", "#22c55e", "#f59e0b", "#ef4444", "#8b5cf6"];

function StatCard({
  label,
  value,
  sub,
  icon: Icon,
  color = "text-primary",
}: {
  label: string;
  value: string | number;
  sub?: string;
  icon: React.ComponentType<{ className?: string }>;
  color?: string;
}) {
  return (
    <Card>
      <CardContent className="pt-5 pb-4">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-muted-foreground">{label}</p>
            <p className={cn("text-2xl font-bold mt-0.5", color)}>{value}</p>
            {sub && <p className="text-xs text-muted-foreground mt-0.5">{sub}</p>}
          </div>
          <Icon className={cn("w-8 h-8 opacity-60", color)} />
        </div>
      </CardContent>
    </Card>
  );
}

function ErrorCard({ suggestion }: { suggestion: ErrorSuggestionDto }) {
  const cfg = SEVERITY_CONFIG[suggestion.severity] ?? SEVERITY_CONFIG.info;
  const Icon = cfg.icon;
  return (
    <div className="border rounded-lg p-4 space-y-1">
      <div className="flex items-center gap-2">
        <Icon className={cn("w-4 h-4", cfg.color)} />
        <span className="font-semibold text-sm">{suggestion.title}</span>
        <Badge variant="outline" className="ml-auto text-xs">
          {suggestion.category}
        </Badge>
      </div>
      <p className="text-sm text-muted-foreground">{suggestion.description}</p>
      <div className="bg-muted rounded px-3 py-2 mt-2">
        <p className="text-xs font-medium">Fix suggestion</p>
        <p className="text-sm mt-0.5">{suggestion.fix}</p>
      </div>
    </div>
  );
}

export default function InsightsPage() {
  const [period, setPeriod] = useState("30d");
  const [analyzeDeploymentId, setAnalyzeDeploymentId] = useState("");
  const [submitId, setSubmitId] = useState<string | null>(null);

  const { data: insights, isLoading, isError, refetch } = useDeploymentInsights(period);
  const { data: errors, isLoading: errorsLoading } = useDeploymentErrors(submitId);

  const dailyStats = insights?.dailyStats ?? [];
  const triggerData = Object.entries(insights?.deploysByTrigger ?? {}).map(([name, value]) => ({
    name,
    value: value as number,
  }));

  return (
    <div className="space-y-6 p-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <TrendingUp className="w-6 h-6 text-primary" />
            Deployment Insights
          </h1>
          <p className="text-muted-foreground mt-1 text-sm">
            Trends, success rates, and smart error intelligence.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => refetch()} className="gap-1.5 h-9">
            <ChevronDown className="h-4 w-4 rotate-180" />Refresh
          </Button>
        <Select value={period} onValueChange={setPeriod}>
          <SelectTrigger className="w-36">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {[
              { value: "7d", label: "Last 7 days" },
              { value: "14d", label: "Last 14 days" },
              { value: "30d", label: "Last 30 days" },
              { value: "90d", label: "Last 90 days" },
            ].map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        </div>
      </div>

      {/* Error state */}
      {isError && (
        <div className="flex items-center gap-3 rounded-lg border border-destructive/40 bg-destructive/5 px-4 py-3.5">
          <AlertTriangle className="h-4 w-4 text-destructive shrink-0" />
          <p className="text-sm text-destructive flex-1">Failed to load insights data. The API may be unreachable.</p>
          <Button size="sm" variant="outline" onClick={() => refetch()}>Retry</Button>
        </div>
      )}

      {/* Summary Stats */}
      {isLoading ? (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {[1, 2, 3, 4].map((i) => (
            <Skeleton key={i} className="h-24 rounded-lg" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <StatCard
            label="Total Deploys"
            value={insights?.totalDeploys ?? 0}
            sub={insights?.periodLabel}
            icon={BarChart3}
          />
          <StatCard
            label="Success Rate"
            value={`${insights?.successRate ?? 0}%`}
            sub={`${insights?.successCount ?? 0} succeeded`}
            icon={CheckCircle2}
            color={
              (insights?.successRate ?? 0) >= 80
                ? "text-green-500"
                : (insights?.successRate ?? 0) >= 50
                ? "text-yellow-500"
                : "text-red-500"
            }
          />
          <StatCard
            label="Avg Build Time"
            value={
              insights?.avgDurationSeconds
                ? `${Math.round(insights.avgDurationSeconds)}s`
                : "—"
            }
            sub={
              insights?.fastestDeploy
                ? `Fastest: ${insights.fastestDeploy}s`
                : undefined
            }
            icon={Clock}
          />
          <StatCard
            label="Failed Deploys"
            value={insights?.failedCount ?? 0}
            icon={XCircle}
            color={(insights?.failedCount ?? 0) > 0 ? "text-red-500" : "text-green-500"}
          />
        </div>
      )}

      {/* Daily Trend Chart */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Daily Deployment Activity</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <Skeleton className="h-64 w-full" />
          ) : dailyStats.length === 0 ? (
            <div className="h-64 flex items-center justify-center text-muted-foreground text-sm">
              No deployment data for this period.
            </div>
          ) : (
            <ResponsiveContainer width="100%" height={260}>
              <LineChart data={dailyStats}>
                <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
                <XAxis
                  dataKey="date"
                  tick={{ fontSize: 11 }}
                  tickFormatter={(v) => v.slice(5)}
                />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip
                  contentStyle={{ fontSize: 12 }}
                  formatter={(v, n) => [v, n === "total" ? "Total" : n === "succeeded" ? "Success" : "Failed"]}
                />
                <Line type="monotone" dataKey="total" stroke="#6366f1" strokeWidth={2} dot={false} name="total" />
                <Line type="monotone" dataKey="succeeded" stroke="#22c55e" strokeWidth={2} dot={false} name="succeeded" />
                <Line type="monotone" dataKey="failed" stroke="#ef4444" strokeWidth={2} dot={false} name="failed" />
              </LineChart>
            </ResponsiveContainer>
          )}
        </CardContent>
      </Card>

      {/* Bottom row */}
      <div className="grid md:grid-cols-2 gap-6">
        {/* Deploys by Trigger */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Deploys by Trigger</CardTitle>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-48 w-full" />
            ) : triggerData.length === 0 ? (
              <div className="h-48 flex items-center justify-center text-muted-foreground text-sm">
                No data
              </div>
            ) : (
              <ResponsiveContainer width="100%" height={200}>
                <PieChart>
                  <Pie data={triggerData} cx="50%" cy="50%" outerRadius={70} dataKey="value" label>
                    {triggerData.map((_, idx) => (
                      <Cell key={idx} fill={TRIGEER_COLORS[idx % TRIGEER_COLORS.length]} />
                    ))}
                  </Pie>
                  <Tooltip contentStyle={{ fontSize: 12 }} />
                  <Legend iconSize={10} wrapperStyle={{ fontSize: 12 }} />
                </PieChart>
              </ResponsiveContainer>
            )}
          </CardContent>
        </Card>

        {/* Smart Error Analysis */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Lightbulb className="w-4 h-4 text-yellow-500" />
              Smart Error Analysis
            </CardTitle>
            <CardDescription>
              Enter a deployment ID to get AI-powered fix suggestions.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex gap-2">
              <Input
                placeholder="Deployment ID (UUID)"
                value={analyzeDeploymentId}
                onChange={(e) => setAnalyzeDeploymentId(e.target.value)}
                className="font-mono text-sm"
              />
              <Button
                variant="outline"
                onClick={() => setSubmitId(analyzeDeploymentId || null)}
                disabled={!analyzeDeploymentId}
              >
                <Zap className="w-4 h-4" />
              </Button>
            </div>

            {errorsLoading && <Skeleton className="h-24 w-full" />}

            {errors && errors.length === 0 && (
              <div className="text-sm text-muted-foreground text-center py-4">
                No error patterns detected in this deployment.
              </div>
            )}

            {errors && errors.length > 0 && (
              <div className="space-y-2 max-h-72 overflow-y-auto pr-1">
                {errors.map((e, i) => (
                  <ErrorCard key={i} suggestion={e} />
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
