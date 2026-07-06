import { useEffect, useState, useCallback } from "react";
import {
  api, type CopyRequestDetail, type FormTemplate, type ParagraphTemplate,
  type AuditEntry, type Lookup, type PanelMember,
} from "../../api/client";
import { useNav } from "../../app/nav";
import { useL, Spinner, ErrorBox } from "../../app/ui";
import { useAuth } from "../../auth/AuthContext";
import { RichText, plainToHtml } from "../../components/RichText";

/** Known header field keys whose Hijri value is auto-derived from the Gregorian date (FR-09).
 *  The pairing is by key convention so the form template still drives rendering. */
const GREGORIAN_KEY = "issueGregorian";
const HIJRI_KEY = "issueHijri";
/** Fixed key under which the president's chosen title (صفة) is stored in the field values. */
const PRESIDENT_TITLE_KEY = "presidentTitle";
/** «رقم القرار»: auto-generated — mirrors the copy's own auto sequential number (copyNumber).
 *  The copyist never types it; the field renders read-only, pre-filled from copyNumber. */
const DECISION_NUMBER_KEY = "decisionNumber";
/** Field-values key holding whether the panel PRESIDENT dissents ("true"/"false"). Per-member
 *  dissent lives on each member object (PanelMember.dissenting). */
const PRESIDENT_DISSENT_KEY = "presidentDissenting";
/** Field-values key: whether the PRESIDENT is a delegated (ندباً) judge from another room/court.
 *  Per-member delegation lives on each member object (PanelMember.delegated). */
const PRESIDENT_DELEGATED_KEY = "presidentDelegated";
/** The fixed capacity (صفة) auto-assigned to a delegated judge — «ندباً» (locked when delegated). */
const DELEGATION_TITLE = "ندباً";

/** Strip the constrained rich-text markup to plain text — used to tell if a dissent section is empty. */
const stripHtml = (html: string) => html.replace(/<[^>]*>/g, "").replace(/&nbsp;/g, " ").trim();

/** Client-only stable ids for section editor rows (keep rich-text instances stable on reorder). */
let _sid = 0;
const nextSid = () => ++_sid;
interface EditSection { id: number; title: string; text: string; }

/** Convert a Gregorian "yyyy-MM-dd" (from <input type=date>) to a Hijri "dd/MM/yyyy" string
 *  using the browser's Umm al-Qura calendar. Returns null if the date can't be parsed. The era
 *  marker (هـ) is intentionally omitted — the printed document appends it. */
function gregorianToHijri(gregorian: string): string | null {
  if (!gregorian) return null;
  const d = new Date(`${gregorian}T00:00:00`);
  if (Number.isNaN(d.getTime())) return null;
  try {
    const parts = new Intl.DateTimeFormat("ar-SA-u-ca-islamic-umalqura", {
      day: "2-digit", month: "2-digit", year: "numeric",
    }).formatToParts(d);
    const y = parts.find((p) => p.type === "year")?.value;
    const m = parts.find((p) => p.type === "month")?.value;
    const day = parts.find((p) => p.type === "day")?.value;
    return y && m && day ? `${day}/${m}/${y}` : null;
  } catch {
    return null;
  }
}

/** Parse the stored panel members, tolerating the legacy shape (array of judge-name strings). */
function parseMembers(raw: string | undefined): PanelMember[] {
  try {
    const a = JSON.parse(raw || "[]");
    if (!Array.isArray(a)) return [];
    return a.map((m) =>
      typeof m === "string"
        ? { judge: m, title: "", dissenting: false, delegated: false }
        : { judge: String(m?.judge ?? m?.name ?? ""), title: String(m?.title ?? ""), dissenting: Boolean(m?.dissenting), delegated: Boolean(m?.delegated) });
  } catch { return []; }
}

/**
 * Reusable ordered-sections editor (insert from the paragraph palette, edit rich text, reorder,
 * remove). Used for BOTH the main copy body and the dissent appendix. Defined at module level so
 * its RichText instances are not remounted on every parent render.
 */
function SectionsEditor({ L, paragraphs, sections, setSections, title, hint }: {
  L: (ar: string, en: string) => string;
  paragraphs: ParagraphTemplate[];
  sections: EditSection[];
  setSections: (fn: (prev: EditSection[]) => EditSection[]) => void;
  title: string;
  hint?: string;
}) {
  const insert = (p: ParagraphTemplate) =>
    setSections((s) => [...s, { id: nextSid(), title: p.title, text: plainToHtml(p.body) }]);
  const update = (i: number, patch: Partial<EditSection>) =>
    setSections((s) => s.map((sec, idx) => (idx === i ? { ...sec, ...patch } : sec)));
  const remove = (i: number) => setSections((s) => s.filter((_, idx) => idx !== i));
  const move = (i: number, dir: -1 | 1) => setSections((s) => {
    const j = i + dir;
    if (j < 0 || j >= s.length) return s;
    const copy = [...s];
    [copy[i], copy[j]] = [copy[j], copy[i]];
    return copy;
  });

  return (
    <div className="card">
      <div className="toolbar">
        <h3 style={{ margin: 0 }}>{title}</h3>
        <div className="spacer" />
        <select className="field" style={{ minWidth: 240 }} value="" onChange={(e) => {
          const p = paragraphs.find((x) => x.id === e.target.value);
          if (p) insert(p);
          e.target.value = "";
        }}>
          <option value="">{L("إدراج فقرة…", "Insert paragraph…")}</option>
          {paragraphs.map((p) => <option key={p.id} value={p.id}>{p.title}</option>)}
        </select>
      </div>

      {hint && <p className="muted">{hint}</p>}
      {sections.length === 0 && (
        <p className="muted">{L("لم تُدرج أي فقرة بعد. اختر فقرة من القائمة أعلاه.",
                                "No sections yet. Insert one from the list above.")}</p>
      )}

      {sections.map((sec, i) => (
        <div key={sec.id} className="section-edit">
          <div className="section-head">
            <input className="section-title" value={sec.title}
              onChange={(e) => update(i, { title: e.target.value })}
              lang="ar" spellCheck
              placeholder={L("عنوان الفقرة", "Section title")} />
            <div className="section-tools">
              <button type="button" className="iconbtn" onClick={() => move(i, -1)} disabled={i === 0} title={L("أعلى", "Up")}>↑</button>
              <button type="button" className="iconbtn" onClick={() => move(i, 1)} disabled={i === sections.length - 1} title={L("أسفل", "Down")}>↓</button>
              <button type="button" className="iconbtn iconbtn--danger" onClick={() => remove(i)} title={L("حذف", "Remove")}>✕</button>
            </div>
          </div>
          <RichText value={sec.text} onChange={(html) => update(i, { text: html })}
            placeholder={L("نص الفقرة…", "Section text…")} />
        </div>
      ))}
    </div>
  );
}

/**
 * FR-07/08/09: the assigned copyist completes content. The FIXED header fields (judging panel +
 * dates) render from the form template; the rest of the document is built dynamically by
 * inserting editable sections from the form type's paragraph palette.
 *
 * The judging panel is a president (judge + chosen title) plus zero or more members, each with a
 * title (صفة) the copyist picks from the admin-defined list; the chosen titles print verbatim.
 * Section text supports inline bold/italic (reflected in the printed PDF).
 *
 * FR-10: when the copy is UnderReview, the Reviewer reuses this same editor to correct it
 * directly (in place) and then approve — instead of returning it to the copyist.
 */
export function PreparePage({ id }: { id: string }) {
  const { navigate } = useNav();
  const L = useL();
  const { user } = useAuth();

  const [detail, setDetail] = useState<CopyRequestDetail | null>(null);
  const [forms, setForms] = useState<FormTemplate[]>([]);
  const [paragraphs, setParagraphs] = useState<ParagraphTemplate[]>([]);
  const [judges, setJudges] = useState<Lookup[]>([]);
  const [allJudges, setAllJudges] = useState<Lookup[]>([]);
  const [titles, setTitles] = useState<Lookup[]>([]);
  const [lastReturn, setLastReturn] = useState<AuditEntry | null>(null);
  const [formTemplateId, setFormTemplateId] = useState<string>("");
  const [values, setValues] = useState<Record<string, string>>({});
  const [sections, setSections] = useState<EditSection[]>([]);
  const [dissentSections, setDissentSections] = useState<EditSection[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const d = await api.getRequest(id);
      const [f, a, jdg, tts, allj] = await Promise.all([
        api.lookupForms(), api.getAudit(id), api.lookupJudges(d.roomId), api.lookupPanelTitles(), api.lookupAllJudges(),
      ]);
      setDetail(d); setForms(f); setJudges(jdg); setTitles(tts); setAllJudges(allj);
      setLastReturn(a.find((x) => x.action === "Return" && x.reason) ?? null);
      setFormTemplateId(d.formTemplateId ?? "");
      try {
        const parsedValues = JSON.parse(d.fieldValuesJson || "{}");
        // «رقم القرار» is auto-generated = the copy's own number; never typed by the copyist.
        if (d.copyNumber) parsedValues[DECISION_NUMBER_KEY] = d.copyNumber;
        setValues(parsedValues);
      } catch { setValues(d.copyNumber ? { [DECISION_NUMBER_KEY]: d.copyNumber } : {}); }
      try {
        const parsed = JSON.parse(d.sectionsJson || "[]");
        setSections(Array.isArray(parsed)
          ? parsed.map((s) => ({ id: nextSid(), title: s.title ?? "", text: s.text ?? "" }))
          : []);
      } catch { setSections([]); }
      try {
        const parsed = JSON.parse(d.dissentSectionsJson || "[]");
        setDissentSections(Array.isArray(parsed)
          ? parsed.map((s) => ({ id: nextSid(), title: s.title ?? "", text: s.text ?? "" }))
          : []);
      } catch { setDissentSections([]); }
    } catch (e) { setErr((e as Error).message); }
  }, [id]);

  useEffect(() => { load(); }, [load]);

  // Insertable paragraphs follow the chosen form type.
  useEffect(() => {
    if (!formTemplateId) { setParagraphs([]); return; }
    api.lookupParagraphs(formTemplateId).then(setParagraphs).catch(() => setParagraphs([]));
  }, [formTemplateId]);

  if (err && !detail) return <ErrorBox message={err} />;
  if (!detail) return <Spinner label={L("جارٍ التحميل…", "Loading…")} />;

  // FR-10: a Reviewer corrects directly while UnderReview; the copyist edits InPreparation/Unlocked.
  const reviewerCorrecting = user?.role === "Reviewer" && detail.state === "UnderReview";
  const copyistEditing = detail.state === "InPreparation" || detail.state === "Unlocked";
  if (!reviewerCorrecting && !copyistEditing) {
    return <ErrorBox message={L("لا يمكن تحرير هذه النسخة في حالتها الحالية.", "This copy cannot be edited in its current state.")} />;
  }

  const selectedForm = forms.find((f) => f.id === formTemplateId);

  // Set a header field; auto-derive the Hijri date whenever the Gregorian date changes (editable).
  const setField = (key: string, val: string) =>
    setValues((v) => {
      const next = { ...v, [key]: val };
      if (key === GREGORIAN_KEY) {
        const hijri = gregorianToHijri(val);
        if (hijri) next[HIJRI_KEY] = hijri;
      }
      return next;
    });

  // Judging panel: a president (single "judge" field) + a dynamic members list ("judges" field,
  // zero or more). Each member carries a chosen title (صفة). No judge may repeat across the panel.
  const presidentKey = selectedForm?.fields.find((f) => f.type === "judge")?.key ?? null;
  const membersKey = selectedForm?.fields.find((f) => f.type === "judges")?.key ?? null;
  const members: PanelMember[] = membersKey ? parseMembers(values[membersKey]) : [];
  const setMembers = (arr: PanelMember[]) => { if (membersKey) setValues((v) => ({ ...v, [membersKey]: JSON.stringify(arr) })); };

  // Dissent (مخالفة): the president and/or any member may dissent. When at least one judge
  // dissents, a dissent appendix (reason sections, signed by the dissenters) is required.
  const presidentDissents = !!presidentKey && !!values[presidentKey] && values[PRESIDENT_DISSENT_KEY] === "true";
  const setPresidentDissents = (on: boolean) => setValues((v) => ({ ...v, [PRESIDENT_DISSENT_KEY]: on ? "true" : "false" }));
  const anyDissent = presidentDissents || members.some((m) => m.judge && !!m.dissenting);

  // Delegation (ندباً): the president/members may be delegated judges from another room/court; their
  // capacity (صفة) is fixed to «ندباً». Toggling delegation resets the judge (the source list changes).
  const presidentDelegated = values[PRESIDENT_DELEGATED_KEY] === "true";
  const setPresidentDelegated = (on: boolean) => setValues((v) => ({
    ...v,
    [PRESIDENT_DELEGATED_KEY]: on ? "true" : "false",
    [PRESIDENT_TITLE_KEY]: on ? DELEGATION_TITLE : "",
    ...(presidentKey ? { [presidentKey]: "" } : {}),
  }));

  const chosenJudges = new Set<string>();
  if (presidentKey && values[presidentKey]) chosenJudges.add(values[presidentKey]);
  members.forEach((m) => { if (m.judge) chosenJudges.add(m.judge); });
  // A delegated (ندباً) judge is picked from ALL active judges (any room/court); otherwise only the
  // copy room's judges. Already-chosen judges are excluded (except the row's own current value).
  const judgeOptions = (current: string, delegated = false) =>
    (delegated ? allJudges : judges).filter((jd) => !chosenJudges.has(jd.name) || jd.name === current);

  // A title <select>: lists the active titles plus (defensively) the currently-stored value even
  // if it was later deactivated, so a saved choice is never silently dropped.
  const titleOptions = (current: string) => {
    const names = titles.map((t) => t.name);
    return current && !names.includes(current) ? [current, ...names] : names;
  };


  // finalize = submit for review (copyist) or approve (reviewer correcting in place).
  async function save(finalize: boolean) {
    setBusy(true); setErr(null); setOk(null);
    try {
      const cleanMembers = members.filter((m) => m.judge);
      // A dissent is meaningful only for a selected judge; recompute from the cleaned panel.
      const hasDissent = presidentDissents || cleanMembers.some((m) => !!m.dissenting);
      if (finalize) {
        if (presidentKey && !values[presidentKey]) throw new Error(L("يجب اختيار رئيس الهيئة.", "Select the panel president."));
        if (presidentKey && values[presidentKey] && !values[PRESIDENT_TITLE_KEY])
          throw new Error(L("يجب اختيار صفة رئيس الهيئة.", "Select the president's title."));
        if (cleanMembers.some((m) => !m.title))
          throw new Error(L("يجب اختيار صفة لكل عضو في الهيئة.", "Select a title for every panel member."));
        // مخالفة: a dissent with no reason text is rejected (منع وإظهار خطأ).
        if (hasDissent && !dissentSections.some((s) => stripHtml(s.text).length > 0 || s.title.trim().length > 0))
          throw new Error(L("يجب كتابة سبب المخالفة في ملحق الرأي المخالف.",
                            "Enter the dissent reason in the dissenting-opinion appendix."));
      }
      const fieldValues = { ...values };
      if (membersKey) fieldValues[membersKey] = JSON.stringify(cleanMembers);
      const payload = {
        formTemplateId: formTemplateId || null,
        fieldValuesJson: JSON.stringify(fieldValues),
        sectionsJson: JSON.stringify(sections.map(({ title, text }) => ({ title, text }))),
        // Persist the dissent appendix only when a judge actually dissents; otherwise store empty.
        dissentSectionsJson: hasDissent ? JSON.stringify(dissentSections.map(({ title, text }) => ({ title, text }))) : "[]",
        body: "",
      };
      // The Reviewer saves through the correct endpoint (stays UnderReview); the copyist saves a draft.
      if (reviewerCorrecting) await api.correct(id, payload); else await api.saveDraft(id, payload);
      if (finalize) {
        if (reviewerCorrecting) await api.approve(id); else await api.submit(id);
        navigate("request", id);
        return;
      }
      setOk(reviewerCorrecting ? L("تم حفظ التصحيح.", "Correction saved.") : L("تم حفظ المسودة.", "Draft saved."));
    } catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }

  const TitleSelect = ({ value, onPick }: { value: string; onPick: (v: string) => void }) => (
    <select style={{ width: 170 }} value={value} onChange={(e) => onPick(e.target.value)}>
      <option value="">{L("اختر الصفة", "Select title")}</option>
      {titleOptions(value).map((t) => <option key={t} value={t}>{t}</option>)}
    </select>
  );

  return (
    <>
      <div className="toolbar">
        <button className="linkbtn" style={{ color: "var(--green-800)" }} onClick={() => navigate("request", id)}>
          ← {L("رجوع", "Back")}
        </button>
      </div>
      <h1 className="page-title">
        {reviewerCorrecting ? L("تصحيح النسخة", "Correct copy") : L("تحضير النسخة", "Prepare copy")} {detail.copyNumber}
      </h1>

      {err && <ErrorBox message={err} />}
      {ok && <div className="okbox">{ok}</div>}
      {lastReturn && (
        <div className="returnbanner">
          <strong>{L("ملاحظات المدقق للتصحيح", "Reviewer's corrections to address")}</strong>
          {lastReturn.reason}
        </div>
      )}

      {/* ── Fixed fields (judging panel + dates) ── */}
      <div className="card">
        <label className="field" style={{ maxWidth: 360 }}>
          <span>{L("نوع القرار", "Decision type")}</span>
          <select value={formTemplateId} onChange={(e) => setFormTemplateId(e.target.value)}>
            <option value="">{L("اختر نوع القرار", "Select decision type")}</option>
            {forms.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
          </select>
        </label>

        {selectedForm && selectedForm.fields.length > 0 && (
          <>
            {(presidentKey || membersKey) && judges.length === 0 && (
              <div className="returnbanner">
                {L("لا يوجد قضاة مخصّصون لهذه الغرفة. أضِفهم من شاشة القضاة.",
                   "No judges are assigned to this room. Add them on the Judges screen.")}
              </div>
            )}
            {(presidentKey || membersKey) && titles.length === 0 && (
              <div className="returnbanner">
                {L("لا توجد صفات معرّفة. عرّفها من شاشة «صفات الهيئة».",
                   "No panel titles defined. Define them on the Panel-titles screen.")}
              </div>
            )}
            <div className="row">
              {selectedForm.fields.map((fld) => (
                fld.type === "judges" ? (
                  <div className="field" key={fld.id} style={{ flexBasis: "100%" }}>
                    <span>{fld.label} — {L("صفر أو أكثر", "zero or more")}</span>
                    {members.map((m, i) => (
                      <div key={i} style={{ display: "flex", gap: 8, marginBottom: 8, flexWrap: "wrap" }}>
                        <select style={{ flex: 1, minWidth: 160 }} value={m.judge}
                          onChange={(e) => setMembers(members.map((x, idx) => idx === i ? { ...x, judge: e.target.value } : x))}>
                          <option value="">{L("اختر القاضي", "Select judge")}</option>
                          {judgeOptions(m.judge, !!m.delegated).map((jd) => <option key={jd.id} value={jd.name}>{jd.name}</option>)}
                        </select>
                        {m.delegated ? (
                          <input style={{ width: 170 }} value={DELEGATION_TITLE} readOnly
                            title={L("صفة القاضي المنتدب — ندباً", "Delegated judge's capacity")} />
                        ) : (
                          <TitleSelect value={m.title}
                            onPick={(v) => setMembers(members.map((x, idx) => idx === i ? { ...x, title: v } : x))} />
                        )}
                        <label style={{ display: "flex", alignItems: "center", gap: 4, whiteSpace: "nowrap" }}
                          title={L("قاضٍ منتدب من غرفة/محكمة أخرى (ندباً)", "Delegated judge from another room/court")}>
                          <input type="checkbox" checked={!!m.delegated}
                            onChange={(e) => setMembers(members.map((x, idx) => idx === i
                              ? { ...x, delegated: e.target.checked, title: e.target.checked ? DELEGATION_TITLE : "", judge: "" }
                              : x))} />
                          {L("منتدب", "Delegated")}
                        </label>
                        <label style={{ display: "flex", alignItems: "center", gap: 4, whiteSpace: "nowrap" }}
                          title={L("مخالف للقرار", "Dissents from the decision")}>
                          <input type="checkbox" checked={!!m.dissenting} disabled={!m.judge}
                            onChange={(e) => setMembers(members.map((x, idx) => idx === i ? { ...x, dissenting: e.target.checked } : x))} />
                          {L("مخالف", "Dissents")}
                        </label>
                        <button type="button" className="iconbtn iconbtn--danger"
                          onClick={() => setMembers(members.filter((_, idx) => idx !== i))} title={L("حذف عضو", "Remove member")}>✕</button>
                      </div>
                    ))}
                    <button type="button" className="btn btn--ghost"
                      onClick={() => setMembers([...members, { judge: "", title: "" }])}>{L("إضافة عضو", "Add member")}</button>
                  </div>
                ) : fld.type === "judge" ? (
                  <div className="field" key={fld.id}>
                    <span>{fld.label}</span>
                    <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                      <select style={{ flex: 1, minWidth: 160 }} value={values[fld.key] ?? ""}
                        onChange={(e) => setValues((v) => ({ ...v, [fld.key]: e.target.value }))}>
                        <option value="">{L("اختر القاضي", "Select judge")}</option>
                        {judgeOptions(values[fld.key] ?? "", presidentDelegated).map((jd) => <option key={jd.id} value={jd.name}>{jd.name}</option>)}
                      </select>
                      {presidentDelegated ? (
                        <input style={{ width: 170 }} value={DELEGATION_TITLE} readOnly
                          title={L("صفة القاضي المنتدب — ندباً", "Delegated judge's capacity")} />
                      ) : (
                        <TitleSelect value={values[PRESIDENT_TITLE_KEY] ?? ""}
                          onPick={(v) => setValues((vv) => ({ ...vv, [PRESIDENT_TITLE_KEY]: v }))} />
                      )}
                      <label style={{ display: "flex", alignItems: "center", gap: 4, whiteSpace: "nowrap" }}
                        title={L("قاضٍ منتدب من غرفة/محكمة أخرى (ندباً)", "Delegated judge from another room/court")}>
                        <input type="checkbox" checked={presidentDelegated}
                          onChange={(e) => setPresidentDelegated(e.target.checked)} />
                        {L("منتدب", "Delegated")}
                      </label>
                      <label style={{ display: "flex", alignItems: "center", gap: 4, whiteSpace: "nowrap" }}
                        title={L("رئيس الغرفة مخالف للقرار", "Room president dissents")}>
                        <input type="checkbox" checked={presidentDissents} disabled={!values[fld.key]}
                          onChange={(e) => setPresidentDissents(e.target.checked)} />
                        {L("مخالف", "Dissents")}
                      </label>
                    </div>
                  </div>
                ) : fld.key === DECISION_NUMBER_KEY ? (
                  <label className="field" key={fld.id}>
                    <span>{fld.label} — {L("يُولَّد تلقائيًا", "auto-generated")}</span>
                    <input
                      type="text"
                      value={detail.copyNumber ?? values[fld.key] ?? ""}
                      readOnly
                      title={L("رقم القرار يُولَّد تلقائيًا من رقم النسخة", "The decision number is auto-generated from the copy number")}
                    />
                  </label>
                ) : (
                  <label className="field" key={fld.id}>
                    <span>{fld.label}</span>
                    <input
                      type={fld.type === "date" ? "date" : fld.type === "number" ? "number" : "text"}
                      value={values[fld.key] ?? ""}
                      onChange={(e) => setField(fld.key, e.target.value)}
                      lang={fld.type === "text" ? "ar" : undefined}
                      spellCheck={fld.type === "text"}
                    />
                  </label>
                )
              ))}
            </div>
          </>
        )}
      </div>

      {/* ── Dynamic sections (main body) ── */}
      <SectionsEditor L={L} paragraphs={paragraphs} sections={sections} setSections={setSections}
        title={L("فقرات النسخة", "Copy sections")} />

      {/* ── Dissent appendix — only when at least one judge dissents (مخالفة القضاة) ── */}
      {anyDissent && (
        <SectionsEditor L={L} paragraphs={paragraphs} sections={dissentSections} setSections={setDissentSections}
          title={L("ملحق: الرأي المخالف", "Appendix: Dissenting opinion")}
          hint={L("يُطبع في صفحة جديدة بعد نهاية القرار، ويُوقّعه القضاة المخالفون.",
                  "Printed on a new page after the decision, signed by the dissenting judges.")} />
      )}

      <div className="btn-row">
        <button className="btn btn--ghost" disabled={busy} onClick={() => save(false)}>
          {reviewerCorrecting ? L("حفظ التصحيح", "Save correction") : L("حفظ مسودة", "Save draft")}
        </button>
        <button className="btn" disabled={busy} onClick={() => save(true)}>
          {reviewerCorrecting ? L("حفظ واعتماد", "Save & approve") : L("حفظ وإرسال للمراجعة", "Save & submit for review")}
        </button>
      </div>
    </>
  );
}
