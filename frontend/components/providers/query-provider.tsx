"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";

const isMock = process.env.NEXT_PUBLIC_USE_MOCK_API === "true";

export function QueryProvider({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            // Mock data never changes — serve from cache indefinitely.
            // Real API: default to 30 s stale — individual hooks can override.
            // This ensures navigating back to a page shows reasonably fresh data
            // without blocking on a network round-trip every single navigation.
            staleTime: isMock ? Infinity : 30_000,
            gcTime: 10 * 60 * 1000, // keep inactive cache for 10 min
            retry: (failureCount, error: any) => {
              if (isMock) return false;
              if (error?.response?.status === 401) return false;
              if (error?.response?.status === 403) return false;
              if (error?.response?.status === 404) return false;
              return failureCount < 1; // max 1 retry
            },
            // Refetch when the user switches back to the tab — gives "always fresh"
            // feel without forcing a spinner on every navigation.
            refetchOnWindowFocus: !isMock,
            refetchOnReconnect: !isMock,
          },
          mutations: {
            retry: false,
          },
        },
      })
  );

  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
}
