import { useI18n } from "../i18n";
import { useAuth } from "../auth/AuthContext";
import { Emblem } from "./Emblem";

/** Government header: emblem + ministry name, search, language toggle, login/logout, socials. */
export function SiteHeader({ onLoginClick, onHomeClick }:
  { onLoginClick: () => void; onHomeClick: () => void }) {
  const { t, lang, toggle } = useI18n();
  const { isAuthenticated, user, logout } = useAuth();
  const ar = lang === "ar";

  return (
    <header className="topbar">
      <div className="wrap topbar__inner">
        <div className="brand" onClick={onHomeClick} style={{ cursor: "pointer" }}>
          <Emblem />
          <div className="brand__divider" />
          <div className="brand__name">
            <span className="ar">{t("ministry")}</span>
            <span className="en">MINISTRY OF JUSTICE</span>
          </div>
        </div>

        <div className="spacer" />

        <label className="search">
          <input type="search" placeholder={t("search")} aria-label={t("search")} />
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
            <circle cx="11" cy="11" r="7" /><path d="M21 21l-4.3-4.3" />
          </svg>
        </label>

        <button className="lang" onClick={toggle} aria-label="Switch language">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
            <path d="M4 5h7M9 3v2c0 4-2.5 7-5 8M5 9c0 2 2.5 4.5 5 5" />
            <path d="M14 19l3.5-9 3.5 9M15.2 16h4.6" />
          </svg>
          <span>{t("toggleLabel")}</span>
        </button>

        {isAuthenticated ? (
          <button className="authbtn" onClick={logout} title={user?.displayName}>
            {ar ? "خروج" : "Logout"}
          </button>
        ) : (
          <button className="authbtn authbtn--primary" onClick={onLoginClick}>
            {ar ? "تسجيل الدخول" : "Login"}
          </button>
        )}

        <nav className="socials" aria-label="Social media">
          <a href="#" aria-label="X"><svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M18.2 2H21l-6.6 7.5L22 22h-6.8l-4.7-6.2L4.9 22H2l7-8L2 2h7l4.3 5.7L18.2 2zm-2.4 18h1.5L8.3 4H6.7l9.1 16z" /></svg></a>
          <a href="#" aria-label="Instagram"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><rect x="3" y="3" width="18" height="18" rx="5" /><circle cx="12" cy="12" r="4" /><circle cx="17.5" cy="6.5" r="1.2" fill="currentColor" stroke="none" /></svg></a>
          <a href="#" aria-label="Telegram"><svg width="21" height="21" viewBox="0 0 24 24" fill="currentColor"><path d="M21.9 4.3l-3.3 15.6c-.2 1-.9 1.3-1.8.8l-4.8-3.6-2.3 2.2c-.3.3-.5.5-1 .5l.3-4.9 9-8.1c.4-.3-.1-.5-.6-.2L6.4 13l-4.7-1.5c-1-.3-1-1 .2-1.5L20.6 2.9c.9-.3 1.6.2 1.3 1.4z" /></svg></a>
          <a href="#" aria-label="Facebook"><svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M22 12a10 10 0 10-11.6 9.9v-7H7.9V12h2.5V9.8c0-2.5 1.5-3.9 3.8-3.9 1.1 0 2.2.2 2.2.2v2.5h-1.2c-1.2 0-1.6.8-1.6 1.6V12h2.7l-.4 2.9h-2.3v7A10 10 0 0022 12z" /></svg></a>
        </nav>
      </div>
    </header>
  );
}
