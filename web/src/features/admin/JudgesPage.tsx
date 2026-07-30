import { useEffect, useState, type FormEvent } from "react";
import { api, type Judge, type Court, type Room } from "../../api/client";
import { useL, ErrorBox, Spinner, Modal, SearchableMultiSelect, useSort, SortTh } from "../../app/ui";

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
  const roomOptions = rooms.map((r) => {
    const court = courts.find((c) => c.id === r.courtId);
    return {
      id: r.id,
      label: `${r.name} (${r.code})`,
      group: court?.name,
      searchText: `${r.name} ${r.code} ${court?.name ?? ""} ${court?.code ?? ""}`,
    };
  });

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

  const RoomPicker = ({ selected, onChange }: { selected: string[]; onChange: (ids: string[]) => void }) => (
    <SearchableMultiSelect
      options={roomOptions}
      selected={selected}
      onChange={onChange}
      placeholder={L("ابحث باسم الغرفة أو رمزها أو المحكمة...", "Search by room, code, or court...")}
      emptyLabel={courts.length === 0
        ? L("أضف محكمة وغرفة أولاً.", "Add a court and a room first.")
        : L("لا توجد غرف مطابقة.", "No matching rooms.")}
      selectedLabel={L("الغرف المختارة", "Selected rooms")}
    />
  );

  return (
    <>
      <h1 className="page-title">{L("القضاة", "Judges")}</h1>
      {err && <ErrorBox message={err} onDismiss={() => setErr(null)} />}

      <Modal open={!!editing} onClose={() => setEditing(null)} title={L("تعديل القاضي", "Edit judge")}>
        <form className="card" onSubmit={saveEdit}>
          <label className="field"><span>{L("اسم القاضي", "Judge name")}</span>
            <input value={edName} onChange={(e) => setEdName(e.target.value)} required /></label>
          <label className="field">
            <span>{L("الغرف (واحدة على الأقل)", "Rooms (at least one)")}</span>
            <RoomPicker selected={edRooms} onChange={setEdRooms} />
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
          <RoomPicker selected={roomIds} onChange={setRoomIds} />
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
