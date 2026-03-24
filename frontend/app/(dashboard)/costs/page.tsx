"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  DollarSign, TrendingUp, TrendingDown, BarChart3, Calendar, Download,
  AlertTriangle, Lightbulb, Server, Database, Container,
  Target, ArrowUp, ArrowDown, Minus, Settings2, ChevronRight,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { useCostDashboard, useCostBreakdown, useSetBudget } from "@/hooks/use-api";
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
  PieChart, Pie, Cell, Legend, BarChart, Bar,
} from "recharts";
import { toast } from "sonner";

const CATEGORY_COLORS: Record<string, string> = {
  compute:   "#4F46E5",
  memory:    "#10B981",
  storage:   "#F59E0B",
  database:  "#EF4444",
  container: "#8B5CF6",
  network:   "#06B6D4",
  server:    "#4F46E5",
};

const CATEGORY_ICONS: Record<string, React.ReactNode> = {
  compute:   <Server className="h-4 w-4" />,
  server:    <Server className="h-4 w-4" />,
  database:  <Database className="h-4 w-4" />,
  container: <Container className="h-4 w-4" />,
};

function getCategoryColor(cat: string) {
  return CATEGORY_COLORS[cat.toLowerCase()] ?? "#94A3B8";
}

export default function CostsPage() {
  const [period, setPeriod] = useState<string>("3m");
  const { data: dash, isLoading } = useCostDashboard(period);
  const { data: breakdown, isLoading: breakdownLoading } = useCostBreakdown(period);
  const setBudget = useSetBudget();

  // Budget state
  const [budgetInput, setBudgetInput] = useState("");
  const [alertAt, setAlertAt] = useState("80");
  const [budgetSaved, setBudgetSaved] = useState<number | null>(null);

  const handleSaveBudget = async () => {
    const amount = parseFloat(budgetInput);
    if (!amount || amount <= 0) { toast.error("Enter a valid budget amount."); return; }
    try {
      await setBudget.mutateAsync({ monthlyBudget: amount, alertAt: parseFloat(alertAt) || 80 });
      setBudgetSaved(amount);
      toast.success("Budget saved successfully!");
    } catch (e: any) {
      toast.error("Failed to save budget", { description: e.message });
    }
  };

  const effectiveBudget = budgetSaved ?? null;
  const budgetUtilization = effectiveBudget ? ((dash?.currentMonthCost ?? 0) / effectiveBudget) * 100 : 0;

  const changeIsUp = (dash?.changePercent ?? 0) >= 0;

  // Separate actual vs forecast trend points
  const actualTrend  = dash?.trend.filter(p => p.amount > 0) ?? [];
  const forecastTrend = dash?.trend.filter(p => (p.forecast ?? 0) > 0) ?? [];

  const pieData = (dash?.byCategory ?? []).map(c => ({
    name:  c.category.charAt(0).toUpperCase() + c.category.slice(1),
    value: c.amount,
    color: getCategoryColor(c.category),
  }));

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Cost Analytics</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            Real-time infrastructure spending with anomaly detection and forecasting
          </p>
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

      {/* Summary KPI cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0 }}>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center gap-3">
                <DollarSign className="h-8 w-8 text-primary" />
                <div>
                  {isLoading ? <Skeleton className="h-7 w-24" /> : (
                    <p className="text-2xl font-bold">${(dash?.totalPeriodCost ?? 0).toFixed(2)}</p>
                  )}
                  <p className="text-sm text-muted-foreground">Total Period Cost</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>

        <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.05 }}>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center gap-3">
                <BarChart3 className="h-8 w-8 text-violet-500" />
                <div>
                  {isLoading ? <Skeleton className="h-7 w-20" /> : (
                    <p className="text-2xl font-bold">${(dash?.currentMonthCost ?? 0).toFixed(2)}</p>
                  )}
                  <p className="text-sm text-muted-foreground">Current Month</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>

        <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }}>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center gap-3">
                {changeIsUp
                  ? <TrendingUp className="h-8 w-8 text-destructive" />
                  : <TrendingDown className="h-8 w-8 text-emerald-500" />}
                <div>
                  {isLoading ? <Skeleton className="h-7 w-20" /> : (
                    <p className={`text-2xl font-bold ${changeIsUp ? "text-destructive" : "text-emerald-500"}`}>
                      {changeIsUp ? "+" : ""}{(dash?.changePercent ?? 0).toFixed(1)}%
                    </p>
                  )}
                  <p className="text-sm text-muted-foreground">vs Last Month</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>

        <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.15 }}>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center gap-3">
                <TrendingUp className="h-8 w-8 text-amber-500" />
                <div>
                  {isLoading ? <Skeleton className="h-7 w-24" /> : (
                    <p className="text-2xl font-bold">${(dash?.forecastMonthCost ?? 0).toFixed(2)}</p>
                  )}
                  <p className="text-sm text-muted-foreground">Month Forecast</p>
                </div>
              </div>
            </CardContent>
          </Card>
        </motion.div>
      </div>

      {/* Charts row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Daily cost trend + forecast */}
        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-medium">Daily Cost Trend</CardTitle>
          </CardHeader>
          <CardContent>
            {isLoading
              ? <Skeleton className="h-52 w-full" />
              : actualTrend.length > 0
              ? (
                <ResponsiveContainer width="100%" height={208}>
                  <AreaChart data={dash?.trend ?? []}>
                    <defs>
                      <linearGradient id="costsGrad" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#4F46E5" stopOpacity={0.2} />
                        <stop offset="95%" stopColor="#4F46E5" stopOpacity={0} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
                    <XAxis
                      dataKey="date"
                      tick={{ fontSize: 11 }}
                      tickFormatter={d => {
                        const dt = new Date(d);
                        return `${dt.getMonth() + 1}/${dt.getDate()}`;
                      }}
                      interval="preserveStartEnd"
                    />
                    <YAxis tick={{ fontSize: 11 }} tickFormatter={v => `$${v}`} width={55} />
                    <Tooltip
                      formatter={(v: number, name: string) => [
                        `$${v.toFixed(2)}`,
                        name === "amount" ? "Actual" : "Forecast",
                      ]}
                      labelFormatter={d => new Date(d).toLocaleDateString()}
                    />
                    <Area
                      type="monotone"
                      dataKey="amount"
                      stroke="#4F46E5"
                      fill="url(#costsGrad)"
                      strokeWidth={2}
                      dot={false}
                    />
                    {forecastTrend.length > 0 && (
                      <Area
                        type="monotone"
                        dataKey="forecast"
                        stroke="#F59E0B"
                        fill="none"
                        strokeWidth={2}
                        strokeDasharray="5 5"
                        dot={false}
                      />
                    )}
                  </AreaChart>
                </ResponsiveContainer>
              )
              : (
                <div className="h-52 flex items-center justify-center text-muted-foreground text-sm">
                  No trend data available yet
                </div>
              )
            }
          </CardContent>
        </Card>

        {/* By category breakdown */}
        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-medium">Cost by Category</CardTitle>
          </CardHeader>
          <CardContent>
            {isLoading
              ? <Skeleton className="h-52 w-full" />
              : pieData.length > 0
              ? (
                <div className="flex gap-4">
                  <ResponsiveContainer width="50%" height={208}>
                    <PieChart>
                      <Pie
                        data={pieData}
                        cx="50%" cy="50%"
                        innerRadius={45}
                        outerRadius={80}
                        dataKey="value"
                        paddingAngle={2}
                      >
                        {pieData.map((entry, i) => (
                          <Cell key={i} fill={entry.color} />
                        ))}
                      </Pie>
                      <Tooltip formatter={(v: number) => `$${v.toFixed(2)}`} />
                    </PieChart>
                  </ResponsiveContainer>
                  <div className="flex flex-col justify-center gap-2 flex-1">
                    {dash?.byCategory.map((c) => (
                      <div key={c.category} className="flex items-center justify-between gap-2">
                        <div className="flex items-center gap-1.5">
                          <span
                            className="inline-block w-2.5 h-2.5 rounded-full flex-shrink-0"
                            style={{ background: getCategoryColor(c.category) }}
                          />
                          <span className="text-xs capitalize">{c.category}</span>
                        </div>
                        <div className="text-right">
                          <span className="text-xs font-medium">${c.amount.toFixed(2)}</span>
                          <span className="text-xs text-muted-foreground ml-1">({c.percent.toFixed(0)}%)</span>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )
              : (
                <div className="h-52 flex items-center justify-center text-muted-foreground text-sm">
                  No cost data available
                </div>
              )
            }
          </CardContent>
        </Card>
      </div>

      {/* Budget + Resource Breakdown */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Budget Tracker */}
        <Card>
          <CardHeader className="flex flex-row items-center gap-2">
            <Target className="h-4 w-4 text-primary" />
            <CardTitle className="text-sm font-medium">Monthly Budget</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {effectiveBudget && (
              <div className="space-y-1.5">
                <div className="flex justify-between text-xs">
                  <span className="text-muted-foreground">Utilization</span>
                  <span className={`font-medium ${
                    budgetUtilization > 90 ? "text-destructive" :
                    budgetUtilization > 75 ? "text-amber-500" : "text-emerald-500"
                  }`}>{budgetUtilization.toFixed(1)}%</span>
                </div>
                <div className="h-2.5 rounded-full bg-muted overflow-hidden">
                  <div
                    className={`h-full rounded-full transition-all ${
                      budgetUtilization > 90 ? "bg-destructive" :
                      budgetUtilization > 75 ? "bg-amber-500" : "bg-emerald-500"
                    }`}
                    style={{ width: `${Math.min(budgetUtilization, 100)}%` }}
                  />
                </div>
                <div className="flex justify-between text-xs text-muted-foreground">
                  <span>${(dash?.currentMonthCost ?? 0).toFixed(2)} spent</span>
                  <span>${effectiveBudget.toFixed(2)} budget</span>
                </div>
              </div>
            )}
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label htmlFor="budget-amount" className="text-xs">Monthly Budget ($)</Label>
                <Input
                  id="budget-amount"
                  type="number"
                  placeholder="e.g. 500"
                  value={budgetInput}
                  onChange={e => setBudgetInput(e.target.value)}
                  className="h-8 text-sm"
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="alert-at" className="text-xs">Alert at (%)</Label>
                <Input
                  id="alert-at"
                  type="number"
                  min={1}
                  max={100}
                  value={alertAt}
                  onChange={e => setAlertAt(e.target.value)}
                  className="h-8 text-sm"
                />
              </div>
            </div>
            <Button
              size="sm"
              className="w-full gap-1.5"
              onClick={handleSaveBudget}
              disabled={setBudget.isPending}
            >
              <Target className="h-3.5 w-3.5" />
              Save Budget
            </Button>
          </CardContent>
        </Card>

        {/* Resource Breakdown Table */}
        <Card>
          <CardHeader className="flex flex-row items-center gap-2">
            <BarChart3 className="h-4 w-4 text-primary" />
            <CardTitle className="text-sm font-medium">Resource Breakdown</CardTitle>
          </CardHeader>
          <CardContent>
            {breakdownLoading ? (
              <div className="space-y-2">{[1,2,3,4].map(i => <Skeleton key={i} className="h-10 w-full" />)}</div>
            ) : !breakdown?.items.length ? (
              <p className="text-sm text-muted-foreground py-4 text-center">No breakdown data available</p>
            ) : (
              <div className="space-y-1">
                {breakdown.items.map((item, i) => (
                  <div key={i} className="flex items-center justify-between gap-3 py-2 border-b border-border/40 last:border-0">
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-medium truncate">{item.label}</p>
                      <p className="text-xs text-muted-foreground capitalize">{item.category} · ${item.dailyAverage.toFixed(2)}/day</p>
                    </div>
                    <div className="flex items-center gap-2 shrink-0">
                      {item.trend === "up" && <ArrowUp className="h-3.5 w-3.5 text-destructive" />}
                      {item.trend === "down" && <ArrowDown className="h-3.5 w-3.5 text-emerald-500" />}
                      {item.trend === "flat" && <Minus className="h-3.5 w-3.5 text-muted-foreground" />}
                      <span className="text-sm font-medium w-16 text-right">${item.amount.toFixed(2)}</span>
                      <Badge variant="secondary" className="text-[10px] px-1.5 w-12 justify-center">
                        {item.percentage.toFixed(0)}%
                      </Badge>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Anomalies + Optimization tips */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Anomaly detection */}
        <Card>
          <CardHeader className="flex flex-row items-center gap-2">
            <AlertTriangle className="h-4 w-4 text-amber-500" />
            <CardTitle className="text-sm font-medium">Cost Anomalies</CardTitle>
            {!isLoading && (dash?.anomalies.length ?? 0) > 0 && (
              <Badge variant="secondary" className="ml-auto text-amber-600 bg-amber-50">
                {dash!.anomalies.length} detected
              </Badge>
            )}
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <div className="space-y-2">
                {[1, 2, 3].map(i => <Skeleton key={i} className="h-12 w-full" />)}
              </div>
            ) : (dash?.anomalies.length ?? 0) === 0 ? (
              <p className="text-sm text-muted-foreground py-4 text-center">
                No anomalies detected in this period
              </p>
            ) : (
              <div className="space-y-2">
                {dash!.anomalies.slice(0, 5).map((a, i) => (
                  <div key={i} className="flex items-center justify-between p-2 rounded-md bg-amber-50/60 border border-amber-100">
                    <div>
                      <p className="text-xs font-medium text-amber-800">{a.date}</p>
                      <p className="text-xs text-amber-600">
                        ${a.amount.toFixed(2)} · expected ${a.expectedAmount.toFixed(2)}
                      </p>
                    </div>
                    <Badge
                      variant="outline"
                      className={`text-xs ${a.zScore > 0 ? "border-red-300 text-red-600" : "border-blue-300 text-blue-600"}`}
                    >
                      Z={a.zScore > 0 ? "+" : ""}{a.zScore.toFixed(1)}
                    </Badge>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        {/* Optimization tips */}
        <Card>
          <CardHeader className="flex flex-row items-center gap-2">
            <Lightbulb className="h-4 w-4 text-emerald-500" />
            <CardTitle className="text-sm font-medium">Optimization Tips</CardTitle>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <div className="space-y-2">
                {[1, 2, 3].map(i => <Skeleton key={i} className="h-16 w-full" />)}
              </div>
            ) : (dash?.optimizationTips.length ?? 0) === 0 ? (
              <p className="text-sm text-muted-foreground py-4 text-center">
                No optimization tips available yet
              </p>
            ) : (
              <div className="space-y-3">
                {dash!.optimizationTips.map((tip, i) => (
                  <div key={i} className="p-3 rounded-md bg-emerald-50/60 border border-emerald-100">
                    <div className="flex items-start justify-between gap-2">
                      <div>
                        <p className="text-sm font-medium text-emerald-800">{tip.title}</p>
                        <p className="text-xs text-emerald-700 mt-0.5">{tip.description}</p>
                      </div>
                      <Badge variant="outline" className="border-emerald-300 text-emerald-700 whitespace-nowrap text-xs flex-shrink-0">
                        Save ${tip.estimatedSavings.toFixed(2)}
                      </Badge>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

