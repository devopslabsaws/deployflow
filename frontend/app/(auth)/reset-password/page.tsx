"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { motion } from "framer-motion";
import { ArrowLeft, KeyRound, Loader2, CheckCircle2, Rocket } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { apiClient } from "@/lib/api-client";
import { toast } from "sonner";

function ResetPasswordForm() {
  const router        = useRouter();
  const params        = useSearchParams();
  const email         = params.get("email") ?? "";
  const token         = params.get("token") ?? "";

  const [newPw, setNewPw]       = useState("");
  const [confirmPw, setConfirmPw] = useState("");
  const [loading, setLoading]   = useState(false);
  const [done, setDone]         = useState(false);

  const isValid  = newPw.length >= 8 && /[A-Z]/.test(newPw) && /[0-9]/.test(newPw);
  const mismatch = confirmPw.length > 0 && newPw !== confirmPw;

  if (!email || !token) {
    return (
      <div className="text-center space-y-4">
        <p className="text-sm text-muted-foreground">
          Invalid or expired password reset link. Please request a new one.
        </p>
        <Button asChild className="w-full">
          <Link href="/forgot-password">Request new link</Link>
        </Button>
      </div>
    );
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid) {
      toast.error("Password must be at least 8 characters with one uppercase letter and one number.");
      return;
    }
    if (newPw !== confirmPw) {
      toast.error("Passwords do not match.");
      return;
    }
    setLoading(true);
    try {
      await apiClient.post("/auth/reset-password", { email, token, newPassword: newPw });
      setDone(true);
    } catch (err: any) {
      toast.error("Password reset failed", { description: err?.message });
    } finally {
      setLoading(false);
    }
  };

  return done ? (
    <div className="text-center space-y-4">
      <div className="w-14 h-14 rounded-2xl bg-success/10 flex items-center justify-center mx-auto">
        <CheckCircle2 className="w-7 h-7 text-success" />
      </div>
      <h2 className="text-xl font-semibold">Password updated!</h2>
      <p className="text-sm text-muted-foreground">
        Your password has been reset successfully. You can now sign in with your new password.
      </p>
      <Button className="w-full" onClick={() => router.replace("/login")}>
        Sign in now
      </Button>
    </div>
  ) : (
    <>
      <div className="w-14 h-14 rounded-2xl bg-muted flex items-center justify-center mx-auto mb-4">
        <KeyRound className="w-7 h-7 text-muted-foreground" />
      </div>
      <h2 className="text-xl font-semibold text-center mb-1">Set new password</h2>
      <p className="text-sm text-muted-foreground text-center mb-6">
        For <strong>{email}</strong>
      </p>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="space-y-2">
          <Label htmlFor="newPw">New Password</Label>
          <Input
            id="newPw"
            type="password"
            placeholder="••••••••"
            autoComplete="new-password"
            value={newPw}
            onChange={(e) => setNewPw(e.target.value)}
            className={newPw.length > 0 && !isValid ? "border-destructive" : ""}
            required
          />
          <p className="text-xs text-muted-foreground">
            At least 8 characters, one uppercase letter, one number
          </p>
        </div>
        <div className="space-y-2">
          <Label htmlFor="confirmPw">Confirm Password</Label>
          <Input
            id="confirmPw"
            type="password"
            placeholder="••••••••"
            autoComplete="new-password"
            value={confirmPw}
            onChange={(e) => setConfirmPw(e.target.value)}
            className={mismatch ? "border-destructive" : ""}
            required
          />
          {mismatch && <p className="text-xs text-destructive">Passwords do not match</p>}
        </div>
        <Button type="submit" className="w-full" disabled={loading || !isValid || mismatch}>
          {loading ? (
            <><Loader2 className="mr-2 h-4 w-4 animate-spin" />Updating...</>
          ) : (
            "Reset Password"
          )}
        </Button>
      </form>

      <div className="mt-6 text-center">
        <Link
          href="/login"
          className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors"
        >
          <ArrowLeft className="w-3.5 h-3.5" />
          Back to Sign In
        </Link>
      </div>
    </>
  );
}

export default function ResetPasswordPage() {
  return (
    <div className="min-h-screen bg-background flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-primary/20 via-background to-background pointer-events-none" />

      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4 }}
        className="w-full max-w-md relative z-10"
      >
        <div className="text-center mb-8">
          <div className="inline-flex items-center gap-2 mb-4">
            <div className="w-10 h-10 rounded-xl bg-primary flex items-center justify-center">
              <Rocket className="w-5 h-5 text-primary-foreground" />
            </div>
            <span className="text-2xl font-bold">DeployFlow</span>
          </div>
        </div>

        <div className="bg-card border border-border rounded-xl p-8 shadow-2xl">
          <Suspense fallback={<div className="flex justify-center py-8"><Loader2 className="w-6 h-6 animate-spin text-primary" /></div>}>
            <ResetPasswordForm />
          </Suspense>
        </div>
      </motion.div>
    </div>
  );
}
