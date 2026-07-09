import { useEffect, useState, type FormEvent } from "react";
import { api, type Court, type Room, type NumberingPolicy, type CopyNumberingPolicy } from "../../api/client";
import { useL, ErrorBox, Spinner, Modal, useSort, SortTh, numberingPolicyLabels } from "../../app/ui";
import { useI18n } from "../../i18n";

const LEVELS = Array.from({ length: 26 }, (_, i) => String.fromCharCode(65 + i)); // A..Z

/** FR-03: Administrator manages courts — create, edit name, activate/deactivate — and the
 *  rooms (غرف) within each court. The court code is immutable (it is embedded in issued copy
 *  numbers, e.g. C-001/2026/0001); a room code is unique within its court. Judges are assigned
 *  to rooms (on the Judges screen), and a copy request targets a room. */
export function CourtsPage() {
  const L = useL();
  const { lang } = useI18n();
  const ak = lang === "ar" ? "ar" : "en";
  const [courts, setCourts] = useState<Court[] | null>(null);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // inline court edit
  const [editing, setEditing] = useState<Court | null>(null);
  const [edName, setEdName] = useState("");
  const [edActive, setEdActive] = useState(true);

  // ── Rooms management ──
  const [roomCourtId, setRoomCourtId] = useState("");
  const [rooms, setRooms] = useState<Room[] | null>(null);
  const [roomCode, setRoomCode] = useState("");
  const [roomName, setRoomName] = useState("");
  const [roomPolicy, setRoomPolicy] = useState<NumberingPolicy>("Court");
  const [roomLevel, setRoomLevel] = useState("A");
  const [roomCopyPolicy, setRoomCopyPolicy] = useState<CopyNumberingPolicy>("Room"); // رقم النسخة scope (default room)
  const [edRoom, setEdRoom] = useState<Room | null>(null);
  const [edRoomName, setEdRoomName] = useState("");
  const [edRoomActive, setEdRoomActive] = useState(true);
  const [edRoomPolicy, setEdRoomPolicy] = useState<NumberingPolicy>("Court");
  const [edRoomLevel, setEdRoomLevel] = useState("A");
  const [edRoomCopyPolicy, setEdRoomCopyPolicy] = useState<CopyNumberingPolicy>("Room");

  const load = () => api.admin.listCourts().then(setCourts).catch((e) => setErr(e.message));
  useEffect(() => { load(); }, []);

  const courtSort = useSort<Court>(courts ?? [], { code: (c) => c.code, name: (c) => c.name, status: (c) => c.isActive });
  const roomSort = useSort<Room>(rooms ?? [], { code: (r) => r.code, name: (r) => r.name, status: (r) => r.isActive });

  const loadRooms = (courtId: string) => {
    if (!courtId) { setRooms(null); return; }
    api.admin.listRooms(courtId).then(setRooms).catch((e) => setErr(e.message));
  };
  useEffect(() => { loadRooms(roomCourtId); }, [roomCourtId]);

  async function run(fn: () => Promise<unknown>, after?: () => void) {
    setErr(null); setBusy(true);
    try { await fn(); await load(); after?.(); }
    catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  function create(e: FormEvent) {
    e.preventDefault();
    run(async () => { await api.admin.createCourt(code, name); setCode(""); setName(""); });
  }

  function startEdit(c: Court) { setErr(null); setEditing(c); setEdName(c.name); setEdActive(c.isActive); }

  function saveEdit(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    const id = editing.id;
    run(async () => { await api.admin.updateCourt(id, edName, edActive); setEditing(null); });
  }

  // ── Rooms actions ──
  function createRoom(e: FormEvent) {
    e.preventDefault();
    if (!roomCourtId) return;
    const level = roomPolicy === "Special" ? roomLevel : null;
    run(async () => { await api.admin.createRoom(roomCourtId, roomCode, roomName, roomPolicy, level, roomCopyPolicy); setRoomCode(""); setRoomName(""); setRoomPolicy("Court"); setRoomLevel("A"); setRoomCopyPolicy("Room"); },
        () => loadRooms(roomCourtId));
  }

  function startEditRoom(r: Room) {
    setErr(null); setEdRoom(r); setEdRoomName(r.name); setEdRoomActive(r.isActive);
    setEdRoomPolicy(r.numberingPolicy); setEdRoomLevel(r.numberingLevel ?? "A");
    setEdRoomCopyPolicy(r.copyNumberingPolicy);
  }

  function saveRoomEdit(e: FormEvent) {
    e.preventDefault();
    if (!edRoom) return;
    const r = edRoom;
    const level = edRoomPolicy === "Special" ? edRoomLevel : null;
    run(async () => { await api.admin.updateRoom(r.id, edRoomName, edRoomActive, edRoomPolicy, level, edRoomCopyPolicy); setEdRoom(null); },
        () => loadRooms(roomCourtId));
  }

  return (
    <>
      <h1 className="page-title">{L("المحاكم والغرف", "Courts & rooms")}</h1>
      {err && <ErrorBox message={err} />}

      <Modal open={!!editing} onClose={() => setEditing(null)} title={L("تعديل المحكمة", "Edit court")}>
        <form className="card" onSubmit={saveEdit}>
          <div className="row">
            <label className="field" style={{ maxWidth: 200 }}>
              <span>{L("الرمز (غير قابل للتعديل)", "Code (read-only)")}</span>
              <input value={editing?.code ?? ""} disabled />
            </label>
            <label className="field">
              <span>{L("اسم المحكمة", "Court name")}</span>
              <input value={edName} onChange={(e) => setEdName(e.target.value)} required />
            </label>
            <label className="field" style={{ maxWidth: 180 }}>
              <span>{L("الحالة", "Status")}</span>
              <select value={edActive ? "1" : "0"} onChange={(e) => setEdActive(e.target.value === "1")}>
                <option value="1">{L("نشط", "Active")}</option>
                <option value="0">{L("معطّل", "Inactive")}</option>
              </select>
            </label>
          </div>
          <div className="btn-row">
            <button className="btn" disabled={busy}>{L("حفظ", "Save")}</button>
            <button className="btn btn--ghost" type="button" onClick={() => setEditing(null)}>{L("إلغاء", "Cancel")}</button>
          </div>
        </form>
      </Modal>

      <form className="card" onSubmit={create}>
        <h3>{L("إضافة محكمة", "Add court")}</h3>
        <div className="row">
          <label className="field" style={{ maxWidth: 200 }}>
            <span>{L("الرمز", "Code")}</span>
            <input value={code} onChange={(e) => setCode(e.target.value)} required />
          </label>
          <label className="field">
            <span>{L("اسم المحكمة", "Court name")}</span>
            <input value={name} onChange={(e) => setName(e.target.value)} required />
          </label>
        </div>
        <button className="btn" disabled={busy}>{L("إضافة محكمة", "Add court")}</button>
      </form>

      {!courts ? <Spinner /> : (
        <table className="table">
          <thead><tr>
            <SortTh label={L("الرمز", "Code")} k="code" sortKey={courtSort.sortKey} sortDir={courtSort.sortDir} onSort={courtSort.onSort} />
            <SortTh label={L("الاسم", "Name")} k="name" sortKey={courtSort.sortKey} sortDir={courtSort.sortDir} onSort={courtSort.onSort} />
            <SortTh label={L("الحالة", "Status")} k="status" sortKey={courtSort.sortKey} sortDir={courtSort.sortDir} onSort={courtSort.onSort} />
            <th></th>
          </tr></thead>
          <tbody>
            {courtSort.sorted.map((c) => (
              <tr key={c.id} style={{ cursor: "default" }}>
                <td>{c.code}</td>
                <td>{c.name}</td>
                <td>{c.isActive ? <span className="badge s-approved">{L("نشط", "Active")}</span> : <span className="badge s-created">{L("معطّل", "Inactive")}</span>}</td>
                <td>
                  <div className="btn-row" style={{ margin: 0 }}>
                    <button className="btn btn--ghost" onClick={() => startEdit(c)}>{L("تعديل", "Edit")}</button>
                    <button className="btn btn--ghost" onClick={() => run(() => api.admin.updateCourt(c.id, c.name, !c.isActive))}>
                      {c.isActive ? L("تعطيل", "Deactivate") : L("تفعيل", "Activate")}
                    </button>
                    <button className="btn btn--ghost" onClick={() => setRoomCourtId(c.id)}>{L("الغرف", "Rooms")}</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {/* ── Rooms (غرف) for a selected court ── */}
      <div className="card" style={{ marginTop: 20 }}>
        <h3>{L("إدارة الغرف", "Manage rooms")}</h3>
        <label className="field" style={{ maxWidth: 360 }}>
          <span>{L("المحكمة", "Court")}</span>
          <select value={roomCourtId} onChange={(e) => setRoomCourtId(e.target.value)}>
            <option value="">{L("اختر المحكمة لإدارة غرفها", "Select a court to manage its rooms")}</option>
            {(courts ?? []).map((c) => <option key={c.id} value={c.id}>{c.name} ({c.code})</option>)}
          </select>
        </label>

        {roomCourtId && (
          <>
            <Modal open={!!edRoom} onClose={() => setEdRoom(null)} title={L("تعديل الغرفة", "Edit room")}>
              <form className="card" onSubmit={saveRoomEdit}>
                <div className="row">
                  <label className="field" style={{ maxWidth: 200 }}>
                    <span>{L("الرمز (غير قابل للتعديل)", "Code (read-only)")}</span>
                    <input value={edRoom?.code ?? ""} disabled />
                  </label>
                  <label className="field">
                    <span>{L("اسم الغرفة", "Room name")}</span>
                    <input value={edRoomName} onChange={(e) => setEdRoomName(e.target.value)} required />
                  </label>
                  <label className="field" style={{ maxWidth: 180 }}>
                    <span>{L("الحالة", "Status")}</span>
                    <select value={edRoomActive ? "1" : "0"} onChange={(e) => setEdRoomActive(e.target.value === "1")}>
                      <option value="1">{L("نشط", "Active")}</option>
                      <option value="0">{L("معطّل", "Inactive")}</option>
                    </select>
                  </label>
                </div>
                <div className="row">
                  <label className="field" style={{ maxWidth: 220 }}>
                    <span>{L("ترقيم المتفرق", "Misc numbering")}</span>
                    <select value={edRoomPolicy} onChange={(e) => setEdRoomPolicy(e.target.value as NumberingPolicy)}>
                      {(["Court", "Room", "Special"] as NumberingPolicy[]).map((p) =>
                        <option key={p} value={p}>{numberingPolicyLabels[p][ak]}</option>)}
                    </select>
                  </label>
                  <label className="field" style={{ maxWidth: 220 }}>
                    <span>{L("ترقيم النسخة (عادي)", "Copy numbering")}</span>
                    <select value={edRoomCopyPolicy} onChange={(e) => setEdRoomCopyPolicy(e.target.value as CopyNumberingPolicy)}>
                      {(["Room", "Court"] as CopyNumberingPolicy[]).map((p) =>
                        <option key={p} value={p}>{numberingPolicyLabels[p][ak]}</option>)}
                    </select>
                  </label>
                  {edRoomPolicy === "Special" && (
                    <label className="field" style={{ maxWidth: 120 }}>
                      <span>{L("المستوى", "Level")}</span>
                      <select value={edRoomLevel} onChange={(e) => setEdRoomLevel(e.target.value)}>
                        {LEVELS.map((l) => <option key={l} value={l}>{l}</option>)}
                      </select>
                    </label>
                  )}
                </div>
                <div className="btn-row">
                  <button className="btn" disabled={busy}>{L("حفظ", "Save")}</button>
                  <button className="btn btn--ghost" type="button" onClick={() => setEdRoom(null)}>{L("إلغاء", "Cancel")}</button>
                </div>
              </form>
            </Modal>

            <form className="row" onSubmit={createRoom} style={{ alignItems: "flex-end" }}>
              <label className="field" style={{ maxWidth: 200 }}>
                <span>{L("رمز الغرفة", "Room code")}</span>
                <input value={roomCode} onChange={(e) => setRoomCode(e.target.value)} required />
              </label>
              <label className="field">
                <span>{L("اسم الغرفة", "Room name")}</span>
                <input value={roomName} onChange={(e) => setRoomName(e.target.value)} required />
              </label>
              <label className="field" style={{ maxWidth: 200 }}>
                <span>{L("ترقيم المتفرق", "Misc numbering")}</span>
                <select value={roomPolicy} onChange={(e) => setRoomPolicy(e.target.value as NumberingPolicy)}>
                  {(["Court", "Room", "Special"] as NumberingPolicy[]).map((p) =>
                    <option key={p} value={p}>{numberingPolicyLabels[p][ak]}</option>)}
                </select>
              </label>
              <label className="field" style={{ maxWidth: 200 }}>
                <span>{L("ترقيم النسخة (عادي)", "Copy numbering")}</span>
                <select value={roomCopyPolicy} onChange={(e) => setRoomCopyPolicy(e.target.value as CopyNumberingPolicy)}>
                  {(["Room", "Court"] as CopyNumberingPolicy[]).map((p) =>
                    <option key={p} value={p}>{numberingPolicyLabels[p][ak]}</option>)}
                </select>
              </label>
              {roomPolicy === "Special" && (
                <label className="field" style={{ maxWidth: 120 }}>
                  <span>{L("المستوى", "Level")}</span>
                  <select value={roomLevel} onChange={(e) => setRoomLevel(e.target.value)}>
                    {LEVELS.map((l) => <option key={l} value={l}>{l}</option>)}
                  </select>
                </label>
              )}
              <button className="btn" disabled={busy}>{L("إضافة غرفة", "Add room")}</button>
            </form>

            {!rooms ? <Spinner /> : rooms.length === 0 ? (
              <p className="muted">{L("لا توجد غرف في هذه المحكمة بعد.", "No rooms in this court yet.")}</p>
            ) : (
              <table className="table">
                <thead><tr>
                  <SortTh label={L("الرمز", "Code")} k="code" sortKey={roomSort.sortKey} sortDir={roomSort.sortDir} onSort={roomSort.onSort} />
                  <SortTh label={L("الاسم", "Name")} k="name" sortKey={roomSort.sortKey} sortDir={roomSort.sortDir} onSort={roomSort.onSort} />
                  <th>{L("ترقيم المتفرق", "Misc numbering")}</th>
                  <th>{L("ترقيم النسخة", "Copy numbering")}</th>
                  <SortTh label={L("الحالة", "Status")} k="status" sortKey={roomSort.sortKey} sortDir={roomSort.sortDir} onSort={roomSort.onSort} />
                  <th></th>
                </tr></thead>
                <tbody>
                  {roomSort.sorted.map((r) => (
                    <tr key={r.id} style={{ cursor: "default" }}>
                      <td>{r.code}</td>
                      <td>{r.name}</td>
                      <td>{numberingPolicyLabels[r.numberingPolicy][ak]}{r.numberingPolicy === "Special" && r.numberingLevel ? ` (${r.numberingLevel})` : ""}</td>
                      <td>{numberingPolicyLabels[r.copyNumberingPolicy][ak]}</td>
                      <td>{r.isActive ? <span className="badge s-approved">{L("نشط", "Active")}</span> : <span className="badge s-created">{L("معطّل", "Inactive")}</span>}</td>
                      <td>
                        <div className="btn-row" style={{ margin: 0 }}>
                          <button className="btn btn--ghost" onClick={() => startEditRoom(r)}>{L("تعديل", "Edit")}</button>
                          <button className="btn btn--ghost" onClick={() => run(() => api.admin.updateRoom(r.id, r.name, !r.isActive, r.numberingPolicy, r.numberingLevel, r.copyNumberingPolicy), () => loadRooms(roomCourtId))}>
                            {r.isActive ? L("تعطيل", "Deactivate") : L("تفعيل", "Activate")}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </div>
    </>
  );
}
