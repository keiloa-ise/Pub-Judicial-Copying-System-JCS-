import { createContext, useContext, useState, useCallback, type ReactNode } from "react";

/**
 * Minimal i18n + direction context. Production default is Arabic/RTL . In the real
 * app these strings move to a proper i18n catalogue; UI text is never hardcoded in components.
 */
export type Lang = "ar" | "en";

type Dict = Record<string, string>;

const strings: Record<Lang, Dict> = {
  ar: {
    ministry: "وزارة العدل",
    search: "بحث",
    toggleLabel: "English",
    nav_home: "الرئيسية",
    nav_news: "الأخبار",
    nav_services: "الخدمات",
    nav_courts: "المحاكم",
    nav_copies: "طلبات النسخ",
    nav_jobs: "الوظائف",
    nav_contact: "اتصل بنا",
    show_news: "عرض الخبر",
    appName: "نظام ديوان النسخ القضائي",
  },
  en: {
    ministry: "MINISTRY OF JUSTICE",
    search: "Search",
    toggleLabel: "العربية",
    nav_home: "Home",
    nav_news: "News",
    nav_services: "Services",
    nav_courts: "Courts",
    nav_copies: "Copy requests",
    nav_jobs: "Job vacancies",
    nav_contact: "Contact us",
    show_news: "Show news",
    appName: "Judicial Copying System",
  },
};

interface I18nValue {
  lang: Lang;
  dir: "rtl" | "ltr";
  t: (key: string) => string;
  toggle: () => void;
}

const I18nContext = createContext<I18nValue | null>(null);

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [lang, setLang] = useState<Lang>("ar");

  const toggle = useCallback(() => {
    setLang((prev) => {
      const next = prev === "ar" ? "en" : "ar";
      const root = document.documentElement;
      root.lang = next;
      root.dir = next === "ar" ? "rtl" : "ltr";
      return next;
    });
  }, []);

  const value: I18nValue = {
    lang,
    dir: lang === "ar" ? "rtl" : "ltr",
    t: (key) => strings[lang][key] ?? key,
    toggle,
  };

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nValue {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error("useI18n must be used within LanguageProvider");
  return ctx;
}
