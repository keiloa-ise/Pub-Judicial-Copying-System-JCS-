import { useEffect, useState, type FormEvent } from "react";
import { api, type FormTemplate } from "../../api/client";
import { useL, ErrorBox, Spinner, useSort, SortTh } from "../../app/ui";

interface DraftField { key: string; label: string; type: string; }
const fieldTypes = ["text", "number", "date", "textarea", "judge", "judges"];
const emptyField = (): DraftField => ({ key: "", label: "", type: "text" });

/** FR-08: Administrator defines AND edits form templates (decision types). Copyist forms render
 *  from these. Editing replaces the field set; the name and active status are editable too. */
export function FormsPage() {
  const L = useL();
  const [items, setItems] = useState<FormTemplate[] | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null); // null = create mode
  const [name, setName] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [fields, setFields] = useState<DraftField[]>([emptyField()]);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = () => api.admin.listForms().then(setItems).catch((e) => setErr(e.message));
  useEffect(() => { load(); }, []);

  const sort = useSort<FormTemplate>(items ?? [], {
    name: (f) => f.name,
    fields: (f) => f.fields.length,
    status: (f) => f.isActive,
  });

  function reset() {
    setEditingId(null); setName(""); setIsActive(true); setFields([emptyField()]);
  }

  function startEdit(t: FormTemplate) {
    setErr(null);
    setEditingId(t.id);
    setName(t.name);
    setIsActive(t.isActive);
    setFields(t.fields.length ? t.fields.map((f) => ({ key: f.key, label: f.label, type: f.type })) : [emptyField()]);
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function setField(i: number, patch: Partial<DraftField>) {
    setFields((fs) => fs.map((f, idx) => (idx === i ? { ...f, ...patch } : f)));
  }
  const removeField = (i: number) => setFields((fs) => fs.filter((_, idx) => idx !== i));

  async function run(fn: () => Promise<unknown>) {
    setErr(null); setBusy(true);
    try { await fn(); await load(); }
    catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  function submit(e: FormEvent) {
    e.preventDefault();
    const payload = fields
      .filter((f) => f.key.trim() && f.label.trim())
      .map((f, i) => ({ key: f.key.trim(), label: f.label.trim(), type: f.type, validationRulesJson: null, order: i }));
    run(async () => {
      if (editingId) await api.admin.updateForm(editingId, name, isActive, payload);
      else await api.admin.createForm(name, payload);
      reset();
    });
  }

  return (
    <>
      <h1 className="page-title">{L("النماذج (أنواع القرارات)", "Templates (decision types)")}</h1>
      {err && <ErrorBox message={err} />}

      <form className="card" onSubmit={submit} style={editingId ? { borderInlineStart: "5px solid var(--gold)" } : undefined}>
        <h3>{editingId ? L("تعديل النموذج", "Edit template") : L("إضافة نموذج", "Add template")}</h3>
        <div className="row">
          <label className="field"><span>{L("اسم النموذج / نوع القرار", "Template name / decision type")}</span>
            <input value={name} onChange={(e) => setName(e.target.value)} required /></label>
          {editingId && (
            <label className="field" style={{ maxWidth: 200 }}><span>{L("الحالة", "Status")}</span>
              <select value={isActive ? "1" : "0"} onChange={(e) => setIsActive(e.target.value === "1")}>
                <option value="1">{L("نشط", "Active")}</option>
                <option value="0">{L("معطّل", "Inactive")}</option>
              </select></label>
          )}
        </div>

        <h3>{L("الحقول الثابتة", "Fixed fields")}</h3>
        {fields.map((f, i) => (
          <div className="row" key={i}>
            <label className="field"><span>{L("المفتاح", "Key")}</span>
              <input value={f.key} onChange={(e) => setField(i, { key: e.target.value })} /></label>
            <label className="field"><span>{L("التسمية", "Label")}</span>
              <input value={f.label} onChange={(e) => setField(i, { label: e.target.value })} /></label>
            <label className="field" style={{ maxWidth: 150 }}><span>{L("النوع", "Type")}</span>
              <select value={f.type} onChange={(e) => setField(i, { type: e.target.value })}>
                {fieldTypes.map((t) => <option key={t} value={t}>{t}</option>)}
              </select></label>
            <button type="button" className="iconbtn iconbtn--danger" style={{ alignSelf: "end", marginBottom: 16 }}
              onClick={() => removeField(i)} title={L("حذف الحقل", "Remove field")}>✕</button>
          </div>
        ))}
        <div className="btn-row">
          <button type="button" className="btn btn--ghost" onClick={() => setFields((fs) => [...fs, emptyField()])}>{L("إضافة حقل", "Add field")}</button>
          <button className="btn" disabled={busy}>{editingId ? L("حفظ التعديلات", "Save changes") : L("حفظ النموذج", "Save template")}</button>
          {editingId && <button type="button" className="btn btn--ghost" onClick={reset}>{L("إلغاء", "Cancel")}</button>}
        </div>
      </form>

      {!items ? <Spinner /> : (
        <table className="table">
          <thead><tr>
            <SortTh label={L("الاسم", "Name")} k="name" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("عدد الحقول", "Fields")} k="fields" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الحالة", "Status")} k="status" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <th></th>
          </tr></thead>
          <tbody>
            {sort.sorted.map((f) => (
              <tr key={f.id} style={{ cursor: "default" }}>
                <td>{f.name}</td>
                <td>{f.fields.length}</td>
                <td>{f.isActive ? <span className="badge s-approved">{L("نشط", "Active")}</span> : <span className="badge s-created">{L("معطّل", "Inactive")}</span>}</td>
                <td>
                  <div className="btn-row" style={{ margin: 0 }}>
                    <button className="btn btn--ghost" onClick={() => startEdit(f)}>{L("تعديل", "Edit")}</button>
                    <button className="btn btn--ghost" onClick={() => run(() =>
                      api.admin.updateForm(f.id, f.name, !f.isActive,
                        f.fields.map((x, i) => ({ key: x.key, label: x.label, type: x.type, validationRulesJson: x.validationRulesJson, order: i }))))}>
                      {f.isActive ? L("تعطيل", "Deactivate") : L("تفعيل", "Activate")}
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
