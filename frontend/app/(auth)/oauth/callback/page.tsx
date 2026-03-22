"use client";

import { Suspense, useEffect, useRef } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";
import { useAuthStore } from "@/store/auth-store";

function OAuthCallbackHandler() {
  const router = useRouter();
  const params = useSearchParams();
  const { setUser, setToken } = useAuthStore();
  const hasRun = useRef(false);

  useEffect(() => {
    if (hasRun.current) return;
    hasRun.current = true;
    const accessToken  = params.get("accessToken");
    const refreshToken = params.get("refreshToken");
    const userJson     = params.get("user");
    const error        = params.get("error");

    if (error) {
      toast.error("Sign in failed", { description: decodeURIComponent(error) });
      router.replace("/login");
      return;
    }

    if (!accessToken || !userJson) {
      toast.error("Sign in failed", { description: "Invalid OAuth response." });
      router.replace("/login");
      return;
    }

    try {
      const user = JSON.parse(decodeURIComponent(userJson));
      setToken(accessToken, refreshToken ?? undefined);
      setUser(user);
      toast.success(`Welcome, ${user.name}!`);
      router.replace("/dashboard");
    } catch {
      toast.error("Sign in failed", { description: "Could not parse authorization response." });
      router.replace("/login");
    }
  }, []);

  return (
    <div className="min-h-screen bg-background flex items-center justify-center">
      <div className="flex flex-col items-center gap-4">
        <Loader2 className="w-8 h-8 animate-spin text-primary" />
        <p className="text-muted-foreground">Completing sign in...</p>
      </div>
    </div>
  );
}

export default function OAuthCallbackPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen bg-background flex items-center justify-center">
          <Loader2 className="w-8 h-8 animate-spin text-primary" />
        </div>
      }
    >
      <OAuthCallbackHandler />
    </Suspense>
  );
}
