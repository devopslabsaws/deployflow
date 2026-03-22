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
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const mockData = [
  { date: "Mar 6",  successful: 18, failed: 2, cancelled: 1 },
  { date: "Mar 7",  successful: 22, failed: 1, cancelled: 0 },
  { date: "Mar 8",  successful: 15, failed: 3, cancelled: 1 },
  { date: "Mar 9",  successful: 27, failed: 0, cancelled: 0 },
  { date: "Mar 10", successful: 12, failed: 2, cancelled: 2 },
  { date: "Mar 11", successful: 20, failed: 1, cancelled: 0 },
  { date: "Mar 12", successful: 25, failed: 0, cancelled: 1 },
  { date: "Mar 13", successful: 19, failed: 2, cancelled: 0 },
  { date: "Mar 14", successful: 23, failed: 1, cancelled: 0 },
  { date: "Mar 15", successful: 16, failed: 3, cancelled: 1 },
  { date: "Mar 16", successful: 28, failed: 0, cancelled: 0 },
  { date: "Mar 17", successful: 21, failed: 2, cancelled: 0 },
  { date: "Mar 18", successful: 17, failed: 1, cancelled: 1 },
  { date: "Mar 19", successful: 24, failed: 0, cancelled: 0 },
];

type TimeRange = "7d" | "14d" | "30d";

export function DeploymentTrend() {
  const [timeRange, setTimeRange] = useState<TimeRange>("14d");

  const displayData =
    timeRange === "7d"
      ? mockData.slice(-7)
      : timeRange === "14d"
      ? mockData
      : mockData;

  return (
    <Card className="glass-card">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-sm font-semibold">Deployment Activity</CardTitle>
          <CardDescription className="mt-0.5 text-xs">
            Deployments over the last {timeRange}
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
              formatter={(v: string) =>
                v.charAt(0).toUpperCase() + v.slice(1)
              }
            />
            <Bar dataKey="successful" fill="hsl(var(--success))" radius={[3, 3, 0, 0]} />
            <Bar dataKey="failed" fill="hsl(var(--destructive))" radius={[3, 3, 0, 0]} />
            <Bar dataKey="cancelled" fill="hsl(var(--muted-foreground))" radius={[3, 3, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  );
}
