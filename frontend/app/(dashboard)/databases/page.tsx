"use client";

import { useEffect, useRef, useState } from "react";
import { motion } from "framer-motion";
import {
  Plus,
  Database as DatabaseIcon,
  MoreVertical,
  RefreshCw,
  Trash2,
  Download,
  Eye,
  EyeOff,
  Copy,
  CheckCircle2,
  XCircle,
  Clock,
  HardDrive,
  RotateCcw,
  History,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useDatabases, useDeleteDatabase, useTriggerBackup, useRestoreJobs } from "@/hooks/use-api";
import { formatRelativeTime, cn } from "@/lib/utils";
import { toast } from "sonner";
import type { Database, DatabaseType } from "@/types";
import { CreateDatabaseDialog } from "@/components/databases/create-database-dialog";
import { BackupPolicyDialog } from "@/components/databases/backup-policy-dialog";
import { ConfirmActionDialog } from "@/components/ui/confirm-action-dialog";
import { apiClient } from "@/lib/api-client";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

const typeConfig: Record<DatabaseType, { color: string; bg: string; label: string }> = {
  postgresql: { color: "text-blue-500", bg: "bg-blue-500/10", label: "PostgreSQL" },
  mysql: { color: "text-orange-500", bg: "bg-orange-500/10", label: "MySQL" },
  mariadb: { color: "text-orange-400", bg: "bg-orange-400/10", label: "MariaDB" },
  mongodb: { color: "text-success", bg: "bg-success/10", label: "MongoDB" },
  redis: { color: "text-red-500", bg: "bg-red-500/10", label: "Redis" },
  mssql: { color: "text-purple-500", bg: "bg-purple-500/10", label: "SQL Server" },
  oracle: { color: "text-rose-600", bg: "bg-rose-600/10", label: "Oracle DB" },
};

const statusConfig = {
  running: { icon: CheckCircle2, color: "text-success", label: "Running" },
  stopped: { icon: XCircle, color: "text-muted-foreground", label: "Stopped" },
  creating: { icon: RefreshCw, color: "text-blue-500", label: "Creating" },
  restoring: { icon: RefreshCw, color: "text-warning", label: "Restoring" },
  error: { icon: XCircle, color: "text-destructive", label: "Error" },
};

interface BackupItem {
  id: string;
  fileName: string;
  status: string;
  sizeBytes: number;
  completedAt?: string;
}

interface RestoreJob {
  jobId: string;
  status: string;
  progressPercent: number;
  message: string;
  targetDatabaseName: string;
}

export default function DatabasesPage() {
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [backupPolicyOpen, setBackupPolicyOpen] = useState(false);
  const [backupPolicyDbId, setBackupPolicyDbId] = useState<string | null>(null);
  const [restoreOpen, setRestoreOpen] = useState(false);
  const [restoreDb, setRestoreDb] = useState<Database | null>(null);
  const [restoreHistoryDbId, setRestoreHistoryDbId] = useState<string | null>(null);
  const [backups, setBackups] = useState<BackupItem[]>([]);
  const [loadingBackups, setLoadingBackups] = useState(false);
  const [selectedBackupId, setSelectedBackupId] = useState("");
  const [targetDatabaseName, setTargetDatabaseName] = useState("");
  const [validationMessage, setValidationMessage] = useState<string | null>(null);
  const [validationOk, setValidationOk] = useState<boolean | null>(null);
  const [startingRestore, setStartingRestore] = useState(false);
  const [restoreJob, setRestoreJob] = useState<RestoreJob | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const { data: databases, isLoading, refetch } = useDatabases();
  const triggerBackup = useTriggerBackup();
  const deleteDatabase = useDeleteDatabase();

  const handleBackupPolicy = (dbId: string) => {
    setBackupPolicyDbId(dbId);
    setBackupPolicyOpen(true);
  };

  const handleBackup = async (id: string, name: string) => {
    try {
      await triggerBackup.mutateAsync(id);
      toast.success(`Backup started for "${name}"`);
    } catch (e: any) {
      toast.error("Backup failed", { description: e.message });
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteDatabase.mutateAsync(deleteTarget.id);
      toast.success(`Database "${deleteTarget.name}" deleted.`);
    } catch (e: any) {
      toast.error("Failed to delete database", { description: e.message });
    }
  };

  useEffect(() => {
    return () => {
      if (pollRef.current) {
        clearInterval(pollRef.current);
        pollRef.current = null;
      }
    };
  }, []);

  const openRestore = async (db: Database) => {
    setRestoreDb(db);
    setTargetDatabaseName(`${db.databaseName}_restore`);
    setValidationMessage(null);
    setValidationOk(null);
    setRestoreJob(null);
    setRestoreOpen(true);
    setLoadingBackups(true);

    try {
      const response = await apiClient.get<BackupItem[]>(`/databases/${db.id}/backups`);
      const items = Array.isArray(response) ? response : [];
      const completed = items.filter((b) => b.status === "completed");
      setBackups(completed);
      setSelectedBackupId(completed[0]?.id ?? "");
    } catch (e: any) {
      setBackups([]);
      setSelectedBackupId("");
      toast.error("Failed to load backups", { description: e.message });
    } finally {
      setLoadingBackups(false);
    }
  };

  const validateRestoreTarget = async () => {
    if (!restoreDb) return false;
    try {
      const result = await apiClient.post<any>(`/databases/${restoreDb.id}/restore/validate-target`, {
        targetDatabaseName,
      });

      const ok = Boolean(result?.isValid);
      setValidationOk(ok);
      setValidationMessage(result?.message ?? (ok ? "Target is valid" : "Target is invalid"));
      return ok;
    } catch (e: any) {
      setValidationOk(false);
      setValidationMessage(e.message);
      return false;
    }
  };

  const startRestore = async () => {
    if (!restoreDb || !selectedBackupId) return;
    setStartingRestore(true);

    try {
      const ok = await validateRestoreTarget();
      if (!ok) {
        setStartingRestore(false);
        return;
      }

      const job = await apiClient.post<RestoreJob>(`/databases/${restoreDb.id}/restore`, {
        backupId: selectedBackupId,
        targetDatabaseName,
      });
      setRestoreJob(job);
      toast.success("Restore job started.");

      if (pollRef.current) {
        clearInterval(pollRef.current);
      }

      pollRef.current = setInterval(async () => {
        try {
          const latest = await apiClient.get<RestoreJob>(`/databases/${restoreDb.id}/restore/jobs/${job.jobId}`);
          setRestoreJob(latest);

          if (latest.status === "completed" || latest.status === "failed") {
            if (pollRef.current) {
              clearInterval(pollRef.current);
              pollRef.current = null;
            }

            if (latest.status === "completed") {
              toast.success("Database restore completed.");
              refetch();
            } else {
              toast.error("Database restore failed", { description: latest.message });
            }
          }
        } catch {
          if (pollRef.current) {
            clearInterval(pollRef.current);
            pollRef.current = null;
          }
        }
      }, 1200);
    } catch (e: any) {
      toast.error("Failed to start restore", { description: e.message });
    } finally {
      setStartingRestore(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Databases</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {databases?.length ?? 0} database{(databases?.length ?? 0) !== 1 ? "s" : ""} managed
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={() => refetch()} className="gap-1.5">
            <RefreshCw className="h-4 w-4" />
            Refresh
          </Button>
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="w-4 h-4 mr-1.5" />
            New Database
          </Button>
        </div>
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {[...Array(4)].map((_, i) => <Skeleton key={i} className="h-56 rounded-xl" />)}
        </div>
      ) : !databases?.length ? (
        <EmptyDatabasesState onNew={() => setCreateOpen(true)} />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {databases.map((db) => (
            <DatabaseCard
              key={db.id}
              database={db}
              onBackup={() => handleBackup(db.id, db.name)}
              onBackupPolicy={() => handleBackupPolicy(db.id)}
              onRestore={() => openRestore(db)}
              onRestoreHistory={() => setRestoreHistoryDbId(db.id)}
              onDelete={() => setDeleteTarget({ id: db.id, name: db.name })}
            />
          ))}
        </div>
      )}

      <CreateDatabaseDialog open={createOpen} onOpenChange={setCreateOpen} />
      <BackupPolicyDialog
        isOpen={backupPolicyOpen}
        onOpenChange={setBackupPolicyOpen}
        databaseId={backupPolicyDbId || ""}
      />
      <ConfirmActionDialog
        open={!!deleteTarget}
        onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}
        title="Delete Database"
        description={deleteTarget
          ? `This permanently deletes database \"${deleteTarget.name}\" and cannot be undone.`
          : "This action cannot be undone."}
        confirmLabel="Delete Database"
        requireText={deleteTarget?.name}
        isConfirming={deleteDatabase.isPending}
        onConfirm={handleDelete}
      />

      <Dialog
        open={restoreOpen}
        onOpenChange={(open) => {
          setRestoreOpen(open);
          if (!open && pollRef.current) {
            clearInterval(pollRef.current);
            pollRef.current = null;
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Restore Database</DialogTitle>
            <DialogDescription>
              Start a restore job from a completed backup and monitor progress in real time.
            </DialogDescription>
          </DialogContent>
        </Dialog>

        {/* Restore History Dialog */}
        <RestoreHistoryDialog
          databaseId={restoreHistoryDbId}
          onClose={() => setRestoreHistoryDbId(null)}
        />
      </div>
    );
  }

  function RestoreHistoryDialog({ databaseId, onClose }: { databaseId: string | null; onClose: () => void }) {
    const { data: jobs, isLoading } = useRestoreJobs(databaseId ?? "");

    const statusColor = (s: string) => {
      if (s === "completed") return "text-success";
      if (s === "failed") return "text-destructive";
      if (s === "running") return "text-blue-500";
      return "text-muted-foreground";
    };

    return (
      <Dialog open={!!databaseId} onOpenChange={(open) => { if (!open) onClose(); }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <History className="h-4 w-4" />Restore History
            </DialogTitle>
            <DialogDescription>Past and in-progress database restore jobs.</DialogDescription>
          </DialogHeader>
          <div className="space-y-2 max-h-80 overflow-y-auto py-1">
            {isLoading && (
              <p className="text-sm text-muted-foreground text-center py-4">Loading...</p>
            )}
            {!isLoading && (!jobs || jobs.length === 0) && (
              <p className="text-sm text-muted-foreground text-center py-4">No restore jobs found.</p>
            )}
            {jobs?.map((job: any) => (
              <div key={job.jobId ?? job.id} className="rounded-md border border-border/60 p-3 space-y-1.5">
                <div className="flex items-center justify-between text-xs">
                  <span className="font-mono font-medium truncate">{job.targetDatabaseName}</span>
                  <Badge variant="outline" className={`text-[10px] ${statusColor(job.status)}`}>
                    {job.status}
                  </Badge>
                </div>
                {job.progressPercent != null && (
                  <Progress value={job.progressPercent} className="h-1.5" />
                )}
                <div className="flex items-center justify-between text-[10px] text-muted-foreground">
                  {job.startedAt && <span>Started {new Date(job.startedAt).toLocaleString()}</span>}
                  {job.completedAt && <span>Completed {new Date(job.completedAt).toLocaleString()}</span>}
                </div>
                {job.errorMessage && (
                  <p className="text-[10px] text-destructive">{job.errorMessage}</p>
                )}
              </div>
            ))}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={onClose}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    );
  }

  function DatabaseCard({
    database: db,
    onBackup,
    onBackupPolicy,
    onRestore,
    onRestoreHistory,
    onDelete,
  }: {
    database: Database;
    onBackup: () => void;
    onBackupPolicy: () => void;
    onRestore: () => void;
    onRestoreHistory: () => void;
    onDelete: () => void;
  })
            <div className="space-y-1.5">
              <Label htmlFor="restore-target-name">Restore Target Database Name</Label>
              <Input
                id="restore-target-name"
                value={targetDatabaseName}
                onChange={(e) => {
                  setTargetDatabaseName(e.target.value);
                  setValidationOk(null);
                  setValidationMessage(null);
                }}
                placeholder="my_database_restore"
              />
            </div>

            {validationMessage && (
              <p className={cn("text-xs", validationOk ? "text-success" : "text-destructive")}>
                {validationMessage}
              </p>
            )}

            {restoreJob && (
              <div className="rounded-md border border-border/70 p-3 space-y-2">
                <div className="flex items-center justify-between text-xs">
                  <span className="text-muted-foreground">Status</span>
                  <span className="font-medium">{restoreJob.status}</span>
                </div>
                <Progress value={restoreJob.progressPercent} className="h-2" />
                <p className="text-xs text-muted-foreground">{restoreJob.message}</p>
              </div>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={validateRestoreTarget} disabled={!restoreDb || !targetDatabaseName.trim()}>
              Validate Target
            </Button>
            <Button
              onClick={startRestore}
              disabled={startingRestore || !selectedBackupId || !targetDatabaseName.trim() || Boolean(restoreJob && restoreJob.status === "running")}
            >
              {startingRestore ? "Starting..." : "Start Restore"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function DatabaseCard({
  database: db,
  onBackup,
  onBackupPolicy,
  onRestore,
  onDelete,
}: {
  database: Database;
  onBackup: () => void;
  onBackupPolicy: () => void;
  onRestore: () => void;
  onDelete: () => void;
}) {
  const [showConnectionString, setShowConnectionString] = useState(false);
  const typeCfg = typeConfig[db.type];
  const statusCfg = statusConfig[db.status] ?? statusConfig.error;
  const StatusIcon = statusCfg.icon;

  const handleCopyConnection = () => {
    if (db.connectionString) {
      navigator.clipboard.writeText(db.connectionString);
      toast.success("Connection string copied!");
    }
  };

  return (
    <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} className="group">
      <Card className="glass-card hover:border-border/80 transition-all hover:shadow-md">
        <CardHeader className="pb-3">
          <div className="flex items-start justify-between">
            <div className="flex items-center gap-3 flex-1 min-w-0">
              <div className={cn("w-10 h-10 rounded-xl flex items-center justify-center text-sm font-bold", typeCfg.bg)}>
                <span className={typeCfg.color}>
                  {db.type === "postgresql" ? "PG" : db.type === "mysql" ? "MY" : db.type === "redis" ? "RD" : db.type === "mongodb" ? "MG" : db.type === "oracle" ? "ORA" : db.type === "mssql" ? "MS" : "DB"}
                </span>
              </div>
              <div className="min-w-0">
                <p className="font-semibold text-sm truncate">{db.name}</p>
                <p className="text-xs text-muted-foreground">{typeCfg.label} {db.version}</p>
              </div>
            </div>

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="sm" className="h-8 w-8 px-0 opacity-0 group-hover:opacity-100">
                  <MoreVertical className="w-4 h-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onClick={onBackup}>
                  <Download className="mr-2 w-4 h-4" />Backup Now
                </DropdownMenuItem>
                <DropdownMenuItem onClick={onBackupPolicy}>
                  <Clock className="mr-2 w-4 h-4" />Backup Policy
                </DropdownMenuItem>
                <DropdownMenuItem>
                  <DatabaseIcon className="mr-2 w-4 h-4" />Open Console
                </DropdownMenuItem>
                <DropdownMenuItem onClick={onRestore}>
                  <RotateCcw className="mr-2 w-4 h-4" />Restore
                                <DropdownMenuItem onClick={onRestoreHistory}>
                                  <History className="mr-2 w-4 h-4" />Restore History
                                </DropdownMenuItem>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem className="text-destructive" onClick={onDelete}>
                  <Trash2 className="mr-2 w-4 h-4" />Delete
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </CardHeader>

        <CardContent className="space-y-4">
          {/* Status */}
          <div className="flex items-center justify-between">
            <div className={cn("flex items-center gap-1.5 text-xs", statusCfg.color)}>
              <StatusIcon className={cn("w-3.5 h-3.5", db.status === "creating" || db.status === "restoring" ? "animate-spin" : "")} />
              <span className="font-medium">{statusCfg.label}</span>
            </div>
            <div className="flex items-center gap-1 text-xs text-muted-foreground">
              <HardDrive className="w-3 h-3" />
              <span>{db.storageGB}GB</span>
            </div>
          </div>

          {/* Connection String */}
          {db.connectionString && (
            <div className="rounded-md bg-muted/50 border border-border/50 p-2">
              <div className="flex items-center justify-between mb-1">
                <span className="text-[10px] text-muted-foreground uppercase tracking-wider">Connection</span>
                <div className="flex gap-1">
                  <Button variant="ghost" size="sm" className="h-5 w-5 p-0" onClick={() => setShowConnectionString(!showConnectionString)}>
                    {showConnectionString ? <EyeOff className="w-3 h-3" /> : <Eye className="w-3 h-3" />}
                  </Button>
                  <Button variant="ghost" size="sm" className="h-5 w-5 p-0" onClick={handleCopyConnection}>
                    <Copy className="w-3 h-3" />
                  </Button>
                </div>
              </div>
              <code className="text-[10px] text-muted-foreground break-all">
                {showConnectionString
                  ? db.connectionString
                  : `${db.type}://${db.username}:****@${db.host}:${db.port}/${db.databaseName}`}
              </code>
            </div>
          )}

          {/* Backup Info */}
          <div className="flex items-center justify-between text-xs border-t border-border/50 pt-3">
            <div className="flex items-center gap-1.5 text-muted-foreground">
              <Clock className="w-3 h-3" />
              <span>
                {db.lastBackupAt
                  ? `Backup: ${formatRelativeTime(db.lastBackupAt)}`
                  : "No backup yet"}
              </span>
            </div>
            <Badge
              variant="outline"
              className={cn(
                "text-[10px]",
                db.backupEnabled
                  ? "text-success border-success/30 bg-success/10"
                  : "text-muted-foreground"
              )}
            >
              {db.backupEnabled ? "Auto-backup on" : "No backup"}
            </Badge>
          </div>
        </CardContent>
      </Card>
    </motion.div>
  );
}

function EmptyDatabasesState({ onNew }: { onNew: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-20 text-center">
      <div className="w-16 h-16 rounded-2xl bg-muted flex items-center justify-center mb-4">
        <DatabaseIcon className="w-8 h-8 text-muted-foreground" />
      </div>
      <h3 className="text-lg font-semibold mb-2">No databases</h3>
      <p className="text-muted-foreground text-sm max-w-sm mb-6">
        Create managed databases with automatic backups, monitoring, and simple connection management.
      </p>
      <Button onClick={onNew}>
        <Plus className="w-4 h-4 mr-1.5" />
        Create Database
      </Button>
    </div>
  );
}
