"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  HardDrive, Plus, Trash2, MoreVertical, RefreshCw, Activity, Link, Link2Off,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useVolumes, useCreateVolume, useDeleteVolume, useAttachVolume, useDetachVolume } from "@/hooks/use-api";
import { toast } from "sonner";
import { ConfirmActionDialog } from "@/components/ui/confirm-action-dialog";

const statusColors = {
  active: "text-success",
  inactive: "text-muted-foreground",
  error: "text-destructive",
};

function formatBytes(bytes: number) {
  if (bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB", "TB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / Math.pow(k, i)).toFixed(1)} ${sizes[i]}`;
}

export default function VolumesPage() {
  const [addOpen, setAddOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [attachTarget, setAttachTarget] = useState<{ id: string; name: string } | null>(null);
  const [attachForm, setAttachForm] = useState({ projectId: "", serviceId: "", mountPath: "" });
  const [form, setForm] = useState({ name: "", mountPath: "", driver: "local" });
  const { data: volumes, isLoading } = useVolumes();
  const createVolume = useCreateVolume();
  const deleteVolume = useDeleteVolume();
  const attachVolume = useAttachVolume();
  const detachVolume = useDetachVolume();

  const handleCreate = async () => {
    if (!form.name) { toast.error("Volume name is required."); return; }
    try {
      await createVolume.mutateAsync({ name: form.name, mountPath: form.mountPath || undefined, driver: form.driver || undefined });
      toast.success(`Volume "${form.name}" created.`);
      setAddOpen(false);
      setForm({ name: "", mountPath: "", driver: "local" });
    } catch (e: any) {
      toast.error("Failed to create volume", { description: e.message });
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteVolume.mutateAsync(deleteTarget.id);
      toast.success("Volume deleted.");
    } catch (e: any) {
      toast.error("Failed to delete volume", { description: e.message });
    }
  };

  const handleAttach = async () => {
    if (!attachTarget) return;
    if (!attachForm.projectId && !attachForm.serviceId) {
      toast.error("Provide a Project ID or Service ID.");
      return;
    }
    try {
      await attachVolume.mutateAsync({
        id: attachTarget.id,
        projectId: attachForm.projectId || undefined,
        serviceId: attachForm.serviceId || undefined,
        mountPath: attachForm.mountPath || undefined,
      });
      toast.success(`Volume "${attachTarget.name}" attached.`);
      setAttachTarget(null);
      setAttachForm({ projectId: "", serviceId: "", mountPath: "" });
    } catch (e: any) {
      toast.error("Failed to attach volume", { description: e.message });
    }
  };

  const handleDetach = async (id: string, name: string) => {
    try {
      await detachVolume.mutateAsync(id);
      toast.success(`Volume "${name}" detached.`);
    } catch (e: any) {
      toast.error("Failed to detach volume", { description: e.message });
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Volumes</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {volumes?.length ?? 0} volume{(volumes?.length ?? 0) !== 1 ? "s" : ""} managed
          </p>
        </div>
        <Button onClick={() => setAddOpen(true)}>
          <Plus className="h-4 w-4 mr-2" />
          Create Volume
        </Button>
      </div>

      <div className="space-y-3">
        {isLoading
          ? Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-20 w-full rounded-lg" />)
          : volumes?.length === 0
          ? (
            <Card>
              <CardContent className="py-16 text-center">
                <HardDrive className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                <p className="text-lg font-medium">No volumes yet</p>
                <p className="text-sm text-muted-foreground mb-4">
                  Persistent volumes store data across container restarts.
                </p>
                <Button onClick={() => setAddOpen(true)}>
                  <Plus className="h-4 w-4 mr-2" />Create Volume
                </Button>
              </CardContent>
            </Card>
          )
          : volumes?.map((volume) => (
            <motion.div key={volume.id} initial={{ opacity: 0, y: 4 }} animate={{ opacity: 1, y: 0 }}>
              <Card>
                <CardContent className="py-4 flex items-center gap-4">
                  <HardDrive className="h-5 w-5 text-muted-foreground flex-shrink-0" />
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-medium font-mono">{volume.name}</span>
                      <Badge variant="outline" className={`text-xs ${statusColors[volume.status as keyof typeof statusColors] ?? ""}`}>
                        <Activity className="h-3 w-3 mr-1" />
                        {volume.status}
                      </Badge>
                      <Badge variant="outline" className="text-xs">{volume.driver}</Badge>
                    </div>
                    <div className="flex items-center gap-4 mt-1">
                      {volume.mountPath && (
                        <p className="text-xs text-muted-foreground font-mono">{volume.mountPath}</p>
                      )}
                      {volume.sizeBytes > 0 && (
                        <p className="text-xs text-muted-foreground">{formatBytes(volume.sizeBytes)}</p>
                      )}
                      {volume.dockerName && (
                        <p className="text-xs text-muted-foreground">
                          <Link className="inline h-3 w-3 mr-0.5" />
                          {volume.dockerName}
                        </p>
                      )}
                    </div>
                  </div>
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button variant="ghost" size="icon" className="h-8 w-8">
                        <MoreVertical className="h-4 w-4" />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuItem onClick={() => { setAttachTarget({ id: volume.id, name: volume.name }); setAttachForm({ projectId: "", serviceId: "", mountPath: volume.mountPath ?? "" }); }}>
                        <Link className="h-4 w-4 mr-2" />Attach
                      </DropdownMenuItem>
                      {volume.dockerName && (
                        <DropdownMenuItem onClick={() => handleDetach(volume.id, volume.name)} disabled={detachVolume.isPending}>
                          <Link2Off className="h-4 w-4 mr-2" />Detach
                        </DropdownMenuItem>
                      )}
                      <DropdownMenuSeparator />
                      <DropdownMenuItem
                        className="text-destructive"
                        onClick={() => setDeleteTarget({ id: volume.id, name: volume.name })}
                      >
                        <Trash2 className="h-4 w-4 mr-2" />Delete
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </CardContent>
              </Card>
            </motion.div>
          ))}
      </div>

      {/* Create Dialog */}
      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>Create Volume</DialogTitle></DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Volume Name</Label>
              <Input
                placeholder="my-app-data"
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Mount Path (optional)</Label>
              <Input
                placeholder="/var/data"
                value={form.mountPath}
                onChange={(e) => setForm((f) => ({ ...f, mountPath: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddOpen(false)}>Cancel</Button>
            <Button onClick={handleCreate} disabled={createVolume.isPending}>
              {createVolume.isPending ? "Creating..." : "Create Volume"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Attach Dialog */}
      <Dialog open={!!attachTarget} onOpenChange={(open) => { if (!open) setAttachTarget(null); }}>
        <DialogContent>
          <DialogHeader><DialogTitle>Attach Volume — {attachTarget?.name}</DialogTitle></DialogHeader>
          <div className="space-y-4 py-2">
            <p className="text-sm text-muted-foreground">Specify exactly one attachment target (project or service).</p>
            <div className="space-y-1.5">
              <Label>Project ID</Label>
              <Input
                placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                value={attachForm.projectId}
                onChange={(e) => setAttachForm((f) => ({ ...f, projectId: e.target.value, serviceId: "" }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label>— or — Service ID</Label>
              <Input
                placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                value={attachForm.serviceId}
                onChange={(e) => setAttachForm((f) => ({ ...f, serviceId: e.target.value, projectId: "" }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Mount Path (optional)</Label>
              <Input
                placeholder="/var/data"
                value={attachForm.mountPath}
                onChange={(e) => setAttachForm((f) => ({ ...f, mountPath: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAttachTarget(null)}>Cancel</Button>
            <Button onClick={handleAttach} disabled={attachVolume.isPending}>
              {attachVolume.isPending ? "Attaching..." : "Attach Volume"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmActionDialog
        open={!!deleteTarget}
        onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}
        title="Delete Volume"
        description={deleteTarget
          ? `Delete volume \"${deleteTarget.name}\"? All stored data will be lost.`
          : "Delete this volume?"}
        confirmLabel="Delete Volume"
        isConfirming={deleteVolume.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}

