"use client";

import { useParams, useRouter } from "next/navigation";
import { motion } from "framer-motion";
import {
  ArrowLeft, Rocket, Clock, GitCommit, Terminal,
  CheckCircle2, XCircle, AlertTriangle, Loader2,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useDeployment } from "@/hooks/use-api";
import { formatRelativeTime, formatDuration, formatDate } from "@/lib/utils";
import { DeploymentLiveLog } from "@/components/deployments/deployment-live-log";
import Link from "next/link";

const statusConfig: Record<string, { icon: React.ElementType; color: string; label: string }> = {
  running: { icon: Loader2, color: "text-blue-500", label: "Running" },
  healthy: { icon: CheckCircle2, color: "text-success", label: "Healthy" },
  failed: { icon: XCircle, color: "text-destructive", label: "Failed" },
  building: { icon: Loader2, color: "text-warning", label: "Building" },
  deploying: { icon: Loader2, color: "text-info", label: "Deploying" },
  queued: { icon: Clock, color: "text-muted-foreground", label: "Queued" },
  cancelled: { icon: AlertTriangle, color: "text-muted-foreground", label: "Cancelled" },
  stopped: { icon: AlertTriangle, color: "text-muted-foreground", label: "Stopped" },
};

export default function DeploymentDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;
  const { data: deployment, isLoading } = useDeployment(id);

  const status = deployment
    ? (statusConfig[deployment.status] ?? { icon: AlertTriangle, color: "text-muted-foreground", label: deployment.status })
    : null;

  return (
    <div className="space-y-6 max-w-4xl mx-auto">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" onClick={() => router.back()}>
          <ArrowLeft className="h-4 w-4 mr-1.5" />Back
        </Button>
      </div>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-48" />
        </div>
      ) : deployment ? (
        <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} className="space-y-1">
          <div className="flex items-center gap-3 flex-wrap">
            <h1 className="text-2xl font-bold">{deployment.projectName}</h1>
            {status && (
              <Badge variant="outline" className={`gap-1 ${status.color}`}>
                <status.icon className={`h-3.5 w-3.5 ${["running","building","deploying"].includes(deployment.status) ? "animate-spin" : ""}`} />
                {status.label}
              </Badge>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            Deployment {deployment.id.slice(0, 8)}
            {" · "}
            <span title={formatDate(deployment.createdAt, "PPpp")} className="cursor-help border-b border-dashed border-muted-foreground/50">
              {formatRelativeTime(deployment.createdAt)}
            </span>
            {" · "}
            <span className="text-xs">{formatDate(deployment.createdAt, "MMM d, yyyy HH:mm")}</span>
          </p>
        </motion.div>
      ) : (
        <div className="text-center py-16">
          <p className="text-muted-foreground">Deployment not found.</p>
          <Button variant="outline" size="sm" className="mt-4" asChild>
            <Link href="/deployments">View all deployments</Link>
          </Button>
        </div>
      )}

      {deployment && (
        <Tabs defaultValue="overview">
          <TabsList>
            <TabsTrigger value="overview">Overview</TabsTrigger>
            <TabsTrigger value="logs">Logs</TabsTrigger>
          </TabsList>
          <TabsContent value="overview" className="mt-4 space-y-4">
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              {[
                { label: "Status", value: status?.label ?? "–" },
                { label: "Branch", value: deployment.branch ?? "–" },
                { label: "Duration", value: deployment.duration ? formatDuration(deployment.duration) : "–" },
                { label: "Trigger", value: deployment.trigger },
              ].map(({ label, value }) => (
                <Card key={label}>
                  <CardContent className="pt-4 pb-3">
                    <p className="text-xs text-muted-foreground mb-1">{label}</p>
                    <p className="font-medium text-sm">{value}</p>
                  </CardContent>
                </Card>
              ))}
            </div>
            {deployment.errorMessage && (
              <Card className="border-destructive/40 bg-destructive/5">
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm text-destructive flex items-center gap-2">
                    Error Details
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-sm text-destructive/90 font-mono whitespace-pre-wrap">{deployment.errorMessage}</p>
                </CardContent>
              </Card>
            )}
            {deployment.commitMessage && (
              <Card>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm flex items-center gap-2">
                    <GitCommit className="h-4 w-4" /> Commit
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-sm">{deployment.commitMessage}</p>
                  {deployment.commitSha && (
                    <p className="text-xs text-muted-foreground font-mono mt-1">{deployment.commitSha.slice(0,12)}</p>
                  )}
                </CardContent>
              </Card>
            )}
          </TabsContent>
          <TabsContent value="logs" className="mt-4">
            <Card>
              <CardHeader className="flex flex-row items-center justify-between pb-3">
                <CardTitle className="text-sm flex items-center gap-2">
                  <Terminal className="h-4 w-4" /> Build Logs
                </CardTitle>
              </CardHeader>
              <CardContent>
                <DeploymentLiveLog
                  deploymentId={id}
                  initialStatus={deployment?.status}
                />
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      )}
    </div>
  );
}