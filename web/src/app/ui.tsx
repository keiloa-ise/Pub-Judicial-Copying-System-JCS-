import { useEffect, useState, type CSSProperties, type ReactNode } from "react";
import { useI18n } from "../i18n";
import type { CopyState, Role } from "../api/client";

/** Bilingual helper: pick ar/en by current language. */
export function useL() {
  const { lang } = useI18n();
  return (ar: string, en: string) => (lang === "ar" ? ar : en);
}

const stateLabels: Record<CopyState, { ar: string; en: string; cls: string }> = {
  Created:       { ar: "أُنشئ", en: "Created", cls: "s-created" },
  InPreparation: { ar: "قيد التحضير", en: "In preparation", cls: "s-prep" },
  UnderReview:   { ar: "قيد المراجعة", en: "Under review", cls: "s-review" },
  Approved:      { ar: "معتمد (مقفل)", en: "Approved (locked)", cls: "s-approved" },
  Unlocked:      { ar: "مفتوح", en: "Unlocked", cls: "s-unlocked" },
};

export function StateBadge({ state }: { state: CopyState }) {
  const { lang } = useI18n();
  const s = stateLabels[state];
  return <span className={`badge ${s.cls}`}>{lang === "ar" ? s.ar : s.en}</span>;
}

export const roleLabels: Record<Role, { ar: string; en: string }> = {
  Administrator: { ar: "مدير النظام", en: "Administrator" },
  RegistryHead: { ar: "رئيس الديوان", en: "Head of Registry" },
  Copyist: { ar: "الناسخ", en: "Copyist" },
  Reviewer: { ar: "المدقق", en: "Reviewer" },
};

export const auditLabels: Record<string, { ar: string; en: string }> = {
  Create: { ar: "إنشاء", en: "Create" },
  Edit: { ar: "تعديل", en: "Edit" },
  Submit: { ar: "إرسال", en: "Submit" },
  Return: { ar: "إعادة", en: "Return" },
  Approve: { ar: "اعتماد", en: "Approve" },
  Unlock: { ar: "فتح", en: "Unlock" },
  Delete: { ar: "حذف", en: "Delete" },
  Accept: { ar: "قبول", en: "Accept" },
  Expedite: { ar: "تصعيد إلى مستعجل", en: "Expedite" },
  Suspend: { ar: "تصعيد إلى موقوف", en: "Suspend" },
  Print: { ar: "طباعة", en: "Print" },
};

export const numberingPolicyLabels: Record<string, { ar: string; en: string }> = {
  Court: { ar: "مستوى المحكمة", en: "Court level" },
  Room: { ar: "مستوى الغرفة", en: "Room level" },
  Special: { ar: "مستوى خاص", en: "Special level" },
};

export const categoryLabels: Record<string, { ar: string; en: string }> = {
  Normal: { ar: "عادي", en: "Normal" },
  Miscellaneous: { ar: "متفرق", en: "Miscellaneous" },
};
export const urgencyLabels: Record<string, { ar: string; en: string }> = {
  Normal: { ar: "عادي", en: "Normal" },
  Suspended: { ar: "موقوف", en: "Suspended" },
  Expedited: { ar: "مستعجل", en: "Expedited" },
};

export function Spinner({ label }: { label?: string }) {
  return <div className="muted" style={{ padding: 24 }}>{label ?? "…"}</div>;
}

export function ErrorBox({ message }: { message: string }) {
  return <div className="errorbox" role="alert">{message}</div>;
}

/* ── Modal dialog ───────────────────────────────────────────────────────────
 * Centered popup over a backdrop. Closes on backdrop click or Escape. RTL-aware. */
export function Modal(
  { open, onClose, title, children }:
  Readonly<{ open: boolean; onClose: () => void; title?: ReactNode; children: ReactNode }>,
) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  if (!open) return null;
  return (
    <div className="modal-overlay" onMouseDown={onClose}>
      <div className="modal" role="dialog" aria-modal="true" onMouseDown={(e) => e.stopPropagation()}>
        <div className="modal__head">
          <h3 className="modal__title">{title}</h3>
          <button type="button" className="modal__close" onClick={onClose} aria-label="إغلاق">✕</button>
        </div>
        <div className="modal__body">{children}</div>
      </div>
    </div>
  );
}

/* ── Sortable tables ─────────────────────────────────────────────────────────
 * useSort(rows, accessors): returns rows sorted by the active column. Pair the returned
 * `sortKey`/`sortDir`/`onSort` with <SortTh> headers; each header's `k` matches an accessor key. */
export type SortDir = "asc" | "desc";
type SortVal = string | number | boolean | null | undefined;

function compareValues(a: SortVal, b: SortVal): number {
  const aEmpty = a === null || a === undefined || a === "";
  const bEmpty = b === null || b === undefined || b === "";
  if (aEmpty && bEmpty) return 0;
  if (aEmpty) return 1;   // empties sort last
  if (bEmpty) return -1;
  if (typeof a === "number" && typeof b === "number") return a - b;
  if (typeof a === "boolean" && typeof b === "boolean") return a === b ? 0 : a ? -1 : 1;
  return String(a).localeCompare(String(b), "ar", { numeric: true, sensitivity: "base" });
}

export function useSort<T>(
  rows: T[],
  accessors: Record<string, (r: T) => SortVal>,
  initial?: { key: string; dir: SortDir },
) {
  const [s, setS] = useState<{ key: string; dir: SortDir } | null>(initial ?? null);
  let sorted = rows;
  if (s && accessors[s.key]) {
    const f = accessors[s.key];
    const sign = s.dir === "asc" ? 1 : -1;
    sorted = [...rows].sort((a, b) => compareValues(f(a), f(b)) * sign);
  }
  const onSort = (key: string) =>
    setS((p) => (p && p.key === key ? { key, dir: p.dir === "asc" ? "desc" : "asc" } : { key, dir: "asc" }));
  return { sorted, sortKey: s?.key ?? null, sortDir: (s?.dir ?? "asc") as SortDir, onSort };
}

export function SortTh(
  { label, k, sortKey, sortDir, onSort, style }:
  Readonly<{ label: ReactNode; k: string; sortKey: string | null; sortDir: SortDir; onSort: (k: string) => void; style?: CSSProperties }>,
) {
  const active = sortKey === k;
  return (
    <th className="sort-th" style={style} onClick={() => onSort(k)}
        aria-sort={active ? (sortDir === "asc" ? "ascending" : "descending") : "none"}>
      <span className="sort-th__in">{label}<span className={`sort-ind${active ? " on" : ""}`}>{active ? (sortDir === "asc" ? "▲" : "▼") : "⇅"}</span></span>
    </th>
  );
}
