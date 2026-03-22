"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Loader2, Eye, EyeOff, ExternalLink, CheckCircle2 } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useConnectDockerHub, useVerifyDockerHub } from "@/hooks/use-api";
import { toast } from "sonner";

const schema = z.object({
  username: z.string().min(1, "Username is required").max(100),
  accessToken: z.string().min(6, "Access token must be at least 6 characters"),
});

type FormData = z.infer<typeof schema>;

interface DockerHubConnectDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function DockerHubConnectDialog({ open, onOpenChange }: DockerHubConnectDialogProps) {
  const [showToken, setShowToken] = useState(false);
  const [verified, setVerified] = useState(false);
  const connect = useConnectDockerHub();
  const verify = useVerifyDockerHub();

  const {
    register,
    handleSubmit,
    reset,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({ resolver: zodResolver(schema) });

  const handleVerify = async () => {
    const values = getValues();
    if (!values.username || !values.accessToken) {
      toast.error("Fill in username and access token first.");
      return;
    }
    try {
      await verify.mutateAsync({ username: values.username, accessToken: values.accessToken });
      setVerified(true);
      toast.success("Docker Hub credentials verified successfully!");
    } catch (e: any) {
      setVerified(false);
      toast.error("Verification failed", { description: e.message });
    }
  };

  const onSubmit = async (data: FormData) => {
    try {
      await connect.mutateAsync({ username: data.username, accessToken: data.accessToken });
      toast.success("Docker Hub connected successfully!");
      reset();
      setVerified(false);
      onOpenChange(false);
    } catch (e: any) {
      toast.error("Failed to connect Docker Hub", { description: e.message });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Connect Docker Hub</DialogTitle>
          <DialogDescription>
            Enter your Docker Hub username and a Personal Access Token to enable
            pulling private images during deployments.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 py-2">
          <div className="space-y-1.5">
            <Label htmlFor="dh-username" className="text-xs">Docker Hub Username</Label>
            <Input
              id="dh-username"
              placeholder="myusername"
              className="h-8 text-sm"
              autoComplete="username"
              {...register("username")}
            />
            {errors.username && (
              <p className="text-xs text-destructive">{errors.username.message}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <Label htmlFor="dh-token" className="text-xs">Access Token</Label>
              <a
                href="https://app.docker.com/settings/personal-access-tokens"
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center gap-1 text-[11px] text-muted-foreground hover:text-foreground transition-colors"
              >
                Create token <ExternalLink className="h-2.5 w-2.5" />
              </a>
            </div>
            <div className="relative">
              <Input
                id="dh-token"
                type={showToken ? "text" : "password"}
                placeholder="dckr_pat_..."
                className="h-8 text-sm pr-9 font-mono"
                autoComplete="current-password"
                {...register("accessToken")}
              />
              <button
                type="button"
                onClick={() => setShowToken((v) => !v)}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                tabIndex={-1}
              >
                {showToken
                  ? <EyeOff className="h-3.5 w-3.5" />
                  : <Eye className="h-3.5 w-3.5" />}
              </button>
            </div>
            {errors.accessToken && (
              <p className="text-xs text-destructive">{errors.accessToken.message}</p>
            )}
            <p className="text-[11px] text-muted-foreground">
              Use a Personal Access Token (PAT) with <strong>Read</strong> scope, not your password.
            </p>
          </div>

          <DialogFooter className="pt-2">
            <Button type="button" variant="outline" size="sm" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={verify.isPending}
              onClick={handleVerify}
            >
              {verify.isPending
                ? <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" />
                : verified
                  ? <CheckCircle2 className="mr-2 h-3.5 w-3.5 text-emerald-500" />
                  : null}
              {verified ? "Verified ✓" : "Test Connection"}
            </Button>
            <Button type="submit" size="sm" disabled={isSubmitting || connect.isPending}>
              {(isSubmitting || connect.isPending) && (
                <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" />
              )}
              Connect
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
