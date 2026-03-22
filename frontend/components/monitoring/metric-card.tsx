"use client";

import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { TrendingUp, TrendingDown, ArrowUpRight } from "lucide-react";
import { cn } from "@/lib/utils";
import Link from "next/link";
import type { LucideIcon } from "lucide-react";

interface MetricCardProps {
  title: string;
  value: string | number | null;
  icon: LucideIcon;
  trend?: { value: number; isPositive: boolean };
  href?: string;
  color?: "blue" | "purple" | "green" | "orange" | "red";
  description?: string;
}

const colorMap = {
  blue: { bg: "bg-blue-500/10", text: "text-blue-500", border: "border-blue-500/20" },
  purple: { bg: "bg-purple-500/10", text: "text-purple-500", border: "border-purple-500/20" },
  green: { bg: "bg-success/10", text: "text-success", border: "border-success/20" },
  orange: { bg: "bg-orange-500/10", text: "text-orange-500", border: "border-orange-500/20" },
  red: { bg: "bg-destructive/10", text: "text-destructive", border: "border-destructive/20" },
};

export function MetricCard({
  title,
  value,
  icon: Icon,
  trend,
  href,
  color = "blue",
  description,
}: MetricCardProps) {
  const colors = colorMap[color];

  const content = (
    <Card
      className={cn(
        "glass-card hover:border-border/80 transition-all duration-200 relative overflow-hidden group",
        href && "cursor-pointer hover:shadow-md"
      )}
    >
      <CardContent className="p-5">
        <div className="flex items-start justify-between mb-3">
          <div className={cn("w-10 h-10 rounded-lg flex items-center justify-center", colors.bg, colors.border, "border")}>
            <Icon className={cn("w-5 h-5", colors.text)} />
          </div>
          {href && (
            <ArrowUpRight className="w-4 h-4 text-muted-foreground/40 group-hover:text-muted-foreground transition-colors" />
          )}
        </div>

        {value === null ? (
          <>
            <Skeleton className="h-8 w-20 mb-1" />
            <Skeleton className="h-3.5 w-28" />
          </>
        ) : (
          <>
            <div className="text-2xl font-bold tracking-tight">{value}</div>
            <div className="flex items-center gap-2 mt-1">
              <span className="text-sm text-muted-foreground">{title}</span>
            </div>
            {(trend || description) && (
              <div className="flex items-center gap-1.5 mt-2">
                {trend && (
                  <span
                    className={cn(
                      "flex items-center gap-0.5 text-xs font-medium",
                      trend.isPositive ? "text-success" : "text-destructive"
                    )}
                  >
                    {trend.isPositive ? (
                      <TrendingUp className="w-3 h-3" />
                    ) : (
                      <TrendingDown className="w-3 h-3" />
                    )}
                    {trend.value}%
                  </span>
                )}
                {description && (
                  <span className="text-xs text-muted-foreground">{description}</span>
                )}
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );

  if (href) return <Link href={href} className="block hover:no-underline">{content}</Link>;
  return content;
}
