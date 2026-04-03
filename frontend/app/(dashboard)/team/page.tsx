"use client";

import { useEffect, useMemo, useState } from "react";
import { motion } from "framer-motion";
import {
  Ban,
  Boxes,
  Clock3,
  Code2,
  Crown,
  Database,
  Eye,
  Globe,
  HardDrive,
  KeyRound,
  Mail,
  MoreVertical,
  Send,
  Shield,
  Trash2,
  UserCog,
  UserPlus,
} from "lucide-react";
import { formatDistanceToNow } from "date-fns";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Skeleton } from "@/components/ui/skeleton";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  useBulkGrantPermissions,
  useDatabases,
  useDomains,
  useInviteMember,
  usePermissionGrants,
  usePermissionPreview,
  useProjects,
  useRemoveMember,
  useResendTeamInvitation,
  useRevokePermission,
  useRevokeTeamInvitation,
  useServices,
  useTeamInvitations,
  useTeamMembers,
  useUpdateMemberRole,
  useVolumes,
} from "@/hooks/use-api";
import type { PermissionGrant, TeamMember } from "@/types";

const roleConfig = {
  owner: { icon: Crown, label: "Owner", badge: "warning" as const },
  admin: { icon: Shield, label: "Admin", badge: "default" as const },
  devops: { icon: Shield, label: "DevOps", badge: "secondary" as const },
  developer: { icon: Code2, label: "Developer", badge: "secondary" as const },
  viewer: { icon: Eye, label: "Viewer", badge: "outline" as const },
};

const resourceTypeMeta = {
  project: { label: "Project", icon: Boxes },
  service: { label: "Service", icon: KeyRound },
  database: { label: "Database", icon: Database },
  domain: { label: "Domain", icon: Globe },
  volume: { label: "Volume", icon: HardDrive },
};

const permissionActions = ["read", "deploy", "configure", "delete"] as const;

export default function TeamPage() {
  const [inviteOpen, setInviteOpen] = useState(false);
  const [inviteForm, setInviteForm] = useState({ email: "", name: "", role: "developer" });
  const [removeDialog, setRemoveDialog] = useState<{ open: boolean; memberId: string; memberName: string }>({
    open: false, memberId: "", memberName: "",
  });
  const [selectedUserId, setSelectedUserId] = useState("");
  const [selectedResourceType, setSelectedResourceType] = useState<keyof typeof resourceTypeMeta>("project");
  const [selectedResourceId, setSelectedResourceId] = useState("");
  const [draftActions, setDraftActions] = useState<string[]>([]);

  const { data: members, isLoading } = useTeamMembers();
  const { data: invitations, isLoading: invitationsLoading } = useTeamInvitations();
  const { data: projects } = useProjects();
  const { data: services } = useServices();
  const { data: databases } = useDatabases();
  const { data: domains } = useDomains();
  const { data: volumes } = useVolumes();
  const { data: grants } = usePermissionGrants(selectedUserId || undefined);
  const { data: preview } = usePermissionPreview(selectedUserId || undefined, selectedResourceType, selectedResourceId || undefined);

  const invite = useInviteMember();
  const updateRole = useUpdateMemberRole();
  const removeMember = useRemoveMember();
  const resendInvitation = useResendTeamInvitation();
  const revokeInvitation = useRevokeTeamInvitation();
  const bulkGrantPermissions = useBulkGrantPermissions();
  const revokePermission = useRevokePermission();

  const resourceOptions = useMemo(() => {
    switch (selectedResourceType) {
      case "project":
        return (projects?.data ?? []).map((item: any) => ({ id: item.id, name: item.name }));
      case "service":
        return (services ?? []).map((item: any) => ({ id: item.id, name: item.name }));
      case "database":
        return (databases ?? []).map((item: any) => ({ id: item.id, name: item.name }));
      case "domain":
        return (domains ?? []).map((item: any) => ({ id: item.id, name: item.name }));
      case "volume":
        return (volumes ?? []).map((item: any) => ({ id: item.id, name: item.name }));
      default:
        return [] as Array<{ id: string; name: string }>;
    }
  }, [databases, domains, projects, selectedResourceType, services, volumes]);

  const resourceNameByKey = useMemo(() => {
    const map = new Map<string, string>();
    Object.entries({
      project: projects?.data ?? [],
      service: services ?? [],
      database: databases ?? [],
      domain: domains ?? [],
      volume: volumes ?? [],
    }).forEach(([type, items]) => {
      (items as any[]).forEach((item) => map.set(`${type}:${item.id}`, item.name));
    });
    return map;
  }, [databases, domains, projects, services, volumes]);

  useEffect(() => {
    if (!members?.length || selectedUserId) return;
    setSelectedUserId(members[0].id);
  }, [members, selectedUserId]);

  useEffect(() => {
    if (!resourceOptions.length) {
      setSelectedResourceId("");
      return;
    }
    setSelectedResourceId((current) => resourceOptions.some((option) => option.id === current) ? current : resourceOptions[0].id);
  }, [resourceOptions]);

  useEffect(() => {
    if (!selectedResourceId) {
      setDraftActions([]);
      return;
    }
    const existing = (grants ?? []).find(
      (grant) => grant.resourceType === selectedResourceType && grant.resourceId === selectedResourceId,
    );
    setDraftActions(existing?.actions ?? []);
  }, [grants, selectedResourceId, selectedResourceType]);

  const toggleAction = (action: string) => {
    setDraftActions((current) => current.includes(action)
      ? current.filter((item) => item !== action)
      : [...current, action]);
  };

  const handleInvite = async () => {
    if (!inviteForm.email || !inviteForm.name) {
      toast.error("Email and name are required.");
      return;
    }
    try {
      await invite.mutateAsync(inviteForm);
      toast.success(`Invitation sent to ${inviteForm.email}`);
      setInviteOpen(false);
      setInviteForm({ email: "", name: "", role: "developer" });
    } catch (e: any) {
      toast.error("Failed to invite member", { description: e.message });
    }
  };

  const handleUpdateRole = async (userId: string, role: string) => {
    try {
      await updateRole.mutateAsync({ userId, role });
      toast.success("Role updated.");
    } catch (e: any) {
      toast.error("Failed to update role", { description: e.message });
    }
  };

  const handleRemove = (userId: string, name: string) => {
    setRemoveDialog({ open: true, memberId: userId, memberName: name });
  };

  const confirmRemove = async () => {
    try {
      await removeMember.mutateAsync(removeDialog.memberId);
      toast.success(`${removeDialog.memberName} removed from team.`);
    } catch (e: any) {
      toast.error("Failed to remove member", { description: e.message });
    } finally {
      setRemoveDialog({ open: false, memberId: "", memberName: "" });
    }
  };

  const handleResendInvitation = async (invitationId: string, email: string) => {
    try {
      await resendInvitation.mutateAsync(invitationId);
      toast.success(`Invitation resent to ${email}.`);
    } catch (e: any) {
      toast.error("Failed to resend invitation", { description: e.message });
    }
  };

  const handleRevokeInvitation = async (invitationId: string, email: string) => {
    try {
      await revokeInvitation.mutateAsync(invitationId);
      toast.success(`Invitation revoked for ${email}.`);
    } catch (e: any) {
      toast.error("Failed to revoke invitation", { description: e.message });
    }
  };

  const handleApplyPermissions = async () => {
    if (!selectedUserId || !selectedResourceId) {
      toast.error("Select a member and resource first.");
      return;
    }
    try {
      await bulkGrantPermissions.mutateAsync({
        userId: selectedUserId,
        grants: [{
          resourceType: selectedResourceType,
          resourceId: selectedResourceId,
          actions: draftActions,
        }],
      });
      toast.success(draftActions.length === 0
        ? "Explicit deny saved for this resource."
        : "Permissions updated.");
    } catch (e: any) {
      toast.error("Failed to save permissions", { description: e.message });
    }
  };

  const handleRevokePermission = async (grant: PermissionGrant) => {
    try {
      await revokePermission.mutateAsync({
        userId: grant.userId,
        resourceType: grant.resourceType,
        resourceId: grant.resourceId,
      });
      toast.success("Permission grant revoked.");
    } catch (e: any) {
      toast.error("Failed to revoke permission", { description: e.message });
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Team</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {members?.length ?? 0} member{(members?.length ?? 0) !== 1 ? "s" : ""} and {invitations?.length ?? 0} pending invitation{(invitations?.length ?? 0) !== 1 ? "s" : ""}
          </p>
        </div>
        <Button onClick={() => setInviteOpen(true)}>
          <UserPlus className="h-4 w-4 mr-2" />
          Invite Member
        </Button>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-[1.3fr_0.9fr] gap-6">
        <Card>
          <CardHeader>
            <CardTitle>Members</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {isLoading
              ? Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-20 w-full rounded-lg" />)
              : members?.map((member) => {
                const role = roleConfig[member.role as keyof typeof roleConfig] ?? roleConfig.viewer;
                const RoleIcon = role.icon;
                return (
                  <motion.div key={member.id} initial={{ opacity: 0, y: 4 }} animate={{ opacity: 1, y: 0 }}>
                    <Card>
                      <CardContent className="py-4 flex items-center gap-4">
                        <Avatar className="h-10 w-10">
                          <AvatarImage src={member.user.avatarUrl} />
                          <AvatarFallback>
                            {member.user.name.split(" ").map((n: string) => n[0]).join("").toUpperCase().slice(0, 2)}
                          </AvatarFallback>
                        </Avatar>
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-2 flex-wrap">
                            <span className="font-medium">{member.user.name}</span>
                            <Badge variant={role.badge}>
                              <RoleIcon className="h-3 w-3 mr-1" />
                              {role.label}
                            </Badge>
                            {!member.user.isActive && <Badge variant="outline">Inactive</Badge>}
                          </div>
                          <p className="text-sm text-muted-foreground">{member.user.email}</p>
                          {member.user.lastLoginAt && (
                            <p className="text-xs text-muted-foreground mt-0.5">
                              Last login {formatDistanceToNow(new Date(member.user.lastLoginAt), { addSuffix: true })}
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
                            {["admin", "devops", "developer", "viewer"].map((r) => (
                              <DropdownMenuItem
                                key={r}
                                onClick={() => handleUpdateRole(member.id, r)}
                                disabled={member.role === r}
                              >
                                <UserCog className="h-4 w-4 mr-2" />
                                Set as {r.charAt(0).toUpperCase() + r.slice(1)}
                              </DropdownMenuItem>
                            ))}
                            <DropdownMenuSeparator />
                            <DropdownMenuItem className="text-destructive" onClick={() => handleRemove(member.id, member.user.name)}>
                              <Trash2 className="h-4 w-4 mr-2" />
                              Remove
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </CardContent>
                    </Card>
                  </motion.div>
                );
              })}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Pending Invitations</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {invitationsLoading
              ? Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-16 w-full rounded-lg" />)
              : !invitations?.length
                ? <p className="text-sm text-muted-foreground">No pending invitations.</p>
                : invitations.map((invitation) => (
                    <div key={invitation.id} className="rounded-lg border border-border/60 p-3 space-y-2">
                      <div className="flex items-center justify-between gap-2">
                        <div>
                          <p className="text-sm font-medium">{invitation.name}</p>
                          <p className="text-xs text-muted-foreground">{invitation.email}</p>
                        </div>
                        <div className="flex items-center gap-2">
                          <Badge variant={invitation.status === "expired" ? "destructive" : "outline"}>
                            {invitation.status === "expired" ? "Expired" : "Pending"}
                          </Badge>
                          <Badge variant="secondary">{invitation.role}</Badge>
                        </div>
                      </div>
                      <div className="grid grid-cols-2 gap-2 text-[11px] text-muted-foreground">
                        <span className="flex items-center gap-1"><Clock3 className="h-3 w-3" />Sent {formatDistanceToNow(new Date(invitation.lastSentAt), { addSuffix: true })}</span>
                        <span>By {invitation.invitedByName}</span>
                        <span>Expires {formatDistanceToNow(new Date(invitation.expiresAt), { addSuffix: true })}</span>
                        <span>{invitation.resendCount} resend{invitation.resendCount !== 1 ? "s" : ""}</span>
                      </div>
                      <div className="flex gap-2">
                        <Button variant="outline" size="sm" onClick={() => handleResendInvitation(invitation.id, invitation.email)} disabled={resendInvitation.isPending}>
                          <Send className="h-3.5 w-3.5 mr-1.5" />Resend
                        </Button>
                        <Button variant="ghost" size="sm" className="text-destructive" onClick={() => handleRevokeInvitation(invitation.id, invitation.email)} disabled={revokeInvitation.isPending}>
                          <Ban className="h-3.5 w-3.5 mr-1.5" />Revoke
                        </Button>
                      </div>
                    </div>
                  ))}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Resource Permissions</CardTitle>
          <p className="text-sm text-muted-foreground">
            Configure explicit resource grants. An empty action set acts as an explicit deny override against the member&apos;s role defaults.
          </p>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-4">
            <div className="space-y-1.5">
              <Label>Member</Label>
              <Select value={selectedUserId} onValueChange={setSelectedUserId}>
                <SelectTrigger><SelectValue placeholder="Select a member" /></SelectTrigger>
                <SelectContent>
                  {(members ?? []).map((member) => (
                    <SelectItem key={member.id} value={member.id}>{member.user.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>Resource Type</Label>
              <Select value={selectedResourceType} onValueChange={(value) => setSelectedResourceType(value as keyof typeof resourceTypeMeta)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {Object.entries(resourceTypeMeta).map(([value, meta]) => (
                    <SelectItem key={value} value={value}>{meta.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5 md:col-span-1 xl:col-span-2">
              <Label>Resource</Label>
              <Select value={selectedResourceId} onValueChange={setSelectedResourceId}>
                <SelectTrigger><SelectValue placeholder="Select a resource" /></SelectTrigger>
                <SelectContent>
                  {resourceOptions.map((resource) => (
                    <SelectItem key={resource.id} value={resource.id}>{resource.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            {permissionActions.map((action) => (
              <label key={action} className="flex items-center gap-2 rounded-lg border border-border/60 px-3 py-2 text-sm cursor-pointer">
                <Checkbox checked={draftActions.includes(action)} onCheckedChange={() => toggleAction(action)} />
                <span className="capitalize">{action}</span>
              </label>
            ))}
          </div>

          <div className="flex flex-wrap gap-2 items-center">
            <Button onClick={handleApplyPermissions} disabled={bulkGrantPermissions.isPending || !selectedUserId || !selectedResourceId}>
              <KeyRound className="h-4 w-4 mr-2" />
              {bulkGrantPermissions.isPending ? "Saving..." : "Apply Grant"}
            </Button>
            {preview && (
              <div className="flex flex-wrap gap-2">
                <span className="text-sm text-muted-foreground mr-1">Effective:</span>
                {preview.effectiveActions.length > 0 ? preview.effectiveActions.map((action) => (
                  <Badge key={action} variant="secondary">{action}</Badge>
                )) : <Badge variant="destructive">none</Badge>}
              </div>
            )}
          </div>

          <div className="space-y-3">
            <h3 className="text-sm font-medium">Current Explicit Grants</h3>
            {!selectedUserId ? (
              <p className="text-sm text-muted-foreground">Select a member to inspect grants.</p>
            ) : !(grants?.length) ? (
              <p className="text-sm text-muted-foreground">No explicit grants saved yet.</p>
            ) : grants.map((grant) => {
              const meta = resourceTypeMeta[grant.resourceType as keyof typeof resourceTypeMeta];
              const Icon = meta?.icon ?? KeyRound;
              const label = resourceNameByKey.get(`${grant.resourceType}:${grant.resourceId}`) ?? grant.resourceId;
              return (
                <div key={grant.id} className="rounded-lg border border-border/60 p-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <Badge variant="outline"><Icon className="h-3 w-3 mr-1" />{meta?.label ?? grant.resourceType}</Badge>
                      <span className="font-medium truncate">{label}</span>
                    </div>
                    <div className="flex flex-wrap gap-2 mt-2">
                      {grant.actions.length > 0 ? grant.actions.map((action) => (
                        <Badge key={action} variant="secondary">{action}</Badge>
                      )) : <Badge variant="destructive">explicit deny</Badge>}
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={() => {
                      setSelectedResourceType(grant.resourceType as keyof typeof resourceTypeMeta);
                      setSelectedResourceId(grant.resourceId);
                      setDraftActions(grant.actions);
                    }}>
                      Edit
                    </Button>
                    <Button variant="ghost" size="sm" className="text-destructive" onClick={() => handleRevokePermission(grant)} disabled={revokePermission.isPending}>
                      <Trash2 className="h-3.5 w-3.5 mr-1.5" />Revoke
                    </Button>
                  </div>
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      <Dialog open={inviteOpen} onOpenChange={setInviteOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Invite Team Member</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Full Name</Label>
              <Input placeholder="Jane Doe" value={inviteForm.name} onChange={(e) => setInviteForm((f) => ({ ...f, name: e.target.value }))} />
            </div>
            <div className="space-y-1.5">
              <Label>Email Address</Label>
              <Input type="email" placeholder="jane@example.com" value={inviteForm.email} onChange={(e) => setInviteForm((f) => ({ ...f, email: e.target.value }))} />
            </div>
            <div className="space-y-1.5">
              <Label>Role</Label>
              <Select value={inviteForm.role} onValueChange={(v) => setInviteForm((f) => ({ ...f, role: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="admin">Admin</SelectItem>
                  <SelectItem value="devops">DevOps</SelectItem>
                  <SelectItem value="developer">Developer</SelectItem>
                  <SelectItem value="viewer">Viewer</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setInviteOpen(false)}>Cancel</Button>
            <Button onClick={handleInvite} disabled={invite.isPending}>
              <Mail className="h-4 w-4 mr-2" />
              {invite.isPending ? "Sending..." : "Send Invitation"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AlertDialog open={removeDialog.open} onOpenChange={(open) => !open && setRemoveDialog((current) => ({ ...current, open: false }))}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove team member?</AlertDialogTitle>
            <AlertDialogDescription>
              This will remove <strong>{removeDialog.memberName}</strong> from the team. They will lose access to all projects and resources immediately.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction className="bg-destructive text-destructive-foreground hover:bg-destructive/90" onClick={confirmRemove}>
              {removeMember.isPending ? "Removing..." : "Remove"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
