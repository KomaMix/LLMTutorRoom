import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useBlocker, useLocation } from "react-router-dom";
import { CheckCircle2, ClipboardCheck, Clock3, Loader2, UserRoundCheck } from "lucide-react";
import { useNavigationGuard } from "../../app/NavigationGuardContext.jsx";
import { getReviewHistory } from "../../api/classroomApi.js";
import { parseReviewHistoryPage } from "../../shared/lib/overview.js";
import { isTerminalReview, mergeReviews } from "../../shared/lib/reviews.js";
import { ManualReviewPanel } from "./ManualReviewPanel.jsx";
import { ReviewDetailsPanel } from "./ReviewDetailsPanel.jsx";
import { ReviewRows } from "./ReviewRows.jsx";

const unsavedManualReviewPrompt = "Есть несохранённая ручная оценка. Продолжить и потерять изменения?";

function createTestOptions(tests, reviewGroups) {
  const optionsById = new Map(tests.map(test => [
    test.id,
    { id: test.id, title: test.title || "Тест без названия" }
  ]));

  reviewGroups.flat().forEach(review => {
    if (!review.testId || optionsById.has(review.testId)) {
      return;
    }

    optionsById.set(review.testId, {
      id: review.testId,
      title: review.testTitle || "Тест без названия"
    });
  });

  return [...optionsById.values()];
}

function getInitialTestId(tests, reviews) {
  const reviewRequiringTeacher = reviews.find(review => review.status === "manual-review");
  return reviewRequiringTeacher?.testId
    ?? reviews[0]?.testId
    ?? tests[0]?.id
    ?? "";
}

function getLinkedSelection(tests, reviews, search) {
  const searchParams = new URLSearchParams(search);
  const reviewId = searchParams.get("reviewId") ?? "";
  const review = reviews.find(item => String(item.id) === reviewId);
  const testId = review?.testId
    ?? tests.find(test => String(test.id) === searchParams.get("testId"))?.id
    ?? getInitialTestId(tests, reviews);

  return { testId, reviewId };
}

export function ReviewQueue({ reviews, tests, terminalReviewsNextCursor, onReviewsChanged }) {
  const { registerBeforeLogout } = useNavigationGuard();
  const { key: locationKey, search } = useLocation();
  const linkedSelection = getLinkedSelection(tests, reviews, search);
  const [selectedTestId, setSelectedTestId] = useState(linkedSelection.testId);
  const [selectedReviewId, setSelectedReviewId] = useState(linkedSelection.reviewId);
  const [reviewScrollRequest, setReviewScrollRequest] = useState(null);
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
  const appliedLocationRef = useRef(null);
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
      setDirtyManualTasks(new Set());
      setManualFormGeneration(current => current + 1);
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

  useEffect(() => {
    if (appliedLocationRef.current === locationKey) {
      return;
    }

    appliedLocationRef.current = locationKey;
    setSelectedTestId(linkedSelection.testId);
    setSelectedReviewId(linkedSelection.reviewId);
    setShowHistory(false);
    setReviewScrollRequest(linkedSelection.reviewId
      ? { reviewId: linkedSelection.reviewId }
      : null);
  }, [linkedSelection.reviewId, linkedSelection.testId, locationKey]);

  const testOptions = useMemo(() => createTestOptions(tests, [
    reviews,
    currentTerminalReviews,
    historicalTerminalReviews
  ]), [currentTerminalReviews, historicalTerminalReviews, reviews, tests]);

  useEffect(() => {
    if (testOptions.some(test => test.id === selectedTestId)) {
      return;
    }

    setSelectedTestId(testOptions[0]?.id ?? "");
    setSelectedReviewId("");
  }, [selectedTestId, testOptions]);

  const nonTerminalReviews = reviews.filter(review => !isTerminalReview(review));
  const reviewsForSelectedMode = mergeReviews(
    nonTerminalReviews,
    showHistory ? historicalTerminalReviews : currentTerminalReviews);
  const visibleReviews = selectedTestId
    ? reviewsForSelectedMode.filter(review => review.testId === selectedTestId)
    : [];
  const selectedReview = visibleReviews.find(
    review => String(review.id) === selectedReviewId) ?? null;
  const nextCursor = showHistory ? historicalNextCursor : currentNextCursor;

  useEffect(() => {
    if (!reviewScrollRequest || String(selectedReview?.id) !== reviewScrollRequest.reviewId) {
      return;
    }

    const frame = window.requestAnimationFrame(() => {
      const details = document.getElementById(`teacher-review-details-${selectedReview.id}`);
      const target = selectedReview.status === "manual-review"
        ? document.getElementById(`teacher-manual-review-${selectedReview.id}`) ?? details
        : details;
      const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
      target?.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth", block: "start" });
      setReviewScrollRequest(null);
    });

    return () => window.cancelAnimationFrame(frame);
  }, [reviewScrollRequest, selectedReview?.id, selectedReview?.status]);

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
    setReviewScrollRequest(null);
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

  function handleTestChange(nextTestId) {
    if (nextTestId === selectedTestId) {
      return;
    }

    if (!confirmDiscardManualReview()) {
      return;
    }

    setSelectedTestId(nextTestId);
    setSelectedReviewId("");
    setReviewScrollRequest(null);
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
    if (!confirmDiscardManualReview()) {
      return;
    }

    const nextReviewId = reviewId === selectedReview?.id ? "" : String(reviewId);
    setSelectedReviewId(nextReviewId);
    setReviewScrollRequest(nextReviewId ? { reviewId: nextReviewId } : null);
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
          <p>Выберите тест, следите за автоматической проверкой и выставляйте баллы там, где нужен преподаватель.</p>
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

      <div className="teacher-review-test-filter">
        <span id="teacher-review-test-label">Тест</span>
        {testOptions.length === 0 ? (
          <p className="muted teacher-review-test-empty">Нет тестов</p>
        ) : (
          <div
            className="teacher-review-test-options"
            role="group"
            aria-labelledby="teacher-review-test-label"
          >
            {testOptions.map(test => {
              const isSelected = test.id === selectedTestId;
              return (
                <button
                  type="button"
                  className={`teacher-review-test-option${isSelected ? " active" : ""}`}
                  key={test.id}
                  aria-pressed={isSelected}
                  disabled={isLoadingHistory}
                  onClick={() => handleTestChange(test.id)}
                >
                  {test.title}
                </button>
              );
            })}
          </div>
        )}
      </div>

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
        emptyMessage={selectedTestId
          ? "Для выбранного теста проверок в этом списке нет."
          : "Выберите тест, чтобы посмотреть проверки."}
        selectedReviewId={selectedReview?.id ?? ""}
        onSelectReview={handleSelectReview}
        selectedReviewDetails={selectedReview && (
          <ReviewDetailsPanel review={selectedReview}>
            {selectedReview.status === "manual-review" && (
              <ManualReviewPanel
                key={`${selectedReview.id}:${manualFormGeneration}`}
                review={selectedReview}
                onDirtyChange={handleManualTaskDirtyChange}
                onSaved={handleReviewsChanged}
              />
            )}
          </ReviewDetailsPanel>
        )}
      />
      {selectedReviewId && !selectedReview && (
        <p className="muted" role="status">
          Этой проверки нет в текущем списке. Попробуйте открыть всю историю.
        </p>
      )}
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
    </section>
  );
}

export default ReviewQueue;
