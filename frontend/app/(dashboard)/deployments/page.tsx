"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { motion } from "framer-motion";
import {
  Search,
  Filter,
  Rocket,
  RefreshCw,
  ChevronLeft,
  ChevronRight,
  ArrowUpDown,
  Clock,
  GitCommit,
  User,
  RotateCcw,
  XCircle,
  Eye,
  Plus,
  GitBranch,
  ExternalLink,
  Loader2,
  AlertCircle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Label } from "@/components/ui/label";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { DeploymentStatusBadge } from "@/components/deployments/deployment-status-badge";
import {
  useDeployments,
  useCancelDeployment,
  useRollbackDeployment,
  useCreateDeployment,
  useProjects,
} from "@/hooks/use-api";
import { formatRelativeTime, formatDuration, formatDate, truncate } from "@/lib/utils";
import { toast } from "sonner";
import Link from "next/link";
import type { DeploymentTrigger } from "@/types";

// Fetch branches from GitHub or GitLab for a public repo URL
async function fetchRepoBranches(repoUrl: string): Promise<string[]> {
  const githubMatch = repoUrl.match(/github\.com\/([^/]+)\/([^/?.#]+)/);
  const gitlabMatch = repoUrl.match(/gitlab\.com\/([^/]+(?:\/[^/]+)*)\/([^/?.#]+)/);

  if (githubMatch) {
    const [, owner, repo] = githubMatch;
    const res = await fetch(
      `https://api.github.com/repos/${owner}/${repo.replace(/\.git$/, "")}/branches?per_page=100`
    );
    if (!res.ok) throw new Error("Repository may be private or not found.");
    const data = await res.json();
    return (data as any[]).map((b) => b.name as string);
  }

  if (gitlabMatch) {
    const [, namespace, repo] = gitlabMatch;
    const encoded = encodeURIComponent(`${namespace}/${repo.replace(/\.git$/, "")}`);
    const res = await fetch(
      `https://gitlab.com/api/v4/projects/${encoded}/repository/branches?per_page=100`
    );
    if (!res.ok) throw new Error("Repository may be private or not found.");
    const data = await res.json();
    return (data as any[]).map((b) => b.name as string);
  }

  return []; // non-GitHub/GitLab: silently return empty
}

const triggerLabels: Record<DeploymentTrigger, string> = {
  git_push: "Git Push",
  manual: "Manual",
  api: "API",
  schedule: "Schedule",
  rollback: "Rollback",
};

export default function DeploymentsPage() {
  const router = useRouter();
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [page, setPage] = useState(1);
  const pageSize = 15;

  // Deploy dialog state
  const [deployOpen, setDeployOpen] = useState(false);
  const [deployProjectId, setDeployProjectId] = useState("");
  const [deployBranch, setDeployBranch] = useState("");
  const [branches, setBranches] = useState<string[]>([]);
  const [fetchingBranches, setFetchingBranches] = useState(false);
  const [branchError, setBranchError] = useState<string | null>(null);

  const { data, isLoading, refetch } = useDeployments({
    status: statusFilter === "all" ? undefined : statusFilter,
    page,
    pageSize,
  });

  const { data: projectsData } = useProjects();
  const projects = projectsData?.data ?? [];

  const cancelDeployment = useCancelDeployment();
  const rollbackDeployment = useRollbackDeployment();
  const createDeployment = useCreateDeployment();

  const handleDeploy = async () => {
    if (!deployProjectId) { toast.error("Please select a project."); return; }
    try {
      const deployment = await createDeployment.mutateAsync({
        projectId: deployProjectId,
        branch: deployBranch || undefined,
      });
      toast.success("Deployment triggered \u2014 opening logs...");
      setDeployOpen(false);
      setDeployProjectId("");
      setDeployBranch("");
      setBranches([]);
      setBranchError(null);
      router.push(`/deployments/${deployment.id}`);
    } catch (e: any) {
      toast.error("Deployment failed", { description: e.message });
    }
  };

  // When a project is selected, auto-fetch its branches from the remote repo
  const handleProjectChange = async (projectId: string) => {
    setDeployProjectId(projectId);
    setDeployBranch("");
    setBranches([]);
    setBranchError(null);

    const project = projects.find((p) => p.id === projectId);
    const repoUrl = project?.repositoryUrl;
    if (!repoUrl) return;

    setFetchingBranches(true);
    try {
      const result = await fetchRepoBranches(repoUrl);
      setBranches(result);
      // Pre-select the project's configured default branch
      const defaultBranch = project?.repositoryBranch ?? "main";
      setDeployBranch(result.includes(defaultBranch) ? defaultBranch : (result[0] ?? ""));
    } catch (e: any) {
      setBranchError(e.message);
    } finally {
      setFetchingBranches(false);
    }
  };

  const handleCancel = async (id: string) => {
    try {
      await cancelDeployment.mutateAsync(id);
      toast.success("Deployment cancelled.");
    } catch (e: any) {
      toast.error("Failed to cancel", { description: e.message });
    }
  };

  const handleRollback = async (projectId: string, deploymentId: string) => {
    try {
      await rollbackDeployment.mutateAsync({ projectId, deploymentId });
      toast.success("Rollback triggered successfully.");
    } catch (e: any) {
      toast.error("Rollback failed", { description: e.message });
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Deployments</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {data?.total ?? 0} total deployments
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={() => refetch()} className="gap-1.5">
            <RefreshCw className="w-3.5 h-3.5" />
            Refresh
          </Button>
          <Button size="sm" className="gap-1.5" onClick={() => setDeployOpen(true)}>
            <Plus className="w-3.5 h-3.5" />
            Deploy
          </Button>
        </div>
      </div>

      {/* Filters */}
      <div className="flex gap-3 flex-wrap">
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input
            placeholder="Search deployments..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9"
          />
        </div>
        <Select value={statusFilter} onValueChange={(v) => { setStatusFilter(v); setPage(1); }}>
          <SelectTrigger className="w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Status</SelectItem>
            <SelectItem value="queued">Queued</SelectItem>
            <SelectItem value="building">Building</SelectItem>
            <SelectItem value="deploying">Deploying</SelectItem>
            <SelectItem value="healthy">Healthy</SelectItem>
            <SelectItem value="failed">Failed</SelectItem>
            <SelectItem value="cancelled">Cancelled</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Deployments Table */}
      <div className="rounded-xl border border-border/50 bg-card overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="border-b border-border/50 bg-muted/30 hover:bg-muted/30">
              <TableHead className="font-semibold">Project</TableHead>
              <TableHead className="font-semibold">Status</TableHead>
              <TableHead className="font-semibold">Trigger</TableHead>
              <TableHead className="font-semibold">Commit</TableHead>
              <TableHead className="font-semibold">Duration</TableHead>
              <TableHead className="font-semibold">Started</TableHead>
              <TableHead className="text-right font-semibold">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              [...Array(8)].map((_, i) => (
                <TableRow key={i} className="hover:bg-muted/20">
                  {[...Array(7)].map((_, j) => (
                    <TableCell key={j}>
                      <Skeleton className="h-4 w-full max-w-[120px]" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : !data?.data?.length ? (
              <TableRow>
                <TableCell colSpan={7} className="text-center py-16 text-muted-foreground">
                  <Rocket className="w-10 h-10 mx-auto mb-3 opacity-30" />
                  No deployments found
                </TableCell>
              </TableRow>
            ) : (
              data.data.map((d) => (
                <TableRow
                  key={d.id}
                  className="hover:bg-muted/20 transition-colors cursor-pointer"
                  onClick={() => router.push(`/deployments/${d.id}`)}
                >
                  <TableCell>
                    <Link
                      href={`/deployments/${d.id}`}
                      className="font-medium text-sm hover:text-primary transition-colors"
                    >
                      {d.projectName}
                    </Link>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      {d.branch && `Branch: ${d.branch}`}
                    </p>
                  </TableCell>

                  <TableCell>
                    <DeploymentStatusBadge status={d.status} />
                  </TableCell>

                  <TableCell>
                    <Badge variant="outline" className="text-xs">
                      {triggerLabels[d.trigger]}
                    </Badge>
                  </TableCell>

                  <TableCell>
                    {d.commitSha ? (
                      <div>
                        <div className="flex items-center gap-1.5 text-xs">
                          <GitCommit className="w-3.5 h-3.5 text-muted-foreground" />
                          <code className="font-mono">{d.commitSha.slice(0, 7)}</code>
                        </div>
                        {d.commitMessage && (
                          <p className="text-xs text-muted-foreground mt-0.5">
                            {truncate(d.commitMessage, 45)}
                          </p>
                        )}
                      </div>
                    ) : (
                      <span className="text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>

                  <TableCell>
                    <span className="text-xs text-muted-foreground">
                      {d.duration ? formatDuration(d.duration) : "—"}
                    </span>
                  </TableCell>

                  <TableCell>
                    <div title={formatDate(d.startedAt ?? d.createdAt, "MMM d, yyyy HH:mm:ss")}>
                      <p className="text-xs font-medium tabular-nums">
                        {formatDate(d.startedAt ?? d.createdAt, "MMM d, HH:mm")}
                      </p>
                      <p className="text-[11px] text-muted-foreground mt-0.5">
                        {formatRelativeTime(d.startedAt ?? d.createdAt)}
                      </p>
                    </div>
                  </TableCell>

                  <TableCell className="text-right" onClick={(e) => e.stopPropagation()}>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="sm" className="h-7 w-7 px-0">
                          <span className="sr-only">Actions</span>
                          <ArrowUpDown className="w-3.5 h-3.5" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem asChild>
                          <Link href={`/deployments/${d.id}`}>
                            <Eye className="mr-2 w-4 h-4" />View Details
                          </Link>
                        </DropdownMenuItem>
                        <DropdownMenuItem asChild>
                          <Link href={`/logs?deploymentId=${d.id}`}>
                            <Clock className="mr-2 w-4 h-4" />View Logs
                          </Link>
                        </DropdownMenuItem>
                        {d.url && (
                          <DropdownMenuItem asChild>
                            <a href={d.url} target="_blank" rel="noopener noreferrer">
                              <ExternalLink className="mr-2 w-4 h-4 text-emerald-500" />Open App
                            </a>
                          </DropdownMenuItem>
                        )}
                        {(d.status === "queued" || d.status === "building" || d.status === "deploying") && (
                          <DropdownMenuItem
                            className="text-destructive"
                            onClick={() => handleCancel(d.id)}
                          >
                            <XCircle className="mr-2 w-4 h-4" />Cancel
                          </DropdownMenuItem>
                        )}
                        {(d.status === "failed" || d.status === "healthy") && (
                          <DropdownMenuItem onClick={() => handleRollback(d.projectId, d.id)}>
                            <RotateCcw className="mr-2 w-4 h-4" />Rollback
                          </DropdownMenuItem>
                        )}
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>

        {/* Pagination */}
        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-border/50">
            <p className="text-sm text-muted-foreground">
              Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, data.total)} of {data.total}
            </p>
            <div className="flex gap-1">
              <Button
                variant="outline"
                size="sm"
                className="h-7"
                disabled={page === 1}
                onClick={() => setPage((p) => p - 1)}
              >
                <ChevronLeft className="w-4 h-4" />
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="h-7"
                disabled={page >= data.totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                <ChevronRight className="w-4 h-4" />
              </Button>
            </div>
          </div>
        )}
      </div>
      {/* Deploy Dialog */}
      <Dialog open={deployOpen} onOpenChange={(open) => {
        setDeployOpen(open);
        if (!open) { setDeployProjectId(""); setDeployBranch(""); setBranches([]); setBranchError(null); }
      }}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Rocket className="h-5 w-5" />
              New Deployment
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="deploy-project">Project</Label>
              <Select value={deployProjectId} onValueChange={handleProjectChange}>
                <SelectTrigger id="deploy-project">
                  <SelectValue placeholder="Select a project…" />
                </SelectTrigger>
                <SelectContent>
                  {projects.filter(p => p.status === "active").map(p => (
                    <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <Label htmlFor="deploy-branch" className="flex items-center gap-1.5">
                  <GitBranch className="h-3.5 w-3.5" />
                  Branch
                </Label>
                {fetchingBranches && (
                  <span className="flex items-center gap-1 text-xs text-muted-foreground">
                    <Loader2 className="h-3 w-3 animate-spin" />
                    Fetching branches…
                  </span>
                )}
                {!fetchingBranches && branches.length > 0 && (
                  <span className="text-xs text-muted-foreground">{branches.length} branch{branches.length !== 1 ? "es" : ""} found</span>
                )}
              </div>

              {/* Branch dropdown when branches are available, otherwise plain text input */}
              {branches.length > 0 ? (
                <Select value={deployBranch} onValueChange={setDeployBranch}>
                  <SelectTrigger id="deploy-branch">
                    <SelectValue placeholder="Select a branch…" />
                  </SelectTrigger>
                  <SelectContent className="max-h-56">
                    {branches.map((b) => (
                      <SelectItem key={b} value={b}>
                        <span className="flex items-center gap-2">
                          <GitBranch className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                          {b}
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <Input
                  id="deploy-branch"
                  placeholder={
                    fetchingBranches
                      ? "Fetching branches…"
                      : deployProjectId
                        ? "Type a branch name (or leave empty for project default)"
                        : "Select a project first"
                  }
                  value={deployBranch}
                  onChange={e => setDeployBranch(e.target.value)}
                  disabled={fetchingBranches}
                />
              )}

              {branchError && (
                <p className="flex items-center gap-1.5 text-xs text-amber-500">
                  <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                  {branchError} — type a branch name manually.
                </p>
              )}
              {!branchError && branches.length === 0 && !fetchingBranches && deployProjectId && (
                <p className="text-xs text-muted-foreground">
                  Uses the project's default branch if left empty.
                </p>
              )}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeployOpen(false)}>Cancel</Button>
            <Button
              onClick={handleDeploy}
              disabled={!deployProjectId || createDeployment.isPending || fetchingBranches}
              className="gap-1.5"
            >
              {createDeployment.isPending
                ? <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                : <Rocket className="h-3.5 w-3.5" />}
              Deploy
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
