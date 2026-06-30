import { useRef, useEffect, useCallback } from "react";

/**
 * Minimal, dependency-free rich-text editor for section text. Supports BOLD and ITALIC only,
 * stored as a tightly-constrained HTML subset (`<b>`, `<i>`, `<br>` + HTML-escaped text) that the
 * server re-sanitizes on save and the PDF renderer turns into styled runs. Uncontrolled after
 * mount (innerHTML is set once) so the caret never jumps; every edit re-serializes the DOM to the
 * safe subset, so pasted markup is dropped automatically.
 */
const escapeHtml = (s: string) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

/** Plain text (e.g. a paragraph-template body) → the safe HTML subset, for first insertion. */
export function plainToHtml(text: string): string {
  return escapeHtml(text ?? "").replace(/\r?\n/g, "<br>");
}

function serializeNode(node: Node): string {
  if (node.nodeType === Node.TEXT_NODE) return escapeHtml(node.textContent ?? "");
  if (node.nodeType !== Node.ELEMENT_NODE) return "";
  const el = node as HTMLElement;
  const tag = el.tagName.toLowerCase();
  if (tag === "br") return "<br>";
  let inner = "";
  el.childNodes.forEach((c) => { inner += serializeNode(c); });
  if (tag === "div" || tag === "p") return inner; // block separation handled at the root level
  const fw = el.style?.fontWeight ?? "";
  const bold = tag === "b" || tag === "strong" || fw === "bold" || /^[6-9]00$/.test(fw);
  const italic = tag === "i" || tag === "em" || el.style?.fontStyle === "italic";
  let r = inner;
  if (italic) r = `<i>${r}</i>`;
  if (bold) r = `<b>${r}</b>`;
  return r;
}

function serializeRoot(root: HTMLElement): string {
  let out = "";
  root.childNodes.forEach((child) => {
    const isBlock = child.nodeType === Node.ELEMENT_NODE && /^(div|p)$/i.test((child as HTMLElement).tagName);
    if (isBlock && out.length > 0 && !out.endsWith("<br>")) out += "<br>";
    out += serializeNode(child);
  });
  return out.replace(/(<br>)+$/, ""); // trim trailing caret break(s)
}

export function RichText({ value, onChange, placeholder, disabled }: {
  value: string; onChange: (html: string) => void; placeholder?: string; disabled?: boolean;
}) {
  const ref = useRef<HTMLDivElement>(null);

  // Set the initial content once; uncontrolled afterwards so the caret is never reset.
  useEffect(() => {
    if (ref.current) ref.current.innerHTML = value || "";
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const emit = useCallback(() => {
    if (ref.current) onChange(serializeRoot(ref.current));
  }, [onChange]);

  const cmd = (command: "bold" | "italic") => {
    ref.current?.focus();
    try { document.execCommand("styleWithCSS", false, "false"); } catch { /* older browsers */ }
    document.execCommand(command);
    emit();
  };

  return (
    <div className="richtext">
      <div className="richtext__toolbar">
        <button type="button" className="iconbtn" title="Bold (Ctrl+B)" disabled={disabled}
          onMouseDown={(e) => { e.preventDefault(); cmd("bold"); }}><b>B</b></button>
        <button type="button" className="iconbtn" title="Italic (Ctrl+I)" disabled={disabled}
          onMouseDown={(e) => { e.preventDefault(); cmd("italic"); }}><i>I</i></button>
      </div>
      <div
        ref={ref}
        className="richtext__area"
        contentEditable={!disabled}
        dir="rtl"
        lang="ar"
        spellCheck
        data-placeholder={placeholder ?? ""}
        onInput={emit}
        suppressContentEditableWarning
      />
    </div>
  );
}
