import { useEffect, useState, useCallback } from "react";
import { useI18n } from "../i18n";
import { Emblem } from "./Emblem";

/**
 * Hero news slider. The slides are PLACEHOLDER data — in production they come from the news
 *  Each slide carries both languages for the RTL toggle.
 */
interface Slide { date: string; ar: string; en: string; }

const slides: Slide[] = [
  { date: "2026-06-10", ar: "وزارة العدل تطلق نظام ديوان النسخ القضائي", en: "Ministry of Justice Launches the Judicial Copying System" },
  { date: "2026-06-08", ar: "سير عمل رقمي جديد يختصر زمن اعتماد النسخ إلى النصف", en: "New Digital Workflow Cuts Copy Approval Time by Half" },
  { date: "2026-06-05", ar: "ديوان محكمة النقض يعتمد الترقيم التسلسلي للنسخ", en: "Court of Cassation Registry Adopts Sequential Copy Numbering" },
  { date: "2026-06-01", ar: "منح المسؤولين صلاحيات فتح موثّقة للنسخ المعتمدة", en: "Administrators Gain Audited Unlock Controls for Approved Copies" },
  { date: "2026-05-28", ar: "وحدة المدققين تضيف تتبّع التصحيحات المباشر", en: "Reviewers Module Adds Inline Correction Tracking" },
];

export function HeroSlider() {
  const { t, lang } = useI18n();
  const [idx, setIdx] = useState(0);

  const go = useCallback((n: number) => setIdx((n + slides.length) % slides.length), []);
  const next = useCallback(() => go(idx + 1), [go, idx]);

  useEffect(() => {
    const timer = setInterval(() => setIdx((i) => (i + 1) % slides.length), 6000);
    return () => clearInterval(timer);
  }, []);

  const slide = slides[idx];

  return (
    <section className="hero">
      <div className="wrap">
        <div className="hero__card">
          <div className="hero__left">
            <div className="geo" />
            <div className="hero__date">
              <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><rect x="3" y="4" width="18" height="18" rx="2" /><path d="M16 2v4M8 2v4M3 10h18" /></svg>
              <span>{slide.date}</span>
            </div>
            <h1 className="hero__title">{lang === "ar" ? slide.ar : slide.en}</h1>
            <button className="btn-news">{t("show_news")}</button>
          </div>

          <div className="hero__right">
            <div className="geo" />
            <div className="crest-watermark">
              <Emblem size={150} color="#cdb079" accent="#cdb079" />
              <span className="ar">{t("ministry")}</span>
            </div>
          </div>

          <button className="arrow arrow--prev" onClick={() => go(idx - 1)} aria-label="Previous">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round"><path d="M15 6l-6 6 6 6" /></svg>
          </button>
          <button className="arrow arrow--next" onClick={next} aria-label="Next">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round"><path d="M9 6l6 6-6 6" /></svg>
          </button>
        </div>

        <div className="dots">
          {slides.map((_, i) => (
            <button key={i} className={i === idx ? "active" : undefined} onClick={() => go(i)} aria-label={`Slide ${i + 1}`} />
          ))}
        </div>
      </div>
    </section>
  );
}
