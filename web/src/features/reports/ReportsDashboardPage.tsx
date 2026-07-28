import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Chart as ChartJS, CategoryScale, LinearScale, BarElement, Tooltip, Legend, type ChartOptions,
} from "chart.js";
import { Bar } from "react-chartjs-2";
import {
  api, downloadReport,
  type Court, type Room, type UserDto, type CopyState,
  type ReportFilter, type ReportSummary, type CountRow, type TurnaroundReport, type CopyRow,
  type Paged, type ReportExportType, type JudgeWorkLogRow, type CopyistAccuracyRow,
} from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import { useL, ErrorBox, Spinner, StateBadge, useSort, SortTh } from "../../app/ui";

ChartJS.register(CategoryScale, LinearScale, BarElement, Tooltip, Legend);

const states: CopyState[] = ["Created", "InPreparation", "UnderReview", "Approved", "Unlocked"];
const stateAr: Record<CopyState, string> = {
  Created: "أُنشئ", InPreparation: "قيد التحضير", UnderReview: "قيد المراجعة", Approved: "معتمد", Unlocked: "مفتوح",
};

type Tab = ReportExportType;

/** FR-13 reporting dashboard. Filters + summary cards + per-court chart + tabular reports with
 *  CSV/Excel export. All data is role/court-scoped server-side; this UI never assumes authorization. */
export function ReportsDashboardPage() {
  const L = useL();
  const { user } = useAuth();
  const isAdmin = user?.role === "Administrator";

  // Filter inputs (draft) vs. applied filter (used for fetches).
  const [draft, setDraft] = useState<ReportFilter>({});
  const [filter, setFilter] = useState<ReportFilter>({});
  const [tab, setTab] = useState<Tab>("by-court");
  const [page, setPage] = useState(1);

  const [courts, setCourts] = useState<Court[]>([]);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [copyists, setCopyists] = useState<UserDto[]>([]);
  const [reviewers, setReviewers] = useState<UserDto[]>([]);

  const [summary, setSummary] = useState<ReportSummary | null>(null);
  const [chart, setChart] = useState<CountRow[]>([]);
  const [counts, setCounts] = useState<CountRow[] | null>(null);
  const [turnaround, setTurnaround] = useState<TurnaroundReport | null>(null);
  const [copies, setCopies] = useState<Paged<CopyRow> | null>(null);
  const [workLog, setWorkLog] = useState<Paged<JudgeWorkLogRow> | null>(null);
  const [accuracy, setAccuracy] = useState<CopyistAccuracyRow[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Reference data for the filter dropdowns.
  useEffect(() => { api.lookupCourts().then(setCourts).catch(() => { /* optional */ }); }, []);
  useEffect(() => {
    if (!draft.courtId) { setRooms([]); return; }
    api.lookupRooms(draft.courtId).then(setRooms).catch(() => setRooms([]));
  }, [draft.courtId]);
  useEffect(() => {
    if (!isAdmin) return;
    api.admin.listUsers().then((us) => {
      setCopyists(us.filter((u) => u.role === "Copyist"));
      setReviewers(us.filter((u) => u.role === "Reviewer"));
    }).catch(() => { /* optional */ });
  }, [isAdmin]);

  const load = useCallback(async (f: ReportFilter, activeTab: Tab, pg: number) => {
    setErr(null); setBusy(true);
    try {
      const [sum, ch] = await Promise.all([api.reports.summary(f), api.reports.byCourt(f)]);
      setSummary(sum); setChart(ch);
      if (activeTab === "turnaround") { setTurnaround(await api.reports.turnaround(f)); setCounts(null); setCopies(null); setWorkLog(null); setAccuracy(null); }
      else if (activeTab === "copies") { setCopies(await api.reports.copies(f, pg, 50)); setCounts(null); setTurnaround(null); setWorkLog(null); setAccuracy(null); }
      else if (activeTab === "judge-work-log") { setWorkLog(await api.reports.judgeWorkLog(f, pg, 50)); setCounts(null); setTurnaround(null); setCopies(null); setAccuracy(null); }
      else if (activeTab === "copyist-accuracy") { setAccuracy(await api.reports.copyistAccuracy(f)); setCounts(null); setTurnaround(null); setCopies(null); setWorkLog(null); }
      else {
        const fn = activeTab === "by-court" ? api.reports.byCourt
          : activeTab === "by-room" ? api.reports.byRoom
          : activeTab === "by-copyist" ? api.reports.byCopyist
          : activeTab === "by-head" ? api.reports.byHead
          : activeTab === "by-judge" ? api.reports.byJudge
          : api.reports.byReviewer;
        setCounts(await fn(f)); setTurnaround(null); setCopies(null); setWorkLog(null); setAccuracy(null);
      }
    } catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }, []);

  // Reports must not run without a bounded date range (enforced server-side too). Applied filters only
  // trigger a load once both dates are set — no unfiltered full-table scans on open.
  const hasRange = !!(filter.fromDate && filter.toDate);
  useEffect(() => { if (hasRange) load(filter, tab, page); }, [load, filter, tab, page, hasRange]);

  function apply() {
    if (!draft.fromDate || !draft.toDate) {
      setErr(L("يجب تحديد المدى التاريخي (من/إلى) قبل عرض التقرير.",
        "A date range (from/to) is required before running a report."));
      return;
    }
    if (draft.fromDate > draft.toDate) {
      setErr(L("«من تاريخ» يجب أن يكون قبل «إلى تاريخ» أو يساويه. الرجاء تصحيح تاريخ البداية.",
        "The start date must be before or equal to the end date. Please correct the start date."));
      return;
    }
    setErr(null); setPage(1); setFilter(draft);
  }
  function reset() { setDraft({}); setPage(1); setFilter({}); setSummary(null); setChart([]); setCounts(null); setTurnaround(null); setCopies(null); setWorkLog(null); setAccuracy(null); }
  const patch = (p: Partial<ReportFilter>) => setDraft((d) => ({ ...d, ...p }));

  return (
    <>
      <h1 className="page-title">{L("التقارير", "Reports")}</h1>
      {err && <ErrorBox message={err} />}

      {/* ── Filters ── */}
      <form className="card filterbar" onSubmit={(e) => { e.preventDefault(); apply(); }}>
        <div className="row">
          <label className="field"><span>{L("من تاريخ", "From date")}</span>
            <input type="date" value={draft.fromDate ?? ""} max={draft.toDate}
              onChange={(e) => patch({ fromDate: e.target.value || undefined })} /></label>
          <label className="field"><span>{L("إلى تاريخ", "To date")}</span>
            <input type="date" value={draft.toDate ?? ""} min={draft.fromDate}
              onChange={(e) => patch({ toDate: e.target.value || undefined })} /></label>
          <label className="field"><span>{L("الحالة", "Status")}</span>
            <select value={draft.status ?? ""} onChange={(e) => patch({ status: (e.target.value || undefined) as CopyState | undefined })}>
              <option value="">{L("الكل", "All")}</option>
              {states.map((s) => <option key={s} value={s}>{stateAr[s]}</option>)}
            </select></label>
        </div>
        <div className="row">
          <label className="field"><span>{L("المحكمة", "Court")}</span>
            <select value={draft.courtId ?? ""} onChange={(e) => patch({ courtId: e.target.value || undefined, roomId: undefined })}>
              <option value="">{L("الكل", "All")}</option>
              {courts.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select></label>
          <label className="field"><span>{L("الغرفة", "Room")}</span>
            <select value={draft.roomId ?? ""} onChange={(e) => patch({ roomId: e.target.value || undefined })} disabled={!draft.courtId}>
              <option value="">{L("الكل", "All")}</option>
              {rooms.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
            </select></label>
          {isAdmin && (
            <>
              <label className="field"><span>{L("الناسخ", "Copyist")}</span>
                <select value={draft.copyistId ?? ""} onChange={(e) => patch({ copyistId: e.target.value || undefined })}>
                  <option value="">{L("الكل", "All")}</option>
                  {copyists.map((u) => <option key={u.id} value={u.id}>{u.displayName}</option>)}
                </select></label>
              <label className="field"><span>{L("المدقق", "Reviewer")}</span>
                <select value={draft.reviewerId ?? ""} onChange={(e) => patch({ reviewerId: e.target.value || undefined })}>
                  <option value="">{L("الكل", "All")}</option>
                  {reviewers.map((u) => <option key={u.id} value={u.id}>{u.displayName}</option>)}
                </select></label>
            </>
          )}
        </div>
        <div className="btn-row">
          <button className="btn" type="submit" disabled={busy}>{L("تطبيق", "Apply")}</button>
          <button className="btn btn--ghost" type="button" onClick={reset} disabled={busy}>{L("إعادة تعيين", "Reset")}</button>
        </div>
      </form>

      {/* ── Summary cards ── */}
      {summary && (
        <div className="stats">
          <Stat label={L("إجمالي النسخ", "Total copies")} value={summary.totalCopies} />
          <Stat label={L("معتمدة", "Approved")} value={summary.approved} />
          <Stat label={L("قيد المراجعة", "Under review")} value={summary.underReview} />
          <Stat label={L("متوسط مدة الإنجاز (ساعات)", "Avg turnaround (h)")} value={summary.avgTurnaroundHours} />
          <Stat label={L("متوسط زمن القبول (ساعات)", "Avg time-to-accept (h)")} value={summary.avgAcceptanceHours} />
        </div>
      )}

      {/* ── Chart (only after a range is applied) ── */}
      {hasRange && (
        <div className="card">
          <h3 style={{ marginTop: 0 }}>{L("النسخ حسب المحكمة", "Copies per court")}</h3>
          <div className="chart-box"><CourtChart rows={chart} totalLabel={L("الإجمالي", "Total")} /></div>
        </div>
      )}

      {/* ── Report tabs + export ── */}
      <div className="tabs">
        {([
          ["by-court", L("حسب المحكمة", "By court")],
          ["by-room", L("حسب الغرفة", "By room")],
          ["by-copyist", L("حسب الناسخ", "By copyist")],
          ["by-reviewer", L("حسب المدقق", "By reviewer")],
          ["by-head", L("حسب رئيس الديوان", "By registry head")],
          ["by-judge", L("حسب القاضي", "By judge")],
          ["judge-work-log", L("سجل أعمال القضاة", "Judge work log")],
          ["copyist-accuracy", L("دقّة المحرِّر", "Copyist accuracy")],
          ["turnaround", L("مدة الإنجاز", "Turnaround")],
          ["copies", L("تفاصيل النسخ", "Copies")],
        ] as [Tab, string][]).map(([t, lbl]) => (
          <button key={t} className={tab === t ? "active" : undefined} onClick={() => { setPage(1); setTab(t); }}>{lbl}</button>
        ))}
      </div>

      <div className="toolbar">
        <div className="spacer" />
        <button className="btn btn--ghost" disabled={busy || !hasRange} onClick={() => downloadReport(tab, "csv", filter).catch((e) => setErr((e as Error).message))}>
          {L("تصدير CSV", "Export CSV")}
        </button>
        <button className="btn btn--ghost" disabled={busy || !hasRange} onClick={() => downloadReport(tab, "xlsx", filter).catch((e) => setErr((e as Error).message))}>
          {L("تصدير Excel", "Export Excel")}
        </button>
      </div>

      {!hasRange ? (
        <div className="card"><p className="muted" style={{ margin: 0 }}>
          {L("حدّد مدى تاريخيًا (من/إلى) ثم اضغط «تطبيق» لعرض التقرير. لا تُشغَّل التقارير دون فلاتر مناسبة.",
             "Select a date range (from/to) and press Apply to run the report. Reports do not run without appropriate filters.")}
        </p></div>
      ) : busy && !counts && !turnaround && !copies && !workLog && !accuracy ? <Spinner label={L("جارٍ التحميل…", "Loading…")} />
        : tab === "turnaround" ? <TurnaroundTables data={turnaround} />
        : tab === "copies" ? <CopiesTable data={copies} page={page} onPage={setPage} />
        : tab === "judge-work-log" ? <JudgeWorkLogTable data={workLog} page={page} onPage={setPage} />
        : tab === "copyist-accuracy" ? <CopyistAccuracyTable rows={accuracy ?? []} L={L} />
        : <CountTable rows={counts ?? []} dimension={dimensionLabel(tab, L)} />}
    </>
  );
}

function CopyistAccuracyTable({ rows, L }: Readonly<{ rows: CopyistAccuracyRow[]; L: (a: string, e: string) => string }>) {
  const sort = useSort<CopyistAccuracyRow>(rows, {
    name: (r) => r.copyistName,
    rate: (r) => r.avgCorrectionRate,
    corrected: (r) => r.totalWordsCorrected,
    cycles: (r) => r.returnCycles,
  });
  if (rows.length === 0) return <p className="muted">{L("لا توجد بيانات تصحيح ضمن المدى المحدد.", "No correction data in the selected range.")}</p>;
  const pct = (x: number) => `${(x * 100).toFixed(1)}%`;
  return (
    <>
      <p className="muted" style={{ marginTop: 0 }}>
        {L("«نسبة التصحيح» = مجموع نِسَب الكلمات المصحّحة عبر دورات الإرجاع لكل قرار (متوسطًا على قرارات المحرِّر). كلما قلّت، كانت الكتابة أدقّ.",
           "\"Correction rate\" = the cumulative share of words corrected across a decision's returns, averaged over the copyist's decisions. Lower means more accurate writing.")}
      </p>
      <table className="table">
        <thead><tr>
          <SortTh label={L("المحرِّر", "Copyist")} k="name" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <SortTh label={L("نسبة التصحيح", "Correction rate")} k="rate" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <th>{L("قرارات مصحّحة", "Decisions corrected")}</th>
          <SortTh label={L("دورات الإرجاع", "Return cycles")} k="cycles" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <SortTh label={L("كلمات مصحّحة", "Words corrected")} k="corrected" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <th>{L("إجمالي الكلمات", "Total words")}</th>
        </tr></thead>
        <tbody>
          {sort.sorted.map((r) => (
            <tr key={r.copyistId ?? r.copyistName}>
              <td><strong>{r.copyistName}</strong></td>
              <td>{pct(r.avgCorrectionRate)}</td>
              <td>{r.decisionsCorrected}</td>
              <td>{r.returnCycles}</td>
              <td>{r.totalWordsCorrected}</td>
              <td>{r.totalWords}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}

function JudgeWorkLogTable({ data, page, onPage }: Readonly<{ data: Paged<JudgeWorkLogRow> | null; page: number; onPage: (p: number) => void }>) {
  const L = useL();
  const rows = data?.items ?? [];
  const sort = useSort<JudgeWorkLogRow>(rows, {
    judge: (r) => r.judgeName, date: (r) => r.reservationDate,
    court: (r) => r.courtName, room: (r) => r.roomName,
  });
  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  return (
    <>
      <table className="table">
        <thead><tr>
          <SortTh label={L("القاضي", "Judge")} k="judge" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <th>{L("الدور", "Role")}</th>
          <th>{L("رقم النسخة/المتفرق", "Copy / misc no.")}</th>
          <th>{L("رقم القرار", "Decision no.")}</th>
          <SortTh label={L("المحكمة", "Court")} k="court" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <SortTh label={L("الغرفة", "Room")} k="room" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <SortTh label={L("تاريخ الحجز", "Reservation")} k="date" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <th>{L("الحالة", "State")}</th>
          <th>{L("منتدب", "Delegated")}</th>
          <th>{L("رقم الندب", "Delegation no.")}</th>
          <th>{L("تاريخ الندب", "Delegation date")}</th>
        </tr></thead>
        <tbody>
          {rows.length === 0 && <tr><td className="empty" colSpan={11}>{L("لا توجد نتائج ضمن المدى المحدد.", "No results in the selected range.")}</td></tr>}
          {sort.sorted.map((r, i) => (
            <tr key={i}>
              <td><strong>{r.judgeName}</strong></td>
              <td>{r.role}</td>
              <td>{r.copyNumber ?? (r.miscNumber != null ? `${L("متفرق", "misc")} ${r.miscNumber}` : "—")}</td>
              <td>{r.decisionNumber ?? "—"}</td>
              <td>{r.courtName}</td>
              <td>{r.roomName}</td>
              <td>{r.reservationDate}</td>
              <td><StateBadge state={r.state} /></td>
              <td>{r.delegated ? L("نعم", "Yes") : "—"}</td>
              <td>{r.delegationNumber ?? "—"}</td>
              <td>{r.delegationDate ?? "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {data && data.total > data.pageSize && (
        <div className="pager">
          <button className="btn btn--ghost" disabled={page <= 1} onClick={() => onPage(page - 1)}>{L("السابق", "Prev")}</button>
          <span className="muted">{L("صفحة", "Page")} {page} / {totalPages} — {data.total} {L("قرار", "decisions")}</span>
          <button className="btn btn--ghost" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>{L("التالي", "Next")}</button>
        </div>
      )}
    </>
  );
}

function Stat({ label, value }: Readonly<{ label: string; value: number }>) {
  return <div className="stat"><div className="stat__label">{label}</div><div className="stat__value">{value}</div></div>;
}

function dimensionLabel(tab: Tab, L: (a: string, e: string) => string): string {
  switch (tab) {
    case "by-room": return L("الغرفة", "Room");
    case "by-copyist": return L("الناسخ", "Copyist");
    case "by-reviewer": return L("المدقق", "Reviewer");
    case "by-head": return L("رئيس الديوان", "Registry head");
    case "by-judge": return L("القاضي", "Judge");
    default: return L("المحكمة", "Court");
  }
}

function CourtChart({ rows, totalLabel }: Readonly<{ rows: CountRow[]; totalLabel: string }>) {
  const options: ChartOptions<"bar"> = useMemo(() => ({
    responsive: true, maintainAspectRatio: false,
    plugins: { legend: { rtl: true, labels: { font: { family: "inherit" } } } },
    scales: { x: { ticks: { font: { family: "inherit" } } }, y: { beginAtZero: true, ticks: { precision: 0 } } },
  }), []);
  const data = useMemo(() => ({
    labels: rows.map((r) => r.name),
    datasets: [{ label: totalLabel, data: rows.map((r) => r.total), backgroundColor: "#1d7a47" }],
  }), [rows, totalLabel]);
  if (rows.length === 0) return <p className="muted">—</p>;
  return <Bar options={options} data={data} />;
}

function CountTable({ rows, dimension }: Readonly<{ rows: CountRow[]; dimension: string }>) {
  const L = useL();
  const sort = useSort<CountRow>(rows, {
    name: (r) => r.name, total: (r) => r.total,
    inPreparation: (r) => r.inPreparation, underReview: (r) => r.underReview,
    approved: (r) => r.approved, unlocked: (r) => r.unlocked,
  }, { key: "total", dir: "desc" });
  return (
    <table className="table">
      <thead><tr>
        <SortTh label={dimension} k="name" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
        <SortTh label={L("الإجمالي", "Total")} k="total" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
        <SortTh label={L("قيد التحضير", "In prep.")} k="inPreparation" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
        <SortTh label={L("قيد المراجعة", "Review")} k="underReview" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
        <SortTh label={L("معتمد", "Approved")} k="approved" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
        <SortTh label={L("مفتوح", "Unlocked")} k="unlocked" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
      </tr></thead>
      <tbody>
        {rows.length === 0 && <tr><td className="empty" colSpan={6}>{L("لا توجد نتائج", "No results")}</td></tr>}
        {sort.sorted.map((r) => (
          <tr key={r.id ?? r.name} style={{ cursor: "default" }}>
            <td>{r.name}</td><td><strong>{r.total}</strong></td>
            <td>{r.inPreparation}</td><td>{r.underReview}</td><td>{r.approved}</td><td>{r.unlocked}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function TurnaroundTables({ data }: Readonly<{ data: TurnaroundReport | null }>) {
  const L = useL();
  if (!data) return null;
  return (
    <>
      <h3>{L("مدة الإنجاز حسب المحكمة", "Turnaround by court")}</h3>
      <TurnaroundTable rows={data.byCourt} dimension={L("المحكمة", "Court")} />
      <h3 style={{ marginTop: 24 }}>{L("مدة الإنجاز حسب الناسخ", "Turnaround by copyist")}</h3>
      <TurnaroundTable rows={data.byCopyist} dimension={L("الناسخ", "Copyist")} />
    </>
  );
}

function TurnaroundTable({ rows, dimension }: Readonly<{ rows: TurnaroundReport["byCourt"]; dimension: string }>) {
  const L = useL();
  const sort = useSort(rows, {
    name: (r) => r.name, count: (r) => r.count, avg: (r) => r.avgHours, min: (r) => r.minHours, max: (r) => r.maxHours,
  }, { key: "avg", dir: "desc" });
  return (
    <table className="table">
      <thead><tr>
        <SortTh label={dimension} k="name" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
        <SortTh label={L("العدد", "Count")} k="count" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
        <SortTh label={L("المتوسط (ساعات)", "Avg (h)")} k="avg" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
        <SortTh label={L("الأدنى (ساعات)", "Min (h)")} k="min" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
        <SortTh label={L("الأقصى (ساعات)", "Max (h)")} k="max" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
      </tr></thead>
      <tbody>
        {rows.length === 0 && <tr><td className="empty" colSpan={5}>{L("لا توجد نتائج", "No results")}</td></tr>}
        {sort.sorted.map((r) => (
          <tr key={r.id ?? r.name} style={{ cursor: "default" }}>
            <td>{r.name}</td><td>{r.count}</td><td>{r.avgHours}</td><td>{r.minHours}</td><td>{r.maxHours}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function CopiesTable({ data, page, onPage }: Readonly<{ data: Paged<CopyRow> | null; page: number; onPage: (p: number) => void }>) {
  const L = useL();
  const rows = data?.items ?? [];
  const sort = useSort<CopyRow>(rows, {
    copyNumber: (r) => r.copyNumber, court: (r) => r.courtName, room: (r) => r.roomName,
    caseBase: (r) => r.caseBaseNumber, copyist: (r) => r.copyistName, reviewer: (r) => r.reviewerName,
    state: (r) => r.state, created: (r) => r.createdUtc, turnaround: (r) => r.turnaroundHours,
  });
  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  return (
    <>
      <table className="table">
        <thead><tr>
          <SortTh label={L("رقم النسخة", "Copy no.")} k="copyNumber" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <SortTh label={L("المحكمة", "Court")} k="court" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <SortTh label={L("الغرفة", "Room")} k="room" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <SortTh label={L("رقم الأساس", "Case base")} k="caseBase" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <SortTh label={L("الناسخ", "Copyist")} k="copyist" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <SortTh label={L("المدقق", "Reviewer")} k="reviewer" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <SortTh label={L("الحالة", "State")} k="state" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
          <SortTh label={L("مدة الإنجاز (س)", "Turnaround (h)")} k="turnaround" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
        </tr></thead>
        <tbody>
          {rows.length === 0 && <tr><td className="empty" colSpan={8}>{L("لا توجد نتائج", "No results")}</td></tr>}
          {sort.sorted.map((r) => (
            <tr key={r.id} style={{ cursor: "default" }}>
              <td><strong>{r.copyNumber ?? "—"}</strong></td>
              <td>{r.courtName}</td><td>{r.roomName}</td><td>{r.caseBaseNumber}</td>
              <td>{r.copyistName ?? "—"}</td><td>{r.reviewerName ?? "—"}</td>
              <td><StateBadge state={r.state} /></td>
              <td>{r.turnaroundHours ?? "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {data && data.total > data.pageSize && (
        <div className="pager">
          <button className="btn btn--ghost" disabled={page <= 1} onClick={() => onPage(page - 1)}>{L("السابق", "Prev")}</button>
          <span className="muted">{L("صفحة", "Page")} {page} / {totalPages} — {data.total} {L("نسخة", "rows")}</span>
          <button className="btn btn--ghost" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>{L("التالي", "Next")}</button>
        </div>
      )}
    </>
  );
}
