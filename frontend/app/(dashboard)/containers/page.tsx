"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
  Container, Plus, Play, Square, RefreshCw, Trash2, MoreVertical,
  Cpu, MemoryStick, Activity, Search,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useContainers } from "@/hooks/use-api";
import { toast } from "sonner";
import { formatDistanceToNow } from "date-fns";

const statusColors: Record<string, string> = {
  running: "text-success",
  stopped: "text-muted-foreground",
  exited: "text-destructive",
  paused: "text-warning",
  restarting: "text-info",
};

export default function ContainersPage() {
  const [search, setSearch] = useState("");
  const { data: containers, isLoading, refetch } = useContainers();

  const filtered = containers?.filter(
    (c) => c.name.toLowerCase().includes(search.toLowerCase()) ||
      c.image.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Containers</h1>
          <p className="text-muted-foreground text-sm mt-0.5">
            {containers?.filter((c) => c.status === "running").length ?? 0} running of {containers?.length ?? 0} total
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => refetch()}>
          <RefreshCw className="h-4 w-4 mr-2" />
          Refresh
        </Button>
      </div>

      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Search containers..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="pl-9"
        />
      </div>

      <div className="space-y-3">
        {isLoading
          ? Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-28 w-full rounded-lg" />)
          : filtered?.length === 0
          ? (
            <Card>
              <CardContent className="py-16 text-center">
                <Container className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                <p className="text-lg font-medium">No containers found</p>
                <p className="text-sm text-muted-foreground">
                  Containers appear here once deployments are running.
                </p>
              </CardContent>
            </Card>
          )
          : filtered?.map((container) => (
            <motion.div key={container.id} initial={{ opacity: 0, y: 4 }} animate={{ opacity: 1, y: 0 }}>
              <Card>
                <CardContent className="py-4">
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex items-start gap-3 min-w-0">
                      <Container className="h-5 w-5 text-muted-foreground mt-0.5 flex-shrink-0" />
                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <span className="font-medium truncate">{container.name}</span>
                          <Badge
                            variant="outline"
                            className={`text-xs ${statusColors[container.status] ?? "text-muted-foreground"}`}
                          >
                            <Activity className="h-3 w-3 mr-1" />
                            {container.status}
                          </Badge>
                        </div>
                        <p className="text-xs text-muted-foreground font-mono mt-0.5 truncate">
                          {container.image}
                        </p>
                        <p className="text-xs text-muted-foreground mt-0.5">
                          Started {formatDistanceToNow(new Date(container.startedAt), { addSuffix: true })}
                        </p>
                      </div>
                    </div>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" className="h-8 w-8 flex-shrink-0">
                          <MoreVertical className="h-4 w-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        {container.status !== "running" && (
                          <DropdownMenuItem>
                            <Play className="h-4 w-4 mr-2" />Start
                          </DropdownMenuItem>
                        )}
                        {container.status === "running" && (
                          <DropdownMenuItem>
                            <Square className="h-4 w-4 mr-2" />Stop
                          </DropdownMenuItem>
                        )}
                        <DropdownMenuItem>
                          <RefreshCw className="h-4 w-4 mr-2" />Restart
                        </DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem className="text-destructive">
                          <Trash2 className="h-4 w-4 mr-2" />Remove
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                  {container.status === "running" && (
                    <div className="mt-3 grid grid-cols-2 gap-4">
                      <div>
                        <div className="flex items-center justify-between text-xs text-muted-foreground mb-1">
                          <span className="flex items-center gap-1"><Cpu className="h-3 w-3" />CPU</span>
                          <span>{container.cpuPercent.toFixed(1)}%</span>
                        </div>
                        <Progress value={container.cpuPercent} className="h-1.5" />
                      </div>
                      <div>
                        <div className="flex items-center justify-between text-xs text-muted-foreground mb-1">
                          <span className="flex items-center gap-1"><MemoryStick className="h-3 w-3" />Memory</span>
                          <span>{(container.memoryBytes / 1024 / 1024).toFixed(0)} MB</span>
                        </div>
                        <Progress
                          value={container.memoryLimitBytes > 0
                            ? (container.memoryBytes / container.memoryLimitBytes) * 100 : 0}
                          className="h-1.5"
                        />
                      </div>
                    </div>
                  )}
                </CardContent>
              </Card>
            </motion.div>
          ))}
      </div>
    </div>
  );
}
