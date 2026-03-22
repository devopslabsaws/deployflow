"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";
import { AppSidebar } from "@/components/layout/app-sidebar";
import { AppTopbar } from "@/components/layout/app-topbar";
import { useAuthStore } from "@/store/auth-store";

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
  const [hydrated, setHydrated] = useState(false);
  const { isAuthenticated } = useAuthStore();
  const router = useRouter();

  // Wait for Zustand persist to rehydrate from localStorage
  useEffect(() => {
    setHydrated(true);
  }, []);

  useEffect(() => {
    if (hydrated && !isAuthenticated) {
      router.replace("/login");
    }
  }, [hydrated, isAuthenticated, router]);

  if (!hydrated || !isAuthenticated) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-primary" />
      </div>
    );
  }

  return (
    <div className="flex h-screen w-full overflow-hidden bg-background">
      {/* Desktop sidebar — always a static flex item */}
      <div className="hidden shrink-0 md:flex">
        <AppSidebar />
      </div>

      {/* Mobile sidebar — fixed overlay, shown only when toggled */}
      {mobileSidebarOpen && (
        <>
          <div
            className="fixed inset-0 z-40 bg-black/60 backdrop-blur-[2px]"
            onClick={() => setMobileSidebarOpen(false)}
          />
          <div className="fixed inset-y-0 left-0 z-50 flex shadow-2xl">
            <AppSidebar onNavClick={() => setMobileSidebarOpen(false)} />
          </div>
        </>
      )}

      {/* Main area */}
      <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
        <AppTopbar onMobileMenuToggle={() => setMobileSidebarOpen((v) => !v)} />
        <main className="flex-1 overflow-y-auto p-6 animate-fade-in" style={{ scrollbarWidth: "thin" }}>
          {children}
        </main>
      </div>
    </div>
  );
}
