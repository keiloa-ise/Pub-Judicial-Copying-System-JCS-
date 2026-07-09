import { useEffect, useState, type FormEvent } from "react";
import { api, type Court, type Room, type CopyNumberCounter, type MiscNumberCounter, type NumberingPolicy } from "../../api/client";
import { useL, ErrorBox, Spinner, numberingPolicyLabels } from "../../app/ui";
import { useI18n } from "../../i18n";

const LEVELS = Array.from({ length: 26 }, (_, i) => String.fromCharCode(65 + i)); // A..Z
const THIS_YEAR = new Date().getFullYear();

/** FR-17: at go-live the Administrator seeds the "last issued number" for each auto-generated
 *  sequence — رقم النسخة (per court/year) and رقم المتفرق (per scope/year) — so the system continues
 *  at the next number. The server refuses any value below the highest number already used. */
export function NumberingStartsPage() {
  const L = useL();
  const { lang } = useI18n();
  const ak = lang === "ar" ? "ar" : "en";

  const [courts, setCourts] = useState<Court[]>([]);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [copyC, setCopyC] = useState<CopyNumberCounter[] | null>(null);
  const [miscC, setMiscC] = useState<MiscNumberCounter[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [cCourt, setCCourt] = useState(""); const [cRoom, setCRoom] = useState(""); // cRoom "" = court-wide scope
  const [cYear, setCYear] = useState(THIS_YEAR); const [cLast, setCLast] = useState(0);
  const [mCourt, setMCourt] = useState(""); const [mScope, setMScope] = useState<NumberingPolicy>("Court");
  const [mRoom, setMRoom] = useState(""); const [mLevel, setMLevel] = useState("A");
  const [mYear, setMYear] = useState(THIS_YEAR); const [mLast, setMLast] = useState(0);

  const load = () => {
    api.admin.listCopyCounters().then(setCopyC).catch((e) => setErr(e.message));
    api.admin.listMiscCounters().then(setMiscC).catch((e) => setErr(e.message));
  };
  useEffect(() => {
    Promise.all([api.admin.listCourts(), api.admin.listRooms()])
      .then(([c, r]) => { setCourts(c); setRooms(r); }).catch((e) => setErr(e.message));
    load();
  }, []);

  async function run(fn: () => Promise<unknown>) {
    setErr(null); setOk(null); setBusy(true);
    try { await fn(); setOk(L("تم الحفظ.", "Saved.")); load(); }
    catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }
  function saveCopy(e: FormEvent) { e.preventDefault(); if (!cCourt) return; run(() => api.admin.setCopyCounter(cCourt, cRoom || null, cYear, cLast)); }
  function saveMisc(e: FormEvent) {
    e.preventDefault(); if (!mCourt) return;
    run(() => api.admin.setMiscCounter(mCourt, mScope, mScope === "Room" ? mRoom : null, mScope === "Special" ? mLevel : null, mYear, mLast));
  }

  const mCourtRooms = rooms.filter((r) => r.courtId === mCourt);
  // Copy scope: court-wide (all court-level rooms) or a specific room-level room.
  const cCourtRoomLevel = rooms.filter((r) => r.courtId === cCourt && r.copyNumberingPolicy === "Room");

  return (
    <>
      <h1 className="page-title">{L("ضبط بدايات الترقيم", "Numbering start points")}</h1>
      <p className="page-sub">
        {L("عند الإطلاق: أدخل «آخر رقم صدر» لكل محكمة/مستوى وسنة، فيبدأ النظام من الرقم التالي. لا يمكن الضبط أقل من أعلى رقم مُستخدَم فعلاً في النظام.",
           "At go-live: enter the last issued number per court/scope and year; the system continues at the next number. You can't set below the highest number already used.")}
      </p>
      {err && <ErrorBox message={err} />}
      {ok && <div className="okbox">{ok}</div>}

      <div className="card">
        <h3>{L("رقم النسخة (حسب النطاق والسنة)", "Copy number (per scope & year)")}</h3>
        <form className="row" onSubmit={saveCopy} style={{ alignItems: "flex-end" }}>
          <label className="field"><span>{L("المحكمة", "Court")}</span>
            <select value={cCourt} onChange={(e) => { setCCourt(e.target.value); setCRoom(""); }} required>
              <option value="" disabled>{L("اختر", "Select")}</option>
              {courts.map((c) => <option key={c.id} value={c.id}>{c.name} ({c.code})</option>)}
            </select></label>
          <label className="field"><span>{L("النطاق", "Scope")}</span>
            <select value={cRoom} onChange={(e) => setCRoom(e.target.value)} disabled={!cCourt}>
              <option value="">{L("مستوى المحكمة", "Court level")}</option>
              {cCourtRoomLevel.map((r) => <option key={r.id} value={r.id}>{r.name} ({r.code})</option>)}
            </select></label>
          <label className="field" style={{ maxWidth: 120 }}><span>{L("السنة", "Year")}</span>
            <input type="number" value={cYear} onChange={(e) => setCYear(+e.target.value)} required /></label>
          <label className="field" style={{ maxWidth: 170 }}><span>{L("آخر رقم صدر", "Last issued no.")}</span>
            <input type="number" min={0} value={cLast} onChange={(e) => setCLast(+e.target.value)} required /></label>
          <button className="btn" disabled={busy}>{L("حفظ", "Save")}</button>
        </form>
        {!copyC ? <Spinner /> : copyC.length > 0 && (
          <table className="table">
            <thead><tr><th>{L("المحكمة", "Court")}</th><th>{L("النطاق", "Scope")}</th><th>{L("السنة", "Year")}</th><th>{L("آخر رقم", "Last no.")}</th></tr></thead>
            <tbody>{copyC.map((x) => <tr key={x.courtId + (x.roomId ?? "court") + x.year}><td>{x.courtName} ({x.courtCode})</td><td>{x.scopeLabel}</td><td>{x.year}</td><td>{x.lastNumber}</td></tr>)}</tbody>
          </table>
        )}
      </div>

      <div className="card">
        <h3>{L("رقم المتفرق (لكل نطاق وسنة)", "Misc number (per scope & year)")}</h3>
        <form className="row" onSubmit={saveMisc} style={{ alignItems: "flex-end" }}>
          <label className="field"><span>{L("المحكمة", "Court")}</span>
            <select value={mCourt} onChange={(e) => { setMCourt(e.target.value); setMRoom(""); }} required>
              <option value="" disabled>{L("اختر", "Select")}</option>
              {courts.map((c) => <option key={c.id} value={c.id}>{c.name} ({c.code})</option>)}
            </select></label>
          <label className="field" style={{ maxWidth: 200 }}><span>{L("النطاق", "Scope")}</span>
            <select value={mScope} onChange={(e) => setMScope(e.target.value as NumberingPolicy)}>
              {(["Court", "Room", "Special"] as NumberingPolicy[]).map((p) => <option key={p} value={p}>{numberingPolicyLabels[p][ak]}</option>)}
            </select></label>
          {mScope === "Room" && (
            <label className="field"><span>{L("الغرفة", "Room")}</span>
              <select value={mRoom} onChange={(e) => setMRoom(e.target.value)} required disabled={!mCourt}>
                <option value="" disabled>{L("اختر", "Select")}</option>
                {mCourtRooms.map((r) => <option key={r.id} value={r.id}>{r.name} ({r.code})</option>)}
              </select></label>
          )}
          {mScope === "Special" && (
            <label className="field" style={{ maxWidth: 120 }}><span>{L("المستوى", "Level")}</span>
              <select value={mLevel} onChange={(e) => setMLevel(e.target.value)}>{LEVELS.map((l) => <option key={l} value={l}>{l}</option>)}</select></label>
          )}
          <label className="field" style={{ maxWidth: 120 }}><span>{L("السنة", "Year")}</span>
            <input type="number" value={mYear} onChange={(e) => setMYear(+e.target.value)} required /></label>
          <label className="field" style={{ maxWidth: 170 }}><span>{L("آخر رقم صدر", "Last issued no.")}</span>
            <input type="number" min={0} value={mLast} onChange={(e) => setMLast(+e.target.value)} required /></label>
          <button className="btn" disabled={busy}>{L("حفظ", "Save")}</button>
        </form>
        {!miscC ? <Spinner /> : miscC.length > 0 && (
          <table className="table">
            <thead><tr><th>{L("المحكمة", "Court")}</th><th>{L("النطاق", "Scope")}</th><th>{L("السنة", "Year")}</th><th>{L("آخر رقم", "Last no.")}</th></tr></thead>
            <tbody>{miscC.map((x) => <tr key={x.scopeKey + x.year}><td>{x.courtName}</td><td>{x.scopeLabel}</td><td>{x.year}</td><td>{x.lastNumber}</td></tr>)}</tbody>
          </table>
        )}
      </div>
    </>
  );
}
