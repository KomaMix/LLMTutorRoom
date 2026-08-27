import { Eye } from "lucide-react";
import { formatDate } from "../../shared/lib/dates.js";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";

export function ReviewRows({
  reviews,
  compact = false,
  emptyMessage = "Нет проверок в этом списке.",
  selectedReviewId = "",
  onSelectReview = null
}) {
  if (reviews.length === 0) {
    return <p className="muted teacher-review-empty">{emptyMessage}</p>;
  }

  return (
    <div className={`review-table teacher-review-list${compact ? " compact" : ""}`}>
      {reviews.map(reviewItem => {
        const studentName = reviewItem.studentName
          || reviewItem.studentUserId
          || "Студент";
        const isSelected = reviewItem.id === selectedReviewId;

        return (
          <article
            className={`review-row teacher-review-row${isSelected ? " selected" : ""}`}
            key={reviewItem.id}
          >
            <div className="teacher-review-identity">
              <span className="teacher-review-avatar" aria-hidden="true">
                {studentName.trim().charAt(0).toLocaleUpperCase("ru-RU") || "У"}
              </span>
              <div>
                <strong>{studentName}</strong>
                <span>{reviewItem.testTitle} · версия {reviewItem.testRevision}</span>
              </div>
            </div>

            {!compact && (
              <div className="teacher-review-field">
                <span>Отправлено</span>
                <strong>{formatDate(reviewItem.submittedAt)}</strong>
              </div>
            )}

            <div className="teacher-review-field status-field">
              {!compact && <span>Статус</span>}
              <StatusBadge status={reviewItem.status} />
            </div>

            <div className="teacher-review-field score-field">
              {!compact && <span>Балл</span>}
              <strong>
                {reviewItem.status === "checked"
                  ? `${reviewItem.score}/${reviewItem.maxScore}`
                  : `—/${reviewItem.maxScore}`}
              </strong>
            </div>

            {!compact && reviewItem.status === "manual-review" && (
              <button
                type="button"
                className="button secondary teacher-review-open"
                aria-expanded={isSelected}
                title="Открыть проверку"
                onClick={() => onSelectReview?.(reviewItem.id)}
              >
                <Eye size={16} aria-hidden="true" />
                Проверить
              </button>
            )}
          </article>
        );
      })}
    </div>
  );
}

export default ReviewRows;
