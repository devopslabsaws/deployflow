"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  FileSearch2,
  Search,
  ChevronLeft,
  ChevronRight,
  RefreshCw,
  PlusCircle,
  Trash2,
  Pencil,
  LogIn,
  Rocket,
  UserPlus,
  KeyRound,
  ShieldAlert,
  type LucideIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useAuditLogs } from "@/hooks/use-api";
import type { AuditLog } from "@/types";
import { formatRelativeTime } from "@/lib/utils";

const RESOURCE_TYPES = [
  "all",
  "Project",
  "Server",
  "Deployment",
  "Domain",
  "Database",
  "Pipeline",
  "Team",
  "ApiKey",
  "SshKey",
];

const actionColor = (action: string) => {
  if (action.startsWith("Create")) return "bg-emerald-500/10 text-emerald-600 border-emerald-500/30";
  if (action.startsWith("Delete")) return "bg-red-500/10 text-red-500 border-red-500/30";
  if (action.startsWith("Update") || action.startsWith("Edit")) return "bg-amber-500/10 text-amber-600 border-amber-500/30";
  return "bg-muted text-muted-foreground";
};

const actionIcon = (action: string): LucideIcon => {
  if (action.startsWith("Create")) return PlusCircle;
  if (action.startsWith("Delete")) return Trash2;
  if (action.startsWith("Update") || action.startsWith("Edit")) return Pencil;
  if (action.toLowerCase().includes("login") || action.toLowerCase().includes("logout")) return LogIn;
  if (action.toLowerCase().includes("deploy")) return Rocket;
  if (action.toLowerCase().includes("invite") || action.toLowerCase().includes("user")) return UserPlus;
  if (action.toLowerCase().includes("key") || action.toLowerCase().includes("permission")) return KeyRound;
  return ShieldAlert;
};

function UserAvatar({ name }: { name: string }) {
  const initials = name
    .split(" ")
    .map((n) => n[0]?.toUpperCase() ?? "")
    .slice(0, 2)
    .join("");
  const hue = (name.charCodeAt(0) * 37 + name.charCodeAt(name.length - 1) * 13) % 360;
  return (
    <div
      className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-[10px] font-bold text-white"
      style={{ background: `hsl(${hue},60%,45%)` }}
    >
      {initials || "?"}
    </div>
  );
}

export default function AuditLogsPage() {
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [resourceType, setResourceType] = useState("all");
  const [search, setSearch] = useState("");

  const { data, isLoading, refetch, isFetching } = useAuditLogs({
    page,
    pageSize,
    resourceType: resourceType !== "all" ? resourceType : undefined,
  });

  const logs: AuditLog[] = data?.data ?? [];
  const total = data?.total ?? 0;
  const totalPages = data?.totalPages ?? 1;

  const filtered = search.trim()
    ? logs.filter(
        (l) =>
          l.userName.toLowerCase().includes(search.toLowerCase()) ||
          l.action.toLowerCase().includes(search.toLowerCase()) ||
          l.resourceName.toLowerCase().includes(search.toLowerCase())
      )
    : logs;

  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      className="space-y-5"
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl border bg-muted/40">
            <FileSearch2 className="h-5 w-5 text-primary" />
          </div>
          <div>
            <h1 className="text-xl font-bold tracking-tight">Audit Logs</h1>
            <p className="text-sm text-muted-foreground">
              Track all actions performed in your workspace
            </p>
          </div>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => refetch()}
          disabled={isFetching}
          className="gap-2"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${isFetching ? "animate-spin" : ""}`} />
          Refresh
        </Button>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-2">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search by user, action, or resource..."
            className="pl-8 h-9"
          />
        </div>
        <Select value={resourceType} onValueChange={(v) => { setResourceType(v); setPage(1); }}>
          <SelectTrigger className="h-9 w-[160px]">
            <SelectValue placeholder="Resource type" />
          </SelectTrigger>
          <SelectContent>
            {RESOURCE_TYPES.map((rt) => (
              <SelectItem key={rt} value={rt}>
                {rt === "all" ? "All resources" : rt}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Table */}
      <Card className="overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/30">
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">User</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Action</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Resource</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">IP Address</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Time</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                Array.from({ length: 8 }).map((_, i) => (
                  <tr key={i} className="border-b">
                    {Array.from({ length: 5 }).map((__, j) => (
                      <td key={j} className="px-4 py-3">
                        <Skeleton className="h-4 w-full" />
                      </td>
                    ))}
                  </tr>
                ))
              ) : filtered.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-4 py-12 text-center text-muted-foreground">
                    No audit log entries found.
                  </td>
                </tr>
              ) : (
                filtered.map((log) => (
                  <tr key={log.id} className="border-b transition-colors hover:bg-muted/20">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <UserAvatar name={log.userName} />
                        <span className="font-medium">{log.userName}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <Badge variant="outline" className={`inline-flex items-center gap-1.5 ${actionColor(log.action)}`}>
                        {(() => { const Icon = actionIcon(log.action); return <Icon className="h-3 w-3" />; })()}
                        {log.action}
                      </Badge>
                    </td>
                    <td className="px-4 py-3">
                      <span className="text-muted-foreground">{log.resourceType}: </span>
                      <span className="font-medium">{log.resourceName}</span>
                    </td>
                    <td className="px-4 py-3 text-muted-foreground font-mono text-xs">
                      {log.ipAddress ?? "—"}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground text-xs">
                      {formatRelativeTime(log.createdAt)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between border-t px-4 py-3">
            <span className="text-sm text-muted-foreground">
              Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, total)} of {total} entries
            </span>
            <div className="flex items-center gap-1">
              <Button
                variant="outline"
                size="sm"
                className="h-8 w-8 p-0"
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <span className="px-2 text-sm text-muted-foreground">
                {page} / {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                className="h-8 w-8 p-0"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}
      </Card>
    </motion.div>
  );
}
