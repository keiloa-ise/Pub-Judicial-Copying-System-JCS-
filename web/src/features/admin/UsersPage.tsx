import { useEffect, useState, type FormEvent } from "react";
import { api, type UserDto, type Court, type Room, type Role } from "../../api/client";
import { useL, ErrorBox, Spinner, roleLabels, Modal, useSort, SortTh } from "../../app/ui";
import { useI18n } from "../../i18n";

const roles: Role[] = ["Administrator", "RegistryHead", "Copyist", "Reviewer"];

/** Copyists and Reviewers are scoped to ROOMS (BR-06); Registry Heads to COURTS; Administrators unrestricted. */
const isRoomScoped = (r: Role) => r === "Copyist" || r === "Reviewer";
const isCourtScoped = (r: Role) => r === "RegistryHead";

/** FR-02/FR-05: Administrator fully manages users — create, edit (role/name/scope),
 *  reset password, and enable/disable. Scope is rooms for copyist/reviewer, courts for head. */
export function UsersPage() {
  const L = useL();
  const { lang } = useI18n();
  const arKey = lang === "ar" ? "ar" : "en";

  const [users, setUsers] = useState<UserDto[] | null>(null);
  const [courts, setCourts] = useState<Court[]>([]);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // create form
  const [username, setUsername] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [role, setRole] = useState<Role>("Copyist");
  const [password, setPassword] = useState("");
  const [courtIds, setCourtIds] = useState<string[]>([]);
  const [roomIds, setRoomIds] = useState<string[]>([]);

  // inline edit
  const [editing, setEditing] = useState<UserDto | null>(null);
  const [edName, setEdName] = useState("");
  const [edRole, setEdRole] = useState<Role>("Copyist");
  const [edCourts, setEdCourts] = useState<string[]>([]);
  const [edRooms, setEdRooms] = useState<string[]>([]);

  const load = () => Promise.all([api.admin.listUsers(), api.admin.listCourts(), api.admin.listRooms()])
    .then(([u, c, r]) => { setUsers(u); setCourts(c); setRooms(r); }).catch((e) => setErr(e.message));
  useEffect(() => { load(); }, []);

  const courtName = (id: string) => courts.find((c) => c.id === id)?.name ?? id;
  const roomName = (id: string) => rooms.find((r) => r.id === id)?.name ?? id;
  const scopeText = (u: UserDto) =>
    isRoomScoped(u.role) ? (u.roomIds.map(roomName).join("، ") || "—")
    : isCourtScoped(u.role) ? (u.courtIds.map(courtName).join("، ") || "—")
    : L("الكل", "All");
  const sort = useSort<UserDto>(users ?? [], {
    username: (u) => u.username,
    name: (u) => u.displayName,
    role: (u) => roleLabels[u.role][arKey],
    scope: (u) => scopeText(u),
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
      // Copyist/Reviewer carry no court rows; their scope is the room set (assigned right after create).
      const { id } = await api.admin.createUser({
        username, displayName, role, password, courtIds: isCourtScoped(role) ? courtIds : [],
      });
      if (isRoomScoped(role) && roomIds.length) await api.admin.setUserRooms(id, roomIds);
      setUsername(""); setDisplayName(""); setPassword(""); setCourtIds([]); setRoomIds([]);
    });
  }

  function startEdit(u: UserDto) {
    setOk(null); setErr(null);
    setEditing(u); setEdName(u.displayName); setEdRole(u.role);
    setEdCourts(u.courtIds); setEdRooms(u.roomIds);
  }

  function saveEdit(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    const id = editing.id;
    run(L("تم حفظ التعديلات.", "Changes saved."), async () => {
      await api.admin.updateUser(id, edName, edRole);
      if (isRoomScoped(edRole)) await api.admin.setUserRooms(id, edRooms);
      else if (isCourtScoped(edRole)) await api.admin.setUserCourts(id, edCourts);
      setEditing(null);
    });
  }

  function resetPwd(u: UserDto) {
    const pwd = window.prompt(L(`كلمة مرور جديدة لـ ${u.username}:`, `New password for ${u.username}:`)) ?? "";
    if (pwd.trim()) run(L("تم تعيين كلمة المرور.", "Password reset."), () => api.admin.resetPassword(u.id, pwd));
  }

  /** Court checkboxes (Registry Head scope). */
  const CourtPicker = ({ selected, onToggle }: { selected: string[]; onToggle: (id: string, on: boolean) => void }) => (
    <div className="chips">
      {courts.map((c) => (
        <label key={c.id} className="chip">
          <input type="checkbox" checked={selected.includes(c.id)} onChange={(e) => onToggle(c.id, e.target.checked)} />
          {c.name}
        </label>
      ))}
    </div>
  );

  /** Room checkboxes grouped by court (Copyist/Reviewer scope — may span courts). */
  const RoomPicker = ({ selected, onToggle }: { selected: string[]; onToggle: (id: string, on: boolean) => void }) => (
    <div className="scope-rooms">
      {courts.map((c) => {
        const courtRooms = rooms.filter((r) => r.courtId === c.id);
        if (courtRooms.length === 0) return null;
        return (
          <div key={c.id} style={{ marginBottom: 8 }}>
            <div className="muted" style={{ fontWeight: 600 }}>{c.name}</div>
            <div className="chips">
              {courtRooms.map((r) => (
                <label key={r.id} className="chip">
                  <input type="checkbox" checked={selected.includes(r.id)} onChange={(e) => onToggle(r.id, e.target.checked)} />
                  {r.name}
                </label>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );

  const scopeLabel = (r: Role) =>
    isRoomScoped(r) ? L("الغرف المخصّصة (BR-06)", "Assigned rooms (BR-06)")
    : isCourtScoped(r) ? L("المحاكم المخصّصة (BR-06)", "Assigned courts (BR-06)")
    : L("النطاق", "Scope");

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
          {isRoomScoped(edRole) ? (
            <label className="field"><span>{scopeLabel(edRole)}</span>
              <RoomPicker selected={edRooms} onToggle={(id, on) => setEdRooms((ids) => toggleIn(ids, id, on))} /></label>
          ) : isCourtScoped(edRole) ? (
            <label className="field"><span>{scopeLabel(edRole)}</span>
              <CourtPicker selected={edCourts} onToggle={(id, on) => setEdCourts((ids) => toggleIn(ids, id, on))} /></label>
          ) : (
            <p className="muted">{L("المدير يصل إلى كل المحاكم والغرف.", "Administrators access all courts and rooms.")}</p>
          )}
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
        {isRoomScoped(role) ? (
          <label className="field"><span>{scopeLabel(role)}</span>
            <RoomPicker selected={roomIds} onToggle={(id, on) => setRoomIds((ids) => toggleIn(ids, id, on))} /></label>
        ) : isCourtScoped(role) ? (
          <label className="field"><span>{scopeLabel(role)}</span>
            <CourtPicker selected={courtIds} onToggle={(id, on) => setCourtIds((ids) => toggleIn(ids, id, on))} /></label>
        ) : (
          <p className="muted">{L("المدير يصل إلى كل المحاكم والغرف.", "Administrators access all courts and rooms.")}</p>
        )}
        <button className="btn" disabled={busy}>{L("إضافة مستخدم", "Add user")}</button>
      </form>

      {!users ? <Spinner /> : (
        <table className="table">
          <thead><tr>
            <SortTh label={L("المستخدم", "Username")} k="username" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الاسم", "Name")} k="name" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الدور", "Role")} k="role" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("النطاق (محاكم/غرف)", "Scope (courts/rooms)")} k="scope" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الحالة", "Status")} k="status" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <th></th>
          </tr></thead>
          <tbody>
            {sort.sorted.map((u) => (
              <tr key={u.id} style={{ cursor: "default" }}>
                <td>{u.username}</td>
                <td>{u.displayName}</td>
                <td>{roleLabels[u.role][arKey]}</td>
                <td>{scopeText(u)}</td>
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
