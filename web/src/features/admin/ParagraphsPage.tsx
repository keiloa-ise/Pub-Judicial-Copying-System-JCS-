import { useEffect, useState, type FormEvent } from "react";
import { api, type ParagraphTemplate, type FormTemplate } from "../../api/client";
import { useL, ErrorBox, Spinner, Modal, useSort, SortTh } from "../../app/ui";

/** FR-09: Administrator manages paragraph templates and scopes each to a form type, so they
 *  appear for insertion when preparing a copy of that type. Archived ones can't be inserted. */
export function ParagraphsPage() {
  const L = useL();
  const [items, setItems] = useState<ParagraphTemplate[] | null>(null);
  const [forms, setForms] = useState<FormTemplate[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // create
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [formTemplateId, setFormTemplateId] = useState("");

  // edit
  const [editing, setEditing] = useState<ParagraphTemplate | null>(null);
  const [edTitle, setEdTitle] = useState("");
  const [edBody, setEdBody] = useState("");
  const [edForm, setEdForm] = useState("");

  const load = () => Promise.all([api.admin.listParagraphs(), api.admin.listForms()])
    .then(([p, f]) => { setItems(p); setForms(f); }).catch((e) => setErr(e.message));
  useEffect(() => { load(); }, []);

  const formName = (fid: string | null) => fid ? (forms.find((f) => f.id === fid)?.name ?? "—") : L("عام (كل النماذج)", "Global (all forms)");
  const sort = useSort<ParagraphTemplate>(items ?? [], {
    title: (p) => p.title,
    form: (p) => formName(p.formTemplateId),
    status: (p) => p.isArchived,
  });

  async function run(fn: () => Promise<unknown>) {
    setErr(null); setBusy(true);
    try { await fn(); await load(); }
    catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  function create(e: FormEvent) {
    e.preventDefault();
    run(async () => {
      await api.admin.createParagraph(title, body, formTemplateId || null);
      setTitle(""); setBody(""); setFormTemplateId("");
    });
  }

  function startEdit(p: ParagraphTemplate) {
    setErr(null); setEditing(p); setEdTitle(p.title); setEdBody(p.body); setEdForm(p.formTemplateId ?? "");
  }
  function saveEdit(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    const p = editing;
    run(async () => { await api.admin.updateParagraph(p.id, edTitle, edBody, p.isArchived, edForm || null); setEditing(null); });
  }

  return (
    <>
      <h1 className="page-title">{L("فقرات النصوص", "Paragraph templates")}</h1>
      {err && <ErrorBox message={err} />}

      <Modal open={!!editing} onClose={() => setEditing(null)} title={L("تعديل الفقرة", "Edit paragraph")}>
        <form className="card" onSubmit={saveEdit}>
          <div className="row">
            <label className="field"><span>{L("العنوان", "Title")}</span>
              <input value={edTitle} onChange={(e) => setEdTitle(e.target.value)} required /></label>
            <label className="field"><span>{L("نوع القرار", "Decision type")}</span>
              <select value={edForm} onChange={(e) => setEdForm(e.target.value)}>
                <option value="">{L("عام (كل النماذج)", "Global (all forms)")}</option>
                {forms.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
              </select></label>
          </div>
          <label className="field"><span>{L("النص الافتراضي", "Default text")}</span>
            <textarea value={edBody} onChange={(e) => setEdBody(e.target.value)} /></label>
          <div className="btn-row">
            <button className="btn" disabled={busy}>{L("حفظ", "Save")}</button>
            <button className="btn btn--ghost" type="button" onClick={() => setEditing(null)}>{L("إلغاء", "Cancel")}</button>
          </div>
        </form>
      </Modal>

      <form className="card" onSubmit={create}>
        <h3>{L("إضافة فقرة", "Add paragraph")}</h3>
        <div className="row">
          <label className="field"><span>{L("اسم الفقرة", "Paragraph name")}</span>
            <input value={title} onChange={(e) => setTitle(e.target.value)} required /></label>
          <label className="field"><span>{L("نوع القرار", "Decision type")}</span>
            <select value={formTemplateId} onChange={(e) => setFormTemplateId(e.target.value)}>
              <option value="">{L("عام (كل النماذج)", "Global (all forms)")}</option>
              {forms.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
            </select></label>
        </div>
        <label className="field"><span>{L("النص الافتراضي", "Default text")}</span>
          <textarea value={body} onChange={(e) => setBody(e.target.value)} /></label>
        <button className="btn" disabled={busy}>{L("إضافة فقرة", "Add paragraph")}</button>
      </form>

      {!items ? <Spinner /> : (
        <table className="table">
          <thead><tr>
            <SortTh label={L("العنوان", "Title")} k="title" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("نوع النموذج", "Form type")} k="form" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الحالة", "Status")} k="status" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <th></th>
          </tr></thead>
          <tbody>
            {sort.sorted.map((p) => (
              <tr key={p.id} style={{ cursor: "default" }}>
                <td>{p.title}</td>
                <td className="muted">{formName(p.formTemplateId)}</td>
                <td>{p.isArchived ? <span className="badge s-created">{L("مؤرشف", "Archived")}</span> : <span className="badge s-approved">{L("متاح", "Available")}</span>}</td>
                <td>
                  <div className="btn-row" style={{ margin: 0 }}>
                    <button className="btn btn--ghost" onClick={() => startEdit(p)}>{L("تعديل", "Edit")}</button>
                    <button className="btn btn--ghost" onClick={() => run(() => api.admin.updateParagraph(p.id, p.title, p.body, !p.isArchived, p.formTemplateId))}>
                      {p.isArchived ? L("استعادة", "Restore") : L("أرشفة", "Archive")}
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
