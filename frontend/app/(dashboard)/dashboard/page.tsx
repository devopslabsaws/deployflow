"use client";

import {
  Activity, AlertTriangle, ArrowUpRight, CheckCircle2,
  DollarSign, Rocket, Server, Cloud, Zap, Container,
  TrendingUp, TrendingDown, Database,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { DeploymentStatusBadge } from "@/components/deployments/deployment-status-badge";
import { ActivityFeed } from "@/components/dashboard/activity-feed";
import { DeploymentTrend } from "@/components/dashboard/deployment-trend";
import { ServerHealthGrid } from "@/components/dashboard/server-health-grid";
import { useDashboardStats, useDeployments, useAlerts, useServers } from "@/hooks/use-api";
import { formatRelativeTime, formatCurrency, cn } from "@/lib/utils";
import Link from "next/link";
import type { LucideIcon } from "lucide-react";

/* ─────────────────────────── Page ─────────────────────────── */

export default function DashboardPage() {
  const { data: stats, isLoading: statsLoading } = useDashboardStats();
  const { data: deployments } = useDeployments({ pageSize: 5 });
  const { data: alerts }      = useAlerts();
  const { data: servers }     = useServers();

  const activeAlerts   = alerts?.filter((a) => a.status === "active") ?? [];
  const criticalAlerts = activeAlerts.filter((a) => a.severity === "critical");

  const totalDeploys   = (stats?.successfulDeploymentsToday ?? 0) + (stats?.failedDeploymentsToday ?? 0);
  const successRate    = totalDeploys
    ? Math.round((stats!.successfulDeploymentsToday / totalDeploys) * 100)
    : null;

  return (
    <div className="mx-auto max-w-[1600px] space-y-6">

      {/* ── Critical alert banner ─────────────────────────── */}
      {criticalAlerts.length > 0 && (
        <div className="flex items-center gap-3 rounded-[var(--radius)] border border-destructive/30 bg-destructive/8 px-5 py-3.5">
          <AlertTriangle className="h-4 w-4 shrink-0 text-destructive" />
          <p className="flex-1 text-sm font-medium text-destructive">
            {criticalAlerts.length} critical alert{criticalAlerts.length > 1 ? "s" : ""} —{" "}
            <span className="font-normal text-muted-foreground">{criticalAlerts[0]?.name}</span>
          </p>
          <Button variant="destructive" size="sm" className="h-7 text-xs" asChild>
            <Link href="/alerts">View</Link>
          </Button>
        </div>
      )}

      {/* ── Primary Stat Cards (reference design) ─────────── */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          label="Active Projects"
          value={statsLoading ? null : String(stats?.totalProjects ?? 0)}
          icon={Cloud}
          iconBg="bg-blue-100 dark:bg-blue-500/15"
          iconColor="text-blue-500"
          delta={{ value: 12, direction: "up" }}
          href="/projects"
        />
        <StatCard
          label="Deployments Today"
          value={statsLoading ? null : String(totalDeploys)}
          icon={Rocket}
          iconBg="bg-violet-100 dark:bg-violet-500/15"
          iconColor="text-violet-500"
          delta={{ value: stats?.failedDeploymentsToday ?? 0, direction: "down", suffix: " failed" }}
          href="/deployments"
        />
        <StatCard
          label="Online Servers"
          value={statsLoading ? null : `${stats?.onlineServers ?? 0} / ${stats?.totalServers ?? 0}`}
          icon={Server}
          iconBg="bg-emerald-100 dark:bg-emerald-500/15"
          iconColor="text-emerald-500"
          delta={{ value: 100, direction: "up", suffix: "% uptime" }}
          href="/servers"
        />
        <StatCard
          label="Monthly Cost"
          value={statsLoading ? null : formatCurrency(stats?.monthlyCost ?? 0)}
          icon={DollarSign}
          iconBg="bg-orange-100 dark:bg-orange-500/15"
          iconColor="text-orange-500"
          delta={{ value: 5, direction: "down" }}
          href="/costs"
        />
      </div>

      {/* ── Secondary stats row ───────────────────────────── */}
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <MiniStat label="Success Rate"    value={successRate !== null ? `${successRate}%` : "—"} icon={CheckCircle2}  iconCls="text-emerald-500" bgCls="bg-emerald-100 dark:bg-emerald-500/15" loading={statsLoading} />
        <MiniStat label="Active Alerts"   value={stats ? String(stats.activeAlerts) : "\u2014"}         icon={AlertTriangle} iconCls="text-amber-500"   bgCls="bg-amber-100 dark:bg-amber-500/15"     loading={statsLoading} />
        <MiniStat label="Avg Deploy Time" value={stats && stats.avgDeploymentDuration > 0 ? `${Math.round(stats.avgDeploymentDuration / 60)}m` : "\u2014"} icon={Zap} iconCls="text-sky-500" bgCls="bg-sky-100 dark:bg-sky-500/15" loading={statsLoading} />
        <MiniStat label="Databases"       value={stats ? String(stats.totalDatabases) : "—"}        icon={Database}     iconCls="text-primary"     bgCls="bg-primary/10"                         loading={statsLoading} />
      </div>

      {/* ── Charts row ────────────────────────────────────── */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2"><DeploymentTrend /></div>
        <div className="lg:col-span-1"><ActivityFeed /></div>
      </div>

      {/* ── Recent deployments + server health ────────────── */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        {/* Recent Deployments */}
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
                    <p className="truncate text-xs text-muted-foreground">{d.branch ?? "—"}</p>
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

/* ─────────────────────────── Stat Card (reference design) ── */

function StatCard({
  label, value, icon: Icon, iconBg, iconColor, delta, href,
}: {
  label: string;
  value: string | null;
  icon: LucideIcon;
  iconBg: string;
  iconColor: string;
  delta?: { value: number; direction: "up" | "down"; suffix?: string };
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
              {delta && (
                <p className={cn(
                  "mt-1 text-[11px] font-semibold",
                  delta.direction === "up" ? "text-emerald-500" : "text-destructive"
                )}>
                  {delta.direction === "up" ? "↑" : "↓"} {delta.value}{delta.suffix ?? "%"}
                </p>
              )}
            </>
          )}
        </div>
        <ArrowUpRight className="h-3.5 w-3.5 shrink-0 text-muted-foreground/0 transition-all group-hover:text-muted-foreground/50" />
      </div>
    </Link>
  );
}

/* ─────────────────────────── Mini Stat ─────────────────────── */

function MiniStat({
  label, value, icon: Icon, iconCls, bgCls, loading,
}: {
  label: string;
  value: string;
  icon: LucideIcon;
  iconCls: string;
  bgCls: string;
  loading?: boolean;
}) {
  return (
    <div className="stat-card">
      <div className={cn("flex h-10 w-10 shrink-0 items-center justify-center rounded-[10px]", bgCls)}>
        <Icon className={cn("h-4 w-4", iconCls)} />
      </div>
      <div className="min-w-0">
        {loading ? (
          <Skeleton className="mb-1 h-5 w-12" />
        ) : (
          <p className="text-lg font-bold leading-tight">{value}</p>
        )}
        <p className="truncate text-[11px] text-muted-foreground">{label}</p>
      </div>
    </div>
  );
}

/* ─────────────────────────── Skeleton ──────────────────────── */

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
