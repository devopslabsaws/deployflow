"use client";

import { useState } from "react";
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
import { useDatabases, useTriggerBackup } from "@/hooks/use-api";
import { formatRelativeTime, cn } from "@/lib/utils";
import { toast } from "sonner";
import type { Database, DatabaseType } from "@/types";
import { CreateDatabaseDialog } from "@/components/databases/create-database-dialog";

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

export default function DatabasesPage() {
  const [createOpen, setCreateOpen] = useState(false);
  const { data: databases, isLoading, refetch } = useDatabases();
  const triggerBackup = useTriggerBackup();

  const handleBackup = async (id: string, name: string) => {
    try {
      await triggerBackup.mutateAsync(id);
      toast.success(`Backup started for "${name}"`);
    } catch (e: any) {
      toast.error("Backup failed", { description: e.message });
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
            />
          ))}
        </div>
      )}

      <CreateDatabaseDialog open={createOpen} onOpenChange={setCreateOpen} />
    </div>
  );
}

function DatabaseCard({ database: db, onBackup }: { database: Database; onBackup: () => void }) {
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
                <DropdownMenuItem>
                  <DatabaseIcon className="mr-2 w-4 h-4" />Open Console
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem className="text-destructive">
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
