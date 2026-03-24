"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  ArrowLeft,
  Plus,
  Trash2,
  Eye,
  EyeOff,
  Lock,
  FileText,
  Variable,
  Save,
  Loader2,
  Search,
  ShieldCheck,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { ConfirmActionDialog } from "@/components/ui/confirm-action-dialog";
import { Textarea } from "@/components/ui/textarea";
import { useProject, useEnvVars, useUpsertEnvVar } from "@/hooks/use-api";
import { useQueryClient, useMutation } from "@tanstack/react-query";
import { apiClient } from "@/lib/api-client";
import { queryKeys } from "@/hooks/use-api";
import { toast } from "sonner";
import type { EnvVariable, EnvVariableType } from "@/types";
import { cn } from "@/lib/utils";

const TYPE_CONFIG: Record<EnvVariableType, { label: string; icon: React.ComponentType<{ className?: string }> }> = {
  plaintext: { label: "Plaintext", icon: Variable },
  secret: { label: "Secret", icon: Lock },
  file: { label: "File", icon: FileText },
};

function EnvRow({
  variable,
  onEdit,
  onDelete,
}: {
  variable: EnvVariable;
  onEdit: (v: EnvVariable) => void;
  onDelete: (id: string) => void;
}) {
  const [revealed, setRevealed] = useState(false);
  const cfg = TYPE_CONFIG[variable.type] ?? TYPE_CONFIG.plaintext;
  const Icon = cfg.icon;
  const isSecret = variable.type === "secret";
  const displayValue = isSecret && !revealed ? "••••••••••••••••" : (variable.value ?? "");

  return (
    <div className="flex items-center gap-3 px-4 py-3 border-b last:border-0 hover:bg-muted/30 transition-colors group">
      <Icon className="w-4 h-4 text-muted-foreground shrink-0" />
      <span className="font-mono text-sm font-medium w-56 shrink-0 truncate">{variable.key}</span>
      <div className="flex-1 flex items-center gap-2 min-w-0">
        <span
          className={cn(
            "font-mono text-sm truncate flex-1",
            isSecret && !revealed ? "text-muted-foreground tracking-widest" : ""
          )}
        >
          {displayValue || <span className="text-muted-foreground italic">empty</span>}
        </span>
        {isSecret && (
          <Button
            size="icon"
            variant="ghost"
            className="w-6 h-6 opacity-0 group-hover:opacity-100"
            onClick={() => setRevealed((r) => !r)}
            title={revealed ? "Hide value" : "Reveal value"}
          >
            {revealed ? <EyeOff className="w-3.5 h-3.5" /> : <Eye className="w-3.5 h-3.5" />}
          </Button>
        )}
      </div>
      <Badge variant="outline" className="text-xs px-1.5 shrink-0">
        {cfg.label}
      </Badge>
      <div className="flex gap-1 opacity-0 group-hover:opacity-100 shrink-0">
        <Button size="sm" variant="ghost" className="h-7 text-xs" onClick={() => onEdit(variable)}>
          Edit
        </Button>
        <Button
          size="icon"
          variant="ghost"
          className="w-7 h-7 text-destructive hover:bg-destructive/10"
          onClick={() => onDelete(String(variable.id))}
        >
          <Trash2 className="w-3.5 h-3.5" />
        </Button>
      </div>
    </div>
  );
}

function EnvVarDialog({
  open,
  onOpenChange,
  initial,
  onSave,
  isPending,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  initial: Partial<EnvVariable> | null;
  onSave: (data: { key: string; value: string; type: EnvVariableType }) => void;
  isPending: boolean;
}) {
  const [key, setKey] = useState(initial?.key ?? "");
  const [value, setValue] = useState(initial?.value ?? "");
  const [type, setType] = useState<EnvVariableType>(initial?.type ?? "plaintext");
  const isFile = type === "file";

  // Reset when dialog opens
  const handleOpen = (v: boolean) => {
    if (v) {
      setKey(initial?.key ?? "");
      setValue(initial?.value ?? "");
      setType(initial?.type ?? "plaintext");
    }
    onOpenChange(v);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpen}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{initial?.id ? "Edit Variable" : "Add Variable"}</DialogTitle>
          <DialogDescription>
            Secret values are encrypted at rest and masked in the UI.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div className="space-y-2">
            <Label>Type</Label>
            <Select value={type} onValueChange={(v) => setType(v as EnvVariableType)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="plaintext">Plaintext</SelectItem>
                <SelectItem value="secret">
                  <div className="flex items-center gap-2">
                    <Lock className="w-3.5 h-3.5" /> Secret (encrypted)
                  </div>
                </SelectItem>
                <SelectItem value="file">File (multi-line)</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Key</Label>
            <Input
              placeholder="DATABASE_URL"
              value={key}
              onChange={(e) => setKey(e.target.value.toUpperCase().replace(/[^A-Z0-9_]/g, "_"))}
              className="font-mono"
              disabled={!!initial?.id}
            />
          </div>
          <div className="space-y-2">
            <Label>Value</Label>
            {isFile ? (
              <Textarea
                placeholder="-----BEGIN CERTIFICATE-----&#10;...&#10;-----END CERTIFICATE-----"
                value={value}
                onChange={(e) => setValue(e.target.value)}
                rows={6}
                className="font-mono text-xs"
              />
            ) : (
              <Input
                type={type === "secret" ? "password" : "text"}
                placeholder={type === "secret" ? "••••••••" : "value"}
                value={value}
                onChange={(e) => setValue(e.target.value)}
                className="font-mono"
              />
            )}
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={() => onSave({ key, value, type })}
            disabled={!key.trim() || isPending}
          >
            {isPending && <Loader2 className="mr-2 w-4 h-4 animate-spin" />}
            Save
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function EnvVariablesPage() {
  const params = useParams<{ id: string }>();
  const projectId = params.id;

  const { data: project } = useProject(projectId);
  const { data: envVars, isLoading } = useEnvVars(projectId);
  const upsert = useUpsertEnvVar(projectId);
  const qc = useQueryClient();

  const [search, setSearch] = useState("");
  const [filterType, setFilterType] = useState<string>("all");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<Partial<EnvVariable> | null>(null);
  const [deleteId, setDeleteId] = useState<string | null>(null);

  const deleteEnvVar = useMutation({
    mutationFn: (id: string) => apiClient.delete(`/env-variables/${id}`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.envVars(projectId) });
      toast.success("Variable deleted");
    },
    onError: () => toast.error("Failed to delete variable"),
  });

  const vars = (envVars ?? []).filter((v) => {
    const matchesSearch =
      v.key.toLowerCase().includes(search.toLowerCase()) ||
      (v.value ?? "").toLowerCase().includes(search.toLowerCase());
    const matchesType = filterType === "all" || v.type === filterType;
    return matchesSearch && matchesType;
  });

  function handleAdd() {
    setEditTarget(null);
    setDialogOpen(true);
  }

  function handleEdit(v: EnvVariable) {
    setEditTarget(v);
    setDialogOpen(true);
  }

  function handleSave(data: { key: string; value: string; type: EnvVariableType }) {
    upsert.mutate(
      { id: editTarget?.id, ...data },
      {
        onSuccess: () => {
          setDialogOpen(false);
          toast.success(editTarget?.id ? "Variable updated" : "Variable added");
        },
        onError: () => toast.error("Failed to save variable"),
      }
    );
  }

  const secretCount = (envVars ?? []).filter((v) => v.type === "secret").length;
  const totalCount = (envVars ?? []).length;

  return (
    <div className="space-y-6 p-6 max-w-4xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Link href="/projects" className="hover:text-foreground">
          Projects
        </Link>
        <span>/</span>
        <Link href={`/projects/${projectId}`} className="hover:text-foreground">
          {(project as any)?.name ?? projectId}
        </Link>
        <span>/</span>
        <span className="text-foreground font-medium">Environment Variables</span>
      </div>

      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <Variable className="w-6 h-6 text-primary" />
            Environment Variables
          </h1>
          <p className="text-muted-foreground mt-1 text-sm">
            Configure build and runtime variables. Secrets are encrypted at rest and masked in the UI.
          </p>
        </div>
        <Button onClick={handleAdd}>
          <Plus className="mr-2 w-4 h-4" /> Add Variable
        </Button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-3 gap-4">
        <Card>
          <CardContent className="pt-4 pb-3 flex items-center gap-3">
            <Variable className="w-5 h-5 text-muted-foreground" />
            <div>
              <p className="text-2xl font-bold">{totalCount}</p>
              <p className="text-xs text-muted-foreground">Total Variables</p>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-4 pb-3 flex items-center gap-3">
            <Lock className="w-5 h-5 text-primary" />
            <div>
              <p className="text-2xl font-bold">{secretCount}</p>
              <p className="text-xs text-muted-foreground">Secrets (Encrypted)</p>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-4 pb-3 flex items-center gap-3">
            <ShieldCheck className="w-5 h-5 text-green-500" />
            <div>
              <p className="text-sm font-semibold text-green-600">AES-256</p>
              <p className="text-xs text-muted-foreground">Encryption at rest</p>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <div className="flex gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input
            placeholder="Search variables…"
            className="pl-9"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <Select value={filterType} onValueChange={setFilterType}>
          <SelectTrigger className="w-40">
            <SelectValue placeholder="All types" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All types</SelectItem>
            <SelectItem value="plaintext">Plaintext</SelectItem>
            <SelectItem value="secret">Secret</SelectItem>
            <SelectItem value="file">File</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Variable List */}
      <Card className="overflow-hidden">
        {isLoading ? (
          <CardContent className="p-0">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="flex items-center gap-3 px-4 py-3 border-b last:border-0">
                <Skeleton className="w-4 h-4 rounded" />
                <Skeleton className="w-40 h-4" />
                <Skeleton className="flex-1 h-4" />
                <Skeleton className="w-16 h-5 rounded-full" />
              </div>
            ))}
          </CardContent>
        ) : vars.length === 0 ? (
          <CardContent className="py-12 text-center space-y-3">
            <Variable className="w-10 h-10 mx-auto text-muted-foreground/40" />
            <p className="text-muted-foreground text-sm">
              {search || filterType !== "all"
                ? "No variables match your filter."
                : "No environment variables yet. Click \"Add Variable\" to get started."}
            </p>
          </CardContent>
        ) : (
          <div>
            {/* Header row */}
            <div className="flex items-center gap-3 px-4 py-2 border-b bg-muted/30 text-xs text-muted-foreground font-medium">
              <span className="w-4" />
              <span className="w-56 shrink-0">KEY</span>
              <span className="flex-1">VALUE</span>
              <span className="w-20">TYPE</span>
              <span className="w-20 opacity-0">Actions</span>
            </div>
            {vars.map((v) => (
              <EnvRow
                key={String(v.id)}
                variable={v}
                onEdit={handleEdit}
                onDelete={(id) => setDeleteId(id)}
              />
            ))}
          </div>
        )}
      </Card>

      {/* Bulk import hint */}
      <Card className="border-dashed">
        <CardContent className="py-4 px-5">
          <p className="text-sm text-muted-foreground">
            <span className="font-medium text-foreground">Tip:</span> You can also set variables from
            the CLI using{" "}
            <code className="bg-muted px-1.5 py-0.5 rounded text-xs font-mono">
              df env-set {projectId.slice(0, 8)}… KEY=VALUE KEY2=VALUE2
            </code>
          </p>
        </CardContent>
      </Card>

      {/* Add/Edit Dialog */}
      <EnvVarDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        initial={editTarget}
        onSave={handleSave}
        isPending={upsert.isPending}
      />

      {/* Delete Confirm */}
      <ConfirmActionDialog
        open={!!deleteId}
        onOpenChange={(v) => !v && setDeleteId(null)}
        title="Delete variable?"
        description="This cannot be undone. Running deployments will not be affected immediately."
        confirmLabel="Delete"
        confirmVariant="destructive"
        onConfirm={() => {
          if (deleteId) deleteEnvVar.mutate(deleteId, { onSettled: () => setDeleteId(null) });
        }}
      />
    </div>
  );
}
