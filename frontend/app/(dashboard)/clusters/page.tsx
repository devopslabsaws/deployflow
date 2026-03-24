"use client";

import { useState, useEffect } from "react";
import {
  Network, Plus, Trash2, Server, Loader2, ChevronRight,
  Shuffle, Activity, MoreHorizontal, ShieldOff, Shield, ArrowDownToLine,
  RefreshCw,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from "@/components/ui/dialog";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { apiClient } from "@/lib/api-client";
import { toast } from "sonner";
import { ConfirmActionDialog } from "@/components/ui/confirm-action-dialog";
import { useCordonNode, useUncordonNode, useDrainNode, useRebalanceCluster } from "@/hooks/use-api";

interface ClusterNode {
  serverId: string;
  name: string;
  ipAddress: string;
  status: string;
  cpuUsagePercent?: number;
  memoryUsagePercent?: number;
  isCordoned?: boolean;
  isDraining?: boolean;
}

interface Cluster {
  id: string;
  name: string;
  strategy: "RoundRobin" | "LeastLoaded" | "Replicated";
  nodeCount: number;
  nodes?: ClusterNode[];
}

interface RebalanceResult {
  clusterId: string;
  totalNodes: number;
  healthyNodes: number;
}

const STRATEGY_LABELS: Record<string, string> = {
  RoundRobin: "Round Robin",
  LeastLoaded: "Least Loaded",
  Replicated: "Replicated",
};

const STRATEGY_DESCRIPTIONS: Record<string, string> = {
  RoundRobin: "Distribute deployments evenly across all nodes in order",
  LeastLoaded: "Always deploy to the server with the lowest CPU usage",
  Replicated: "Deploy to all nodes simultaneously",
};

function StrategyBadge({ strategy }: { strategy: string }) {
  const colors: Record<string, string> = {
    RoundRobin: "bg-blue-500/10 text-blue-500 border-blue-500/20",
    LeastLoaded: "bg-green-500/10 text-green-500 border-green-500/20",
    Replicated: "bg-purple-500/10 text-purple-500 border-purple-500/20",
  };
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs border font-medium ${colors[strategy] ?? ""}`}>
      {STRATEGY_LABELS[strategy] ?? strategy}
    </span>
  );
}

export default function ClustersPage() {
  const [clusters, setClusters] = useState<Cluster[]>([]);
  const [loading, setLoading] = useState(true);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({ name: "", strategy: "RoundRobin" });

  const [addNodeOpen, setAddNodeOpen] = useState<string | null>(null);
  const [nodeServerId, setNodeServerId] = useState("");
  const [addingNode, setAddingNode] = useState(false);
  const [removingNode, setRemovingNode] = useState<string | null>(null);
  const [removeNodeTarget, setRemoveNodeTarget] = useState<{ clusterId: string; serverId: string; name: string } | null>(null);

  const [previewClusterId, setPreviewClusterId] = useState<string | null>(null);
  const [nextNode, setNextNode] = useState<ClusterNode | null>(null);
  const [previewing, setPreviewing] = useState(false);

  // Rebalance
  const [rebalanceTarget, setRebalanceTarget] = useState<Cluster | null>(null);
  const [rebalanceResult, setRebalanceResult] = useState<RebalanceResult | null>(null);
  const rebalance = useRebalanceCluster();

  // Node actions
  const cordonNode = useCordonNode();
  const uncordonNode = useUncordonNode();
  const drainNode = useDrainNode();
  const [nodeActionBusy, setNodeActionBusy] = useState<string | null>(null);

  const load = () => {
    setLoading(true);
    apiClient.get("/clusters")
      .then((d: any) => setClusters(d.data ?? d ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, []);

  const loadNodes = async (clusterId: string) => {
    try {
      const d: any = await apiClient.get(`/clusters/${clusterId}/nodes`);
      setClusters(cs =>
        cs.map(c => c.id === clusterId ? { ...c, nodes: d.data ?? d ?? [] } : c)
      );
    } catch { /* silent */ }
  };

  const toggleExpand = (id: string) => {
    const next = expanded === id ? null : id;
    setExpanded(next);
    if (next && !clusters.find(c => c.id === id)?.nodes) loadNodes(id);
  };

  const handleCreate = async () => {
    if (!form.name) { toast.error("Cluster name is required."); return; }
    setCreating(true);
    try {
      const created: any = await apiClient.post("/clusters", form);
      setClusters(cs => [...cs, created]);
      setCreateOpen(false);
      setForm({ name: "", strategy: "RoundRobin" });
      toast.success("Cluster created!");
    } catch (e: any) {
      toast.error("Failed to create cluster", { description: e.message });
    } finally {
      setCreating(false);
    }
  };

  const handleAddNode = async () => {
    if (!addNodeOpen || !nodeServerId) return;
    setAddingNode(true);
    try {
      await apiClient.post(`/clusters/${addNodeOpen}/nodes/${nodeServerId}`, {});
      await loadNodes(addNodeOpen);
      setClusters(cs =>
        cs.map(c => c.id === addNodeOpen
          ? { ...c, nodeCount: c.nodeCount + 1 }
          : c
        )
      );
      setAddNodeOpen(null);
      setNodeServerId("");
      toast.success("Node added to cluster.");
    } catch (e: any) {
      toast.error("Failed to add node", { description: e.message });
    } finally {
      setAddingNode(false);
    }
  };

  const handleRemoveNode = async () => {
    if (!removeNodeTarget) return;
    setRemovingNode(removeNodeTarget.serverId);
    try {
      await apiClient.delete(`/clusters/${removeNodeTarget.clusterId}/nodes/${removeNodeTarget.serverId}`);
      setClusters(cs =>
        cs.map(c => c.id === removeNodeTarget.clusterId
          ? { ...c, nodeCount: Math.max(0, c.nodeCount - 1), nodes: c.nodes?.filter(n => n.serverId !== removeNodeTarget.serverId) }
          : c
        )
      );
      toast.success("Node removed.");
    } catch (e: any) {
      toast.error("Failed to remove node", { description: e.message });
    } finally {
      setRemovingNode(null);
    }
  };

  const handlePreviewNext = async (clusterId: string) => {
    setPreviewing(true);
    setPreviewClusterId(clusterId);
    try {
      const d: any = await apiClient.get(`/clusters/${clusterId}/next-node`);
      setNextNode(d);
    } catch (e: any) {
      toast.error("Failed to preview", { description: e.message });
    } finally {
      setPreviewing(false);
    }
  };

  const handleNodeAction = async (
    action: "cordon" | "uncordon" | "drain",
    clusterId: string,
    node: ClusterNode,
  ) => {
    setNodeActionBusy(node.serverId);
    try {
      if (action === "cordon") {
        await cordonNode.mutateAsync({ clusterId, serverId: node.serverId });
        toast.success(`Node ${node.name} cordoned — no new deployments will be scheduled here`);
        setClusters(cs => cs.map(c => c.id === clusterId
          ? { ...c, nodes: c.nodes?.map(n => n.serverId === node.serverId ? { ...n, isCordoned: true } : n) }
          : c));
      } else if (action === "uncordon") {
        await uncordonNode.mutateAsync({ clusterId, serverId: node.serverId });
        toast.success(`Node ${node.name} uncordoned — accepting deployments again`);
        setClusters(cs => cs.map(c => c.id === clusterId
          ? { ...c, nodes: c.nodes?.map(n => n.serverId === node.serverId ? { ...n, isCordoned: false, isDraining: false } : n) }
          : c));
      } else {
        await drainNode.mutateAsync({ clusterId, serverId: node.serverId });
        toast.success(`Node ${node.name} is draining — workloads will migrate`);
        setClusters(cs => cs.map(c => c.id === clusterId
          ? { ...c, nodes: c.nodes?.map(n => n.serverId === node.serverId ? { ...n, isCordoned: true, isDraining: true } : n) }
          : c));
      }
    } catch (e: any) {
      toast.error(`Failed to ${action} node`, { description: e.message });
    } finally {
      setNodeActionBusy(null);
    }
  };

  const handleRebalance = async () => {
    if (!rebalanceTarget) return;
    try {
      const result: any = await rebalance.mutateAsync(rebalanceTarget.id);
      setRebalanceResult(result);
    } catch (e: any) {
      toast.error("Rebalance failed", { description: e.message });
      setRebalanceTarget(null);
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Network className="w-6 h-6 text-muted-foreground" />
          <div>
            <h1 className="text-2xl font-bold">Clusters</h1>
            <p className="text-sm text-muted-foreground">
              Multi-node server groups for distributing deployments
            </p>
          </div>
        </div>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="w-4 h-4 mr-1.5" />
          New Cluster
        </Button>
      </div>

      {/* Cluster list */}
      {loading ? (
        <div className="grid gap-4">
          {[...Array(3)].map((_, i) => (
            <div key={i} className="h-24 rounded-xl bg-muted animate-pulse" />
          ))}
        </div>
      ) : clusters.length === 0 ? (
        <Card className="glass-card">
          <CardContent className="flex flex-col items-center py-16 gap-4 text-muted-foreground">
            <Network className="w-12 h-12 opacity-30" />
            <p className="text-sm">No clusters configured</p>
            <Button variant="outline" size="sm" onClick={() => setCreateOpen(true)}>
              <Plus className="w-3.5 h-3.5 mr-1.5" />
              Create your first cluster
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {clusters.map(cluster => (
            <Card key={cluster.id} className="glass-card">
              <CardHeader className="pb-3">
                <div className="flex items-center justify-between gap-3">
                  <div className="flex items-center gap-3 min-w-0">
                    <button
                      onClick={() => toggleExpand(cluster.id)}
                      className="flex items-center gap-3 text-left min-w-0"
                    >
                      <ChevronRight
                        className={`w-4 h-4 text-muted-foreground transition-transform ${expanded === cluster.id ? "rotate-90" : ""}`}
                      />
                      <CardTitle className="text-base">{cluster.name}</CardTitle>
                    </button>
                    <StrategyBadge strategy={cluster.strategy} />
                    <Badge variant="outline" className="text-xs">
                      {cluster.nodeCount} node{cluster.nodeCount !== 1 ? "s" : ""}
                    </Badge>
                  </div>
                  <div className="flex gap-2 shrink-0">
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={previewing && previewClusterId === cluster.id}
                      onClick={() => handlePreviewNext(cluster.id)}
                    >
                      {previewing && previewClusterId === cluster.id
                        ? <Loader2 className="w-3.5 h-3.5 animate-spin mr-1.5" />
                        : <Shuffle className="w-3.5 h-3.5 mr-1.5" />}
                      Preview Next
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => { setRebalanceTarget(cluster); setRebalanceResult(null); }}
                    >
                      <RefreshCw className="w-3.5 h-3.5 mr-1.5" />
                      Rebalance
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => { setAddNodeOpen(cluster.id); setNodeServerId(""); }}
                    >
                      <Plus className="w-3.5 h-3.5 mr-1.5" />
                      Add Node
                    </Button>
                  </div>
                </div>
                {previewClusterId === cluster.id && nextNode && (
                  <div className="mt-2 rounded-lg border border-primary/20 bg-primary/5 p-3 text-xs space-y-1">
                    <p className="font-medium text-primary">Next deployment target:</p>
                    <p>{nextNode.name} — <code>{nextNode.ipAddress}</code></p>
                  </div>
                )}
                <p className="text-xs text-muted-foreground pl-7">
                  {STRATEGY_DESCRIPTIONS[cluster.strategy]}
                </p>
              </CardHeader>

              {expanded === cluster.id && (
                <CardContent className="pt-0 pb-4 pl-10">
                  {!cluster.nodes ? (
                    <div className="h-16 rounded bg-muted animate-pulse" />
                  ) : cluster.nodes.length === 0 ? (
                    <p className="text-xs text-muted-foreground py-2">No nodes — add a server to this cluster</p>
                  ) : (
                    <div className="space-y-2">
                      {cluster.nodes.map(node => (
                        <div
                          key={node.serverId}
                          className={`flex items-center justify-between rounded-lg border px-3 py-2 ${
                            node.isDraining ? "bg-orange-500/5 border-orange-500/20" :
                            node.isCordoned ? "bg-yellow-500/5 border-yellow-500/20" :
                            "bg-card/40"
                          }`}
                        >
                          <div className="flex items-center gap-3">
                            <Server className="w-4 h-4 text-muted-foreground" />
                            <div>
                              <div className="flex items-center gap-2">
                                <p className="text-sm font-medium">{node.name}</p>
                                {node.isDraining && (
                                  <span className="text-xs px-1.5 py-0.5 rounded bg-orange-500/10 text-orange-500 border border-orange-500/20 font-medium">
                                    Draining
                                  </span>
                                )}
                                {!node.isDraining && node.isCordoned && (
                                  <span className="text-xs px-1.5 py-0.5 rounded bg-yellow-500/10 text-yellow-500 border border-yellow-500/20 font-medium">
                                    Cordoned
                                  </span>
                                )}
                              </div>
                              <p className="text-xs text-muted-foreground font-mono">{node.ipAddress}</p>
                            </div>
                          </div>
                          <div className="flex items-center gap-3">
                            {node.cpuUsagePercent !== undefined && (
                              <div className="flex items-center gap-1 text-xs text-muted-foreground">
                                <Activity className="w-3 h-3" />
                                {node.cpuUsagePercent.toFixed(1)}% CPU
                              </div>
                            )}
                            <Badge
                              variant={node.status === "active" ? "default" : "secondary"}
                              className="text-xs"
                            >
                              {node.status}
                            </Badge>

                            {/* Node action menu */}
                            <DropdownMenu>
                              <DropdownMenuTrigger asChild>
                                <Button
                                  size="icon"
                                  variant="ghost"
                                  className="h-6 w-6"
                                  disabled={nodeActionBusy === node.serverId}
                                >
                                  {nodeActionBusy === node.serverId
                                    ? <Loader2 className="w-3 h-3 animate-spin" />
                                    : <MoreHorizontal className="w-3 h-3" />}
                                </Button>
                              </DropdownMenuTrigger>
                              <DropdownMenuContent align="end">
                                {node.isCordoned ? (
                                  <DropdownMenuItem onClick={() => handleNodeAction("uncordon", cluster.id, node)}>
                                    <Shield className="w-3.5 h-3.5 mr-2 text-green-500" />
                                    Uncordon
                                  </DropdownMenuItem>
                                ) : (
                                  <DropdownMenuItem onClick={() => handleNodeAction("cordon", cluster.id, node)}>
                                    <ShieldOff className="w-3.5 h-3.5 mr-2 text-yellow-500" />
                                    Cordon
                                  </DropdownMenuItem>
                                )}
                                <DropdownMenuItem
                                  disabled={node.isDraining}
                                  onClick={() => handleNodeAction("drain", cluster.id, node)}
                                >
                                  <ArrowDownToLine className="w-3.5 h-3.5 mr-2 text-orange-500" />
                                  Drain
                                </DropdownMenuItem>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  className="text-destructive focus:text-destructive"
                                  disabled={removingNode === node.serverId}
                                  onClick={() => setRemoveNodeTarget({ clusterId: cluster.id, serverId: node.serverId, name: node.name })}
                                >
                                  <Trash2 className="w-3.5 h-3.5 mr-2" />
                                  Remove from cluster
                                </DropdownMenuItem>
                              </DropdownMenuContent>
                            </DropdownMenu>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </CardContent>
              )}
            </Card>
          ))}
        </div>
      )}

      {/* Create Cluster Dialog */}
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Network className="w-4 h-4" />
              New Cluster
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="cluster-name" className="text-xs">Cluster Name *</Label>
              <Input
                id="cluster-name"
                placeholder="e.g. Production East"
                value={form.name}
                onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
                autoFocus
              />
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">Deployment Strategy</Label>
              <Select
                value={form.strategy}
                onValueChange={v => setForm(f => ({ ...f, strategy: v }))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {Object.entries(STRATEGY_LABELS).map(([value, label]) => (
                    <SelectItem key={value} value={value}>
                      <div>
                        <p className="font-medium">{label}</p>
                        <p className="text-xs text-muted-foreground">{STRATEGY_DESCRIPTIONS[value]}</p>
                      </div>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button disabled={creating} onClick={handleCreate}>
              {creating && <Loader2 className="w-3.5 h-3.5 animate-spin mr-1.5" />}
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Add Node Dialog */}
      <Dialog open={!!addNodeOpen} onOpenChange={v => !v && setAddNodeOpen(null)}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Add Node to Cluster</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="node-server-id" className="text-xs">Server ID *</Label>
              <Input
                id="node-server-id"
                placeholder="Server UUID"
                value={nodeServerId}
                onChange={e => setNodeServerId(e.target.value)}
                className="font-mono text-xs"
              />
              <p className="text-xs text-muted-foreground">
                Enter the ID of the server to add. You can find it in the server settings URL.
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddNodeOpen(null)}>Cancel</Button>
            <Button disabled={addingNode || !nodeServerId} onClick={handleAddNode}>
              {addingNode && <Loader2 className="w-3.5 h-3.5 animate-spin mr-1.5" />}
              Add Node
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Rebalance Dialog */}
      <Dialog open={!!rebalanceTarget} onOpenChange={v => { if (!v) { setRebalanceTarget(null); setRebalanceResult(null); } }}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <RefreshCw className="w-4 h-4" />
              Rebalance Cluster
            </DialogTitle>
          </DialogHeader>
          {rebalanceResult ? (
            <div className="py-4 space-y-3">
              <div className="rounded-lg border bg-green-500/5 border-green-500/20 p-4 space-y-2">
                <p className="text-sm font-medium text-green-600">Rebalance complete</p>
                <div className="grid grid-cols-2 gap-3 text-sm">
                  <div>
                    <p className="text-xs text-muted-foreground">Total Nodes</p>
                    <p className="font-semibold">{rebalanceResult.totalNodes}</p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">Healthy Nodes</p>
                    <p className="font-semibold text-green-600">{rebalanceResult.healthyNodes}</p>
                  </div>
                </div>
                <p className="text-xs text-muted-foreground">
                  Round-robin cursor reset — next deployment will start from the first available node.
                </p>
              </div>
            </div>
          ) : (
            <div className="py-2">
              <p className="text-sm text-muted-foreground">
                Rebalancing <span className="font-medium text-foreground">{rebalanceTarget?.name}</span> will reset the round-robin cursor and return a health summary of all active nodes.
              </p>
              <p className="text-xs text-muted-foreground mt-2">
                Cordoned and draining nodes will be excluded from the healthy count.
              </p>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => { setRebalanceTarget(null); setRebalanceResult(null); }}>
              {rebalanceResult ? "Close" : "Cancel"}
            </Button>
            {!rebalanceResult && (
              <Button disabled={rebalance.isPending} onClick={handleRebalance}>
                {rebalance.isPending && <Loader2 className="w-3.5 h-3.5 animate-spin mr-1.5" />}
                Rebalance
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmActionDialog
        open={!!removeNodeTarget}
        onOpenChange={(open) => { if (!open) setRemoveNodeTarget(null); }}
        title="Remove Cluster Node"
        description={removeNodeTarget
          ? `Remove node "${removeNodeTarget.name}" from this cluster?`
          : "Remove this node from the cluster?"}
        confirmLabel="Remove Node"
        isConfirming={Boolean(removingNode)}
        onConfirm={handleRemoveNode}
      />
    </div>
  );
}
