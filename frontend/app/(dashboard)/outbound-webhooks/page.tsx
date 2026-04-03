"use client";

import { useState } from "react";
import {
  Webhook,
  Plus,
  Trash2,
  Zap,
  CheckCircle2,
  XCircle,
  Clock,
  Globe,
  AlertTriangle,
  Copy,
  Check,
  Loader2,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Checkbox } from "@/components/ui/checkbox";
import { ConfirmActionDialog } from "@/components/ui/confirm-action-dialog";
import {
  useOutboundWebhooks,
  useCreateOutboundWebhook,
  useDeleteOutboundWebhook,
  useTestOutboundWebhook,
  type OutboundWebhookDto,
} from "@/hooks/use-api";
import { useProjects } from "@/hooks/use-api";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import { formatDistanceToNow } from "date-fns";
import { cn } from "@/lib/utils";

const ALL_EVENTS = [
  { id: "deploy.started", label: "Deploy Started" },
  { id: "deploy.success", label: "Deploy Success" },
  { id: "deploy.failed", label: "Deploy Failed" },
  { id: "deploy.cancelled", label: "Deploy Cancelled" },
  { id: "preview.created", label: "Preview Created" },
  { id: "preview.closed", label: "Preview Closed" },
  { id: "rollback.triggered", label: "Rollback Triggered" },
];

function parseEvents(events: string): string[] {
  try {
    return JSON.parse(events);
  } catch {
    return [];
  }
}

function WebhookCard({
  hook,
  onDelete,
  onTest,
  testing,
}: {
  hook: OutboundWebhookDto;
  onDelete: () => void;
  onTest: () => void;
  testing: boolean;
}) {
  const [urlCopied, setUrlCopied] = useState(false);
  const events = parseEvents(hook.events);
  const successRate =
    hook.deliveryCount > 0
      ? Math.round(((hook.deliveryCount - hook.failureCount) / hook.deliveryCount) * 100)
      : null;

  return (
    <Card className="hover:border-primary/40 transition-colors">
      <CardContent className="pt-5 pb-4 space-y-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="font-semibold text-sm">{hook.name}</span>
              <Badge variant={hook.isEnabled ? "default" : "secondary"} className="text-xs">
                {hook.isEnabled ? "Active" : "Disabled"}
              </Badge>
              {hook.lastResponseStatus?.startsWith("2") === false && hook.deliveryCount > 0 && (
                <Badge variant="outline" className="text-xs gap-1 text-amber-600 border-amber-400">
                  <AlertTriangle className="w-2.5 h-2.5" /> last failed
                </Badge>
              )}
            </div>
            <div className="flex items-center gap-1.5 mt-1">
              <Globe className="w-3 h-3 text-muted-foreground" />
              <code className="text-xs text-muted-foreground truncate max-w-64">{hook.url}</code>
              <Button
                size="icon"
                variant="ghost"
                className="w-5 h-5"
                onClick={async () => {
                  await navigator.clipboard.writeText(hook.url);
                  setUrlCopied(true);
                  setTimeout(() => setUrlCopied(false), 2000);
                }}
              >
                {urlCopied ? (
                  <Check className="w-3 h-3 text-green-500" />
                ) : (
                  <Copy className="w-3 h-3" />
                )}
              </Button>
            </div>
          </div>
          <div className="flex items-center gap-1 shrink-0">
            <Button
              size="sm"
              variant="outline"
              className="h-7 text-xs gap-1"
              onClick={onTest}
              disabled={testing}
            >
              {testing ? (
                <Loader2 className="w-3 h-3 animate-spin" />
              ) : (
                <Zap className="w-3 h-3" />
              )}
              Test
            </Button>
            <Button
              size="icon"
              variant="ghost"
              className="w-7 h-7 text-destructive hover:bg-destructive/10"
              onClick={onDelete}
            >
              <Trash2 className="w-3.5 h-3.5" />
            </Button>
          </div>
        </div>

        {/* Events */}
        <div className="flex flex-wrap gap-1.5">
          {events.map((e) => (
            <Badge key={e} variant="secondary" className="text-xs font-mono px-1.5 py-0">
              {e}
            </Badge>
          ))}
        </div>

        {/* Stats */}
        <div className="flex items-center gap-4 text-xs text-muted-foreground pt-1 border-t">
          <span className="flex items-center gap-1">
            <CheckCircle2 className="w-3 h-3 text-green-500" />
            {hook.deliveryCount - hook.failureCount} delivered
          </span>
          {hook.failureCount > 0 && (
            <span className="flex items-center gap-1">
              <XCircle className="w-3 h-3 text-red-500" />
              {hook.failureCount} failed
            </span>
          )}
          {successRate !== null && (
            <span
              className={cn(
                successRate >= 90 ? "text-green-600" : successRate >= 60 ? "text-yellow-600" : "text-red-600"
              )}
            >
              {successRate}% success rate
            </span>
          )}
          {hook.lastDeliveredAt && (
            <span className="ml-auto flex items-center gap-1">
              <Clock className="w-3 h-3" />
              {formatDistanceToNow(new Date(hook.lastDeliveredAt), { addSuffix: true })}
            </span>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function CreateWebhookDialog({
  open,
  onOpenChange,
  projectList,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  projectList: Array<{ id: string; name: string }>;
}) {
  const create = useCreateOutboundWebhook();
  const [name, setName] = useState("");
  const [url, setUrl] = useState("");
  const [secret, setSecret] = useState("");
  const [projectId, setProjectId] = useState("_global");
  const [selectedEvents, setSelectedEvents] = useState<string[]>([
    "deploy.success",
    "deploy.failed",
  ]);

  function toggleEvent(id: string) {
    setSelectedEvents((prev) =>
      prev.includes(id) ? prev.filter((e) => e !== id) : [...prev, id]
    );
  }

  function handleCreate() {
    if (!name.trim() || !url.trim() || selectedEvents.length === 0) return;
    create.mutate(
      {
        name,
        url,
        events: JSON.stringify(selectedEvents),
        secret: secret || undefined,
        projectId: projectId === "_global" ? undefined : projectId,
      },
      {
        onSuccess: () => {
          onOpenChange(false);
          setName(""); setUrl(""); setSecret(""); setProjectId("_global");
          setSelectedEvents(["deploy.success", "deploy.failed"]);
          toast.success("Webhook created");
        },
        onError: () => toast.error("Failed to create webhook"),
      }
    );
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Create Outbound Webhook</DialogTitle>
          <DialogDescription>
            DeployFlow will POST to this URL when events fire. Optionally sign payloads with HMAC-SHA256.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div className="space-y-2">
            <Label>Name</Label>
            <Input
              placeholder="Slack Notifications"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label>Endpoint URL</Label>
            <Input
              type="url"
              placeholder="https://hooks.slack.com/services/..."
              value={url}
              onChange={(e) => setUrl(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label>
              Signing Secret{" "}
              <span className="text-muted-foreground font-normal text-xs">(optional, for HMAC-SHA256)</span>
            </Label>
            <Input
              type="password"
              placeholder="whsec_..."
              value={secret}
              onChange={(e) => setSecret(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label>Scope</Label>
            <Select value={projectId} onValueChange={setProjectId}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="_global">All projects (global)</SelectItem>
                {projectList.map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-3">
            <Label>Events</Label>
            <div className="grid grid-cols-2 gap-2">
              {ALL_EVENTS.map((e) => (
                <label key={e.id} className="flex items-center gap-2 cursor-pointer select-none text-sm">
                  <Checkbox
                    checked={selectedEvents.includes(e.id)}
                    onCheckedChange={() => toggleEvent(e.id)}
                  />
                  {e.label}
                </label>
              ))}
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={handleCreate}
            disabled={!name || !url || selectedEvents.length === 0 || create.isPending}
          >
            {create.isPending && <Loader2 className="mr-2 w-4 h-4 animate-spin" />}
            Create
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function OutboundWebhooksPage() {
  const [filterProject, setFilterProject] = useState<string>("all");
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteId, setDeleteId] = useState<string | null>(null);
  const [testingId, setTestingId] = useState<string | null>(null);

  const { data: hooks, isLoading } = useOutboundWebhooks(
    filterProject !== "all" ? filterProject : undefined
  );
  const deleteHook = useDeleteOutboundWebhook();
  const testHook = useTestOutboundWebhook();
  const { data: projects } = useProjects();
  const projectList = projects?.data ?? [];

  function handleTest(id: string) {
    setTestingId(id);
    testHook.mutate(id, {
      onSuccess: (msg) => {
        toast.success(typeof msg === "string" ? msg : "Test delivered");
        setTestingId(null);
      },
      onError: (e: any) => {
        toast.error("Test failed", { description: e?.message });
        setTestingId(null);
      },
    });
  }

  return (
    <div className="space-y-6 p-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <Webhook className="w-6 h-6 text-primary" />
            Outbound Webhooks
          </h1>
          <p className="text-muted-foreground mt-1 text-sm">
            Notify external services (Slack, Teams, CI systems) when deployment events fire.
            Payloads can be signed with HMAC-SHA256.
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="mr-2 w-4 h-4" /> Create Webhook
        </Button>
      </div>

      {/* Filter */}
      <div className="flex gap-3">
        <Select value={filterProject} onValueChange={setFilterProject}>
          <SelectTrigger className="w-52">
            <SelectValue placeholder="All projects" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All projects</SelectItem>
            {projectList.map((p) => (
              <SelectItem key={p.id} value={p.id}>
                {p.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* List */}
      {isLoading ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-32 rounded-lg" />
          ))}
        </div>
      ) : (hooks ?? []).length === 0 ? (
        <Card>
          <CardContent className="py-12 text-center space-y-3">
            <Webhook className="w-12 h-12 mx-auto text-muted-foreground/40" />
            <p className="text-muted-foreground">No outbound webhooks configured.</p>
            <p className="text-xs text-muted-foreground">
              Add a webhook to receive deployment notifications in Slack, Discord, or any HTTP endpoint.
            </p>
            <Button variant="outline" onClick={() => setCreateOpen(true)}>
              <Plus className="mr-2 w-4 h-4" /> Create first webhook
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {(hooks ?? []).map((hook) => (
            <WebhookCard
              key={hook.id}
              hook={hook}
              onDelete={() => setDeleteId(hook.id)}
              onTest={() => handleTest(hook.id)}
              testing={testingId === hook.id}
            />
          ))}
        </div>
      )}

      {/* Create dialog */}
      <CreateWebhookDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        projectList={projectList}
      />

      {/* Delete confirm */}
      <ConfirmActionDialog
        open={!!deleteId}
        onOpenChange={(v) => !v && setDeleteId(null)}
        title="Delete webhook?"
        description="All future events will stop being delivered to this endpoint."
        confirmLabel="Delete"
        confirmVariant="destructive"
        onConfirm={() => {
          if (deleteId)
            deleteHook.mutate(deleteId, {
              onSuccess: () => {
                toast.success("Webhook deleted");
                setDeleteId(null);
              },
              onError: () => toast.error("Failed to delete"),
            });
        }}
      />
    </div>
  );
}
