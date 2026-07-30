import { useEffect, useState, useCallback, useRef, type FormEvent } from "react";
import { api, type CopyRequestListItem, type Court, type CopyState, type RequestSearch } from "../../api/client";
import { useNav } from "../../app/nav";
import { useL, StateBadge, Spinner, ErrorBox, useSort, SortTh } from "../../app/ui";
import { useAuth } from "../../auth/AuthContext";
import { useI18n } from "../../i18n";
import { ConnectionStatus, type ConnState } from "../../components/ConnectionStatus";

const POLL_MS = 45_000; // auto-refresh interval for new/updated requests
const PAGE_SIZE = 50;   // server-paged; keeps payload + DOM bounded at any table size

const states: CopyState[] = ["Created", "InPreparation", "UnderReview", "Approved", "Unlocked"];
const stateLabel: Record<CopyState, { ar: string; en: string }> = {
  Created: { ar: "أُنشئ", en: "Created" },
  InPreparation: { ar: "قيد التحضير", en: "In preparation" },
  UnderReview: { ar: "قيد المراجعة", en: "Under review" },
  Approved: { ar: "معتمد", en: "Approved" },
  Unlocked: { ar: "مفتوح", en: "Unlocked" },
};

const empty: RequestSearch = {};

/** Role-scoped list of copy requests with an advanced-search filter bar. */
export function RequestsListPage() {
  const { navigate } = useNav();
  const { user } = useAuth();
  const { lang } = useI18n();
  const L = useL();
  const ar = lang === "ar";

  const [items, setItems] = useState<CopyRequestListItem[] | null>(null);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [courts, setCourts] = useState<Court[]>([]);
  const [filters, setFilters] = useState<RequestSearch>(empty);
  const [err, setErr] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [conn, setConn] = useState<ConnState>("online");
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const appliedRef = useRef<RequestSearch>(empty); // the search currently shown (what polling re-runs)
  const pageRef = useRef(1);                        // the page currently shown (what polling re-runs)

  // Load one page of the list. Paged server-side so even the Administrator's unscoped queue never pulls
  // the whole table (500k+). `silent` keeps the current table visible (polling + status button) and only
  // flips the connection indicator; an explicit load shows the spinner.
  const load = useCallback(async (search: RequestSearch, pg: number, silent = false) => {
    if (!silent) { setItems(null); setErr(null); }
    appliedRef.current = search; pageRef.current = pg;
    setConn("refreshing");
    try {
      const data = await api.listRequests(search, pg, PAGE_SIZE);
      setItems(data.items); setTotal(data.total); setPage(data.page);
      setConn("online"); setLastUpdated(new Date()); setErr(null);
    } catch (e) {
      setConn("offline");
      if (!silent) setErr((e as Error).message);
    }
  }, []);

  useEffect(() => { load(empty, 1); }, [load]);
  useEffect(() => { api.lookupCourts().then(setCourts).catch(() => { /* courts optional for filter */ }); }, []);

  // Auto-poll for new/updated requests; skip while the tab is hidden to avoid useless calls.
  useEffect(() => {
    const id = setInterval(() => { if (!document.hidden) load(appliedRef.current, pageRef.current, true); }, POLL_MS);
    return () => clearInterval(id);
  }, [load]);

  const title = user?.role === "Copyist" ? L("قائمة عملي", "My queue")
    : user?.role === "Reviewer" ? L("قائمة المراجعة", "Review queue")
    : user?.role === "RegistryHead" ? L("طلباتي", "My requests")
    : L("جميع الطلبات", "All requests");

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  function patch(p: Partial<RequestSearch>) { setFilters((f) => ({ ...f, ...p })); }
  function submit(e: FormEvent) { e.preventDefault(); load(filters, 1); }
  function reset() { setFilters(empty); load(empty, 1); }
  function goToPage(p: number) { if (p >= 1 && p <= totalPages) load(appliedRef.current, p); }

  const activeCount = Object.values(filters).filter(Boolean).length;

  // FR-13: the copyist may accept only the top-ranked unaccepted copy. The list arrives in server
  // priority order, so the FIRST unaccepted In-preparation item is the one available to accept; any
  // other unaccepted item is locked (blurred + not openable) until the higher-priority ones are taken.
  const isCopyist = user?.role === "Copyist";
  const acceptableId = isCopyist
    ? (items ?? []).find((r) => r.state === "InPreparation" && !r.acceptedUtc)?.id ?? null
    : null;

  const sort = useSort<CopyRequestListItem>(items ?? [], {
    copyNumber: (r) => r.copyNumber,
    court: (r) => r.courtName,
    room: (r) => r.roomName,
    caseBase: (r) => r.caseBaseNumber,
    copyist: (r) => r.assignedCopyistName,
    state: (r) => r.state,
  });

  return (
    <>
      <div className="toolbar">
        <h1 className="page-title">{title}</h1>
        <div className="spacer" />
        <ConnectionStatus state={conn} lastUpdated={lastUpdated} onRefresh={() => load(appliedRef.current, pageRef.current, true)} />
        <button className="btn btn--ghost" onClick={() => setOpen((o) => !o)}>
          {L("بحث متقدم", "Advanced search")}{activeCount ? ` (${activeCount})` : ""}
        </button>
        {user?.role === "RegistryHead" && (
          <button className="btn" onClick={() => navigate("create")}>{L("طلب جديد", "New request")}</button>
        )}
      </div>

      {open && (
        <form className="card filterbar" onSubmit={submit}>
          <div className="row">
            <label className="field"><span>{L("الحالة", "State")}</span>
              <select value={filters.state ?? ""} onChange={(e) => patch({ state: (e.target.value || undefined) as CopyState | undefined })}>
                <option value="">{L("الكل", "All")}</option>
                {states.map((s) => <option key={s} value={s}>{stateLabel[s][ar ? "ar" : "en"]}</option>)}
              </select>
            </label>
            <label className="field"><span>{L("رقم النسخة", "Copy number")}</span>
              <input value={filters.copyNumber ?? ""} onChange={(e) => patch({ copyNumber: e.target.value || undefined })} /></label>
            <label className="field"><span>{L("رقم الأساس", "Case base no.")}</span>
              <input value={filters.caseBaseNumber ?? ""} onChange={(e) => patch({ caseBaseNumber: e.target.value || undefined })} /></label>
          </div>
          <div className="row">
            <label className="field"><span>{L("المحكمة", "Court")}</span>
              <select value={filters.courtId ?? ""} onChange={(e) => patch({ courtId: e.target.value || undefined })}>
                <option value="">{L("الكل", "All")}</option>
                {courts.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </label>
            <label className="field"><span>{L("تاريخ الحجز من", "Reservation from")}</span>
              <input type="date" value={filters.fromReservation ?? ""} onChange={(e) => patch({ fromReservation: e.target.value || undefined })} /></label>
            <label className="field"><span>{L("إلى", "to")}</span>
              <input type="date" value={filters.toReservation ?? ""} onChange={(e) => patch({ toReservation: e.target.value || undefined })} /></label>
          </div>
          <div className="btn-row">
            <button className="btn" type="submit">{L("بحث", "Search")}</button>
            <button className="btn btn--ghost" type="button" onClick={reset}>{L("إعادة تعيين", "Reset")}</button>
          </div>
        </form>
      )}

      {err && <ErrorBox message={err} />}
      {!items && !err && <Spinner label={L("جارٍ التحميل…", "Loading…")} />}

      {items && (
        <table className="table">
          <thead>
            <tr>
              <SortTh label={L("رقم النسخة", "Copy no.")} k="copyNumber" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
              <SortTh label={L("المحكمة", "Court")} k="court" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
              <SortTh label={L("الغرفة", "Room")} k="room" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
              <SortTh label={L("رقم الأساس", "Case base no.")} k="caseBase" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
              <th>{L("رقم المتفرق", "Misc no.")}</th>
              <th>{L("رقم المستعجل", "Expedite no.")}</th>
              <SortTh label={L("الناسخ", "Copyist")} k="copyist" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
              <SortTh label={L("الحالة", "State")} k="state" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            </tr>
          </thead>
          <tbody>
            {items.length === 0 && (
              <tr><td className="empty" colSpan={8}>{L("لا توجد نتائج", "No results")}</td></tr>
            )}
            {sort.sorted.map((r) => {
              const unaccepted = r.state === "InPreparation" && !r.acceptedUtc;
              const acceptable = isCopyist && r.id === acceptableId;
              const locked = isCopyist && unaccepted && r.id !== acceptableId;
              // 7B: returned by the reviewer for correction and still awaiting it (state InPreparation).
              const returned = !!r.returnedForCorrectionUtc && r.state === "InPreparation";
              const cls = [r.acceptedUtc ? "row-accepted" : "", acceptable ? "row-acceptable" : "", locked ? "row-locked" : "", returned ? "row-returned" : ""]
                .filter(Boolean).join(" ") || undefined;
              return (
                <tr key={r.id} className={cls}
                  onClick={() => { if (!locked) navigate("request", r.id); }}
                  title={locked ? L("لا يمكن فتح هذا الطلب قبل قبول الطلبات الأعلى أولوية", "Cannot open until higher-priority requests are accepted")
                    : returned ? L("قرار معاد للتصحيح من المدقق", "Returned for correction by the reviewer")
                    : acceptable ? L("متاح للقبول", "Available to accept")
                    : r.acceptedUtc ? L("مقبول من الناسخ", "Accepted by the copyist") : undefined}>
                  <td><strong>{r.copyNumber ?? "—"}</strong>
                    {returned && <span className="pill--returned" title={L("معاد للتصحيح", "Returned for correction")}>↩ {L("معاد للتصحيح", "Returned")}</span>}
                  </td>
                  <td>{r.courtName}</td>
                  <td>{r.roomName}</td>
                  <td>{r.caseBaseNumber}</td>
                  <td>{r.miscNumber ?? "—"}</td>
                  <td>{r.expediteRequestNumber ?? "—"}</td>
                  <td>{r.assignedCopyistName ?? "—"}</td>
                  <td><StateBadge state={r.state} awaitingAcceptance={unaccepted} /></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}

      {items && total > 0 && (
        <div className="toolbar" style={{ marginTop: 8 }}>
          <span className="muted">
            {L(`صفحة ${page} من ${totalPages} — إجمالي ${total}`, `Page ${page} of ${totalPages} — ${total} total`)}
          </span>
          <div className="spacer" />
          <button className="btn btn--ghost" disabled={page <= 1} onClick={() => goToPage(page - 1)}>
            {L("السابق", "Previous")}
          </button>
          <button className="btn btn--ghost" disabled={page >= totalPages} onClick={() => goToPage(page + 1)}>
            {L("التالي", "Next")}
          </button>
        </div>
      )}
    </>
  );
}
