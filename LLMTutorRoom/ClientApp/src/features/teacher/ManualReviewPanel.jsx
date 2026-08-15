import { UserRoundCheck } from "lucide-react";
import { ManualTaskReviewForm } from "./ManualTaskReviewForm.jsx";

export function ManualReviewPanel({ review, onSaved }) {
  const manualResults = review.taskResults.filter(result => result.status === "manual-review");

  if (manualResults.length === 0) {
    return null;
  }

  return (
    <div className="manual-review-panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Ручная проверка</span>
          <h3>{review.testTitle}</h3>
        </div>
        <UserRoundCheck size={18} aria-hidden="true" />
      </div>
      <div className="result-list">
        {manualResults.map(result => (
          <ManualTaskReviewForm
            key={`${review.id}:${result.taskId}`}
            reviewId={review.id}
            result={result}
            onSaved={onSaved}
          />
        ))}
      </div>
    </div>
  );
}

export default ManualReviewPanel;
