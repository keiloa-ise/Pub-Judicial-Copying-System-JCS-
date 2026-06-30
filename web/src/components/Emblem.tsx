import { useState } from "react";

/**
 * Ministry emblem. Renders the real logo from `/logo.png` (place the file in web/public/).
 * If that file is missing it gracefully falls back to a built-in vector crest so the UI never
 * shows a broken image. Used in the header, login, hero watermark, and app bar.
 */
export function Emblem({ size = 58, color = "#b89456", accent = "#0d4536" }:
  { size?: number; color?: string; accent?: string }) {
  const [failed, setFailed] = useState(false);

  if (!failed) {
    return (
      <img
        src="/logo.png"
        alt="شعار وزارة العدل"
        width={size}
        height={size}
        onError={() => setFailed(true)}
        style={{ width: size, height: size, borderRadius: "50%", objectFit: "cover", display: "block" }}
      />
    );
  }

  // Fallback vector crest (only shown if /logo.png is absent).
  return (
    <svg width={size} height={size} viewBox="0 0 64 64" role="img" aria-label="Emblem">
      <circle cx="32" cy="32" r="31" fill="none" stroke={color} strokeWidth="1.4" />
      <path d="M32 12l4 9 10-2-6 8 6 8-10-2-4 9-4-9-10 2 6-8-6-8 10 2z" fill={color} />
      <path d="M20 40c4 6 8 8 12 8s8-2 12-8c-4 3-8 4-12 4s-8-1-12-4z" fill={accent} />
      <circle cx="32" cy="30" r="3.4" fill={accent} />
    </svg>
  );
}
