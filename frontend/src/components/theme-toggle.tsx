"use client";

import { useTheme } from "next-themes";
import { useSyncExternalStore } from "react";
import { cn } from "@/lib/utils";

const OPTIONS = [
  { value: "light", label: "Light" },
  { value: "system", label: "System" },
  { value: "dark", label: "Dark" },
] as const;

// Never emits, so the snapshot is only ever read on render.
const noopSubscribe = () => () => {};

/**
 * False while rendering on the server and through hydration, true afterwards.
 *
 * The stored preference lives in localStorage, so the server cannot know which
 * option is active — rendering the highlight straight away would mismatch. The
 * usual fix is a `setMounted(true)` effect, but eslint-config-next 16 rejects
 * synchronous setState in an effect. Splitting the server and client snapshots
 * expresses the same "am I hydrated yet" question as a store read, which React
 * settles during hydration rather than with a cascading render.
 */
function useHydrated(): boolean {
  return useSyncExternalStore(
    noopSubscribe,
    () => true,
    () => false,
  );
}

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const hydrated = useHydrated();

  return (
    <div
      role="group"
      aria-label="Colour theme"
      className="border-border inline-flex gap-1 rounded-lg border p-1"
    >
      {OPTIONS.map((option) => {
        const active = hydrated && theme === option.value;

        return (
          <button
            key={option.value}
            type="button"
            aria-pressed={active}
            onClick={() => setTheme(option.value)}
            className={cn(
              "focus-visible:ring-ring rounded-md px-3 py-1.5 text-sm font-medium",
              "focus-visible:ring-offset-background focus-visible:ring-2",
              "focus-visible:ring-offset-1 focus-visible:outline-none",
              active
                ? "bg-primary text-primary-foreground"
                : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
            )}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
