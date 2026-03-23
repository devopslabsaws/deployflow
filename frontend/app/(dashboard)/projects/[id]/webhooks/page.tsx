"use client";

import { useState, useEffect } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  ArrowLeft, Webhook, Copy, RefreshCw, Loader2, Check,
  Github, GitlabIcon, Eye, EyeOff,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Switch } from "@/components/ui/switch";
import { Badge } from "@/components/ui/badge";
import { useProject } from "@/hooks/use-api";
import { apiClient } from "@/lib/api-client";
import { toast } from "sonner";
import { ConfirmActionDialog } from "@/components/ui/confirm-action-dialog";

interface WebhookConfig {
  token: string;
  isActive: boolean;
  githubSecret?: string;
  gitlabSecret?: string;
  bitbucketSecret?: string;
  giteaSecret?: string;
  deployWebhookUrl: string;
  githubWebhookUrl: string;
  gitlabWebhookUrl: string;
  bitbucketWebhookUrl: string;
  giteaWebhookUrl: string;
}

function CopyInput({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    await navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };
  return (
    <div className="space-y-1.5">
      <Label className="text-xs">{label}</Label>
      <div className="flex gap-2">
        <Input readOnly value={value} className="font-mono text-xs" />
        <Button size="icon" variant="outline" className="shrink-0" onClick={copy}>
          {copied ? <Check className="w-3.5 h-3.5 text-green-500" /> : <Copy className="w-3.5 h-3.5" />}
        </Button>
      </div>
    </div>
  );
}

function SecretInput({ value, label, onChange }: { value: string; label: string; onChange: (v: string) => void }) {
  const [show, setShow] = useState(false);
  return (
    <div className="space-y-1.5">
      <Label className="text-xs">{label}</Label>
      <div className="flex gap-2">
        <Input
          type={show ? "text" : "password"}
          value={value}
          onChange={e => onChange(e.target.value)}
          placeholder="Leave blank to auto-generate"
          className="font-mono text-xs"
        />
        <Button size="icon" variant="outline" className="shrink-0" onClick={() => setShow(v => !v)}>
          {show ? <EyeOff className="w-3.5 h-3.5" /> : <Eye className="w-3.5 h-3.5" />}
        </Button>
      </div>
    </div>
  );
}

export default function ProjectWebhooksPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;

  const { data: project } = useProject(id);
  const p = project as any;

  const [config, setConfig] = useState<WebhookConfig | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [regenerating, setRegenerating] = useState(false);
  const [regenerateOpen, setRegenerateOpen] = useState(false);

  const [ghSecret, setGhSecret] = useState("");
  const [glSecret, setGlSecret] = useState("");
  const [bbSecret, setBbSecret] = useState("");
  const [gtSecret, setGtSecret] = useState("");
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    setLoading(true);
    apiClient.get(`/projects/${id}/webhooks`)
      .then((d: any) => {
        setConfig(d);
        setGhSecret(d.githubSecret ?? "");
        setGlSecret(d.gitlabSecret ?? "");
        setBbSecret(d.bitbucketSecret ?? "");
        setGtSecret(d.giteaSecret ?? "");
        setIsActive(d.isActive ?? true);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const handleSave = async () => {
    setSaving(true);
    try {
      const updated: any = await apiClient.put(`/projects/${id}/webhooks`, {
        githubSecret: ghSecret || null,
        gitlabSecret: glSecret || null,
        bitbucketSecret: bbSecret || null,
        giteaSecret: gtSecret || null,
        isActive,
      });
      setConfig(updated);
      toast.success("Webhook settings saved!");
    } catch (e: any) {
      toast.error("Failed to save", { description: e.message });
    } finally {
      setSaving(false);
    }
  };

  const handleRegenerate = async () => {
    setRegenerating(true);
    try {
      const updated: any = await apiClient.post(`/projects/${id}/webhooks/regenerate`, {});
      setConfig(updated);
      toast.success("Token regenerated.");
    } catch (e: any) {
      toast.error("Failed to regenerate", { description: e.message });
    } finally {
      setRegenerating(false);
    }
  };

  if (loading) {
    return (
      <div className="max-w-2xl space-y-4">
        <div className="h-6 w-48 bg-muted animate-pulse rounded" />
        <div className="h-64 bg-muted animate-pulse rounded-xl" />
      </div>
    );
  }

  return (
    <div className="max-w-2xl space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Link href={`/projects/${id}`}>
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
            <ArrowLeft className="w-4 h-4" />
          </Button>
        </Link>
        <Webhook className="w-5 h-5 text-muted-foreground" />
        <div>
          <h1 className="text-xl font-bold">Webhooks</h1>
          <p className="text-sm text-muted-foreground">{p?.name}</p>
        </div>
        <Badge variant={isActive ? "default" : "secondary"} className="ml-auto">
          {isActive ? "Active" : "Inactive"}
        </Badge>
      </div>

      {/* Deploy Webhook */}
      <Card className="glass-card">
        <CardHeader className="flex flex-row items-center justify-between pb-3">
          <div>
            <CardTitle className="text-sm">Deploy Webhook</CardTitle>
            <CardDescription>Trigger a deployment by sending a POST request to this URL</CardDescription>
          </div>
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2">
              <Label className="text-xs text-muted-foreground">Active</Label>
              <Switch checked={isActive} onCheckedChange={setIsActive} />
            </div>
            <Button size="sm" variant="outline" disabled={regenerating} onClick={() => setRegenerateOpen(true)}>
              {regenerating
                ? <Loader2 className="w-3.5 h-3.5 animate-spin mr-1.5" />
                : <RefreshCw className="w-3.5 h-3.5 mr-1.5" />}
              Regenerate
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {config && (
            <CopyInput value={config.deployWebhookUrl} label="Deploy Webhook URL" />
          )}
        </CardContent>
      </Card>

      {/* Git Provider Webhooks */}
      <Card className="glass-card">
        <CardHeader className="flex flex-row items-center justify-between pb-3">
          <div>
            <CardTitle className="text-sm">Git Provider Webhooks</CardTitle>
            <CardDescription>
              Add these URLs to your repository settings so pushes trigger automatic deployments
            </CardDescription>
          </div>
          <Button size="sm" disabled={saving} onClick={handleSave}>
            {saving && <Loader2 className="w-3.5 h-3.5 animate-spin mr-1.5" />}
            Save Secrets
          </Button>
        </CardHeader>
        <CardContent className="space-y-6">
          {/* GitHub */}
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-sm font-medium">
              <Github className="w-4 h-4" />
              GitHub
            </div>
            {config && (
              <CopyInput value={config.githubWebhookUrl} label="Webhook URL" />
            )}
            <SecretInput value={ghSecret} label="Secret (optional)" onChange={setGhSecret} />
            <p className="text-xs text-muted-foreground">
              Set the webhook content type to <code className="text-xs bg-muted px-1 rounded">application/json</code> and
              trigger on <code className="text-xs bg-muted px-1 rounded">push</code> events.
            </p>
          </div>

          <Separator />

          {/* GitLab */}
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-sm font-medium">
              <GitlabIcon className="w-4 h-4 text-orange-500" />
              GitLab
            </div>
            {config && (
              <CopyInput value={config.gitlabWebhookUrl} label="Webhook URL" />
            )}
            <SecretInput value={glSecret} label="Secret Token (optional)" onChange={setGlSecret} />
          </div>

          <Separator />

          {/* Bitbucket */}
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-sm font-medium">
              <svg className="w-4 h-4 text-blue-500" viewBox="0 0 24 24" fill="currentColor">
                <path d="M.778 1.213a.768.768 0 00-.768.892l3.263 19.81c.084.5.515.868 1.022.873H19.95a.772.772 0 00.77-.646l3.27-20.03a.768.768 0 00-.768-.891zM14.52 15.53H9.522L8.17 8.466h7.561z" />
              </svg>
              Bitbucket
            </div>
            {config && (
              <CopyInput value={config.bitbucketWebhookUrl} label="Webhook URL" />
            )}
            <SecretInput value={bbSecret} label="Secret (optional)" onChange={setBbSecret} />
          </div>

          <Separator />

          {/* Gitea */}
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-sm font-medium">
              <svg className="w-4 h-4 text-green-500" viewBox="0 0 24 24" fill="currentColor">
                <path d="M11.955 0A12 12 0 0 0 0 12a12 12 0 0 0 12 12 12 12 0 0 0 12-12A12 12 0 0 0 12 0a12 12 0 0 0-.045 0zm4.204 17.152v.912H9.052v-.912l1.44-1.44V9.6c0-1.512.756-2.556 1.692-3.096l-1.44-1.44.648-.648 3.456 3.456-.648.648-1.2-1.2c-.516.408-.9 1.068-.9 1.944v6.108z" />
              </svg>
              Gitea / Forgejo
            </div>
            {config && (
              <CopyInput value={config.giteaWebhookUrl} label="Webhook URL" />
            )}
            <SecretInput value={gtSecret} label="Secret (optional)" onChange={setGtSecret} />
          </div>
        </CardContent>
      </Card>

      <ConfirmActionDialog
        open={regenerateOpen}
        onOpenChange={setRegenerateOpen}
        title="Regenerate Webhook Token"
        description="Regenerate deploy token? All existing webhook URLs will change."
        confirmLabel="Regenerate"
        confirmVariant="default"
        isConfirming={regenerating}
        onConfirm={handleRegenerate}
      />
    </div>
  );
}
