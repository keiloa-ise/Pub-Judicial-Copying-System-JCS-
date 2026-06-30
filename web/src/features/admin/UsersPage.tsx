import { useEffect, useState, type FormEvent } from "react";
import { api, type UserDto, type Court, type Role } from "../../api/client";
import { useL, ErrorBox, Spinner, roleLabels, Modal, useSort, SortTh } from "../../app/ui";
import { useI18n } from "../../i18n";

const roles: Role[] = ["Administrator", "RegistryHead", "Copyist", "Reviewer"];

/** FR-02/FR-05: Administrator fully manages users — create, edit (role/name/courts),
 *  reset password, and enable/disable. */
export function UsersPage() {
  const L = useL();
  const { lang } = useI18n();
  const arKey = lang === "ar" ? "ar" : "en";

  const [users, setUsers] = useState<UserDto[] | null>(null);
  const [courts, setCourts] = useState<Court[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // create form
  const [username, setUsername] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [role, setRole] = useState<Role>("Copyist");
  const [password, setPassword] = useState("");
  const [courtIds, setCourtIds] = useState<string[]>([]);

  // inline edit
  const [editing, setEditing] = useState<UserDto | null>(null);
  const [edName, setEdName] = useState("");
  const [edRole, setEdRole] = useState<Role>("Copyist");
  const [edCourts, setEdCourts] = useState<string[]>([]);

  const load = () => Promise.all([api.admin.listUsers(), api.admin.listCourts()])
    .then(([u, c]) => { setUsers(u); setCourts(c); }).catch((e) => setErr(e.message));
  useEffect(() => { load(); }, []);

  const courtName = (id: string) => courts.find((c) => c.id === id)?.name ?? id;
  const sort = useSort<UserDto>(users ?? [], {
    username: (u) => u.username,
    name: (u) => u.displayName,
    role: (u) => roleLabels[u.role][arKey],
    courts: (u) => u.courtIds.map(courtName).sort().join("، "),
    status: (u) => u.isActive,
  });

  function toggleIn(list: string[], id: string, on: boolean) {
    return on ? [...list, id] : list.filter((x) => x !== id);
  }

  async function run(label: string, fn: () => Promise<unknown>) {
    setErr(null); setOk(null); setBusy(true);
    try { await fn(); await load(); setOk(label); }
    catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  function create(e: FormEvent) {
    e.preventDefault();
    run(L("تمت إضافة المستخدم.", "User created."), async () => {
      await api.admin.createUser({ username, displayName, role, password, courtIds });
      setUsername(""); setDisplayName(""); setPassword(""); setCourtIds([]);
    });
  }

  function startEdit(u: UserDto) {
    setOk(null); setErr(null);
    setEditing(u); setEdName(u.displayName); setEdRole(u.role); setEdCourts(u.courtIds);
  }

  function saveEdit(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    const id = editing.id;
    run(L("تم حفظ التعديلات.", "Changes saved."), async () => {
      await api.admin.updateUser(id, edName, edRole);
      await api.admin.setUserCourts(id, edCourts);
      setEditing(null);
    });
  }

  function resetPwd(u: UserDto) {
    const pwd = window.prompt(L(`كلمة مرور جديدة لـ ${u.username}:`, `New password for ${u.username}:`)) ?? "";
    if (pwd.trim()) run(L("تم تعيين كلمة المرور.", "Password reset."), () => api.admin.resetPassword(u.id, pwd));
  }

  return (
    <>
      <h1 className="page-title">{L("المستخدمون", "Users")}</h1>
      {err && <ErrorBox message={err} />}
      {ok && <div className="okbox">{ok}</div>}

      {/* Edit panel */}
      <Modal open={!!editing} onClose={() => setEditing(null)}
        title={`${L("تعديل المستخدم", "Edit user")}: ${editing?.username ?? ""}`}>
        <form className="card" onSubmit={saveEdit}>
          <div className="row">
            <label className="field"><span>{L("الاسم المعروض", "Display name")}</span>
              <input value={edName} onChange={(e) => setEdName(e.target.value)} required /></label>
            <label className="field"><span>{L("الدور", "Role")}</span>
              <select value={edRole} onChange={(e) => setEdRole(e.target.value as Role)}>
                {roles.map((r) => <option key={r} value={r}>{roleLabels[r][arKey]}</option>)}
              </select></label>
          </div>
          <label className="field">
            <span>{L("المحاكم المخصّصة (BR-06)", "Assigned courts (BR-06)")}</span>
            <div className="chips">
              {courts.map((c) => (
                <label key={c.id} className="chip">
                  <input type="checkbox" checked={edCourts.includes(c.id)}
                    onChange={(e) => setEdCourts((ids) => toggleIn(ids, c.id, e.target.checked))} />
                  {c.name}
                </label>
              ))}
            </div>
          </label>
          <div className="btn-row">
            <button className="btn" disabled={busy}>{L("حفظ", "Save")}</button>
            <button className="btn btn--ghost" type="button" onClick={() => setEditing(null)}>{L("إلغاء", "Cancel")}</button>
          </div>
        </form>
      </Modal>

      {/* Create form */}
      <form className="card" onSubmit={create}>
        <h3>{L("إضافة مستخدم", "Add user")}</h3>
        <div className="row">
          <label className="field"><span>{L("اسم المستخدم", "Username")}</span>
            <input value={username} onChange={(e) => setUsername(e.target.value)} required /></label>
          <label className="field"><span>{L("الاسم المعروض", "Display name")}</span>
            <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} required /></label>
        </div>
        <div className="row">
          <label className="field"><span>{L("الدور", "Role")}</span>
            <select value={role} onChange={(e) => setRole(e.target.value as Role)}>
              {roles.map((r) => <option key={r} value={r}>{roleLabels[r][arKey]}</option>)}
            </select></label>
          <label className="field"><span>{L("كلمة المرور", "Password")}</span>
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required /></label>
        </div>
        <label className="field">
          <span>{L("المحاكم المخصّصة (BR-06)", "Assigned courts (BR-06)")}</span>
          <div className="chips">
            {courts.map((c) => (
              <label key={c.id} className="chip">
                <input type="checkbox" checked={courtIds.includes(c.id)}
                  onChange={(e) => setCourtIds((ids) => toggleIn(ids, c.id, e.target.checked))} />
                {c.name}
              </label>
            ))}
          </div>
        </label>
        <button className="btn" disabled={busy}>{L("إضافة مستخدم", "Add user")}</button>
      </form>

      {!users ? <Spinner /> : (
        <table className="table">
          <thead><tr>
            <SortTh label={L("المستخدم", "Username")} k="username" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الاسم", "Name")} k="name" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الدور", "Role")} k="role" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("المحاكم", "Courts")} k="courts" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الحالة", "Status")} k="status" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <th></th>
          </tr></thead>
          <tbody>
            {sort.sorted.map((u) => (
              <tr key={u.id} style={{ cursor: "default" }}>
                <td>{u.username}</td>
                <td>{u.displayName}</td>
                <td>{roleLabels[u.role][arKey]}</td>
                <td>{u.courtIds.map(courtName).join("، ") || "—"}</td>
                <td>{u.isActive ? <span className="badge s-approved">{L("نشط", "Active")}</span> : <span className="badge s-unlocked">{L("معطّل", "Disabled")}</span>}</td>
                <td>
                  <div className="btn-row" style={{ margin: 0 }}>
                    <button className="btn btn--ghost" onClick={() => startEdit(u)}>{L("تعديل", "Edit")}</button>
                    <button className="btn btn--ghost" onClick={() => resetPwd(u)}>{L("كلمة المرور", "Password")}</button>
                    <button className="btn btn--ghost" onClick={() => run(
                      u.isActive ? L("تم التعطيل.", "Disabled.") : L("تم التفعيل.", "Enabled."),
                      () => api.admin.setUserActive(u.id, !u.isActive))}>
                      {u.isActive ? L("تعطيل", "Disable") : L("تفعيل", "Enable")}
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
