import { useEffect, useMemo, useRef, useState, type FormEvent, type KeyboardEvent } from "react";
import { api, type Court, type Room, type Lookup, type CaseCategory, type CaseUrgency, type OriginalCopyOption, type LastNumber } from "../../api/client";
import { useNav } from "../../app/nav";
import { useL, ErrorBox, categoryLabels, urgencyLabels } from "../../app/ui";
import { useAuth } from "../../auth/AuthContext";
import { useAutoSaveDraft, type AutoSaveDraftStatus } from "../../hooks/useAutoSaveDraft";
import { useI18n } from "../../i18n";

const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

function draftStatusText(status: AutoSaveDraftStatus, L: (ar: string, en: string) => string) {
  switch (status) {
    case "saving": return L("جاري حفظ المسودة...", "Saving draft...");
    case "saved": return L("تم حفظ المسودة", "Draft saved");
    case "offline": return L("غير متصل، تم الحفظ محلياً", "Offline, saved locally");
    case "syncing": return L("جاري مزامنة المسودة...", "Syncing draft...");
    case "synced": return L("تمت المزامنة", "Draft synced");
    case "error": return L("تعذر حفظ المسودة", "Could not save draft");
    default: return null;
  }
}

/**
 * FR-06 / BR-11: Registry Head creates a copy request.
 * - عادي: pick court + room; the system issues the sequential رقم النسخة.
 * - متفرق: pick an Approved عادي "original copy"; the متفرق inherits its court/room/رقم الأساس and
 *   gets only a رقم المتفرق linked to that original (no رقم النسخة).
 */
export function CreateRequestPage() {
  const { navigate } = useNav();
  const { user } = useAuth();
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
  const [originalSearch, setOriginalSearch] = useState("");
  const [lastNo, setLastNo] = useState<LastNumber | null>(null);
  const [copyistId, setCopyistId] = useState("");
  const [filingDate, setFilingDate] = useState("");
  const [caseBase, setCaseBase] = useState("");
  const [category, setCategory] = useState<CaseCategory>("Normal");
  const [urgency, setUrgency] = useState<CaseUrgency>("Normal");
  const [expediteNo, setExpediteNo] = useState("");
  const [referenceNo, setReferenceNo] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const searchSeq = useRef(0);
  const restoringDraft = useRef(false);

  const isMisc = category === "Miscellaneous";
  const draftFormKey = user ? `registry-head:create-copy-request:${user.userId}` : null;
  const draftPayload = useMemo(() => ({
    courtId,
    roomId,
    originalId,
    originalSearch,
    copyistId,
    filingDate,
    caseBase,
    category,
    urgency,
    expediteNo,
    referenceNo,
  }), [caseBase, category, copyistId, courtId, expediteNo, filingDate, originalId, originalSearch, referenceNo, roomId, urgency]);
  const autoSave = useAutoSaveDraft({
    userId: user?.userId,
    role: user?.role,
    formKey: draftFormKey,
    payload: draftPayload,
    enabled: user?.role === "RegistryHead",
    restorePrompt: L("توجد مسودة محفوظة، هل تريد استرجاعها؟", "A saved draft exists. Restore it?"),
    onRestore: (payload) => {
      restoringDraft.current = true;
      setCourtId(typeof payload.courtId === "string" ? payload.courtId : "");
      setRoomId(typeof payload.roomId === "string" ? payload.roomId : "");
      setOriginalId(typeof payload.originalId === "string" ? payload.originalId : "");
      setOriginalSearch(typeof payload.originalSearch === "string" ? payload.originalSearch : "");
      setCopyistId(typeof payload.copyistId === "string" ? payload.copyistId : "");
      setFilingDate(typeof payload.filingDate === "string" ? payload.filingDate : "");
      setCaseBase(typeof payload.caseBase === "string" ? payload.caseBase : "");
      setCategory(payload.category === "Miscellaneous" ? "Miscellaneous" : "Normal");
      setUrgency(payload.urgency === "Suspended" || payload.urgency === "Expedited" ? payload.urgency : "Normal");
      setExpediteNo(typeof payload.expediteNo === "string" ? payload.expediteNo : "");
      setReferenceNo(typeof payload.referenceNo === "string" ? payload.referenceNo : "");
      window.setTimeout(() => { restoringDraft.current = false; }, 100);
    },
  });
  const autoSaveText = draftStatusText(autoSave.status, L);

  useEffect(() => {
    api.lookupCourts().then(setCourts).catch((e) => setErr(e.message));
  }, []);

  // متفرق picker: fetch the chosen room's Approved originals from the server — filtered + capped there,
  // so the payload is bounded at any table size (500k+). Search runs on Enter only.
  function searchOriginals() {
    if (!isMisc || !roomId) { setOriginals([]); return; }
    const seq = ++searchSeq.current;
    api.originals(roomId, originalSearch.trim())
      .then((data) => { if (seq === searchSeq.current) setOriginals(data); })
      .catch((e) => { if (seq === searchSeq.current) setErr(e.message); });
  }

  // When the room changes, load the unfiltered list once.
  useEffect(() => {
    searchSeq.current += 1;
    if (!isMisc || !roomId) { setOriginals([]); return; }
    let cancelled = false;
    api.originals(roomId, "")
      .then((data) => { if (!cancelled) setOriginals(data); })
      .catch((e) => { if (!cancelled) setErr(e.message); });
    return () => { cancelled = true; };
  }, [isMisc, roomId]);

  useEffect(() => () => { searchSeq.current += 1; }, []);

  function onOriginalSearchKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter") {
      e.preventDefault();
      searchOriginals();
    }
  }

  // Copyists follow the selected court (both عادي and متفرق now pick court + room).
  useEffect(() => {
    if (!restoringDraft.current) setCopyistId("");
    if (!courtId) { setCopyists([]); return; }
    api.lookupCopyists(courtId).then(setCopyists).catch((e) => setErr(e.message));
  }, [courtId]);

  // Rooms follow the selected court (for both عادي and متفرق). Reset the room when the court changes.
  useEffect(() => {
    if (!restoringDraft.current) setRoomId("");
    if (!courtId) { setRooms([]); return; }
    api.lookupRooms(courtId).then(setRooms).catch((e) => setErr(e.message));
  }, [courtId]);

  // A stale original from another court/room must never survive a court/room change.
  useEffect(() => {
    if (!restoringDraft.current) { setOriginalId(""); setOriginalSearch(""); }
  }, [courtId, roomId]);

  // FR-03/FR-06: once court+room are chosen, show the last issued number for that scope this year.
  useEffect(() => {
    setLastNo(null);
    if (!courtId || !roomId) return;
    let cancelled = false;
    api.lastNumber(courtId, roomId, category).then((r) => { if (!cancelled) setLastNo(r); }).catch(() => {});
    return () => { cancelled = true; };
  }, [courtId, roomId, category]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setErr(null); setBusy(true);
    try {
      if (isMisc && !originalId) throw new Error(L("يجب اختيار النسخة الأصلية (قرار معتمد).", "Select the original (Approved) copy."));
      const res = await api.createRequest({
        courtId,                                           // متفرق: server re-derives court from the original
        roomId: isMisc ? EMPTY_GUID : roomId,              // متفرق: server uses the original's room
        caseBaseNumber: isMisc ? "" : caseBase,            // متفرق: server uses the original's رقم الأساس
        assignedCopyistId: copyistId,
        caseFilingDate: filingDate || null,
        category, urgency,
        expediteRequestNumber: urgency === "Expedited" ? expediteNo : null,
        referenceNumber: isMisc && referenceNo ? referenceNo : null,
        originalCopyId: isMisc ? originalId : null,
      });
      await autoSave.clearDraft();
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

        {/* Court + Room — chosen for both عادي and متفرق (متفرق uses them to narrow the originals picker). */}
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

        {/* FR-03/FR-06: last issued number for the chosen court+room scope this year, and the next to allocate. */}
        {courtId && roomId && lastNo && (
          <p className="muted" style={{ fontSize: 13 }}>
            {isMisc ? L("رقم المتفرق", "Misc no.") : L("رقم النسخة", "Copy no.")} — {L("آخر رقم صدر", "Last issued")}:{" "}
            <strong>{lastNo.last ?? L("لا يوجد", "none")}</strong> — {L("التالي", "Next")}: <strong>{lastNo.next}</strong>
          </p>
        )}

        {/* متفرق: pick the Approved original within the chosen court+room — searchable by رقم النسخة / رقم الأساس. */}
        {isMisc && (
          <div className="row">
            <label className="field" style={{ flexBasis: "100%" }}>
              <span>{L("النسخة الأصلية (قرار معتمد)", "Original copy (Approved)")}</span>
              <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                <input value={originalSearch} onChange={(e) => setOriginalSearch(e.target.value)} onKeyDown={onOriginalSearchKeyDown} disabled={!roomId}
                  placeholder={L("ابحث برقم النسخة أو رقم الأساس…", "Search by copy no. or base no.…")} />
                <button type="button" className="btn btn--ghost" onClick={searchOriginals} disabled={!roomId}>
                  {L("بحث", "Search")}
                </button>
              </div>
              <div style={{ maxHeight: 220, overflowY: "auto", border: "1px solid var(--border, #ccc)", borderRadius: 8, marginTop: 6 }}>
                {!roomId ? (
                  <p className="muted" style={{ padding: 10, margin: 0 }}>{L("اختر المحكمة والغرفة أولاً.", "Choose a court and room first.")}</p>
                ) : originals.length === 0 ? (
                  <p className="muted" style={{ padding: 10, margin: 0 }}>{L("لا توجد قرارات معتمدة مطابقة في هذه الغرفة.", "No matching Approved decisions in this room.")}</p>
                ) : originals.map((o) => (
                  <button type="button" key={o.id} onClick={() => setOriginalId(o.id)}
                    style={{
                      display: "block", width: "100%", textAlign: "start", padding: "8px 10px", cursor: "pointer",
                      border: "none", borderBottom: "1px solid var(--border, #eee)",
                      background: o.id === originalId ? "var(--green-100, #e6f4ea)" : "transparent",
                      fontWeight: o.id === originalId ? 600 : 400,
                    }}>
                    {o.copyNumber} — {L("أساس", "base")} {o.caseBaseNumber}
                  </button>
                ))}
              </div>
              {!originalId && <span className="muted" style={{ fontSize: 12 }}>{L("يجب اختيار النسخة الأصلية.", "Select the original copy.")}</span>}
            </label>
          </div>
        )}

        <div className="row">
          <label className="field">
            <span>{L("الناسخ المكلَّف", "Assigned copyist")}</span>
            <select value={copyistId} onChange={(e) => setCopyistId(e.target.value)} required disabled={!courtId}>
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
        {autoSaveText && <p className="muted" style={{ fontSize: 13 }}>{autoSaveText}</p>}

        <div className="btn-row">
          <button className="btn" type="submit" disabled={busy}>{busy ? L("جارٍ الإنشاء…", "Creating…") : L("إنشاء الطلب", "Create request")}</button>
          <button className="btn btn--ghost" type="button" onClick={() => navigate("requests")}>{L("إلغاء", "Cancel")}</button>
        </div>
      </form>
    </>
  );
}
