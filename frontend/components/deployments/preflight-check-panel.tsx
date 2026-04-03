"use client";

import { useState } from "react";
import { CheckCircle2, XCircle, AlertTriangle, Loader2, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { useRunPreflight, type PreflightCheckResult } from "@/hooks/use-api";
import { toast } from "sonner";

function CheckRow({ check }: { check: PreflightCheckResult }) {
  const Icon =
    check.status === "pass" ? CheckCircle2 :
    check.status === "warn" ? AlertTriangle :
    XCircle;

  const color =
    check.status === "pass" ? "text-emerald-500" :
    check.status === "warn" ? "text-amber-500"   :
    "text-destructive";

  return (
    <div className="flex items-start gap-3 py-1.5 border-b border-border/30 last:border-0">
      <Icon className={cn("h-4 w-4 mt-0.5 shrink-0", color)} />
      <div className="flex-1 min-w-0">
        <span className="text-sm font-medium">{check.name}</span>
        {check.message && (
          <p className="text-xs text-muted-foreground mt-0.5">{check.message}</p>
        )}
      </div>
      <Badge
        variant="outline"
        className={cn(
          "text-[10px] h-5 shrink-0",
          check.status === "pass" ? "border-emerald-500/40 text-emerald-500 bg-emerald-500/10" :
          check.status === "warn" ? "border-amber-500/40 text-amber-500 bg-amber-500/10" :
          "border-destructive/40 text-destructive bg-destructive/10"
        )}
      >
        {check.status.toUpperCase()}
      </Badge>
    </div>
  );
}

interface PreflightCheckPanelProps {
  projectId: string;
}

export function PreflightCheckPanel({ projectId }: PreflightCheckPanelProps) {
  const [report, setReport] = useState<{ canDeploy: boolean; checks: PreflightCheckResult[]; generatedAt: string } | null>(null);
  const runPreflight = useRunPreflight();

  const handleRun = async () => {
    try {
      const result = await runPreflight.mutateAsync(projectId);
      setReport(result);
      if (result.canDeploy) {
        toast.success("Preflight checks passed — server is ready");
      } else {
        toast.error("Preflight checks failed — resolve issues before deploying");
      }
    } catch (e: any) {
      toast.error("Preflight check failed", { description: e?.message });
    }
  };

  return (
    <div className="rounded-lg border border-border/50 bg-muted/5 p-4 space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <ShieldCheck className="h-4 w-4 text-primary" />
          <span className="text-sm font-semibold">Production Readiness Check</span>
        </div>
        <Button
          size="sm"
          variant="outline"
          className="h-7 text-xs gap-1.5"
          onClick={handleRun}
          disabled={runPreflight.isPending}
        >
          {runPreflight.isPending ? (
            <><Loader2 className="h-3 w-3 animate-spin" />Running…</>
          ) : (
            <><ShieldCheck className="h-3 w-3" />Run Preflight</>
          )}
        </Button>
      </div>

      <p className="text-xs text-muted-foreground">
        Validates SSH connectivity, Docker availability, disk space, memory, and port conflicts before deploying.
      </p>

      {report && (
        <div className="space-y-1 mt-2">
          <div className="flex items-center gap-2 mb-3">
            {report.canDeploy ? (
              <Badge className="gap-1 bg-emerald-500/20 text-emerald-500 border-emerald-500/40">
                <CheckCircle2 className="h-3 w-3" />Ready to Deploy
              </Badge>
            ) : (
              <Badge variant="destructive" className="gap-1">
                <XCircle className="h-3 w-3" />Not Ready
              </Badge>
            )}
            <span className="text-[11px] text-muted-foreground">
              {new Date(report.generatedAt).toLocaleTimeString()}
            </span>
          </div>
          {report.checks.map((check, i) => (
            <CheckRow key={i} check={check} />
          ))}
        </div>
      )}
    </div>
  );
}
