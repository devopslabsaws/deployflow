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

type LogLevel = "info" | "warn" | "error" | "debug" | "all";

interface LogLine {
  id: string;
  timestamp: string;
  level: "info" | "warn" | "error" | "debug";
  message: string;
  service?: string;
}

// Simulated log stream data
const generateLogs = (count: number, offset: number = 0): LogLine[] => {
  const levels: Array<"info" | "warn" | "error" | "debug"> = ["info", "info", "info", "warn", "error", "debug"];
  const services = ["api", "frontend", "worker", "nginx", "postgres"];
  const messages = [
    "Starting HTTP server on :3000",
    "Connected to database successfully",
    "Processing request GET /api/health",
    "Cache miss for key user:12345",
    "Request completed in 23ms [200]",
    "Worker job completed successfully",
    "High memory usage detected: 87%",
    "ERROR: Connection timeout after 30s",
    "Retrying failed request (attempt 2/3)",
    "Build step: Installing dependencies",
    "✓ Build completed in 45.2s",
    "Container started with ID abc123def",
    "Health check passed",
    "SSL certificate renewed successfully",
    "Deployment rollout 60% complete",
  ];

  return Array.from({ length: count }, (_, i) => ({
    id: `log-${offset + i}`,
    timestamp: new Date(Date.now() - (count - i) * 2000).toISOString(),
    level: levels[Math.floor(Math.random() * levels.length)],
    message: messages[Math.floor(Math.random() * messages.length)],
    service: services[Math.floor(Math.random() * services.length)],
  }));
};

const levelConfig = {
  info: { badge: "bg-blue-500/10 text-blue-500 border-blue-500/30", text: "text-foreground" },
  warn: { badge: "bg-warning/10 text-warning border-warning/30", text: "text-warning" },
  error: { badge: "bg-destructive/10 text-destructive border-destructive/30", text: "text-destructive" },
  debug: { badge: "bg-muted text-muted-foreground", text: "text-muted-foreground" },
};

export default function LogsPage() {
  const [logs, setLogs] = useState<LogLine[]>(() => generateLogs(80));
  const [search, setSearch] = useState("");
  const [levelFilter, setLevelFilter] = useState<LogLevel>("all");
  const [serviceFilter, setServiceFilter] = useState("all");
  const [autoScroll, setAutoScroll] = useState(true);
  const [isStreaming, setIsStreaming] = useState(true);
  const bottomRef = useRef<HTMLDivElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  // Simulate real-time log streaming
  useEffect(() => {
    if (!isStreaming) return;
    const interval = setInterval(() => {
      const newLogs = generateLogs(1, logs.length);
      setLogs((prev) => [...prev.slice(-500), ...newLogs]);
    }, 2000);
    return () => clearInterval(interval);
  }, [isStreaming, logs.length]);

  useEffect(() => {
    if (autoScroll && bottomRef.current) {
      bottomRef.current.scrollIntoView({ behavior: "smooth" });
    }
  }, [logs, autoScroll]);

  const filteredLogs = logs.filter((log) => {
    if (levelFilter !== "all" && log.level !== levelFilter) return false;
    if (serviceFilter !== "all" && log.service !== serviceFilter) return false;
    if (search && !log.message.toLowerCase().includes(search.toLowerCase())) return false;
    return true;
  });

  const handleDownload = () => {
    const content = filteredLogs
      .map((l) => `[${l.timestamp}] [${l.level.toUpperCase()}] [${l.service}] ${l.message}`)
      .join("\n");
    const blob = new Blob([content], { type: "text/plain" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `logs-${new Date().toISOString().split("T")[0]}.txt`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const services = ["all", ...Array.from(new Set(logs.map((l) => l.service).filter(Boolean)))];

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
          <Button variant="outline" size="sm" onClick={handleDownload} className="gap-1.5">
            <Download className="w-3.5 h-3.5" />Download
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
            {(services as string[]).map((s) => (
              <SelectItem key={s} value={s}>
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
          {filteredLogs.map((log) => {
            const cfg = levelConfig[log.level];
            return (
              <div
                key={log.id}
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
