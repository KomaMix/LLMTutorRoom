import { useEffect, useRef, useState } from "react";
import { ClipboardCheck, Loader2 } from "lucide-react";
import { getReviewHistory } from "../../api/classroomApi.js";
import { parseReviewHistoryPage } from "../../shared/lib/overview.js";
import { isTerminalReview, mergeReviews } from "../../shared/lib/reviews.js";
import { ManualReviewPanel } from "./ManualReviewPanel.jsx";
import { ReviewRows } from "./ReviewRows.jsx";

export function ReviewQueue({ reviews, terminalReviewsNextCursor, onReviewsChanged }) {
  const [selectedReviewId, setSelectedReviewId] = useState("");
  const [showHistory, setShowHistory] = useState(false);
  const [currentTerminalReviews, setCurrentTerminalReviews] = useState(() =>
    reviews.filter(isTerminalReview));
  const [currentNextCursor, setCurrentNextCursor] = useState(terminalReviewsNextCursor ?? null);
  const [historicalTerminalReviews, setHistoricalTerminalReviews] = useState([]);
  const [historicalNextCursor, setHistoricalNextCursor] = useState(null);
  const [isLoadingHistory, setIsLoadingHistory] = useState(false);
  const [historyError, setHistoryError] = useState("");
  const requestIdRef = useRef(0);

  useEffect(() => {
    setCurrentTerminalReviews(reviews.filter(isTerminalReview));
    setCurrentNextCursor(terminalReviewsNextCursor ?? null);
  }, [reviews, terminalReviewsNextCursor]);

  const nonTerminalReviews = reviews.filter(review => !isTerminalReview(review));
  const visibleReviews = mergeReviews(
    nonTerminalReviews,
    showHistory ? historicalTerminalReviews : currentTerminalReviews);
  const selectedReview = visibleReviews.find(review => review.id === selectedReviewId)
    ?? visibleReviews.find(review => review.status === "manual-review")
    ?? null;
  const nextCursor = showHistory ? historicalNextCursor : currentNextCursor;

  async function loadHistoryPage({ includeHistoricalVersions, cursor, replace }) {
    const requestId = ++requestIdRef.current;
    setIsLoadingHistory(true);
    setHistoryError("");
    try {
      const page = parseReviewHistoryPage(await getReviewHistory({
        cursor,
        includeHistoricalVersions
      }));
      if (requestId !== requestIdRef.current) {
        return;
      }

      if (includeHistoricalVersions) {
        setHistoricalTerminalReviews(current => replace
          ? page.reviews
          : mergeReviews(current, page.reviews));
        setHistoricalNextCursor(page.nextCursor ?? null);
      } else {
        setCurrentTerminalReviews(current => replace
          ? page.reviews
          : mergeReviews(current, page.reviews));
        setCurrentNextCursor(page.nextCursor ?? null);
      }
    } catch (error) {
      if (requestId === requestIdRef.current && error?.name !== "AbortError") {
        setHistoryError("Не удалось загрузить историю проверок.");
      }
    } finally {
      if (requestId === requestIdRef.current) {
        setIsLoadingHistory(false);
      }
    }
  }

  function handleHistoryToggle() {
    const nextShowHistory = !showHistory;
    setShowHistory(nextShowHistory);
    setSelectedReviewId("");
    setHistoryError("");
    if (nextShowHistory && historicalTerminalReviews.length === 0) {
      loadHistoryPage({
        includeHistoricalVersions: true,
        cursor: null,
        replace: true
      });
    }
  }

  function handleLoadMore() {
    if (!nextCursor || isLoadingHistory) {
      return;
    }

    loadHistoryPage({
      includeHistoricalVersions: showHistory,
      cursor: nextCursor,
      replace: false
    });
  }

  async function handleReviewsChanged() {
    await onReviewsChanged?.();
    if (showHistory) {
      await loadHistoryPage({
        includeHistoricalVersions: true,
        cursor: null,
        replace: true
      });
    }
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Submissions</span>
          <h2>Проверки учеников</h2>
        </div>
        <button
          type="button"
          className="button secondary"
          aria-pressed={showHistory}
          disabled={isLoadingHistory}
          onClick={handleHistoryToggle}
        >
          {isLoadingHistory
            ? <Loader2 className="spin" size={16} aria-hidden="true" />
            : <ClipboardCheck size={16} aria-hidden="true" />}
          {showHistory ? "Только актуальные" : "Все версии"}
        </button>
      </div>
      <ReviewRows reviews={visibleReviews} onSelectReview={setSelectedReviewId} />
      {nextCursor && (
        <button
          type="button"
          className="button secondary"
          disabled={isLoadingHistory}
          onClick={handleLoadMore}
        >
          {isLoadingHistory && <Loader2 className="spin" size={16} aria-hidden="true" />}
          {isLoadingHistory ? "Загрузка..." : "Загрузить ещё"}
        </button>
      )}
      {historyError && <p className="form-note error" role="alert">{historyError}</p>}
      {selectedReview?.status === "manual-review" && (
        <ManualReviewPanel review={selectedReview} onSaved={handleReviewsChanged} />
      )}
    </section>
  );
}

export default ReviewQueue;
