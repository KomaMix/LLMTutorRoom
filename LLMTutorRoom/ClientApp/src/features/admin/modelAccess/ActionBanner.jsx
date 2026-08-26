import { ShieldCheck } from "lucide-react";

export function ActionBanner({ actionBannerRef, message }) {
  if (!message) {
    return null;
  }

  return (
    <div
      ref={actionBannerRef}
      className={`admin-action-banner ${message.type}`}
      role={message.type === "error" ? "alert" : "status"}
      tabIndex={-1}
    >
      <ShieldCheck size={18} aria-hidden="true" />
      <p className={message.type === "error" ? "form-error" : "form-note"}>
        {message.text}
      </p>
    </div>
  );
}
