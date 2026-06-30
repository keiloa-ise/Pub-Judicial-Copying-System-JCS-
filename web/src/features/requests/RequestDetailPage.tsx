import { useEffect, useState, useCallback, Fragment } from "react";
import { api, type CopyRequestDetail, type AuditEntry } from "../../api/client";
import { useNav } from "../../app/nav";
import { useL, StateBadge, Spinner, ErrorBox, auditLabels, categoryLabels, urgencyLabels } from "../../app/ui";
import { useAuth } from "../../auth/AuthContext";
import { useI18n } from "../../i18n";

// FR-13: stage names for the per-stage timeline — the stage that FOLLOWS each audit action.
const STAGE_AR: Record<string, string> = {
  Create: "بانتظار قبول الناسخ", Accept: "التحضير لدى الناسخ", Submit: "المراجعة لدى المدقق",
  Return: "إعادة التحضير", Approve: "بعد الاعتماد", Unlock: "بعد الفتح", Edit: "تعديل", Expedite: "بعد التصعيد",
};
function fmtDuration(ms: number): string {
  const mins = Math.max(0, Math.round(ms / 60000));
  if (mins < 60) return `${mins} دقيقة`;
  const h = Math.floor(mins / 60), m = mins % 60;
  if (h < 24) return `${h} ساعة${m ? ` و${m} د` : ""}`;
  const d = Math.floor(h / 24), hh = h % 24;
  return `${d} يوم${hh ? ` و${hh} س` : ""}`;
}

export function RequestDetailPage({ id }: { id: string }) {
  const { navigate } = useNav();
  const { user } = useAuth();
  const { lang } = useI18n();
  const L = useL();
  const [detail, setDetail] = useState<CopyRequestDetail | null>(null);
  const [audit, setAudit] = useState<AuditEntry[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setErr(null);
    try {
      const [d, a] = await Promise.all([api.getRequest(id), api.getAudit(id)]);
      setDetail(d); setAudit(a);
    } catch (e) { setErr((e as Error).message); }
  }, [id]);

  useEffect(() => { load(); }, [load]);

  async function act(fn: () => Promise<unknown>) {
    setBusy(true); setErr(null);
    try { await fn(); await load(); }
    catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  if (err && !detail) return <ErrorBox message={err} />;
  if (!detail) return <Spinner label={L("جارٍ التحميل…", "Loading…")} />;

  const isAssignedCopyist = user?.role === "Copyist" && detail.assignedCopyistId === user.userId;
  // Audit is newest-first, so the first matching entry is the most recent.
  const lastReturn = audit.find((a) => a.action === "Return" && a.reason);
  const lastUnlock = audit.find((a) => a.action === "Unlock" && a.reason);
  const fields: Record<string, unknown> = (() => {
    try { return JSON.parse(detail.fieldValuesJson || "{}"); } catch { return {}; }
  })();

  return (
    <>
      <div className="toolbar">
        <button className="linkbtn" style={{ color: "var(--green-800)" }} onClick={() => navigate("requests")}>
          ← {L("رجوع للقائمة", "Back to list")}
        </button>
        <div className="spacer" />
        <StateBadge state={detail.state} />
      </div>

      <h1 className="page-title">{L("النسخة رقم", "Copy")} {detail.copyNumber ?? "—"}</h1>
      {err && <ErrorBox message={err} />}

      {detail.state === "InPreparation" && lastReturn && (
        <div className="returnbanner">
          <strong>{L("أُعيدت للتصحيح من المدقق", "Returned for correction by the reviewer")}</strong>
          {lastReturn.reason}
        </div>
      )}

      {detail.state === "Unlocked" && lastUnlock && (
        <div className="returnbanner">
          <strong>{L("فُتحت النسخة بواسطة المسؤول", "Unlocked by the administrator")}</strong>
          {lastUnlock.reason}
        </div>
      )}

      <div className="card">
        <dl className="kv">
          <dt>{L("المحكمة", "Court")}</dt><dd>{detail.courtName}</dd>
          <dt>{L("الغرفة", "Room")}</dt><dd>{detail.roomName}</dd>
          <dt>{L("رقم الأساس", "Case base no.")}</dt><dd>{detail.caseBaseNumber}</dd>
          <dt>{L("قيد الدعوى", "Case filing date")}</dt><dd>{detail.caseFilingDate || "—"}</dd>
          <dt>{L("تاريخ الحجز", "Reservation date")}</dt><dd>{detail.reservationDate}</dd>
          <dt>{L("التصنيف", "Category")}</dt><dd>{categoryLabels[detail.category]?.[lang === "ar" ? "ar" : "en"] ?? detail.category}</dd>
          <dt>{L("الحالة", "Status")}</dt><dd>{urgencyLabels[detail.urgency]?.[lang === "ar" ? "ar" : "en"] ?? detail.urgency}</dd>
          {detail.expediteRequestNumber && (
            <><dt>{L("رقم طلب الاستعجال", "Expedite request no.")}</dt><dd>{detail.expediteRequestNumber}</dd></>
          )}
          {detail.referenceNumber && (
            <><dt>{L("رقم المرجع", "Reference no.")}</dt><dd>{detail.referenceNumber}</dd></>
          )}
          {detail.miscNumber != null && (
            <><dt>{L("رقم المتفرق", "Misc no.")}</dt><dd>{detail.miscNumber}</dd></>
          )}
          <dt>{L("الناسخ", "Copyist")}</dt><dd>{detail.assignedCopyistName ?? "—"}</dd>
          {detail.acceptedUtc && (
            <><dt>{L("تاريخ القبول", "Accepted on")}</dt>
              <dd>{new Date(detail.acceptedUtc).toLocaleString(lang === "ar" ? "ar" : "en-GB")}</dd></>
          )}
        </dl>
      </div>

      {/* BR-11: a متفرق links back to its original copy */}
      {detail.originalCopyId && (
        <div className="returnbanner">
          <strong>{L("قرار متفرق", "Misc decision")}</strong>
          {L("مستند إلى النسخة: ", "Based on copy: ")}
          <button className="linkbtn" style={{ color: "var(--green-800)" }} onClick={() => navigate("request", detail.originalCopyId!)}>
            {detail.originalCopyNumber ?? "—"}
          </button>
        </div>
      )}

      {/* BR-11: an original copy lists its linked متفرق decisions */}
      {detail.linkedMisc.length > 0 && (
        <div className="card">
          <h3>{L("القرارات المتفرقة المرتبطة", "Linked misc decisions")}</h3>
          <table className="table">
            <thead><tr>
              <th>{L("رقم المتفرق", "Misc no.")}</th>
              <th>{L("رقم المرجع", "Reference")}</th>
              <th>{L("تاريخ الحجز", "Reservation")}</th>
              <th>{L("الحالة", "State")}</th>
            </tr></thead>
            <tbody>
              {detail.linkedMisc.map((m) => (
                <tr key={m.id} onClick={() => navigate("request", m.id)}>
                  <td>{m.miscNumber ?? "—"}</td>
                  <td>{m.referenceNumber ?? "—"}</td>
                  <td>{m.reservationDate}</td>
                  <td><StateBadge state={m.state} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {Object.keys(fields).length > 0 && (
        <div className="card">
          <h3>{L("حقول النموذج", "Form fields")}</h3>
          <dl className="kv">
            {Object.entries(fields).map(([k, v]) => (<Fragment key={k}><dt>{k}</dt><dd>{String(v)}</dd></Fragment>))}
          </dl>
        </div>
      )}

      <div className="card">
        <h3>{L("نص النسخة", "Copy text")}</h3>
        <div className="bodybox">{detail.body || <span className="muted">{L("لا يوجد نص بعد", "No text yet")}</span>}</div>
      </div>

      {/* Role + state actions */}
      <div className="btn-row">
        <button className="btn btn--gold" onClick={() => navigate("print", detail.id)}>
          {L("معاينة وطباعة (إعلام الحكم)", "Preview & print (judgment notice)")}
        </button>
        {/* FR-07: the copyist must accept before editing. */}
        {isAssignedCopyist && detail.state === "InPreparation" && !detail.acceptedUtc && (
          <button className="btn btn--gold" disabled={busy} onClick={() => act(() => api.accept(detail.id))}>
            {L("قبول القرار", "Accept decision")}
          </button>
        )}
        {isAssignedCopyist && ((detail.state === "InPreparation" && detail.acceptedUtc) || detail.state === "Unlocked") && (
          <button className="btn" onClick={() => navigate("prepare", detail.id)}>
            {detail.state === "Unlocked" ? L("تحرير النسخة المفتوحة", "Edit unlocked copy") : L("تحرير وإرسال", "Edit & submit")}
          </button>
        )}
        {/* FR-06: Registry Head escalates a non-approved copy to مستعجل. */}
        {user?.role === "RegistryHead" && detail.state !== "Approved" && detail.urgency !== "Expedited" && (
          <button className="btn btn--ghost" disabled={busy} onClick={() => {
            const no = window.prompt(L("رقم طلب الاستعجال:", "Expedite request number:")) ?? "";
            if (no.trim()) act(() => api.expedite(detail.id, no.trim()));
          }}>{L("تصعيد إلى مستعجل", "Escalate to expedited")}</button>
        )}
        {user?.role === "Reviewer" && detail.state === "UnderReview" && (
          <>
            <button className="btn" disabled={busy} onClick={() => act(() => api.approve(detail.id))}>{L("اعتماد", "Approve")}</button>
            <button className="btn" disabled={busy} onClick={() => navigate("prepare", detail.id)}>{L("تصحيح مباشر", "Correct directly")}</button>
            <button className="btn btn--ghost" disabled={busy} onClick={() => {
              const c = window.prompt(L("سبب الإعادة للتصحيح:", "Corrections / reason for return:")) ?? "";
              if (c.trim()) act(() => api.returnForCorrection(detail.id, c.trim()));
            }}>{L("إعادة للتصحيح", "Return for correction")}</button>
          </>
        )}
        {user?.role === "Administrator" && detail.state === "Approved" && (
          <button className="btn btn--danger" disabled={busy} onClick={() => {
            const reason = window.prompt(L("سبب الفتح (إلزامي):", "Unlock reason (required):")) ?? "";
            if (reason.trim()) act(() => api.unlock(detail.id, reason.trim()));
          }}>{L("فتح النسخة المعتمدة", "Unlock approved copy")}</button>
        )}
      </div>

      {audit.length > 1 && (
        <div className="card" style={{ marginTop: 20 }}>
          <h3>{L("المخطط الزمني للمراحل", "Stage timeline")}</h3>
          <ul className="timeline">
            {[...audit].reverse().map((a, i, arr) => {
              const start = new Date(a.timestampUtc).getTime();
              const next = arr[i + 1];
              const durMs = next ? new Date(next.timestampUtc).getTime() - start : null;
              const stage = STAGE_AR[a.action] ?? (auditLabels[a.action]?.ar ?? a.action);
              return (
                <li key={i} className="timeline__row">
                  <span className="timeline__dot" />
                  <div className="timeline__body">
                    <div className="timeline__head">
                      <strong>{auditLabels[a.action]?.[lang === "ar" ? "ar" : "en"] ?? a.action}</strong>
                      <span className="muted">{new Date(a.timestampUtc).toLocaleString(lang === "ar" ? "ar" : "en-GB")}</span>
                    </div>
                    {durMs != null
                      ? <div className="timeline__stage">{L("المرحلة", "Stage")}: {stage} — <strong>{fmtDuration(durMs)}</strong></div>
                      : <div className="timeline__stage muted">{L("الحالة الحالية", "current state")}</div>}
                  </div>
                </li>
              );
            })}
          </ul>
        </div>
      )}

      <div className="card" style={{ marginTop: 20 }}>
        <h3>{L("سجل التدقيق", "Audit history")}</h3>
        <ul className="audit">
          {audit.length === 0 && <li className="muted">{L("لا يوجد سجل", "No entries")}</li>}
          {audit.map((a, i) => (
            <li key={i}>
              <span className="when">{new Date(a.timestampUtc).toLocaleString(lang === "ar" ? "ar" : "en-GB")}</span>
              <span className="who">{a.actorName}</span>
              <span>{auditLabels[a.action]?.[lang === "ar" ? "ar" : "en"] ?? a.action}</span>
              {a.reason && <span className="muted">— {a.reason}</span>}
            </li>
          ))}
        </ul>
      </div>
    </>
  );
}
