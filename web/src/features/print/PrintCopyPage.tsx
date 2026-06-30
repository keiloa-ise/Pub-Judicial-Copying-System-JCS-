import { useEffect, useRef, useState } from "react";
import { api, type CopyRequestDetail } from "../../api/client";
import { useNav } from "../../app/nav";
import { useL, ErrorBox, StateBadge } from "../../app/ui";
import "./print.css";

/**
 * FR-15: preview page for the copy to be printed. The document ("إعلام الحكم") is rendered to a
 * PDF on the SERVER and shown here in the browser's native PDF viewer via a direct same-origin
 * URL (authorized by the HttpOnly "jcs_pdf" cookie). The preview IS exactly what will print, and
 * there is no editable DOM to alter before printing (integrity control). Watermarks (faint logo
 * always, "مسودة قرار" for non-approved copies) are produced server-side.
 */
export function PrintCopyPage({ id }: { id: string }) {
  const { navigate } = useNav();
  const L = useL();
  const [detail, setDetail] = useState<CopyRequestDetail | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const frameRef = useRef<HTMLIFrameElement>(null);

  useEffect(() => { api.getRequest(id).then(setDetail).catch((e) => setErr(e.message)); }, [id]);

  const pdfSrc = api.pdfUrl(id);

  return (
    <div className="pdfwrap">
      <div className="toolbar">
        <button className="linkbtn" style={{ color: "var(--green-800)" }} onClick={() => navigate("request", id)}>
          ← {L("رجوع", "Back")}
        </button>
        <div className="spacer" />
        {detail && <StateBadge state={detail.state} />}
      </div>

      <h1 className="page-title">{L("معاينة النسخة", "Copy preview")} {detail?.copyNumber ?? ""}</h1>
      {detail && <p className="muted" style={{ marginTop: -4 }}>{detail.courtName} — {detail.roomName}</p>}
      {detail && detail.state !== "Approved" && (
        <div className="returnbanner">{L("هذه نسخة غير معتمدة — تُطبع بعلامة «مسودة قرار».",
          "This copy is not approved — it prints with a “draft” watermark.")}</div>
      )}
      {err && <ErrorBox message={err} />}

      <div className="print-toolbar noprint">
        <button className="btn" onClick={() => frameRef.current?.contentWindow?.print()}>{L("طباعة", "Print")}</button>
        <a className="btn btn--ghost" href={pdfSrc} download={`judgment-${detail?.copyNumber?.replace(/\//g, "-") ?? id}.pdf`}>
          {L("تنزيل PDF", "Download PDF")}
        </a>
      </div>

      <iframe ref={frameRef} className="pdfframe" src={pdfSrc} title={L("معاينة الطباعة", "Print preview")} />
    </div>
  );
}
