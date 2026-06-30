import { useEffect, useState, useCallback, useRef, type FormEvent } from "react";
import { api, type CopyRequestListItem, type Court, type CopyState, type RequestSearch } from "../../api/client";
import { useNav } from "../../app/nav";
import { useL, StateBadge, Spinner, ErrorBox, useSort, SortTh } from "../../app/ui";
import { useAuth } from "../../auth/AuthContext";
import { useI18n } from "../../i18n";
import { ConnectionStatus, type ConnState } from "../../components/ConnectionStatus";

const POLL_MS = 45_000; // auto-refresh interval for new/updated requests

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
  const [courts, setCourts] = useState<Court[]>([]);
  const [filters, setFilters] = useState<RequestSearch>(empty);
  const [err, setErr] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [conn, setConn] = useState<ConnState>("online");
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const appliedRef = useRef<RequestSearch>(empty); // the search currently shown (what polling re-runs)

  // Load the list. `silent` keeps the current table visible (used by polling + the status button)
  // and only flips the connection indicator; an explicit load shows the spinner.
  const load = useCallback(async (search: RequestSearch, silent = false) => {
    if (!silent) { setItems(null); setErr(null); }
    appliedRef.current = search;
    setConn("refreshing");
    try {
      const data = await api.listRequests(search);
      setItems(data); setConn("online"); setLastUpdated(new Date()); setErr(null);
    } catch (e) {
      setConn("offline");
      if (!silent) setErr((e as Error).message);
    }
  }, []);

  useEffect(() => { load(empty); }, [load]);
  useEffect(() => { api.lookupCourts().then(setCourts).catch(() => { /* courts optional for filter */ }); }, []);

  // Auto-poll for new/updated requests; skip while the tab is hidden to avoid useless calls.
  useEffect(() => {
    const id = setInterval(() => { if (!document.hidden) load(appliedRef.current, true); }, POLL_MS);
    return () => clearInterval(id);
  }, [load]);

  const title = user?.role === "Copyist" ? L("قائمة عملي", "My queue")
    : user?.role === "Reviewer" ? L("قائمة المراجعة", "Review queue")
    : user?.role === "RegistryHead" ? L("طلباتي", "My requests")
    : L("جميع الطلبات", "All requests");

  function patch(p: Partial<RequestSearch>) { setFilters((f) => ({ ...f, ...p })); }
  function submit(e: FormEvent) { e.preventDefault(); load(filters); }
  function reset() { setFilters(empty); load(empty); }

  const activeCount = Object.values(filters).filter(Boolean).length;

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
        <ConnectionStatus state={conn} lastUpdated={lastUpdated} onRefresh={() => load(appliedRef.current, true)} />
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
            {sort.sorted.map((r) => (
              <tr key={r.id} onClick={() => navigate("request", r.id)} className={r.acceptedUtc ? "row-accepted" : undefined}
                title={r.acceptedUtc ? L("مقبول من الناسخ", "Accepted by the copyist") : undefined}>
                <td><strong>{r.copyNumber ?? "—"}</strong></td>
                <td>{r.courtName}</td>
                <td>{r.roomName}</td>
                <td>{r.caseBaseNumber}</td>
                <td>{r.miscNumber ?? "—"}</td>
                <td>{r.expediteRequestNumber ?? "—"}</td>
                <td>{r.assignedCopyistName ?? "—"}</td>
                <td><StateBadge state={r.state} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
