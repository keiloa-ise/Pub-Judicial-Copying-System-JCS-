import { useCallback, useEffect, useMemo, useRef, useState, type RefObject } from "react";
import { api, type FormDraft, type Role } from "../api/client";

export type AutoSaveDraftStatus = "idle" | "saving" | "saved" | "offline" | "syncing" | "synced" | "error";

type DraftSource = "local" | "server";
const DEFAULT_SERVER_SYNC_INTERVAL_MS = 10000;

interface StoredDraft<TPayload> {
  formKey: string;
  role: string;
  copyRequestId?: string | null;
  payload: TPayload;
  updatedAt: string;
  source: DraftSource;
}

interface UseAutoSaveDraftOptions<TPayload> {
  userId?: string | null;
  role?: Role | string | null;
  formKey?: string | null;
  copyRequestId?: string | null;
  payload?: TPayload;
  formRef?: RefObject<HTMLFormElement | null>;
  enabled?: boolean;
  debounceMs?: number;
  serverIntervalMs?: number;
  restorePrompt?: string;
  onRestore?: (payload: TPayload) => void;
}

export function draftStorageKey(userId: string, formKey: string) {
  return `jcs:draft:${userId}:${formKey}`;
}

export function clearLocalFormDraft(userId: string | null | undefined, formKey: string | null | undefined) {
  if (!userId || !formKey || typeof window === "undefined") return;
  window.localStorage.removeItem(draftStorageKey(userId, formKey));
}

export function readFormPayload(form: HTMLFormElement): Record<string, unknown> {
  const payload: Record<string, unknown> = {};
  const fields = form.querySelectorAll<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>("input, textarea, select");

  fields.forEach((field) => {
    if (field.closest("[data-draft-ignore]")) return;
    const key = field.name || field.id;
    if (!key) return;

    if (field instanceof HTMLInputElement) {
      const type = field.type.toLowerCase();
      if (type === "password" || type === "file") return;
      if (type === "checkbox") {
        payload[key] = field.checked;
        return;
      }
      if (type === "radio") {
        if (field.checked) payload[key] = field.value;
        return;
      }
    }

    if (field instanceof HTMLSelectElement && field.multiple) {
      payload[key] = Array.from(field.selectedOptions).map((option) => option.value);
      return;
    }

    payload[key] = field.value;
  });

  return payload;
}

export function useAutoSaveDraft<TPayload extends Record<string, unknown>>(
  options: UseAutoSaveDraftOptions<TPayload>,
) {
  const configuredServerIntervalMs = getConfiguredServerIntervalMs();
  const {
    userId,
    role,
    formKey,
    copyRequestId = null,
    payload,
    formRef,
    enabled = true,
    debounceMs = 500,
    serverIntervalMs = configuredServerIntervalMs,
  } = options;

  const [status, setStatus] = useState<AutoSaveDraftStatus>("idle");
  const [hasDraft, setHasDraft] = useState(false);
  const [formSnapshot, setFormSnapshot] = useState<Record<string, unknown>>({});
  const hydratedRef = useRef(false);
  const pendingServerSyncRef = useRef(false);
  const syncingRef = useRef(false);
  const promptRef = useRef(options.restorePrompt);
  const onRestoreRef = useRef(options.onRestore);
  const roleRef = useRef(role);
  const copyRequestIdRef = useRef(copyRequestId);

  useEffect(() => { promptRef.current = options.restorePrompt; }, [options.restorePrompt]);
  useEffect(() => { onRestoreRef.current = options.onRestore; }, [options.onRestore]);
  useEffect(() => { roleRef.current = role; }, [role]);
  useEffect(() => { copyRequestIdRef.current = copyRequestId; }, [copyRequestId]);

  const active = enabled && !!userId && !!formKey && !!role;
  const storageKey = useMemo(
    () => (userId && formKey ? draftStorageKey(userId, formKey) : null),
    [userId, formKey],
  );

  useEffect(() => {
    if (payload || !formRef?.current || !active) return;

    const form = formRef.current;
    const update = () => setFormSnapshot(readFormPayload(form));
    update();
    form.addEventListener("input", update);
    form.addEventListener("change", update);
    return () => {
      form.removeEventListener("input", update);
      form.removeEventListener("change", update);
    };
  }, [active, formRef, payload]);

  const currentPayload = payload ?? (formSnapshot as TPayload);
  const payloadJson = useMemo(() => safeStringify(currentPayload), [currentPayload]);

  useEffect(() => {
    hydratedRef.current = false;
    pendingServerSyncRef.current = false;
    setHasDraft(false);
  }, [storageKey]);

  useEffect(() => {
    if (!active || !storageKey || !formKey) {
      hydratedRef.current = false;
      return;
    }

    const key = storageKey;
    const currentFormKey = formKey;
    let cancelled = false;
    async function hydrate() {
      const localDraft = readLocalDraft<TPayload>(key);
      let serverDraft: FormDraft<TPayload> | null = null;

      if (isOnline()) {
        try {
          serverDraft = await api.getFormDraft<TPayload>(currentFormKey);
        } catch {
          setStatus(localDraft ? "offline" : "error");
        }
      }

      if (cancelled) return;

      const latest = newestDraft(localDraft, serverDraft);
      setHasDraft(!!latest);

      if (latest && onRestoreRef.current) {
        const promptKey = promptDecisionStorageKey(key);
        const previousDecision = readPromptDecision(promptKey, latest.updatedAt);
        const shouldRestore = previousDecision ?? window.confirm(promptRef.current ?? "A saved draft exists. Restore it?");
        writePromptDecision(promptKey, latest.updatedAt, shouldRestore);

        if (shouldRestore) {
          onRestoreRef.current(latest.payload);
          writeLocalDraft(key, latest);
          pendingServerSyncRef.current = latest.source === "local";
        } else {
          window.localStorage.removeItem(key);
          setHasDraft(false);
          if (isOnline()) api.deleteFormDraft(currentFormKey).catch(() => {});
        }
      }

      hydratedRef.current = true;
    }

    hydrate();
    return () => { cancelled = true; };
  }, [active, formKey, storageKey]);

  useEffect(() => {
    if (!active || !storageKey || !hydratedRef.current) return;

    const timer = window.setTimeout(() => {
      const updatedAt = new Date().toISOString();
      const draft: StoredDraft<TPayload> = {
        formKey: formKey!,
        role: String(roleRef.current),
        copyRequestId: copyRequestIdRef.current,
        payload: JSON.parse(payloadJson) as TPayload,
        updatedAt,
        source: "local",
      };
      writeLocalDraft(storageKey, draft);
      pendingServerSyncRef.current = true;
      setHasDraft(true);
      setStatus("saving");
      window.setTimeout(() => setStatus(isOnline() ? "saved" : "offline"), 150);
    }, debounceMs);

    return () => window.clearTimeout(timer);
  }, [active, debounceMs, formKey, payloadJson, storageKey]);

  const syncNow = useCallback(async () => {
    if (!active || !storageKey || !formKey || !isOnline() || syncingRef.current) return;
    const draft = readLocalDraft<TPayload>(storageKey);
    if (!draft || !pendingServerSyncRef.current) return;

    syncingRef.current = true;
    setStatus("syncing");
    try {
      const synced = await api.upsertFormDraft<TPayload>(
        formKey,
        draft.payload,
        draft.updatedAt,
        draft.copyRequestId ?? copyRequestIdRef.current ?? null,
      );
      const latestLocal = readLocalDraft<TPayload>(storageKey);
      if (latestLocal?.updatedAt === draft.updatedAt) {
        writeLocalDraft(storageKey, { ...synced, source: "server" });
        pendingServerSyncRef.current = false;
        setStatus("synced");
      }
    } catch {
      pendingServerSyncRef.current = true;
      setStatus("error");
    } finally {
      syncingRef.current = false;
    }
  }, [active, formKey, storageKey]);

  useEffect(() => {
    if (!active) return;
    const onOnline = () => { void syncNow(); };
    const onOffline = () => setStatus("offline");
    window.addEventListener("online", onOnline);
    window.addEventListener("offline", onOffline);
    return () => {
      window.removeEventListener("online", onOnline);
      window.removeEventListener("offline", onOffline);
    };
  }, [active, syncNow]);

  useEffect(() => {
    if (!active) return;
    const timer = window.setInterval(() => { void syncNow(); }, serverIntervalMs);
    return () => window.clearInterval(timer);
  }, [active, serverIntervalMs, syncNow]);

  const clearDraft = useCallback(async () => {
    if (storageKey) window.localStorage.removeItem(storageKey);
    if (storageKey) window.sessionStorage.removeItem(promptDecisionStorageKey(storageKey));
    pendingServerSyncRef.current = false;
    setHasDraft(false);
    setStatus("idle");

    if (active && formKey && isOnline()) {
      try { await api.deleteFormDraft(formKey); }
      catch { setStatus("error"); }
    }
  }, [active, formKey, storageKey]);

  return { status, hasDraft, syncNow, clearDraft };
}

function safeStringify(value: unknown) {
  try { return JSON.stringify(value ?? {}) ?? "{}"; }
  catch { return "{}"; }
}

function getConfiguredServerIntervalMs() {
  const raw = import.meta.env.VITE_AUTO_SAVE_DRAFT_SYNC_INTERVAL_MS;
  const parsed = Number(raw);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_SERVER_SYNC_INTERVAL_MS;
}

function isOnline() {
  return typeof navigator === "undefined" ? true : navigator.onLine;
}

function readLocalDraft<TPayload>(key: string): StoredDraft<TPayload> | null {
  try {
    const raw = window.localStorage.getItem(key);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as StoredDraft<TPayload>;
    if (!parsed || typeof parsed.formKey !== "string" || typeof parsed.updatedAt !== "string") return null;
    return { ...parsed, source: parsed.source === "server" ? "server" : "local" };
  } catch {
    return null;
  }
}

function writeLocalDraft<TPayload>(key: string, draft: StoredDraft<TPayload>) {
  window.localStorage.setItem(key, JSON.stringify(draft));
}

function promptDecisionStorageKey(storageKey: string) {
  return `jcs:draft-prompt:${storageKey}`;
}

function readPromptDecision(key: string, updatedAt: string): boolean | null {
  try {
    const raw = window.sessionStorage.getItem(key);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as { updatedAt?: string; restore?: boolean };
    return parsed.updatedAt === updatedAt && typeof parsed.restore === "boolean" ? parsed.restore : null;
  } catch {
    return null;
  }
}

function writePromptDecision(key: string, updatedAt: string, restore: boolean) {
  window.sessionStorage.setItem(key, JSON.stringify({ updatedAt, restore }));
}

function newestDraft<TPayload>(
  localDraft: StoredDraft<TPayload> | null,
  serverDraft: FormDraft<TPayload> | null,
): StoredDraft<TPayload> | null {
  const normalizedServer = serverDraft
    ? { ...serverDraft, source: "server" as const, copyRequestId: serverDraft.copyRequestId ?? null }
    : null;

  if (!localDraft) return normalizedServer;
  if (!normalizedServer) return localDraft;

  const localTime = Date.parse(localDraft.updatedAt);
  const serverTime = Date.parse(normalizedServer.updatedAt);
  return localTime >= serverTime ? localDraft : normalizedServer;
}
