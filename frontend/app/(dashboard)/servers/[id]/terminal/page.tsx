"use client";

import { useState, useEffect, useRef } from "react";
import Link from "next/link";
import { ArrowLeft, Send, Loader2, Terminal as TerminalIcon, Wifi, WifiOff, AlertCircle, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useServer, queryKeys } from "@/hooks/use-api";
import { useQueryClient } from "@tanstack/react-query";
import { cn } from "@/lib/utils";

function getToken(): string {
  try {
    const raw = localStorage.getItem("deployflow-auth");
    if (raw) return JSON.parse(raw)?.state?.accessToken ?? "";
  } catch {}
  return "";
}

// Strip ANSI/VT100 escape sequences so programs like nano, top, etc.
// don't produce garbage when run without a PTY.
function stripAnsi(str: string): string {
  return str.replace(/\x1b(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])/g, "");
}

// Interactive TUI programs require a full PTY — block them with a helpful hint.
const TUI_CMDS = new Set(["nano", "vim", "vi", "emacs", "pico", "less", "more",
  "man", "top", "htop", "mc", "nvim", "screen", "tmux"]);

function tuiHint(base: string, args: string): string {
  const file = args.trim();
  if (["nano", "vi", "vim", "emacs", "pico", "nvim"].includes(base))
    return file ? `View: cat ${file}    Append: echo '...' >> ${file}    Overwrite: echo '...' > ${file}` : "";
  if (["less", "more"].includes(base))
    return file ? `Use: cat ${file}   or   head -100 ${file}` : "Use: cat <file>   or   head -100 <file>";
  if (base === "man") return args ? `Try: ${args} --help` : "";
  if (["top", "htop"].includes(base)) return "Use: ps aux   or   cat /proc/loadavg";
  return "";
}

type HistoryEntry = { cmd: string; out: string; ts: string; error?: boolean };

export default function ServerTerminalPage({ params }: { params: { id: string } }) {
  const { id } = params;
  const { data: server, isLoading } = useServer(id);
  const queryClient = useQueryClient();
  const [command, setCommand] = useState("");
  const [history, setHistory] = useState<HistoryEntry[]>([]);
  const [running, setRunning] = useState(false);
  const [sshConnected, setSshConnected] = useState<boolean | null>(null);
  const [testingConnection, setTestingConnection] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [history]);

  useEffect(() => { inputRef.current?.focus(); }, []);

  // Probe SSH on mount -- transitions server Provisioning -> Online in DB
  useEffect(() => {
    if (!id) return;
    setTestingConnection(true);
    fetch(`/proxy/servers/${id}/test`, {
      method: "POST",
      headers: { Authorization: `Bearer ${getToken()}` },
    })
      .then(async (res) => {
        if (res.ok) {
          setSshConnected(true);
          queryClient.invalidateQueries({ queryKey: queryKeys.servers.detail(id) });
        } else {
          setSshConnected(false);
        }
      })
      .catch(() => setSshConnected(false))
      .finally(() => setTestingConnection(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const retestConnection = () => {
    setTestingConnection(true);
    setSshConnected(null);
    fetch(`/proxy/servers/${id}/test`, {
      method: "POST",
      headers: { Authorization: `Bearer ${getToken()}` },
    })
      .then(async (res) => {
        setSshConnected(res.ok);
        if (res.ok) queryClient.invalidateQueries({ queryKey: queryKeys.servers.detail(id) });
      })
      .catch(() => setSshConnected(false))
      .finally(() => setTestingConnection(false));
  };

  const push = (entry: HistoryEntry) => setHistory(h => [...h, entry]);

  const runCommand = async () => {
    const cmd = command.trim();
    if (!cmd || running) return;

    // Block interactive TUI programs — they require a PTY and won't work here.
    const [baseCmd, ...rest] = cmd.split(/\s+/);
    if (TUI_CMDS.has(baseCmd.toLowerCase())) {
      const hint = tuiHint(baseCmd.toLowerCase(), rest.join(" "));
      push({
        cmd,
        out: `[info] '${baseCmd}' is an interactive program and requires a full TTY terminal.${hint ? `\n${hint}` : ""}`,
        ts: new Date().toLocaleTimeString(),
      });
      return;
    }

    setCommand("");
    setRunning(true);
    const ts = new Date().toLocaleTimeString();

    try {
      const res = await fetch(`/proxy/servers/${id}/exec`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${getToken()}`,
        },
        body: JSON.stringify({ command: cmd }),
      });

      if (res.ok) {
        const data = await res.json();
        const rawOut = [
          data.stdOut ?? data.output ?? data.result ?? "",
          data.stdErr ? `\n[stderr]: ${data.stdErr}` : "",
        ].filter(Boolean).join("") || "(no output)";
        const output = stripAnsi(rawOut);
        push({ cmd, out: output, ts, error: data.exitCode !== 0 && data.exitCode !== undefined });
        if (!sshConnected) {
          setSshConnected(true);
          queryClient.invalidateQueries({ queryKey: queryKeys.servers.detail(id) });
        }
      } else if (res.status === 404 || res.status === 400) {
        const body = await res.json().catch(() => ({}));
        push({
          cmd,
          out: [
            `[info] ${body?.error || "SSH exec failed."}`,
            ``,
            `To connect manually:`,
            `  ssh ${(server as any)?.sshUser ?? "azureuser"}@${server?.ipAddress ?? "SERVER_IP"} -p ${server?.port ?? 22}`,
          ].join("\n"),
          ts,
        });
      } else if (res.status === 401) {
        push({ cmd, out: "Unauthorized. Please log in again.", ts, error: true });
      } else {
        const body = await res.text().catch(() => "");
        push({ cmd, out: `Server returned ${res.status}: ${body || res.statusText}`, ts, error: true });
      }
    } catch (err: any) {
      push({
        cmd,
        out: [
          `Could not reach exec endpoint (${err?.message ?? "network error"}).`,
          `Manual connection: ssh ${(server as any)?.sshUser ?? "azureuser"}@${server?.ipAddress ?? "SERVER_IP"} -p ${server?.port ?? 22}`,
        ].join("\n"),
        ts,
        error: true,
      });
    } finally {
      setRunning(false);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-4 max-w-4xl">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  const statusLabel = server?.status ?? "unknown";
  const isOnline = statusLabel === "online";
  const sshReachable = isOnline || sshConnected === true;
  const statusColor = isOnline
    ? "bg-emerald-500/15 text-emerald-500"
    : statusLabel === "provisioning"
    ? "bg-amber-500/15 text-amber-500"
    : "bg-red-500/15 text-red-500";

  return (
    <div className="space-y-4 max-w-5xl">
      <div className="flex items-center gap-3 flex-wrap">
        <Link href={`/servers/${id}`}>
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
            <ArrowLeft className="w-4 h-4" />
          </Button>
        </Link>
        <TerminalIcon className="w-5 h-5 text-muted-foreground" />
        <div>
          <h1 className="text-xl font-bold">Terminal</h1>
          <p className="text-sm text-muted-foreground">
            {server?.name ?? id} - {server?.ipAddress}
          </p>
        </div>
        <span className={cn("ml-auto text-xs font-medium px-2.5 py-1 rounded-full capitalize", statusColor)}>
          {statusLabel}
        </span>
      </div>

      {!isOnline && statusLabel !== "provisioning" && sshConnected !== true && (
        <Card className="border-amber-500/40 bg-amber-500/5">
          <CardContent className="py-3 flex items-center gap-2 text-sm text-amber-600 dark:text-amber-400">
            <AlertCircle className="w-4 h-4 shrink-0" />
            Server is <strong className="mx-1">{statusLabel}</strong>. Commands may fail until it is reachable via SSH.
          </CardContent>
        </Card>
      )}

      <Card className="glass-card font-mono border-zinc-800">
        <CardHeader className="pb-2 border-b border-zinc-800 bg-zinc-900/60 rounded-t-lg">
          <div className="flex items-center gap-1.5">
            <div className="w-3 h-3 rounded-full bg-red-500/80" />
            <div className="w-3 h-3 rounded-full bg-amber-400/80" />
            <div className="w-3 h-3 rounded-full bg-emerald-500/80" />
            <CardTitle className="text-xs font-normal text-zinc-400 ml-3 select-text">
              {(server as any)?.sshUser ?? "azureuser"}@{server?.ipAddress ?? id}
            </CardTitle>
            <div className="ml-auto flex items-center gap-2">
              <button
                onClick={retestConnection}
                disabled={testingConnection}
                title="Test SSH connection"
                className="text-zinc-500 hover:text-zinc-300 transition-colors disabled:opacity-40"
              >
                <RefreshCw className={cn("w-3 h-3", testingConnection && "animate-spin")} />
              </button>
              <span className={cn(
                "text-[10px] px-1.5 py-0.5 rounded flex items-center gap-1",
                testingConnection ? "bg-zinc-700 text-zinc-400"
                  : sshReachable ? "bg-emerald-500/20 text-emerald-400"
                  : "bg-zinc-700 text-zinc-400"
              )}>
                {testingConnection
                  ? <><Loader2 className="w-3 h-3 animate-spin" /> Connecting</>
                  : sshReachable
                  ? <><Wifi className="w-3 h-3" /> SSH</>
                  : <><WifiOff className="w-3 h-3" /> SSH (offline)</>
                }
              </span>
            </div>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          <div
            className="h-[420px] overflow-y-auto p-4 space-y-3 bg-zinc-950 rounded-b-lg cursor-text"
            onClick={() => inputRef.current?.focus()}
          >
            <p className="text-zinc-500 text-xs">
              DeployFlow SSH Terminal - {server?.name} ({server?.ipAddress})
              {!sshReachable && !testingConnection && " - server not online, commands may fail"}
            </p>

            {history.map((item, i) => (
              <div key={i} className="space-y-0.5">
                <div className="flex items-baseline gap-2 text-xs">
                  <span className="text-emerald-400 shrink-0">$</span>
                  <span className="text-zinc-100 break-all">{item.cmd}</span>
                  <span className="text-zinc-600 ml-auto text-[10px] shrink-0">{item.ts}</span>
                </div>
                <pre className={cn(
                  "text-xs whitespace-pre-wrap break-words pl-4 leading-relaxed",
                  item.error ? "text-red-400" : "text-zinc-300"
                )}>{item.out}</pre>
              </div>
            ))}

            {!running && (
              <div className="flex items-center gap-2 text-xs">
                <span className="text-emerald-400">$</span>
                <span className="w-2 h-3.5 bg-zinc-400 animate-pulse rounded-sm" />
              </div>
            )}

            <div ref={bottomRef} />
          </div>
        </CardContent>
      </Card>

      <div className="flex gap-2">
        <div className="flex items-center gap-2 flex-1 bg-zinc-950 border border-zinc-800 rounded-lg px-3 font-mono text-sm focus-within:border-zinc-600 transition-colors">
          <span className="text-emerald-400 shrink-0 select-none">$</span>
          <Input
            ref={inputRef}
            value={command}
            onChange={e => setCommand(e.target.value)}
            onKeyDown={e => {
              if (e.key === "Enter") runCommand();
              if (e.key === "c" && e.ctrlKey) { setCommand(""); setRunning(false); }
            }}
            placeholder="Enter command..."
            disabled={running}
            className="border-0 bg-transparent focus-visible:ring-0 text-zinc-100 placeholder:text-zinc-600 px-0 font-mono h-10"
          />
        </div>
        <Button onClick={runCommand} disabled={running || !command.trim()} className="shrink-0">
          {running ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
        </Button>
      </div>
      <p className="text-xs text-muted-foreground">Enter to run - Ctrl+C to clear</p>
    </div>
  );
}