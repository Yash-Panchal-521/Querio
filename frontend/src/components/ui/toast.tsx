"use client";

import { createContext, useCallback, useContext, useMemo, useRef, useState } from "react";
import { cn } from "@/lib/utils";

type Tone = "error" | "success" | "info";

interface Toast {
  id: number;
  message: string;
  tone: Tone;
}

interface ToastValue {
  /** Announces something and gets out of the way. Deliberately offers no actions. */
  showToast: (message: string, tone?: Tone) => void;
}

const ToastContext = createContext<ToastValue | null>(null);

/** Long enough to read a sentence, short enough not to sit over the page. */
const DISMISS_AFTER_MS = 6000;

const TONES: Record<Tone, string> = {
  error: "border-destructive/50 bg-card text-foreground",
  success: "border-success/50 bg-card text-foreground",
  info: "border-border bg-card text-foreground",
};

const DOTS: Record<Tone, string> = {
  error: "bg-destructive",
  success: "bg-success",
  info: "bg-muted-foreground",
};

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(0);

  const showToast = useCallback((message: string, tone: Tone = "info") => {
    const id = nextId.current++;

    setToasts((current) => {
      // Repeating an identical message stacks noise without adding information — a retry
      // loop would otherwise fill the screen with the same sentence.
      if (current.some((toast) => toast.message === message)) {
        return current;
      }

      return [...current, { id, message, tone }];
    });

    setTimeout(() => {
      setToasts((current) => current.filter((toast) => toast.id !== id));
    }, DISMISS_AFTER_MS);
  }, []);

  const value = useMemo<ToastValue>(() => ({ showToast }), [showToast]);

  return (
    <ToastContext value={value}>
      {children}

      <div
        // Fixed to the corner and non-interactive, so it never intercepts a click meant for
        // the page underneath.
        className="pointer-events-none fixed right-4 bottom-4 z-50 flex w-full max-w-sm flex-col gap-2"
      >
        {toasts.map((toast) => (
          <div
            key={toast.id}
            role={toast.tone === "error" ? "alert" : "status"}
            aria-live={toast.tone === "error" ? "assertive" : "polite"}
            className={cn(
              "flex items-start gap-2.5 rounded-md border px-3.5 py-3 text-sm shadow-lg",
              "toast-enter",
              TONES[toast.tone],
            )}
          >
            <span
              aria-hidden="true"
              className={cn("mt-1.5 size-2 shrink-0 rounded-full", DOTS[toast.tone])}
            />
            <p className="text-pretty">{toast.message}</p>
          </div>
        ))}
      </div>
    </ToastContext>
  );
}

export function useToast(): ToastValue {
  const value = useContext(ToastContext);

  if (!value) {
    throw new Error("useToast must be used inside a ToastProvider.");
  }

  return value;
}
