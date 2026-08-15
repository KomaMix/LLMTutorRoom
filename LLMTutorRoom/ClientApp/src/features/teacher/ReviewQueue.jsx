import { useState } from "react";
import { ClipboardCheck } from "lucide-react";
import { ManualReviewPanel } from "./ManualReviewPanel.jsx";
import { ReviewRows } from "./ReviewRows.jsx";

export function ReviewQueue({ reviews, onReviewsChanged }) {
  const [selectedReviewId, setSelectedReviewId] = useState("");
  const selectedReview = reviews.find(review => review.id === selectedReviewId)
    ?? reviews.find(review => review.status === "manual-review")
    ?? null;

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Submissions</span>
          <h2>Проверки учеников</h2>
        </div>
        <ClipboardCheck size={18} aria-hidden="true" />
      </div>
      <ReviewRows reviews={reviews} onSelectReview={setSelectedReviewId} />
      {selectedReview?.status === "manual-review" && (
        <ManualReviewPanel review={selectedReview} onSaved={onReviewsChanged} />
      )}
    </section>
  );
}

export default ReviewQueue;
