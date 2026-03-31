"use client";

import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  Legend,
} from "recharts";
import { useState, useMemo } from "react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import { useDashboardStats, type DailyDeploymentStat } from "@/hooks/use-api";

type TimeRange = "7d" | "14d" | "30d";

function formatDateLabel(dateStr: string): string {
  // dateStr is "yyyy-MM-dd"; display as "Mar 6"
  try {
    const d = new Date(dateStr + "T00:00:00Z");
    return d.toLocaleDateString("en-US", { month: "short", day: "numeric", timeZone: "UTC" });
  } catch {
    return dateStr;
  }
}

export function DeploymentTrend() {
  const [timeRange, setTimeRange] = useState<TimeRange>("14d");
  const { data: stats, isLoading } = useDashboardStats();

  const displayData = useMemo(() => {
    const raw: DailyDeploymentStat[] = stats?.deploymentTrend ?? [];
    // Sort ascending by date
    const sorted = [...raw].sort((a, b) => a.date.localeCompare(b.date));
    const days = timeRange === "7d" ? 7 : timeRange === "14d" ? 14 : 30;
    const sliced = sorted.slice(-days);
    return sliced.map((d) => ({
      date: formatDateLabel(d.date),
      successful: d.successful,
      failed: d.failed,
      cancelled: d.cancelled,
    }));
  }, [stats, timeRange]);

  const totalSuccessful = displayData.reduce((s, d) => s + d.successful, 0);
  const totalFailed     = displayData.reduce((s, d) => s + d.failed, 0);

  return (
    <Card className="glass-card">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-sm font-semibold">Deployment Activity</CardTitle>
          <CardDescription className="mt-0.5 text-xs">
            Deployments over the last {timeRange}
            {!isLoading && displayData.length > 0 && (
              <span className="ml-2 text-muted-foreground/70">
                · {totalSuccessful} succeeded · {totalFailed} failed
              </span>
            )}
          </CardDescription>
        </div>
        <div className="flex gap-0.5 rounded-lg bg-muted/50 p-0.5">
          {(["7d", "14d", "30d"] as TimeRange[]).map((r) => (
            <Button
              key={r}
              variant="ghost"
              size="sm"
              className={cn(
                "h-7 px-2.5 text-xs font-medium rounded-md",
                timeRange === r
                  ? "bg-background text-foreground shadow-sm"
                  : "text-muted-foreground hover:text-foreground"
              )}
              onClick={() => setTimeRange(r)}
            >
              {r}
            </Button>
          ))}
        </div>
      </CardHeader>
      <CardContent className="pb-4">
        {isLoading ? (
          <div className="space-y-2">
            <Skeleton className="h-[220px] w-full" />
          </div>
        ) : displayData.length === 0 ? (
          <div className="flex h-[220px] items-center justify-center text-sm text-muted-foreground">
            No deployment data for this period
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={displayData} barGap={2} barCategoryGap="30%">
              <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" vertical={false} />
              <XAxis
                dataKey="date"
                tick={{ fontSize: 11, fill: "hsl(var(--muted-foreground))" }}
                axisLine={false}
                tickLine={false}
              />
              <YAxis
                tick={{ fontSize: 11, fill: "hsl(var(--muted-foreground))" }}
                axisLine={false}
                tickLine={false}
                width={28}
                allowDecimals={false}
              />
              <Tooltip
                contentStyle={{
                  backgroundColor: "hsl(var(--card))",
                  border: "1px solid hsl(var(--border))",
                  borderRadius: "8px",
                  fontSize: 12,
                  boxShadow: "0 4px 12px rgb(0 0 0 / 0.15)",
                }}
              />
              <Legend
                wrapperStyle={{ fontSize: 11, paddingTop: 12 }}
                formatter={(v: string) => v.charAt(0).toUpperCase() + v.slice(1)}
              />
              <Bar dataKey="successful" fill="hsl(var(--success))" radius={[3, 3, 0, 0]} />
              <Bar dataKey="failed"     fill="hsl(var(--destructive))" radius={[3, 3, 0, 0]} />
              <Bar dataKey="cancelled"  fill="hsl(var(--muted-foreground))" radius={[3, 3, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  );
}

