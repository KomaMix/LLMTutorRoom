import { statusText } from "../config/status.js";

export function StatusBadge({ status, variant = "outlined" }) {
  const statusClass = Object.hasOwn(statusText, status) ? status : "unknown";
  return (
    <span className={`status ${statusClass}${variant === "inline" ? " status-inline" : ""}`}>
      {statusText[status] ?? status ?? "—"}
    </span>
  );
}
