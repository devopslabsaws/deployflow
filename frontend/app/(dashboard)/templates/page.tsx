"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Search,
  Layers,
  Rocket,
  ExternalLink,
  Github,
  Star,
  Database,
  Globe,
  Box,
  Cpu,
  BarChart2,
  MessageSquare,
  Mail,
  Settings2,
  HardDrive,
  Server,
  ShieldCheck,
  Plus,
  Trash2,
  RefreshCw,
  CheckCircle2,
  XCircle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  useTemplates, useDeployTemplate, type TemplateDto,
  usePolicyTemplates, useCreatePolicyTemplate, useUpdatePolicyTemplate, useDeletePolicyTemplate,
  type PolicyTemplateDto,
} from "@/hooks/use-api";
import { useProjects, useServers } from "@/hooks/use-api";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

const categories = [
  { id: "all", label: "All", icon: Layers },
  { id: "cms", label: "CMS", icon: Globe },
  { id: "database", label: "Database", icon: Database },
  { id: "analytics", label: "Analytics", icon: BarChart2 },
  { id: "monitoring", label: "Monitoring", icon: Cpu },
  { id: "automation", label: "Automation", icon: Settings2 },
  { id: "storage", label: "Storage", icon: HardDrive },
  { id: "communication", label: "Comms", icon: MessageSquare },
  { id: "productivity", label: "Productivity", icon: Mail },
  { id: "backend", label: "Backend", icon: Box },
  { id: "web", label: "Web", icon: Server },
];

const categoryColors: Record<string, string> = {
  cms: "text-blue-400 bg-blue-400/10",
  database: "text-green-400 bg-green-400/10",
  analytics: "text-purple-400 bg-purple-400/10",
  monitoring: "text-orange-400 bg-orange-400/10",
  automation: "text-yellow-400 bg-yellow-400/10",
  storage: "text-cyan-400 bg-cyan-400/10",
  communication: "text-pink-400 bg-pink-400/10",
  productivity: "text-indigo-400 bg-indigo-400/10",
  backend: "text-rose-400 bg-rose-400/10",
  web: "text-teal-400 bg-teal-400/10",
};

interface DeployDialogState {
  template: TemplateDto;
  envValues: Record<string, string>;
  projectId: string;
  serverId: string;
  environmentName: string;
}

export default function TemplatesPage() {
  const [activeCategory, setActiveCategory] = useState("all");
  const [search, setSearch] = useState("");
  const [deployState, setDeployState] = useState<DeployDialogState | null>(null);
  const [policyDialogOpen, setPolicyDialogOpen] = useState(false);
  const [policyForm, setPolicyForm] = useState({ name: "", description: "", appliesTo: "production", requiredApprovals: 1, autoApprovePattern: "", allowedHoursUtc: "" });

  const { data: templates = [], isLoading } = useTemplates(
    activeCategory !== "all" ? activeCategory : undefined
  );
  const { data: projects } = useProjects();
  const projectList = projects?.data ?? [];
  const { data: servers = [] } = useServers();
  const deployTemplate = useDeployTemplate();

  const { data: policies = [], isLoading: policiesLoading } = usePolicyTemplates();
  const createPolicy = useCreatePolicyTemplate();
  const deletePolicy = useDeletePolicyTemplate();
  const updatePolicy = useUpdatePolicyTemplate();

  const filtered = templates.filter(
    (t) =>
      search === "" ||
      t.name.toLowerCase().includes(search.toLowerCase()) ||
      t.description.toLowerCase().includes(search.toLowerCase())
  );

  function openDeploy(template: TemplateDto) {
    const initial: Record<string, string> = {};
    template.envVariables.forEach((v) => {
      if (v.defaultValue) initial[v.key] = v.defaultValue;
    });
    setDeployState({
      template,
      envValues: initial,
      projectId: projectList[0]?.id ?? "",
      serverId: servers[0]?.id ?? "",
      environmentName: "production",
    });
  }

  async function handleDeploy() {
    if (!deployState) return;
    try {
      await deployTemplate.mutateAsync({
        slug: deployState.template.slug,
        projectId: deployState.projectId,
        serverId: deployState.serverId || undefined,
        envOverrides: deployState.envValues,
        environmentName: deployState.environmentName,
      });
      toast.success(`${deployState.template.name} deployed successfully!`);
      setDeployState(null);
    } catch {
      toast.error("Deploy failed. Please check your configuration.");
    }
  }

  async function handleCreatePolicy() {
    try {
      await createPolicy.mutateAsync({
        name: policyForm.name,
        description: policyForm.description || undefined,
        appliesTo: policyForm.appliesTo,
        requiredApprovals: policyForm.requiredApprovals,
        autoApprovePattern: policyForm.autoApprovePattern || undefined,
        allowedHoursUtc: policyForm.allowedHoursUtc || undefined,
        isEnabled: true,
      } as any);
      toast.success("Policy template created");
      setPolicyDialogOpen(false);
      setPolicyForm({ name: "", description: "", appliesTo: "production", requiredApprovals: 1, autoApprovePattern: "", allowedHoursUtc: "" });
    } catch (e: any) {
      toast.error("Failed to create policy", { description: e?.message });
    }
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <Layers className="h-6 w-6 text-primary" />
            Templates &amp; Policies
          </h1>
          <p className="text-muted-foreground text-sm mt-1">
            App templates and approval governance policies
          </p>
        </div>
        <Badge variant="secondary" className="text-sm">
          {templates.length} templates
        </Badge>
      </div>

      <Tabs defaultValue="app-templates">
        <TabsList>
          <TabsTrigger value="app-templates" className="gap-1.5">
            <Layers className="h-3.5 w-3.5" />App Templates
          </TabsTrigger>
          <TabsTrigger value="policy-templates" className="gap-1.5">
            <ShieldCheck className="h-3.5 w-3.5" />Approval Policies
            {policies.length > 0 && (
              <Badge variant="secondary" className="ml-1 text-[10px] h-4 px-1">{policies.length}</Badge>
            )}
          </TabsTrigger>
        </TabsList>

        {/* ── App Templates tab ── */}
        <TabsContent value="app-templates" className="space-y-4 mt-4">
          {/* Category filters + search */}
          <div className="flex flex-col sm:flex-row gap-3">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-9"
                placeholder="Search templates..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
          </div>

          <div className="flex gap-2 flex-wrap">
            {categories.map((cat) => {
              const Icon = cat.icon;
              return (
                <Button
                  key={cat.id}
                  variant={activeCategory === cat.id ? "default" : "outline"}
                  size="sm"
                  onClick={() => setActiveCategory(cat.id)}
                  className="gap-1.5"
                >
                  <Icon className="h-3.5 w-3.5" />
                  {cat.label}
                </Button>
              );
            })}
          </div>

          {/* Template grid */}
          {isLoading ? (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
              {Array.from({ length: 8 }).map((_, i) => (
                <Skeleton key={i} className="h-52 rounded-xl" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <div className="text-center py-20 text-muted-foreground">
              <Layers className="h-12 w-12 mx-auto mb-4 opacity-20" />
              <p>No templates match your search.</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
              {filtered.map((template, idx) => (
                <motion.div
                  key={template.slug}
              initial={{ opacity: 0, y: 16 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: idx * 0.04 }}
            >
              <TemplateCard
                template={template}
                onDeploy={() => openDeploy(template)}
              />
            </motion.div>
          ))}
        </div>
        )}
        </TabsContent>

        {/* ── Policy Templates tab ── */}
        <TabsContent value="policy-templates" className="space-y-4 mt-4">
          <div className="flex items-center justify-between">
            <p className="text-sm text-muted-foreground">
              Define approval rules and governance policies for deployments to protected environments.
            </p>
            <Button size="sm" className="gap-1.5" onClick={() => setPolicyDialogOpen(true)}>
              <Plus className="h-3.5 w-3.5" />New Policy
            </Button>
          </div>
          {policiesLoading ? (
            <div className="space-y-3">{[1,2,3].map(i => <Skeleton key={i} className="h-20 rounded-lg" />)}</div>
          ) : policies.length === 0 ? (
            <div className="text-center py-16 text-muted-foreground">
              <ShieldCheck className="h-12 w-12 mx-auto mb-3 opacity-20" />
              <p className="font-medium">No approval policies yet</p>
              <p className="text-xs mt-1">Create a policy to require approvals before deploying to production.</p>
            </div>
          ) : (
            <div className="space-y-3">
              {policies.map((policy) => (
                <div key={policy.id} className="rounded-lg border border-border/50 bg-muted/5 p-4 flex items-start justify-between gap-4">
                  <div className="space-y-1 flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-sm">{policy.name}</span>
                      <Badge variant={policy.isEnabled ? "default" : "secondary"} className="text-[10px] h-4">
                        {policy.isEnabled ? "Active" : "Disabled"}
                      </Badge>
                    </div>
                    {policy.description && <p className="text-xs text-muted-foreground">{policy.description}</p>}
                    <div className="flex items-center gap-3 text-xs text-muted-foreground flex-wrap">
                      <span>Environments: <strong className="text-foreground">{policy.appliesTo}</strong></span>
                      <span>Required approvals: <strong className="text-foreground">{policy.requiredApprovals}</strong></span>
                      {policy.requiredApproverRole && <span>Role: <strong className="text-foreground">{policy.requiredApproverRole}</strong></span>}
                      {policy.allowedHoursUtc && <span>Hours: <strong className="text-foreground">{policy.allowedHoursUtc} UTC</strong></span>}
                      {policy.autoApprovePattern && <span>Auto-approve: <code className="font-mono text-foreground">{policy.autoApprovePattern}</code></span>}
                    </div>
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    <Button size="sm" variant="ghost" className="h-7 text-xs"
                      onClick={() => updatePolicy.mutate({ id: policy.id, isEnabled: !policy.isEnabled },
                        { onSuccess: () => toast.success(policy.isEnabled ? "Policy disabled" : "Policy enabled") })}>
                      {policy.isEnabled ? <XCircle className="h-3.5 w-3.5" /> : <CheckCircle2 className="h-3.5 w-3.5" />}
                    </Button>
                    <Button size="sm" variant="ghost" className="h-7 text-xs text-destructive hover:text-destructive"
                      onClick={() => deletePolicy.mutate(policy.id,
                        { onSuccess: () => toast.success("Policy deleted") })}>
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>

      {/* Create Policy Dialog */}
      <Dialog open={policyDialogOpen} onOpenChange={setPolicyDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create Approval Policy</DialogTitle>
            <DialogDescription>Define when deployments require approval before running.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div><Label>Policy Name</Label><Input placeholder="Production Gate" value={policyForm.name} onChange={e => setPolicyForm(f => ({...f, name: e.target.value}))} /></div>
            <div><Label>Description</Label><Input placeholder="Optional description" value={policyForm.description} onChange={e => setPolicyForm(f => ({...f, description: e.target.value}))} /></div>
            <div><Label>Applies To (comma-separated env names)</Label><Input placeholder="production,staging" value={policyForm.appliesTo} onChange={e => setPolicyForm(f => ({...f, appliesTo: e.target.value}))} /></div>
            <div><Label>Required Approvals</Label><Input type="number" min={1} max={10} value={policyForm.requiredApprovals} onChange={e => setPolicyForm(f => ({...f, requiredApprovals: parseInt(e.target.value)||1}))} /></div>
            <div><Label>Auto-Approve Branch Pattern (regex, optional)</Label><Input placeholder="^(develop|feature/.+)$" value={policyForm.autoApprovePattern} onChange={e => setPolicyForm(f => ({...f, autoApprovePattern: e.target.value}))} /></div>
            <div><Label>Allowed Deploy Hours UTC (optional, e.g. 09:00-17:00)</Label><Input placeholder="09:00-18:00" value={policyForm.allowedHoursUtc} onChange={e => setPolicyForm(f => ({...f, allowedHoursUtc: e.target.value}))} /></div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPolicyDialogOpen(false)}>Cancel</Button>
            <Button onClick={handleCreatePolicy} disabled={createPolicy.isPending || !policyForm.name}>
              {createPolicy.isPending ? <><RefreshCw className="h-3.5 w-3.5 animate-spin mr-1" />Creating…</> : "Create Policy"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Deploy Dialog */}
      {deployState && (
        <Dialog open onOpenChange={() => setDeployState(null)}>
          <DialogContent className="max-w-lg max-h-[80vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>Deploy {deployState.template.name}</DialogTitle>
              <DialogDescription>
                {deployState.template.description}
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-4">
              <div>
                <Label>Project</Label>
                <Select
                  value={deployState.projectId}
                  onValueChange={(v) =>
                    setDeployState((s) => s && { ...s, projectId: v })
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select project" />
                  </SelectTrigger>
                  <SelectContent>
                    {projectList.map((p: { id: string; name: string }) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div>
                <Label>Server</Label>
                <Select
                  value={deployState.serverId}
                  onValueChange={(v) =>
                    setDeployState((s) => s && { ...s, serverId: v })
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select server" />
                  </SelectTrigger>
                  <SelectContent>
                    {servers.map((s: { id: string; name: string }) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div>
                <Label>Environment</Label>
                <Select
                  value={deployState.environmentName}
                  onValueChange={(v) =>
                    setDeployState((s) => s && { ...s, environmentName: v })
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {["development", "qa", "staging", "production"].map((e) => (
                      <SelectItem key={e} value={e}>
                        {e.charAt(0).toUpperCase() + e.slice(1)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {deployState.template.envVariables.length > 0 && (
                <div className="space-y-3">
                  <Label className="text-sm font-semibold">
                    Environment Variables
                  </Label>
                  {deployState.template.envVariables.map((ev) => (
                    <div key={ev.key}>
                      <Label className="text-xs text-muted-foreground mb-1 flex items-center gap-1">
                        {ev.key}
                        {ev.required && (
                          <span className="text-destructive">*</span>
                        )}
                        {ev.description && (
                          <span className="font-normal opacity-70">
                            — {ev.description}
                          </span>
                        )}
                      </Label>
                      <Input
                        type={ev.isSecret ? "password" : "text"}
                        placeholder={ev.defaultValue ?? "Enter value..."}
                        value={deployState.envValues[ev.key] ?? ""}
                        onChange={(e) =>
                          setDeployState((s) =>
                            s
                              ? {
                                  ...s,
                                  envValues: {
                                    ...s.envValues,
                                    [ev.key]: e.target.value,
                                  },
                                }
                              : s
                          )
                        }
                      />
                    </div>
                  ))}
                </div>
              )}
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={() => setDeployState(null)}>
                Cancel
              </Button>
              <Button
                onClick={handleDeploy}
                disabled={deployTemplate.isPending}
                className="gap-2"
              >
                <Rocket className="h-4 w-4" />
                {deployTemplate.isPending ? "Deploying..." : "Deploy Now"}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </div>
  );
}

function TemplateCard({
  template,
  onDeploy,
}: {
  template: TemplateDto;
  onDeploy: () => void;
}) {
  const catColor =
    categoryColors[template.category] ?? "text-gray-400 bg-gray-400/10";

  return (
    <Card className="group hover:border-primary/40 transition-colors h-full flex flex-col">
      <CardContent className="p-4 flex flex-col h-full gap-3">
        {/* Logo + name */}
        <div className="flex items-start gap-3">
          <div
            className={cn(
              "w-10 h-10 rounded-lg flex items-center justify-center shrink-0",
              catColor
            )}
          >
            {template.logoUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={template.logoUrl}
                alt={template.name}
                className="w-6 h-6 object-contain"
                onError={(e) =>
                  ((e.currentTarget as HTMLImageElement).style.display = "none")
                }
              />
            ) : (
              <Box className="h-5 w-5" />
            )}
          </div>
          <div className="min-w-0">
            <div className="flex items-center gap-1.5">
              <span className="font-semibold text-sm truncate">
                {template.name}
              </span>
              {template.isOfficial && (
                <Star className="h-3 w-3 text-yellow-500 fill-yellow-500 shrink-0" />
              )}
            </div>
            <Badge
              variant="outline"
              className={cn("text-xs mt-0.5 capitalize", catColor)}
            >
              {template.category}
            </Badge>
          </div>
        </div>

        {/* Description */}
        <p className="text-xs text-muted-foreground line-clamp-2 flex-1">
          {template.description}
        </p>

        {/* Meta */}
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span>Port {template.defaultPort}</span>
          {template.requiresDatabase && (
            <>
              <span>·</span>
              <span className="flex items-center gap-0.5">
                <Database className="h-3 w-3" />
                {template.defaultDatabaseType}
              </span>
            </>
          )}
          <span className="ml-auto">{template.deployCount.toLocaleString()} deploys</span>
        </div>

        {/* Actions */}
        <div className="flex gap-2 pt-1">
          <Button size="sm" className="flex-1 gap-1.5" onClick={onDeploy}>
            <Rocket className="h-3.5 w-3.5" />
            Deploy
          </Button>
          {template.githubUrl && (
            <Button size="sm" variant="outline" asChild>
              <a
                href={template.githubUrl}
                target="_blank"
                rel="noopener noreferrer"
              >
                <Github className="h-3.5 w-3.5" />
              </a>
            </Button>
          )}
          {template.documentationUrl && (
            <Button size="sm" variant="outline" asChild>
              <a
                href={template.documentationUrl}
                target="_blank"
                rel="noopener noreferrer"
              >
                <ExternalLink className="h-3.5 w-3.5" />
              </a>
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
