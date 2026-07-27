import { useEffect, useState } from "react";
import { api, type FeatureFlags } from "../api/client";

// Module-level cache so the /api/config call is made once per session, shared across components.
let cache: FeatureFlags | null = null;

/** FR-15: server feature flags used to hide role-gated UI (individual reprint, batch-print tab).
 *  Returns null until loaded; both flags default true, so treat null as "allowed" where sensible. */
export function useConfig(): FeatureFlags | null {
  const [flags, setFlags] = useState<FeatureFlags | null>(cache);
  useEffect(() => {
    if (cache) { setFlags(cache); return; }
    let cancelled = false;
    api.config().then((c) => { cache = c; if (!cancelled) setFlags(c); }).catch(() => { /* keep null → default-allow */ });
    return () => { cancelled = true; };
  }, []);
  return flags;
}
