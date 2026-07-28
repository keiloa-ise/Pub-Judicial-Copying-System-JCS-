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
      </div>
    </header>
  );
}
