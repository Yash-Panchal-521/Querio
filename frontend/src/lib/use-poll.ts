"use client";

import { useEffect, useRef } from "react";

type PollAction = () => Promise<void>;

interface PollOptions {
  baseMs?: number;
  maxMs?: number;
  /**
   * A value that changes whenever the polled data does. The schedule restarts when it
   * changes, which resets the delay to `baseMs`.
   *
   * This is how progress keeps the poll fast without the action having to report anything:
   * asking the action "did that change something" means comparing state that React has not
   * re-rendered yet, whereas the caller is handed the new value on the next render and can
   * simply pass it here.
   */
  resetKey?: string;
}

/**
 * Polling that cannot overlap itself.
 *
 * `setInterval` fires on a clock whether or not the previous call returned. Give it a request
 * slower than the interval and the calls stack, exhaust the browser's handful of connections
 * per origin, and everything after that sits pending behind them — which reads as an infinite
 * loop in the network panel while the code contains no loop at all. That is not a theoretical
 * failure; it is what this replaced.
 *
 * The guarantee here rests on two things, not one. The next call is scheduled only after the
 * previous one settles, and a flag rejects re-entry from anywhere else — the visibility
 * listener below is exactly such an "anywhere else", since clearing a timer does nothing to a
 * request already in flight.
 */
export function usePoll(action: PollAction, active: boolean, options: PollOptions = {}) {
  const { baseMs = 2000, maxMs = 15000, resetKey } = options;

  // A ref so a changing action identity does not restart the schedule, assigned in an effect
  // rather than during render — writing refs while rendering is a side effect in a function
  // React may call more than once.
  const latest = useRef(action);

  useEffect(() => {
    latest.current = action;
  }, [action]);

  useEffect(() => {
    if (!active) {
      return;
    }

    let cancelled = false;
    let running = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    let delay = baseMs;

    function schedule(ms: number) {
      clearTimeout(timer);

      if (!cancelled) {
        timer = setTimeout(() => void run(), ms);
      }
    }

    async function run() {
      // Re-entry guard. Not belt and braces: the visibility listener calls this directly, and
      // without the flag a tab switched away and back mid-request starts a second one.
      if (cancelled || running) {
        return;
      }

      // Nobody watches a progress bar they cannot see, and a backgrounded tab left open
      // overnight would otherwise keep an idle instance and its database awake.
      if (document.hidden) {
        schedule(delay);

        return;
      }

      running = true;

      try {
        await latest.current();

        // Grows unconditionally. Progress does not need to be detected here — it changes
        // `resetKey`, which restarts this effect and puts the delay back to `baseMs`.
        delay = Math.min(delay * 1.5, maxMs);
      } catch {
        // Swallowed deliberately: the action reports its own failures through the interface,
        // and an unhandled rejection here would kill the schedule and freeze the display.
        // Backed off, because a failing request that repeats at full rate is the worse bug.
        delay = Math.min(delay * 1.5, maxMs);
      } finally {
        running = false;
      }

      if (!cancelled) {
        schedule(delay);
      }
    }

    function onVisibilityChange() {
      if (!document.hidden) {
        // Returning to the tab should show current state, not whatever the delay had grown
        // to. Safe to call directly because `run` refuses to re-enter.
        delay = baseMs;
        void run();
      }
    }

    document.addEventListener("visibilitychange", onVisibilityChange);
    schedule(delay);

    return () => {
      cancelled = true;
      clearTimeout(timer);
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, [active, baseMs, maxMs, resetKey]);
}
