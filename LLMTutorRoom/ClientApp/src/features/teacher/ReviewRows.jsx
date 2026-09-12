import { Fragment } from "react";
import { Eye } from "lucide-react";
import { Link } from "react-router-dom";
import { formatDate } from "../../shared/lib/dates.js";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";

export function ReviewRows({
  reviews,
  compact = false,
  emptyMessage = "Нет проверок в этом списке.",
  selectedReviewId = "",
  selectedReviewDetails = null,
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
        const isManualReview = reviewItem.status === "manual-review";
        const reviewSearchParams = new URLSearchParams({
          testId: reviewItem.testId,
          reviewId: reviewItem.id
        });
        const reviewAnchor = isManualReview
          ? `teacher-manual-review-${reviewItem.id}`
          : `teacher-review-details-${reviewItem.id}`;

        return (
          <Fragment key={reviewItem.id}>
            <article className={`review-row teacher-review-row${isSelected ? " selected" : ""}`}>
              <div className="teacher-review-identity">
                <strong>{studentName}</strong>
                <span>{reviewItem.testTitle} · версия {reviewItem.testRevision}</span>
              </div>

              {!compact && (
                <div className="teacher-review-field">
                  <span>Отправлено</span>
                  <strong>{formatDate(reviewItem.submittedAt)}</strong>
                </div>
              )}

              <div className="teacher-review-field status-field">
                {!compact && <span>Статус</span>}
                {compact ? (
                  <Link
                    className="teacher-review-status-link"
                    to={`/teacher/reviews?${reviewSearchParams}#${reviewAnchor}`}
                    aria-label={`${isManualReview ? "Открыть ручную проверку" : "Открыть проверку"}: ${reviewItem.testTitle}, ${studentName}`}
                  >
                    <StatusBadge status={reviewItem.status} />
                  </Link>
                ) : (
                  <StatusBadge status={reviewItem.status} />
                )}
              </div>

              {!compact && (
                <button
                  type="button"
                  className="button secondary teacher-review-open"
                  aria-expanded={isSelected}
                  aria-controls={`teacher-review-details-${reviewItem.id}`}
                  title={isSelected
                    ? "Свернуть подробности проверки"
                    : isManualReview
                      ? "Открыть ручную проверку"
                      : "Открыть подробности проверки"}
                  onClick={() => onSelectReview?.(reviewItem.id)}
                >
                  <Eye size={16} aria-hidden="true" />
                  {reviewItem.status === "manual-review" ? "Проверить" : "Подробнее"}
                </button>
              )}
            </article>
            {!compact && isSelected && selectedReviewDetails}
          </Fragment>
        );
      })}
    </div>
  );
}

export default ReviewRows;
