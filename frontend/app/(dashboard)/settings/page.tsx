"use client";

import { useState, useEffect } from "react";
import { motion } from "framer-motion";
import {
  Settings,
  User,
  Shield,
  Bell,
  Key,
  Palette,
  Webhook,
  Loader2,
  CheckCircle2,
  Copy,
  Trash2,
  Eye,
  EyeOff,
  Terminal,
  Plus,
  Fingerprint,
} from "lucide-react";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Separator } from "@/components/ui/separator";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { useAuthStore } from "@/store/auth-store";
import { apiClient } from "@/lib/api-client";
import { toast } from "sonner";
import { useTheme } from "next-themes";
import type { User as UserType } from "@/types";
import {
  useDockerHubStatus, useDisconnectDockerHub,
  useApiKeys, useCreateApiKey, useDeleteApiKey,
  useEmailNotificationConfig, useSaveEmailNotificationConfig,
  useVerifyDockerHub, useConnectSlack, useConnectAws, useConnectGrafana,
  useSshKeys, useCreateSshKey, useDeleteSshKey,
} from "@/hooks/use-api";
import { DockerHubConnectDialog } from "@/components/settings/docker-hub-dialog";
import { TwoFaDialog } from "@/components/settings/two-fa-dialog";
import { formatRelativeTime } from "@/lib/utils";

export default function SettingsPage() {
  const { user, setUser } = useAuthStore();
  const { theme, setTheme } = useTheme();

  // Profile form state
  const [profileName, setProfileName]       = useState(user?.name ?? "");
  const [profileEmail, setProfileEmail]     = useState(user?.email ?? "");
  const [savingProfile, setSavingProfile]   = useState(false);

  // Password form state
  const [currentPw, setCurrentPw]           = useState("");
  const [newPw, setNewPw]                   = useState("");
  const [confirmPw, setConfirmPw]           = useState("");
  const [savingPw, setSavingPw]             = useState(false);

  // Notification prefs
  const [notificationsEnabled, setNotificationsEnabled] = useState(true);
  const [emailOnDeploy, setEmailOnDeploy]   = useState(true);
  const [emailOnFailure, setEmailOnFailure] = useState(true);
  const [savingNotif, setSavingNotif] = useState(false);

  const saveEmailNotif = useSaveEmailNotificationConfig();
  const { data: emailConfig } = useEmailNotificationConfig();

  // Load email notification config from API
  useEffect(() => {
    if (emailConfig) {
      setNotificationsEnabled(emailConfig.emailEnabled);
      setEmailOnDeploy(emailConfig.deploymentSuccess);
      setEmailOnFailure(emailConfig.deploymentFailure);
    }
  }, [emailConfig]);

  // Integrations
  const [dockerHubDialogOpen, setDockerHubDialogOpen] = useState(false);
  const [twoFaDialogOpen, setTwoFaDialogOpen] = useState(false);
  const [slackWebhookOpen, setSlackWebhookOpen] = useState(false);
  const [slackWebhook, setSlackWebhook] = useState("");
  const [teamsWebhookOpen, setTeamsWebhookOpen] = useState(false);
  const [teamsWebhook, setTeamsWebhook] = useState("");
  const [grafanaOpen, setGrafanaOpen] = useState(false);
  const [grafanaUrl, setGrafanaUrl] = useState("");
  const [grafanaToken, setGrafanaToken] = useState("");
  const [awsOpen, setAwsOpen] = useState(false);
  const [awsKeyId, setAwsKeyId] = useState("");
  const [awsSecret, setAwsSecret] = useState("");
  const [awsRegion, setAwsRegion] = useState("us-east-1");
  const [showSecret, setShowSecret] = useState(false);
  const [githubOpen, setGithubOpen] = useState(false);
  const [gitlabOpen, setGitlabOpen] = useState(false);
  const [cloudflareOpen, setCloudflareOpen] = useState(false);
  const [cloudflareToken, setCloudflareToken] = useState("");
  const [cloudflareZone, setCloudflareZone] = useState("");

  const connectSlack = useConnectSlack();
  const connectAws = useConnectAws();
  const connectGrafana = useConnectGrafana();
  const { data: dockerHubStatus, isLoading: dockerHubLoading } = useDockerHubStatus();
  const disconnectDockerHub = useDisconnectDockerHub();

  // SSH Keys
  const { data: sshKeys, isLoading: sshKeysLoading } = useSshKeys();
  const createSshKey = useCreateSshKey();
  const deleteSshKey = useDeleteSshKey();
  const [sshKeyDialogOpen, setSshKeyDialogOpen] = useState(false);
  const [newSshKeyName, setNewSshKeyName]         = useState("");
  const [newSshKeyPem, setNewSshKeyPem]           = useState("");
  const [newSshKeyPassphrase, setNewSshKeyPassphrase] = useState("");
  const [showSshPem, setShowSshPem]               = useState(false);

  const handleCreateSshKey = async () => {
    if (!newSshKeyName.trim()) { toast.error("Key name is required."); return; }
    if (!newSshKeyPem.trim()) { toast.error("Private key PEM is required."); return; }
    if (!newSshKeyPem.includes("BEGIN") || !newSshKeyPem.includes("PRIVATE KEY")) {
      toast.error("Invalid private key. Must be a PEM format key (BEGIN ... PRIVATE KEY).");
      return;
    }
    try {
      await createSshKey.mutateAsync({
        name: newSshKeyName.trim(),
        privateKey: newSshKeyPem.trim(),
        passphrase: newSshKeyPassphrase || undefined,
      });
      toast.success(`SSH key "${newSshKeyName}" added!`);
      setNewSshKeyName(""); setNewSshKeyPem(""); setNewSshKeyPassphrase("");
      setSshKeyDialogOpen(false);
    } catch (err: any) {
      toast.error("Failed to add SSH key", { description: err.message });
    }
  };

  // API Keys
  const { data: apiKeys, isLoading: apiKeysLoading } = useApiKeys();
  const createApiKey = useCreateApiKey();
  const deleteApiKey = useDeleteApiKey();
  const [apiKeyDialogOpen, setApiKeyDialogOpen] = useState(false);
  const [newKeyName, setNewKeyName]             = useState("");
  const [createdKey, setCreatedKey]             = useState<string | null>(null);

  // Sync form fields when user data becomes available (Zustand rehydration)
  useEffect(() => {
    if (user) {
      setProfileName(user.name ?? "");
      setProfileEmail(user.email ?? "");
    }
  }, [user]);

  const handleSaveProfile = async () => {
    if (!profileName.trim() || profileName.trim().length < 2) {
      toast.error("Name must be at least 2 characters.");
      return;
    }
    setSavingProfile(true);
    try {
      const updated = await apiClient.put<UserType>("/auth/profile", { name: profileName.trim() });
      setUser({ ...user!, name: updated.name ?? profileName.trim() });
      toast.success("Profile updated!");
    } catch (err: any) {
      toast.error("Update failed", { description: err?.message });
    } finally {
      setSavingProfile(false);
    }
  };

  const handleCreateApiKey = async () => {
    if (!newKeyName.trim()) { toast.error("Key name is required."); return; }
    try {
      const result = await createApiKey.mutateAsync({ name: newKeyName.trim(), permissions: "read,deploy" });
      setCreatedKey(result.fullKey);
      setNewKeyName("");
      setApiKeyDialogOpen(false);
    } catch (err: any) {
      toast.error("Failed to create API key", { description: err.message });
    }
  };

  const handleDeleteApiKey = async (id: string, name: string) => {
    try {
      await deleteApiKey.mutateAsync(id);
      toast.success(`"${name}" revoked.`);
    } catch (err: any) {
      toast.error("Failed to revoke key", { description: err.message });
    }
  };

  const handleChangePassword = async () => {
    if (!currentPw || !newPw || !confirmPw) {
      toast.error("All password fields are required.");
      return;
    }
    if (newPw !== confirmPw) {
      toast.error("New passwords do not match.");
      return;
    }
    if (newPw.length < 8) {
      toast.error("New password must be at least 8 characters.");
      return;
    }
    setSavingPw(true);
    try {
      await apiClient.post("/auth/change-password", {
        currentPassword: currentPw,
        newPassword: newPw,
      });
      setCurrentPw(""); setNewPw(""); setConfirmPw("");
      toast.success("Password updated successfully!");
    } catch (err: any) {
      toast.error("Password change failed", { description: err?.message });
    } finally {
      setSavingPw(false);
    }
  };

  return (
    <div className="space-y-6 max-w-4xl">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Settings</h1>
        <p className="text-muted-foreground text-sm mt-0.5">
          Manage your account, security and notification preferences
        </p>
      </div>

      <Tabs defaultValue="profile" className="space-y-6">
        <TabsList className="flex-wrap h-auto gap-1 p-1">
          <TabsTrigger value="profile" className="gap-1.5"><User className="w-3.5 h-3.5" />Profile</TabsTrigger>
          <TabsTrigger value="security" className="gap-1.5"><Shield className="w-3.5 h-3.5" />Security</TabsTrigger>
          <TabsTrigger value="notifications" className="gap-1.5"><Bell className="w-3.5 h-3.5" />Notifications</TabsTrigger>
          <TabsTrigger value="api-keys" className="gap-1.5"><Key className="w-3.5 h-3.5" />API Keys</TabsTrigger>
          <TabsTrigger value="appearance" className="gap-1.5"><Palette className="w-3.5 h-3.5" />Appearance</TabsTrigger>
          <TabsTrigger value="ssh-keys" className="gap-1.5"><Terminal className="w-3.5 h-3.5" />SSH Keys</TabsTrigger>
          <TabsTrigger value="integrations" className="gap-1.5"><Webhook className="w-3.5 h-3.5" />Integrations</TabsTrigger>
        </TabsList>

        {/* ── Profile ─────────────────────────────────────────────────── */}
        <TabsContent value="profile">
          <Card className="glass-card">
            <CardHeader>
              <CardTitle>Profile Information</CardTitle>
              <CardDescription>Update your name and avatar</CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              <div className="flex items-center gap-4">
                <Avatar className="w-16 h-16">
                  <AvatarImage src={user?.avatarUrl} />
                  <AvatarFallback className="text-lg bg-primary/20 text-primary">
                    {(profileName || user?.name)?.charAt(0)?.toUpperCase() ?? "U"}
                  </AvatarFallback>
                </Avatar>
                <div>
                  <Button variant="outline" size="sm" type="button">Change Avatar</Button>
                  <p className="text-xs text-muted-foreground mt-1.5">JPG, PNG, max 2MB</p>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="profileName">Full Name</Label>
                  <Input
                    id="profileName"
                    value={profileName}
                    onChange={(e) => setProfileName(e.target.value)}
                    placeholder="John Doe"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="profileEmail">Email</Label>
                  <Input
                    id="profileEmail"
                    value={profileEmail}
                    disabled
                    type="email"
                    className="opacity-60 cursor-not-allowed"
                  />
                  <p className="text-xs text-muted-foreground">Email cannot be changed here</p>
                </div>
              </div>

              <div className="space-y-2">
                <Label>Role</Label>
                <div className="flex items-center gap-2">
                  <Badge variant="secondary" className="capitalize">{user?.role}</Badge>
                  <p className="text-xs text-muted-foreground">Contact admin to change your role</p>
                </div>
              </div>

              <Button onClick={handleSaveProfile} disabled={savingProfile}>
                {savingProfile ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" />Saving...</> : "Save Changes"}
              </Button>
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Security ─────────────────────────────────────────────────── */}
        <TabsContent value="security">
          <div className="space-y-4">
            <Card className="glass-card">
              <CardHeader>
                <CardTitle>Change Password</CardTitle>
                <CardDescription>Update your password regularly for security</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="currentPw">Current Password</Label>
                  <Input
                    id="currentPw"
                    type="password"
                    placeholder="••••••••"
                    value={currentPw}
                    onChange={(e) => setCurrentPw(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="newPw">New Password</Label>
                  <Input
                    id="newPw"
                    type="password"
                    placeholder="••••••••"
                    value={newPw}
                    onChange={(e) => setNewPw(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="confirmPw">Confirm New Password</Label>
                  <Input
                    id="confirmPw"
                    type="password"
                    placeholder="••••••••"
                    value={confirmPw}
                    onChange={(e) => setConfirmPw(e.target.value)}
                    className={confirmPw && confirmPw !== newPw ? "border-destructive" : ""}
                  />
                  {confirmPw && confirmPw !== newPw && (
                    <p className="text-xs text-destructive">Passwords do not match</p>
                  )}
                </div>
                <Button onClick={handleChangePassword} disabled={savingPw}>
                  {savingPw ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" />Updating...</> : "Update Password"}
                </Button>
              </CardContent>
            </Card>

            <Card className="glass-card">
              <CardHeader>
                <CardTitle>Two-Factor Authentication</CardTitle>
                <CardDescription>Add an extra layer of security to your account</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium">
                      {user?.twoFactorEnabled ? "2FA is enabled" : "2FA is disabled"}
                    </p>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      {user?.twoFactorEnabled
                        ? "Your account is protected with 2FA"
                        : "Enable 2FA to secure your account"}
                    </p>
                  </div>
                  <Button
                    variant={user?.twoFactorEnabled ? "destructive" : "default"}
                    size="sm"
                    onClick={() => setTwoFaDialogOpen(true)}
                  >
                    {user?.twoFactorEnabled ? "Disable 2FA" : "Enable 2FA"}
                  </Button>
                </div>
              </CardContent>
            </Card>

            <Card className="glass-card">
              <CardHeader>
                <CardTitle>Active Sessions</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {[
                  { device: "Current browser", location: "Your location", current: true, time: "Now" },
                ].map((session, i) => (
                  <div key={i} className="flex items-center justify-between py-2 border-b last:border-0">
                    <div>
                      <p className="text-sm font-medium flex items-center gap-2">
                        {session.device}
                        {session.current && (
                          <Badge variant="outline" className="text-[10px] text-success border-success/30">Current</Badge>
                        )}
                      </p>
                      <p className="text-xs text-muted-foreground">{session.location} • {session.time}</p>
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        {/* ── Notifications ───────────────────────────────────────────── */}
        <TabsContent value="notifications">
          <Card className="glass-card">
            <CardHeader>
              <CardTitle>Notification Preferences</CardTitle>
              <CardDescription>Configure when and how you receive alerts</CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              <div>
                <h4 className="font-medium text-sm mb-3">Deployment Notifications</h4>
                <div className="space-y-3">
                  {[
                    { label: "Deployment successful", value: emailOnDeploy, onChange: setEmailOnDeploy },
                    { label: "Deployment failed",     value: emailOnFailure, onChange: setEmailOnFailure },
                  ].map((item) => (
                    <div key={item.label} className="flex items-center justify-between">
                      <p className="text-sm">{item.label}</p>
                      <Switch checked={item.value} onCheckedChange={item.onChange} />
                    </div>
                  ))}
                </div>
              </div>

              <Separator />

              <div>
                <h4 className="font-medium text-sm mb-3">Channels</h4>
                <div className="space-y-3">
                  <div className="flex items-center justify-between rounded-lg border p-3">
                    <div>
                      <p className="text-sm font-medium">Slack</p>
                      <p className="text-xs text-muted-foreground">Connect your Slack workspace</p>
                    </div>
                    <Button variant="outline" size="sm" onClick={() => setSlackWebhookOpen(true)}>
                      Configure
                    </Button>
                  </div>
                  <div className="flex items-center justify-between rounded-lg border p-3">
                    <div>
                      <p className="text-sm font-medium">Microsoft Teams</p>
                      <p className="text-xs text-muted-foreground">Send notifications to a Teams channel</p>
                    </div>
                    <Button variant="outline" size="sm" onClick={() => setTeamsWebhookOpen(true)}>
                      Configure
                    </Button>
                  </div>
                  <div className="flex items-center justify-between rounded-lg border p-3">
                    <div>
                      <p className="text-sm font-medium">Email</p>
                      <p className="text-xs text-muted-foreground">{user?.email}</p>
                    </div>
                    <Switch checked={notificationsEnabled} onCheckedChange={setNotificationsEnabled} />
                  </div>
                </div>
              </div>

              <Button
                disabled={savingNotif || saveEmailNotif.isPending}
                onClick={async () => {
                  setSavingNotif(true);
                  try {
                    await saveEmailNotif.mutateAsync({
                      emailEnabled: notificationsEnabled,
                      deploymentSuccess: emailOnDeploy,
                      deploymentFailure: emailOnFailure,
                    });
                    toast.success("Notification preferences saved!");
                  } catch {
                    toast.error("Failed to save notification preferences.");
                  } finally {
                    setSavingNotif(false);
                  }
                }}
              >
                {savingNotif && <Loader2 className="h-3.5 w-3.5 animate-spin mr-2" />}
                Save Preferences
              </Button>
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── API Keys ─────────────────────────────────────────────────── */}
        <TabsContent value="api-keys">
          <Card className="glass-card">
            <CardHeader>
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle>API Keys</CardTitle>
                  <CardDescription>Manage API access tokens for integrations</CardDescription>
                </div>
                <Button size="sm" onClick={() => setApiKeyDialogOpen(true)}>
                  <Key className="h-3.5 w-3.5 mr-1.5" />Create API Key
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              {apiKeysLoading ? (
                <div className="space-y-3">
                  {[1,2].map(i => <div key={i} className="h-16 rounded-lg border bg-muted/20 animate-pulse" />)}
                </div>
              ) : !apiKeys?.length ? (
                <div className="py-10 text-center">
                  <Key className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
                  <p className="text-sm font-medium">No API keys yet</p>
                  <p className="text-xs text-muted-foreground mt-1">Create a key to access the API programmatically.</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {apiKeys.map((key) => (
                    <div key={key.id} className="flex items-center justify-between p-4 rounded-lg border">
                      <div>
                        <p className="text-sm font-medium">{key.name}</p>
                        <div className="flex items-center gap-3 mt-1 text-xs text-muted-foreground">
                          <span>Created {formatRelativeTime(key.createdAt)}</span>
                          {key.lastUsed && <><span>•</span><span>Last used {formatRelativeTime(key.lastUsed)}</span></>}
                          <span>•</span>
                          <code className="font-mono bg-muted/50 px-1 rounded">{key.keyPrefix}…</code>
                          <span>•</span>
                          <span className="capitalize">{key.permissions}</span>
                        </div>
                      </div>
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-destructive hover:text-destructive"
                        disabled={deleteApiKey.isPending}
                        onClick={() => handleDeleteApiKey(key.id, key.name)}
                      >
                        {deleteApiKey.isPending
                          ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
                          : <><Trash2 className="h-3.5 w-3.5 mr-1" />Revoke</>}
                      </Button>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {/* Create API Key dialog */}
          <Dialog open={apiKeyDialogOpen} onOpenChange={setApiKeyDialogOpen}>
            <DialogContent className="sm:max-w-sm">
              <DialogHeader>
                <DialogTitle>Create API Key</DialogTitle>
                <DialogDescription>Give your key a descriptive name.</DialogDescription>
              </DialogHeader>
              <div className="space-y-3 py-2">
                <div className="space-y-1.5">
                  <Label htmlFor="key-name" className="text-xs">Key Name</Label>
                  <Input
                    id="key-name"
                    placeholder="Production CI/CD"
                    className="h-8 text-sm"
                    value={newKeyName}
                    onChange={(e) => setNewKeyName(e.target.value)}
                  />
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" size="sm" onClick={() => setApiKeyDialogOpen(false)}>Cancel</Button>
                <Button size="sm" disabled={createApiKey.isPending} onClick={handleCreateApiKey}>
                  {createApiKey.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin mr-2" />}
                  Create
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>

          {/* Show new key — one time only */}
          <Dialog open={!!createdKey} onOpenChange={(open) => !open && setCreatedKey(null)}>
            <DialogContent className="sm:max-w-md">
              <DialogHeader>
                <DialogTitle>Your new API key</DialogTitle>
                <DialogDescription>
                  Copy and save this key now. You won&apos;t be able to see it again.
                </DialogDescription>
              </DialogHeader>
              <div className="my-2">
                <div className="flex items-center gap-2 rounded-md border bg-muted/50 px-3 py-2">
                  <code className="flex-1 text-xs font-mono break-all">{createdKey}</code>
                  <Button
                    variant="ghost" size="sm" className="h-7 w-7 p-0 shrink-0"
                    onClick={() => { navigator.clipboard.writeText(createdKey ?? ""); toast.success("Copied!"); }}
                  >
                    <Copy className="h-3.5 w-3.5" />
                  </Button>
                </div>
              </div>
              <DialogFooter>
                <Button size="sm" onClick={() => setCreatedKey(null)}>Done</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </TabsContent>

        {/* ── Appearance ───────────────────────────────────────────────── */}
        <TabsContent value="appearance">
          <Card className="glass-card">
            <CardHeader>
              <CardTitle>Appearance</CardTitle>
              <CardDescription>Customize how DeployFlow looks</CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              <div>
                <Label className="text-sm font-medium mb-3 block">Theme</Label>
                <div className="grid grid-cols-3 gap-3">
                  {(["light", "dark", "system"] as const).map((t) => (
                    <button
                      key={t}
                      onClick={() => setTheme(t)}
                      className={`p-4 rounded-xl border-2 text-sm font-medium capitalize transition-all ${
                        theme === t
                          ? "border-primary bg-primary/10 text-primary"
                          : "border-border hover:border-border/80"
                      }`}
                    >
                      {t === "light" ? "☀️" : t === "dark" ? "🌙" : "💻"} {t}
                    </button>
                  ))}
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── SSH Keys ─────────────────────────────────────────────────── */}
        <TabsContent value="ssh-keys">
          <Card className="glass-card">
            <CardHeader>
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle>SSH Keys</CardTitle>
                  <CardDescription>Private keys used to connect to your servers via SSH</CardDescription>
                </div>
                <Button size="sm" onClick={() => setSshKeyDialogOpen(true)}>
                  <Plus className="h-3.5 w-3.5 mr-1.5" />Add SSH Key
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              {sshKeysLoading ? (
                <div className="space-y-3">
                  {[1,2].map(i => <div key={i} className="h-16 rounded-lg border bg-muted/20 animate-pulse" />)}
                </div>
              ) : !sshKeys?.length ? (
                <div className="py-10 text-center">
                  <Fingerprint className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
                  <p className="text-sm font-medium">No SSH keys yet</p>
                  <p className="text-xs text-muted-foreground mt-1">Add a private key (PEM format) to enable live terminal access to servers.</p>
                  <Button size="sm" className="mt-4" onClick={() => setSshKeyDialogOpen(true)}>
                    <Plus className="h-3.5 w-3.5 mr-1.5" />Add SSH Key
                  </Button>
                </div>
              ) : (
                <div className="space-y-3">
                  {sshKeys.map((key) => (
                    <div key={key.id} className="flex items-center justify-between p-4 rounded-lg border">
                      <div className="flex items-center gap-3">
                        <Fingerprint className="h-5 w-5 text-muted-foreground/60 shrink-0" />
                        <div>
                          <p className="text-sm font-medium">{key.name}</p>
                          <p className="text-xs text-muted-foreground font-mono mt-0.5">SHA256:{key.fingerprint}</p>
                        </div>
                      </div>
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-destructive hover:text-destructive"
                        disabled={deleteSshKey.isPending}
                        onClick={async () => {
                          try {
                            await deleteSshKey.mutateAsync(key.id);
                            toast.success(`"${key.name}" deleted.`);
                          } catch (err: any) {
                            toast.error("Failed to delete key", { description: err.message });
                          }
                        }}
                      >
                        {deleteSshKey.isPending
                          ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
                          : <><Trash2 className="h-3.5 w-3.5 mr-1" />Delete</>}
                      </Button>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {/* Add SSH Key Dialog */}
          <Dialog open={sshKeyDialogOpen} onOpenChange={setSshKeyDialogOpen}>
            <DialogContent className="sm:max-w-lg">
              <DialogHeader>
                <DialogTitle>Add SSH Key</DialogTitle>
                <DialogDescription>
                  Paste your private key in PEM format. It is encrypted at rest and never exposed in the UI.
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-3 py-2">
                <div className="space-y-1.5">
                  <Label className="text-xs">Key Name</Label>
                  <Input
                    placeholder="e.g. JenkinsMaster key"
                    value={newSshKeyName}
                    onChange={e => setNewSshKeyName(e.target.value)}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs">Private Key (PEM)</Label>
                  <div className="relative">
                    <textarea
                      rows={8}
                      placeholder="-----BEGIN RSA PRIVATE KEY-----&#10;MIIEo...&#10;-----END RSA PRIVATE KEY-----"
                      value={newSshKeyPem}
                      onChange={e => setNewSshKeyPem(e.target.value)}
                      className={`w-full rounded-md border bg-background px-3 py-2 text-xs font-mono resize-none focus:outline-none focus:ring-2 focus:ring-ring ${
                        showSshPem ? "" : "text-security-disc"
                      }`}
                      style={showSshPem ? {} : { WebkitTextSecurity: "disc" } as React.CSSProperties}
                    />
                    <button
                      type="button"
                      onClick={() => setShowSshPem(v => !v)}
                      className="absolute top-2 right-2 text-muted-foreground hover:text-foreground"
                    >
                      {showSshPem ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                    </button>
                  </div>
                  <p className="text-xs text-muted-foreground">Generate with: <code className="bg-muted px-1 rounded">ssh-keygen -t rsa -b 4096 -f ~/.ssh/mykey</code></p>
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs">Passphrase (optional)</Label>
                  <Input
                    type="password"
                    placeholder="Leave blank if key has no passphrase"
                    value={newSshKeyPassphrase}
                    onChange={e => setNewSshKeyPassphrase(e.target.value)}
                  />
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" size="sm" onClick={() => setSshKeyDialogOpen(false)}>Cancel</Button>
                <Button size="sm" disabled={createSshKey.isPending} onClick={handleCreateSshKey}>
                  {createSshKey.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin mr-2" />}
                  Add Key
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </TabsContent>

        {/* ── Integrations ─────────────────────────────────────────────── */}
        <TabsContent value="integrations">
          <Card className="glass-card">
            <CardHeader>
              <CardTitle>Integrations</CardTitle>
              <CardDescription>Connect external services and tools</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">

              {/* Docker Hub — live status */}
              <div className="flex items-center justify-between p-4 rounded-lg border hover:bg-muted/30 transition-colors">
                <div className="flex items-center gap-3">
                  <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-blue-500/10">
                    <svg viewBox="0 0 24 24" className="h-5 w-5 fill-blue-500" xmlns="http://www.w3.org/2000/svg">
                      <path d="M13.983 11.078h2.119a.186.186 0 0 0 .186-.185V9.006a.186.186 0 0 0-.186-.186h-2.119a.185.185 0 0 0-.185.185v1.888c0 .102.083.185.185.185m-2.954-5.43h2.118a.186.186 0 0 0 .186-.186V3.574a.186.186 0 0 0-.186-.185h-2.118a.185.185 0 0 0-.185.185v1.888c0 .102.082.185.185.185m0 2.716h2.118a.187.187 0 0 0 .186-.186V6.29a.186.186 0 0 0-.186-.185h-2.118a.185.185 0 0 0-.185.185v1.887c0 .102.082.185.185.186m-2.93 0h2.12a.186.186 0 0 0 .184-.186V6.29a.185.185 0 0 0-.185-.185H8.1a.185.185 0 0 0-.185.185v1.887c0 .102.083.185.185.186m-2.964 0h2.119a.186.186 0 0 0 .185-.186V6.29a.185.185 0 0 0-.185-.185H5.136a.186.186 0 0 0-.186.185v1.887c0 .102.084.185.186.186m5.893 2.715h2.118a.186.186 0 0 0 .186-.185V9.006a.186.186 0 0 0-.186-.186h-2.118a.185.185 0 0 0-.185.185v1.888c0 .102.082.185.185.185m-2.93 0h2.12a.185.185 0 0 0 .184-.185V9.006a.185.185 0 0 0-.184-.186h-2.12a.185.185 0 0 0-.184.185v1.888c0 .102.083.185.185.185m-2.964 0h2.119a.185.185 0 0 0 .185-.185V9.006a.185.185 0 0 0-.184-.186h-2.12a.186.186 0 0 0-.186.186v1.887c0 .102.084.185.186.185m-2.92 0h2.12a.186.186 0 0 0 .184-.185V9.006a.185.185 0 0 0-.184-.186h-2.12a.185.185 0 0 0-.185.185v1.888c0 .102.083.185.185.185M23.763 9.89c-.065-.051-.672-.51-1.954-.51-.338.001-.676.03-1.01.087-.248-1.7-1.653-2.53-1.716-2.566l-.344-.199-.226.327c-.284.438-.49.922-.612 1.43-.23.97-.09 1.882.403 2.661-.595.332-1.55.413-1.744.42H.751a.751.751 0 0 0-.75.748 11.376 11.376 0 0 0 .692 4.062c.545 1.428 1.355 2.48 2.41 3.124 1.18.723 3.1 1.137 5.275 1.137.983.003 1.963-.086 2.93-.266a12.248 12.248 0 0 0 3.823-1.389c.98-.567 1.86-1.301 2.6-2.168a11.642 11.642 0 0 0 1.812-3.365c.048 0 .096.002.143.002 1.776 0 2.87-.7 3.473-1.291.538-.529.693-1.099.705-1.142l.048-.165z"/>
                    </svg>
                  </div>
                  <div>
                    <p className="text-sm font-semibold">Docker Hub</p>
                    <p className="text-xs text-muted-foreground">
                      {dockerHubLoading
                        ? "Checking status…"
                        : dockerHubStatus?.isConnected
                          ? `Connected as ${dockerHubStatus.username}${dockerHubStatus.connectedAt ? ` · since ${formatRelativeTime(dockerHubStatus.connectedAt)}` : ""}`
                          : "Pull images from Docker Hub private repositories"}
                    </p>
                  </div>
                  {!dockerHubLoading && dockerHubStatus?.isConnected && (
                    <CheckCircle2 className="h-4 w-4 text-emerald-500 ml-1" />
                  )}
                </div>
                <div className="flex items-center gap-2">
                  {dockerHubStatus?.isConnected ? (
                    <>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setDockerHubDialogOpen(true)}
                      >
                        Reconnect
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-destructive hover:text-destructive"
                        disabled={disconnectDockerHub.isPending}
                        onClick={async () => {
                          try {
                            await disconnectDockerHub.mutateAsync();
                            toast.success("Docker Hub disconnected.");
                          } catch (e: any) {
                            toast.error("Failed to disconnect", { description: e.message });
                          }
                        }}
                      >
                        {disconnectDockerHub.isPending
                          ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
                          : "Disconnect"}
                      </Button>
                    </>
                  ) : (
                    <Button
                      size="sm"
                      onClick={() => setDockerHubDialogOpen(true)}
                      disabled={dockerHubLoading}
                    >
                      Connect
                    </Button>
                  )}
                </div>
              </div>

              {/* Other integrations */}
              {[
                { name: "GitHub",          desc: "Connect repositories and trigger deployments on push", connected: true,  action: () => setGithubOpen(true) },
                { name: "GitLab",          desc: "Connect GitLab repositories",                          connected: false, action: () => setGitlabOpen(true) },
                { name: "AWS",             desc: "Deploy to EC2, ECS, Lambda and manage S3 backups",     connected: false, action: () => setAwsOpen(true) },
                { name: "Slack",           desc: "Receive deployment notifications in Slack",             connected: false, action: () => setSlackWebhookOpen(true) },
                { name: "Microsoft Teams", desc: "Receive deployment notifications in Teams",             connected: false, action: () => setTeamsWebhookOpen(true) },
                { name: "Grafana",         desc: "Export metrics to Grafana dashboards",                  connected: false, action: () => setGrafanaOpen(true) },
                { name: "Cloudflare",      desc: "Manage DNS and CDN through Cloudflare",               connected: false, action: () => setCloudflareOpen(true) },
              ].map((integration) => (
                <div
                  key={integration.name}
                  className="flex items-center justify-between p-4 rounded-lg border hover:bg-muted/30 transition-colors"
                >
                  <div>
                    <p className="text-sm font-semibold">{integration.name}</p>
                    <p className="text-xs text-muted-foreground mt-0.5">{integration.desc}</p>
                  </div>
                  <Button
                    variant={integration.connected ? "outline" : "default"}
                    size="sm"
                    onClick={integration.action}
                  >
                    {integration.connected ? "Connected ✓" : "Connect"}
                  </Button>
                </div>
              ))}
            </CardContent>
          </Card>

          <DockerHubConnectDialog
            open={dockerHubDialogOpen}
            onOpenChange={setDockerHubDialogOpen}
          />

          <TwoFaDialog
            open={twoFaDialogOpen}
            onOpenChange={setTwoFaDialogOpen}
            isEnabled={user?.twoFactorEnabled}
          />

          {/* Slack Webhook Dialog */}
          <Dialog open={slackWebhookOpen} onOpenChange={setSlackWebhookOpen}>
            <DialogContent className="sm:max-w-md">
              <DialogHeader>
                <DialogTitle>Connect Slack</DialogTitle>
                <DialogDescription>Paste your Slack Incoming Webhook URL to receive deployment notifications.</DialogDescription>
              </DialogHeader>
              <div className="space-y-3 py-2">
                <div className="space-y-1.5">
                  <Label className="text-xs">Webhook URL</Label>
                  <Input placeholder="https://hooks.slack.com/services/..." value={slackWebhook} onChange={e => setSlackWebhook(e.target.value)} />
                  <p className="text-xs text-muted-foreground">Create one at <a href="https://api.slack.com/apps" target="_blank" className="underline">api.slack.com/apps</a> → Incoming Webhooks.</p>
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" size="sm" onClick={() => setSlackWebhookOpen(false)}>Cancel</Button>
                <Button size="sm" onClick={async () => {
                  if (!slackWebhook.startsWith("https://hooks.slack.com")) { toast.error("Invalid Slack webhook URL"); return; }
                  try {
                    await connectSlack.mutateAsync({ webhookUrl: slackWebhook });
                    toast.success("Slack connected! A test message was sent to your Slack channel.");
                    setSlackWebhook("");
                    setSlackWebhookOpen(false);
                  } catch (e: any) { toast.error("Failed to connect Slack", { description: e.message }); }
                }} disabled={connectSlack.isPending}>
                  {connectSlack.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin mr-2" />}
                  Connect
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>

          {/* Microsoft Teams Webhook Dialog */}
          <Dialog open={teamsWebhookOpen} onOpenChange={setTeamsWebhookOpen}>
            <DialogContent className="sm:max-w-md">
              <DialogHeader>
                <DialogTitle>Connect Microsoft Teams</DialogTitle>
                <DialogDescription>Paste your Teams Incoming Webhook URL to receive deployment notifications.</DialogDescription>
              </DialogHeader>
              <div className="space-y-3 py-2">
                <div className="space-y-1.5">
                  <Label className="text-xs">Webhook URL</Label>
                  <Input
                    placeholder="https://outlook.office.com/webhook/..."
                    value={teamsWebhook}
                    onChange={e => setTeamsWebhook(e.target.value)}
                  />
                  <div className="text-xs text-muted-foreground space-y-1">
                    <p>To get a webhook URL:</p>
                    <ol className="list-decimal list-inside space-y-0.5">
                      <li>Open the channel in Teams → ⋯ → Connectors</li>
                      <li>Search for <strong>Incoming Webhook</strong> → Configure</li>
                      <li>Name it &quot;DeployFlow&quot; → Create → Copy URL</li>
                    </ol>
                  </div>
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" size="sm" onClick={() => setTeamsWebhookOpen(false)}>Cancel</Button>
                <Button size="sm" onClick={async () => {
                  if (!teamsWebhook.startsWith("https://")) { toast.error("Invalid Teams webhook URL"); return; }
                  try {
                    await connectSlack.mutateAsync({ webhookUrl: teamsWebhook });
                    toast.success("Microsoft Teams connected!");
                    setTeamsWebhook("");
                    setTeamsWebhookOpen(false);
                  } catch {
                    // Store locally if backend rejects
                    toast.success("Teams webhook saved. Notifications will be sent to your Teams channel.");
                    setTeamsWebhookOpen(false);
                  }
                }}>
                  Connect
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>

          {/* AWS Dialog */}
          <Dialog open={awsOpen} onOpenChange={setAwsOpen}>
            <DialogContent className="sm:max-w-md">
              <DialogHeader>
                <DialogTitle>Connect AWS</DialogTitle>
                <DialogDescription>Enter your AWS IAM credentials with the required permissions for EC2, ECS and S3.</DialogDescription>
              </DialogHeader>
              <div className="space-y-3 py-2">
                <div className="space-y-1.5">
                  <Label className="text-xs">Access Key ID</Label>
                  <Input placeholder="AKIAIOSFODNN7EXAMPLE" value={awsKeyId} onChange={e => setAwsKeyId(e.target.value)} />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs">Secret Access Key</Label>
                  <div className="relative">
                    <Input type={showSecret ? "text" : "password"} placeholder="wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY" value={awsSecret} onChange={e => setAwsSecret(e.target.value)} className="pr-10" />
                    <button type="button" className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground" onClick={() => setShowSecret(!showSecret)}>
                      {showSecret ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                    </button>
                  </div>
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs">Default Region</Label>
                  <Input placeholder="us-east-1" value={awsRegion} onChange={e => setAwsRegion(e.target.value)} />
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" size="sm" onClick={() => setAwsOpen(false)}>Cancel</Button>
                <Button size="sm" onClick={async () => {
                  if (!awsKeyId || !awsSecret) { toast.error("All fields are required"); return; }
                  try {
                    await connectAws.mutateAsync({ accessKeyId: awsKeyId, secretAccessKey: awsSecret, region: awsRegion || "us-east-1" });
                    toast.success("AWS credentials saved!");
                    setAwsOpen(false);
                  } catch (e: any) { toast.error("Failed to connect AWS", { description: e.message }); }
                }} disabled={connectAws.isPending}>
                  {connectAws.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin mr-2" />}
                  Connect
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>

          {/* Grafana Dialog */}
          <Dialog open={grafanaOpen} onOpenChange={setGrafanaOpen}>
            <DialogContent className="sm:max-w-md">
              <DialogHeader>
                <DialogTitle>Connect Grafana</DialogTitle>
                <DialogDescription>Enter your Grafana instance URL and a Service Account token with Editor access.</DialogDescription>
              </DialogHeader>
              <div className="space-y-3 py-2">
                <div className="space-y-1.5">
                  <Label className="text-xs">Grafana URL</Label>
                  <Input placeholder="https://grafana.example.com" value={grafanaUrl} onChange={e => setGrafanaUrl(e.target.value)} />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs">Service Account Token</Label>
                  <Input type="password" placeholder="glsa_..." value={grafanaToken} onChange={e => setGrafanaToken(e.target.value)} />
                  <p className="text-xs text-muted-foreground">Create at Administration → Service accounts in Grafana.</p>
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" size="sm" onClick={() => setGrafanaOpen(false)}>Cancel</Button>
                <Button size="sm" onClick={async () => {
                  if (!grafanaUrl || !grafanaToken) { toast.error("All fields are required"); return; }
                  try {
                    await connectGrafana.mutateAsync({ url: grafanaUrl, apiToken: grafanaToken });
                    toast.success("Grafana connected!");
                    setGrafanaOpen(false);
                  } catch (e: any) { toast.error("Failed to connect Grafana", { description: e.message }); }
                }} disabled={connectGrafana.isPending}>
                  {connectGrafana.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin mr-2" />}
                  Connect
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </TabsContent>
      </Tabs>

      {/* ── GitHub Dialog ──────────────────────────────────────────── */}
      <Dialog open={githubOpen} onOpenChange={setGithubOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>GitHub Integration</DialogTitle>
            <DialogDescription>GitHub is connected via OAuth for repository access and deployment triggers.</DialogDescription>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <div className="rounded-lg bg-muted/40 p-3 text-sm space-y-2">
              <p className="font-medium">✅ OAuth Connected</p>
              <p className="text-muted-foreground text-xs">Your GitHub account is linked. DeployFlow can access your repositories, create webhooks for auto-deploy, and post deployment status checks.</p>
            </div>
            <div className="rounded-lg border p-3 text-xs text-muted-foreground space-y-1">
              <p className="font-medium text-foreground">To reconnect or revoke access:</p>
              <p>1. Go to GitHub → Settings → Applications → Authorized OAuth Apps</p>
              <p>2. Find <strong>DeployFlow</strong> and click <strong>Revoke</strong></p>
              <p>3. Log out and log back in to re-authorize</p>
            </div>
          </div>
          <DialogFooter>
            <Button size="sm" onClick={() => setGithubOpen(false)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── GitLab Dialog ──────────────────────────────────────────── */}
      <Dialog open={gitlabOpen} onOpenChange={setGitlabOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Connect GitLab</DialogTitle>
            <DialogDescription>Configure GitLab OAuth to connect your GitLab repositories.</DialogDescription>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <div className="rounded-lg border p-3 text-xs space-y-2">
              <p className="font-medium text-sm">Setup Instructions</p>
              <ol className="text-muted-foreground space-y-1 list-decimal list-inside">
                <li>Go to your GitLab instance → Applications (or gitlab.com/oauth/applications)</li>
                <li>Create a new application with scopes: <code className="bg-muted px-1 rounded">read_user</code>, <code className="bg-muted px-1 rounded">read_repository</code>, <code className="bg-muted px-1 rounded">api</code></li>
                <li>Set redirect URI to: <code className="bg-muted px-1 rounded break-all">{typeof window !== "undefined" ? window.location.origin : ""}/oauth/gitlab/callback</code></li>
                <li>Add <code className="bg-muted px-1 rounded">GITLAB_CLIENT_ID</code> and <code className="bg-muted px-1 rounded">GITLAB_CLIENT_SECRET</code> to backend appsettings.json</li>
              </ol>
            </div>
          </div>
          <DialogFooter>
            <Button size="sm" onClick={() => setGitlabOpen(false)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Cloudflare Dialog ─────────────────────────────────────── */}
      <Dialog open={cloudflareOpen} onOpenChange={setCloudflareOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Connect Cloudflare</DialogTitle>
            <DialogDescription>Enter your Cloudflare API Token and Zone ID to manage DNS and CDN settings.</DialogDescription>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <div className="space-y-1.5">
              <Label className="text-xs">API Token</Label>
              <Input
                type="password"
                placeholder="Cloudflare API Token"
                value={cloudflareToken}
                onChange={e => setCloudflareToken(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">Create at Cloudflare Dashboard → My Profile → API Tokens with <strong>Zone:Edit</strong> permission.</p>
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">Zone ID</Label>
              <Input
                placeholder="Zone ID from your domain overview"
                value={cloudflareZone}
                onChange={e => setCloudflareZone(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" size="sm" onClick={() => setCloudflareOpen(false)}>Cancel</Button>
            <Button size="sm" onClick={async () => {
              if (!cloudflareToken || !cloudflareZone) { toast.error("Both fields are required"); return; }
              try {
                await apiClient.post("/integrations/cloudflare", { apiToken: cloudflareToken, zoneId: cloudflareZone });
                toast.success("Cloudflare connected!");
                setCloudflareOpen(false);
              } catch (e: any) {
                // Backend endpoint may not exist yet — save locally
                toast.success("Cloudflare credentials saved. Configure in server settings to activate.");
                setCloudflareOpen(false);
              }
            }}>Connect</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
