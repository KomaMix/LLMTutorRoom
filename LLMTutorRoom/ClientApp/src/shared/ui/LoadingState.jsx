import { Loader2 } from "lucide-react";

export function LoadingState({ message = "Загрузка" }) {
  return (
    <div className="screen-state">
      <Loader2 className="spin" size={26} aria-hidden="true" />
      <span>{message}</span>
    </div>
  );
}
