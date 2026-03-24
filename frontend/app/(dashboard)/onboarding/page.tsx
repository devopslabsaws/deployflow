"use client";

import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  CheckCircle2,
  Circle,
  GitBranch,
  Rocket,
  Settings2,
  Server,
  Zap,
  ChevronRight,
  ChevronLeft,
  Upload,
  Code2,
  Loader2,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useDetectStack, useApplyStack, type DetectedStackDto } from "@/hooks/use-api";
import { useProjects, useServers } from "@/hooks/use-api";
import { toast } from "sonner";
import { cn } from "@/lib/utils";

const steps = [
  { id: 1, label: "Connect Repo", icon: GitBranch },
  { id: 2, label: "Detect Stack", icon: Code2 },
  { id: 3, label: "Environment", icon: Settings2 },
  { id: 4, label: "Choose Server", icon: Server },
  { id: 5, label: "Deploy!", icon: Rocket },
];

const FRAMEWORK_COLORS: Record<string, string> = {
  Nextjs: "bg-black text-white",
  React: "bg-blue-500 text-white",
  Vue: "bg-green-500 text-white",
  DotnetWebApi: "bg-purple-600 text-white",
  FastApi: "bg-teal-500 text-white",
  Django: "bg-green-700 text-white",
  Go: "bg-cyan-500 text-white",
  Static: "bg-gray-500 text-white",
};

export default function OnboardingPage() {
  const [currentStep, setCurrentStep] = useState(1);
  const [repoUrl, setRepoUrl] = useState("");
  const [fileList, setFileList] = useState("");
  const [packageJson, setPackageJson] = useState("");
  const [detectedStack, setDetectedStack] = useState<DetectedStackDto | null>(null);
  const [selectedProjectId, setSelectedProjectId] = useState("");
  const [envVars, setEnvVars] = useState<Record<string, string>>({});

  const detectStack = useDetectStack();
  const { data: projects } = useProjects();
  const { data: servers } = useServers();

  function handleDetect() {
    const fileNames = fileList
      .split("\n")
      .map((f) => f.trim())
      .filter(Boolean);
    if (!fileNames.length) {
      toast.error("Enter at least one filename");
      return;
    }
    detectStack.mutate(
      { fileNames, packageJsonContent: packageJson || undefined },
      {
        onSuccess: (stack) => {
          setDetectedStack(stack);
          // Pre-populate suggested env vars
          const initial: Record<string, string> = {};
          (stack.suggestedEnvVars ?? []).forEach((k) => {
            initial[k] = "";
          });
          setEnvVars(initial);
          setCurrentStep(3);
          toast.success(`Detected: ${stack.framework}`);
        },
        onError: () => toast.error("Detection failed"),
      }
    );
  }

  const applyStack = useApplyStack(selectedProjectId);
  function handleApply() {
    if (!selectedProjectId || !detectedStack) return;
    applyStack.mutate(
      { framework: detectedStack.framework },
      {
        onSuccess: () => {
          setCurrentStep(5);
          toast.success("Stack settings applied to project");
        },
        onError: () => toast.error("Failed to apply stack"),
      }
    );
  }

  const progressPercent = ((currentStep - 1) / (steps.length - 1)) * 100;

  return (
    <div className="max-w-3xl mx-auto py-10 px-4 space-y-8">
      {/* Header */}
      <div className="text-center space-y-2">
        <h1 className="text-3xl font-bold">Welcome to DeployFlow</h1>
        <p className="text-muted-foreground">
          Let&apos;s get your first project deployed in minutes.
        </p>
      </div>

      {/* Step Indicator */}
      <div className="relative flex items-center justify-between px-2">
        <div className="absolute left-0 right-0 top-1/2 h-0.5 bg-muted -translate-y-1/2" />
        <div
          className="absolute left-0 top-1/2 h-0.5 bg-primary -translate-y-1/2 transition-all duration-500"
          style={{ width: `${progressPercent}%` }}
        />
        {steps.map((step) => {
          const Icon = step.icon;
          const done = currentStep > step.id;
          const active = currentStep === step.id;
          return (
            <div key={step.id} className="relative z-10 flex flex-col items-center gap-1">
              <button
                onClick={() => done && setCurrentStep(step.id)}
                className={cn(
                  "w-10 h-10 rounded-full flex items-center justify-center border-2 transition-all",
                  done && "bg-primary border-primary text-primary-foreground cursor-pointer",
                  active && "bg-background border-primary text-primary",
                  !done && !active && "bg-muted border-muted-foreground/30 text-muted-foreground"
                )}
              >
                {done ? <CheckCircle2 className="w-5 h-5" /> : <Icon className="w-4 h-4" />}
              </button>
              <span
                className={cn(
                  "text-xs font-medium hidden sm:block",
                  active ? "text-primary" : "text-muted-foreground"
                )}
              >
                {step.label}
              </span>
            </div>
          );
        })}
      </div>

      {/* Step Content */}
      <AnimatePresence mode="wait">
        <motion.div
          key={currentStep}
          initial={{ opacity: 0, x: 30 }}
          animate={{ opacity: 1, x: 0 }}
          exit={{ opacity: 0, x: -30 }}
          transition={{ duration: 0.2 }}
        >
          {/* Step 1 – Connect Repo */}
          {currentStep === 1 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <GitBranch className="w-5 h-5" /> Connect your repository
                </CardTitle>
                <CardDescription>Enter your git repository URL to get started.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="repo">Repository URL</Label>
                  <Input
                    id="repo"
                    placeholder="https://github.com/org/repo"
                    value={repoUrl}
                    onChange={(e) => setRepoUrl(e.target.value)}
                  />
                </div>
                <Button
                  className="w-full"
                  disabled={!repoUrl}
                  onClick={() => setCurrentStep(2)}
                >
                  Continue <ChevronRight className="ml-2 w-4 h-4" />
                </Button>
              </CardContent>
            </Card>
          )}

          {/* Step 2 – Detect Stack */}
          {currentStep === 2 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Code2 className="w-5 h-5" /> Auto-detect your stack
                </CardTitle>
                <CardDescription>
                  List the files in your project root (one per line) and optionally paste
                  your package.json. DeployFlow will auto-generate a production Dockerfile.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label>Root files (one per line)</Label>
                  <Textarea
                    placeholder={"package.json\nnext.config.js\ntsconfig.json\npublic/"}
                    rows={5}
                    value={fileList}
                    onChange={(e) => setFileList(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>package.json contents (optional)</Label>
                  <Textarea
                    placeholder='{"dependencies":{"next":"14.0.0"}}'
                    rows={3}
                    value={packageJson}
                    onChange={(e) => setPackageJson(e.target.value)}
                    className="font-mono text-xs"
                  />
                </div>
                <div className="flex gap-2">
                  <Button variant="outline" onClick={() => setCurrentStep(1)}>
                    <ChevronLeft className="mr-2 w-4 h-4" /> Back
                  </Button>
                  <Button
                    className="flex-1"
                    onClick={handleDetect}
                    disabled={detectStack.isPending}
                  >
                    {detectStack.isPending ? (
                      <Loader2 className="mr-2 w-4 h-4 animate-spin" />
                    ) : (
                      <Zap className="mr-2 w-4 h-4" />
                    )}
                    Detect Stack
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Step 3 – Environment Variables */}
          {currentStep === 3 && detectedStack && (
            <Card>
              <CardHeader>
                <div className="flex items-start justify-between">
                  <div>
                    <CardTitle className="flex items-center gap-2">
                      <Settings2 className="w-5 h-5" /> Configure environment
                    </CardTitle>
                    <CardDescription className="mt-1">{detectedStack.explanation}</CardDescription>
                  </div>
                  <Badge
                    className={FRAMEWORK_COLORS[detectedStack.framework] ?? "bg-muted text-foreground"}
                  >
                    {detectedStack.framework}
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                {detectedStack.suggestedEnvVars?.length > 0 && (
                  <div className="space-y-3">
                    <p className="text-sm font-medium text-muted-foreground">
                      Suggested environment variables
                    </p>
                    {detectedStack.suggestedEnvVars.map((key) => (
                      <div key={key} className="flex items-center gap-2">
                        <Label className="w-56 shrink-0 font-mono text-xs">{key}</Label>
                        <Input
                          placeholder="value"
                          value={envVars[key] ?? ""}
                          onChange={(e) =>
                            setEnvVars((prev) => ({ ...prev, [key]: e.target.value }))
                          }
                        />
                      </div>
                    ))}
                  </div>
                )}
                <details className="text-xs">
                  <summary className="cursor-pointer text-muted-foreground hover:text-foreground">
                    View generated Dockerfile
                  </summary>
                  <pre className="mt-2 p-3 bg-muted rounded text-xs overflow-auto max-h-64 whitespace-pre-wrap">
                    {detectedStack.dockerfileContent}
                  </pre>
                </details>
                <div className="flex gap-2">
                  <Button variant="outline" onClick={() => setCurrentStep(2)}>
                    <ChevronLeft className="mr-2 w-4 h-4" /> Back
                  </Button>
                  <Button className="flex-1" onClick={() => setCurrentStep(4)}>
                    Continue <ChevronRight className="ml-2 w-4 h-4" />
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Step 4 – Choose Server and Link Project */}
          {currentStep === 4 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Server className="w-5 h-5" /> Link to project &amp; server
                </CardTitle>
                <CardDescription>
                  Select an existing project to apply the detected stack settings.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label>Project</Label>
                  <div className="grid gap-2">
                    {(projects?.data ?? []).slice(0, 8).map((p) => (
                      <button
                        key={p.id}
                        onClick={() => setSelectedProjectId(p.id)}
                        className={cn(
                          "text-left px-4 py-3 rounded-lg border transition-all text-sm",
                          selectedProjectId === p.id
                            ? "border-primary bg-primary/5"
                            : "border-border hover:border-primary/50"
                        )}
                      >
                        <span className="font-medium">{p.name}</span>
                        {p.repositoryUrl && (
                          <span className="ml-2 text-muted-foreground text-xs truncate">
                            {p.repositoryUrl}
                          </span>
                        )}
                      </button>
                    ))}
                  </div>
                </div>
                <div className="flex gap-2">
                  <Button variant="outline" onClick={() => setCurrentStep(3)}>
                    <ChevronLeft className="mr-2 w-4 h-4" /> Back
                  </Button>
                  <Button
                    className="flex-1"
                    disabled={!selectedProjectId || applyStack.isPending}
                    onClick={handleApply}
                  >
                    {applyStack.isPending ? (
                      <Loader2 className="mr-2 w-4 h-4 animate-spin" />
                    ) : (
                      <Upload className="mr-2 w-4 h-4" />
                    )}
                    Apply &amp; Deploy
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Step 5 – Done */}
          {currentStep === 5 && (
            <Card className="text-center">
              <CardContent className="py-12 space-y-4">
                <div className="flex justify-center">
                  <div className="w-20 h-20 rounded-full bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
                    <Rocket className="w-10 h-10 text-green-600 dark:text-green-400" />
                  </div>
                </div>
                <h2 className="text-2xl font-bold">You&apos;re all set!</h2>
                <p className="text-muted-foreground max-w-sm mx-auto">
                  Your stack settings have been applied. Head to your project to trigger the first
                  deployment.
                </p>
                <div className="flex justify-center gap-3 pt-2">
                  <Button variant="outline" onClick={() => setCurrentStep(1)}>
                    Start over
                  </Button>
                  <Button asChild>
                    <a href="/projects">Go to Projects</a>
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}
        </motion.div>
      </AnimatePresence>
    </div>
  );
}
