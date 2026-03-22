"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { ArrowLeft, Server, Loader2, Trash2, RefreshCw, Fingerprint } from "lucide-react";
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
import { useRouter } from "next/navigation";

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
  }, [server]);

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
      </Tabs>
    </div>
  );
}
