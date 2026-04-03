"use client";

import { useState } from "react";
import { Cloud, Plus, Loader2, CheckCircle2, XCircle, AlertCircle, Edit, Trash2 } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  useS3Destinations,
  useCreateS3Destination,
  useUpdateS3Destination,
  useDeleteS3Destination,
  useTestS3Destination,
} from "@/hooks/use-api";
import { S3Destination, S3DestinationStatus } from "@/types";
import { toast } from "sonner";

interface S3FormData {
  name: string;
  description?: string;
  endpoint: string;
  bucketName: string;
  region?: string;
  accessKeyId: string;
  secretAccessKey: string;
  isDefault: boolean;
}

const DEFAULT_FORM: S3FormData = {
  name: "",
  description: "",
  endpoint: "",
  bucketName: "",
  region: "",
  accessKeyId: "",
  secretAccessKey: "",
  isDefault: false,
};

export default function S3DestinationsPage() {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<S3FormData>(DEFAULT_FORM);
  const [testResult, setTestResult] = useState<{ isConnected: boolean; message: string } | null>(null);
  const [isTesting, setIsTesting] = useState(false);
  const [testError, setTestError] = useState<string | null>(null);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  const { data: destinations, isLoading } = useS3Destinations();
  const createMutation = useCreateS3Destination();
  const updateMutation = useUpdateS3Destination();
  const deleteMutation = useDeleteS3Destination();
  const testMutation = useTestS3Destination();

  const handleOpenCreate = () => {
    setEditingId(null);
    setForm(DEFAULT_FORM);
    setTestResult(null);
    setTestError(null);
    setIsDialogOpen(true);
  };

  const handleOpenEdit = (destination: S3Destination) => {
    setEditingId(destination.id);
    setForm({
      name: destination.name,
      description: destination.description,
      endpoint: destination.endpoint,
      bucketName: destination.bucketName,
      region: destination.region,
      accessKeyId: "",
      secretAccessKey: "",
      isDefault: destination.isDefault,
    });
    setTestResult(null);
    setTestError(null);
    setIsDialogOpen(true);
  };

  const handleSave = async () => {
    try {
      if (editingId) {
        await updateMutation.mutateAsync({ id: editingId, ...form });
      } else {
        await createMutation.mutateAsync(form);
      }
      setIsDialogOpen(false);
      setForm(DEFAULT_FORM);
      setTestResult(null);
    } catch (error: any) {
      setTestError(error.message || "Failed to save S3 destination");
    }
  };

  const handleTest = async () => {
    setIsTesting(true);
    setTestError(null);
    setTestResult(null);

    try {
      const result = await testMutation.mutateAsync({
        endpoint: form.endpoint,
        bucketName: form.bucketName,
        region: form.region,
        accessKeyId: form.accessKeyId,
        secretAccessKey: form.secretAccessKey,
      });
      setTestResult(result);
    } catch (error: any) {
      setTestError(error.message || "Connection test failed");
    } finally {
      setIsTesting(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteMutation.mutateAsync(id);
      toast.success("S3 destination deleted.");
      setDeleteConfirmId(null);
    } catch (error: any) {
      toast.error("Failed to delete S3 destination", {
        description: error?.message ?? "Unknown error",
      });
    }
  };

  const getStatusColor = (status: S3DestinationStatus) => {
    switch (status) {
      case "active":
        return "text-emerald-600 bg-emerald-50";
      case "error":
        return "text-red-600 bg-red-50";
      default:
        return "text-amber-600 bg-amber-50";
    }
  };

  const getStatusLabel = (status: S3DestinationStatus) => {
    switch (status) {
      case "active":
        return "Active";
      case "error":
        return "Error";
      default:
        return "Unconfigured";
    }
  };

  return (
    <div className="space-y-6 max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight flex items-center gap-2">
            <Cloud className="w-6 h-6" />
            S3 Destinations
          </h1>
          <p className="text-muted-foreground text-sm mt-1">
            Manage S3-compatible storage destinations for database backups
          </p>
        </div>
        <Button onClick={handleOpenCreate}>
          <Plus className="w-4 h-4 mr-2" />
          Add Destination
        </Button>
      </div>

      {isLoading ? (
        <Card className="glass-card">
          <CardContent className="flex items-center justify-center py-8">
            <Loader2 className="w-5 h-5 animate-spin text-muted-foreground" />
          </CardContent>
        </Card>
      ) : destinations?.length === 0 ? (
        <Card className="glass-card">
          <CardContent className="flex flex-col items-center justify-center py-12 text-center">
            <Cloud className="w-12 h-12 text-muted-foreground mb-4 opacity-50" />
            <h3 className="font-semibold text-lg">No S3 destinations yet</h3>
            <p className="text-sm text-muted-foreground mt-1 mb-4">
              Create your first S3 destination to enable database backups
            </p>
            <Button onClick={handleOpenCreate}>
              <Plus className="w-4 h-4 mr-2" />
              Create Destination
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-3">
          {destinations?.map((destination) => (
            <Card key={destination.id} className="glass-card">
              <CardContent className="py-4">
                <div className="flex items-start justify-between">
                  <div className="flex-1 space-y-2">
                    <div className="flex items-baseline gap-3">
                      <h3 className="font-semibold">{destination.name}</h3>
                      {destination.isDefault && (
                        <span className="text-xs font-medium px-2 py-0.5 bg-blue-100 text-blue-700 rounded">
                          Default
                        </span>
                      )}
                      <span className={`text-xs font-medium px-2 py-0.5 rounded ${getStatusColor(destination.status)}`}>
                        {getStatusLabel(destination.status)}
                      </span>
                    </div>
                    {destination.description && (
                      <p className="text-sm text-muted-foreground">{destination.description}</p>
                    )}
                    <div className="grid grid-cols-2 gap-4 text-xs text-muted-foreground pt-2">
                      <div>
                        <span className="font-medium">Endpoint:</span> {destination.endpoint}
                      </div>
                      <div>
                        <span className="font-medium">Bucket:</span> {destination.bucketName}
                      </div>
                      {destination.region && (
                        <div>
                          <span className="font-medium">Region:</span> {destination.region}
                        </div>
                      )}
                      {destination.lastTestedAt && (
                        <div>
                          <span className="font-medium">Last tested:</span>{" "}
                          {new Date(destination.lastTestedAt).toLocaleString()}
                        </div>
                      )}
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => handleOpenEdit(destination)}
                    >
                      <Edit className="w-4 h-4" />
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      className="text-destructive hover:text-destructive"
                      onClick={() => setDeleteConfirmId(destination.id)}
                    >
                      <Trash2 className="w-4 h-4" />
                    </Button>
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* Create/Edit Dialog */}
      <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{editingId ? "Edit S3 Destination" : "Create S3 Destination"}</DialogTitle>
            <DialogDescription>
              Configure your S3-compatible storage destination for database backups
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Name *</Label>
                <Input
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  placeholder="Primary Backup"
                />
              </div>
              <div className="space-y-1.5">
                <Label>Region</Label>
                <Input
                  value={form.region || ""}
                  onChange={(e) => setForm((f) => ({ ...f, region: e.target.value }))}
                  placeholder="us-east-1"
                />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label>Description</Label>
              <Input
                value={form.description || ""}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                placeholder="Optional description"
              />
            </div>

            <div className="space-y-1.5">
              <Label>Endpoint *</Label>
              <Input
                value={form.endpoint}
                onChange={(e) => setForm((f) => ({ ...f, endpoint: e.target.value }))}
                placeholder="https://s3.us-east-1.amazonaws.com"
              />
            </div>

            <div className="space-y-1.5">
              <Label>Bucket Name *</Label>
              <Input
                value={form.bucketName}
                onChange={(e) => setForm((f) => ({ ...f, bucketName: e.target.value }))}
                placeholder="my-backup-bucket"
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label>Access Key ID *</Label>
                <Input
                  value={form.accessKeyId}
                  onChange={(e) => setForm((f) => ({ ...f, accessKeyId: e.target.value }))}
                  placeholder="AKIA..."
                />
              </div>
              <div className="space-y-1.5">
                <Label>Secret Access Key *</Label>
                <Input
                  type="password"
                  value={form.secretAccessKey}
                  onChange={(e) => setForm((f) => ({ ...f, secretAccessKey: e.target.value }))}
                  placeholder="••••••••"
                />
              </div>
            </div>

            <div className="flex items-center justify-between rounded-lg border p-3">
              <div>
                <p className="text-sm font-medium">Set as Default</p>
                <p className="text-xs text-muted-foreground">Use this for new backup policies</p>
              </div>
              <Switch
                checked={form.isDefault}
                onCheckedChange={(v) => setForm((f) => ({ ...f, isDefault: v }))}
              />
            </div>

            {testResult && (
              <Card className="border-emerald-200 bg-emerald-50">
                <CardContent className="pt-4 flex items-start gap-3 text-sm">
                  <CheckCircle2 className="w-5 h-5 text-emerald-600 flex-shrink-0 mt-0.5" />
                  <div>
                    <p className="font-medium text-emerald-900">{testResult.message}</p>
                  </div>
                </CardContent>
              </Card>
            )}

            {testError && (
              <Card className="border-red-200 bg-red-50">
                <CardContent className="pt-4 flex items-start gap-3 text-sm">
                  <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
                  <p className="text-red-900">{testError}</p>
                </CardContent>
              </Card>
            )}

            <Button
              variant="outline"
              onClick={handleTest}
              disabled={isTesting || !form.bucketName || !form.accessKeyId || !form.secretAccessKey}
              className="w-full"
            >
              {isTesting ? <Loader2 className="w-4 h-4 mr-2 animate-spin" /> : <Cloud className="w-4 h-4 mr-2" />}
              Test Connection
            </Button>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setIsDialogOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={handleSave}
              disabled={createMutation.isPending || updateMutation.isPending || !form.name || !form.endpoint || !form.bucketName}
            >
              {createMutation.isPending || updateMutation.isPending ? (
                <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              ) : null}
              {editingId ? "Update" : "Create"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <Dialog open={!!deleteConfirmId} onOpenChange={(open) => !open && setDeleteConfirmId(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete S3 Destination?</DialogTitle>
            <DialogDescription>
              This action cannot be undone. The S3 destination will be removed and cannot be used for future backups.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteConfirmId(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => deleteConfirmId && handleDelete(deleteConfirmId)}
              disabled={deleteMutation.isPending}
            >
              {deleteMutation.isPending && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}
              Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
