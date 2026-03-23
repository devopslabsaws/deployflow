"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Globe, Plus, CheckCircle, AlertCircle, Clock, Lock, Trash2, MoreVertical, RefreshCw,
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
import { Switch } from "@/components/ui/switch";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useDomains, useAddDomain, useDeleteDomain } from "@/hooks/use-api";
import { toast } from "sonner";
import { formatDistanceToNow } from "date-fns";
import { ConfirmActionDialog } from "@/components/ui/confirm-action-dialog";

const statusConfig = {
  active: { icon: CheckCircle, color: "text-success", label: "Active" },
  pending: { icon: Clock, color: "text-warning", label: "Pending Verification" },
  error: { icon: AlertCircle, color: "text-destructive", label: "Error" },
  expired: { icon: AlertCircle, color: "text-destructive", label: "SSL Expired" },
};

export default function DomainsPage() {
  const [addOpen, setAddOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [form, setForm] = useState({ domainName: "", sslEnabled: true });
  const { data: domains, isLoading } = useDomains();
  const addDomain = useAddDomain();
  const deleteDomain = useDeleteDomain();

  const handleAdd = async () => {
    if (!form.domainName) { toast.error("Domain name is required."); return; }
    try {
      await addDomain.mutateAsync(form);
      toast.success(`Domain ${form.domainName} added.`);
      setAddOpen(false);
      setForm({ domainName: "", sslEnabled: true });
    } catch (e: any) {
      toast.error("Failed to add domain", { description: e.message });
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteDomain.mutateAsync(deleteTarget.id);
      toast.success("Domain removed.");
    } catch (e: any) {
      toast.error("Failed to remove domain", { description: e.message });
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Domains</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {domains?.length ?? 0} domain{(domains?.length ?? 0) !== 1 ? "s" : ""} configured
          </p>
        </div>
        <Button onClick={() => setAddOpen(true)}>
          <Plus className="h-4 w-4 mr-2" />
          Add Domain
        </Button>
      </div>

      <div className="space-y-3">
        {isLoading
          ? Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-20 w-full rounded-lg" />)
          : domains?.length === 0
          ? (
            <Card>
              <CardContent className="py-16 text-center">
                <Globe className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                <p className="text-lg font-medium">No domains configured</p>
                <p className="text-sm text-muted-foreground mb-4">
                  Add a custom domain to serve your apps on your own hostname.
                </p>
                <Button onClick={() => setAddOpen(true)}>
                  <Plus className="h-4 w-4 mr-2" />Add Domain
                </Button>
              </CardContent>
            </Card>
          )
          : domains?.map((domain) => {
            const statusStr = domain.dnsVerified ? "active" : "pending";
            const status = statusConfig[statusStr as keyof typeof statusConfig] ?? statusConfig.pending;
            const StatusIcon = status.icon;
            return (
              <motion.div key={domain.id} initial={{ opacity: 0, y: 4 }} animate={{ opacity: 1, y: 0 }}>
                <Card>
                  <CardContent className="py-4 flex items-center gap-4">
                    <Globe className="h-5 w-5 text-muted-foreground flex-shrink-0" />
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="font-medium font-mono">{domain.name}</span>
                        <StatusIcon className={`h-4 w-4 ${status.color}`} />
                        <span className={`text-xs ${status.color}`}>{status.label}</span>
                        {domain.sslEnabled && (
                          <Badge variant="outline" className="text-xs">
                            <Lock className="h-3 w-3 mr-1" />SSL
                          </Badge>
                        )}
                      </div>
                      {domain.sslExpiresAt && (
                        <p className="text-xs text-muted-foreground mt-0.5">
                          SSL expires {formatDistanceToNow(new Date(domain.sslExpiresAt), { addSuffix: true })}
                        </p>
                      )}
                    </div>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" className="h-8 w-8">
                          <MoreVertical className="h-4 w-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem>
                          <RefreshCw className="h-4 w-4 mr-2" />Re-verify DNS
                        </DropdownMenuItem>
                        <DropdownMenuItem
                          className="text-destructive"
                          onClick={() => setDeleteTarget({ id: domain.id, name: domain.name })}
                        >
                          <Trash2 className="h-4 w-4 mr-2" />Remove
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </CardContent>
                </Card>
              </motion.div>
            );
          })}
      </div>

      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>Add Custom Domain</DialogTitle></DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Domain Name</Label>
              <Input
                placeholder="app.example.com"
                value={form.domainName}
                onChange={(e) => setForm((f) => ({ ...f, domainName: e.target.value }))}
              />
            </div>
            <div className="flex items-center justify-between">
              <div>
                <Label>Enable SSL (Let&apos;s Encrypt)</Label>
                <p className="text-xs text-muted-foreground">Automatic HTTPS certificate</p>
              </div>
              <Switch
                checked={form.sslEnabled}
                onCheckedChange={(v) => setForm((f) => ({ ...f, sslEnabled: v }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddOpen(false)}>Cancel</Button>
            <Button onClick={handleAdd} disabled={addDomain.isPending}>
              {addDomain.isPending ? "Adding..." : "Add Domain"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmActionDialog
        open={!!deleteTarget}
        onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}
        title="Remove Domain"
        description={deleteTarget
          ? `Remove domain \"${deleteTarget.name}\" from this project?`
          : "Remove this domain?"}
        confirmLabel="Remove Domain"
        isConfirming={deleteDomain.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}
