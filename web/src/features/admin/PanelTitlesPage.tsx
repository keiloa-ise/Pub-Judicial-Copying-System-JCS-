import { useEffect, useState, type FormEvent } from "react";
import { api, type PanelMemberTitle } from "../../api/client";
import { useL, ErrorBox, Spinner, Modal, useSort, SortTh } from "../../app/ui";

/** Administrator manages the judging-panel member titles (صفات) — e.g. رئيس الهيئة، نائب الرئيس،
 *  عضو، مستشار. While editing a copy, the copyist picks one of the active titles per panel member;
 *  the chosen title is printed verbatim. Display order controls the order shown in those pickers. */
export function PanelTitlesPage() {
  const L = useL();
  const [titles, setTitles] = useState<PanelMemberTitle[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // create
  const [name, setName] = useState("");
  const [order, setOrder] = useState("");

  // edit
  const [editing, setEditing] = useState<PanelMemberTitle | null>(null);
  const [edName, setEdName] = useState("");
  const [edOrder, setEdOrder] = useState("");

  const load = () => api.admin.listPanelTitles().then(setTitles).catch((e) => setErr(e.message));
  useEffect(() => { load(); }, []);

  const sort = useSort<PanelMemberTitle>(titles ?? [], {
    order: (t) => t.displayOrder,
    name: (t) => t.name,
    status: (t) => t.isActive,
  });

  async function run(fn: () => Promise<unknown>) {
    setErr(null); setBusy(true);
    try { await fn(); await load(); }
    catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  function create(e: FormEvent) {
    e.preventDefault();
    const nextOrder = order.trim() ? Number(order) : (titles?.length ?? 0) + 1;
    run(async () => { await api.admin.createPanelTitle(name, nextOrder); setName(""); setOrder(""); });
  }

  function startEdit(t: PanelMemberTitle) {
    setErr(null); setEditing(t); setEdName(t.name); setEdOrder(String(t.displayOrder));
  }

  function saveEdit(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    const t = editing;
    run(async () => { await api.admin.updatePanelTitle(t.id, edName, t.isActive, Number(edOrder) || 0); setEditing(null); });
  }

  return (
    <>
      <h1 className="page-title">{L("صفات أعضاء الهيئة", "Panel member titles")}</h1>
      {err && <ErrorBox message={err} />}

      <Modal open={!!editing} onClose={() => setEditing(null)} title={L("تعديل الصفة", "Edit title")}>
        <form className="card" onSubmit={saveEdit}>
          <label className="field"><span>{L("الصفة", "Title")}</span>
            <input value={edName} onChange={(e) => setEdName(e.target.value)} required /></label>
          <label className="field" style={{ maxWidth: 160 }}><span>{L("الترتيب", "Order")}</span>
            <input type="number" value={edOrder} onChange={(e) => setEdOrder(e.target.value)} /></label>
          <div className="btn-row">
            <button className="btn" disabled={busy}>{L("حفظ", "Save")}</button>
            <button className="btn btn--ghost" type="button" onClick={() => setEditing(null)}>{L("إلغاء", "Cancel")}</button>
          </div>
        </form>
      </Modal>

      <form className="card" onSubmit={create}>
        <h3>{L("إضافة صفة", "Add title")}</h3>
        <div className="row">
          <label className="field"><span>{L("الصفة", "Title")}</span>
            <input value={name} onChange={(e) => setName(e.target.value)} required
              placeholder={L("مثال: رئيس الهيئة", "e.g. President")} /></label>
          <label className="field" style={{ maxWidth: 160 }}><span>{L("الترتيب", "Order")}</span>
            <input type="number" value={order} onChange={(e) => setOrder(e.target.value)} /></label>
        </div>
        <button className="btn" disabled={busy}>{L("إضافة", "Add")}</button>
      </form>

      {!titles ? <Spinner /> : (
        <table className="table">
          <thead><tr>
            <SortTh label={L("الترتيب", "Order")} k="order" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الصفة", "Title")} k="name" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الحالة", "Status")} k="status" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <th></th>
          </tr></thead>
          <tbody>
            {sort.sorted.map((t) => (
              <tr key={t.id} style={{ cursor: "default" }}>
                <td>{t.displayOrder}</td>
                <td>{t.name}</td>
                <td>{t.isActive ? <span className="badge s-approved">{L("نشط", "Active")}</span> : <span className="badge s-created">{L("معطّل", "Inactive")}</span>}</td>
                <td>
                  <div className="btn-row" style={{ margin: 0 }}>
                    <button className="btn btn--ghost" onClick={() => startEdit(t)}>{L("تعديل", "Edit")}</button>
                    <button className="btn btn--ghost" onClick={() => run(() => api.admin.updatePanelTitle(t.id, t.name, !t.isActive, t.displayOrder))}>
                      {t.isActive ? L("تعطيل", "Deactivate") : L("تفعيل", "Activate")}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
