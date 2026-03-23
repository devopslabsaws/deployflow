"use client";

import { useEffect, useRef, useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { Terminal, CheckCircle2, XCircle, Loader2, Wifi, WifiOff } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/store/auth-store";

// @microsoft/signalr is declared in types/signalr.d.ts and installed via package.json
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type HubConnection = any;
// eslint-disable-next-line @typescript-eslint/no-require-imports, @typescript-eslint/no-explicit-any
const getSignalR = (): any => {
  try {
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    return require("@microsoft/signalr");
  } catch {
    return null;
  }
};

interface LogLine {
  id: string;
  message: string;
  stream?: string;
  timestamp: string;
}

interface DeploymentLiveLogProps {
  deploymentId: string;
  initialStatus?: string;
}

const SIGNALR_URL = process.env.NEXT_PUBLIC_API_URL
  ? `${process.env.NEXT_PUBLIC_API_URL}/hubs/logs`
  : "http://localhost:5000/hubs/logs";

export function DeploymentLiveLog({ deploymentId, initialStatus }: DeploymentLiveLogProps) {
  const token = useAuthStore((s) => s.accessToken);
  const [lines, setLines] = useState<LogLine[]>([]);
  const [connected, setConnected] = useState(false);
  const [status, setStatus] = useState(initialStatus ?? "");
  const [autoScroll, setAutoScroll] = useState(true);
  const bottomRef = useRef<HTMLDivElement>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!deploymentId) return;

    const signalR = getSignalR();
    if (!signalR) {
      console.warn("@microsoft/signalr not available — live logs disabled.");
      return;
    }

    const connection: HubConnection = new signalR.HubConnectionBuilder()
      .withUrl(SIGNALR_URL, {
        accessTokenFactory: () => token ?? "",
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on("log", (data: { message: string; stream?: string; timestamp: string }) => {
      setLines((prev) => [
        ...prev,
        { id: `${Date.now()}-${Math.random()}`, ...data },
      ]);
    });

    connection.on("statusChanged", (data: { status: string }) => {
      setStatus(data.status);
    });

    connection.onreconnecting(() => setConnected(false));
    connection.onreconnected(async () => {
      setConnected(true);
      await connection.invoke("SubscribeToDeployment", deploymentId);
    });
    connection.onclose(() => setConnected(false));

    connection
      .start()
      .then(async () => {
        setConnected(true);
        await connection.invoke("SubscribeToDeployment", deploymentId);
      })
      .catch((err: unknown) => {
        console.warn("SignalR connection failed:", err);
        setConnected(false);
      });

    connectionRef.current = connection;

    return () => {
      connection.invoke("UnsubscribeFromDeployment", deploymentId).catch(() => {});
      connection.stop();
      connectionRef.current = null;
    };
  }, [deploymentId, token]);

  // Auto-scroll to bottom when new lines arrive
  useEffect(() => {
    if (autoScroll) {
      bottomRef.current?.scrollIntoView({ behavior: "smooth" });
    }
  }, [lines, autoScroll]);

  const isActive = ["queued", "building", "deploying"].includes(status);
  const isSuccess = status === "healthy";
  const isFailed = status === "failed";

  return (
    <div className="space-y-3">
      {/* Status bar */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Badge
            variant="outline"
            className={`gap-1.5 ${
              connected
                ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-500"
                : "border-muted text-muted-foreground"
            }`}
          >
            {connected ? <Wifi className="h-3 w-3" /> : <WifiOff className="h-3 w-3" />}
            {connected ? "Live" : "Disconnected"}
          </Badge>

          {isActive && (
            <Badge variant="outline" className="gap-1 border-blue-500/30 bg-blue-500/10 text-blue-500">
              <Loader2 className="h-3 w-3 animate-spin" />
              {status}
            </Badge>
          )}
          {isSuccess && (
            <Badge variant="outline" className="gap-1 border-emerald-500/30 bg-emerald-500/10 text-emerald-500">
              <CheckCircle2 className="h-3 w-3" />
              Succeeded
            </Badge>
          )}
          {isFailed && (
            <Badge variant="outline" className="gap-1 border-destructive/30 bg-destructive/10 text-destructive">
              <XCircle className="h-3 w-3" />
              Failed
            </Badge>
          )}
        </div>

        <div className="flex items-center gap-2">
          <span className="text-xs text-muted-foreground">{lines.length} lines</span>
          <Button
            variant="ghost"
            size="sm"
            className="h-6 px-2 text-xs"
            onClick={() => setLines([])}
          >
            Clear
          </Button>
          <Button
            variant={autoScroll ? "secondary" : "ghost"}
            size="sm"
            className="h-6 px-2 text-xs"
            onClick={() => setAutoScroll((v) => !v)}
          >
            Auto-scroll
          </Button>
        </div>
      </div>

      {/* Log terminal */}
      <ScrollArea
        className="h-[400px] rounded-lg border border-border/40 bg-[#0d1117] font-mono text-xs"
        onScrollCapture={(e) => {
          const el = e.currentTarget.querySelector("[data-radix-scroll-area-viewport]") as HTMLElement;
          if (!el) return;
          const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 40;
          setAutoScroll(atBottom);
        }}
      >
        <div className="p-4 space-y-0.5">
          {lines.length === 0 ? (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              className="flex flex-col items-center justify-center py-16 text-center"
            >
              <Terminal className="h-8 w-8 mb-3 text-muted-foreground/30" />
              <p className="text-muted-foreground/50">
                {connected
                  ? "Waiting for deployment logs…"
                  : "Connect to see live deployment logs."}
              </p>
            </motion.div>
          ) : (
            <AnimatePresence initial={false}>
              {lines.map((line, i) => (
                <motion.div
                  key={line.id}
                  initial={{ opacity: 0, x: -4 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ duration: 0.1 }}
                  className={`flex gap-3 py-0.5 ${
                    line.stream === "stderr" ? "text-red-400" : "text-green-400"
                  }`}
                >
                  <span className="text-muted-foreground/30 select-none w-6 text-right shrink-0">
                    {i + 1}
                  </span>
                  <span className="text-muted-foreground/40 shrink-0 select-none">
                    {new Date(line.timestamp).toLocaleTimeString("en-US", {
                      hour12: false,
                      hour: "2-digit",
                      minute: "2-digit",
                      second: "2-digit",
                    })}
                  </span>
                  <span className="flex-1 whitespace-pre-wrap break-all">{line.message}</span>
                </motion.div>
              ))}
            </AnimatePresence>
          )}
          <div ref={bottomRef} />
        </div>
      </ScrollArea>
    </div>
  );
}
