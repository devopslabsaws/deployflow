import { useState, useCallback, useRef } from "react";

export type FeedbackState = "idle" | "loading" | "success" | "error";

interface UseActionFeedbackOptions {
  /** How long to show the success/error state before returning to idle (ms). Default 2000/3000. */
  successDuration?: number;
  errorDuration?: number;
}

interface UseActionFeedbackReturn<T> {
  state: FeedbackState;
  run: () => Promise<T | undefined>;
  reset: () => void;
}

/**
 * Wraps an async action with loading/success/error state, auto-resetting to idle.
 *
 * Usage:
 *   const feedback = useActionFeedback(() => deployMutation.mutateAsync());
 *   <button onClick={feedback.run} disabled={feedback.state === "loading"}>
 *     {feedback.state === "success" ? "Deployed!" : "Deploy Now"}
 *   </button>
 */
export function useActionFeedback<T = void>(
  action: () => Promise<T>,
  options: UseActionFeedbackOptions = {},
): UseActionFeedbackReturn<T> {
  const { successDuration = 2000, errorDuration = 3000 } = options;
  const [state, setState] = useState<FeedbackState>("idle");
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const reset = useCallback(() => {
    if (timerRef.current) clearTimeout(timerRef.current);
    setState("idle");
  }, []);

  const run = useCallback(async (): Promise<T | undefined> => {
    if (state === "loading") return;
    if (timerRef.current) clearTimeout(timerRef.current);

    setState("loading");
    try {
      const result = await action();
      setState("success");
      timerRef.current = setTimeout(() => setState("idle"), successDuration);
      return result;
    } catch (err) {
      setState("error");
      timerRef.current = setTimeout(() => setState("idle"), errorDuration);
      throw err;
    }
  }, [action, state, successDuration, errorDuration]);

  return { state, run, reset };
}
