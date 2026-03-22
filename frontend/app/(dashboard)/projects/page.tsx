"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Plus, Search, FolderOpen, GitBranch, Rocket, MoreVertical,
  Trash2, Settings, ExternalLink, CheckCircle2, XCircle,
  AlertTriangle, Archive, Activity, RefreshCw,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem,
  DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { useProjects, useDeleteProject, useCreateDeployment } from "@/hooks/use-api";
import { formatRelativeTime, cn } from "@/lib/utils";
import { toast } from "sonner";
import Link from "next/link";
import type { Project } from "@/types";
import { CreateProjectDialog } from "@/components/projects/create-project-dialog";

export default function ProjectsPage() {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [createOpen, setCreateOpen] = useState(false);

  const { data, isLoading, refetch } = useProjects({
    search: search || undefined,
    status: statusFilter === "all" ? undefined : statusFilter,
  });

  const deleteProject = useDeleteProject();
  const createDeployment = useCreateDeployment();

  const projects = data?.data ?? [];

  // Compute summary stats from real data
  const activeCount = projects.filter((p) => p.status === "active").length;
  const archivedCount = projects.filter((p) => p.status === "archived").length;
  const totalDeploys = projects.reduce((sum, p) => sum + p.deploymentCount, 0);
  const recentlyDeployed = projects.filter((p) => p.lastDeployedAt).length;

  const handleDelete = async (id: string, name: string) => {
    if (!confirm(`Delete project "${name}"? This action cannot be undone.`)) return;
    try {
      await deleteProject.mutateAsync(id);
      toast.success(`Project "${name}" deleted.`);
    } catch (e: any) {
      toast.error("Failed to delete project", { description: e.message });
    }
  };

  const handleDeploy = async (project: Project) => {
    try {
      await createDeployment.mutateAsync({
        projectId: project.id,
        branch: project.repositoryBranch ?? "main",
      });
      toast.success(`Deployment started for "${project.name}"!`);
    } catch (e: any) {
      toast.error("Deployment failed", { description: e.message });
    }
  };

  // Filter locally for search (mock doesn't query-filter)
  const filteredProjects = projects.filter((p) => {
    if (search && !p.name.toLowerCase().includes(search.toLowerCase()) &&
        !p.description?.toLowerCase().includes(search.toLowerCase())) return false;
    if (statusFilter !== "all" && p.status !== statusFilter) return false;
    return true;
  });

  return (
    <div className="mx-auto max-w-[1600px] space-y-6">
      {/* ── Page Header ─────────────────────────────────── */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-bold tracking-tight">Projects</h1>
          <p className="text-xs text-muted-foreground mt-0.5">
            Manage your applications and deployments
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" className="h-8 gap-1.5 text-xs" onClick={() => refetch()}>
            <RefreshCw className="h-3.5 w-3.5" />
            Refresh
          </Button>
          <Button size="sm" className="h-8 gap-1.5 text-xs" onClick={() => setCreateOpen(true)}>
            <Plus className="h-3.5 w-3.5" />
            New Project
          </Button>
        </div>
      </div>

      {/* ── Summary Cards ───────────────────────────────── */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <SummaryCard
          label="Total Projects"
          value={isLoading ? null : String(projects.length)}
          icon={FolderOpen}
          iconClass="text-primary"
          bgClass="bg-primary/10"
        />
        <SummaryCard
          label="Active"
          value={isLoading ? null : String(activeCount)}
          icon={CheckCircle2}
          iconClass="text-emerald-500"
          bgClass="bg-emerald-500/10"
        />
        <SummaryCard
          label="Total Deployments"
          value={isLoading ? null : String(totalDeploys)}
          icon={Rocket}
          iconClass="text-violet-500"
          bgClass="bg-violet-500/10"
        />
        <SummaryCard
          label="Archived"
          value={isLoading ? null : String(archivedCount)}
          icon={Archive}
          iconClass="text-amber-500"
          bgClass="bg-amber-500/10"
        />
      </div>

      {/* ── Filters ─────────────────────────────────────── */}
      <div className="flex gap-3 flex-wrap">
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
          <Input
            placeholder="Search projects..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="h-9 pl-9 text-sm"
          />
        </div>
        <Select value={statusFilter} onValueChange={setStatusFilter}>
          <SelectTrigger className="h-9 w-36 text-sm">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Status</SelectItem>
            <SelectItem value="active">Active</SelectItem>
            <SelectItem value="archived">Archived</SelectItem>
            <SelectItem value="suspended">Suspended</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* ── Projects Grid ───────────────────────────────── */}
      {isLoading ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {[...Array(6)].map((_, i) => (
            <Skeleton key={i} className="h-48 rounded-xl" />
          ))}
        </div>
      ) : !filteredProjects.length ? (
        <EmptyProjectsState onNew={() => setCreateOpen(true)} hasFilter={search !== "" || statusFilter !== "all"} />
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {filteredProjects.map((project, i) => (
            <ProjectCard
              key={project.id}
              project={project}
              index={i}
              onDelete={() => handleDelete(project.id, project.name)}
              onDeploy={() => handleDeploy(project)}
            />
          ))}
        </div>
      )}

      <CreateProjectDialog open={createOpen} onOpenChange={setCreateOpen} />
    </div>
  );
}

/* ─────────────────────────── Summary Card ─────────────────── */

function SummaryCard({
  label, value, icon: Icon, iconClass, bgClass,
}: {
  label: string;
  value: string | null;
  icon: React.ComponentType<{ className?: string }>;
  iconClass: string;
  bgClass: string;
}) {
  return (
    <div className="flex items-center gap-3 rounded-xl border border-border/60 bg-card px-4 py-3 shadow-sm">
      <div className={cn("flex h-9 w-9 shrink-0 items-center justify-center rounded-lg", bgClass)}>
        <Icon className={cn("h-4 w-4", iconClass)} />
      </div>
      <div className="min-w-0">
        {value === null ? (
          <Skeleton className="mb-1 h-5 w-10" />
        ) : (
          <p className="text-lg font-bold leading-tight">{value}</p>
        )}
        <p className="truncate text-[11px] text-muted-foreground">{label}</p>
      </div>
    </div>
  );
}

/* ─────────────────────────── Project Card ─────────────────── */

function ProjectCard({
  project, index, onDelete, onDeploy,
}: {
  project: Project;
  index: number;
  onDelete: () => void;
  onDeploy: () => void;
}) {
  const statusColors = {
    active:    "text-emerald-500 border-emerald-500/30 bg-emerald-500/10",
    archived:  "text-muted-foreground border-border bg-muted/50",
    suspended: "text-amber-500 border-amber-500/30 bg-amber-500/10",
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.04 }}
      className="group"
    >
      <Card className="glass-card h-full transition-all duration-150 hover:border-primary/20 hover:shadow-md">
        <CardHeader className="pb-3">
          <div className="flex items-start justify-between gap-2">
            <Link href={`/projects/${project.id}`} className="flex items-center gap-3 min-w-0 flex-1">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-primary/15 to-primary/5 border border-primary/10">
                <FolderOpen className="h-5 w-5 text-primary" />
              </div>
              <div className="min-w-0">
                <p className="text-sm font-semibold truncate group-hover:text-primary transition-colors">
                  {project.name}
                </p>
                <p className="text-xs text-muted-foreground truncate mt-0.5 leading-snug">
                  {project.description || "No description"}
                </p>
              </div>
            </Link>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
                  <MoreVertical className="h-3.5 w-3.5" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-44">
                <DropdownMenuItem onClick={onDeploy}>
                  <Rocket className="mr-2 h-3.5 w-3.5" />Deploy Now
                </DropdownMenuItem>
                <DropdownMenuItem asChild>
                  <Link href={`/projects/${project.id}/settings`}>
                    <Settings className="mr-2 h-3.5 w-3.5" />Settings
                  </Link>
                </DropdownMenuItem>
                {project.repositoryUrl && (
                  <DropdownMenuItem asChild>
                    <a href={project.repositoryUrl} target="_blank" rel="noopener noreferrer">
                      <ExternalLink className="mr-2 h-3.5 w-3.5" />Open Repo
                    </a>
                  </DropdownMenuItem>
                )}
                <DropdownMenuSeparator />
                <DropdownMenuItem className="text-destructive focus:text-destructive" onClick={onDelete}>
                  <Trash2 className="mr-2 h-3.5 w-3.5" />Delete
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </CardHeader>

        <CardContent className="pt-0 space-y-3">
          {/* Repo branch + auto-deploy */}
          {project.repositoryUrl && (
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <GitBranch className="h-3 w-3 shrink-0" />
              <span className="truncate font-mono">{project.repositoryBranch ?? "main"}</span>
              {project.settings?.autoDeployEnabled && (
                <Badge variant="outline" className="ml-auto h-4 border-primary/20 bg-primary/5 px-1.5 text-[9px] text-primary">
                  auto-deploy
                </Badge>
              )}
            </div>
          )}

          {/* Tags */}
          {project.tags.length > 0 && (
            <div className="flex flex-wrap gap-1">
              {project.tags.slice(0, 4).map((tag) => (
                <Badge key={tag} variant="secondary" className="text-[10px] px-1.5 py-0 h-[18px] font-normal">
                  {tag}
                </Badge>
              ))}
              {project.tags.length > 4 && (
                <Badge variant="secondary" className="text-[10px] px-1.5 py-0 h-[18px] font-normal">
                  +{project.tags.length - 4}
                </Badge>
              )}
            </div>
          )}

          {/* Footer stats */}
          <div className="flex items-center justify-between border-t border-border/40 pt-3">
            <div className="flex items-center gap-3 text-xs text-muted-foreground">
              <span className="flex items-center gap-1">
                <Rocket className="h-3 w-3" />
                {project.deploymentCount}
              </span>
              {project.lastDeployedAt && (
                <span className="text-muted-foreground/60">
                  {formatRelativeTime(project.lastDeployedAt)}
                </span>
              )}
            </div>
            <Badge
              variant="outline"
              className={cn("text-[10px] px-1.5 py-0 h-[18px] font-medium",
                statusColors[project.status] ?? "text-muted-foreground"
              )}
            >
              {project.status}
            </Badge>
          </div>

          {/* Deploy button */}
          {project.status === "active" && (
            <Button
              size="sm"
              variant="outline"
              className="h-8 w-full gap-1.5 text-xs"
              onClick={onDeploy}
            >
              <Rocket className="h-3 w-3" />
              Deploy
            </Button>
          )}
        </CardContent>
      </Card>
    </motion.div>
  );
}

/* ─────────────────────────── Empty State ──────────────────── */

function EmptyProjectsState({ onNew, hasFilter }: { onNew: () => void; hasFilter: boolean }) {
  return (
    <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-border/60 py-16 text-center">
      <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-muted">
        <FolderOpen className="h-7 w-7 text-muted-foreground/50" />
      </div>
      <h3 className="text-sm font-semibold mb-1">
        {hasFilter ? "No matching projects" : "No projects yet"}
      </h3>
      <p className="text-xs text-muted-foreground max-w-xs mb-5">
        {hasFilter
          ? "Try adjusting your search or filter criteria."
          : "Create your first project to start deploying applications to your infrastructure."}
      </p>
      {!hasFilter && (
        <Button size="sm" className="h-8 gap-1.5 text-xs" onClick={onNew}>
          <Plus className="h-3.5 w-3.5" />
          Create Project
        </Button>
      )}
    </div>
  );
}
