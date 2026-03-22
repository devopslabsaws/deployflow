"use client";

import { useState } from "react";
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
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
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
import { DeploymentStatusBadge } from "@/components/deployments/deployment-status-badge";
import {
  useDeployments,
  useCancelDeployment,
  useRollbackDeployment,
} from "@/hooks/use-api";
import { formatRelativeTime, formatDuration, truncate } from "@/lib/utils";
import { toast } from "sonner";
import Link from "next/link";
import type { DeploymentTrigger } from "@/types";

const triggerLabels: Record<DeploymentTrigger, string> = {
  git_push: "Git Push",
  manual: "Manual",
  api: "API",
  schedule: "Schedule",
  rollback: "Rollback",
};

export default function DeploymentsPage() {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [page, setPage] = useState(1);
  const pageSize = 15;

  const { data, isLoading, refetch } = useDeployments({
    status: statusFilter === "all" ? undefined : statusFilter,
    page,
    pageSize,
  });

  const cancelDeployment = useCancelDeployment();
  const rollbackDeployment = useRollbackDeployment();

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
        <Button variant="outline" size="sm" onClick={() => refetch()} className="gap-1.5">
          <RefreshCw className="w-3.5 h-3.5" />
          Refresh
        </Button>
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
                    <span className="text-xs text-muted-foreground">
                      {formatRelativeTime(d.createdAt)}
                    </span>
                  </TableCell>

                  <TableCell className="text-right">
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
    </div>
  );
}
