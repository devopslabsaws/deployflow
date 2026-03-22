import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import type { DeploymentStatus } from "@/types";

interface DeploymentStatusBadgeProps {
  status: DeploymentStatus;
  compact?: boolean;
}

const statusConfig: Record<
  DeploymentStatus,
  { label: string; className: string; dotClass: string }
> = {
  queued: {
    label: "Queued",
    className: "bg-muted text-muted-foreground border-muted-foreground/20",
    dotClass: "bg-muted-foreground",
  },
  building: {
    label: "Building",
    className: "bg-blue-500/10 text-blue-500 border-blue-500/30",
    dotClass: "bg-blue-500 animate-pulse",
  },
  deploying: {
    label: "Deploying",
    className: "bg-purple-500/10 text-purple-500 border-purple-500/30",
    dotClass: "bg-purple-500 animate-pulse",
  },
  running: {
    label: "Running",
    className: "bg-success/10 text-success border-success/30",
    dotClass: "bg-success",
  },
  healthy: {
    label: "Healthy",
    className: "bg-success/10 text-success border-success/30",
    dotClass: "bg-success",
  },
  unhealthy: {
    label: "Unhealthy",
    className: "bg-warning/10 text-warning border-warning/30",
    dotClass: "bg-warning animate-pulse",
  },
  failed: {
    label: "Failed",
    className: "bg-destructive/10 text-destructive border-destructive/30",
    dotClass: "bg-destructive",
  },
  cancelled: {
    label: "Cancelled",
    className: "bg-muted text-muted-foreground border-muted-foreground/20",
    dotClass: "bg-muted-foreground",
  },
  stopped: {
    label: "Stopped",
    className: "bg-muted text-muted-foreground border-muted-foreground/20",
    dotClass: "bg-muted-foreground",
  },
  rolled_back: {
    label: "Rolled Back",
    className: "bg-orange-500/10 text-orange-500 border-orange-500/30",
    dotClass: "bg-orange-500",
  },
};

export function DeploymentStatusBadge({ status, compact = false }: DeploymentStatusBadgeProps) {
  const config = statusConfig[status] ?? statusConfig.failed;

  return (
    <Badge
      variant="outline"
      className={cn(
        "gap-1.5 font-medium border",
        config.className,
        compact ? "text-[10px] px-1.5 py-0 h-5" : "text-xs"
      )}
    >
      <span className={cn("w-1.5 h-1.5 rounded-full shrink-0", config.dotClass)} />
      {config.label}
    </Badge>
  );
}
