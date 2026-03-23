"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { motion } from "framer-motion";
import { Loader2, Mail, ShieldCheck, UserPlus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useAcceptInvitation, useInvitationPreview } from "@/hooks/use-api";
import { useAuthStore } from "@/store/auth-store";
import { toast } from "sonner";

export default function AcceptInvitationPage() {
  const params = useSearchParams();
  const router = useRouter();
  const token = params.get("token") ?? "";
  const { setToken, setUser } = useAuthStore();

  const { data: invitation, isLoading, isError } = useInvitationPreview(token || undefined);
  const acceptInvitation = useAcceptInvitation();

  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  useEffect(() => {
    if (invitation?.name) setName(invitation.name);
  }, [invitation]);

  const onAccept = async () => {
    if (!token) {
      toast.error("Missing invitation token.");
      return;
    }
    if (!name.trim()) {
      toast.error("Name is required.");
      return;
    }
    if (password.length < 8) {
      toast.error("Password must be at least 8 characters.");
      return;
    }
    if (password !== confirmPassword) {
      toast.error("Passwords do not match.");
      return;
    }

    try {
      const response = await acceptInvitation.mutateAsync({
        token,
        name: name.trim(),
        password,
      });

      setToken(response.accessToken, response.refreshToken);
      if (response.user) {
        setUser(response.user);
      }

      toast.success("Invitation accepted.");
      router.push("/dashboard");
    } catch (e: any) {
      toast.error("Failed to accept invitation", {
        description: e?.message || "Please verify your invitation link and try again.",
      });
    }
  };

  return (
    <div className="min-h-screen bg-background flex items-center justify-center p-4">
      <motion.div
        initial={{ opacity: 0, y: 10 }}
        animate={{ opacity: 1, y: 0 }}
        className="w-full max-w-md"
      >
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <UserPlus className="h-5 w-5" />Accept Team Invitation
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {!token && (
              <p className="text-sm text-destructive">This invitation link is missing a token.</p>
            )}

            {isLoading && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />Loading invitation...
              </div>
            )}

            {!isLoading && isError && (
              <p className="text-sm text-destructive">Invitation not found or expired.</p>
            )}

            {!isLoading && invitation && (
              <>
                <div className="rounded-md border border-border/60 p-3 space-y-1.5 text-sm">
                  <div className="flex items-center gap-2 text-muted-foreground">
                    <Mail className="h-4 w-4" />
                    <span>{invitation.email}</span>
                  </div>
                  <div className="flex items-center gap-2 text-muted-foreground">
                    <ShieldCheck className="h-4 w-4" />
                    <span>Role: {invitation.role}</span>
                  </div>
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="name">Full Name</Label>
                  <Input id="name" value={name} onChange={(e) => setName(e.target.value)} />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="password">Password</Label>
                  <Input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="confirmPassword">Confirm Password</Label>
                  <Input id="confirmPassword" type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} />
                </div>

                <Button className="w-full" onClick={onAccept} disabled={acceptInvitation.isPending || !token}>
                  {acceptInvitation.isPending ? "Accepting..." : "Accept Invitation"}
                </Button>
              </>
            )}

            <p className="text-xs text-muted-foreground text-center">
              Already have an account? <Link href="/login" className="text-primary hover:underline">Sign in</Link>
            </p>
          </CardContent>
        </Card>
      </motion.div>
    </div>
  );
}
