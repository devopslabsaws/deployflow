"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  DollarSign, TrendingUp, TrendingDown, BarChart3, Calendar, Download,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { useCostRecords } from "@/hooks/use-api";
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
  BarChart, Bar, PieChart, Pie, Cell, Legend,
} from "recharts";
import { format, subMonths, startOfMonth } from "date-fns";

const COLORS = ["#4F46E5", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#06B6D4"];

export default function CostsPage() {
  const [period, setPeriod] = useState<string>("3m");
  const { data: costs, isLoading } = useCostRecords({ period });

  const totalCost = costs?.reduce((sum, r) => sum + r.totalCost, 0) ?? 0;

  // Group by resource type for pie chart
  const byType = costs?.reduce((acc: Record<string, number>, r) => {
    acc[r.provider] = (acc[r.provider] ?? 0) + r.totalCost;
    return acc;
  }, {}) ?? {};

  const pieData = Object.entries(byType).map(([name, value]) => ({ name, value }));

  // Group by period for trend
  const byPeriod = costs?.reduce((acc: Record<string, number>, r) => {
    acc[r.period] = (acc[r.period] ?? 0) + r.totalCost;
    return acc;
  }, {}) ?? {};

  const trendData = Object.entries(byPeriod)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([period, amount]) => ({ period, amount }));

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Cost Analytics</h1>
          <p className="text-muted-foreground text-sm mt-0.5">Track resource spending and optimize costs</p>
        </div>
        <div className="flex gap-2">
          <Select value={period} onValueChange={setPeriod}>
            <SelectTrigger className="w-36">
              <Calendar className="h-4 w-4 mr-2" />
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="1m">Last month</SelectItem>
              <SelectItem value="3m">Last 3 months</SelectItem>
              <SelectItem value="6m">Last 6 months</SelectItem>
              <SelectItem value="1y">Last year</SelectItem>
            </SelectContent>
          </Select>
          <Button variant="outline" size="icon">
            <Download className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-3 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center gap-3">
              <DollarSign className="h-8 w-8 text-primary" />
              <div>
                <p className="text-2xl font-bold">${totalCost.toFixed(2)}</p>
                <p className="text-sm text-muted-foreground">Total Period Cost</p>
              </div>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center gap-3">
              <BarChart3 className="h-8 w-8 text-violet-500" />
              <div>
                <p className="text-2xl font-bold">{costs?.length ?? 0}</p>
                <p className="text-sm text-muted-foreground">Line Items</p>
              </div>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center gap-3">
              <TrendingUp className="h-8 w-8 text-success" />
              <div>
                <p className="text-2xl font-bold">
                  ${trendData.length > 1 ? (trendData[trendData.length - 1].amount).toFixed(2) : "—"}
                </p>
                <p className="text-sm text-muted-foreground">Current Month</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Charts */}
      <div className="grid grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-medium">Cost Trend</CardTitle>
          </CardHeader>
          <CardContent>
            {isLoading
              ? <Skeleton className="h-48 w-full" />
              : (
                <ResponsiveContainer width="100%" height={192}>
                  <AreaChart data={trendData}>
                    <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
                    <XAxis dataKey="period" className="text-xs" />
                    <YAxis className="text-xs" tickFormatter={(v) => `$${v}`} />
                    <Tooltip formatter={(v: number) => [`$${v.toFixed(2)}`, "Cost"]} />
                    <Area type="monotone" dataKey="amount" stroke="#4F46E5" fill="#4F46E5" fillOpacity={0.1} />
                  </AreaChart>
                </ResponsiveContainer>
              )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-medium">Cost by Resource Type</CardTitle>
          </CardHeader>
          <CardContent>
            {isLoading
              ? <Skeleton className="h-48 w-full" />
              : pieData.length > 0
              ? (
                <ResponsiveContainer width="100%" height={192}>
                  <PieChart>
                    <Pie data={pieData} cx="50%" cy="50%" outerRadius={70} dataKey="value" label={({ name, percent }) => `${name} ${(percent * 100).toFixed(0)}%`} labelLine={false}>
                      {pieData.map((_, i) => (
                        <Cell key={i} fill={COLORS[i % COLORS.length]} />
                      ))}
                    </Pie>
                    <Tooltip formatter={(v: number) => `$${v.toFixed(2)}`} />
                  </PieChart>
                </ResponsiveContainer>
              )
              : (
                <div className="h-48 flex items-center justify-center text-muted-foreground">
                  No cost data available
                </div>
              )}
          </CardContent>
        </Card>
      </div>

      {/* Cost breakdown table */}
      {costs && costs.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-medium">Cost Breakdown</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {costs.slice(0, 10).map((r) => (
                <div key={r.id} className="flex items-center justify-between py-2 border-b last:border-0">
                  <div>
                    <p className="text-sm font-medium">{r.provider}</p>
                    <p className="text-xs text-muted-foreground">{r.currency} · {r.period}</p>
                  </div>
                  <span className="font-medium">${r.totalCost.toFixed(2)}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
