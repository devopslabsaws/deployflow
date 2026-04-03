"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  KeyRound, Plus, Eye, EyeOff, Trash2, RotateCcw, Shield,
  Lock, Unlock, AlertTriangle, Copy, Check, Clock, Tag,
} from "lucide-react";
import { useAuthStore } from "@/store/auth-store";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { ScrollArea } from "@/components/ui/scroll-area";
import { apiClient } from "@/lib/api-client";
import { cn, formatRelativeTime } from "@/lib/utils";
import { toast } from "sonner";

interface Vault {
  id: string;
  name: string;
  description?: string;
  isLocked: boolean;
  secretCount: number;
  lastAccessedAt?: string;
  createdAt: string;
}

interface Secret {
  id: string;
  key: string;
  description?: string;
  secretType: string;
  rotationPolicy: string;
  expiresAt?: string;
  version: number;
  tags?: string;
  createdAt: string;
  updatedAt: string;
  isExpired: boolean;
}

interface AuditEntry {
  id: string;
  secretId: string;
  action: string;
  ipAddress?: string;
  success: boolean;
  createdAt: string;
}

export default function SecretManagementPage() {
  const qc = useQueryClient();
  const user = useAuthStore(s => s.user);
  const [selectedVaultId, setSelectedVaultId] = useState<string | null>(null);
  const [createVaultOpen, setCreateVaultOpen] = useState(false);
  const [createSecretOpen, setCreateSecretOpen] = useState(false);
  const [revealedValues, setRevealedValues] = useState<Record<string, string>>({});
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const [newVault, setNewVault] = useState({ name: "", description: "" });
  const [newSecret, setNewSecret] = useState({ key: "", value: "", description: "", secretType: "PlainText", rotationPolicy: "Never", expiresAt: "" });
  const [auditOpen, setAuditOpen] = useState(false);

  const { data: vaults, isLoading: vaultsLoading } = useQuery<Vault[]>({
    queryKey: ["secrets", "vaults"],
    queryFn: () => apiClient.get("/secrets/vaults"),
    staleTime: 30_000,
  });

  const { data: secrets, isLoading: secretsLoading } = useQuery<Secret[]>({
    queryKey: ["secrets", "list", selectedVaultId],
    queryFn: () => apiClient.get(`/secrets/vaults/${selectedVaultId}/secrets`),
    enabled: !!selectedVaultId,
    staleTime: 30_000,
  });

  const { data: auditLog } = useQuery<AuditEntry[]>({
    queryKey: ["secrets", "audit"],
    queryFn: () => apiClient.get("/secrets/audit?limit=50"),
    enabled: auditOpen,
    staleTime: 15_000,
  });

  const createVaultMutation = useMutation({
    mutationFn: (d: any) => apiClient.post("/secrets/vaults", d),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["secrets"] }); setCreateVaultOpen(false); setNewVault({ name: "", description: "" }); toast.success("Vault created."); },
    onError: (e: any) => toast.error(e.message),
  });

  const createSecretMutation = useMutation({
    mutationFn: (d: any) => apiClient.post(`/secrets/vaults/${selectedVaultId}/secrets`, d),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["secrets", "list", selectedVaultId] }); setCreateSecretOpen(false); setNewSecret({ key: "", value: "", description: "", secretType: "PlainText", rotationPolicy: "Never", expiresAt: "" }); toast.success("Secret stored (encrypted)."); },
    onError: (e: any) => toast.error(e.message),
  });

  const deleteSecretMutation = useMutation({
    mutationFn: (id: string) => apiClient.delete(`/secrets/vaults/${selectedVaultId}/${id}`),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["secrets"] }); toast.success("Secret deleted."); },
  });

  const revealSecret = async (id: string) => {
    if (revealedValues[id]) { setRevealedValues(p => { const n = { ...p }; delete n[id]; return n; }); return; }
    try {
      const res = await apiClient.get<{ value: string }>(`/secrets/vaults/${selectedVaultId}/secrets/${id}/reveal`);
      setRevealedValues(p => ({ ...p, [id]: res.value }));
    } catch (e: any) { toast.error("Failed to reveal: " + e.message); }
  };

  const copyValue = async (id: string, value: string) => {
    await navigator.clipboard.writeText(value);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 1500);
  };

  const selectedVault = vaults?.find(v => v.id === selectedVaultId);

  return (
    <div className="mx-auto max-w-[1400px] space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
            <KeyRound className="h-6 w-6 text-amber-400" />
            Secret Management
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Encrypted secret store — AES-256. Secrets are injected at runtime, never stored in plain text.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" className="gap-1.5" onClick={() => setAuditOpen(true)}>
            <Clock className="h-3.5 w-3.5" /> Audit Log
          </Button>
          <Button className="gap-2" onClick={() => setCreateVaultOpen(true)}>
            <Plus className="h-4 w-4" /> New Vault
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-4">
        {/* Vault list */}
        <div className="space-y-2">
          <h2 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Vaults</h2>
          {vaultsLoading && <Skeleton className="h-20 w-full rounded-xl" />}
          {vaults?.length === 0 && (
            <Card>
              <CardContent className="flex flex-col items-center py-8 text-center">
                <Shield className="h-8 w-8 text-muted-foreground/30 mb-2" />
                <p className="text-xs text-muted-foreground">No vaults yet.</p>
              </CardContent>
            </Card>
          )}
          {vaults?.map(v => (
            <button key={v.id} type="button" onClick={() => setSelectedVaultId(v.id)}
              className={cn("w-full text-left rounded-xl border bg-card p-3 space-y-1 transition-all hover:border-border/60", selectedVaultId === v.id && "ring-1 ring-primary border-primary/50")}>
              <div className="flex items-center gap-1.5">
                {v.isLocked ? <Lock className="h-3.5 w-3.5 text-amber-400" /> : <Unlock className="h-3.5 w-3.5 text-emerald-400" />}
                <span className="text-sm font-semibold truncate">{v.name}</span>
              </div>
              <p className="text-xs text-muted-foreground">{v.secretCount} secret{v.secretCount !== 1 ? "s" : ""}</p>
              {v.description && <p className="text-[10px] text-muted-foreground/60 truncate">{v.description}</p>}
            </button>
          ))}
          <Button variant="ghost" size="sm" className="w-full gap-1.5 justify-start text-xs text-muted-foreground" onClick={() => setCreateVaultOpen(true)}>
            <Plus className="h-3 w-3" /> Add Vault
          </Button>
        </div>

        {/* Secrets table */}
        <div className="lg:col-span-3 space-y-4">
          {!selectedVaultId && (
            <Card>
              <CardContent className="flex flex-col items-center justify-center py-20 text-muted-foreground/40">
                <KeyRound className="h-10 w-10 mb-2" />
                <p className="text-sm">Select a vault to view secrets.</p>
              </CardContent>
            </Card>
          )}

          {selectedVaultId && (
            <>
              <div className="flex items-center justify-between">
                <div>
                  <h2 className="font-semibold">{selectedVault?.name}</h2>
                  <p className="text-xs text-muted-foreground">{secrets?.length ?? 0} secrets</p>
                </div>
                <Button size="sm" className="gap-1.5" onClick={() => setCreateSecretOpen(true)}>
                  <Plus className="h-3.5 w-3.5" /> Add Secret
                </Button>
              </div>

              {secretsLoading && <Skeleton className="h-40 w-full rounded-xl" />}

              {secrets?.length === 0 && (
                <Card>
                  <CardContent className="flex flex-col items-center py-12 text-center">
                    <KeyRound className="h-8 w-8 text-muted-foreground/30 mb-2" />
                    <p className="text-sm text-muted-foreground">No secrets in this vault.</p>
                    <Button size="sm" variant="outline" className="mt-3 gap-1.5" onClick={() => setCreateSecretOpen(true)}>
                      <Plus className="h-3.5 w-3.5" /> Add Secret
                    </Button>
                  </CardContent>
                </Card>
              )}

              <div className="space-y-2">
                {secrets?.map(s => {
                  const revealed = revealedValues[s.id];
                  return (
                    <Card key={s.id} className={cn(s.isExpired && "border-destructive/40 bg-destructive/5")}>
                      <CardContent className="flex items-start gap-4 p-4">
                        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-amber-500/10">
                          <KeyRound className="h-4 w-4 text-amber-400" />
                        </div>
                        <div className="flex-1 min-w-0 space-y-1">
                          <div className="flex items-center gap-2 flex-wrap">
                            <span className="font-mono text-sm font-semibold">{s.key}</span>
                            <Badge variant="outline" className="text-[10px]">{s.secretType}</Badge>
                            <Badge variant="outline" className="text-[10px]">v{s.version}</Badge>
                            {s.isExpired && <Badge variant="destructive" className="text-[10px] gap-1"><AlertTriangle className="h-2.5 w-2.5" />Expired</Badge>}
                          </div>
                          {s.description && <p className="text-xs text-muted-foreground">{s.description}</p>}
                          {revealed && (
                            <div className="flex items-center gap-2 mt-1.5">
                              <code className="flex-1 rounded bg-muted/60 px-2 py-1 text-xs font-mono break-all">{revealed}</code>
                              <Button size="icon" variant="ghost" className="h-6 w-6 shrink-0" onClick={() => copyValue(s.id, revealed)}>
                                {copiedId === s.id ? <Check className="h-3.5 w-3.5 text-emerald-400" /> : <Copy className="h-3.5 w-3.5" />}
                              </Button>
                            </div>
                          )}
                          <p className="text-[10px] text-muted-foreground">Rotation: {s.rotationPolicy} · Updated {formatRelativeTime(s.updatedAt)}</p>
                        </div>
                        <div className="flex gap-1 shrink-0">
                          <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => revealSecret(s.id)}>
                            {revealed ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
                          </Button>
                          <Button size="icon" variant="ghost" className="h-7 w-7 text-destructive hover:text-destructive" onClick={() => deleteSecretMutation.mutate(s.id)}>
                            <Trash2 className="h-3.5 w-3.5" />
                          </Button>
                        </div>
                      </CardContent>
                    </Card>
                  );
                })}
              </div>
            </>
          )}
        </div>
      </div>

      {/* Create Vault dialog */}
      <Dialog open={createVaultOpen} onOpenChange={setCreateVaultOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader><DialogTitle>New Vault</DialogTitle></DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Vault Name</Label>
              <Input value={newVault.name} onChange={e => setNewVault(p => ({ ...p, name: e.target.value }))} placeholder="Production Secrets" />
            </div>
            <div className="space-y-1.5">
              <Label>Description (optional)</Label>
              <Input value={newVault.description} onChange={e => setNewVault(p => ({ ...p, description: e.target.value }))} placeholder="API keys and credentials for production" />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateVaultOpen(false)}>Cancel</Button>
            <Button onClick={() => createVaultMutation.mutate({ tenantId: user?.tenantId ?? "", ...newVault })} disabled={createVaultMutation.isPending || !newVault.name}>
              Create Vault
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Create Secret dialog */}
      <Dialog open={createSecretOpen} onOpenChange={setCreateSecretOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>Add Secret</DialogTitle></DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Key (ENV_VAR style)</Label>
              <Input value={newSecret.key} onChange={e => setNewSecret(p => ({ ...p, key: e.target.value.toUpperCase().replace(/[^A-Z0-9_]/g, "_") }))} placeholder="DATABASE_URL" className="font-mono" />
            </div>
            <div className="space-y-1.5">
              <Label>Value</Label>
              <Input type="password" value={newSecret.value} onChange={e => setNewSecret(p => ({ ...p, value: e.target.value }))} placeholder="Will be AES-256 encrypted" />
              <p className="text-[10px] text-muted-foreground">Encrypted before storage. Value is never logged.</p>
            </div>
            <div className="space-y-1.5">
              <Label>Description (optional)</Label>
              <Input value={newSecret.description} onChange={e => setNewSecret(p => ({ ...p, description: e.target.value }))} />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Type</Label>
                <select value={newSecret.secretType} onChange={e => setNewSecret(p => ({ ...p, secretType: e.target.value }))} className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm">
                  {["PlainText", "Base64", "Json", "Certificate"].map(t => <option key={t}>{t}</option>)}
                </select>
              </div>
              <div className="space-y-1.5">
                <Label>Rotation Policy</Label>
                <select value={newSecret.rotationPolicy} onChange={e => setNewSecret(p => ({ ...p, rotationPolicy: e.target.value }))} className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm">
                  {["Never", "Daily", "Weekly", "Monthly"].map(t => <option key={t}>{t}</option>)}
                </select>
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>Expiry Date (optional)</Label>
              <Input type="date" value={newSecret.expiresAt} onChange={e => setNewSecret(p => ({ ...p, expiresAt: e.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateSecretOpen(false)}>Cancel</Button>
            <Button onClick={() => createSecretMutation.mutate({ tenantId: user?.tenantId ?? "", ...newSecret, expiresAt: newSecret.expiresAt || null })} disabled={createSecretMutation.isPending || !newSecret.key || !newSecret.value}>
              Store Secret
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Audit log dialog */}
      <Dialog open={auditOpen} onOpenChange={setAuditOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader><DialogTitle>Secret Audit Log</DialogTitle></DialogHeader>
          <ScrollArea className="h-96">
            <div className="space-y-0 divide-y divide-border/40">
              {(!auditLog || auditLog.length === 0) && <p className="text-sm text-muted-foreground p-4 text-center">No audit entries.</p>}
              {auditLog?.map(e => (
                <div key={e.id} className="flex items-center gap-3 px-1 py-2.5 text-xs">
                  <Badge variant="outline" className={cn("text-[10px] shrink-0", e.action === "read" ? "text-blue-400 border-blue-400/30" : e.action === "delete" ? "text-destructive border-destructive/30" : e.action === "rotate" ? "text-amber-400 border-amber-400/30" : "text-emerald-400 border-emerald-400/30")}>
                    {e.action}
                  </Badge>
                  <span className="font-mono text-muted-foreground/70 text-[10px]">{e.secretId.slice(0, 8)}…</span>
                  <span className="text-muted-foreground">{e.ipAddress ?? "—"}</span>
                  <span className="ml-auto text-muted-foreground/60">{formatRelativeTime(e.createdAt)}</span>
                </div>
              ))}
            </div>
          </ScrollArea>
        </DialogContent>
      </Dialog>
    </div>
  );
}
