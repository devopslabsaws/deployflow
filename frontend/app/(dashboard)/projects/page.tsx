"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { motion } from "framer-motion";
import {
  Plus, Search, FolderGit2, GitBranch, Rocket, MoreVertical,
  Trash2, Settings, ExternalLink, CheckCircle2, XCircle,
  AlertTriangle, Archive, Activity, RefreshCw, Copy, Loader2, Clock,
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
import { Label } from "@/components/ui/label";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from "@/components/ui/dialog";
import { useProjects, useDeleteProject, useCreateDeployment, useCloneProject } from "@/hooks/use-api";
import { formatRelativeTime, cn } from "@/lib/utils";
import { toast } from "sonner";
import Link from "next/link";
import type { Project } from "@/types";
import { CreateProjectDialog } from "@/components/projects/create-project-dialog";
import { ConfirmActionDialog } from "@/components/ui/confirm-action-dialog";

export default function ProjectsPage() {
  const router = useRouter();
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [cloneTarget, setCloneTarget] = useState<{ id: string; name: string } | null>(null);
  const [cloneName, setCloneName] = useState("");

  const { data, isLoading, refetch } = useProjects({
    search: search || undefined,
    status: statusFilter === "all" ? undefined : statusFilter,
  });

  const deleteProject = useDeleteProject();
  const createDeployment = useCreateDeployment();
  const cloneProject = useCloneProject();

  const projects = data?.data ?? [];

  // Compute summary stats from real data
  const activeCount = projects.filter((p) => p.status === "active").length;
  const archivedCount = projects.filter((p) => p.status === "archived").length;
  const totalDeploys = projects.reduce((sum, p) => sum + p.deploymentCount, 0);
  const recentlyDeployed = projects.filter((p) => p.lastDeployedAt).length;

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteProject.mutateAsync(deleteTarget.id);
      toast.success(`Project "${deleteTarget.name}" deleted.`);
    } catch (e: any) {
      toast.error("Failed to delete project", { description: e.message });
    }
  };

  const handleClone = async () => {
    if (!cloneTarget) return;
    try {
      await cloneProject.mutateAsync({ id: cloneTarget.id, newName: cloneName.trim() || undefined });
      toast.success(`Project cloned successfully!`);
      setCloneTarget(null);
      setCloneName("");
    } catch (e: any) {
      toast.error("Clone failed", { description: e.message });
    }
  };

  const handleDeploy = async (project: Project) => {
    try {
      const deployment = await createDeployment.mutateAsync({
        projectId: project.id,
        branch: project.repositoryBranch ?? "main",
      });
      toast.success(`Deployment started for "${project.name}" — opening logs...`);
      router.push(`/deployments/${deployment.id}`);
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
          icon={FolderGit2}
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
              onDelete={() => setDeleteTarget({ id: project.id, name: project.name })}
              onDeploy={() => handleDeploy(project)}
              onClone={() => { setCloneTarget({ id: project.id, name: project.name }); setCloneName(""); }}
            />
          ))}
        </div>
      )}

      <ConfirmActionDialog
        open={!!deleteTarget}
        onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}
        title="Delete Project"
        description={deleteTarget
          ? `This permanently deletes project \"${deleteTarget.name}\" and cannot be undone.`
          : "This action cannot be undone."}
        confirmLabel="Delete Project"
        requireText={deleteTarget?.name}
        isConfirming={deleteProject.isPending}
        onConfirm={handleDelete}
      />

      <CreateProjectDialog open={createOpen} onOpenChange={setCreateOpen} />

      {/* Clone Dialog */}
      <Dialog open={!!cloneTarget} onOpenChange={open => { if (!open) { setCloneTarget(null); setCloneName(""); } }}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Copy className="h-4 w-4" />
              Clone Project
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <p className="text-sm text-muted-foreground">
              Cloning <span className="font-medium text-foreground">{cloneTarget?.name}</span>. All settings will be copied.
            </p>
            <div className="space-y-1.5">
              <Label htmlFor="clone-name" className="text-xs">New Name <span className="font-normal text-muted-foreground">(optional)</span></Label>
              <Input
                id="clone-name"
                placeholder={cloneTarget ? `${cloneTarget.name} (Clone)` : ""}
                value={cloneName}
                onChange={e => setCloneName(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloneTarget(null)}>Cancel</Button>
            <Button onClick={handleClone} disabled={cloneProject.isPending} className="gap-1.5">
              {cloneProject.isPending ? <RefreshCw className="h-3.5 w-3.5 animate-spin" /> : <Copy className="h-3.5 w-3.5" />}
              Clone
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
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
  project, index, onDelete, onDeploy, onClone,
}: {
  project: Project;
  index: number;
  onDelete: () => void;
  onDeploy: () => void;
  onClone: () => void;
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
                <FolderGit2 className="h-5 w-5 text-primary" />
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
                <DropdownMenuItem onClick={onClone}>
                  <Copy className="mr-2 h-3.5 w-3.5" />Clone Project
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

          {/* Deploy / Status footer */}
          {project.status === "active" && (() => {
            const ds = project.lastDeploymentStatus;
            const inFlight = ds === "queued" || ds === "building" || ds === "deploying";
            const isLive = ds === "healthy" || ds === "running";
            const isFailed = ds === "failed";
            const isCancelled = ds === "cancelled";
            if (inFlight) return (
              <div className="flex items-center justify-between rounded-lg border border-blue-500/25 bg-blue-500/8 px-3 py-2">
                <div className="flex items-center gap-2">
                  <Loader2 className="h-3.5 w-3.5 text-blue-400 animate-spin" />
                  <span className="text-xs font-medium text-blue-400 capitalize">{ds}</span>
                </div>
                {project.lastDeploymentId ? (
                  <Link href={`/deployments/${project.lastDeploymentId}`} className="text-[11px] text-blue-400/70 hover:text-blue-400 underline underline-offset-2">
                    View logs
                  </Link>
                ) : null}
              </div>
            );
            if (isLive) return (
              <div className="flex items-center justify-between gap-2">
                <div className="flex items-center gap-1.5 text-xs text-emerald-400">
                  <CheckCircle2 className="h-3.5 w-3.5" />
                  <span className="font-medium">Live</span>
                  {project.lastDeployedAt && (
                    <span className="text-muted-foreground/60">&middot; {formatRelativeTime(project.lastDeployedAt)}</span>
                  )}
                </div>
                <Button size="sm" variant="outline" className="h-7 gap-1 text-xs" onClick={onDeploy}>
                  <Rocket className="h-3 w-3" />Re-deploy
                </Button>
              </div>
            );
            if (isFailed) return (
              <div className="flex items-center justify-between gap-2">
                <div className="flex items-center gap-1.5 text-xs text-destructive">
                  <XCircle className="h-3.5 w-3.5" />
                  <span className="font-medium">Failed</span>
                </div>
                <Button size="sm" variant="outline" className="h-7 gap-1 text-xs border-destructive/30 text-destructive hover:bg-destructive/10" onClick={onDeploy}>
                  <RefreshCw className="h-3 w-3" />Retry
                </Button>
              </div>
            );
            if (isCancelled) return (
              <div className="flex items-center justify-between gap-2">
                <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  <XCircle className="h-3.5 w-3.5" />
                  <span>Cancelled</span>
                </div>
                <Button size="sm" variant="outline" className="h-7 gap-1 text-xs" onClick={onDeploy}>
                  <Rocket className="h-3 w-3" />Deploy
                </Button>
              </div>
            );
            return (
              <Button size="sm" variant="outline" className="h-8 w-full gap-1.5 text-xs" onClick={onDeploy}>
                <Rocket className="h-3 w-3" />Deploy
              </Button>
            );
          })()}
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
        <FolderGit2 className="h-7 w-7 text-muted-foreground/50" />
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
