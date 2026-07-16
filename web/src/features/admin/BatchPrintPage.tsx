import { useEffect, useState } from "react";
import { api, type Court, type Room, type CopyRequestListItem } from "../../api/client";
import { useL, ErrorBox, StateBadge } from "../../app/ui";

/** Triggers a browser download of an in-memory blob (the batch ZIP). */
function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url; a.download = filename;
  document.body.appendChild(a); a.click(); a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

/**
 * FR-15 batch print (Administrator): print a set of decisions in a specific court+room between two
 * تاريخ الحجز dates — مثبتة (approved) or مسودة (non-approved). Each decision is rendered on the server
 * to its OWN PDF and the set is delivered as a ZIP. Read-only export: it never marks copies as printed
 * and is not subject to the single-print order / once-per-approval rules.
 */
export function BatchPrintPage() {
  const L = useL();
  const [courts, setCourts] = useState<Court[]>([]);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [courtId, setCourtId] = useState("");
  const [roomId, setRoomId] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [approved, setApproved] = useState(true); // true = مثبتة, false = مسودة
  const [items, setItems] = useState<CopyRequestListItem[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => { api.lookupCourts().then(setCourts).catch((e) => setErr(e.message)); }, []);
  useEffect(() => {
    setRoomId(""); setItems(null);
    if (!courtId) { setRooms([]); return; }
    api.lookupRooms(courtId).then(setRooms).catch((e) => setErr(e.message));
  }, [courtId]);
  // Any change to the filter invalidates a stale preview.
  useEffect(() => { setItems(null); }, [roomId, from, to, approved]);

  const ready = !!courtId && !!roomId && !!from && !!to && from <= to;

  async function preview() {
    setBusy(true); setErr(null);
    try { setItems(await api.batchPrintPreview(courtId, roomId, from, to, approved)); }
    catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  async function download() {
    setBusy(true); setErr(null);
    try {
      const blob = await api.batchPrintZip(courtId, roomId, from, to, approved);
      downloadBlob(blob, `batch-${approved ? "approved" : "draft"}-${from}_${to}.zip`);
    } catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  return (
    <>
      <h1 className="page-title">{L("طباعة دفعة قرارات", "Batch print decisions")}</h1>
      <p className="page-sub">
        {L("لغرفة محددة بين تاريخين (تاريخ الحجز) — كل قرار في ملف PDF مستقل داخل ملف مضغوط.",
           "For a room between two reservation dates — each decision as its own PDF inside a ZIP.")}
      </p>

      <div className="card" style={{ maxWidth: 760 }}>
        {err && <ErrorBox message={err} />}

        <div className="row">
          <label className="field">
            <span>{L("المحكمة", "Court")}</span>
            <select value={courtId} onChange={(e) => setCourtId(e.target.value)}>
              <option value="" disabled>{L("اختر المحكمة", "Select court")}</option>
              {courts.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </label>
          <label className="field">
            <span>{L("الغرفة", "Room")}</span>
            <select value={roomId} onChange={(e) => setRoomId(e.target.value)} disabled={!courtId}>
              <option value="" disabled>{L("اختر الغرفة", "Select room")}</option>
              {rooms.map((r) => <option key={r.id} value={r.id}>{r.name} ({r.code})</option>)}
            </select>
          </label>
        </div>

        <div className="row">
          <label className="field">
            <span>{L("من تاريخ (الحجز)", "From (reservation)")}</span>
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label className="field">
            <span>{L("إلى تاريخ (الحجز)", "To (reservation)")}</span>
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </label>
          <label className="field">
            <span>{L("النوع", "Kind")}</span>
            <select value={approved ? "approved" : "draft"} onChange={(e) => setApproved(e.target.value === "approved")}>
              <option value="approved">{L("مثبتة (معتمدة)", "Approved")}</option>
              <option value="draft">{L("مسودة (غير معتمدة)", "Draft")}</option>
            </select>
          </label>
        </div>

        {from && to && from > to && (
          <p className="muted" style={{ color: "var(--danger, #b00)" }}>{L("«من» يجب ألا يتجاوز «إلى».", "“From” must not exceed “To”.")}</p>
        )}

        <div className="btn-row">
          <button className="btn btn--ghost" disabled={!ready || busy} onClick={preview}>{L("عرض المطابق", "Preview matches")}</button>
          <button className="btn" disabled={!ready || busy || (items != null && items.length === 0)} onClick={download}>
            {busy ? L("جارٍ التحضير…", "Preparing…") : L("تنزيل ZIP", "Download ZIP")}
          </button>
        </div>
      </div>

      {items != null && (
        <div className="card" style={{ maxWidth: 760, marginTop: 16 }}>
          <h3 style={{ marginTop: 0 }}>{L("القرارات المطابقة", "Matching decisions")}: {items.length}</h3>
          {items.length === 0 ? (
            <p className="muted">{L("لا توجد قرارات مطابقة لهذا النطاق.", "No decisions match this range.")}</p>
          ) : (
            <table className="table">
              <thead><tr>
                <th>{L("رقم النسخة/المتفرق", "Copy / misc no.")}</th>
                <th>{L("رقم الأساس", "Base no.")}</th>
                <th>{L("تاريخ الحجز", "Reservation")}</th>
                <th>{L("الحالة", "State")}</th>
              </tr></thead>
              <tbody>
                {items.map((it) => (
                  <tr key={it.id}>
                    <td>{it.copyNumber ?? (it.miscNumber != null ? `${L("متفرق", "misc")} ${it.miscNumber}` : "—")}</td>
                    <td>{it.caseBaseNumber}</td>
                    <td>{it.reservationDate}</td>
                    <td><StateBadge state={it.state} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </>
  );
}
