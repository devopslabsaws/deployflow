"use client";

import { useEffect, useRef, useState } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { motion, AnimatePresence } from "framer-motion";

export function NavigationProgress() {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [progress, setProgress] = useState(0);
  const [visible, setVisible] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const clear = () => {
    if (timerRef.current) clearTimeout(timerRef.current);
    if (intervalRef.current) clearInterval(intervalRef.current);
  };

  const start = () => {
    clear();
    setProgress(10);
    setVisible(true);
    let current = 10;
    intervalRef.current = setInterval(() => {
      // Ease toward 85% — never quite reaches 100 until done
      current += (85 - current) * 0.1;
      setProgress(current);
    }, 200);
  };

  const done = () => {
    clear();
    setProgress(100);
    timerRef.current = setTimeout(() => {
      setVisible(false);
      setProgress(0);
    }, 400);
  };

  const prevRef = useRef({ pathname, searchParams: searchParams?.toString() ?? "" });

  useEffect(() => {
    const prev = prevRef.current;
    const next = { pathname, searchParams: searchParams?.toString() ?? "" };

    if (prev.pathname !== next.pathname || prev.searchParams !== next.searchParams) {
      start();
      // Treat as complete after a short delay (App Router doesn't expose navigation events)
      timerRef.current = setTimeout(done, 350);
    }

    prevRef.current = next;

    return clear;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname, searchParams]);

  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          className="fixed top-0 left-0 right-0 z-[9999] h-[2.5px] origin-left"
          style={{ background: "hsl(var(--primary))" }}
          initial={{ scaleX: 0, opacity: 1 }}
          animate={{ scaleX: progress / 100 }}
          exit={{ opacity: 0 }}
          transition={{ ease: "easeOut", duration: 0.2 }}
        />
      )}
    </AnimatePresence>
  );
}
