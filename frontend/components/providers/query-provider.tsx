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
            // Mock data never changes — serve from cache indefinitely
            // Real API — keep fresh for 5 minutes so navigating back is instant
            staleTime: isMock ? Infinity : 5 * 60 * 1000,
            gcTime: 30 * 60 * 1000, // hold cache for 30 min
            retry: (failureCount, error: any) => {
              if (isMock) return false; // mock never needs retries
              if (error?.response?.status === 401) return false;
              if (error?.response?.status === 403) return false;
              if (error?.response?.status === 404) return false;
              return failureCount < 1; // max 1 retry for real API
            },
            refetchOnWindowFocus: false,
            refetchOnReconnect: false,
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
