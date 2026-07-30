import { useEffect, useRef, useState, type CSSProperties, type KeyboardEvent as ReactKeyboardEvent, type ReactNode } from "react";
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

export function StateBadge({ state, awaitingAcceptance }: { state: CopyState; awaitingAcceptance?: boolean }) {
  const { lang } = useI18n();
  // FR-13: an unaccepted In-preparation copy reads as «بانتظار القبول», not «قيد التحضير».
  if (awaitingAcceptance)
    return <span className="badge s-awaiting">{lang === "ar" ? "بانتظار القبول" : "Awaiting acceptance"}</span>;
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

export function ErrorBox({ message, onDismiss }: { message: string | string[]; onDismiss?: () => void }) {
  const messages = (Array.isArray(message) ? message : message.split(/\r?\n/))
    .map((x) => x.trim())
    .filter(Boolean);
  const noticeKey = messages.join("\n");
  const [visible, setVisible] = useState(true);
  const dismissRef = useRef(onDismiss);

  useEffect(() => { dismissRef.current = onDismiss; }, [onDismiss]);

  useEffect(() => {
    if (!noticeKey) {
      setVisible(false);
      return;
    }
    setVisible(true);
    const timer = window.setTimeout(() => {
      setVisible(false);
      dismissRef.current?.();
    }, 4000);
    return () => window.clearTimeout(timer);
  }, [noticeKey]);

  if (messages.length === 0 || !visible) return null;
  if (messages.length === 1) return <div className="errorbox" role="alert">{messages[0]}</div>;

  return (
    <div className="errorbox-stack" aria-live="assertive">
      {messages.map((item, i) => <div className="errorbox" role="alert" key={`${i}-${item}`}>{item}</div>)}
    </div>
  );
}

export interface SearchableSelectOption {
  id: string;
  label: string;
  group?: string;
  searchText?: string;
}

export function SearchableMultiSelect(
  { options, selected, onChange, placeholder, emptyLabel, selectedLabel }:
  Readonly<{
    options: SearchableSelectOption[];
    selected: string[];
    onChange: (ids: string[]) => void;
    placeholder: string;
    emptyLabel: string;
    selectedLabel: string;
  }>,
) {
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const selectedSet = new Set(selected);
  const selectedOptions = selected
    .map((id) => options.find((o) => o.id === id))
    .filter((o): o is SearchableSelectOption => !!o);
  const q = query.trim().toLocaleLowerCase("ar");
  const visibleOptions = options
    .filter((o) => !selectedSet.has(o.id))
    .filter((o) => !q || `${o.label} ${o.group ?? ""} ${o.searchText ?? ""}`.toLocaleLowerCase("ar").includes(q))
    .slice(0, 80);

  function add(id: string) {
    if (!selectedSet.has(id)) onChange([...selected, id]);
    setQuery("");
    setOpen(true);
  }

  function remove(id: string) {
    onChange(selected.filter((x) => x !== id));
  }

  function onInputKeyDown(e: ReactKeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter") {
      const first = visibleOptions[0];
      if (first) {
        e.preventDefault();
        add(first.id);
      }
    } else if (e.key === "Backspace" && !query && selected.length > 0) {
      remove(selected[selected.length - 1]);
    } else if (e.key === "Escape") {
      setOpen(false);
    }
  }

  return (
    <div className="searchselect">
      <div className="searchselect__selected" aria-label={selectedLabel}>
        {selectedOptions.length === 0 ? (
          <span className="searchselect__placeholder">{selectedLabel}</span>
        ) : selectedOptions.map((option) => (
          <span className="searchselect__chip" key={option.id}>
            <span>{option.group ? `${option.group} / ${option.label}` : option.label}</span>
            <button type="button" onClick={() => remove(option.id)} aria-label={`Remove ${option.label}`}>x</button>
          </span>
        ))}
      </div>
      <div className="searchselect__control">
        <input
          className="searchselect__input"
          value={query}
          onChange={(e) => { setQuery(e.target.value); setOpen(true); }}
          onFocus={() => setOpen(true)}
          onBlur={() => window.setTimeout(() => setOpen(false), 120)}
          onKeyDown={onInputKeyDown}
          placeholder={placeholder}
          autoComplete="off"
        />
        {open && (
          <div className="searchselect__menu" role="listbox">
            {visibleOptions.length === 0 ? (
              <div className="searchselect__empty">{emptyLabel}</div>
            ) : visibleOptions.map((option) => (
              <button
                type="button"
                className="searchselect__option"
                key={option.id}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => add(option.id)}
                role="option"
              >
                <span className="searchselect__option-main">{option.label}</span>
                {option.group && <span className="searchselect__option-sub">{option.group}</span>}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
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

export function SearchableSelect(
  { options, value, onChange, placeholder, emptyLabel, disabled = false, allowClear = true, clearLabel = "Clear" }:
  Readonly<{
    options: SearchableSelectOption[];
    value: string;
    onChange: (id: string) => void;
    placeholder: string;
    emptyLabel: string;
    disabled?: boolean;
    allowClear?: boolean;
    clearLabel?: string;
  }>,
) {
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const selectedOption = options.find((o) => o.id === value);
  const q = query.trim().toLocaleLowerCase("ar");
  const visibleOptions = options
    .filter((o) => !q || `${o.label} ${o.group ?? ""} ${o.searchText ?? ""}`.toLocaleLowerCase("ar").includes(q))
    .slice(0, 80);
  const hasClear = allowClear && !!value && !disabled;

  function pick(id: string) {
    onChange(id);
    setQuery("");
    setOpen(false);
  }

  function clear() {
    onChange("");
    setQuery("");
    setOpen(false);
  }

  function onInputKeyDown(e: ReactKeyboardEvent<HTMLInputElement>) {
    if (disabled) return;
    if (e.key === "Enter") {
      const first = visibleOptions[0];
      if (first) {
        e.preventDefault();
        pick(first.id);
      }
    } else if (e.key === "Backspace" && !query && hasClear) {
      clear();
    } else if (e.key === "Escape") {
      setOpen(false);
      setQuery("");
    }
  }

  return (
    <div className={`searchselect searchselect--single${disabled ? " searchselect--disabled" : ""}`}>
      <div className="searchselect__control">
        <input
          className={`searchselect__input${hasClear ? " searchselect__input--clearable" : ""}`}
          value={open ? query : selectedOption?.label ?? ""}
          onChange={(e) => { setQuery(e.target.value); setOpen(true); }}
          onFocus={(e) => {
            if (disabled) return;
            setQuery(selectedOption?.label ?? "");
            setOpen(true);
            e.currentTarget.select();
          }}
          onBlur={() => window.setTimeout(() => { setOpen(false); setQuery(""); }, 120)}
          onKeyDown={onInputKeyDown}
          placeholder={placeholder}
          disabled={disabled}
          autoComplete="off"
        />
        {hasClear && (
          <button
            type="button"
            className="searchselect__clear"
            onMouseDown={(e) => e.preventDefault()}
            onClick={clear}
            aria-label={clearLabel}
          >
            x
          </button>
        )}
        {open && !disabled && (
          <div className="searchselect__menu" role="listbox">
            {visibleOptions.length === 0 ? (
              <div className="searchselect__empty">{emptyLabel}</div>
            ) : visibleOptions.map((option) => (
              <button
                type="button"
                className="searchselect__option"
                key={option.id}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => pick(option.id)}
                role="option"
                aria-selected={option.id === value}
              >
                <span className="searchselect__option-main">{option.label}</span>
                {option.group && <span className="searchselect__option-sub">{option.group}</span>}
              </button>
            ))}
          </div>
        )}
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
