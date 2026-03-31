"use client";

import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { useAuthStore } from "@/store/auth-store";
import { clearCachedTokens } from "@/lib/api-client";

/** Warn user when fewer than this many ms remain on the access token. */
const WARN_BEFORE_MS = 5 * 60 * 1000; // 5 minutes

/**
 * Decode JWT payload (without verification) only to read the `exp` claim.
 * Returns expiry as a JS timestamp (ms), or null if the token can't be parsed.
 */
function getTokenExpiry(token: string): number | null {
  try {
    // Base64url → Base64 → JSON
    const b64 = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
    const payload = JSON.parse(atob(b64));
    return typeof payload.exp === "number" ? payload.exp * 1000 : null;
  } catch {
    return null;
  }
}

/**
 * Mounts invisibly inside the authenticated layout.
 * Watches the JWT access token and:
 *  - Shows a warning toast ~5 min before expiry.
 *  - Redirects to /login on expiry (with an error toast).
 * Timers are reset whenever accessToken changes (e.g. after a silent refresh).
 */
export function TokenExpiryWatcher() {
  const router = useRouter();
  const { accessToken, isAuthenticated, logout } = useAuthStore();
  const warnTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const expireTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const warnToastIdRef = useRef<string | number | null>(null);

  useEffect(() => {
    // Clear previous timers whenever token changes
    if (warnTimerRef.current) clearTimeout(warnTimerRef.current);
    if (expireTimerRef.current) clearTimeout(expireTimerRef.current);

    if (!isAuthenticated || !accessToken) return;

    const expiry = getTokenExpiry(accessToken);
    if (!expiry) return;

    const now = Date.now();
    const msUntilExpiry = expiry - now;

    // Already expired — clean up immediately
    if (msUntilExpiry <= 0) {
      logout();
      clearCachedTokens();
      toast.error("Session expired", {
        description: "Your session has expired. Please log in again.",
      });
      router.replace("/login");
      return;
    }

    // Schedule warning (5 min before expiry, or immediately if < 5 min left)
    const warnDelay = Math.max(0, msUntilExpiry - WARN_BEFORE_MS);
    warnTimerRef.current = setTimeout(() => {
      const minutesLeft = Math.max(1, Math.ceil((expiry - Date.now()) / 60_000));
      warnToastIdRef.current = toast.warning("Session expiring soon", {
        description: `Your session will expire in ~${minutesLeft} minute${minutesLeft !== 1 ? "s" : ""}. You will be redirected to the login page.`,
        duration: WARN_BEFORE_MS,
      }) as string | number;
    }, warnDelay);

    // Schedule logout + redirect at actual expiry
    expireTimerRef.current = setTimeout(() => {
      if (warnToastIdRef.current !== null) {
        toast.dismiss(warnToastIdRef.current);
        warnToastIdRef.current = null;
      }
      logout();
      clearCachedTokens();
      toast.error("Session expired", {
        description: "Your session has expired. Redirecting to the login page...",
        duration: 4000,
      });
      router.replace("/login");
    }, msUntilExpiry);

    return () => {
      if (warnTimerRef.current) clearTimeout(warnTimerRef.current);
      if (expireTimerRef.current) clearTimeout(expireTimerRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken, isAuthenticated]);

  return null;
}
