"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import {
  DndContext,
  DragEndEvent,
  DragOverEvent,
  DragOverlay,
  DragStartEvent,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import {
  SortableContext,
  arrayMove,
  horizontalListSortingStrategy,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import {
  ArrowLeft,
  ChevronDown,
  ChevronUp,
  FileCode,
  GripHorizontal,
  GripVertical,
  Loader2,
  Plus,
  Save,
  Settings2,
  Trash2,
  X,
} from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { usePipeline, useUpdatePipelineStages } from "@/hooks/use-api";
import { apiClient } from "@/lib/api-client";
import { cn } from "@/lib/utils";
import type { PipelineStep } from "@/types";

// ── Types ─────────────────────────────────────────────────────────────────────

type LocalStep = {
  id: string; // client-only ephemeral ID
  name: string;
  type: PipelineStep["type"];
  command?: string;
  timeout?: number;
};

type LocalStage = {
  id: string; // client-only ephemeral ID
  name: string;
  runParallel: boolean;
  steps: LocalStep[];
};

type ActiveDrag =
  | { kind: "stage"; id: string }
  | { kind: "step"; id: string; stageId: string };

const STEP_TYPES: { value: PipelineStep["type"]; label: string; color: string }[] = [
  { value: "command", label: "Command", color: "bg-slate-500/15 text-slate-300 border-slate-500/30" },
  { value: "docker", label: "Docker", color: "bg-blue-500/15 text-blue-300 border-blue-500/30" },
  { value: "deploy", label: "Deploy", color: "bg-emerald-500/15 text-emerald-300 border-emerald-500/30" },
  { value: "test", label: "Test", color: "bg-amber-500/15 text-amber-300 border-amber-500/30" },
  { value: "notify", label: "Notify", color: "bg-purple-500/15 text-purple-300 border-purple-500/30" },
  { value: "approval", label: "Approval", color: "bg-rose-500/15 text-rose-300 border-rose-500/30" },
];

function stepTypeColor(type: string) {
  return STEP_TYPES.find((t) => t.value === type)?.color ?? STEP_TYPES[0].color;
}

function uid() {
  return Math.random().toString(36).slice(2, 10);
}

// ── SortableStepCard ──────────────────────────────────────────────────────────

function SortableStepCard({
  step,
  stageId,
  onUpdate,
  onDelete,
}: {
  step: LocalStep;
  stageId: string;
  onUpdate: (id: string, patch: Partial<LocalStep>) => void;
  onDelete: (id: string) => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } =
    useSortable({ id: step.id, data: { kind: "step", stageId } });

  const [expanded, setExpanded] = useState(false);

  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.35 : 1,
  };

  return (
    <div ref={setNodeRef} style={style} className="group">
      <div
        className={cn(
          "rounded-lg border border-border/60 bg-background/60 p-2.5 hover:border-border transition-all",
          isDragging && "shadow-lg"
        )}
      >
        <div className="flex items-center gap-2">
          <button
            {...attributes}
            {...listeners}
            className="cursor-grab active:cursor-grabbing touch-none text-muted-foreground/40 hover:text-muted-foreground transition-colors shrink-0"
            tabIndex={-1}
          >
            <GripVertical className="w-3.5 h-3.5" />
          </button>

          <div className="flex-1 min-w-0">
            <Input
              value={step.name}
              onChange={(e) => onUpdate(step.id, { name: e.target.value })}
              className="h-6 text-xs border-0 bg-transparent p-0 font-medium focus-visible:ring-0 focus-visible:ring-offset-0"
              placeholder="Step name…"
            />
          </div>

          <Badge
            variant="outline"
            className={cn("text-[10px] px-1.5 py-0 shrink-0 capitalize", stepTypeColor(step.type))}
          >
            {step.type}
          </Badge>

          <button
            onClick={() => setExpanded((v) => !v)}
            className="text-muted-foreground/40 hover:text-muted-foreground transition-colors shrink-0"
            tabIndex={-1}
          >
            {expanded ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
          </button>

          <button
            onClick={() => onDelete(step.id)}
            className="text-muted-foreground/30 hover:text-destructive transition-colors shrink-0 opacity-0 group-hover:opacity-100"
            tabIndex={-1}
          >
            <X className="w-3 h-3" />
          </button>
        </div>

        {expanded && (
          <div className="mt-3 space-y-2 pl-5">
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label className="text-[10px] text-muted-foreground uppercase tracking-wide">Type</Label>
                <Select
                  value={step.type}
                  onValueChange={(v) => onUpdate(step.id, { type: v as LocalStep["type"] })}
                >
                  <SelectTrigger className="h-7 text-xs">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {STEP_TYPES.map((t) => (
                      <SelectItem key={t.value} value={t.value} className="text-xs">
                        {t.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label className="text-[10px] text-muted-foreground uppercase tracking-wide">Timeout (s)</Label>
                <Input
                  type="number"
                  value={step.timeout ?? ""}
                  onChange={(e) =>
                    onUpdate(step.id, { timeout: e.target.value ? Number(e.target.value) : undefined })
                  }
                  className="h-7 text-xs"
                  placeholder="300"
                />
              </div>
            </div>
            {(step.type === "command" || step.type === "docker" || step.type === "deploy") && (
              <div className="space-y-1">
                <Label className="text-[10px] text-muted-foreground uppercase tracking-wide">
                  {step.type === "docker" ? "Image" : "Command"}
                </Label>
                <Input
                  value={step.command ?? ""}
                  onChange={(e) => onUpdate(step.id, { command: e.target.value || undefined })}
                  className="h-7 text-xs font-mono"
                  placeholder={step.type === "docker" ? "nginx:alpine" : "npm run test"}
                />
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

// ── StepDragOverlay ───────────────────────────────────────────────────────────

function StepDragOverlay({ step }: { step: LocalStep }) {
  return (
    <div className="rounded-lg border border-primary/40 bg-background shadow-xl p-2.5 w-64 rotate-1">
      <div className="flex items-center gap-2">
        <GripVertical className="w-3.5 h-3.5 text-muted-foreground/40 shrink-0" />
        <span className="text-xs font-medium flex-1 truncate">{step.name || "Unnamed step"}</span>
        <Badge variant="outline" className={cn("text-[10px] px-1.5 py-0 capitalize", stepTypeColor(step.type))}>
          {step.type}
        </Badge>
      </div>
    </div>
  );
}

// ── SortableStageColumn ───────────────────────────────────────────────────────

function SortableStageColumn({
  stage,
  onUpdateStage,
  onDeleteStage,
  onAddStep,
  onUpdateStep,
  onDeleteStep,
}: {
  stage: LocalStage;
  onUpdateStage: (id: string, patch: Partial<Omit<LocalStage, "steps">>) => void;
  onDeleteStage: (id: string) => void;
  onAddStep: (stageId: string) => void;
  onUpdateStep: (stageId: string, stepId: string, patch: Partial<LocalStep>) => void;
  onDeleteStep: (stageId: string, stepId: string) => void;
}) {
  const {
    attributes,
    listeners,
    setNodeRef: setStageRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: stage.id, data: { kind: "stage" } });

  const stageStyle: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.4 : 1,
  };

  const stepIds = stage.steps.map((s) => s.id);

  return (
    <div ref={setStageRef} style={stageStyle} className="shrink-0 w-72">
      <Card
        className={cn(
          "glass-card flex flex-col h-full border-border/60",
          isDragging && "shadow-2xl border-primary/30"
        )}
      >
        {/* Stage header */}
        <CardHeader className="pb-2 px-3 pt-3 space-y-0">
          <div className="flex items-center gap-2">
            <button
              {...attributes}
              {...listeners}
              className="cursor-grab active:cursor-grabbing touch-none text-muted-foreground/40 hover:text-muted-foreground transition-colors shrink-0"
              tabIndex={-1}
            >
              <GripHorizontal className="w-4 h-4" />
            </button>

            <Input
              value={stage.name}
              onChange={(e) => onUpdateStage(stage.id, { name: e.target.value })}
              className="h-7 text-sm font-semibold border-0 bg-transparent px-0 focus-visible:ring-0 focus-visible:ring-offset-0 flex-1"
              placeholder="Stage name…"
            />

            <button
              onClick={() => onDeleteStage(stage.id)}
              className="text-muted-foreground/30 hover:text-destructive transition-colors shrink-0"
              tabIndex={-1}
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="flex items-center gap-2 pl-6 mt-1">
            <Switch
              id={`parallel-${stage.id}`}
              checked={stage.runParallel}
              onCheckedChange={(v) => onUpdateStage(stage.id, { runParallel: v })}
              className="h-3.5 w-6 [&>span]:h-3 [&>span]:w-3"
            />
            <Label
              htmlFor={`parallel-${stage.id}`}
              className="text-[10px] text-muted-foreground cursor-pointer"
            >
              Run steps in parallel
            </Label>
          </div>
        </CardHeader>

        {/* Steps */}
        <CardContent className="px-3 pb-3 flex-1 space-y-2">
          <SortableContext items={stepIds} strategy={verticalListSortingStrategy}>
            {stage.steps.map((step) => (
              <SortableStepCard
                key={step.id}
                step={step}
                stageId={stage.id}
                onUpdate={(stepId, patch) => onUpdateStep(stage.id, stepId, patch)}
                onDelete={(stepId) => onDeleteStep(stage.id, stepId)}
              />
            ))}
          </SortableContext>

          {stage.steps.length === 0 && (
            <div className="rounded-lg border border-dashed border-border/50 py-5 text-center text-xs text-muted-foreground">
              No steps yet
            </div>
          )}

          <Button
            variant="ghost"
            size="sm"
            className="w-full h-7 text-xs text-muted-foreground hover:text-foreground border border-dashed border-border/40 hover:border-border/80"
            onClick={() => onAddStep(stage.id)}
          >
            <Plus className="w-3 h-3 mr-1" />
            Add Step
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

// ── StageDragOverlay ──────────────────────────────────────────────────────────

function StageDragOverlay({ stage }: { stage: LocalStage }) {
  return (
    <div className="w-72 rounded-xl border border-primary/40 bg-background/90 shadow-2xl p-3 -rotate-1">
      <div className="flex items-center gap-2 mb-3">
        <GripHorizontal className="w-4 h-4 text-muted-foreground/40 shrink-0" />
        <span className="text-sm font-semibold flex-1 truncate">{stage.name || "Unnamed stage"}</span>
        <Badge variant="outline" className="text-[10px]">
          {stage.steps.length} step{stage.steps.length !== 1 ? "s" : ""}
        </Badge>
      </div>
      <div className="space-y-1.5 opacity-50">
        {stage.steps.slice(0, 3).map((s) => (
          <div
            key={s.id}
            className="h-7 rounded-md border border-border/40 bg-muted/30 px-2 flex items-center"
          >
            <span className="text-xs truncate">{s.name || "Unnamed step"}</span>
          </div>
        ))}
        {stage.steps.length > 3 && (
          <span className="text-[10px] text-muted-foreground pl-1">
            +{stage.steps.length - 3} more
          </span>
        )}
      </div>
    </div>
  );
}

// ── Main Page ─────────────────────────────────────────────────────────────────

export default function PipelineBuilderPage() {
  const params = useParams<{ id: string }>();
  const pipelineId = params?.id ?? "";
  const router = useRouter();

  const { data: pipeline, isLoading } = usePipeline(pipelineId);
  const updateStages = useUpdatePipelineStages();

  const [stages, setStages] = useState<LocalStage[]>([]);
  const [activeDrag, setActiveDrag] = useState<ActiveDrag | null>(null);
  const [isDirty, setIsDirty] = useState(false);
  const [yamlContent, setYamlContent] = useState("");
  const [applyingYaml, setApplyingYaml] = useState(false);
  const initializedRef = useRef(false);

  // Populate local state from loaded pipeline (once)
  useEffect(() => {
    if (!pipeline || initializedRef.current) return;
    initializedRef.current = true;
    setStages(
      [...(pipeline.stages ?? [])]
        .sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
        .map((s) => ({
          id: uid(),
          name: s.name,
          runParallel: s.runParallel ?? false,
          steps: (s.steps ?? []).map((st) => ({
            id: uid(),
            name: st.name,
            type: st.type ?? "command",
            command: st.command,
            timeout: st.timeout,
          })),
        }))
    );
  }, [pipeline]);

  const markDirty = useCallback(() => setIsDirty(true), []);

  // ── Stage mutations ──────────────────────────────────────────────────────

  const addStage = useCallback(() => {
    setStages((prev) => [
      ...prev,
      { id: uid(), name: `Stage ${prev.length + 1}`, runParallel: false, steps: [] },
    ]);
    markDirty();
  }, [markDirty]);

  const updateStage = useCallback(
    (id: string, patch: Partial<Omit<LocalStage, "steps">>) => {
      setStages((prev) =>
        prev.map((s) => (s.id === id ? { ...s, ...patch } : s))
      );
      markDirty();
    },
    [markDirty]
  );

  const deleteStage = useCallback(
    (id: string) => {
      setStages((prev) => prev.filter((s) => s.id !== id));
      markDirty();
    },
    [markDirty]
  );

  // ── Step mutations ───────────────────────────────────────────────────────

  const addStep = useCallback(
    (stageId: string) => {
      setStages((prev) =>
        prev.map((s) =>
          s.id === stageId
            ? {
                ...s,
                steps: [
                  ...s.steps,
                  { id: uid(), name: "New Step", type: "command" as const },
                ],
              }
            : s
        )
      );
      markDirty();
    },
    [markDirty]
  );

  const updateStep = useCallback(
    (stageId: string, stepId: string, patch: Partial<LocalStep>) => {
      setStages((prev) =>
        prev.map((s) =>
          s.id === stageId
            ? { ...s, steps: s.steps.map((st) => (st.id === stepId ? { ...st, ...patch } : st)) }
            : s
        )
      );
      markDirty();
    },
    [markDirty]
  );

  const deleteStep = useCallback(
    (stageId: string, stepId: string) => {
      setStages((prev) =>
        prev.map((s) =>
          s.id === stageId ? { ...s, steps: s.steps.filter((st) => st.id !== stepId) } : s
        )
      );
      markDirty();
    },
    [markDirty]
  );

  // ── Save ─────────────────────────────────────────────────────────────────

  const handleSave = async () => {
    for (const stage of stages) {
      if (!stage.name.trim()) {
        toast.error("All stages must have a name.");
        return;
      }
      for (const step of stage.steps) {
        if (!step.name.trim()) {
          toast.error(`All steps in "${stage.name}" must have a name.`);
          return;
        }
      }
    }

    try {
      await updateStages.mutateAsync({
        id: pipelineId,
        stages: stages.map((s) => ({
          name: s.name.trim(),
          runParallel: s.runParallel,
          steps: s.steps.map((st) => ({
            name: st.name.trim(),
            type: st.type,
            command: st.command,
            timeout: st.timeout,
          })),
        })),
      });
      toast.success("Pipeline stages saved.");
      setIsDirty(false);
      router.push(`/pipelines/${pipelineId}`);
    } catch (e: any) {
      toast.error("Failed to save stages", { description: e.message });
    }
  };

  const handleApplyYaml = async () => {
    if (!yamlContent.trim()) { toast.error("Paste your deployflow.yml content first."); return; }
    setApplyingYaml(true);
    try {
      const result: any = await apiClient.post(`/pipelines/${pipelineId}/apply-yaml`, { yamlContent });
      toast.success(`YAML applied — ${result.stageCount} stages, ${result.stepCount} steps`);
      // Reload page to reflect new stages
      router.refresh();
      initializedRef.current = false;
    } catch (e: any) {
      toast.error("Failed to apply YAML", { description: e.message });
    } finally {
      setApplyingYaml(false);
    }
  };

  // ── DnD ──────────────────────────────────────────────────────────────────

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );

  function findStageForStep(stepId: string): string | undefined {
    return stages.find((s) => s.steps.some((st) => st.id === stepId))?.id;
  }

  const onDragStart = useCallback(
    ({ active }: DragStartEvent) => {
      const kind = active.data.current?.kind as "stage" | "step";
      if (kind === "stage") {
        setActiveDrag({ kind: "stage", id: active.id as string });
      } else {
        const stageId = active.data.current?.stageId as string;
        setActiveDrag({ kind: "step", id: active.id as string, stageId });
      }
    },
    []
  );

  const onDragOver = useCallback(
    ({ active, over }: DragOverEvent) => {
      if (!over || activeDrag?.kind !== "step") return;

      const activeId = active.id as string;
      const overId = over.id as string;

      const sourceStageId = findStageForStep(activeId);
      // over could be a stage ID or a step ID
      const overStageId =
        stages.find((s) => s.id === overId)?.id ??
        findStageForStep(overId);

      if (!sourceStageId || !overStageId || sourceStageId === overStageId) return;

      // Move step to the new stage
      setStages((prev) => {
        const srcStage = prev.find((s) => s.id === sourceStageId)!;
        const dstStage = prev.find((s) => s.id === overStageId)!;
        const movingStep = srcStage.steps.find((st) => st.id === activeId)!;

        // Determine insertion index
        const overIsStep = dstStage.steps.some((st) => st.id === overId);
        const insertAt = overIsStep
          ? dstStage.steps.findIndex((st) => st.id === overId)
          : dstStage.steps.length;

        const newSrc = srcStage.steps.filter((st) => st.id !== activeId);
        const newDst = [
          ...dstStage.steps.slice(0, insertAt),
          movingStep,
          ...dstStage.steps.slice(insertAt),
        ];

        return prev.map((s) => {
          if (s.id === sourceStageId) return { ...s, steps: newSrc };
          if (s.id === overStageId) return { ...s, steps: newDst };
          return s;
        });
      });

      // Update activeDrag stageId so onDragEnd can still find it
      setActiveDrag({ kind: "step", id: activeId, stageId: overStageId });
      markDirty();
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [activeDrag, stages, markDirty]
  );

  const onDragEnd = useCallback(
    ({ active, over }: DragEndEvent) => {
      setActiveDrag(null);
      if (!over || active.id === over.id) return;

      const activeId = active.id as string;
      const overId = over.id as string;

      const kind = active.data.current?.kind as "stage" | "step";

      if (kind === "stage") {
        setStages((prev) => {
          const oldIndex = prev.findIndex((s) => s.id === activeId);
          const newIndex = prev.findIndex((s) => s.id === overId);
          if (oldIndex === -1 || newIndex === -1) return prev;
          return arrayMove(prev, oldIndex, newIndex);
        });
        markDirty();
        return;
      }

      // Step reorder within the same stage
      const stageId = findStageForStep(activeId);
      if (!stageId) return;

      const overIsInSameStage = stages
        .find((s) => s.id === stageId)
        ?.steps.some((st) => st.id === overId);
      if (!overIsInSameStage) return; // cross-stage already handled by onDragOver

      setStages((prev) =>
        prev.map((s) => {
          if (s.id !== stageId) return s;
          const oldIdx = s.steps.findIndex((st) => st.id === activeId);
          const newIdx = s.steps.findIndex((st) => st.id === overId);
          if (oldIdx === -1 || newIdx === -1) return s;
          return { ...s, steps: arrayMove(s.steps, oldIdx, newIdx) };
        })
      );
      markDirty();
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [stages, markDirty]
  );

  // ── Active drag overlay items ────────────────────────────────────────────

  const activeDragStage =
    activeDrag?.kind === "stage"
      ? (stages.find((s) => s.id === activeDrag.id) ?? null)
      : null;

  const activeDragStep =
    activeDrag?.kind === "step"
      ? (stages
          .flatMap((s) => s.steps)
          .find((st) => st.id === activeDrag.id) ?? null)
      : null;

  const stageIds = stages.map((s) => s.id);

  // ── Render ───────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-72" />
        <div className="flex gap-4">
          {[...Array(3)].map((_, i) => (
            <Skeleton key={i} className="h-80 w-72 shrink-0" />
          ))}
        </div>
      </div>
    );
  }

  if (!pipeline) {
    return (
      <div className="space-y-4">
        <p className="text-sm text-muted-foreground">Pipeline not found.</p>
        <Button asChild variant="outline" size="sm">
          <Link href="/pipelines">Back to pipelines</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col gap-4">
      {/* Header */}
      <div className="flex items-center justify-between shrink-0">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" asChild>
            <Link href={`/pipelines/${pipelineId}`}>
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-xl font-bold tracking-tight">{pipeline.name}</h1>
            <p className="text-xs text-muted-foreground">
              Stage Builder — drag to reorder stages and steps
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {isDirty && (
            <span className="text-xs text-amber-400 font-medium">Unsaved changes</span>
          )}
          <Button variant="outline" size="sm" asChild>
            <Link href={`/pipelines/${pipelineId}`}>Cancel</Link>
          </Button>
          <Button size="sm" onClick={handleSave} disabled={updateStages.isPending}>
            {updateStages.isPending ? (
              <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-1.5 h-4 w-4" />
            )}
            Save Stages
          </Button>
        </div>
      </div>

      {/* Canvas */}
      <div className="flex-1 overflow-x-auto overflow-y-auto">
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragStart={onDragStart}
          onDragOver={onDragOver}
          onDragEnd={onDragEnd}
        >
          <div className="flex gap-4 pb-6 pt-1 px-1 min-w-max items-start">
            <SortableContext items={stageIds} strategy={horizontalListSortingStrategy}>
              {stages.map((stage) => (
                <SortableStageColumn
                  key={stage.id}
                  stage={stage}
                  onUpdateStage={updateStage}
                  onDeleteStage={deleteStage}
                  onAddStep={addStep}
                  onUpdateStep={updateStep}
                  onDeleteStep={deleteStep}
                />
              ))}
            </SortableContext>

            {/* Add stage button */}
            <div className="shrink-0 w-72">
              <button
                onClick={addStage}
                className={cn(
                  "w-full h-full min-h-[120px] rounded-xl border-2 border-dashed border-border/50",
                  "hover:border-primary/40 hover:bg-primary/5 transition-all",
                  "flex flex-col items-center justify-center gap-2 text-muted-foreground hover:text-primary"
                )}
              >
                <Plus className="w-6 h-6" />
                <span className="text-sm font-medium">Add Stage</span>
              </button>
            </div>
          </div>

          <DragOverlay dropAnimation={{ duration: 200, easing: "cubic-bezier(0.18,0.67,0.6,1.22)" }}>
            {activeDragStage && <StageDragOverlay stage={activeDragStage} />}
            {activeDragStep && <StepDragOverlay step={activeDragStep} />}
          </DragOverlay>
        </DndContext>
      </div>

      {stages.length === 0 && (
        <div className="flex-1 flex flex-col items-center justify-center gap-3 text-center">
          <div className="w-14 h-14 rounded-2xl bg-muted flex items-center justify-center">
            <Settings2 className="w-7 h-7 text-muted-foreground" />
          </div>
          <div>
            <p className="text-sm font-medium">No stages yet</p>
            <p className="text-xs text-muted-foreground mt-0.5">Add a stage to start building your pipeline</p>
          </div>
          <Button variant="outline" size="sm" onClick={addStage}>
            <Plus className="w-4 h-4 mr-1.5" />
            Add First Stage
          </Button>
        </div>
      )}

      {/* YAML Import Section */}
      <div className="p-4 border-t border-border/50 space-y-2">
        <div className="flex items-center gap-2 mb-2">
          <FileCode className="h-4 w-4 text-muted-foreground" />
          <span className="text-sm font-medium">Import from deployflow.yml</span>
          <span className="text-xs text-muted-foreground ml-1">— paste YAML to replace all stages</span>
        </div>
        <Textarea
          placeholder={`version: "1"\nname: my-pipeline\nstages:\n  - name: install\n    steps:\n      - name: npm ci\n        command: npm ci\n  - name: test\n    depends_on: [install]\n    steps:\n      - name: run tests\n        command: npm test`}
          value={yamlContent}
          onChange={(e) => setYamlContent(e.target.value)}
          rows={8}
          className="font-mono text-xs resize-none"
        />
        <Button
          size="sm" variant="outline" onClick={handleApplyYaml}
          disabled={applyingYaml || !yamlContent.trim()}
          className="w-full"
        >
          {applyingYaml ? <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" /> : <FileCode className="mr-1.5 h-3.5 w-3.5" />}
          {applyingYaml ? "Applying…" : "Apply YAML"}
        </Button>
      </div>
    </div>
  );
}
