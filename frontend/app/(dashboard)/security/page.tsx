"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Shield, Key, Eye, EyeOff, Copy, Trash2, Plus, Lock,
  Smartphone, FileText, History, CheckCircle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Switch } from "@/components/ui/switch";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from "@/components/ui/dialog";
import { useSshKeys, useCreateSshKey, useDeleteSshKey } from "@/hooks/use-api";
import { toast } from "sonner";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { TwoFaDialog } from "@/components/settings/two-fa-dialog";
import { useAuthStore } from "@/store/auth-store";

export default function SecurityPage() {
  const { user } = useAuthStore();
  const [addKeyOpen, setAddKeyOpen] = useState(false);
  const [twoFaOpen, setTwoFaOpen] = useState(false);
  const [keyForm, setKeyForm] = useState({ name: "", privateKey: "", passphrase: "" });
  const [showKey, setShowKey] = useState(false);
  const { data: sshKeys, isLoading } = useSshKeys();
  const createSshKey = useCreateSshKey();
  const deleteSshKey = useDeleteSshKey();

  const handleCreateKey = async () => {
    if (!keyForm.name || !keyForm.privateKey) {
      toast.error("Name and private key are required.");
      return;
    }
    try {
      await createSshKey.mutateAsync({ name: keyForm.name, privateKey: keyForm.privateKey, passphrase: keyForm.passphrase || undefined });
      toast.success(`SSH key "${keyForm.name}" added.`);
      setAddKeyOpen(false);
      setKeyForm({ name: "", privateKey: "", passphrase: "" });
    } catch (e: any) {
      toast.error("Failed to add SSH key", { description: e.message });
    }
  };

  const handleDeleteKey = async (id: string, name: string) => {
    if (!confirm(`Delete SSH key "${name}"?`)) return;
    try {
      await deleteSshKey.mutateAsync(id);
      toast.success("SSH key deleted.");
    } catch (e: any) {
      toast.error("Failed to delete key", { description: e.message });
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Security</h1>
        <p className="text-muted-foreground text-sm mt-0.5">
          Manage SSH keys, two-factor authentication, and security settings.
        </p>
      </div>

      <Tabs defaultValue="ssh-keys">
        <TabsList>
          <TabsTrigger value="ssh-keys">
            <Key className="h-4 w-4 mr-2" />SSH Keys
          </TabsTrigger>
          <TabsTrigger value="2fa">
            <Smartphone className="h-4 w-4 mr-2" />Two-Factor Auth
          </TabsTrigger>
          <TabsTrigger value="audit">
            <History className="h-4 w-4 mr-2" />Audit Log
          </TabsTrigger>
        </TabsList>

        {/* SSH Keys */}
        <TabsContent value="ssh-keys" className="mt-4 space-y-4">
          <div className="flex justify-between items-center">
            <p className="text-sm text-muted-foreground">
              SSH keys are used to authenticate with your servers.
            </p>
            <Button size="sm" onClick={() => setAddKeyOpen(true)}>
              <Plus className="h-4 w-4 mr-2" />Add Key
            </Button>
          </div>

          {sshKeys?.length === 0
            ? (
              <Card>
                <CardContent className="py-12 text-center">
                  <Key className="h-10 w-10 text-muted-foreground mx-auto mb-3" />
                  <p className="font-medium">No SSH keys</p>
                  <p className="text-sm text-muted-foreground">Add an SSH key to connect to your servers.</p>
                </CardContent>
              </Card>
            )
            : (
              <div className="space-y-3">
                {sshKeys?.map((key) => (
                  <motion.div key={key.id} initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
                    <Card>
                      <CardContent className="py-4 flex items-start gap-3">
                        <Key className="h-5 w-5 text-muted-foreground mt-0.5 flex-shrink-0" />
                        <div className="flex-1 min-w-0">
                          <p className="font-medium">{key.name}</p>
                          <p className="text-xs font-mono text-muted-foreground mt-0.5 truncate">
                            {key.fingerprint}
                          </p>
                          <p className="text-xs text-muted-foreground mt-0.5">
                            Added {new Date(key.createdAt).toLocaleDateString()}
                          </p>
                        </div>
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 text-destructive hover:text-destructive"
                          onClick={() => handleDeleteKey(key.id, key.name)}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </CardContent>
                    </Card>
                  </motion.div>
                ))}
              </div>
            )}
        </TabsContent>

        {/* 2FA */}
        <TabsContent value="2fa" className="mt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Two-Factor Authentication</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-sm text-muted-foreground">
                Add an extra layer of security to your account. When enabled, you&apos;ll need your
                authenticator app code to sign in.
              </p>
              <div className="flex items-center justify-between py-3 border rounded-lg px-4">
                <div className="flex items-center gap-3">
                  <Smartphone className="h-5 w-5 text-muted-foreground" />
                  <div>
                    <p className="font-medium text-sm">Authenticator App</p>
                    <p className="text-xs text-muted-foreground">Use Google Authenticator, Authy, or similar</p>
                  </div>
                    {user?.twoFactorEnabled && (
                      <span className="ml-1 text-xs text-emerald-500 font-medium">● Enabled</span>
                    )}
                  </div>
                  <Button
                    variant={user?.twoFactorEnabled ? "destructive" : "outline"}
                    size="sm"
                    onClick={() => setTwoFaOpen(true)}
                  >
                    <Shield className="h-4 w-4 mr-2" />
                    {user?.twoFactorEnabled ? "Disable" : "Enable"}
                  </Button>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

        {/* Audit Log */}
        <TabsContent value="audit" className="mt-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Recent Activity</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground text-center py-8">
                View the full audit log in the <a href="/logs" className="text-primary underline">Logs</a> section.
              </p>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Add SSH Key Dialog */}
      <Dialog open={addKeyOpen} onOpenChange={setAddKeyOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader><DialogTitle>Add SSH Key</DialogTitle></DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Key Name</Label>
              <Input
                placeholder="production-key"
                value={keyForm.name}
                onChange={(e) => setKeyForm((f) => ({ ...f, name: e.target.value }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Private Key</Label>
              <div className="relative">
                <textarea
                  className="w-full h-32 font-mono text-xs p-3 rounded-md border bg-background resize-none"
                  placeholder="-----BEGIN RSA PRIVATE KEY-----&#10;..."
                  value={keyForm.privateKey}
                  onChange={(e) => setKeyForm((f) => ({ ...f, privateKey: e.target.value }))}
                />
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>Passphrase (optional)</Label>
              <Input
                type="password"
                placeholder="Leave blank if no passphrase"
                value={keyForm.passphrase}
                onChange={(e) => setKeyForm((f) => ({ ...f, passphrase: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddKeyOpen(false)}>Cancel</Button>
            <Button onClick={handleCreateKey} disabled={createSshKey.isPending}>
              {createSshKey.isPending ? "Adding..." : "Add SSH Key"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <TwoFaDialog
        open={twoFaOpen}
        onOpenChange={setTwoFaOpen}
        isEnabled={user?.twoFactorEnabled}
      />
    </div>
  );
}
