"use client";

import { useState } from "react";
import { Shield, Loader2, CheckCircle2, Copy, Smartphone } from "lucide-react";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { apiClient } from "@/lib/api-client";
import { useAuthStore } from "@/store/auth-store";
import { toast } from "sonner";

interface TwoFaDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** If true, show disable flow instead of enable flow */
  isEnabled?: boolean;
}

interface SetupData {
  secret: string;
  qrCodeUri: string;
  email: string;
}

export function TwoFaDialog({ open, onOpenChange, isEnabled }: TwoFaDialogProps) {
  const { user, setUser } = useAuthStore();
  const [step, setStep] = useState<"setup" | "verify" | "done">("setup");
  const [loading, setLoading]       = useState(false);
  const [setupData, setSetupData]   = useState<SetupData | null>(null);
  const [code, setCode]             = useState("");
  const [codeError, setCodeError]   = useState("");

  const qrUrl = setupData
    ? `https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(setupData.qrCodeUri)}`
    : null;

  const handleOpen = async (newOpen: boolean) => {
    if (!newOpen) {
      // Reset on close
      setStep("setup");
      setSetupData(null);
      setCode("");
      setCodeError("");
      setLoading(false);
      onOpenChange(false);
      return;
    }
    onOpenChange(true);

    if (!isEnabled) {
      // Fetch setup data
      setLoading(true);
      try {
        const data = await apiClient.get<SetupData>("/auth/2fa/setup");
        setSetupData(data);
      } catch (e: any) {
        toast.error("Failed to start 2FA setup", { description: e.message });
        onOpenChange(false);
      } finally {
        setLoading(false);
      }
    }
  };

  const handleEnable = async () => {
    const trimmed = code.replace(/\s/g, "");
    if (trimmed.length !== 6 || !/^\d{6}$/.test(trimmed)) {
      setCodeError("Enter the 6-digit code from your authenticator app.");
      return;
    }
    setLoading(true);
    setCodeError("");
    try {
      await apiClient.post("/auth/2fa/enable", { code: trimmed });
      setUser({ ...user!, twoFactorEnabled: true });
      setStep("done");
    } catch (e: any) {
      setCodeError(e.message ?? "Invalid code. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  const handleDisable = async () => {
    setLoading(true);
    try {
      await apiClient.post("/auth/2fa/disable", {});
      setUser({ ...user!, twoFactorEnabled: false });
      toast.success("Two-factor authentication disabled.");
      onOpenChange(false);
    } catch (e: any) {
      toast.error("Failed to disable 2FA", { description: e.message });
    } finally {
      setLoading(false);
    }
  };

  // Open was triggered — fetch setup data on mount if enabling
  const handleDialogOpenChange = (open: boolean) => {
    if (open && !isEnabled && !setupData && !loading) {
      handleOpen(true);
    } else if (!open) {
      handleOpen(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleDialogOpenChange}>
      <DialogContent className="sm:max-w-md">
        {isEnabled ? (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <Shield className="h-5 w-5 text-destructive" />
                Disable Two-Factor Authentication
              </DialogTitle>
              <DialogDescription>
                Disabling 2FA will make your account less secure. You can re-enable it at any time.
              </DialogDescription>
            </DialogHeader>
            <div className="py-4">
              <div className="rounded-lg bg-destructive/10 border border-destructive/20 p-4 text-sm text-destructive">
                Warning: Without 2FA, your account relies solely on your password for security.
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
              <Button variant="destructive" onClick={handleDisable} disabled={loading}>
                {loading && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
                Disable 2FA
              </Button>
            </DialogFooter>
          </>
        ) : step === "done" ? (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <CheckCircle2 className="h-5 w-5 text-emerald-500" />
                2FA Enabled Successfully
              </DialogTitle>
              <DialogDescription>
                Your account is now protected with two-factor authentication.
              </DialogDescription>
            </DialogHeader>
            <div className="py-4 text-sm text-muted-foreground space-y-2">
              <p>From now on, you'll be asked for a 6-digit code from your authenticator app each time you sign in.</p>
            </div>
            <DialogFooter>
              <Button onClick={() => { onOpenChange(false); }}>Done</Button>
            </DialogFooter>
          </>
        ) : step === "setup" ? (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <Smartphone className="h-5 w-5" />
                Set Up Authenticator App
              </DialogTitle>
              <DialogDescription>
                Use Google Authenticator, Authy, or any TOTP-compatible app.
              </DialogDescription>
            </DialogHeader>

            {loading ? (
              <div className="py-8 flex justify-center">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
              </div>
            ) : setupData ? (
              <div className="space-y-4 py-2">
                <div className="space-y-2">
                  <p className="text-sm font-medium">Step 1 — Scan this QR code</p>
                  <div className="flex justify-center">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                      src={qrUrl!}
                      alt="2FA QR Code"
                      width={180}
                      height={180}
                      className="rounded-lg border"
                    />
                  </div>
                </div>
                <div className="space-y-1.5">
                  <p className="text-sm font-medium">Or enter the key manually:</p>
                  <div className="flex items-center gap-2 rounded-md border bg-muted/50 px-3 py-2">
                    <code className="flex-1 text-xs font-mono tracking-widest">{setupData.secret}</code>
                    <Button
                      variant="ghost" size="sm" className="h-7 w-7 p-0 shrink-0"
                      onClick={() => {
                        navigator.clipboard.writeText(setupData.secret.replace(/\s/g, ""));
                        toast.success("Key copied!");
                      }}
                    >
                      <Copy className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                </div>
              </div>
            ) : null}

            <DialogFooter>
              <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
              <Button
                onClick={() => setStep("verify")}
                disabled={loading || !setupData}
              >
                Next: Verify Code
              </Button>
            </DialogFooter>
          </>
        ) : (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <Shield className="h-5 w-5 text-primary" />
                Verify Authenticator Code
              </DialogTitle>
              <DialogDescription>
                Enter the 6-digit code currently shown in your authenticator app.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-3 py-2">
              <div className="space-y-1.5">
                <Label htmlFor="totp-code">Verification Code</Label>
                <Input
                  id="totp-code"
                  placeholder="000 000"
                  maxLength={7}
                  value={code}
                  onChange={(e) => {
                    setCode(e.target.value.replace(/[^0-9 ]/g, ""));
                    setCodeError("");
                  }}
                  onKeyDown={(e) => e.key === "Enter" && handleEnable()}
                  className={`text-center tracking-widest text-lg ${codeError ? "border-destructive" : ""}`}
                />
                {codeError && <p className="text-xs text-destructive">{codeError}</p>}
              </div>
              <p className="text-xs text-muted-foreground">
                Codes refresh every 30 seconds. Make sure your device clock is accurate.
              </p>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setStep("setup")}>Back</Button>
              <Button onClick={handleEnable} disabled={loading}>
                {loading && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
                Enable 2FA
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
