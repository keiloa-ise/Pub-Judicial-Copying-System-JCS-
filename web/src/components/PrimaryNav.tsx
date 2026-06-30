import { useState } from "react";
import { useI18n } from "../i18n";

// Nav items are keys into the i18n catalogue — labels are never hardcoded per language.
const items = ["nav_home", "nav_news", "nav_services", "nav_courts", "nav_copies", "nav_jobs", "nav_contact"];

export function PrimaryNav() {
  const { t } = useI18n();
  const [active, setActive] = useState("nav_home");

  return (
    <nav className="nav" aria-label="Primary">
      <div className="wrap nav__inner">
        {items.map((key) => (
          <button
            key={key}
            className={active === key ? "active" : undefined}
            onClick={() => setActive(key)}
          >
            {t(key)}
          </button>
        ))}
      </div>
    </nav>
  );
}
