import { useL } from "../app/ui";

export type ConnState = "online" | "offline" | "refreshing";

/**
 * A pill button showing the live connection state with the API via colour:
 * green = connected, red = disconnected, amber (pulsing) = refreshing. Clicking it
 * triggers an immediate (silent) refresh. Used by the auto-polling lists (FR-13 UX).
 */
export function ConnectionStatus({ state, lastUpdated, onRefresh }: {
  state: ConnState;
  lastUpdated: Date | null;
  onRefresh: () => void;
}) {
  const L = useL();
  const label =
    state === "refreshing" ? L("جارٍ التحديث…", "Refreshing…")
    : state === "offline" ? L("انقطع الاتصال", "Disconnected")
    : L("متصل", "Connected");
  const time = lastUpdated
    ? lastUpdated.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" })
    : "—";

  return (
    <button
      type="button"
      className={`connstatus connstatus--${state}`}
      onClick={onRefresh}
      disabled={state === "refreshing"}
      title={L("تحديث الآن", "Refresh now")}
    >
      <span className="connstatus__dot" aria-hidden="true" />
      <span>{label}</span>
      <span className="connstatus__time">· {L("آخر تحديث", "Updated")} {time}</span>
    </button>
  );
}
