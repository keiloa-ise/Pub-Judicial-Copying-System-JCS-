import { useEffect, useState, Fragment } from "react";
import { api, type Court, type Room } from "../../api/client";
import { useL, ErrorBox, Spinner, numberingPolicyLabels } from "../../app/ui";
import { useI18n } from "../../i18n";

/** FR-06: a convenient read-only view of رقم المتفرق numbering — per court, the special levels
 *  (A–Z) and the rooms grouped onto each, plus the court-level and room-level rooms. Special levels
 *  are scoped PER COURT, so rooms sharing a level within a court share one running sequence. */
export function NumberingLevelsPage() {
  const L = useL();
  const { lang } = useI18n();
  const ak = lang === "ar" ? "ar" : "en";
  const [courts, setCourts] = useState<Court[] | null>(null);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([api.admin.listCourts(), api.admin.listRooms()])
      .then(([c, r]) => { setCourts(c); setRooms(r); })
      .catch((e) => setErr(e.message));
  }, []);

  if (err) return <ErrorBox message={err} />;
  if (!courts) return <Spinner label={L("جارٍ التحميل…", "Loading…")} />;

  return (
    <>
      <h1 className="page-title">{L("مستويات ترقيم المتفرق", "Misc numbering levels")}</h1>
      <p className="page-sub">
        {L("لكل محكمة: المستويات الخاصة (A–Z) والغرف المرتبطة بكل مستوى، إضافةً إلى غرف مستوى المحكمة ومستوى الغرفة. المستويات الخاصة مُعرَّفة داخل كل محكمة على حدة.",
           "Per court: special levels (A–Z) and their rooms, plus court-level and room-level rooms. Special levels are defined within each court.")}
      </p>

      {courts.map((c) => {
        const courtRooms = rooms.filter((r) => r.courtId === c.id);
        if (courtRooms.length === 0) return null;
        const courtLevel = courtRooms.filter((r) => r.numberingPolicy === "Court");
        const roomLevel = courtRooms.filter((r) => r.numberingPolicy === "Room");
        const byLevel = new Map<string, Room[]>();
        for (const r of courtRooms.filter((x) => x.numberingPolicy === "Special")) {
          const k = r.numberingLevel ?? "?";
          if (!byLevel.has(k)) byLevel.set(k, []);
          byLevel.get(k)!.push(r);
        }
        return (
          <div className="card" key={c.id}>
            <h3>{c.name} ({c.code})</h3>
            <dl className="kv">
              <dt>{numberingPolicyLabels.Court[ak]}</dt>
              <dd>{courtLevel.length ? L(`${courtLevel.length} غرفة — تسلسل واحد على مستوى المحكمة`, `${courtLevel.length} rooms — one court-wide sequence`) : "—"}</dd>
              <dt>{numberingPolicyLabels.Room[ak]}</dt>
              <dd>{roomLevel.length ? roomLevel.map((r) => r.name).join("، ") : "—"}</dd>
              {[...byLevel.keys()].sort().map((level) => (
                <Fragment key={level}>
                  <dt>{numberingPolicyLabels.Special[ak]} {level}</dt>
                  <dd>{byLevel.get(level)!.map((r) => r.name).join("، ")}</dd>
                </Fragment>
              ))}
            </dl>
          </div>
        );
      })}
    </>
  );
}
