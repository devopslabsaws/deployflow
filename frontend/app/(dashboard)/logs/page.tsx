"use client";

import { useRef, useEffect, useState, useCallback } from "react";
import { motion } from "framer-motion";
import {
  Search,
  Download,
  RefreshCw,
  X,
  Filter,
  Terminal,
  ChevronDown,
  PlayCircle,
  PauseCircle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import { useLogs } from "@/hooks/use-api";
import { apiClient, BASE_URL } from "@/lib/api-client";
import { toast } from "sonner";

type LogLevel = "info" | "warn" | "error" | "debug" | "all";

interface LogLine {
  id: string;
  timestamp: string;
  level: string;
  message: string;
  service: string;
}

const levelConfig = {
  info: { badge: "bg-blue-500/10 text-blue-500 border-blue-500/30", text: "text-foreground" },
  warn: { badge: "bg-warning/10 text-warning border-warning/30", text: "text-warning" },
  error: { badge: "bg-destructive/10 text-destructive border-destructive/30", text: "text-destructive" },
  debug: { badge: "bg-muted text-muted-foreground", text: "text-muted-foreground" },
};

export default function LogsPage() {
  const [search, setSearch] = useState("");
  const [levelFilter, setLevelFilter] = useState<LogLevel>("all");
  const [serviceFilter, setServiceFilter] = useState("all");
  const [autoScroll, setAutoScroll] = useState(true);
  const [isStreaming, setIsStreaming] = useState(true);
  const [streamedLogs, setStreamedLogs] = useState<LogLine[]>([]);
  const [oldestCursor, setOldestCursor] = useState<number | null>(null);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const esRef = useRef<EventSource | null>(null);
  const seenIdsRef = useRef<Set<string>>(new Set());

  // Initial page load — fetch first batch via REST
  const { data: initialData } = useLogs({ pageSize: 200 });

  useEffect(() => {
    if (!initialData?.items) return;
    const newItems = initialData.items.filter((l: LogLine) => !seenIdsRef.current.has(l.id));
    if (newItems.length === 0) return;
    newItems.forEach((l: LogLine) => seenIdsRef.current.add(l.id));
    setStreamedLogs((prev) => [...newItems.slice().reverse(), ...prev]);
    if (initialData.nextCursor) setOldestCursor(initialData.nextCursor);
    setHasMore(initialData.hasMore ?? false);
  }, [initialData]);

  // SSE stream for live tailing
  useEffect(() => {
    if (!isStreaming) {
      esRef.current?.close();
      esRef.current = null;
      return;
    }

    const params = new URLSearchParams();
    if (levelFilter !== "all") params.set("level", levelFilter);
    if (serviceFilter !== "all") params.set("service", serviceFilter);

    // EventSource cannot send Authorization headers — pass token as query param instead.
    // The backend accepts `access_token` in the query string for SSE endpoints.
    try {
      const raw = localStorage.getItem("deployflow-auth");
      const token = raw ? (JSON.parse(raw)?.state?.accessToken ?? null) : null;
      if (token) params.set("access_token", token);
    } catch { /* ignore */ }

    const url = `${BASE_URL}/logs/stream?${params.toString()}`;
    const es = new EventSource(url);
    esRef.current = es;

    es.addEventListener("log", (e) => {
      try {
        const log: LogLine = JSON.parse(e.data);
        if (seenIdsRef.current.has(log.id)) return;
        seenIdsRef.current.add(log.id);
        setStreamedLogs((prev) => [...prev, log]);
      } catch {
        // ignore malformed messages
      }
    });

    es.onerror = () => {
      es.close();
      esRef.current = null;
      // brief backoff before reconnect is handled by toggling state
    };

    return () => {
      es.close();
      esRef.current = null;
    };
  }, [isStreaming, levelFilter, serviceFilter]);

  useEffect(() => {
    if (autoScroll && bottomRef.current) {
      bottomRef.current.scrollIntoView({ behavior: "smooth" });
    }
  }, [streamedLogs, autoScroll]);

  const loadOlderLogs = useCallback(async () => {
    if (!oldestCursor || loadingOlder) return;
    setLoadingOlder(true);
    try {
      const data = await apiClient.get<{ items: LogLine[]; nextCursor: number | null; hasMore: boolean }>(
        "/logs",
        { params: { cursor: oldestCursor, pageSize: 200 } }
      );
      const newItems = data.items.filter((l) => !seenIdsRef.current.has(l.id));
      newItems.forEach((l) => seenIdsRef.current.add(l.id));
      setStreamedLogs((prev) => [...newItems.slice().reverse(), ...prev]);
      setOldestCursor(data.nextCursor);
      setHasMore(data.hasMore ?? false);
    } catch (e: any) {
      toast.error("Failed to load older logs", { description: e.message });
    } finally {
      setLoadingOlder(false);
    }
  }, [oldestCursor, loadingOlder]);

  const filteredLogs = streamedLogs.filter((log) => {
    if (levelFilter !== "all" && log.level !== levelFilter) return false;
    if (serviceFilter !== "all" && log.service !== serviceFilter) return false;
    if (search && !log.message.toLowerCase().includes(search.toLowerCase())) return false;
    return true;
  });

  const handleExport = async () => {
    try {
      const body: Record<string, unknown> = {};
      if (levelFilter !== "all") body.level = levelFilter;
      if (serviceFilter !== "all") body.service = serviceFilter;
      if (search) body.search = search;
      body.limit = 50000;

      let exportToken: string | null = null;
      try {
        const raw = localStorage.getItem("deployflow-auth");
        exportToken = raw ? (JSON.parse(raw)?.state?.accessToken ?? null) : null;
      } catch { /* ignore */ }

      const response = await fetch(
        `${BASE_URL}/logs/export`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            ...(exportToken ? { Authorization: `Bearer ${exportToken}` } : {}),
          },
          body: JSON.stringify(body),
        }
      );
      if (!response.ok) throw new Error(`${response.status}`);
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `logs-${new Date().toISOString().split("T")[0]}.txt`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e: any) {
      toast.error("Export failed", { description: e.message });
    }
  };

  const services = [
    "all",
    ...Array.from(
      new Set(
        streamedLogs
          .map((l) => l.service)
          .filter((s): s is string => Boolean(s) && s !== "all")
      )
    ),
  ];

  return (
    <div className="space-y-4 flex flex-col h-[calc(100vh-10rem)]">
      {/* Header */}
      <div className="flex items-center justify-between shrink-0">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Logs</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            Real-time log streaming across all services
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={handleExport} className="gap-1.5">
            <Download className="w-3.5 h-3.5" />Export
          </Button>
          <Button
            variant={isStreaming ? "default" : "outline"}
            size="sm"
            onClick={() => setIsStreaming((s) => !s)}
            className="gap-1.5"
          >
            {isStreaming ? (
              <><PauseCircle className="w-3.5 h-3.5" />Pause</>
            ) : (
              <><PlayCircle className="w-3.5 h-3.5" />Resume</>
            )}
          </Button>
        </div>
      </div>

      {/* Filters */}
      <div className="flex gap-3 flex-wrap items-center shrink-0">
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input
            placeholder="Search logs..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9"
          />
          {search && (
            <Button
              variant="ghost"
              size="sm"
              className="absolute right-1 top-1/2 -translate-y-1/2 h-6 w-6 p-0"
              onClick={() => setSearch("")}
            >
              <X className="w-3 h-3" />
            </Button>
          )}
        </div>

        <Select value={levelFilter} onValueChange={(v) => setLevelFilter(v as LogLevel)}>
          <SelectTrigger className="w-36">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Levels</SelectItem>
            <SelectItem value="info">Info</SelectItem>
            <SelectItem value="warn">Warning</SelectItem>
            <SelectItem value="error">Error</SelectItem>
            <SelectItem value="debug">Debug</SelectItem>
          </SelectContent>
        </Select>

        <Select value={serviceFilter} onValueChange={setServiceFilter}>
          <SelectTrigger className="w-36">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(services as string[]).map((s, idx) => (
              <SelectItem key={`service-${s}-${idx}`} value={s}>
                {s === "all" ? "All Services" : s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <div className="flex items-center gap-2 ml-auto">
          <Switch
            id="auto-scroll"
            checked={autoScroll}
            onCheckedChange={setAutoScroll}
            className="scale-90"
          />
          <Label htmlFor="auto-scroll" className="text-xs cursor-pointer">
            Auto-scroll
          </Label>
        </div>

        <Badge variant="outline" className="text-xs">
          {filteredLogs.length} lines
        </Badge>
      </div>

      {/* Log Viewer */}
      <div
        ref={containerRef}
        className="flex-1 rounded-xl border border-border/50 bg-[#0d1117] overflow-y-auto font-mono text-xs"
      >
        <div className="sticky top-0 flex items-center gap-2 px-4 py-2 bg-[#161b22] border-b border-border/30">
          <Terminal className="w-3.5 h-3.5 text-muted-foreground" />
          <span className="text-muted-foreground text-xs">Log Stream</span>
          {isStreaming && (
            <Badge className="bg-success/20 text-success border-success/30 text-[9px] px-1.5 py-0 h-4 gap-1 ml-auto">
              <span className="w-1.5 h-1.5 rounded-full bg-success animate-pulse" />
              LIVE
            </Badge>
          )}
        </div>

        <div className="p-4 space-y-0.5">
          {hasMore && (
            <div className="flex justify-center py-2">
              <Button
                variant="ghost"
                size="sm"
                className="text-xs text-muted-foreground gap-1.5"
                onClick={loadOlderLogs}
                disabled={loadingOlder}
              >
                <ChevronDown className="w-3 h-3" />
                {loadingOlder ? "Loading..." : "Load older logs"}
              </Button>
            </div>
          )}

          {filteredLogs.map((log, idx) => {
            const cfg = levelConfig[log.level as keyof typeof levelConfig] ?? levelConfig.info;
            return (
              <div
                key={log.id || `${log.timestamp}-${log.service}-${idx}`}
                className="flex items-start gap-3 py-0.5 px-2 rounded hover:bg-white/5 transition-colors log-line group"
              >
                <span className="text-[10px] text-gray-600 shrink-0 pt-0.5 w-52">
                  {new Date(log.timestamp).toLocaleTimeString("en-US", {
                    hour12: false,
                    hour: "2-digit",
                    minute: "2-digit",
                    second: "2-digit",
                    fractionalSecondDigits: 3,
                  })}
                </span>
                <Badge
                  variant="outline"
                  className={cn("text-[9px] px-1 py-0 h-4 shrink-0 font-mono uppercase border", cfg.badge)}
                >
                  {log.level}
                </Badge>
                {log.service && (
                  <span className="text-[10px] text-purple-400 shrink-0 w-16">{log.service}</span>
                )}
                <span className={cn("flex-1 break-all", cfg.text)}>{log.message}</span>
              </div>
            );
          })}
          <div ref={bottomRef} />
        </div>
      </div>
    </div>
  );
}
