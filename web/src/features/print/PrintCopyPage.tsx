import { useEffect, useState, useCallback } from "react";
import { api, type CopyRequestDetail } from "../../api/client";
import { useNav } from "../../app/nav";
import { useL, ErrorBox, StateBadge } from "../../app/ui";
import "./print.css";

/** Sends an already-fetched PDF blob to the printer via a hidden iframe (no server round-trip). */
function printBlob(blob: Blob) {
  const url = URL.createObjectURL(blob);
  const frame = document.createElement("iframe");
  frame.style.cssText = "position:fixed;right:0;bottom:0;width:0;height:0;border:0";
  frame.src = url;
  frame.onload = () => { frame.contentWindow?.focus(); frame.contentWindow?.print(); };
  document.body.appendChild(frame);
  setTimeout(() => { URL.revokeObjectURL(url); frame.remove(); }, 60_000);
}

/** sessionStorage key the detail page sets right after an approval so print opens automatically (FR-15 R2). */
const AUTOPRINT_KEY = "jcs_autoprint_id";

/**
 * FR-15: preview + print. The document ("إعلام الحكم") is rendered on the SERVER and shown here in the
 * browser's PDF viewer via a same-origin URL (read-only PREVIEW — never records a print). PRINTING is a
 * separate action (POST /print) that enforces the print order + once-per-approval rule server-side and
 * returns the bytes to print. On approval the copy auto-prints (R2). An approved copy prints once; a
 * re-print needs an Administrator unlock + re-approval (R3).
 */
export function PrintCopyPage({ id }: { id: string }) {
  const { navigate } = useNav();
  const L = useL();
  const [detail, setDetail] = useState<CopyRequestDetail | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(() => api.getRequest(id).then(setDetail).catch((e) => setErr(e.message)), [id]);
  useEffect(() => { load(); }, [load]);

  const pdfSrc = api.pdfUrl(id);

  const doPrint = useCallback(async () => {
    setBusy(true); setErr(null); setOk(null);
    try {
      const blob = await api.printPdf(id);   // enforces order + once-per-approval; records the print
      printBlob(blob);
      setOk(L("تم إرسال القرار إلى الطباعة.", "Sent to the printer."));
      await load();                           // refresh so the printed state (and gating) updates
    } catch (e) { setErr((e as Error).message); }
    finally { setBusy(false); }
  }, [id, L, load]);

  // R2: auto-print once when navigated here with the flag set (after an approval, or the detail-page
  // «طباعة» button).
  useEffect(() => {
    if (!detail) return;
    if (sessionStorage.getItem(AUTOPRINT_KEY) === id) {
      sessionStorage.removeItem(AUTOPRINT_KEY);
      doPrint();
    }
  }, [detail, id, doPrint]);

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
      {ok && <div className="okbox">{ok}</div>}

      <div className="print-toolbar noprint">
        {/* طباعة: the controlled action — enforces the print order on first print, records it, then prints. */}
        <button className="btn" disabled={busy} onClick={doPrint}>
          {busy ? L("جارٍ الطباعة…", "Printing…") : L("طباعة", "Print")}
        </button>
        {/* تنزيل: read-only preview download — never records a print. */}
        <a className="btn btn--ghost" href={pdfSrc} download={`judgment-${detail?.copyNumber?.replace(/\//g, "-") ?? id}.pdf`}>
          {L("تنزيل PDF (معاينة)", "Download PDF (preview)")}
        </a>
      </div>

      {/* Read-only preview (does NOT record a print). */}
      <iframe className="pdfframe" src={pdfSrc} title={L("معاينة الطباعة", "Print preview")} />
    </div>
  );
}
