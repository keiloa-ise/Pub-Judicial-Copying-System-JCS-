import { useEffect, useState, useCallback } from "react";
import { api, type DeletionTargets, type DeletableCopy, type DeletableMisc } from "../../api/client";
import { useL, ErrorBox, Spinner, Modal, StateBadge } from "../../app/ui";

type Target =
  | { kind: "normal"; row: DeletableCopy }
  | { kind: "misc"; row: DeletableMisc };

/**
 * FR-16: the Registry Head's deletion window — two sections (BR-09 / BR-11):
 *  • عادي: the latest copy per court (disabled when it has linked متفرق decisions).
 *  • متفرق: the last متفرق per numbering scope.
 * Deleting rolls back the relevant counter so the next copy reuses the number (no gap); audit is kept.
 */
export function DeletionOperationsPage() {
  const L = useL();
  const [data, setData] = useState<DeletionTargets | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState<Target | null>(null);

  const load = useCallback(() => {
    setData(null); setErr(null);
    api.deletionTargets().then(setData).catch((e) => setErr(e.message));
  }, []);
  useEffect(() => { load(); }, [load]);

  function doDelete() {
    if (!confirm) return;
    const id = confirm.kind === "normal" ? confirm.row.copyRequestId : confirm.row.copyRequestId;
    setBusy(true); setErr(null); setOk(null);
    api.deleteRequest(id)
      .then(() => { setOk(L("تم حذف القرار وإعادة الترقيم دون فجوة.", "Decision deleted; numbering rolled back (no gap).")); setConfirm(null); load(); })
      .catch((e) => { setErr((e as Error).message); })
      .finally(() => setBusy(false));
  }

  const normals = data?.normals ?? [];
  const miscs = data?.miscs ?? [];

  return (
    <>
      <h1 className="page-title">{L("عمليات الحذف", "Deletion operations")}</h1>
      <p className="page-sub">
        {L("احذف آخر قرار عادي في كل محكمة، أو آخر قرار متفرق في كل مستوى ترقيم، ضمن محاكمك. يُعاد الترقيم دون فجوة، ويبقى سجل التدقيق محفوظاً.",
           "Delete the latest عادي copy per court, or the last متفرق per numbering scope, within your courts. Numbering is rolled back (no gap); the audit trail is kept.")}
      </p>

      {err && <ErrorBox message={err} />}
      {ok && <div className="okbox">{ok}</div>}

      {!data ? <Spinner label={L("جارٍ التحميل…", "Loading…")} /> : (
        <>
          <div className="card">
            <h3>{L("آخر قرار عادي (لكل محكمة)", "Latest عادي copy (per court)")}</h3>
            {normals.length === 0 ? <p className="muted">{L("لا يوجد", "None")}</p> : (
              <table className="table">
                <thead><tr>
                  <th>{L("المحكمة", "Court")}</th><th>{L("رقم النسخة", "Copy no.")}</th>
                  <th>{L("الغرفة", "Room")}</th><th>{L("الحالة", "State")}</th><th></th>
                </tr></thead>
                <tbody>
                  {normals.map((r) => (
                    <tr key={r.courtId}>
                      <td>{r.courtName}</td>
                      <td><strong>{r.copyNumber}</strong></td>
                      <td>{r.roomName}</td>
                      <td><StateBadge state={r.state} /></td>
                      <td>
                        <button className="btn btn--danger" disabled={r.hasLinkedMisc}
                          title={r.hasLinkedMisc ? L("توجد قرارات متفرقة مرتبطة — احذفها أولاً", "Has linked متفرق — delete those first") : ""}
                          onClick={() => { setOk(null); setConfirm({ kind: "normal", row: r }); }}>
                          {L("حذف", "Delete")}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          <div className="card">
            <h3>{L("آخر قرار متفرق (لكل مستوى ترقيم)", "Last متفرق (per numbering scope)")}</h3>
            {miscs.length === 0 ? <p className="muted">{L("لا يوجد", "None")}</p> : (
              <table className="table">
                <thead><tr>
                  <th>{L("المحكمة", "Court")}</th><th>{L("المستوى", "Scope")}</th>
                  <th>{L("رقم المتفرق", "Misc no.")}</th><th>{L("النسخة الأصلية", "Original")}</th>
                  <th>{L("الحالة", "State")}</th><th></th>
                </tr></thead>
                <tbody>
                  {miscs.map((r) => (
                    <tr key={r.scopeKey}>
                      <td>{r.courtName}</td>
                      <td>{r.scopeLabel}</td>
                      <td><strong>{r.miscNumber}</strong></td>
                      <td>{r.originalCopyNumber ?? "—"}</td>
                      <td><StateBadge state={r.state} /></td>
                      <td>
                        <button className="btn btn--danger" onClick={() => { setOk(null); setConfirm({ kind: "misc", row: r }); }}>
                          {L("حذف", "Delete")}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}

      <Modal open={!!confirm} onClose={() => setConfirm(null)} title={L("تأكيد حذف القرار", "Confirm deletion")}>
        {confirm && (
          <>
            <p>{L("سيُحذف القرار التالي نهائياً، ويُعاد الترقيم لإعادة استخدامه (دون فجوة):",
                  "The following decision will be permanently deleted; numbering is freed for reuse (no gap):")}</p>
            <dl className="kv">
              {confirm.kind === "normal" ? (
                <>
                  <dt>{L("المحكمة", "Court")}</dt><dd>{confirm.row.courtName}</dd>
                  <dt>{L("الغرفة", "Room")}</dt><dd>{confirm.row.roomName}</dd>
                  <dt>{L("رقم النسخة", "Copy no.")}</dt><dd>{confirm.row.copyNumber}</dd>
                </>
              ) : (
                <>
                  <dt>{L("المحكمة", "Court")}</dt><dd>{confirm.row.courtName}</dd>
                  <dt>{L("المستوى", "Scope")}</dt><dd>{confirm.row.scopeLabel}</dd>
                  <dt>{L("رقم المتفرق", "Misc no.")}</dt><dd>{confirm.row.miscNumber}</dd>
                  <dt>{L("النسخة الأصلية", "Original")}</dt><dd>{confirm.row.originalCopyNumber ?? "—"}</dd>
                </>
              )}
            </dl>
            <div className="btn-row">
              <button className="btn btn--danger" disabled={busy} onClick={doDelete}>{L("تأكيد الحذف", "Confirm delete")}</button>
              <button className="btn btn--ghost" type="button" onClick={() => setConfirm(null)}>{L("إلغاء", "Cancel")}</button>
            </div>
          </>
        )}
      </Modal>
    </>
  );
}
