import { useEffect, useState, type FormEvent } from "react";
import { api, type Judge, type Court, type Room } from "../../api/client";
import { useL, ErrorBox, Spinner, Modal, useSort, SortTh } from "../../app/ui";

/** FR-04: Administrator manages judges. A judge must be assigned to one or more rooms (غرف),
 *  and judges are editable (name, status, room assignments). The room determines the court. */
export function JudgesPage() {
  const L = useL();
  const [judges, setJudges] = useState<Judge[] | null>(null);
  const [courts, setCourts] = useState<Court[]>([]);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // create
  const [name, setName] = useState("");
  const [roomIds, setRoomIds] = useState<string[]>([]);

  // edit
  const [editing, setEditing] = useState<Judge | null>(null);
  const [edName, setEdName] = useState("");
  const [edRooms, setEdRooms] = useState<string[]>([]);

  const load = () => Promise.all([api.admin.listJudges(), api.admin.listCourts(), api.admin.listRooms()])
    .then(([j, c, r]) => { setJudges(j); setCourts(c); setRooms(r); }).catch((e) => setErr(e.message));
  useEffect(() => { load(); }, []);

  const roomLabelFor = (id: string) => {
    const r = rooms.find((x) => x.id === id);
    if (!r) return id;
    const c = courts.find((x) => x.id === r.courtId);
    return `${c?.name ?? ""} / ${r.name}`;
  };
  const sort = useSort<Judge>(judges ?? [], {
    name: (j) => j.name,
    rooms: (j) => j.roomIds.map(roomLabelFor).sort().join("، "),
    status: (j) => j.isActive,
  });

  const toggle = (list: string[], id: string, on: boolean) => on ? [...list, id] : list.filter((x) => x !== id);

  async function run(fn: () => Promise<unknown>) {
    setErr(null); setBusy(true);
    try { await fn(); await load(); }
    catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  function create(e: FormEvent) {
    e.preventDefault();
    run(async () => { await api.admin.createJudge(name, roomIds); setName(""); setRoomIds([]); });
  }

  function startEdit(j: Judge) { setErr(null); setEditing(j); setEdName(j.name); setEdRooms(j.roomIds); }

  function saveEdit(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    const j = editing;
    run(async () => { await api.admin.updateJudge(j.id, edName, j.isActive, edRooms); setEditing(null); });
  }

  // Room picker: rooms grouped by their court.
  const RoomPicker = ({ selected, onToggle }: { selected: string[]; onToggle: (id: string, on: boolean) => void }) => (
    <div className="chips" style={{ flexDirection: "column", alignItems: "stretch", gap: 10 }}>
      {courts.length === 0 && <span className="muted">{L("أضف محكمة وغرفة أولاً", "Add a court and a room first")}</span>}
      {courts.map((c) => {
        const courtRooms = rooms.filter((r) => r.courtId === c.id);
        if (courtRooms.length === 0) return null;
        return (
          <div key={c.id}>
            <div className="muted" style={{ marginBottom: 4 }}>{c.name}</div>
            <div className="chips">
              {courtRooms.map((r) => (
                <label key={r.id} className="chip">
                  <input type="checkbox" checked={selected.includes(r.id)}
                    onChange={(e) => onToggle(r.id, e.target.checked)} />
                  {r.name} ({r.code})
                </label>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );

  return (
    <>
      <h1 className="page-title">{L("القضاة", "Judges")}</h1>
      {err && <ErrorBox message={err} />}

      <Modal open={!!editing} onClose={() => setEditing(null)} title={L("تعديل القاضي", "Edit judge")}>
        <form className="card" onSubmit={saveEdit}>
          <label className="field"><span>{L("اسم القاضي", "Judge name")}</span>
            <input value={edName} onChange={(e) => setEdName(e.target.value)} required /></label>
          <label className="field">
            <span>{L("الغرف (واحدة على الأقل)", "Rooms (at least one)")}</span>
            <RoomPicker selected={edRooms} onToggle={(id, on) => setEdRooms((ids) => toggle(ids, id, on))} />
          </label>
          <div className="btn-row">
            <button className="btn" disabled={busy}>{L("حفظ", "Save")}</button>
            <button className="btn btn--ghost" type="button" onClick={() => setEditing(null)}>{L("إلغاء", "Cancel")}</button>
          </div>
        </form>
      </Modal>

      <form className="card" onSubmit={create}>
        <h3>{L("إضافة قاضٍ", "Add judge")}</h3>
        <label className="field"><span>{L("اسم القاضي", "Judge name")}</span>
          <input value={name} onChange={(e) => setName(e.target.value)} required /></label>
        <label className="field">
          <span>{L("الغرف (واحدة على الأقل)", "Rooms (at least one)")}</span>
          <RoomPicker selected={roomIds} onToggle={(id, on) => setRoomIds((ids) => toggle(ids, id, on))} />
        </label>
        <button className="btn" disabled={busy || roomIds.length === 0}>{L("إضافة قاضٍ", "Add judge")}</button>
      </form>

      {!judges ? <Spinner /> : (
        <table className="table">
          <thead><tr>
            <SortTh label={L("الاسم", "Name")} k="name" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الغرف", "Rooms")} k="rooms" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <SortTh label={L("الحالة", "Status")} k="status" sortKey={sort.sortKey} sortDir={sort.sortDir} onSort={sort.onSort} />
            <th></th>
          </tr></thead>
          <tbody>
            {sort.sorted.map((j) => (
              <tr key={j.id} style={{ cursor: "default" }}>
                <td>{j.name}</td>
                <td>{j.roomIds.map(roomLabelFor).join("، ") || "—"}</td>
                <td>{j.isActive ? <span className="badge s-approved">{L("نشط", "Active")}</span> : <span className="badge s-created">{L("معطّل", "Inactive")}</span>}</td>
                <td>
                  <div className="btn-row" style={{ margin: 0 }}>
                    <button className="btn btn--ghost" onClick={() => startEdit(j)}>{L("تعديل", "Edit")}</button>
                    <button className="btn btn--ghost" onClick={() => run(() => api.admin.updateJudge(j.id, j.name, !j.isActive, j.roomIds))}>
                      {j.isActive ? L("تعطيل", "Deactivate") : L("تفعيل", "Activate")}
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
