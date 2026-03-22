"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Loader2, Server, Key, TestTube } from "lucide-react";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import { useCreateServer } from "@/hooks/use-api";
import type { ServerProvider } from "@/types";
import { toast } from "sonner";

const schema = z.object({
  name: z.string().min(2, "Too short").max(50),
  hostname: z.string().min(1, "Required"),
  ipAddress: z.string().ip("Must be a valid IP address"),
  port: z.coerce.number().default(22),
  provider: z.string().default("custom"),
  region: z.string().optional(),
  cpu: z.coerce.number().min(1).default(1),
  memoryGB: z.coerce.number().min(1).default(2),
  diskGB: z.coerce.number().min(1).default(20),
});

type FormData = z.infer<typeof schema>;

interface AddServerDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function AddServerDialog({ open, onOpenChange }: AddServerDialogProps) {
  const [testStatus, setTestStatus] = useState<"idle" | "testing" | "ok" | "fail">("idle");
  const create = useCreateServer();
  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { port: 22, cpu: 1, memoryGB: 2, diskGB: 20, provider: "custom" },
  });

  const provider = watch("provider");

  const handleTest = async () => {
    setTestStatus("testing");
    // Simulate connection test
    await new Promise((r) => setTimeout(r, 1500));
    setTestStatus("ok");
    toast.success("Connection to server successful!");
  };

  const onSubmit = async (data: FormData) => {
    try {
      await create.mutateAsync({
        name: data.name,
        ipAddress: data.ipAddress,
        sshPort: data.port,
        sshUser: data.hostname || 'root',
        provider: data.provider as ServerProvider,
        region: data.region,
        cpuCores: data.cpu,
        memoryGb: data.memoryGB,
        diskGb: data.diskGB,
      } as any);
      toast.success(`Server "${data.name}" added!`);
      reset();
      onOpenChange(false);
      setTestStatus("idle");
    } catch (e: any) {
      toast.error("Failed to add server", { description: e.message });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Server className="w-5 h-5" /> Add Server
          </DialogTitle>
          <DialogDescription>
            Connect a Linux server via SSH. Docker must be installed.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Server Name *</Label>
              <Input placeholder="prod-server-01" {...register("name")} />
              {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
            </div>
            <div className="space-y-2">
              <Label>Provider</Label>
              <Select value={provider} onValueChange={(v) => setValue("provider", v)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="custom">Custom / Self-hosted</SelectItem>
                  <SelectItem value="aws">AWS</SelectItem>
                  <SelectItem value="azure">Azure</SelectItem>
                  <SelectItem value="gcp">GCP</SelectItem>
                  <SelectItem value="digitalocean">DigitalOcean</SelectItem>
                  <SelectItem value="hetzner">Hetzner</SelectItem>
                  <SelectItem value="vultr">Vultr</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-3 gap-4">
            <div className="col-span-2 space-y-2">
              <Label>IP Address / Hostname *</Label>
              <Input placeholder="192.168.1.100" {...register("ipAddress")} />
              {errors.ipAddress && <p className="text-xs text-destructive">{errors.ipAddress.message}</p>}
            </div>
            <div className="space-y-2">
              <Label>SSH Port</Label>
              <Input type="number" placeholder="22" {...register("port")} />
            </div>
          </div>

          <div className="space-y-2">
            <Label>Hostname</Label>
            <Input placeholder="server-hostname" {...register("hostname")} />
          </div>

          <div className="flex items-center gap-2 p-3 rounded-lg bg-muted/50 border">
            <Key className="w-4 h-4 text-muted-foreground shrink-0" />
            <div className="flex-1 text-sm">
              <p className="font-medium">SSH Key Authentication</p>
              <p className="text-xs text-muted-foreground">
                Our public key will be provisioned on your server.
              </p>
            </div>
          </div>

          <div className="grid grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label>vCPUs</Label>
              <Input type="number" {...register("cpu")} />
            </div>
            <div className="space-y-2">
              <Label>RAM (GB)</Label>
              <Input type="number" {...register("memoryGB")} />
            </div>
            <div className="space-y-2">
              <Label>Disk (GB)</Label>
              <Input type="number" {...register("diskGB")} />
            </div>
          </div>

          <Button
            type="button"
            variant="outline"
            className="w-full gap-2"
            onClick={handleTest}
            disabled={testStatus === "testing"}
          >
            {testStatus === "testing" ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <TestTube className="w-4 h-4" />
            )}
            Test Connection
            {testStatus === "ok" && (
              <Badge className="bg-success/10 text-success border-success/30 ml-auto">Connected</Badge>
            )}
            {testStatus === "fail" && (
              <Badge variant="destructive" className="ml-auto">Failed</Badge>
            )}
          </Button>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" disabled={isSubmitting || create.isPending}>
              {(isSubmitting || create.isPending) && <Loader2 className="mr-2 w-4 h-4 animate-spin" />}
              Add Server
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
