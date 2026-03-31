"use client";

import { useState, useRef, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { motion, AnimatePresence } from "framer-motion";
import {
  Bot,
  Send,
  Sparkles,
  Server,
  Rocket,
  AlertTriangle,
  Copy,
  ThumbsUp,
  ThumbsDown,
  User,
  Zap,
  ArrowLeft,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { cn } from "@/lib/utils";
import { toast } from "sonner";
import ReactMarkdown from "react-markdown";
import { useAiChat } from "@/hooks/use-api";

interface Message {
  id: string;
  role: "user" | "assistant";
  content: string;
  timestamp: Date;
  thinking?: boolean;
  quickReplies?: string[];
}

const suggestedPrompts = [
  { icon: Server, text: "Suggest optimal server sizing for my Node.js API handling 10k req/min" },
  { icon: AlertTriangle, text: "Analyze my deployment logs and identify the root cause of failures" },
  { icon: Rocket, text: "Create a production-ready Docker Compose for a Next.js + PostgreSQL app" },
  { icon: Zap, text: "Review my CI/CD pipeline and suggest performance improvements" },
];

const mockResponses = [
  `Based on your current usage patterns, here's my recommendation for optimal server sizing:

**For a Node.js API handling 10k req/min:**

**Production Setup:**
- **Primary:** 2x t3.medium (2 vCPU, 4GB RAM) behind a load balancer
- **Database:** t3.small RDS PostgreSQL with Multi-AZ
- **Cache:** cache.t3.micro ElastiCache Redis

**Estimated Monthly Cost:** ~$120-180/month

**Key configurations:**
- Enable horizontal auto-scaling at 70% CPU threshold
- Use connection pooling (PgBouncer) with max 100 connections
- Set Node.js cluster mode with \`--max-old-space-size=2048\`

Would you like me to generate the Terraform configuration for this setup?`,
  `I've analyzed your deployment log patterns. Here's what I found:

**Root Cause:** Health check timeout on port 3000 before the app finishes initialization

**Evidence from logs:**
\`\`\`
[ERROR] Health check failed: GET /health returned 503
[INFO] Server initializing database connection...
[WARN] Connection pool taking longer than expected (12s)
\`\`\`

**Fix:** Increase your healthcheck start period to 30s:
\`\`\`yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:3000/health"]
  interval: 10s
  timeout: 5s
  retries: 3
  start_period: 30s  # ← Add this
\`\`\`

**Risk level:** Low — this is a configuration issue, not a code bug.`,
];

export default function AIAssistantPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const fromDeploymentId = searchParams.get("id");
  const fromContext = searchParams.get("context");
  const [messages, setMessages] = useState<Message[]>([
    {
      id: "welcome",
      role: "assistant",
      content: "Hello! I'm your DeployFlow AI assistant. I can help you with deployment optimization, infrastructure planning, log analysis, and DevOps best practices. What would you like help with today?",
      timestamp: new Date(),
      quickReplies: ["How do I optimize my deployment?", "Analyze recent failures", "Best practices for Docker"],
    },
  ]);
  const [input, setInput] = useState("");
  const [isTyping, setIsTyping] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const aiChat = useAiChat();

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  const sendMessage = async (text: string) => {
    if (!text.trim() || isTyping) return;

    const userMsg: Message = {
      id: `user-${Date.now()}`,
      role: "user",
      content: text,
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMsg]);
    setInput("");
    setIsTyping(true);

    try {
      const result = await aiChat.mutateAsync({ message: text });
      const assistantMsg: Message = {
        id: `assistant-${Date.now()}`,
        role: "assistant",
        content: result.reply,
        timestamp: new Date(),
        quickReplies: result.quickReplies,
      };
      setMessages((prev) => [...prev, assistantMsg]);
    } catch {
      toast.error("Failed to get AI response");
    } finally {
      setIsTyping(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      sendMessage(input);
    }
  };

  return (
    <div className="mx-auto flex h-[calc(100vh-7rem)] max-w-4xl flex-col">
      {/* Header */}
      <div className="mb-4 flex shrink-0 items-center gap-3">
        {(fromDeploymentId || fromContext) && (
          <Button
            variant="ghost"
            size="sm"
            className="h-8 w-8 shrink-0 rounded-lg p-0 text-muted-foreground hover:text-foreground"
            onClick={() =>
              fromDeploymentId
                ? router.push(`/deployments/${fromDeploymentId}`)
                : router.back()
            }
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
        )}
        <div className="flex h-10 w-10 items-center justify-center rounded-xl border border-purple-500/20 bg-gradient-to-br from-purple-500/15 to-blue-500/15">
          <Bot className="h-5 w-5 text-purple-400" />
        </div>
        <div className="min-w-0">
          <h1 className="flex items-center gap-2 text-lg font-bold tracking-tight">
            AI Assistant
            <Badge variant="outline" className="border-purple-500/30 bg-purple-500/10 text-purple-400 text-[10px] px-1.5 py-0">
              <Sparkles className="mr-1 h-2.5 w-2.5" />BETA
            </Badge>
          </h1>
          <p className="text-xs text-muted-foreground">
            Intelligent DevOps assistant powered by AI
          </p>
        </div>
      </div>

      {/* Chat Area */}
      <div className="glass-card flex min-h-0 flex-1 flex-col overflow-hidden">
        <ScrollArea className="flex-1 px-4 py-4">
          <div className="space-y-4">
            {messages.map((msg) => (
              <MessageBubble key={msg.id} message={msg} onQuickReply={sendMessage} />
            ))}

            {/* Typing indicator */}
            <AnimatePresence>
              {isTyping && (
                <motion.div
                  initial={{ opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0 }}
                  className="flex gap-3"
                >
                  <div className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full border border-purple-500/20 bg-purple-500/10">
                    <Bot className="h-3.5 w-3.5 text-purple-400" />
                  </div>
                  <div className="rounded-xl bg-muted/50 px-4 py-3 text-sm">
                    <div className="flex h-5 items-center gap-1">
                      {[0, 0.2, 0.4].map((delay) => (
                        <motion.span
                          key={delay}
                          className="h-1.5 w-1.5 rounded-full bg-muted-foreground/60"
                          animate={{ y: [0, -4, 0] }}
                          transition={{ repeat: Infinity, duration: 0.8, delay }}
                        />
                      ))}
                    </div>
                  </div>
                </motion.div>
              )}
            </AnimatePresence>

            <div ref={bottomRef} />
          </div>
        </ScrollArea>

        {/* Suggested Prompts (only on welcome) */}
        {messages.length === 1 && (
          <div className="border-t border-border/40 px-4 py-3">
            <p className="mb-2 text-[11px] font-medium text-muted-foreground/60">Suggested prompts</p>
            <div className="grid grid-cols-1 gap-1.5 sm:grid-cols-2">
              {suggestedPrompts.map((prompt) => (
                <button
                  key={prompt.text}
                  onClick={() => sendMessage(prompt.text)}
                  className="flex items-start gap-2.5 rounded-lg border border-border/40 bg-muted/20 p-2.5 text-left text-xs text-muted-foreground transition-colors hover:bg-muted/40 hover:text-foreground"
                >
                  <prompt.icon className="mt-0.5 h-3.5 w-3.5 shrink-0 text-primary/70" />
                  <span className="leading-relaxed">{prompt.text}</span>
                </button>
              ))}
            </div>
          </div>
        )}

        {/* Input */}
        <div className="flex items-center gap-2 border-t border-border/40 px-4 py-3">
          <input
            ref={inputRef}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Ask about deployments, infrastructure, logs..."
            className="h-9 flex-1 rounded-lg border border-border/50 bg-background px-3 text-sm placeholder:text-muted-foreground/50 focus:outline-none focus:ring-1 focus:ring-ring disabled:opacity-50"
            disabled={isTyping}
          />
          <Button
            onClick={() => sendMessage(input)}
            disabled={!input.trim() || isTyping}
            size="sm"
            className="h-9 w-9 shrink-0 rounded-lg p-0"
          >
            <Send className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}

function MessageBubble({ message, onQuickReply }: { message: Message; onQuickReply?: (text: string) => void }) {
  const isUser = message.role === "user";
  const [feedback, setFeedback] = useState<"up" | "down" | null>(null);

  const handleCopy = () => {
    navigator.clipboard.writeText(message.content);
    toast.success("Copied to clipboard");
  };

  const handleFeedback = (value: "up" | "down") => {
    setFeedback(value);
    toast.success(value === "up" ? "Thanks for the feedback!" : "Got it — we'll improve this response.");
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      className={cn("flex gap-3", isUser && "flex-row-reverse")}
    >
      {/* Avatar */}
      <div
        className={cn(
          "mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full",
          isUser
            ? "bg-primary/10 border border-primary/20"
            : "bg-purple-500/10 border border-purple-500/20"
        )}
      >
        {isUser ? (
          <User className="h-3.5 w-3.5 text-primary" />
        ) : (
          <Bot className="h-3.5 w-3.5 text-purple-400" />
        )}
      </div>

      {/* Bubble */}
      <div className={cn("group max-w-[80%]", isUser && "flex flex-col items-end")}>
        <div
          className={cn(
            "rounded-xl px-3.5 py-2.5 text-sm leading-relaxed",
            isUser
              ? "bg-primary text-primary-foreground"
              : "bg-muted/40 border border-border/40"
          )}
        >
          {isUser ? (
            <p>{message.content}</p>
          ) : (
            <div className="prose prose-sm dark:prose-invert max-w-none prose-p:my-1.5 prose-pre:my-2 prose-pre:bg-background/50 prose-pre:border prose-pre:border-border/50 prose-pre:rounded-lg prose-code:text-primary prose-code:bg-primary/8 prose-code:px-1 prose-code:rounded prose-code:text-[13px] prose-strong:text-foreground prose-ul:my-1.5 prose-li:my-0.5">
              <ReactMarkdown>{message.content}</ReactMarkdown>
            </div>
          )}
        </div>

        {/* Actions */}
        {!isUser && (
          <div className="mt-1 flex items-center gap-0.5 opacity-0 transition-opacity group-hover:opacity-100">
            <Button variant="ghost" size="sm" className="h-6 w-6 rounded-md p-0" onClick={handleCopy}>
              <Copy className="h-3 w-3" />
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className={cn("h-6 w-6 rounded-md p-0", feedback === "up" && "text-emerald-400 bg-emerald-400/10")}
              onClick={() => handleFeedback("up")}
              disabled={feedback !== null}
            >
              <ThumbsUp className="h-3 w-3" />
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className={cn("h-6 w-6 rounded-md p-0", feedback === "down" && "text-red-400 bg-red-400/10")}
              onClick={() => handleFeedback("down")}
              disabled={feedback !== null}
            >
              <ThumbsDown className="h-3 w-3" />
            </Button>
            <span className="ml-1 text-[10px] text-muted-foreground/50">
              {message.timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </span>
          </div>
        )}

        {/* Quick replies */}
        {!isUser && message.quickReplies && message.quickReplies.length > 0 && onQuickReply && (
          <div className="mt-2 flex flex-wrap gap-1.5">
            {message.quickReplies.map((qr) => (
              <button
                key={qr}
                onClick={() => onQuickReply(qr)}
                className="rounded-full border border-border/40 bg-muted/30 px-2.5 py-1 text-[11px] text-muted-foreground transition-colors hover:bg-muted/60 hover:text-foreground"
              >
                {qr}
              </button>
            ))}
          </div>
        )}
      </div>
    </motion.div>
  );
}
