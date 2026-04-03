"use client";

import {
  AlertTriangle, ArrowUpRight, BellRing, Boxes,
  Database, GitBranch, GitMerge, LayoutDashboard,
  MonitorCheck, Plus, RefreshCw, Rocket,
  Timer, TrendingUp, Wallet, ServerCog,
  HardDrive, PlayCircle,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { DeploymentStatusBadge } from "@/components/deployments/deployment-status-badge";
import { ActivityFeed } from "@/components/dashboard/activity-feed";
import { DeploymentTrend } from "@/components/dashboard/deployment-trend";
import { ServerHealthGrid } from "@/components/dashboard/server-health-grid";
import { useDashboardStats, useDeployments, useAlerts, useServers } from "@/hooks/use-api";
import { useAuthStore } from "@/store/auth-store";
import { formatRelativeTime, formatCurrency, cn } from "@/lib/utils";
import Link from "next/link";
import type { LucideIcon } from "lucide-react";

/* helpers */

function getGreeting() {
  const h = new Date().getHours();
  if (h < 12) return "Good morning";
  if (h < 17) return "Good afternoon";
  return "Good evening";
}

/* Page */

export default function DashboardPage() {
  const { data: stats, isLoading: statsLoading, isFetching: statsFetching } = useDashboardStats();
  const { data: deployments } = useDeployments({ pageSize: 5 });
  const { data: alerts }      = useAlerts();
  const { data: servers }     = useServers();
  const { user }              = useAuthStore();

  const activeAlerts   = alerts?.filter((a) => a.status === "active") ?? [];
  const criticalAlerts = activeAlerts.filter((a) => a.severity === "critical");

  // Use deploymentsToday from backend (includes cancelled); fall back to summing if absent
  const deploymentsToday = stats?.deploymentsToday
    ?? ((stats?.successfulDeploymentsToday ?? 0) + (stats?.failedDeploymentsToday ?? 0));
  const successfulToday  = stats?.successfulDeploymentsToday ?? 0;
  const failedToday      = stats?.failedDeploymentsToday ?? 0;
  const successRate      = deploymentsToday > 0
    ? Math.round((successfulToday / deploymentsToday) * 100)
    : null;

  // avgDeploymentDurationSeconds is the canonical name from the backend
  const avgSec = stats?.avgDeploymentDurationSeconds ?? stats?.avgDeploymentDuration ?? 0;
  const avgDeployLabel = avgSec > 0
    ? avgSec >= 60
      ? `${Math.floor(avgSec / 60)}m${avgSec % 60 > 0 ? ` ${Math.round(avgSec % 60)}s` : ""}`
      : `${Math.round(avgSec)}s`
    : "\u2014";

  const displayName = user?.name ?? "there";

  return (
    <div className="mx-auto max-w-[1600px] space-y-6">

      {/* Critical alert banner */}
      {criticalAlerts.length > 0 && (
        <div className="flex items-center gap-3 rounded-[var(--radius)] border border-destructive/30 bg-destructive/8 px-5 py-3.5">
          <AlertTriangle className="h-4 w-4 shrink-0 text-destructive" />
          <p className="flex-1 text-sm font-medium text-destructive">
            {criticalAlerts.length} critical alert{criticalAlerts.length > 1 ? "s" : ""} &mdash;{" "}
            <span className="font-normal text-muted-foreground">{criticalAlerts[0]?.name}</span>
          </p>
          <Button variant="destructive" size="sm" className="h-7 text-xs" asChild>
            <Link href="/alerts">View</Link>
          </Button>
        </div>
      )}

      {/* Welcome banner */}
      <div className="relative overflow-hidden rounded-[var(--radius)] border border-border bg-card px-6 py-5">
        <div className="pointer-events-none absolute right-0 top-0 h-full w-[45%] bg-gradient-to-l from-primary/5 to-transparent hidden sm:block" />
        <div className="relative flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <div className="flex items-center gap-2 mb-1">
              <LayoutDashboard className="h-4 w-4 text-primary" />
              <span className="text-xs font-medium text-primary uppercase tracking-widest">Overview</span>
              {/* Subtle background-refresh spinner — only when refetching (cache hit) */}
              {statsFetching && !statsLoading && (
                <RefreshCw className="h-3 w-3 text-muted-foreground/40 animate-spin ml-1" />
              )}
            </div>
            <h1 className="text-xl font-bold tracking-tight">
              {getGreeting()}, {displayName} {"\uD83D\uDC4B"}
            </h1>
            <p className="mt-0.5 text-sm text-muted-foreground">
              {"Here's what's happening with your infrastructure today."}
            </p>
          </div>
          <div className="flex flex-wrap gap-2 shrink-0">
            <Button size="sm" className="gap-1.5 h-8 text-xs" asChild>
              <Link href="/projects">
                <Plus className="h-3.5 w-3.5" />New Project
              </Link>
            </Button>
            <Button size="sm" variant="outline" className="gap-1.5 h-8 text-xs" asChild>
              <Link href="/deployments">
                <PlayCircle className="h-3.5 w-3.5" />Deploy
              </Link>
            </Button>
            <Button size="sm" variant="outline" className="gap-1.5 h-8 text-xs" asChild>
              <Link href="/servers">
                <HardDrive className="h-3.5 w-3.5" />Add Server
              </Link>
            </Button>
          </div>
        </div>
      </div>

      {/* Primary Stat Cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          label="Active Projects"
          value={statsLoading ? null : String(stats?.totalProjects ?? 0)}
          icon={Boxes}
          iconBg="bg-blue-500/15"
          iconColor="text-blue-400"
          href="/projects"
        />
        <StatCard
          label="Deployments Today"
          value={statsLoading ? null : String(deploymentsToday)}
          icon={GitMerge}
          iconBg="bg-violet-500/15"
          iconColor="text-violet-400"
          successCount={statsLoading ? undefined : successfulToday}
          failedCount={statsLoading ? undefined : failedToday}
          href="/deployments"
        />
        <StatCard
          label="Online Servers"
          value={statsLoading ? null : `${stats?.onlineServers ?? 0} / ${stats?.totalServers ?? 0}`}
          icon={MonitorCheck}
          iconBg="bg-emerald-500/15"
          iconColor="text-emerald-400"
          href="/servers"
        />
        <StatCard
          label="Monthly Cost"
          value={statsLoading ? null : formatCurrency(stats?.monthlyCost ?? 0)}
          icon={Wallet}
          iconBg="bg-orange-500/15"
          iconColor="text-orange-400"
          href="/costs"
        />
      </div>

      {/* Secondary stats row */}
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <MiniStat label="Success Rate"    value={successRate !== null ? `${successRate}%` : "\u2014"} icon={TrendingUp}   iconCls="text-emerald-400" bgCls="bg-emerald-500/15" loading={statsLoading} href="/deployments" />
        <MiniStat label="Active Alerts"   value={String(stats?.pendingAlerts ?? activeAlerts.length)} icon={BellRing}    iconCls="text-amber-400"   bgCls="bg-amber-500/15"   loading={statsLoading} href="/alerts" />
        <MiniStat label="Avg Deploy Time" value={avgDeployLabel}                                       icon={Timer}       iconCls="text-sky-400"     bgCls="bg-sky-500/15"     loading={statsLoading} href="/insights" />
        <MiniStat label="Databases"       value={stats ? String(stats.totalDatabases ?? 0) : "\u2014"} icon={Database}   iconCls="text-indigo-400"  bgCls="bg-indigo-500/15"  loading={statsLoading} href="/databases" />
      </div>

      {/* Charts row */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2"><DeploymentTrend /></div>
        <div className="lg:col-span-1"><ActivityFeed /></div>
      </div>

      {/* Recent deployments + server health */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card className="glass-card flex flex-col">
          <CardHeader className="flex shrink-0 flex-row items-center justify-between space-y-0 pb-3">
            <div>
              <CardTitle className="text-sm font-semibold">Recent Deployments</CardTitle>
              <CardDescription className="mt-0.5 text-xs">Latest deployment activity</CardDescription>
            </div>
            <Button variant="ghost" size="sm" className="h-7 gap-1 text-xs text-muted-foreground" asChild>
              <Link href="/deployments">
                View all <ArrowUpRight className="h-3 w-3" />
              </Link>
            </Button>
          </CardHeader>
          <CardContent className="flex-1 pt-0">
            {!deployments && <DeploymentsSkeleton />}
            {deployments?.data?.length === 0 && (
              <div className="flex flex-col items-center justify-center py-10 text-center">
                <Rocket className="mb-3 h-9 w-9 text-muted-foreground/25" />
                <p className="text-sm text-muted-foreground">No deployments yet</p>
                <Button size="sm" variant="outline" className="mt-3 gap-1.5 text-xs" asChild>
                  <Link href="/projects"><Plus className="h-3 w-3" />Create your first project</Link>
                </Button>
              </div>
            )}
            <div className="space-y-0.5">
              {deployments?.data?.slice(0, 5).map((d) => (
                <Link
                  key={d.id}
                  href="/deployments"
                  className="flex items-center gap-3 rounded-lg px-2.5 py-2.5 transition-colors hover:bg-muted/40"
                >
                  <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/8">
                    <Rocket className="h-4 w-4 text-primary" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-[13px] font-semibold leading-snug">{d.projectName}</p>
                    <p className="truncate text-xs text-muted-foreground flex items-center gap-1">
                      <GitBranch className="h-3 w-3 shrink-0" />{d.branch ?? "\u2014"}
                    </p>
                  </div>
                  <div className="flex shrink-0 items-center gap-3">
                    <DeploymentStatusBadge status={d.status} compact />
                    <span className="hidden w-20 text-right text-[11px] text-muted-foreground/70 sm:block">
                      {formatRelativeTime(d.createdAt)}
                    </span>
                  </div>
                </Link>
              ))}
            </div>
          </CardContent>
        </Card>

        <ServerHealthGrid servers={servers ?? []} />
      </div>
    </div>
  );
}

/* Stat Card */

function StatCard({
  label, value, icon: Icon, iconBg, iconColor, delta, href, successCount, failedCount,
}: {
  label: string;
  value: string | null;
  icon: LucideIcon;
  iconBg: string;
  iconColor: string;
  delta?: { value: number; direction: "up" | "down"; suffix?: string };
  /** Optional: rendered as ↑ N succeeded ↓ N failed on two sub-lines */
  successCount?: number;
  failedCount?: number;
  href: string;
}) {
  return (
    <Link href={href} className="block group">
      <div className="stat-card">
        <div className={cn("stat-icon", iconBg)}>
          <Icon className={cn("h-5 w-5", iconColor)} />
        </div>
        <div className="flex-1 min-w-0">
          {value === null ? (
            <>
              <Skeleton className="mb-1.5 h-7 w-20" />
              <Skeleton className="h-3.5 w-28" />
            </>
          ) : (
            <>
              <p className="text-2xl font-extrabold leading-none tracking-tight">{value}</p>
              <p className="mt-0.5 text-xs text-muted-foreground">{label}</p>
              {/* Show success + failed counts as two sub-lines when provided */}
              {(successCount !== undefined || failedCount !== undefined) ? (
                <div className="mt-1 flex items-center gap-2">
                  {successCount !== undefined && (
                    <span className="text-[11px] font-semibold text-emerald-500">↑ {successCount} succeeded</span>
                  )}
                  {failedCount !== undefined && failedCount > 0 && (
                    <span className="text-[11px] font-semibold text-destructive">↓ {failedCount} failed</span>
                  )}
                </div>
              ) : delta ? (
                <p className={cn(
                  "mt-1 text-[11px] font-semibold",
                  delta.direction === "up" ? "text-emerald-500" : "text-destructive"
                )}>
                  {delta.direction === "up" ? "\u2191" : "\u2193"} {delta.value}{delta.suffix ?? "%"}
                </p>
              ) : null}
            </>
          )}
        </div>
        <ArrowUpRight className="h-3.5 w-3.5 shrink-0 text-muted-foreground/0 transition-all group-hover:text-muted-foreground/50" />
      </div>
    </Link>
  );
}

/* Mini Stat */

function MiniStat({
  label, value, icon: Icon, iconCls, bgCls, loading, href,
}: {
  label: string;
  value: string;
  icon: LucideIcon;
  iconCls: string;
  bgCls: string;
  loading?: boolean;
  href: string;
}) {
  return (
    <Link href={href} className="block group">
      <div className="stat-card cursor-pointer transition-all group-hover:border-border/60 group-hover:shadow-md">
        <div className={cn("flex h-10 w-10 shrink-0 items-center justify-center rounded-[10px] transition-transform group-hover:scale-105", bgCls)}>
          <Icon className={cn("h-4 w-4", iconCls)} />
        </div>
        <div className="min-w-0 flex-1">
          {loading ? (
            <Skeleton className="mb-1 h-5 w-12" />
          ) : (
            <p className="text-lg font-bold leading-tight">{value}</p>
          )}
          <p className="truncate text-[11px] text-muted-foreground">{label}</p>
        </div>
        <ArrowUpRight className="h-3.5 w-3.5 shrink-0 text-muted-foreground/0 transition-all group-hover:text-muted-foreground/50" />
      </div>
    </Link>
  );
}

/* Skeleton */

function DeploymentsSkeleton() {
  return (
    <div className="space-y-0.5">
      {[...Array(4)].map((_, i) => (
        <div key={i} className="flex items-center gap-3 rounded-lg px-2.5 py-2.5">
          <Skeleton className="h-9 w-9 shrink-0 rounded-lg" />
          <div className="flex-1 space-y-1.5">
            <Skeleton className="h-3.5 w-36" />
            <Skeleton className="h-3 w-24" />
          </div>
          <Skeleton className="h-5 w-16 shrink-0" />
        </div>
      ))}
    </div>
  );
}