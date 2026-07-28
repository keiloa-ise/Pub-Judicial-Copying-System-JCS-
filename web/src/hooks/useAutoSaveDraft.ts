import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { api, type FormDraft, type Role } from "../api/client";

export type AutoSaveStatus = "idle" | "saving" | "saved" | "offline" | "syncing" | "synced" | "error";

const DEFAULT_SERVER_SYNC_MS = 10_000;

interface StoredDraft<T> { formKey: string; role: string; copyRequestId: string | null; payload: T; updatedAt: string; source: "local" | "server"; }

interface Options<T> {
  userId?: string | null;
  role?: Role | string | null;
  formKey?: string | null;
  copyRequestId?: string | null;
  payload: T;
  enabled?: boolean;
  debounceMs?: number;
  serverIntervalMs?: number;
  restorePrompt?: string;
  onRestore?: (payload: T) => void;
}

export function draftStorageKey(userId: string, formKey: string) {
  return `jcs:draft:${userId}:${formKey}`;
}

/** Remove one local draft (+ its restore-prompt decision). Used on submit/approve and logout. */
export function clearLocalFormDraft(userId: string | null | undefined, formKey: string | null | undefined) {
  if (!userId || !formKey || typeof window === "undefined") return;
  const key = draftStorageKey(userId, formKey);
  window.localStorage.removeItem(key);
  window.sessionStorage.removeItem(promptKey(key));
}

/** Confidentiality on shared machines: clear ALL local drafts (call on logout). */
export function clearAllLocalFormDrafts() {
  if (typeof window === "undefined") return;
  for (const k of Object.keys(window.localStorage)) if (k.startsWith("jcs:draft:")) window.localStorage.removeItem(k);
  for (const k of Object.keys(window.sessionStorage)) if (k.startsWith("jcs:draft-prompt:")) window.sessionStorage.removeItem(k);
}

/**
 * JC-32: auto-saves a form's payload to localStorage (debounced) and syncs it to the server on an
 * interval / on reconnect, so a power outage or lost session never loses unsent work. On mount it
 * offers to restore the newest saved draft. `clearDraft()` is called once the work is committed.
 */
export function useAutoSaveDraft<T extends Record<string, unknown>>(opts: Options<T>) {
  const { userId, role, formKey, copyRequestId = null, payload, enabled = true,
    debounceMs = 500, serverIntervalMs = getConfiguredInterval() } = opts;

  const [status, setStatus] = useState<AutoSaveStatus>("idle");
  const hydratedRef = useRef(false);
  const pendingSyncRef = useRef(false);
  const syncingRef = useRef(false);
  const roleRef = useRef(role); useEffect(() => { roleRef.current = role; }, [role]);
  const copyRef = useRef(copyRequestId); useEffect(() => { copyRef.current = copyRequestId; }, [copyRequestId]);
  const promptRef = useRef(opts.restorePrompt); useEffect(() => { promptRef.current = opts.restorePrompt; }, [opts.restorePrompt]);
  const onRestoreRef = useRef(opts.onRestore); useEffect(() => { onRestoreRef.current = opts.onRestore; }, [opts.onRestore]);

  const active = enabled && !!userId && !!formKey && !!role;
  const storageKey = useMemo(() => (userId && formKey ? draftStorageKey(userId, formKey) : null), [userId, formKey]);
  const payloadJson = useMemo(() => safeStringify(payload), [payload]);

  // Reset per (user, form) so switching copies re-hydrates.
  useEffect(() => { hydratedRef.current = false; pendingSyncRef.current = false; }, [storageKey]);

  // Hydrate: pick the newest of local/server and (once) offer to restore it.
  useEffect(() => {
    if (!active || !storageKey || !formKey) { hydratedRef.current = false; return; }
    let cancelled = false;
    (async () => {
      const local = readLocalDraft<T>(storageKey);
      let server: FormDraft<T> | null = null;
      if (isOnline()) { try { server = await api.getFormDraft<T>(formKey); } catch { setStatus(local ? "offline" : "error"); } }
      if (cancelled) return;

      const latest = newestDraft(local, server);
      if (latest && onRestoreRef.current) {
        const pk = promptKey(storageKey);
        const prior = readPromptDecision(pk, latest.updatedAt);
        const restore = prior ?? window.confirm(promptRef.current ?? "A saved draft exists. Restore it?");
        writePromptDecision(pk, latest.updatedAt, restore);
        if (restore) {
          onRestoreRef.current(latest.payload);
          writeLocalDraft(storageKey, latest);
          pendingSyncRef.current = latest.source === "local";
        } else {
          window.localStorage.removeItem(storageKey);
          if (isOnline()) api.deleteFormDraft(formKey).catch(() => {});
        }
      }
      hydratedRef.current = true;
    })();
    return () => { cancelled = true; };
  }, [active, storageKey, formKey]);

  // Debounced local save on payload change.
  useEffect(() => {
    if (!active || !storageKey || !hydratedRef.current) return;
    let statusTimer = 0;
    const saveTimer = window.setTimeout(() => {
      const draft: StoredDraft<T> = {
        formKey: formKey!, role: String(roleRef.current), copyRequestId: copyRef.current ?? null,
        payload: JSON.parse(payloadJson) as T, updatedAt: new Date().toISOString(), source: "local",
      };
      writeLocalDraft(storageKey, draft);
      pendingSyncRef.current = true;
      setStatus("saving");
      statusTimer = window.setTimeout(() => setStatus(isOnline() ? "saved" : "offline"), 150);
    }, debounceMs);
    return () => { window.clearTimeout(saveTimer); if (statusTimer) window.clearTimeout(statusTimer); };
  }, [active, storageKey, formKey, payloadJson, debounceMs]);

  const syncNow = useCallback(async () => {
    if (!active || !storageKey || !formKey || !isOnline() || syncingRef.current || !pendingSyncRef.current) return;
    const draft = readLocalDraft<T>(storageKey);
    if (!draft) return;
    syncingRef.current = true; setStatus("syncing");
    try {
      const synced = await api.upsertFormDraft<T>(formKey, draft.payload, draft.copyRequestId ?? copyRef.current ?? null);
      const still = readLocalDraft<T>(storageKey);
      if (still?.updatedAt === draft.updatedAt) {
        writeLocalDraft(storageKey, { ...synced, source: "server" });
        pendingSyncRef.current = false; setStatus("synced");
      }
    } catch { pendingSyncRef.current = true; setStatus("error"); }
    finally { syncingRef.current = false; }
  }, [active, storageKey, formKey]);

  // Periodic sync + reconnect handling.
  useEffect(() => {
    if (!active) return;
    const id = window.setInterval(() => { void syncNow(); }, serverIntervalMs);
    const onOnline = () => { void syncNow(); };
    const onOffline = () => setStatus("offline");
    window.addEventListener("online", onOnline);
    window.addEventListener("offline", onOffline);
    return () => { window.clearInterval(id); window.removeEventListener("online", onOnline); window.removeEventListener("offline", onOffline); };
  }, [active, serverIntervalMs, syncNow]);

  const clearDraft = useCallback(async () => {
    if (storageKey) { window.localStorage.removeItem(storageKey); window.sessionStorage.removeItem(promptKey(storageKey)); }
    pendingSyncRef.current = false;
    setStatus("idle");
    if (active && formKey && isOnline()) { try { await api.deleteFormDraft(formKey); } catch { /* cleanup must not block */ } }
  }, [active, formKey, storageKey]);

  return { status, syncNow, clearDraft };
}

// ── helpers ──
function safeStringify(v: unknown) { try { return JSON.stringify(v ?? {}) ?? "{}"; } catch { return "{}"; } }
function getConfiguredInterval() {
  const n = Number(import.meta.env.VITE_AUTO_SAVE_DRAFT_SYNC_INTERVAL_MS);
  return Number.isFinite(n) && n > 0 ? n : DEFAULT_SERVER_SYNC_MS;
}
function isOnline() { return typeof navigator === "undefined" ? true : navigator.onLine; }
function promptKey(storageKey: string) { return `jcs:draft-prompt:${storageKey}`; }

function readLocalDraft<T>(key: string): StoredDraft<T> | null {
  try {
    const raw = window.localStorage.getItem(key);
    if (!raw) return null;
    const p = JSON.parse(raw) as StoredDraft<T>;
    if (!p || typeof p.formKey !== "string" || typeof p.updatedAt !== "string") return null;
    return { ...p, source: p.source === "server" ? "server" : "local" };
  } catch { return null; }
}
function writeLocalDraft<T>(key: string, d: StoredDraft<T>) { window.localStorage.setItem(key, JSON.stringify(d)); }

function readPromptDecision(key: string, updatedAt: string): boolean | null {
  try {
    const raw = window.sessionStorage.getItem(key); if (!raw) return null;
    const p = JSON.parse(raw) as { updatedAt?: string; restore?: boolean };
    return p.updatedAt === updatedAt && typeof p.restore === "boolean" ? p.restore : null;
  } catch { return null; }
}
function writePromptDecision(key: string, updatedAt: string, restore: boolean) {
  window.sessionStorage.setItem(key, JSON.stringify({ updatedAt, restore }));
}

function newestDraft<T>(local: StoredDraft<T> | null, server: FormDraft<T> | null): StoredDraft<T> | null {
  const s = server ? { ...server, source: "server" as const, copyRequestId: server.copyRequestId ?? null } : null;
  if (!local) return s;
  if (!s) return local;
  return Date.parse(local.updatedAt) >= Date.parse(s.updatedAt) ? local : s;
}
