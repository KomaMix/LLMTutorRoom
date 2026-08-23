import { useCallback, useEffect, useRef, useState } from "react";
import { useBlocker } from "react-router-dom";
import { CheckCircle2, ClipboardCheck, Clock3, Loader2, UserRoundCheck } from "lucide-react";
import { useNavigationGuard } from "../../app/NavigationGuardContext.jsx";
import { getReviewHistory } from "../../api/classroomApi.js";
import { parseReviewHistoryPage } from "../../shared/lib/overview.js";
import { isTerminalReview, mergeReviews } from "../../shared/lib/reviews.js";
import { ManualReviewPanel } from "./ManualReviewPanel.jsx";
import { ReviewRows } from "./ReviewRows.jsx";

const unsavedManualReviewPrompt = "Есть несохранённая ручная оценка. Покинуть страницу и потерять изменения?";

export function ReviewQueue({ reviews, terminalReviewsNextCursor, onReviewsChanged }) {
  const { registerBeforeLogout } = useNavigationGuard();
  const [selectedReviewId, setSelectedReviewId] = useState("");
  const [showHistory, setShowHistory] = useState(false);
  const [currentTerminalReviews, setCurrentTerminalReviews] = useState(() =>
    reviews.filter(isTerminalReview));
  const [currentNextCursor, setCurrentNextCursor] = useState(terminalReviewsNextCursor ?? null);
  const [historicalTerminalReviews, setHistoricalTerminalReviews] = useState([]);
  const [historicalNextCursor, setHistoricalNextCursor] = useState(null);
  const [isLoadingHistory, setIsLoadingHistory] = useState(false);
  const [historyError, setHistoryError] = useState("");
  const [dirtyManualTasks, setDirtyManualTasks] = useState(() => new Set());
  const [manualFormGeneration, setManualFormGeneration] = useState(0);
  const requestIdRef = useRef(0);
  const hasUnsavedManualReview = dirtyManualTasks.size > 0;
  const confirmDiscardManualReview = useCallback(() => {
    if (!hasUnsavedManualReview) {
      return true;
    }

    if (!window.confirm(unsavedManualReviewPrompt)) {
      return false;
    }

    setDirtyManualTasks(new Set());
    setManualFormGeneration(current => current + 1);
    return true;
  }, [hasUnsavedManualReview]);
  const shouldBlockNavigation = useCallback(({ currentLocation, nextLocation }) => {
    const currentUrl = `${currentLocation.pathname}${currentLocation.search}${currentLocation.hash}`;
    const nextUrl = `${nextLocation.pathname}${nextLocation.search}${nextLocation.hash}`;
    return currentUrl !== nextUrl && hasUnsavedManualReview;
  }, [hasUnsavedManualReview]);
  const blocker = useBlocker(shouldBlockNavigation);
  const {
    state: blockerState,
    location: blockedLocation,
    proceed: proceedNavigation,
    reset: resetNavigation
  } = blocker;

  useEffect(() => registerBeforeLogout(confirmDiscardManualReview), [
    confirmDiscardManualReview,
    registerBeforeLogout
  ]);

  useEffect(() => {
    if (blockerState !== "blocked") {
      return;
    }

    if (window.confirm(unsavedManualReviewPrompt)) {
      proceedNavigation();
    } else {
      resetNavigation();
    }
  }, [blockedLocation?.key, blockerState, proceedNavigation, resetNavigation]);

  useEffect(() => {
    if (!hasUnsavedManualReview) {
      return;
    }

    function handleBeforeUnload(event) {
      event.preventDefault();
      event.returnValue = "";
    }

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [hasUnsavedManualReview]);

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

  function handleHistoryMode(nextShowHistory) {
    if (nextShowHistory === showHistory) {
      return;
    }

    if (!confirmDiscardManualReview()) {
      return;
    }

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

  const handleManualTaskDirtyChange = useCallback((taskKey, isDirty) => {
    setDirtyManualTasks(current => {
      const alreadyDirty = current.has(taskKey);
      if (alreadyDirty === isDirty) {
        return current;
      }

      const next = new Set(current);
      if (isDirty) {
        next.add(taskKey);
      } else {
        next.delete(taskKey);
      }
      return next;
    });
  }, []);

  function handleSelectReview(reviewId) {
    if (reviewId === selectedReview?.id) {
      return;
    }

    if (!confirmDiscardManualReview()) {
      return;
    }

    setSelectedReviewId(reviewId);
  }

  const manualReviewCount = visibleReviews.filter(
    review => review.status === "manual-review").length;
  const processingCount = visibleReviews.filter(
    review => !isTerminalReview(review) && review.status !== "manual-review").length;
  const completedCount = visibleReviews.filter(isTerminalReview).length;

  return (
    <section className="panel teacher-page teacher-review-page">
      <header className="teacher-page-heading">
        <div>
          <span className="eyebrow">Работы учеников</span>
          <h2>Проверки учеников</h2>
          <p>Следите за автоматической проверкой и выставляйте баллы там, где нужен преподаватель.</p>
        </div>
        <div className="teacher-segmented" role="group" aria-label="Версии проверок">
          <button
            type="button"
            className={!showHistory ? "active" : ""}
            aria-pressed={!showHistory}
            disabled={isLoadingHistory}
            onClick={() => handleHistoryMode(false)}
          >
            Актуальные
          </button>
          <button
            type="button"
            className={showHistory ? "active" : ""}
            aria-pressed={showHistory}
            disabled={isLoadingHistory}
            onClick={() => handleHistoryMode(true)}
          >
            {isLoadingHistory && showHistory
              ? <Loader2 className="spin" size={15} aria-hidden="true" />
              : <ClipboardCheck size={15} aria-hidden="true" />}
            Вся история
          </button>
        </div>
      </header>

      <div className="teacher-review-stats" aria-label="Состояние проверок">
        <div>
          <span className="manual"><UserRoundCheck size={17} aria-hidden="true" /></span>
          <p><strong>{manualReviewCount}</strong><span>Нужен преподаватель</span></p>
        </div>
        <div>
          <span className="processing"><Clock3 size={17} aria-hidden="true" /></span>
          <p><strong>{processingCount}</strong><span>Проверяются</span></p>
        </div>
        <div>
          <span className="complete"><CheckCircle2 size={17} aria-hidden="true" /></span>
          <p><strong>{completedCount}</strong><span>Закрыто в списке</span></p>
        </div>
      </div>

      <ReviewRows
        reviews={visibleReviews}
        selectedReviewId={selectedReview?.id ?? ""}
        onSelectReview={handleSelectReview}
      />
      {nextCursor && (
        <button
          type="button"
          className="button secondary teacher-history-load"
          disabled={isLoadingHistory}
          onClick={handleLoadMore}
        >
          {isLoadingHistory && <Loader2 className="spin" size={16} aria-hidden="true" />}
          {isLoadingHistory ? "Загрузка..." : "Загрузить ещё"}
        </button>
      )}
      {historyError && <p className="form-error" role="alert">{historyError}</p>}
      {selectedReview?.status === "manual-review" && (
        <ManualReviewPanel
          key={`${selectedReview.id}:${manualFormGeneration}`}
          review={selectedReview}
          onDirtyChange={handleManualTaskDirtyChange}
          onSaved={handleReviewsChanged}
        />
      )}
    </section>
  );
}

export default ReviewQueue;
