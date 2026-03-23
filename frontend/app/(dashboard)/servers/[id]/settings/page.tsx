"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import {
  ArrowLeft, Server, Loader2, Trash2, RefreshCw, Fingerprint,
  CloudCog, Zap, AlertTriangle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Switch } from "@/components/ui/switch";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useServer, useSshKeys } from "@/hooks/use-api";
import { apiClient } from "@/lib/api-client";
import { toast } from "sonner";

export default function ServerSettingsPage({ params }: { params: { id: string } }) {
  const { id } = params;
  const router = useRouter();
  const { data: server, isLoading } = useServer(id);
  const { data: sshKeys } = useSshKeys();

  const [name, setName]           = useState("");
  const [sshUser, setSshUser]     = useState("");
  const [sshPort, setSshPort]     = useState("22");
  const [sshKeyId, setSshKeyId]   = useState("");
  const [region, setRegion]       = useState("");
  const [cpuCores, setCpuCores]   = useState("");
  const [memoryGb, setMemoryGb]   = useState("");
  const [diskGb, setDiskGb]       = useState("");
  const [saving, setSaving]       = useState(false);
  const [removing, setRemoving]   = useState(false);
  const [maintenanceMode, setMaintenanceMode] = useState(false);
  // Docker Cleanup state
  const [cleanupFreq, setCleanupFreq]         = useState("0 0 * * *");
  const [cleanupForce, setCleanupForce]       = useState(true);
  const [deleteVolumes, setDeleteVolumes]     = useState(false);
  const [deleteNetworks, setDeleteNetworks]   = useState(false);
  const [disableRetention, setDisableRetention] = useState(false);
  const [savingCleanup, setSavingCleanup]     = useState(false);
  const [runningCleanup, setRunningCleanup]   = useState(false);

  // Cloudflare Tunnel state
  const [cfToken, setCfToken]         = useState("");
  const [cfSshDomain, setCfSshDomain] = useState("");
  const [cfManual, setCfManual]       = useState(false);
  const [savingCf, setSavingCf]       = useState(false);
  useEffect(() => {
    if (server) {
      setName(server.name ?? "");
      setSshUser((server as any).sshUser ?? "root");
      setSshPort(String(server.port ?? (server as any).sshPort ?? 22));
      setSshKeyId((server as any).sshKeyId ?? "");
      setRegion(server.region ?? "");
      setCpuCores(String(server.cpu ?? 0));
      setMemoryGb(String(server.memoryGB ?? 0));
      setDiskGb(String(server.diskGB ?? 0));
      setMaintenanceMode(server.status === "maintenance");
    }

    // Load docker cleanup + cloudflare tunnel settings
    if (id) {
      apiClient.get(`/servers/${id}/docker-cleanup`).then((d: any) => {
        setCleanupFreq(d.dockerCleanupFrequency ?? "0 0 * * *");
        setCleanupForce(d.dockerCleanupForce ?? true);
        setDeleteVolumes(d.deleteUnusedVolumes ?? false);
        setDeleteNetworks(d.deleteUnusedNetworks ?? false);
        setDisableRetention(d.disableAppImageRetention ?? false);
      }).catch(() => {});

      apiClient.get(`/servers/${id}/cloudflare-tunnel`).then((d: any) => {
        setCfSshDomain(d.cloudflareSshDomain ?? "");
        setCfManual(d.cloudflareTunnelManual ?? false);
      }).catch(() => {});
    }
  }, [server, id]);

  const handleSave = async () => {
    setSaving(true);
    try {
      await apiClient.put(`/servers/${id}`, {
        name,
        sshUser,
        sshPort: parseInt(sshPort),
        sshKeyId: sshKeyId || null,
        region,
        cpuCores: parseInt(cpuCores),
        memoryGb: parseInt(memoryGb),
        diskGb: parseInt(diskGb),
      });
      toast.success("Server settings saved!");
    } catch (e: any) {
      toast.error("Failed to save settings", { description: e.message });
    } finally {
      setSaving(false);
    }
  };

  const handleSaveDockerCleanup = async () => {
    setSavingCleanup(true);
    try {
      await apiClient.put(`/servers/${id}/docker-cleanup`, {
        frequency: cleanupFreq,
        force: cleanupForce,
        deleteUnusedVolumes: deleteVolumes,
        deleteUnusedNetworks: deleteNetworks,
        disableAppImageRetention: disableRetention,
      });
      toast.success("Docker cleanup settings saved!");
    } catch (e: any) {
      toast.error("Failed to save cleanup settings", { description: e.message });
    } finally {
      setSavingCleanup(false);
    }
  };

  const handleTriggerCleanup = async () => {
    setRunningCleanup(true);
    try {
      const result: any = await apiClient.post(`/servers/${id}/docker-cleanup/run`, {});
      toast.success("Cleanup complete.", { description: result.output?.slice(0, 200) });
    } catch (e: any) {
      toast.error("Cleanup failed", { description: e.message });
    } finally {
      setRunningCleanup(false);
    }
  };

  const handleSaveCloudflareTunnel = async () => {
    setSavingCf(true);
    try {
      await apiClient.put(`/servers/${id}/cloudflare-tunnel`, {
        token: cfToken || null,
        sshDomain: cfSshDomain || null,
        manual: cfManual,
      });
      toast.success("Cloudflare Tunnel settings saved!");
      setCfToken(""); // clear token from state after save
    } catch (e: any) {
      toast.error("Failed to save Cloudflare Tunnel settings", { description: e.message });
    } finally {
      setSavingCf(false);
    }
  };

  const handleRemove = async () => {
    if (!confirm(`Remove server "${server?.name}"? This cannot be undone. Active deployments will be affected.`)) return;
    setRemoving(true);
    try {
      await apiClient.delete(`/servers/${id}`);
      toast.success("Server removed.");
      router.push("/servers");
    } catch (e: any) {
      toast.error("Failed to remove server", { description: e.message });
      setRemoving(false);
    }
  };

  const handleRecheck = async () => {
    try {
      await apiClient.post(`/servers/${id}/health-check`, {});
      toast.success("Health check triggered.");
    } catch (e: any) {
      toast.error("Health check failed", { description: e.message });
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-4 max-w-2xl">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-2xl">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Link href={`/servers/${id}`}>
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0"><ArrowLeft className="w-4 h-4" /></Button>
        </Link>
        <Server className="w-5 h-5 text-muted-foreground" />
        <div>
          <h1 className="text-xl font-bold">Server Settings</h1>
          <p className="text-sm text-muted-foreground">{server?.name} — {server?.ipAddress}</p>
        </div>
      </div>

      <Tabs defaultValue="general">
        <TabsList>
          <TabsTrigger value="general">General</TabsTrigger>
          <TabsTrigger value="ssh">SSH</TabsTrigger>
          <TabsTrigger value="docker-cleanup">Docker Cleanup</TabsTrigger>
          <TabsTrigger value="cloudflare-tunnel">Cloudflare Tunnel</TabsTrigger>
          <TabsTrigger value="danger">Danger Zone</TabsTrigger>
        </TabsList>

        {/* ── General ── */}
        <TabsContent value="general" className="space-y-4 mt-4">
          <Card className="glass-card">
            <CardHeader>
              <CardTitle className="text-sm">General Settings</CardTitle>
              <CardDescription>Basic server configuration</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="s-name" className="text-xs">Display Name</Label>
                <Input id="s-name" value={name} onChange={e => setName(e.target.value)} placeholder="My Server" />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="s-region" className="text-xs">Region / Location</Label>
                <Input id="s-region" value={region} onChange={e => setRegion(e.target.value)} placeholder="us-east-1" />
              </div>

              <Separator />
              <p className="text-xs font-medium text-muted-foreground">Hardware Specs</p>
              <div className="grid grid-cols-3 gap-3">
                <div className="space-y-1.5">
                  <Label htmlFor="s-cpu" className="text-xs">vCPUs</Label>
                  <Input id="s-cpu" type="number" min={1} value={cpuCores} onChange={e => setCpuCores(e.target.value)} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="s-mem" className="text-xs">Memory (GB)</Label>
                  <Input id="s-mem" type="number" min={1} value={memoryGb} onChange={e => setMemoryGb(e.target.value)} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="s-disk" className="text-xs">Disk (GB)</Label>
                  <Input id="s-disk" type="number" min={1} value={diskGb} onChange={e => setDiskGb(e.target.value)} />
                </div>
              </div>

              <Separator />
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium">Maintenance Mode</p>
                  <p className="text-xs text-muted-foreground">Prevent new deployments to this server</p>
                </div>
                <Switch checked={maintenanceMode} onCheckedChange={setMaintenanceMode} />
              </div>
            </CardContent>
          </Card>

          <div className="flex gap-2 justify-end">
            <Button variant="outline" size="sm" onClick={handleRecheck}>
              <RefreshCw className="w-4 h-4 mr-1.5" />Re-check Health
            </Button>
            <Button size="sm" disabled={saving} onClick={handleSave}>
              {saving && <Loader2 className="w-4 h-4 animate-spin mr-2" />}
              Save Changes
            </Button>
          </div>
        </TabsContent>

        {/* ── SSH ── */}
        <TabsContent value="ssh" className="space-y-4 mt-4">
          <Card className="glass-card">
            <CardHeader>
              <CardTitle className="text-sm">SSH Configuration</CardTitle>
              <CardDescription>Update SSH connection details for this server</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label htmlFor="s-ssh-user" className="text-xs">SSH User</Label>
                  <Input id="s-ssh-user" value={sshUser} onChange={e => setSshUser(e.target.value)} placeholder="root" />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="s-ssh-port" className="text-xs">SSH Port</Label>
                  <Input id="s-ssh-port" type="number" value={sshPort} onChange={e => setSshPort(e.target.value)} />
                </div>
              </div>

              <div className="space-y-1.5">
                <Label className="text-xs">SSH Key</Label>
                <Select value={sshKeyId || "__none__"} onValueChange={v => setSshKeyId(v === "__none__" ? "" : v)}>
                  <SelectTrigger className="h-9 text-sm">
                    <SelectValue placeholder="— Select an SSH key —" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">No key assigned</SelectItem>
                    {sshKeys?.map(k => (
                      <SelectItem key={k.id} value={k.id}>
                        <span className="flex items-center gap-2">
                          <Fingerprint className="h-3.5 w-3.5 text-muted-foreground" />
                          {k.name}
                          <span className="text-xs text-muted-foreground font-mono ml-1">{k.fingerprint?.slice(0,12)}…</span>
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {!sshKeys?.length && (
                  <p className="text-xs text-amber-500">
                    No SSH keys found. <Link href="/settings?tab=ssh-keys" className="underline">Add one in Settings → SSH Keys</Link>
                  </p>
                )}
              </div>
              <div className="rounded-lg bg-muted/40 p-3 text-xs text-muted-foreground">
                <p className="font-medium text-foreground mb-1">Connection string</p>
                <code>ssh -p {sshPort} {sshUser}@{server?.ipAddress}</code>
              </div>
            </CardContent>
          </Card>
          <div className="flex justify-end">
            <Button size="sm" disabled={saving} onClick={handleSave}>
              {saving && <Loader2 className="w-4 h-4 animate-spin mr-2" />}
              Save SSH Settings
            </Button>
          </div>
        </TabsContent>

        {/* ── Danger Zone ── */}
        <TabsContent value="danger" className="space-y-4 mt-4">
          <Card className="border-destructive/40 glass-card">
            <CardHeader>
              <CardTitle className="text-sm text-destructive">Danger Zone</CardTitle>
              <CardDescription>Irreversible actions — proceed with caution</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex items-center justify-between p-3 rounded-lg border border-destructive/20">
                <div>
                  <p className="text-sm font-medium">Remove Server</p>
                  <p className="text-xs text-muted-foreground mt-0.5">
                    Permanently remove this server from DeployFlow. Running containers will not be stopped.
                  </p>
                </div>
                <Button
                  variant="destructive"
                  size="sm"
                  disabled={removing}
                  onClick={handleRemove}
                >
                  {removing
                    ? <Loader2 className="w-4 h-4 animate-spin mr-2" />
                    : <Trash2 className="w-4 h-4 mr-1.5" />}
                  Remove
                </Button>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Docker Cleanup ── */}
        <TabsContent value="docker-cleanup" className="space-y-4 mt-4">
          <Card className="glass-card">
            <CardHeader className="flex flex-row items-center justify-between pb-3">
              <div>
                <CardTitle className="text-sm">Docker Cleanup</CardTitle>
                <CardDescription>Configure automated and manual Docker resource cleanup</CardDescription>
              </div>
              <div className="flex gap-2">
                <Button size="sm" variant="outline" disabled={runningCleanup} onClick={handleTriggerCleanup}>
                  {runningCleanup ? <Loader2 className="w-3.5 h-3.5 animate-spin mr-1.5" /> : <Zap className="w-3.5 h-3.5 mr-1.5" />}
                  Trigger Manual Cleanup
                </Button>
                <Button size="sm" disabled={savingCleanup} onClick={handleSaveDockerCleanup}>
                  {savingCleanup && <Loader2 className="w-3.5 h-3.5 animate-spin mr-1.5" />}
                  Save
                </Button>
              </div>
            </CardHeader>
            <CardContent className="space-y-5">
              <div className="space-y-1.5">
                <Label htmlFor="cleanup-freq" className="text-xs font-medium">
                  Docker cleanup frequency <span className="text-muted-foreground">(cron expression)</span>
                </Label>
                <Input
                  id="cleanup-freq"
                  value={cleanupFreq}
                  onChange={e => setCleanupFreq(e.target.value)}
                  placeholder="0 0 * * *"
                  className="font-mono text-sm"
                />
                <p className="text-xs text-muted-foreground">
                  Standard cron format: minute hour day month weekday. Example: <code className="text-xs">0 0 * * *</code> = daily at midnight.
                </p>
              </div>
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium">Force Docker Cleanup</p>
                  <p className="text-xs text-muted-foreground">Run <code>docker system prune --force</code></p>
                </div>
                <Switch checked={cleanupForce} onCheckedChange={setCleanupForce} />
              </div>

              <Separator />
              <div className="rounded-lg border border-amber-500/30 bg-amber-500/5 p-3 space-y-1">
                <div className="flex items-center gap-2 text-amber-500 text-sm font-medium">
                  <AlertTriangle className="w-4 h-4" />
                  Advanced — Caution
                </div>
                <p className="text-xs text-muted-foreground">
                  These options can cause permanent data loss. Only enable if you understand the consequences.
                </p>
              </div>

              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm">Delete Unused Volumes</p>
                    <p className="text-xs text-muted-foreground">Removes Docker volumes not attached to any container</p>
                  </div>
                  <Switch checked={deleteVolumes} onCheckedChange={setDeleteVolumes} />
                </div>
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm">Delete Unused Networks</p>
                    <p className="text-xs text-muted-foreground">Removes Docker networks not used by any container</p>
                  </div>
                  <Switch checked={deleteNetworks} onCheckedChange={setDeleteNetworks} />
                </div>
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm">Disable Application Image Retention</p>
                    <p className="text-xs text-muted-foreground">Remove all old application images after each deployment</p>
                  </div>
                  <Switch checked={disableRetention} onCheckedChange={setDisableRetention} />
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Cloudflare Tunnel ── */}
        <TabsContent value="cloudflare-tunnel" className="space-y-4 mt-4">
          <Card className="glass-card">
            <CardHeader>
              <div className="flex items-center gap-2">
                <CloudCog className="w-4 h-4 text-orange-500" />
                <CardTitle className="text-sm">Cloudflare Tunnel</CardTitle>
              </div>
              <CardDescription>
                Secure your server with Cloudflare Tunnel — expose SSH and HTTPS without opening firewall ports.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-5">
              {/* Automated */}
              <div className="space-y-4">
                <h3 className="text-sm font-semibold">Automated</h3>
                <div className="space-y-1.5">
                  <Label htmlFor="cf-token" className="text-xs font-medium">
                    Cloudflare Token <span className="text-destructive">*</span>
                  </Label>
                  <Input
                    id="cf-token"
                    type="password"
                    value={cfToken}
                    onChange={e => setCfToken(e.target.value)}
                    placeholder="eyJhIjoixxxxxx..."
                    autoComplete="off"
                  />
                  <p className="text-xs text-muted-foreground">
                    Create a tunnel token in your{" "}
                    <a
                      href="https://one.dash.cloudflare.com/"
                      target="_blank"
                      rel="noopener noreferrer"
                      className="underline text-primary"
                    >
                      Cloudflare Zero Trust dashboard
                    </a>.
                  </p>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="cf-domain" className="text-xs font-medium">
                    Configured SSH Domain <span className="text-destructive">*</span>
                  </Label>
                  <Input
                    id="cf-domain"
                    value={cfSshDomain}
                    onChange={e => setCfSshDomain(e.target.value)}
                    placeholder="ssh.yourserver.example.com"
                  />
                </div>
                <Button size="sm" disabled={savingCf || cfManual} onClick={handleSaveCloudflareTunnel}>
                  {savingCf && <Loader2 className="w-3.5 h-3.5 animate-spin mr-1.5" />}
                  Continue
                </Button>
              </div>

              <Separator />

              {/* Manual */}
              <div className="space-y-3">
                <h3 className="text-sm font-semibold">Manual</h3>
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm">I manually configured Cloudflare Tunnel</p>
                    <p className="text-xs text-muted-foreground">
                      Skip automated setup — you've already installed cloudflared on this server.
                    </p>
                  </div>
                  <Switch checked={cfManual} onCheckedChange={v => { setCfManual(v); if (v) handleSaveCloudflareTunnel(); }} />
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
