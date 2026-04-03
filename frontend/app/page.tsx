"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";
import { useAuthStore } from "@/store/auth-store";

export default function HomePage() {
  const router = useRouter();
  const { isAuthenticated } = useAuthStore();
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    const authPersist = (useAuthStore as any).persist;
    if (!authPersist) {
      setHydrated(true);
      return;
    }
    if (authPersist.hasHydrated()) {
      setHydrated(true);
      return;
    }
    const unsub = authPersist.onFinishHydration(() => setHydrated(true));
    return unsub;
  }, []);

  useEffect(() => {
    if (!hydrated) return;
    router.replace(isAuthenticated ? "/dashboard" : "/login");
  }, [hydrated, isAuthenticated, router]);

  return (
    <div className="min-h-screen bg-background flex items-center justify-center">
      <Loader2 className="w-8 h-8 animate-spin text-primary" />
    </div>
  );
}
