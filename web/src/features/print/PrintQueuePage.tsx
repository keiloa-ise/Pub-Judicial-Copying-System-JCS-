import { useCallback, useEffect, useState } from "react";
import { api, type CopyRequestListItem } from "../../api/client";
import { useL, StateBadge, Spinner, ErrorBox } from "../../app/ui";

/** Sends an already-fetched merged PDF blob to the printer via a hidden iframe. */
function printBlob(blob: Blob) {
  const url = URL.createObjectURL(blob);
  const frame = document.createElement("iframe");
  frame.style.cssText = "position:fixed;right:0;bottom:0;width:0;height:0;border:0";
  frame.src = url;
  frame.onload = () => { frame.contentWindow?.focus(); frame.contentWindow?.print(); };
  document.body.appendChild(frame);
  setTimeout(() => { URL.revokeObjectURL(url); frame.remove(); }, 60_000);
}

/**
 * FR-15 print queues.
 * - Reviewer (`mode="reviewer"`): Approved, not-yet-printed decisions, priority-ordered. Selection is
 *   **cumulative** — checking decision #N selects 1..N (reflecting the strict print order).
 * - Copyist (`mode="copyist"`): the copyist's accepted, in-preparation decisions. Selection is **arbitrary**.
 * Printing the selection renders ONE merged PDF (printed directly). Reviewer decisions then leave the
 * queue; copyist decisions stay in the queue and remain re-printable until the copy is submitted/approved.
 */
export function PrintQueuePage({ mode }: { mode: "reviewer" | "copyist" }) {
  const L = useL();
  const [rows, setRows] = useState<CopyRequestListItem[] | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [err, setErr] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(() => {
    setErr(null);
    const fn = mode === "reviewer" ? api.printQueue.reviewer : api.printQueue.copyist;
    return fn().then((d) => { setRows(d); setSelected(new Set()); }).catch((e) => setErr((e as Error).message));
  }, [mode]);
  useEffect(() => { load(); }, [load]);

  function toggle(index: number) {
    if (!rows) return;
    if (mode === "reviewer") {
      // Cumulative: selecting #N selects 1..N; clicking the current top boundary shrinks the selection.
      const isTopSelected = selected.has(rows[index].id);
      const upto = isTopSelected && index > 0 && selected.has(rows[index - 1].id) ? index - 1 : index;
      const shrinkToNone = isTopSelected && index === 0;
      setSelected(shrinkToNone ? new Set() : new Set(rows.slice(0, upto + 1).map((r) => r.id)));
    } else {
      const next = new Set(selected);
      const id = rows[index].id;
      if (next.has(id)) next.delete(id); else next.add(id);
      setSelected(next);
    }
  }

  async function printSelected() {
    if (!rows) return;
    const ids = rows.filter((r) => selected.has(r.id)).map((r) => r.id); // keep queue order
    if (ids.length === 0) { setErr(L("لم يتم اختيار أي قرار.", "No decisions selected.")); return; }
    setBusy(true); setErr(null); setOk(null);
    try {
      const blob = await api.printQueue.print(ids);
      printBlob(blob);
      // Reviewer queue items leave the queue after printing; copyist queue items remain (re-printable).
      setOk(mode === "reviewer"
        ? L(`تمت طباعة ${ids.length} قرار وإزالتها من الرتل.`, `Printed ${ids.length} decision(s); removed from the queue.`)
        : L(`تمت طباعة ${ids.length} قرار. تبقى في الرتل.`, `Printed ${ids.length} decision(s); they remain in the queue.`));
      await load();
    } catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  const title = mode === "reviewer" ? L("رتل طباعة المدقق", "Reviewer print queue") : L("رتل طباعة المحرر", "Copyist print queue");
  const hint = mode === "reviewer"
    ? L("تحديد قرار يحدد كل ما قبله تلقائياً (حسب الأولوية والتسلسل).", "Selecting a decision cumulatively selects all before it (by priority + sequence).")
    : L("حدّد أي مجموعة من القرارات للطباعة.", "Select any set of decisions to print.");

  return (
    <>
      <h1 className="page-title">{title}</h1>
      <p className="page-sub">{hint}</p>
      {err && <ErrorBox message={err} onDismiss={() => setErr(null)} />}
      {ok && <div className="okbox">{ok}</div>}
      {!rows ? <Spinner label={L("جارٍ التحميل…", "Loading…")} /> : (
        <>
          <div className="toolbar">
            <span className="muted">{L("المحدد", "Selected")}: {selected.size} / {rows.length}</span>
            <div className="spacer" />
            <button className="btn" disabled={busy || selected.size === 0} onClick={printSelected}>
              {busy ? L("جارٍ الطباعة…", "Printing…") : L("طباعة المحدد", "Print selected")}
            </button>
          </div>
          {rows.length === 0 ? (
            <p className="muted">{L("لا توجد قرارات في الرتل.", "The queue is empty.")}</p>
          ) : (
            <table className="table">
              <thead><tr>
                <th></th>
                <th>{L("رقم النسخة/المتفرق", "Copy / misc no.")}</th>
                <th>{L("المحكمة", "Court")}</th>
                <th>{L("الغرفة", "Room")}</th>
                <th>{L("رقم الأساس", "Case base no.")}</th>
                <th>{L("الحالة", "State")}</th>
              </tr></thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={r.id} className={selected.has(r.id) ? "row-accepted" : undefined}>
                    <td><input type="checkbox" checked={selected.has(r.id)} onChange={() => toggle(i)} /></td>
                    <td><strong>{r.copyNumber ?? (r.miscNumber != null ? `${L("متفرق", "misc")} ${r.miscNumber}` : "—")}</strong></td>
                    <td>{r.courtName}</td>
                    <td>{r.roomName}</td>
                    <td>{r.caseBaseNumber}</td>
                    <td><StateBadge state={r.state} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}
    </>
  );
}
