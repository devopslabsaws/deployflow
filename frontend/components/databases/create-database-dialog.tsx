"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Loader2 } from "lucide-react";
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
import { useCreateDatabase } from "@/hooks/use-api";
import { toast } from "sonner";

const schema = z.object({
  name: z.string().min(2).max(50),
  type: z.enum(["postgresql", "mysql", "mariadb", "mongodb", "redis", "mssql", "oracle"]),
  version: z.string().default("latest"),
  databaseName: z.string().min(1).default("app"),
  username: z.string().min(1).default("admin"),
  storageGB: z.coerce.number().min(1).default(10),
  backupEnabled: z.boolean().default(true),
});

type FormData = z.infer<typeof schema>;

interface CreateDatabaseDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const dbVersions: Record<string, string[]> = {
  postgresql: ["16", "15", "14", "13"],
  mysql: ["8.0", "5.7"],
  mariadb: ["11.2", "10.11"],
  mongodb: ["7.0", "6.0"],
  redis: ["7.2", "7.0"],
  mssql: ["2022", "2019"],
  oracle: ["21c", "19c", "12c"],
};

export function CreateDatabaseDialog({ open, onOpenChange }: CreateDatabaseDialogProps) {
  const create = useCreateDatabase();
  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { type: "postgresql", backupEnabled: true, storageGB: 10, version: "16", databaseName: "app", username: "admin" },
  });

  const type = watch("type");
  const backupEnabled = watch("backupEnabled");

  const onSubmit = async (data: FormData) => {
    try {
      await create.mutateAsync({
        name: data.name,
        engine: data.type,
        version: data.version,
        databaseName: data.databaseName,
        username: data.username,
        storageGb: data.storageGB,
        autoBackup: data.backupEnabled,
      } as any);
      toast.success(`Database "${data.name}" is being created!`);
      reset();
      onOpenChange(false);
    } catch (e: any) {
      toast.error("Failed to create database", { description: e.message });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Create Database</DialogTitle>
          <DialogDescription>
            Deploy a managed database with automated backups and monitoring.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="space-y-2">
            <Label>Database Name *</Label>
            <Input placeholder="my-postgres-db" {...register("name")} />
            {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Engine</Label>
              <Select value={type} onValueChange={(v) => { setValue("type", v as any); setValue("version", dbVersions[v]?.[0] ?? "latest"); }}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="postgresql">PostgreSQL</SelectItem>
                  <SelectItem value="mysql">MySQL</SelectItem>
                  <SelectItem value="mariadb">MariaDB</SelectItem>
                  <SelectItem value="mongodb">MongoDB</SelectItem>
                  <SelectItem value="redis">Redis</SelectItem>
                  <SelectItem value="mssql">SQL Server</SelectItem>
                  <SelectItem value="oracle">Oracle DB</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Version</Label>
              <Select value={watch("version")} onValueChange={(v) => setValue("version", v)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(dbVersions[type] ?? ["latest"]).map((v) => (
                    <SelectItem key={v} value={v}>{v}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          {type !== "redis" && (
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label>Database</Label>
                <Input placeholder="app" {...register("databaseName")} />
              </div>
              <div className="space-y-2">
                <Label>Username</Label>
                <Input placeholder="admin" {...register("username")} />
              </div>
            </div>
          )}

          <div className="space-y-2">
            <Label>Storage (GB)</Label>
            <Input type="number" {...register("storageGB")} />
          </div>

          <div className="flex items-center justify-between rounded-lg border p-3">
            <div>
              <p className="text-sm font-medium">Automatic Backups</p>
              <p className="text-xs text-muted-foreground">Daily backups retained for 7 days</p>
            </div>
            <Switch checked={backupEnabled} onCheckedChange={(v) => setValue("backupEnabled", v)} />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" disabled={isSubmitting || create.isPending}>
              {(isSubmitting || create.isPending) && <Loader2 className="mr-2 w-4 h-4 animate-spin" />}
              Create Database
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
