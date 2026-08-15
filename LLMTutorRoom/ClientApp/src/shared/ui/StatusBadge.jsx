import { statusText } from "../config/status.js";

export function StatusBadge({ status }) {
  const statusClass = Object.hasOwn(statusText, status) ? status : "unknown";
  return <span className={`status ${statusClass}`}>{statusText[status] ?? status ?? "—"}</span>;
}
