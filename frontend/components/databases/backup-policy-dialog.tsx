"use client";

import { useState, useEffect } from "react";
import { Loader2, AlertCircle } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Card, CardContent } from "@/components/ui/card";
import {
  useBackupPolicy,
  useUpdateBackupPolicy,
  useS3Destinations,
} from "@/hooks/use-api";
import { BackupPolicy } from "@/types";

interface BackupPolicyDialogProps {
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  databaseId: string;
}

export function BackupPolicyDialog({
  isOpen,
  onOpenChange,
  databaseId,
}: BackupPolicyDialogProps) {
  const [form, setForm] = useState({
    isEnabled: false,
    cronExpression: "0 2 * * *",
    retentionDays: 30,
    s3DestinationId: "",
    storageLocation: "local" as const,
  });

  const { data: policy, isLoading } = useBackupPolicy(databaseId);
  const { data: s3Destinations } = useS3Destinations();
  const updateMutation = useUpdateBackupPolicy();

  useEffect(() => {
    if (policy) {
      setForm({
        isEnabled: policy.isEnabled,
        cronExpression: policy.cronExpression,
        retentionDays: policy.retentionDays,
        s3DestinationId: policy.s3DestinationId || "",
        storageLocation: policy.storageLocation,
      });
    }
  }, [policy]);

  const handleSave = async () => {
    try {
      await updateMutation.mutateAsync({
        databaseId,
        ...form,
      });
      onOpenChange(false);
    } catch (error: any) {
      console.error("Failed to update backup policy:", error);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Backup Policy Settings</DialogTitle>
          <DialogDescription>
            Configure automated backup schedule and retention for this database
          </DialogDescription>
        </DialogHeader>

        {isLoading ? (
          <div className="flex items-center justify-center py-8">
            <Loader2 className="w-5 h-5 animate-spin text-muted-foreground" />
          </div>
        ) : (
          <div className="space-y-5">
            {/* Enable backups */}
            <div className="flex items-center justify-between rounded-lg border p-3">
              <div>
                <p className="text-sm font-medium">Enable Automatic Backups</p>
                <p className="text-xs text-muted-foreground">
                  Schedule regular database backups
                </p>
              </div>
              <Switch
                checked={form.isEnabled}
                onCheckedChange={(v) =>
                  setForm((f) => ({ ...f, isEnabled: v }))
                }
              />
            </div>

            {form.isEnabled && (
              <>
                <hr />

                {/* Storage location */}
                <div className="space-y-1.5">
                  <Label>Storage Location</Label>
                  <Select
                    value={form.storageLocation}
                    onValueChange={(value) =>
                      setForm((f) => ({
                        ...f,
                        storageLocation: value as "local" | "s3",
                      }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="local">
                        Local Storage (Server Disk)
                      </SelectItem>
                      <SelectItem value="s3">S3-Compatible (Cloud)</SelectItem>
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-muted-foreground mt-1">
                    {form.storageLocation === "local"
                      ? "Backups stored on the server. No external credentials required."
                      : "Backups uploaded to S3-compatible storage. Select a destination below."}
                  </p>
                </div>

                {/* S3 destination (shown when S3 is selected) */}
                {form.storageLocation === "s3" && (
                  <div className="space-y-1.5">
                    <Label>S3 Destination</Label>
                    {!s3Destinations || s3Destinations.length === 0 ? (
                      <Card className="border-amber-200 bg-amber-50">
                        <CardContent className="pt-3 text-sm text-amber-900 flex items-start gap-2">
                          <AlertCircle className="w-4 h-4 flex-shrink-0 mt-0.5" />
                          <div>
                            No S3 destinations configured. Create one in{" "}
                            <a
                              href="/s3-destinations"
                              className="font-medium underline"
                            >
                              S3 Destinations
                            </a>{" "}
                            first.
                          </div>
                        </CardContent>
                      </Card>
                    ) : (
                      <Select
                        value={form.s3DestinationId}
                        onValueChange={(v) =>
                          setForm((f) => ({ ...f, s3DestinationId: v }))
                        }
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select S3 destination" />
                        </SelectTrigger>
                        <SelectContent>
                          {s3Destinations.map((dest) => (
                            <SelectItem key={dest.id} value={dest.id}>
                              {dest.name}
                              {dest.isDefault && " (default)"}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  </div>
                )}

                {/* Cron expression */}
                <div className="space-y-1.5">
                  <Label htmlFor="cron">
                    Schedule (Cron Expression)
                  </Label>
                  <Input
                    id="cron"
                    value={form.cronExpression}
                    onChange={(e) =>
                      setForm((f) => ({ ...f, cronExpression: e.target.value }))
                    }
                    placeholder="0 2 * * *"
                  />
                  <div className="text-xs text-muted-foreground space-y-1">
                    <p>Examples:</p>
                    <ul className="list-disc list-inside space-y-0.5">
                      <li>
                        <code className="bg-muted px-1 rounded">0 2 * * *</code>{" "}
                        = Every day at 2:00 AM
                      </li>
                      <li>
                        <code className="bg-muted px-1 rounded">0 0 * * 0</code>{" "}
                        = Every Sunday at midnight
                      </li>
                      <li>
                        <code className="bg-muted px-1 rounded">0 */6 * * *</code>{" "}
                        = Every 6 hours
                      </li>
                    </ul>
                  </div>
                </div>

                {/* Retention days */}
                <div className="space-y-1.5">
                  <Label htmlFor="retention">
                    Retention Period (days)
                  </Label>
                  <Input
                    id="retention"
                    type="number"
                    min="1"
                    max="365"
                    value={form.retentionDays}
                    onChange={(e) =>
                      setForm((f) => ({
                        ...f,
                        retentionDays: Math.max(1, parseInt(e.target.value) || 1),
                      }))
                    }
                  />
                  <p className="text-xs text-muted-foreground">
                    Backups older than {form.retentionDays} days will be automatically deleted
                  </p>
                </div>
              </>
            )}
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            disabled={updateMutation.isPending || isLoading}
          >
            {updateMutation.isPending && (
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
            )}
            Save Policy
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
