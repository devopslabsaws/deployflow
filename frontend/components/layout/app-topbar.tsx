"use client";

import { usePathname, useRouter } from "next/navigation";
import { Bell, Search, Settings, User, Moon, Sun, Plus, Menu } from "lucide-react";
import { useTheme } from "next-themes";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem,
  DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  CommandDialog, CommandEmpty, CommandGroup,
  CommandInput, CommandItem, CommandList,
} from "@/components/ui/command";
// Avatar replaced with inline gradient circle to match reference design
import { useEffect, useState } from "react";
import { useAuthStore } from "@/store/auth-store";

const PAGE_TITLES: Record<string, string> = {
  dashboard: "Dashboard",
  projects: "Projects",
  deployments: "Deployments",
  servers: "Servers",
  logs: "Logs",
  pipelines: "Pipelines",
  databases: "Databases",
  monitoring: "Monitoring",
  settings: "Settings",
  team: "Team",
  security: "Security",
  costs: "Cost Monitor",
  alerts: "Alerts",
  containers: "Containers",
  domains: "Domains",
  volumes: "Volumes",
  services: "Services",
  "ai-assistant": "AI Assistant",
};

export function AppTopbar({ onMobileMenuToggle }: { onMobileMenuToggle?: () => void }) {
  const pathname = usePathname();
  const router = useRouter();
  const { resolvedTheme, setTheme } = useTheme();
  const [commandOpen, setCommandOpen] = useState(false);
  const [mounted, setMounted] = useState(false);
  const { user, logout } = useAuthStore();

  useEffect(() => { setMounted(true); }, []);

  const isDark = resolvedTheme === "dark";

  const segments = pathname.split("/").filter(Boolean);
  const pageKey = segments[segments.length - 1] ?? "dashboard";
  const pageTitle = PAGE_TITLES[pageKey] ?? pageKey.charAt(0).toUpperCase() + pageKey.slice(1);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === "k" && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        setCommandOpen((o) => !o);
      }
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, []);

  return (
    <>
      <header className="flex h-[60px] w-full shrink-0 items-center justify-between border-b border-border bg-card px-4 md:px-6">
        {/* Left: mobile toggle + page title */}
        <div className="flex items-center gap-3">
          <Button
            variant="ghost"
            size="icon"
            className="h-9 w-9 md:hidden"
            onClick={onMobileMenuToggle}
          >
            <Menu className="h-4.5 w-4.5" />
          </Button>
          <h1 className="text-lg font-bold text-foreground tracking-tight">{pageTitle}</h1>
        </div>

        {/* Right: actions */}
        <div className="flex items-center gap-2">
          {/* Search bar — styled like reference */}
          <div
            className="hidden md:flex items-center gap-2 h-9 w-[220px] rounded-lg border border-border bg-background px-3 cursor-text transition-colors focus-within:border-primary"
            onClick={() => setCommandOpen(true)}
          >
            <Search className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
            <span className="flex-1 text-sm text-muted-foreground select-none">Search…</span>
            <kbd className="pointer-events-none h-5 select-none items-center gap-1 rounded border border-border bg-muted px-1.5 font-mono text-[10px] text-muted-foreground hidden sm:flex">
              ⌘K
            </kbd>
          </div>

          <div className="hidden h-5 w-px bg-border md:block" />

          <Button
            size="sm"
            className="hidden h-9 gap-1.5 rounded-lg px-4 text-[13px] font-semibold md:flex"
            onClick={() => router.push("/deployments")}
          >
            <Plus className="h-3.5 w-3.5" />
            Deploy
          </Button>

          <div className="hidden h-5 w-px bg-border md:block" />

          <Button
            variant="ghost"
            size="icon"
            className="h-9 w-9 rounded-lg"
            onClick={() => setTheme(isDark ? "light" : "dark")}
            title={isDark ? "Switch to light mode" : "Switch to dark mode"}
          >
            {!mounted ? (
              <span className="h-4 w-4" />
            ) : isDark ? (
              <Sun className="h-4 w-4" />
            ) : (
              <Moon className="h-4 w-4" />
            )}
          </Button>

          <Button
            variant="ghost"
            size="icon"
            className="relative h-9 w-9 rounded-lg"
            onClick={() => router.push("/alerts")}
          >
            <Bell className="h-4 w-4" />
            <span className="absolute right-1.5 top-1.5 flex h-[7px] w-[7px] rounded-full bg-destructive ring-2 ring-card" />
          </Button>

          <div className="hidden h-5 w-px bg-border md:block" />

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <button className="flex h-8 w-8 items-center justify-center rounded-full bg-gradient-to-br from-primary to-violet-500 text-[13px] font-bold text-white cursor-pointer shrink-0">
                {user?.name?.charAt(0)?.toUpperCase() ?? "U"}
              </button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="z-50 w-52">
              <DropdownMenuLabel className="font-normal">
                <p className="text-sm font-semibold">{user?.name ?? "User"}</p>
                <p className="text-xs text-muted-foreground">{user?.email ?? ""}</p>
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => router.push("/settings")}>
                <User className="mr-2 h-4 w-4" />
                Profile
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => router.push("/settings")}>
                <Settings className="mr-2 h-4 w-4" />
                Settings
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                className="text-destructive focus:text-destructive"
                onClick={() => { logout(); router.push("/login"); }}
              >
                Sign out
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </header>

      {/* Command palette */}
      <CommandDialog open={commandOpen} onOpenChange={setCommandOpen}>
        <CommandInput placeholder="Search pages, actions..." />
        <CommandList>
          <CommandEmpty>No results found.</CommandEmpty>
          <CommandGroup heading="Navigate">
            {Object.entries(PAGE_TITLES).map(([key, label]) => (
              <CommandItem
                key={key}
                onSelect={() => { router.push(`/${key}`); setCommandOpen(false); }}
              >
                {label}
              </CommandItem>
            ))}
          </CommandGroup>
          <CommandGroup heading="Actions">
            <CommandItem onSelect={() => { router.push("/deployments"); setCommandOpen(false); }}>
              New deployment
            </CommandItem>
            <CommandItem onSelect={() => { router.push("/servers"); setCommandOpen(false); }}>
              Add server
            </CommandItem>
          </CommandGroup>
        </CommandList>
      </CommandDialog>
    </>
  );
}
