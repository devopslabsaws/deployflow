"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Users, UserPlus, Mail, Shield, MoreVertical,
  Crown, Code2, Eye, Trash2, UserCog,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
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
import { useTeamMembers, useInviteMember, useUpdateMemberRole, useRemoveMember } from "@/hooks/use-api";
import { toast } from "sonner";
import { formatDistanceToNow } from "date-fns";
import type { TeamMember } from "@/types";

const roleConfig = {
  owner: { icon: Crown, color: "text-amber-500", label: "Owner", badge: "warning" as const },
  admin: { icon: Shield, color: "text-blue-500", label: "Admin", badge: "default" as const },
  developer: { icon: Code2, color: "text-violet-500", label: "Developer", badge: "secondary" as const },
  viewer: { icon: Eye, color: "text-slate-500", label: "Viewer", badge: "outline" as const },
};

export default function TeamPage() {
  const [inviteOpen, setInviteOpen] = useState(false);
  const [inviteForm, setInviteForm] = useState({ email: "", name: "", role: "developer" });
  const [removeDialog, setRemoveDialog] = useState<{ open: boolean; memberId: string; memberName: string }>({
    open: false, memberId: "", memberName: "",
  });
  const { data: members, isLoading } = useTeamMembers();
  const invite = useInviteMember();
  const updateRole = useUpdateMemberRole();
  const removeMember = useRemoveMember();

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

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Team</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {members?.length ?? 0} member{(members?.length ?? 0) !== 1 ? "s" : ""}
          </p>
        </div>
        <Button onClick={() => setInviteOpen(true)}>
          <UserPlus className="h-4 w-4 mr-2" />
          Invite Member
        </Button>
      </div>

      <div className="space-y-3">
        {isLoading
          ? Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-20 w-full rounded-lg" />
            ))
          : members?.map((member) => {
            const role = roleConfig[member.role as keyof typeof roleConfig] ?? roleConfig.viewer;
            const RoleIcon = role.icon;
            return (
              <motion.div
                key={member.id}
                initial={{ opacity: 0, y: 4 }}
                animate={{ opacity: 1, y: 0 }}
              >
                <Card>
                  <CardContent className="py-4 flex items-center gap-4">
                    <Avatar className="h-10 w-10">
                      <AvatarImage src={member.user.avatarUrl} />
                      <AvatarFallback>
                        {member.user.name.split(" ").map((n: string) => n[0]).join("").toUpperCase().slice(0, 2)}
                      </AvatarFallback>
                    </Avatar>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
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
                    {member.role !== "admin" && (
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" className="h-8 w-8">
                            <MoreVertical className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          {["admin", "developer", "viewer"].map((r) => (
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
                          <DropdownMenuItem
                            className="text-destructive"
                            onClick={() => handleRemove(member.id, member.user.name)}
                          >
                            <Trash2 className="h-4 w-4 mr-2" />
                            Remove
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    )}
                  </CardContent>
                </Card>
              </motion.div>
            );
          })}
      </div>

      {/* Invite Dialog */}
      <Dialog open={inviteOpen} onOpenChange={setInviteOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Invite Team Member</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label>Full Name</Label>
              <Input
                placeholder="Jane Doe"
                value={inviteForm.name}
                onChange={(e) => setInviteForm((f) => ({ ...f, name: e.target.value }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Email Address</Label>
              <Input
                type="email"
                placeholder="jane@example.com"
                value={inviteForm.email}
                onChange={(e) => setInviteForm((f) => ({ ...f, email: e.target.value }))}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Role</Label>
              <Select
                value={inviteForm.role}
                onValueChange={(v) => setInviteForm((f) => ({ ...f, role: v }))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="admin">Admin</SelectItem>
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

      {/* Remove Confirmation */}
      <AlertDialog open={removeDialog.open} onOpenChange={(open) => !open && setRemoveDialog((d) => ({ ...d, open: false }))}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove team member?</AlertDialogTitle>
            <AlertDialogDescription>
              This will remove <strong>{removeDialog.memberName}</strong> from the team. They will lose access to all projects and resources immediately.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={confirmRemove}
            >
              {removeMember.isPending ? "Removing…" : "Remove"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
