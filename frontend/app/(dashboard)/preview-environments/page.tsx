"use client";

import { useState } from "react";
import {
  GitPullRequest,
  ExternalLink,
  Trash2,
  RefreshCw,
  GitBranch,
  CheckCircle2,
  XCircle,
  Clock,
  Merge,
  Search,
  Plus,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import {
  usePreviewEnvironments,
  useCleanupPreviewEnvironment,
  useCreatePreviewEnvironment,
  type PreviewEnvironmentDto,
} from "@/hooks/use-api";
import { useProjects } from "@/hooks/use-api";
import { toast } from "sonner";
import { formatDistanceToNow } from "date-fns";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

const STATUS_CONFIG: Record<
  string,
  { label: string; variant: "default" | "secondary" | "destructive" | "outline"; icon: React.ComponentType<{ className?: string }> }
> = {
  building: { label: "Building", variant: "secondary", icon: RefreshCw },
  live: { label: "Live", variant: "default", icon: CheckCircle2 },
  merged: { label: "Merged", variant: "outline", icon: Merge },
  closed: { label: "Closed", variant: "outline", icon: XCircle },
  failed: { label: "Failed", variant: "destructive", icon: XCircle },
};

function StatusBadge({ status }: { status: string }) {
  const cfg = STATUS_CONFIG[status] ?? { label: status, variant: "secondary", icon: Clock };
  const Icon = cfg.icon;
  return (
    <Badge variant={cfg.variant} className="gap-1">
      <Icon className="w-3 h-3" />
      {cfg.label}
    </Badge>
  );
}

function PreviewCard({
  preview,
  onDelete,
}: {
  preview: PreviewEnvironmentDto;
  onDelete: (id: string) => void;
}) {
  return (
    <Card className="hover:border-primary/40 transition-colors">
      <CardContent className="pt-5 pb-4">
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-start gap-3 min-w-0">
            <GitPullRequest className="w-5 h-5 mt-0.5 text-primary shrink-0" />
            <div className="min-w-0">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="font-semibold text-sm">#{preview.prNumber}</span>
                <span className="font-medium truncate text-sm">{preview.prTitle}</span>
                <StatusBadge status={preview.status} />
              </div>
              <div className="flex items-center gap-2 mt-1 text-xs text-muted-foreground">
                <GitBranch className="w-3 h-3" />
                <span className="font-mono">{preview.branch}</span>
                <span>·</span>
                <span>{formatDistanceToNow(new Date(preview.createdAt), { addSuffix: true })}</span>
              </div>
              {preview.url && (
                <a
                  href={preview.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-1 text-xs text-primary hover:underline flex items-center gap-1"
                >
                  <ExternalLink className="w-3 h-3" />
                  {preview.url}
                </a>
              )}
            </div>
          </div>
          <Button
            size="icon"
            variant="ghost"
            className="text-destructive hover:bg-destructive/10 shrink-0"
            onClick={() => onDelete(preview.id)}
            title="Remove preview"
          >
            <Trash2 className="w-4 h-4" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

export default function PreviewEnvironmentsPage() {
  const [search, setSearch] = useState("");
  const [filterProject, setFilterProject] = useState<string>("all");
  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState({ projectId: "", prNumber: "", prTitle: "", branch: "" });

  // Auto-fill branch when project changes
  const handleProjectChange = (projectId: string) => {
    const proj = projectList.find((p) => p.id === projectId) as any;
    setForm((f) => ({
      ...f,
      projectId,
      branch: proj?.defaultBranch ?? proj?.branch ?? "main",
    }));
  };

  // Auto-suggest next PR number based on existing previews for this project
  const suggestPrNumber = (projectId: string) => {
    const existing = (previews ?? []).filter((p) => p.projectId === projectId || !p.projectId);
    if (existing.length === 0) return "";
    const max = Math.max(...existing.map((p) => p.prNumber ?? 0));
    return String(max + 1);
  };

  const { data: previews, isLoading } = usePreviewEnvironments(
    filterProject !== "all" ? filterProject : undefined
  );
  const cleanup = useCleanupPreviewEnvironment();
  const create = useCreatePreviewEnvironment();
  const { data: projects } = useProjects();
  const projectList = projects?.data ?? [];

  const filtered = (previews ?? []).filter(
    (p) =>
      p.prTitle.toLowerCase().includes(search.toLowerCase()) ||
      p.branch.toLowerCase().includes(search.toLowerCase()) ||
      String(p.prNumber).includes(search)
  );

  function handleDelete(id: string) {
    cleanup.mutate(id, {
      onSuccess: () => toast.success("Preview environment removed"),
      onError: () => toast.error("Failed to remove preview"),
    });
  }

  function handleCreate() {
    create.mutate(
      {
        projectId: form.projectId,
        prNumber: Number(form.prNumber),
        prTitle: form.prTitle,
        branch: form.branch,
      },
      {
        onSuccess: () => {
          setCreateOpen(false);
          setForm({ projectId: "", prNumber: "", prTitle: "", branch: "" });
          toast.success("Preview environment created");
        },
        onError: () => toast.error("Failed to create preview"),
      }
    );
  }

  const stats = {
    total: (previews ?? []).length,
    live: (previews ?? []).filter((p) => p.status === "live").length,
    building: (previews ?? []).filter((p) => p.status === "building").length,
  };

  return (
    <div className="space-y-6 p-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <GitPullRequest className="w-6 h-6 text-primary" />
            Preview Environments
          </h1>
          <p className="text-muted-foreground mt-1 text-sm">
            Ephemeral environments spun up automatically for every pull request.
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="mr-2 w-4 h-4" /> New Preview
        </Button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-3 gap-4">
        {[
          { label: "Total", value: stats.total },
          { label: "Live", value: stats.live },
          { label: "Building", value: stats.building },
        ].map((s) => (
          <Card key={s.label}>
            <CardContent className="pt-5 pb-4 text-center">
              <p className="text-3xl font-bold">{s.value}</p>
              <p className="text-sm text-muted-foreground mt-1">{s.label}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Filters */}
      <div className="flex gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input
            placeholder="Search PR title, branch, number…"
            className="pl-9"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <Select value={filterProject} onValueChange={setFilterProject}>
          <SelectTrigger className="w-48">
            <SelectValue placeholder="All projects" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All projects</SelectItem>
            {projectList.map((p) => (
              <SelectItem key={p.id} value={p.id}>
                {p.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* List */}
      {isLoading ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-24 rounded-lg" />
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center space-y-3">
            <GitPullRequest className="w-12 h-12 mx-auto text-muted-foreground/40" />
            <p className="text-muted-foreground">
              {search ? "No preview environments match your search." : "No preview environments yet."}
            </p>
            <p className="text-xs text-muted-foreground">
              Enable &quot;Preview Deployments&quot; in your project settings to auto-create previews for
              every PR.
            </p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {filtered.map((preview) => (
            <PreviewCard key={preview.id} preview={preview} onDelete={handleDelete} />
          ))}
        </div>
      )}

      {/* Create Dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create Preview Environment</DialogTitle>
            <DialogDescription>
              Manually create a preview environment for a pull request.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label>Project</Label>
              <Select
                value={form.projectId}
                onValueChange={(v) => {
                  handleProjectChange(v);
                  setForm((f) => ({ ...f, prNumber: suggestPrNumber(v) || f.prNumber }));
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select project" />
                </SelectTrigger>
                <SelectContent>
                  {projectList.map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>PR Number</Label>
                <Input
                  type="number"
                  placeholder="42"
                  value={form.prNumber}
                  onChange={(e) => setForm((f) => ({ ...f, prNumber: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Branch</Label>
                <Input
                  placeholder="feat/my-feature"
                  value={form.branch}
                  onChange={(e) => setForm((f) => ({ ...f, branch: e.target.value }))}
                />
                <p className="text-xs text-muted-foreground">e.g. <code className="bg-muted px-1 rounded font-mono text-[10px]">feature/login</code>, <code className="bg-muted px-1 rounded font-mono text-[10px]">fix/bug-123</code></p>
              </div>
            </div>
            <div className="space-y-2">
              <Label>PR Title</Label>
              <Input
                placeholder="Add new user dashboard"
                value={form.prTitle}
                onChange={(e) => setForm((f) => ({ ...f, prTitle: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={handleCreate}
              disabled={
                create.isPending ||
                !form.projectId ||
                !form.prNumber ||
                !form.prTitle ||
                !form.branch
              }
            >
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
