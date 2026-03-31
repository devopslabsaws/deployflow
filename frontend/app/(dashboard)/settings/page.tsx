"use client";

import { useState, useEffect, useRef, useCallback } from "react";
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
  Camera,
  ZoomIn,
  ZoomOut,
  RotateCcw,
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
  useVerifyDockerHub,
  useConnectSlack, useSlackStatus, useDisconnectSlack,
  useConnectAws, useAwsStatus, useDisconnectAws,
  useConnectGrafana, useGrafanaStatus, useDisconnectGrafana,
  useGitHubIntegrationStatus, useConnectGitHub, useDisconnectGitHub,
  useGitLabIntegrationStatus, useConnectGitLab, useDisconnectGitLab,
  useTeamsStatus, useConnectTeams, useDisconnectTeams,
  useCloudflareStatus, useConnectCloudflare, useDisconnectCloudflare,
  useTestNotificationChannel,
  useSshKeys, useCreateSshKey, useDeleteSshKey,
} from "@/hooks/use-api";
import type { NotificationChannel } from "@/types";
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
  const [githubUsername, setGithubUsername] = useState("");
  const [githubPat, setGithubPat] = useState("");
  const [showGithubPat, setShowGithubPat] = useState(false);
  const [gitlabOpen, setGitlabOpen] = useState(false);
  const [gitlabUsername, setGitlabUsername] = useState("");
  const [gitlabPat, setGitlabPat] = useState("");
  const [showGitlabPat, setShowGitlabPat] = useState(false);
  const [cloudflareOpen, setCloudflareOpen] = useState(false);
  const [cloudflareToken, setCloudflareToken] = useState("");
  const [cloudflareZone, setCloudflareZone] = useState("");
  const [showCloudflareToken, setShowCloudflareToken] = useState(false);

  // Integration status queries
  const { data: dockerHubStatus, isLoading: dockerHubLoading } = useDockerHubStatus();
  const { data: githubStatus,    isLoading: githubLoading }    = useGitHubIntegrationStatus();
  const { data: gitlabStatus,    isLoading: gitlabLoading }    = useGitLabIntegrationStatus();
  const { data: slackStatus,     isLoading: slackLoading }     = useSlackStatus();
  const { data: teamsStatus,     isLoading: teamsLoading }     = useTeamsStatus();
  const { data: awsStatus,       isLoading: awsLoading }       = useAwsStatus();
  const { data: grafanaStatus,   isLoading: grafanaLoading }   = useGrafanaStatus();
  const { data: cloudflareStatus, isLoading: cloudflareLoading } = useCloudflareStatus();

  // Integration connect mutations
  const connectSlack     = useConnectSlack();
  const connectAws       = useConnectAws();
  const connectGrafana   = useConnectGrafana();
  const connectGitHub    = useConnectGitHub();
  const connectGitLab    = useConnectGitLab();
  const connectTeams     = useConnectTeams();
  const connectCloudflare = useConnectCloudflare();

  // Integration disconnect mutations
  const disconnectDockerHub  = useDisconnectDockerHub();
  const disconnectGitHub     = useDisconnectGitHub();
  const disconnectGitLab     = useDisconnectGitLab();
  const disconnectSlack      = useDisconnectSlack();
  const disconnectTeams      = useDisconnectTeams();
  const disconnectAws        = useDisconnectAws();
  const disconnectGrafana    = useDisconnectGrafana();
  const disconnectCloudflare = useDisconnectCloudflare();

  const testNotificationChannel = useTestNotificationChannel();

  const handleTestChannel = async (channel: NotificationChannel, label: string) => {
    try {
      const result = await testNotificationChannel.mutateAsync(channel);
      toast.success(`${label} test sent.`, { description: result.message });
    } catch (e: any) {
      toast.error(`${label} test failed`, { description: e.message });
    }
  };

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

  // Avatar upload state
  const [avatarDialogOpen, setAvatarDialogOpen] = useState(false);
  const [avatarSrc, setAvatarSrc] = useState<string | null>(null);
  const [avatarZoom, setAvatarZoom] = useState(1);
  const [avatarOffset, setAvatarOffset] = useState({ x: 0, y: 0 });
  const [avatarDragging, setAvatarDragging] = useState(false);
  const [avatarDragStart, setAvatarDragStart] = useState({ x: 0, y: 0, ox: 0, oy: 0 });
  const [savingAvatar, setSavingAvatar] = useState(false);
  const avatarCanvasRef = useRef<HTMLCanvasElement>(null);
  const avatarFileRef = useRef<HTMLInputElement>(null);
  const avatarImgRef = useRef<HTMLImageElement | null>(null);

  const CROP_SIZE = 200;

  const drawAvatarCanvas = useCallback(() => {
    const canvas = avatarCanvasRef.current;
    const img = avatarImgRef.current;
    if (!canvas || !img) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    ctx.clearRect(0, 0, CROP_SIZE, CROP_SIZE);
    // Clip to circle
    ctx.save();
    ctx.beginPath();
    ctx.arc(CROP_SIZE / 2, CROP_SIZE / 2, CROP_SIZE / 2, 0, Math.PI * 2);
    ctx.clip();
    const scale = avatarZoom;
    const w = img.naturalWidth * scale;
    const h = img.naturalHeight * scale;
    const x = (CROP_SIZE - w) / 2 + avatarOffset.x;
    const y = (CROP_SIZE - h) / 2 + avatarOffset.y;
    ctx.drawImage(img, x, y, w, h);
    ctx.restore();
    // Circle border
    ctx.strokeStyle = "rgba(99,102,241,0.6)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(CROP_SIZE / 2, CROP_SIZE / 2, CROP_SIZE / 2 - 1, 0, Math.PI * 2);
    ctx.stroke();
  }, [avatarZoom, avatarOffset]);

  useEffect(() => { drawAvatarCanvas(); }, [drawAvatarCanvas, avatarSrc]);

  const handleAvatarFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (file.size > 2 * 1024 * 1024) { toast.error("Image must be under 2MB."); return; }
    const reader = new FileReader();
    reader.onload = (ev) => {
      const src = ev.target?.result as string;
      const img = new Image();
      img.onload = () => {
        avatarImgRef.current = img;
        const scale = Math.max(CROP_SIZE / img.naturalWidth, CROP_SIZE / img.naturalHeight);
        setAvatarZoom(scale);
        setAvatarOffset({ x: 0, y: 0 });
        setAvatarSrc(src);
        setAvatarDialogOpen(true);
      };
      img.src = src;
    };
    reader.readAsDataURL(file);
    e.target.value = "";
  };

  const handleAvatarMouseDown = (e: React.MouseEvent) => {
    setAvatarDragging(true);
    setAvatarDragStart({ x: e.clientX, y: e.clientY, ox: avatarOffset.x, oy: avatarOffset.y });
  };
  const handleAvatarMouseMove = (e: React.MouseEvent) => {
    if (!avatarDragging) return;
    setAvatarOffset({
      x: avatarDragStart.ox + (e.clientX - avatarDragStart.x),
      y: avatarDragStart.oy + (e.clientY - avatarDragStart.y),
    });
  };
  const handleAvatarTouchStart = (e: React.TouchEvent) => {
    const t = e.touches[0];
    setAvatarDragging(true);
    setAvatarDragStart({ x: t.clientX, y: t.clientY, ox: avatarOffset.x, oy: avatarOffset.y });
  };
  const handleAvatarTouchMove = (e: React.TouchEvent) => {
    if (!avatarDragging) return;
    const t = e.touches[0];
    setAvatarOffset({
      x: avatarDragStart.ox + (t.clientX - avatarDragStart.x),
      y: avatarDragStart.oy + (t.clientY - avatarDragStart.y),
    });
  };

  const handleSaveAvatar = async () => {
    const canvas = avatarCanvasRef.current;
    if (!canvas) return;
    setSavingAvatar(true);
    try {
      const dataUrl = canvas.toDataURL("image/jpeg", 0.85);
      const updated = await apiClient.put<UserType>("/auth/profile", {
        name: user?.name ?? profileName,
        avatarUrl: dataUrl,
      });
      setUser({ ...user!, avatarUrl: dataUrl, name: updated.name ?? user?.name ?? "" });
      toast.success("Avatar updated!");
      setAvatarDialogOpen(false);
      setAvatarSrc(null);
    } catch (err: any) {
      toast.error("Failed to update avatar", { description: err.message });
    } finally {
      setSavingAvatar(false);
    }
  };

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
                <div className="relative group cursor-pointer" onClick={() => avatarFileRef.current?.click()}>
                  <Avatar className="w-16 h-16">
                    <AvatarImage src={user?.avatarUrl} />
                    <AvatarFallback className="text-lg bg-primary/20 text-primary">
                      {(profileName || user?.name)?.charAt(0)?.toUpperCase() ?? "U"}
                    </AvatarFallback>
                  </Avatar>
                  <div className="absolute inset-0 flex items-center justify-center rounded-full bg-black/50 opacity-0 group-hover:opacity-100 transition-opacity">
                    <Camera className="w-5 h-5 text-white" />
                  </div>
                </div>
                <div>
                  <Button variant="outline" size="sm" type="button" onClick={() => avatarFileRef.current?.click()}>
                    Change Avatar
                  </Button>
                  <p className="text-xs text-muted-foreground mt-1.5">JPG, PNG, max 2MB</p>
                </div>
                <input
                  ref={avatarFileRef}
                  type="file"
                  accept="image/jpeg,image/png,image/gif,image/webp"
                  className="hidden"
                  onChange={handleAvatarFileChange}
                />
              </div>

              {/* ── Avatar Crop Dialog ─────────────────────────────── */}
              <Dialog open={avatarDialogOpen} onOpenChange={(o) => { if (!o) { setAvatarDialogOpen(false); setAvatarSrc(null); } }}>
                <DialogContent className="sm:max-w-sm">
                  <DialogHeader>
                    <DialogTitle>Crop Profile Picture</DialogTitle>
                    <DialogDescription>Drag to reposition · scroll or pinch to zoom</DialogDescription>
                  </DialogHeader>
                  <div className="flex flex-col items-center gap-4 py-2">
                    <div
                      className="rounded-full overflow-hidden border-2 border-primary/40 cursor-grab active:cursor-grabbing select-none"
                      style={{ width: CROP_SIZE, height: CROP_SIZE }}
                      onMouseDown={handleAvatarMouseDown}
                      onMouseMove={handleAvatarMouseMove}
                      onMouseUp={() => setAvatarDragging(false)}
                      onMouseLeave={() => setAvatarDragging(false)}
                      onTouchStart={handleAvatarTouchStart}
                      onTouchMove={handleAvatarTouchMove}
                      onTouchEnd={() => setAvatarDragging(false)}
                      onWheel={(e) => {
                        e.preventDefault();
                        setAvatarZoom((z) => Math.min(5, Math.max(0.5, z - e.deltaY * 0.002)));
                      }}
                    >
                      <canvas
                        ref={avatarCanvasRef}
                        width={CROP_SIZE}
                        height={CROP_SIZE}
                        style={{ display: "block", pointerEvents: "none" }}
                      />
                    </div>
                    {/* Zoom controls */}
                    <div className="flex items-center gap-3 w-full px-2">
                      <ZoomOut className="w-4 h-4 text-muted-foreground shrink-0" />
                      <input
                        type="range" min={0.3} max={5} step={0.05}
                        value={avatarZoom}
                        onChange={(e) => setAvatarZoom(Number(e.target.value))}
                        className="flex-1 accent-primary h-1.5"
                      />
                      <ZoomIn className="w-4 h-4 text-muted-foreground shrink-0" />
                      <Button variant="ghost" size="icon" className="h-7 w-7 shrink-0" title="Reset"
                        onClick={() => { setAvatarOffset({ x: 0, y: 0 }); const img = avatarImgRef.current; if (img) setAvatarZoom(Math.max(CROP_SIZE / img.naturalWidth, CROP_SIZE / img.naturalHeight)); }}>
                        <RotateCcw className="w-3.5 h-3.5" />
                      </Button>
                    </div>
                  </div>
                  <DialogFooter>
                    <Button variant="outline" onClick={() => { setAvatarDialogOpen(false); setAvatarSrc(null); }}>Cancel</Button>
                    <Button onClick={handleSaveAvatar} disabled={savingAvatar}>
                      {savingAvatar ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" />Saving…</> : "Set as Avatar"}
                    </Button>
                  </DialogFooter>
                </DialogContent>
              </Dialog>

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
                    <div className="flex items-center gap-2">
                      <Button variant="outline" size="sm" onClick={() => setSlackWebhookOpen(true)}>
                        Configure
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => handleTestChannel("slack", "Slack")}>Test</Button>
                    </div>
                  </div>
                  <div className="flex items-center justify-between rounded-lg border p-3">
                    <div>
                      <p className="text-sm font-medium">Microsoft Teams</p>
                      <p className="text-xs text-muted-foreground">Send notifications to a Teams channel</p>
                    </div>
                    <div className="flex items-center gap-2">
                      <Button variant="outline" size="sm" onClick={() => setTeamsWebhookOpen(true)}>
                        Configure
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => handleTestChannel("msteams", "Microsoft Teams")}>Test</Button>
                    </div>
                  </div>
                  <div className="flex items-center justify-between rounded-lg border p-3">
                    <div>
                      <p className="text-sm font-medium">Email</p>
                      <p className="text-xs text-muted-foreground">{user?.email}</p>
                    </div>
                    <div className="flex items-center gap-2">
                      <Switch checked={notificationsEnabled} onCheckedChange={setNotificationsEnabled} />
                      <Button variant="ghost" size="sm" onClick={() => handleTestChannel("email", "Email")}>Test</Button>
                    </div>
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
              {([
                {
                  key: "github", name: "GitHub", desc: "Connect repositories and trigger deployments on push",
                  status: githubStatus, loading: githubLoading, disconnect: disconnectGitHub,
                  onConnect: () => setGithubOpen(true), testChannel: "github" as NotificationChannel,
                  iconBg: "bg-zinc-500/10",
                  icon: <svg viewBox="0 0 24 24" className="h-5 w-5 fill-zinc-400" xmlns="http://www.w3.org/2000/svg"><path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"/></svg>,
                },
                {
                  key: "gitlab", name: "GitLab", desc: "Connect GitLab repositories",
                  status: gitlabStatus, loading: gitlabLoading, disconnect: disconnectGitLab,
                  onConnect: () => setGitlabOpen(true), testChannel: "gitlab" as NotificationChannel,
                  iconBg: "bg-orange-500/10",
                  icon: <svg viewBox="0 0 24 24" className="h-5 w-5 fill-orange-500" xmlns="http://www.w3.org/2000/svg"><path d="M23.955 13.587l-1.342-4.135-2.664-8.189c-.135-.423-.73-.423-.867 0L16.418 9.45H7.582L4.918 1.263C4.783.84 4.185.84 4.05 1.263L1.386 9.452.044 13.587a.916.916 0 0 0 .333 1.024L12 21.602l11.623-6.991a.92.92 0 0 0 .332-1.024"/></svg>,
                },
                {
                  key: "aws", name: "AWS", desc: "Deploy to EC2, ECS, Lambda and manage S3 backups",
                  status: awsStatus, loading: awsLoading, disconnect: disconnectAws,
                  onConnect: () => setAwsOpen(true),
                  iconBg: "bg-amber-500/10",
                  icon: <svg viewBox="0 0 24 24" className="h-5 w-5 fill-amber-500" xmlns="http://www.w3.org/2000/svg"><path d="M6.763 10.036c0 .296.032.535.088.71.064.176.144.368.256.576.04.063.056.127.056.183 0 .08-.048.16-.152.24l-.503.335a.383.383 0 0 1-.208.072c-.08 0-.16-.04-.239-.112a2.47 2.47 0 0 1-.287-.375 6.18 6.18 0 0 1-.248-.471c-.623.734-1.405 1.101-2.347 1.101-.67 0-1.205-.191-1.596-.574-.391-.384-.59-.894-.59-1.533 0-.678.239-1.23.726-1.644.487-.415 1.133-.623 1.955-.623.272 0 .551.024.846.064.296.04.6.104.918.176v-.583c0-.607-.127-1.03-.375-1.277-.255-.248-.686-.367-1.3-.367-.28 0-.568.031-.863.103-.295.072-.583.16-.862.272a2.287 2.287 0 0 1-.28.104.488.488 0 0 1-.127.023c-.112 0-.168-.08-.168-.247v-.391c0-.128.016-.224.056-.28a.597.597 0 0 1 .224-.167c.279-.144.614-.264 1.005-.36a4.84 4.84 0 0 1 1.246-.151c.95 0 1.644.216 2.091.647.439.43.662 1.085.662 1.963v2.586zm-3.24 1.214c.263 0 .534-.048.822-.144.287-.096.543-.271.758-.51.128-.152.224-.32.272-.512.047-.191.08-.423.08-.694v-.335a6.66 6.66 0 0 0-.735-.136 6.02 6.02 0 0 0-.75-.048c-.535 0-.926.104-1.19.32-.263.215-.39.518-.39.917 0 .375.095.655.295.846.191.2.47.296.838.296zm6.41.862c-.144 0-.24-.024-.304-.08-.064-.048-.12-.16-.168-.311L7.586 5.55a1.398 1.398 0 0 1-.072-.32c0-.128.064-.2.191-.2h.783c.151 0 .255.025.31.08.065.048.113.16.16.312l1.342 5.284 1.245-5.284c.04-.16.088-.264.151-.312a.549.549 0 0 1 .32-.08h.638c.152 0 .256.025.32.08.063.048.12.16.151.312l1.261 5.348 1.381-5.348c.048-.16.104-.264.16-.312a.52.52 0 0 1 .311-.08h.743c.127 0 .2.065.2.2 0 .04-.009.08-.017.128a1.137 1.137 0 0 1-.056.2l-1.923 6.17c-.048.16-.104.263-.168.311a.51.51 0 0 1-.303.08h-.687c-.151 0-.255-.025-.32-.08-.063-.056-.119-.16-.15-.32l-1.238-5.148-1.23 5.14c-.04.16-.087.264-.15.32-.065.056-.177.08-.32.08zm10.256.215c-.415 0-.83-.048-1.229-.143-.399-.096-.71-.2-.918-.32-.128-.071-.215-.151-.247-.223a.563.563 0 0 1-.048-.224v-.407c0-.167.064-.247.183-.247.048 0 .096.008.144.024.048.016.12.048.2.08.271.12.566.215.878.279.319.064.63.096.95.096.502 0 .894-.088 1.165-.264a.86.86 0 0 0 .415-.758.777.777 0 0 0-.215-.559c-.144-.151-.415-.287-.807-.415l-1.157-.36c-.583-.183-1.014-.454-1.277-.813a1.902 1.902 0 0 1-.4-1.158c0-.335.073-.63.216-.886.144-.255.335-.479.575-.654.24-.184.51-.32.83-.415.32-.096.655-.136 1.006-.136.175 0 .359.008.535.032.183.024.35.056.518.088.16.04.312.08.455.127.144.048.256.096.336.144a.69.69 0 0 1 .24.2.43.43 0 0 1 .071.263v.375c0 .168-.064.256-.184.256a.83.83 0 0 1-.303-.096 3.652 3.652 0 0 0-1.532-.311c-.455 0-.815.071-1.062.223-.248.152-.375.383-.375.71 0 .224.08.416.24.567.159.152.454.304.877.44l1.134.358c.574.184.99.44 1.237.767.247.327.371.7.371 1.112 0 .343-.072.655-.207.926-.144.272-.336.511-.583.703-.248.2-.543.343-.886.44-.36.111-.734.167-1.142.167zM20.16 16.725c-2.395 1.77-5.875 2.713-8.865 2.713-4.19 0-7.959-1.547-10.806-4.116-.225-.199-.024-.472.247-.315 3.079 1.786 6.886 2.865 10.82 2.865 2.655 0 5.574-.55 8.257-1.691.406-.175.743.263.347.544zm1.01-1.155c-.305-.39-2.01-.184-2.775-.092-.234.028-.27-.175-.06-.32 1.358-.955 3.587-.677 3.846-.359.259.322-.067 2.555-1.342 3.62-.195.164-.381.077-.294-.14.286-.716.927-2.314.625-2.709z"/></svg>,
                },
                {
                  key: "slack", name: "Slack", desc: "Receive deployment notifications in Slack",
                  status: slackStatus, loading: slackLoading, disconnect: disconnectSlack,
                  onConnect: () => setSlackWebhookOpen(true), testChannel: "slack" as NotificationChannel,
                  iconBg: "bg-purple-500/10",
                  icon: <svg viewBox="0 0 24 24" className="h-5 w-5 fill-purple-500" xmlns="http://www.w3.org/2000/svg"><path d="M5.042 15.165a2.528 2.528 0 0 1-2.52 2.523A2.528 2.528 0 0 1 0 15.165a2.527 2.527 0 0 1 2.522-2.52h2.52v2.52zM6.313 15.165a2.527 2.527 0 0 1 2.521-2.52 2.527 2.527 0 0 1 2.521 2.52v6.313A2.528 2.528 0 0 1 8.834 24a2.528 2.528 0 0 1-2.521-2.522v-6.313zM8.834 5.042a2.528 2.528 0 0 1-2.521-2.52A2.528 2.528 0 0 1 8.834 0a2.528 2.528 0 0 1 2.521 2.522v2.52H8.834zM8.834 6.313a2.528 2.528 0 0 1 2.521 2.521 2.528 2.528 0 0 1-2.521 2.521H2.522A2.528 2.528 0 0 1 0 8.834a2.528 2.528 0 0 1 2.522-2.521h6.312zM18.956 8.834a2.528 2.528 0 0 1 2.522-2.521A2.528 2.528 0 0 1 24 8.834a2.528 2.528 0 0 1-2.522 2.521h-2.522V8.834zM17.688 8.834a2.528 2.528 0 0 1-2.523 2.521 2.527 2.527 0 0 1-2.52-2.521V2.522A2.527 2.527 0 0 1 15.165 0a2.528 2.528 0 0 1 2.523 2.522v6.312zM15.165 18.956a2.528 2.528 0 0 1 2.523 2.522A2.528 2.528 0 0 1 15.165 24a2.527 2.527 0 0 1-2.52-2.522v-2.522h2.52zM15.165 17.688a2.527 2.527 0 0 1-2.52-2.523 2.526 2.526 0 0 1 2.52-2.52h6.313A2.527 2.527 0 0 1 24 15.165a2.528 2.528 0 0 1-2.522 2.523h-6.313z"/></svg>,
                },
                {
                  key: "msteams", name: "Microsoft Teams", desc: "Receive deployment notifications in Teams",
                  status: teamsStatus, loading: teamsLoading, disconnect: disconnectTeams,
                  onConnect: () => setTeamsWebhookOpen(true), testChannel: "msteams" as NotificationChannel,
                  iconBg: "bg-blue-600/10",
                  icon: <svg viewBox="0 0 24 24" className="h-5 w-5 fill-blue-500" xmlns="http://www.w3.org/2000/svg"><path d="M20.625 7.875H14.25v8.25a3.375 3.375 0 0 0 3.375 3.375h3.375V9.75a1.875 1.875 0 0 0-1.875-1.875zm-6.375 0V6a3 3 0 0 0-3-3H6a3 3 0 0 0-3 3v10.5a3 3 0 0 0 3 3h5.25a3 3 0 0 0 3-3V7.875z"/></svg>,
                },
                {
                  key: "grafana", name: "Grafana", desc: "Export metrics to Grafana dashboards",
                  status: grafanaStatus, loading: grafanaLoading, disconnect: disconnectGrafana,
                  onConnect: () => setGrafanaOpen(true),
                  iconBg: "bg-orange-600/10",
                  icon: <svg viewBox="0 0 24 24" className="h-5 w-5 fill-orange-600" xmlns="http://www.w3.org/2000/svg"><path d="M12 2C6.477 2 2 6.477 2 12s4.477 10 10 10 10-4.477 10-10S17.523 2 12 2zm0 2c4.418 0 8 3.582 8 8s-3.582 8-8 8-8-3.582-8-8 3.582-8 8-8zm-1 3v6l5 3-.75-1.3L14 13.5V7h-3z"/></svg>,
                },
                {
                  key: "cloudflare", name: "Cloudflare", desc: "Manage DNS and CDN through Cloudflare",
                  status: cloudflareStatus, loading: cloudflareLoading, disconnect: disconnectCloudflare,
                  onConnect: () => setCloudflareOpen(true),
                  iconBg: "bg-amber-600/10",
                  icon: <svg viewBox="0 0 24 24" className="h-5 w-5 fill-amber-600" xmlns="http://www.w3.org/2000/svg"><path d="M16.608 15.408l.24-.83c.09-.3.06-.579-.077-.787a.733.733 0 0 0-.63-.285l-8.03.108a.24.24 0 0 1-.22-.143.24.24 0 0 1 .041-.258l.162-.195c.212-.224.485-.348.78-.348l8.128-.035c1.025-.045 2.137-.875 2.525-1.894l.492-1.278a.477.477 0 0 0 .025-.273A7.495 7.495 0 0 0 12.002 4.5C8.46 4.5 5.46 6.957 4.63 10.23a4.003 4.003 0 0 0-2.793 1.268 3.965 3.965 0 0 0-1.02 2.9A4.002 4.002 0 0 0 4.8 18.203l11.4-.03a.477.477 0 0 0 .46-.352l.226-.78-.277-.165.277.165-.278-.168z"/></svg>,
                },
              ] as { key: string; name: string; desc: string; status: any; loading: boolean; disconnect: any; onConnect: () => void; testChannel?: NotificationChannel; iconBg: string; icon: JSX.Element }[]).map((intg) => (
                <div key={intg.key} className="flex items-center justify-between p-4 rounded-lg border hover:bg-muted/30 transition-colors">
                  <div className="flex items-center gap-3">
                    <div className={`flex h-9 w-9 items-center justify-center rounded-lg ${intg.iconBg}`}>
                      {intg.icon}
                    </div>
                    <div>
                      <p className="text-sm font-semibold">{intg.name}</p>
                      <p className="text-xs text-muted-foreground mt-0.5">
                        {intg.loading
                          ? "Checking status…"
                          : intg.status?.isConnected
                            ? `Connected${intg.status.username ? ` as ${intg.status.username}` : ""}${intg.status.connectedAt ? ` · since ${formatRelativeTime(intg.status.connectedAt)}` : ""}`
                            : intg.desc}
                      </p>
                    </div>
                    {!intg.loading && intg.status?.isConnected && (
                      <CheckCircle2 className="h-4 w-4 text-emerald-500 ml-1" />
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    {intg.status?.isConnected ? (
                      <>
                        <Button variant="outline" size="sm" onClick={intg.onConnect}>
                          Reconnect
                        </Button>
                        {intg.testChannel && (
                          <Button variant="ghost" size="sm"
                            onClick={() => handleTestChannel(intg.testChannel!, intg.name)}>
                            Test
                          </Button>
                        )}
                        <Button
                          variant="ghost" size="sm"
                          className="text-destructive hover:text-destructive"
                          disabled={intg.disconnect.isPending}
                          onClick={async () => {
                            try {
                              await intg.disconnect.mutateAsync();
                              toast.success(`${intg.name} disconnected.`);
                            } catch (e: any) {
                              toast.error(`Failed to disconnect ${intg.name}`, { description: e.message });
                            }
                          }}
                        >
                          {intg.disconnect.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : "Disconnect"}
                        </Button>
                      </>
                    ) : (
                      <Button size="sm" onClick={intg.onConnect} disabled={intg.loading}>
                        Connect
                      </Button>
                    )}
                  </div>
                </div>
              ))}
            </CardContent>
          </Card>

          <DockerHubConnectDialog
            open={dockerHubDialogOpen}
            onOpenChange={setDockerHubDialogOpen}
          />


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

      {/* ── Dialogs triggered from Security / Notifications tabs (must live outside TabsContent) ── */}
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
              <p className="text-xs text-muted-foreground">Create one at api.slack.com/apps → Incoming Webhooks.</p>
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
            <Button size="sm" disabled={connectTeams.isPending} onClick={async () => {
              if (!teamsWebhook.startsWith("https://")) { toast.error("Invalid Teams webhook URL"); return; }
              try {
                await connectTeams.mutateAsync({ webhookUrl: teamsWebhook });
                toast.success("Microsoft Teams connected!");
                setTeamsWebhook("");
                setTeamsWebhookOpen(false);
              } catch (e: any) { toast.error("Failed to connect Teams", { description: e.message }); }
            }}>
              {connectTeams.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin mr-2" />}
              {teamsStatus?.isConnected ? "Reconnect" : "Connect"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── GitHub Dialog ──────────────────────────────────────────── */}
      <Dialog open={githubOpen} onOpenChange={(o) => { setGithubOpen(o); if (!o) { setGithubUsername(""); setGithubPat(""); } }}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{githubStatus?.isConnected ? "Reconnect GitHub" : "Connect GitHub"}</DialogTitle>
            <DialogDescription>Enter your GitHub username and a Personal Access Token with <code>repo</code> scope to enable repository access and deployment triggers.</DialogDescription>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <div className="space-y-1.5">
              <Label className="text-xs">GitHub Username</Label>
              <Input placeholder="octocat" value={githubUsername} onChange={e => setGithubUsername(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">Personal Access Token</Label>
              <div className="relative">
                <Input type={showGithubPat ? "text" : "password"} placeholder="ghp_..." value={githubPat} onChange={e => setGithubPat(e.target.value)} className="pr-10" />
                <button type="button" className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground" onClick={() => setShowGithubPat(!showGithubPat)}>
                  {showGithubPat ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
              <p className="text-xs text-muted-foreground">Create at GitHub → Settings → Developer settings → Personal access tokens. Requires <code className="bg-muted px-1 rounded">repo</code> scope.</p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" size="sm" onClick={() => setGithubOpen(false)}>Cancel</Button>
            <Button size="sm" disabled={connectGitHub.isPending} onClick={async () => {
              if (!githubUsername || !githubPat) { toast.error("Both fields are required"); return; }
              try {
                await connectGitHub.mutateAsync({ username: githubUsername, personalAccessToken: githubPat });
                toast.success("GitHub connected!");
                setGithubOpen(false); setGithubUsername(""); setGithubPat("");
              } catch (e: any) { toast.error("Failed to connect GitHub", { description: e.message }); }
            }}>
              {connectGitHub.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin mr-2" />}
              {githubStatus?.isConnected ? "Reconnect" : "Connect"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── GitLab Dialog ──────────────────────────────────────────── */}
      <Dialog open={gitlabOpen} onOpenChange={(o) => { setGitlabOpen(o); if (!o) { setGitlabUsername(""); setGitlabPat(""); } }}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{gitlabStatus?.isConnected ? "Reconnect GitLab" : "Connect GitLab"}</DialogTitle>
            <DialogDescription>Enter your GitLab username and a Personal Access Token with <code>read_api</code> scope.</DialogDescription>
          </DialogHeader>
          <div className="space-y-3 py-2">
            <div className="space-y-1.5">
              <Label className="text-xs">GitLab Username</Label>
              <Input placeholder="gitlab-user" value={gitlabUsername} onChange={e => setGitlabUsername(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">Personal Access Token</Label>
              <div className="relative">
                <Input type={showGitlabPat ? "text" : "password"} placeholder="glpat-..." value={gitlabPat} onChange={e => setGitlabPat(e.target.value)} className="pr-10" />
                <button type="button" className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground" onClick={() => setShowGitlabPat(!showGitlabPat)}>
                  {showGitlabPat ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
              <p className="text-xs text-muted-foreground">Create at GitLab → User Settings → Access Tokens. Requires <code className="bg-muted px-1 rounded">read_api</code> and <code className="bg-muted px-1 rounded">read_repository</code> scopes.</p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" size="sm" onClick={() => setGitlabOpen(false)}>Cancel</Button>
            <Button size="sm" disabled={connectGitLab.isPending} onClick={async () => {
              if (!gitlabUsername || !gitlabPat) { toast.error("Both fields are required"); return; }
              try {
                await connectGitLab.mutateAsync({ username: gitlabUsername, personalAccessToken: gitlabPat });
                toast.success("GitLab connected!");
                setGitlabOpen(false); setGitlabUsername(""); setGitlabPat("");
              } catch (e: any) { toast.error("Failed to connect GitLab", { description: e.message }); }
            }}>
              {connectGitLab.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin mr-2" />}
              {gitlabStatus?.isConnected ? "Reconnect" : "Connect"}
            </Button>
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
            <Button size="sm" disabled={connectCloudflare.isPending} onClick={async () => {
              if (!cloudflareToken || !cloudflareZone) { toast.error("Both fields are required"); return; }
              try {
                await connectCloudflare.mutateAsync({ apiToken: cloudflareToken, zoneId: cloudflareZone });
                toast.success("Cloudflare connected!");
                setCloudflareToken(""); setCloudflareZone("");
                setCloudflareOpen(false);
              } catch (e: any) { toast.error("Failed to connect Cloudflare", { description: e.message }); }
            }}>
              {connectCloudflare.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin mr-2" />}
              {cloudflareStatus?.isConnected ? "Reconnect" : "Connect"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
