import { UserRoundCheck } from "lucide-react";
import { ManualTaskReviewForm } from "./ManualTaskReviewForm.jsx";

export function ManualReviewPanel({ review, onDirtyChange, onSaved }) {
  const manualResults = review.taskResults.filter(result => result.status === "manual-review");

  if (manualResults.length === 0) {
    return null;
  }

  return (
    <section
      className="manual-review-panel teacher-manual-review-panel"
      id={`teacher-manual-review-${review.id}`}
      aria-labelledby={`teacher-manual-review-title-${review.id}`}
    >
      <header className="teacher-section-header">
        <div>
          <span className="eyebrow">Ручная проверка</span>
          <h3 id={`teacher-manual-review-title-${review.id}`}>{review.testTitle}</h3>
        </div>
        <UserRoundCheck size={18} aria-hidden="true" />
      </header>
      <div className="result-list">
        {manualResults.map(result => (
          <ManualTaskReviewForm
            key={`${review.id}:${result.taskId}`}
            reviewId={review.id}
            result={result}
            onDirtyChange={onDirtyChange}
            onSaved={onSaved}
          />
        ))}
      </div>
    </section>
  );
}

export default ManualReviewPanel;
