"use client";

import { useEffect } from "react";
import { AlertTriangle, RefreshCw, ArrowLeft } from "lucide-react";
import { Button } from "@/components/ui/button";
import Link from "next/link";

export default function PipelinesError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("[PipelinesError]", error);
  }, [error]);

  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center gap-6 text-center">
      <div className="flex h-14 w-14 items-center justify-center rounded-full bg-destructive/10">
        <AlertTriangle className="h-7 w-7 text-destructive" />
      </div>
      <div className="space-y-1.5">
        <h2 className="text-lg font-semibold">Failed to load Pipelines</h2>
        <p className="max-w-xs text-sm text-muted-foreground">
          {error.message || "The pipelines page encountered an error."}
        </p>
      </div>
      <div className="flex gap-3">
        <Button variant="outline" size="sm" onClick={reset} className="gap-2">
          <RefreshCw className="h-4 w-4" /> Retry
        </Button>
        <Button variant="ghost" size="sm" asChild className="gap-2">
          <Link href="/dashboard">
            <ArrowLeft className="h-4 w-4" /> Dashboard
          </Link>
        </Button>
      </div>
    </div>
  );
}
