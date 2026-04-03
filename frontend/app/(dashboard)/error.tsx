"use client";

import { useEffect } from "react";
import { AlertTriangle, RefreshCw, Home } from "lucide-react";
import { Button } from "@/components/ui/button";
import Link from "next/link";

export default function DashboardError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("[DashboardError]", error);
  }, [error]);

  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center gap-6 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-destructive/10">
        <AlertTriangle className="h-8 w-8 text-destructive" />
      </div>
      <div className="space-y-2">
        <h2 className="text-xl font-semibold tracking-tight">Something went wrong</h2>
        <p className="max-w-sm text-sm text-muted-foreground">
          {error.message || "An unexpected error occurred while loading this page."}
        </p>
        {error.digest && (
          <p className="text-xs text-muted-foreground/50">Error ID: {error.digest}</p>
        )}
      </div>
      <div className="flex gap-3">
        <Button variant="outline" onClick={reset} className="gap-2">
          <RefreshCw className="h-4 w-4" /> Try again
        </Button>
        <Button asChild className="gap-2">
          <Link href="/dashboard">
            <Home className="h-4 w-4" /> Go to Dashboard
          </Link>
        </Button>
      </div>
    </div>
  );
}
