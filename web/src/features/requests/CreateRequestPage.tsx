import { useEffect, useMemo, useState, type FormEvent } from "react";
import { api, type Court, type Room, type Lookup, type CaseCategory, type CaseUrgency, type OriginalCopyOption } from "../../api/client";
import { useNav } from "../../app/nav";
import { useL, ErrorBox, categoryLabels, urgencyLabels } from "../../app/ui";
import { useI18n } from "../../i18n";

const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

/**
 * FR-06 / BR-11: Registry Head creates a copy request.
 * - عادي: pick court + room; the system issues the sequential رقم النسخة.
 * - متفرق: pick an Approved عادي "original copy"; the متفرق inherits its court/room/رقم الأساس and
 *   gets only a رقم المتفرق linked to that original (no رقم النسخة).
 */
export function CreateRequestPage() {
  const { navigate } = useNav();
  const { lang } = useI18n();
  const L = useL();
  const ak = lang === "ar" ? "ar" : "en";

  const [courts, setCourts] = useState<Court[]>([]);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [copyists, setCopyists] = useState<Lookup[]>([]);
  const [originals, setOriginals] = useState<OriginalCopyOption[]>([]);
  const [courtId, setCourtId] = useState("");
  const [roomId, setRoomId] = useState("");
  const [originalId, setOriginalId] = useState("");
  const [copyistId, setCopyistId] = useState("");
  const [filingDate, setFilingDate] = useState("");
  const [caseBase, setCaseBase] = useState("");
  const [category, setCategory] = useState<CaseCategory>("Normal");
  const [urgency, setUrgency] = useState<CaseUrgency>("Normal");
  const [expediteNo, setExpediteNo] = useState("");
  const [referenceNo, setReferenceNo] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const isMisc = category === "Miscellaneous";
  const chosenOriginal = useMemo(() => originals.find((o) => o.id === originalId), [originals, originalId]);
  // For متفرق the effective court is the original's court; for عادي it's the selected court.
  const effectiveCourt = isMisc ? (chosenOriginal?.courtId ?? "") : courtId;

  useEffect(() => {
    api.lookupCourts().then(setCourts).catch((e) => setErr(e.message));
    api.originals().then(setOriginals).catch((e) => setErr(e.message));
  }, []);

  // Copyists follow the effective court (selected court, or the original's court for متفرق).
  useEffect(() => {
    setCopyistId("");
    if (!effectiveCourt) { setCopyists([]); return; }
    api.lookupCopyists(effectiveCourt).then(setCopyists).catch((e) => setErr(e.message));
  }, [effectiveCourt]);

  // Rooms apply to عادي only.
  useEffect(() => {
    if (isMisc || !courtId) { setRooms([]); setRoomId(""); return; }
    api.lookupRooms(courtId).then(setRooms).catch((e) => setErr(e.message));
  }, [courtId, isMisc]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setErr(null); setBusy(true);
    try {
      const res = await api.createRequest({
        courtId: isMisc ? (chosenOriginal?.courtId ?? "") : courtId,
        roomId: isMisc ? EMPTY_GUID : roomId,              // متفرق: server uses the original's room
        caseBaseNumber: isMisc ? "" : caseBase,            // متفرق: server uses the original's رقم الأساس
        assignedCopyistId: copyistId,
        caseFilingDate: filingDate || null,
        category, urgency,
        expediteRequestNumber: urgency === "Expedited" ? expediteNo : null,
        referenceNumber: isMisc && referenceNo ? referenceNo : null,
        originalCopyId: isMisc ? originalId : null,
      });
      navigate("request", res.id);
    } catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  return (
    <>
      <h1 className="page-title">{L("طلب نسخة جديد", "New copy request")}</h1>
      <p className="page-sub">
        {isMisc
          ? L("القرار المتفرق يستند إلى نسخة معتمدة ويأخذ رقم متفرق فقط (دون رقم نسخة).", "A متفرق is based on an Approved copy and gets only a misc number (no copy number).")
          : L("يُصدر النظام رقم النسخة تلقائيًا.", "The system issues the copy number automatically.")}
      </p>

      <form className="card" style={{ maxWidth: 720 }} onSubmit={submit}>
        {err && <ErrorBox message={err} />}

        {/* Category first — it drives the rest of the form */}
        <div className="row">
          <label className="field">
            <span>{L("التصنيف", "Category")}</span>
            <select value={category} onChange={(e) => { setCategory(e.target.value as CaseCategory); setErr(null); }} required>
              {(["Normal", "Miscellaneous"] as CaseCategory[]).map((v) =>
                <option key={v} value={v}>{categoryLabels[v][ak]}</option>)}
            </select>
          </label>
        </div>

        {isMisc ? (
          <div className="row">
            <label className="field" style={{ flex: 2 }}>
              <span>{L("النسخة الأصلية (قرار معتمد)", "Original copy (Approved)")}</span>
              <select value={originalId} onChange={(e) => setOriginalId(e.target.value)} required>
                <option value="" disabled>{L("اختر النسخة الأصلية", "Select original copy")}</option>
                {originals.map((o) => (
                  <option key={o.id} value={o.id}>{o.copyNumber} — {o.courtName} — {L("أساس", "base")} {o.caseBaseNumber}</option>
                ))}
              </select>
            </label>
          </div>
        ) : (
          <div className="row">
            <label className="field">
              <span>{L("المحكمة", "Court")}</span>
              <select value={courtId} onChange={(e) => setCourtId(e.target.value)} required>
                <option value="" disabled>{L("اختر المحكمة", "Select court")}</option>
                {courts.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </label>
            <label className="field">
              <span>{L("الغرفة", "Room")}</span>
              <select value={roomId} onChange={(e) => setRoomId(e.target.value)} required disabled={!courtId}>
                <option value="" disabled>{L("اختر الغرفة", "Select room")}</option>
                {rooms.map((r) => <option key={r.id} value={r.id}>{r.name} ({r.code})</option>)}
              </select>
            </label>
          </div>
        )}

        <div className="row">
          <label className="field">
            <span>{L("الناسخ المكلَّف", "Assigned copyist")}</span>
            <select value={copyistId} onChange={(e) => setCopyistId(e.target.value)} required disabled={!effectiveCourt}>
              <option value="" disabled>{L("اختر الناسخ", "Select copyist")}</option>
              {copyists.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </label>
          {!isMisc && (
            <label className="field">
              <span>{L("رقم الأساس", "Case base number")}</span>
              <input value={caseBase} onChange={(e) => setCaseBase(e.target.value)} required />
            </label>
          )}
          <label className="field">
            <span>{L("قيد الدعوى", "Case filing date")}</span>
            <input type="date" value={filingDate} onChange={(e) => setFilingDate(e.target.value)} />
          </label>
        </div>

        <div className="row">
          <label className="field">
            <span>{L("الحالة", "Status")}</span>
            <select value={urgency} onChange={(e) => { setUrgency(e.target.value as CaseUrgency); if (e.target.value !== "Expedited") setExpediteNo(""); }} required>
              {(["Normal", "Suspended", "Expedited"] as CaseUrgency[]).map((v) =>
                <option key={v} value={v}>{urgencyLabels[v][ak]}</option>)}
            </select>
          </label>
          {urgency === "Expedited" && (
            <label className="field">
              <span>{L("رقم طلب الاستعجال", "Expedite request no.")}</span>
              <input value={expediteNo} onChange={(e) => setExpediteNo(e.target.value)} required />
            </label>
          )}
          {isMisc && (
            <label className="field">
              <span>{L("رقم المرجع (اختياري)", "Reference no. (optional)")}</span>
              <input value={referenceNo} onChange={(e) => setReferenceNo(e.target.value)} />
            </label>
          )}
        </div>

        <p className="muted" style={{ fontSize: 13 }}>
          {L("يُسجّل «تاريخ الحجز» تلقائياً من النظام عند الإنشاء.", "The reservation date is set automatically by the system at creation.")}
        </p>

        <div className="btn-row">
          <button className="btn" type="submit" disabled={busy}>{busy ? L("جارٍ الإنشاء…", "Creating…") : L("إنشاء الطلب", "Create request")}</button>
          <button className="btn btn--ghost" type="button" onClick={() => navigate("requests")}>{L("إلغاء", "Cancel")}</button>
        </div>
      </form>
    </>
  );
}
