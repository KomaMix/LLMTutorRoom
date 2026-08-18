import { useEffect, useRef, useState } from "react";
import { CheckCircle2, Loader2 } from "lucide-react";
import { getReviewHistory } from "../../api/classroomApi.js";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";
import { formatDate } from "../../shared/lib/dates.js";
import { parseReviewHistoryPage } from "../../shared/lib/overview.js";
import { isTerminalReview, mergeReviews } from "../../shared/lib/reviews.js";
import {
  getEffectiveAttemptStatus,
  getReviewStatusMessage
} from "./attemptUtils.js";

export function StudentResults({ attempts, reviews, tests, terminalReviewsNextCursor }) {
  const [currentTime, setCurrentTime] = useState(Date.now());
  const [terminalReviews, setTerminalReviews] = useState(() =>
    reviews.filter(isTerminalReview));
  const [nextCursor, setNextCursor] = useState(terminalReviewsNextCursor ?? null);
  const [isLoadingHistory, setIsLoadingHistory] = useState(false);
  const [historyError, setHistoryError] = useState("");
  const loadedAdditionalHistoryRef = useRef(false);
  const completedAttempts = attempts.filter(attempt =>
    attempt.status !== "in-progress" || new Date(attempt.endsAt).getTime() <= currentTime);
  const visibleReviews = mergeReviews(
    reviews.filter(review => !isTerminalReview(review)),
    terminalReviews);

  useEffect(() => {
    setTerminalReviews(current => mergeReviews(
      current,
      reviews.filter(isTerminalReview)));
    if (!loadedAdditionalHistoryRef.current) {
      setNextCursor(terminalReviewsNextCursor ?? null);
    }
  }, [reviews, terminalReviewsNextCursor]);

  useEffect(() => {
    const nextDeadline = attempts
      .filter(attempt => attempt.status === "in-progress")
      .map(attempt => new Date(attempt.endsAt).getTime())
      .filter(deadline => Number.isFinite(deadline) && deadline > currentTime)
      .sort((left, right) => left - right)[0];

    if (!nextDeadline) {
      return;
    }

    const maximumTimeout = 2_147_000_000;
    const delay = Math.min(
      maximumTimeout,
      Math.max(25, nextDeadline - Date.now() + 25));
    const timer = window.setTimeout(() => setCurrentTime(Date.now()), delay);
    return () => window.clearTimeout(timer);
  }, [attempts, currentTime]);

  async function handleLoadMore() {
    if (!nextCursor || isLoadingHistory) {
      return;
    }

    setIsLoadingHistory(true);
    setHistoryError("");
    try {
      const page = parseReviewHistoryPage(await getReviewHistory({ cursor: nextCursor }));
      loadedAdditionalHistoryRef.current = true;
      setTerminalReviews(current => mergeReviews(current, page.reviews));
      setNextCursor(page.nextCursor ?? null);
    } catch (error) {
      if (error?.name !== "AbortError") {
        setHistoryError("Не удалось загрузить историю проверок.");
      }
    } finally {
      setIsLoadingHistory(false);
    }
  }

  if (completedAttempts.length === 0 && visibleReviews.length === 0) {
    return (
      <section className="panel empty-state">
        <CheckCircle2 size={28} aria-hidden="true" />
        <h2>Результатов пока нет</h2>
      </section>
    );
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">История</span>
          <h2>Результаты</h2>
        </div>
        <CheckCircle2 size={18} aria-hidden="true" />
      </div>

      {completedAttempts.length > 0 && (
        <div className="result-list">
          {completedAttempts.map(attempt => {
            const test = tests.find(item => item.id === attempt.testId);
            const attemptStatus = getEffectiveAttemptStatus(
              attempt,
              Math.ceil((new Date(attempt.endsAt).getTime() - currentTime) / 1000));
            return (
              <article className="result-card" key={attempt.id}>
                <div>
                  <strong>{test?.title ?? "Тест"}</strong>
                  <StatusBadge status={attemptStatus} />
                </div>
                <p>{attemptStatus === "submitted"
                  ? "Ответы отправлены. Результаты станут доступны позже."
                  : "Время выполнения истекло. Ответы больше нельзя изменить."}</p>
                <div className="result-meta">
                  <span>Начало: {formatDate(attempt.startedAt)}</span>
                  <span>Окончание: {formatDate(attempt.submittedAt ?? attempt.endsAt)}</span>
                </div>
              </article>
            );
          })}
        </div>
      )}

      {visibleReviews.length > 0 && (
        <>
          <h3>Проверки</h3>
          <div className="result-list">
            {visibleReviews.map(review => (
              <article className="result-card" key={review.id}>
                <div>
                  <strong>{review.testTitle}</strong>
                  <StatusBadge status={review.status} />
                </div>
                <p>{review.status === "checked" ? review.summary : getReviewStatusMessage(review.status)}</p>
                {review.status === "checked" && (
                  <>
                    <div className="result-meta">
                      <span>{review.score}/{review.maxScore}</span>
                    </div>
                    <ul>
                      {review.taskResults.flatMap(result =>
                        result.findings.map((finding, index) => (
                          <li key={`${result.taskId}-${index}`}>{finding}</li>
                        )))}
                    </ul>
                  </>
                )}
              </article>
            ))}
          </div>
        </>
      )}
      {nextCursor && (
        <button
          type="button"
          className="button secondary"
          disabled={isLoadingHistory}
          onClick={handleLoadMore}
        >
          {isLoadingHistory && <Loader2 className="spin" size={16} aria-hidden="true" />}
          {isLoadingHistory ? "Загрузка..." : "Загрузить ещё проверки"}
        </button>
      )}
      {historyError && <p className="form-note error" role="alert">{historyError}</p>}
    </section>
  );
}

export default StudentResults;
