"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { toApiMessage } from "@/lib/api/api-messages";

interface AsyncData<T> {
  data: T | null;
  error: string | null;
  loading: boolean;
  reload: () => Promise<void>;
}

/**
 * Minimal fetch-on-mount with a manual reload.
 *
 * Deliberately not a data-fetching library: these screens load a short list once and reload
 * after a mutation, which does not justify a cache.
 *
 * `load` is the only dependency, so callers memoise it with useCallback and the identity of
 * that function decides when a refetch happens — no second dependency array to keep in sync.
 */
export function useAsyncData<T>(load: () => Promise<T>): AsyncData<T> {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  // Discards a response that arrived after a newer request — reloading twice quickly would
  // otherwise let the slower answer win.
  const generation = useRef(0);

  useEffect(() => {
    const token = ++generation.current;

    // Nothing is set synchronously here on purpose: state only changes once the request
    // settles, which keeps the initial fetch out of the render pass entirely.
    load()
      .then((result) => {
        if (token === generation.current) {
          setData(result);
          setError(null);
        }
      })
      .catch((caught: unknown) => {
        if (token === generation.current) {
          setError(toApiMessage(caught));
        }
      })
      .finally(() => {
        if (token === generation.current) {
          setLoading(false);
        }
      });

    return () => {
      // Invalidates the in-flight response rather than cancelling it, which is enough to
      // stop it writing into an unmounted tree.
      generation.current += 1;
    };
  }, [load]);

  // Called from event handlers after a mutation, where showing a pending state immediately
  // is exactly what is wanted.
  const reload = useCallback(async () => {
    const token = ++generation.current;

    setLoading(true);

    try {
      const result = await load();

      if (token === generation.current) {
        setData(result);
        setError(null);
      }
    } catch (caught) {
      if (token === generation.current) {
        setError(toApiMessage(caught));
      }
    } finally {
      if (token === generation.current) {
        setLoading(false);
      }
    }
  }, [load]);

  return { data, error, loading, reload };
}
