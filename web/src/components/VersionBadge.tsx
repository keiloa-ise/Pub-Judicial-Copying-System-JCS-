import { useEffect, useState } from "react";
import { api, type AppVersion } from "../api/client";
import { useL } from "../app/ui";

export function VersionBadge() {
  const L = useL();
  const [version, setVersion] = useState<AppVersion | null>(null);

  useEffect(() => {
    let alive = true;
    api.version()
      .then((value) => { if (alive) setVersion(value); })
      .catch(() => { if (alive) setVersion(null); });

    return () => { alive = false; };
  }, []);

  if (!version) return null;

  const label = version.version || version.commit?.slice(0, 12) || "development";
  const title = [
    `${L("الإصدار", "Version")}: ${label}`,
    version.branch ? `${L("الفرع", "Branch")}: ${version.branch}` : null,
    version.commit ? `Commit: ${version.commit}` : null,
    version.deployedAt ? `${L("تاريخ النشر", "Deployed")}: ${version.deployedAt}` : null,
    version.commitDate ? `${L("تاريخ الكومِت", "Commit date")}: ${version.commitDate}` : null,
  ].filter(Boolean).join("\n");

  return (
    <span className="version-badge" title={title} aria-label={title}>
      <span className="version-badge__label">{L("الإصدار", "Version")}</span>
      <code>{label}</code>
    </span>
  );
}
