import { Check, X } from "lucide-react";

export function AnswerOption({ text, state = "", showMarker = true, accessibleLabel }) {
  return (
    <li className={`answer-option ${state}`}>
      <span className="answer-option-icon" aria-hidden="true">
        {showMarker && (state === "incorrect" ? <X size={18} /> : <Check size={18} />)}
      </span>
      <span className="answer-option-text">
        <span className="visually-hidden">{accessibleLabel}</span>
        {text}
      </span>
    </li>
  );
}
