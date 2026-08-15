import { Eye } from "lucide-react";
import { formatDate } from "../../shared/lib/dates.js";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";

export function ReviewRows({ reviews, compact = false, onSelectReview = null }) {
  if (reviews.length === 0) {
    return <p className="muted">Нет проверок в этом списке.</p>;
  }

  return (
    <div className="review-table">
      {reviews.map(reviewItem => (
        <article className="review-row" key={reviewItem.id}>
          <div>
            <strong>{reviewItem.studentName || reviewItem.studentUserId || "Студент"}</strong>
            <span>{reviewItem.testTitle}</span>
          </div>
          {!compact && <span>{formatDate(reviewItem.submittedAt)}</span>}
          <StatusBadge status={reviewItem.status} />
          <strong>{reviewItem.status === "checked" ? `${reviewItem.score}/${reviewItem.maxScore}` : `-/${reviewItem.maxScore}`}</strong>
          {!compact && reviewItem.status === "manual-review" && (
            <button
              type="button"
              className="icon-button"
              title="Открыть проверку"
              onClick={() => onSelectReview?.(reviewItem.id)}
            >
              <Eye size={16} aria-hidden="true" />
            </button>
          )}
        </article>
      ))}
    </div>
  );
}

export default ReviewRows;
