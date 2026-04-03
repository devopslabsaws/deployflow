"use client";

import { useEffect } from "react";
import { AlertTriangle, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("[GlobalError]", error);
  }, [error]);

  return (
    <html>
      <body className="min-h-screen bg-background flex flex-col items-center justify-center gap-6 text-center p-8">
        <div className="flex h-16 w-16 items-center justify-center rounded-full bg-red-500/10">
          <AlertTriangle className="h-8 w-8 text-red-500" />
        </div>
        <div className="space-y-2">
          <h2 className="text-xl font-semibold">Application Error</h2>
          <p className="max-w-sm text-sm text-muted-foreground">
            {error.message || "A critical error occurred. Please refresh and try again."}
          </p>
        </div>
        <Button onClick={reset} className="gap-2">
          <RefreshCw className="h-4 w-4" /> Reload application
        </Button>
      </body>
    </html>
  );
}
