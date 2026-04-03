"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  Eye, EyeOff, Github, Loader2, Rocket, Building2, ChevronRight,
  ShieldCheck, Zap, GitMerge, BarChart3, ArrowRight,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { useAuthStore } from "@/store/auth-store";
import { BACKEND_BASE_URL } from "@/lib/api-client";
import { toast } from "sonner";
import { useQueryClient } from "@tanstack/react-query";

const loginSchema = z.object({
  email: z.string().email("Invalid email address"),
  password: z.string().min(8, "Password must be at least 8 characters"),
});

type LoginFormData = z.infer<typeof loginSchema>;

const BACKEND = BACKEND_BASE_URL;

const FEATURES = [
  { icon: Zap, title: "One-click deploys", desc: "Push to git and deploy automatically with zero downtime." },
  { icon: GitMerge, title: "CI/CD pipelines", desc: "Chain builds, tests, and rollouts with visual pipeline editor." },
  { icon: BarChart3, title: "Live monitoring", desc: "Real-time metrics, logs and alerts across your entire fleet." },
  { icon: ShieldCheck, title: "Role-based access", desc: "Granular permissions with SSO, MFA and full audit logs." },
];

export default function LoginPage() {
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [showSso, setShowSso] = useState(false);
  const [workspace, setWorkspace] = useState("");

  // Synchronous auth check: read persisted state before first render so
  // already-authenticated users never see the login UI paint at all.
  const [redirecting] = useState(() => {
    if (typeof window === "undefined") return false;
    try {
      const raw = localStorage.getItem("deployflow-auth");
      return raw ? (JSON.parse(raw)?.state?.isAuthenticated === true) : false;
    } catch { return false; }
  });

  const router = useRouter();
  const queryClient = useQueryClient();
  const { login, isAuthenticated } = useAuthStore();

  // Prefetch the dashboard JS bundle while user fills in the form —
  // reduces the navigation gap after a successful login.
  useEffect(() => {
    router.prefetch("/dashboard");
  }, [router]);

  // Fast-path redirect — fires before Zustand hydration completes
  useEffect(() => {
    if (redirecting) router.replace("/dashboard");
  }, [redirecting, router]);

  // Fallback: handle Zustand store hydrating after first render
  useEffect(() => {
    if (redirecting) return;
    const authPersist = (useAuthStore as any).persist;
    const checkAndRedirect = () => {
      if (isAuthenticated) router.replace("/dashboard");
    };
    if (!authPersist) {
      checkAndRedirect();
      return;
    }
    if (authPersist.hasHydrated()) {
      checkAndRedirect();
    } else {
      const unsub = authPersist.onFinishHydration(checkAndRedirect);
      return unsub;
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated]);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormData>({ resolver: zodResolver(loginSchema) });

  const onSubmit = async (data: LoginFormData) => {
    setIsLoading(true);
    try {
      await login(data.email, data.password);
      toast.success("Welcome back!", { description: "Redirecting to dashboard…" });
      // Kick off a background pre-fetch of dashboard stats so data is ready on arrival.
      void queryClient.prefetchQuery({
        queryKey: ["stats", "dashboard"],
        queryFn: () => import("@/lib/api-client").then(m => m.apiClient.get("/stats/dashboard")),
        staleTime: 60_000,
      });
      router.push("/dashboard");
    } catch (error: any) {
      toast.error("Login failed", {
        description: error?.message || "Invalid credentials. Please try again.",
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleSsoContinue = () => {
    if (!workspace.trim()) return;
    window.location.href = `${BACKEND}/api/auth/sso?workspace=${encodeURIComponent(workspace.trim())}`;
  };

  // Don't paint the login UI if we already know the user is authenticated
  if (redirecting) return null;

  return (
    <div className="min-h-screen flex">
      {/* ── Left branding panel (hidden on mobile) ── */}
      {/* CSS radial-gradient replaces 3x blur-3xl DOM blobs — avoids expensive multi-pass GPU rasterization */}
      <div
        className="hidden lg:flex lg:w-[52%] xl:w-[55%] flex-col justify-between relative overflow-hidden bg-[hsl(231,72%,18%)] p-12 text-white"
        style={{
          backgroundImage: [
            "radial-gradient(ellipse 70% 55% at -5% -5%, hsl(231 72% 40% / 0.28) 0%, transparent 70%)",
            "radial-gradient(ellipse 60% 55% at 105% 105%, hsl(271 72% 40% / 0.28) 0%, transparent 70%)",
            "radial-gradient(ellipse 80% 65% at 50% 48%, hsl(231 72% 30% / 0.12) 0%, transparent 65%)",
          ].join(", "),
        }}
      >

        {/* Logo */}
        <div className="relative z-10 flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/20">
            <Rocket className="h-5 w-5 text-white" />
          </div>
          <span className="text-xl font-bold tracking-tight">DeployFlow</span>
        </div>

        {/* Center content */}
        <div className="relative z-10 space-y-8">
          <div className="space-y-3">
            <h1 className="text-4xl font-extrabold leading-tight tracking-tight">
              Ship faster.<br />Scale smarter.
            </h1>
            <p className="text-base text-white/65 max-w-sm leading-relaxed">
              The self-hosted deployment platform built for teams that need power and simplicity in equal measure.
            </p>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {FEATURES.map(({ icon: Icon, title, desc }) => (
              <div key={title} className="flex gap-3 rounded-xl bg-white/10 p-4 border border-white/10">
                <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-white/12">
                  <Icon className="h-4 w-4 text-white" />
                </div>
                <div>
                  <p className="text-sm font-semibold">{title}</p>
                  <p className="mt-0.5 text-xs text-white/55 leading-relaxed">{desc}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Footer quote */}
        <div className="relative z-10">
          <p className="text-xs text-white/35">Trusted by engineering teams worldwide · Self-hosted · Open source</p>
        </div>
      </div>

      {/* ── Right form panel ── */}
      <div className="flex flex-1 flex-col items-center justify-center bg-background p-6 sm:p-10">
        {/* Mobile logo */}
        <div className="mb-8 flex items-center gap-2 lg:hidden">
          <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-primary">
            <Rocket className="h-4 w-4 text-primary-foreground" />
          </div>
          <span className="text-xl font-bold">DeployFlow</span>
        </div>

        <div className="w-full max-w-[400px] login-form-enter">
          <div className="mb-7">
            <h2 className="text-2xl font-bold tracking-tight">Sign in</h2>
            <p className="mt-1 text-sm text-muted-foreground">Welcome back — enter your credentials to continue.</p>
          </div>

          {/* OAuth / SSO Buttons */}
          <div className="space-y-2 mb-5">
            {/* SSO */}
            <Button
              variant="outline"
              className="w-full gap-2"
              type="button"
              onClick={() => setShowSso(!showSso)}
            >
              <Building2 className="w-4 h-4" />
              Sign in with SSO
              <ChevronRight
                className={`w-3 h-3 ml-auto text-muted-foreground transition-transform duration-200 ${showSso ? "rotate-90" : ""}`}
              />
            </Button>

            {showSso && (
              <div className="overflow-hidden animate-fade-in">
                <div className="flex gap-2 pt-1">
                  <Input
                    placeholder="your-workspace"
                    value={workspace}
                    onChange={(e) => setWorkspace(e.target.value)}
                    onKeyDown={(e) => e.key === "Enter" && handleSsoContinue()}
                    autoFocus
                    className="text-sm"
                  />
                  <Button
                    type="button"
                    size="sm"
                    disabled={!workspace.trim()}
                    onClick={handleSsoContinue}
                  >
                    Continue
                  </Button>
                </div>
                <p className="text-xs text-muted-foreground mt-1 pl-1">
                  Enter your organization workspace slug
                </p>
              </div>
            )}

            {/* GitHub */}
            <Button
              variant="outline"
              className="w-full gap-2"
              type="button"
              onClick={() => { window.location.href = `${BACKEND}/api/auth/github`; }}
            >
              <Github className="w-4 h-4" />
              Continue with GitHub
            </Button>

            {/* GitLab */}
            <Button
              variant="outline"
              className="w-full gap-2"
              type="button"
              onClick={() => { window.location.href = `${BACKEND}/api/auth/gitlab`; }}
            >
              <svg className="w-4 h-4" viewBox="0 0 24 24" fill="currentColor">
                <path d="M4.845.904a.99.99 0 0 0-.943.686L.029 13.71a1 1 0 0 0 .362 1.114l11.557 8.394a.98.98 0 0 0 1.152-.002l11.547-8.392a1 1 0 0 0 .362-1.114L21.1 1.59a.99.99 0 0 0-.943-.686H4.845z" />
              </svg>
              Continue with GitLab
            </Button>
          </div>

          <div className="relative mb-5">
            <Separator />
            <span className="absolute left-1/2 -translate-x-1/2 -translate-y-1/2 bg-background px-3 text-xs text-muted-foreground">
              or continue with email
            </span>
          </div>

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="email">Email address</Label>
              <Input
                id="email"
                type="email"
                placeholder="admin@deployflow.io"
                autoComplete="email"
                {...register("email")}
                className={errors.email ? "border-destructive" : ""}
              />
              {errors.email && <p className="text-xs text-destructive">{errors.email.message}</p>}
            </div>

            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <Label htmlFor="password">Password</Label>
                <Link href="/forgot-password" className="text-xs text-primary hover:underline">
                  Forgot password?
                </Link>
              </div>
              <div className="relative">
                <Input
                  id="password"
                  type={showPassword ? "text" : "password"}
                  placeholder="••••••••"
                  autoComplete="current-password"
                  {...register("password")}
                  className={errors.password ? "border-destructive pr-10" : "pr-10"}
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="absolute right-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                  onClick={() => setShowPassword(!showPassword)}
                >
                  {showPassword ? <EyeOff className="h-4 w-4 text-muted-foreground" /> : <Eye className="h-4 w-4 text-muted-foreground" />}
                </Button>
              </div>
              {errors.password && <p className="text-xs text-destructive">{errors.password.message}</p>}
            </div>

            <Button type="submit" className="w-full gap-2" disabled={isLoading}>
              {isLoading ? (
                <><Loader2 className="h-4 w-4 animate-spin" />Signing in…</>
              ) : (
                <><span>Sign in</span><ArrowRight className="h-4 w-4" /></>
              )}
            </Button>
          </form>

          <p className="mt-6 text-center text-sm text-muted-foreground">
            Don&apos;t have an account?{" "}
            <Link href="/register" className="text-primary hover:underline font-medium">
              Create one free
            </Link>
          </p>

          <p className="mt-4 text-center text-[11px] text-muted-foreground/60">
            By signing in you agree to our{" "}
            <Link href="/terms" className="hover:underline">Terms</Link>{" "}
            &amp;{" "}
            <Link href="/privacy" className="hover:underline">Privacy Policy</Link>.
          </p>
        </div>
      </div>
    </div>
  );
}
