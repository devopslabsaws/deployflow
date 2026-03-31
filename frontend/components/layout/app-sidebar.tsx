"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard, FolderGit2, Rocket, Server, ScrollText,
  Database, BarChart3, Settings, Bot, ChevronLeft, ChevronRight,
  Users, ShieldCheck, BellDot, CircleDollarSign, Globe, HardDrive,
  Boxes, Zap, Package, Network, BookTemplate, GitMerge,
  GitPullRequestArrow, Webhook, Layers, Cpu, Activity,
  FileSearch2, LogsIcon, Compass, TrendingUp, Container,
  Cloud, Waypoints, AreaChart,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useSidebarStore } from "@/store/ui-store";
import { useDashboardStats } from "@/hooks/use-api";

interface NavItem {
  label: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  badge?: string;
  badgeColor?: string;
}
interface NavGroup {
  label: string;
  items: NavItem[];
}

const navGroups: NavGroup[] = [
  {
    label: "Overview",
    items: [
      { label: "Dashboard",    href: "/dashboard",    icon: LayoutDashboard },
      { label: "AI Assistant", href: "/ai-assistant", icon: Bot, badge: "AI", badgeColor: "bg-violet-500 text-white" },
      { label: "Onboarding",   href: "/onboarding",   icon: Compass, badge: "Start", badgeColor: "bg-emerald-500 text-white" },
      { label: "Insights",     href: "/insights",     icon: AreaChart },
    ],
  },
  {
    label: "Applications",
    items: [
      { label: "Projects",          href: "/projects",              icon: FolderGit2 },
      { label: "Deployments",       href: "/deployments",           icon: Rocket },
      { label: "Services",          href: "/services",              icon: Package },
      { label: "Templates",         href: "/templates",             icon: BookTemplate, badge: "New", badgeColor: "bg-primary text-primary-foreground" },
      { label: "Environments",      href: "/environments",          icon: Layers },
      { label: "Preview Envs",      href: "/preview-environments",  icon: GitPullRequestArrow },
      { label: "Outbound Webhooks", href: "/outbound-webhooks",     icon: Webhook },
      { label: "Compose",           href: "/compose",               icon: GitMerge },
    ],
  },
  {
    label: "Infrastructure",
    items: [
      { label: "Servers",         href: "/servers",         icon: Server },
      { label: "Clusters",        href: "/clusters",        icon: Network },
      { label: "Containers",      href: "/containers",      icon: Boxes },
      { label: "Databases",       href: "/databases",       icon: Database },
      { label: "S3 Destinations", href: "/s3-destinations", icon: Cloud },
      { label: "Domains",         href: "/domains",         icon: Globe },
      { label: "Volumes",         href: "/volumes",         icon: HardDrive },
    ],
  },
  {
    label: "Operations",
    items: [
      { label: "Pipelines",  href: "/pipelines",  icon: Waypoints },
      { label: "Logs",       href: "/logs",       icon: ScrollText },
      { label: "Monitoring", href: "/monitoring", icon: Activity },
      { label: "Alerts",     href: "/alerts",     icon: BellDot },
    ],
  },
  {
    label: "Management",
    items: [
      { label: "Team",         href: "/team",       icon: Users },
      { label: "Security",     href: "/security",   icon: ShieldCheck },
      { label: "Cost Monitor", href: "/costs",      icon: CircleDollarSign },
      { label: "Audit Logs",   href: "/audit-logs", icon: FileSearch2 },
      { label: "Settings",     href: "/settings",   icon: Settings },
    ],
  },
];

export function AppSidebar({ onNavClick }: { onNavClick?: () => void }) {
  const pathname = usePathname();
  const { isCollapsed, toggleSidebar } = useSidebarStore();
  const { data: stats } = useDashboardStats();

  // Compute dynamic badges from live stats
  const dynamicBadge = (href: string): Pick<NavItem, "badge" | "badgeColor"> => {
    if (href === "/projects" && stats?.totalProjects) {
      return { badge: String(stats.totalProjects), badgeColor: "bg-primary/20 text-primary" };
    }
    if (href === "/alerts" && stats?.activeAlerts) {
      return { badge: String(stats.activeAlerts), badgeColor: "bg-destructive text-destructive-foreground" };
    }
    if (href === "/servers" && stats?.onlineServers !== undefined && stats?.totalServers !== undefined) {
      return { badge: `${stats.onlineServers}/${stats.totalServers}`, badgeColor: stats.onlineServers === stats.totalServers ? "bg-emerald-500/20 text-emerald-600" : "bg-amber-500/20 text-amber-600" };
    }
    return {};
  };

  return (
    <TooltipProvider delayDuration={0}>
      <aside
        className={cn(
          "relative flex flex-col h-full",
          "bg-sidebar border-r border-sidebar-border",
          "transition-[width] duration-200 ease-in-out",
          isCollapsed ? "w-16" : "w-[260px]"
        )}
      >
        {/* Brand */}
        <div className={cn(
          "flex h-[60px] shrink-0 items-center border-b border-sidebar-border",
          isCollapsed ? "justify-center px-0" : "px-[18px] gap-[10px]"
        )}>
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br from-primary to-violet-600 shadow-sm">
            <Zap className="h-[18px] w-[18px] text-white" />
          </div>
          {!isCollapsed && (
            <div className="min-w-0">
              <p className="text-[15px] font-bold leading-tight text-sidebar-foreground truncate">
                DeployFlow
              </p>
              <p className="text-[10px] leading-tight text-muted-foreground truncate uppercase tracking-wider">
                Enterprise Platform
              </p>
            </div>
          )}
        </div>

        {/* Navigation */}
        <nav className="flex-1 overflow-y-auto py-3" style={{ scrollbarWidth: "thin" }}>
          {navGroups.map((group, groupIndex) => (
            <div key={group.label} className={cn(groupIndex > 0 && "mt-1")}>
              {!isCollapsed ? (
                <p className="mb-1 px-[18px] py-[10px] pb-1 text-[11px] font-semibold uppercase tracking-[0.06em] select-none text-muted-foreground">
                  {group.label}
                </p>
              ) : (
                groupIndex > 0 && (
                  <div className="mx-auto my-2 h-px w-6 bg-sidebar-border" />
                )
              )}
              <div className="flex flex-col">
                {group.items.map((item) => {
                  const isActive =
                    item.href === "/dashboard"
                      ? pathname === "/dashboard"
                      : pathname.startsWith(item.href);

                  const { badge, badgeColor } = { ...item, ...dynamicBadge(item.href) };

                  const navLink = (
                    <Link
                      href={item.href}
                      onClick={onNavClick}
                      className={cn(
                        "group relative flex h-10 w-full items-center text-[13.5px] font-medium",
                        "transition-all duration-100",
                        isCollapsed ? "justify-center px-0" : "gap-[10px] px-3 mx-0",
                        isActive
                          ? "bg-sidebar-accent text-sidebar-accent-foreground"
                          : "text-sidebar-foreground hover:bg-muted/50"
                      )}
                    >
                      {isActive && (
                        <span className="absolute left-0 top-1 bottom-1 w-[3px] rounded-r-[3px] bg-primary" />
                      )}
                      <item.icon
                        className={cn(
                          "h-4 w-4 shrink-0 transition-colors",
                          isCollapsed && "mx-auto",
                          isActive
                            ? "opacity-100"
                            : "opacity-50 group-hover:opacity-75"
                        )}
                      />
                      {!isCollapsed && (
                        <>
                          <span className={cn("flex-1 truncate", !isActive && "text-sidebar-foreground")}>{item.label}</span>
                          {badge && (
                            <span className={cn(
                              "ml-auto text-[10px] font-bold px-1.5 leading-4 rounded-full",
                              badgeColor ?? "bg-muted text-muted-foreground"
                            )}>
                              {badge}
                            </span>
                          )}
                        </>
                      )}
                    </Link>
                  );

                  return isCollapsed ? (
                    <Tooltip key={item.href}>
                      <TooltipTrigger asChild>{navLink}</TooltipTrigger>
                      <TooltipContent side="right" className="ml-1 text-xs font-medium">
                        {item.label}
                      </TooltipContent>
                    </Tooltip>
                  ) : (
                    <div key={item.href}>{navLink}</div>
                  );
                })}
              </div>
            </div>
          ))}
        </nav>

        {/* Collapse toggle */}
        <div className="shrink-0 border-t border-sidebar-border p-3">
          <button
            onClick={toggleSidebar}
            className={cn(
              "flex h-9 w-full items-center rounded-lg text-xs font-medium",
              "text-muted-foreground hover:text-foreground",
              "hover:bg-muted/50 transition-colors",
              isCollapsed ? "justify-center px-0" : "gap-2 px-2"
            )}
          >
            {isCollapsed ? (
              <ChevronRight className="h-4 w-4" />
            ) : (
              <>
                <ChevronLeft className="h-4 w-4" />
                <span>Collapse</span>
              </>
            )}
          </button>
        </div>
      </aside>
    </TooltipProvider>
  );
}
