import { useEffect, useRef, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Hourglass,
  Loader2,
  Send
} from "lucide-react";
import { useLocation } from "react-router-dom";
import { getReviewHistory } from "../../api/classroomApi.js";
import { formatDate } from "../../shared/lib/dates.js";
import { parseReviewHistoryPage } from "../../shared/lib/overview.js";
import { isTerminalReview, mergeReviews } from "../../shared/lib/reviews.js";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";
import {
  getEffectiveAttemptStatus,
  getReviewStatusMessage
} from "./attemptUtils.js";

function getEntryTime(entry) {
  const value = entry.review?.submittedAt
    ?? entry.attempt?.submittedAt
    ?? entry.attempt?.endsAt
    ?? entry.attempt?.startedAt;
  const timestamp = new Date(value).getTime();
  return Number.isFinite(timestamp) ? timestamp : 0;
}

function ReviewProgress({ attemptStatus, isReviewDeferred, review }) {
  if (attemptStatus === "expired" && !review) {
    return (
      <div className="student-result-state expired">
        <Clock3 size={20} aria-hidden="true" />
        <div>
          <strong>Время выполнения истекло</strong>
          <p>Ответы больше нельзя изменить.</p>
        </div>
      </div>
    );
  }

  if (isReviewDeferred) {
    return (
      <div className="student-result-state deferred">
        <Clock3 size={20} aria-hidden="true" />
        <div>
          <strong>Результат находится дальше в истории</strong>
          <p>Загрузите следующие записи, чтобы открыть подробности этой проверки.</p>
        </div>
      </div>
    );
  }

  const isFailed = review?.status === "failed";
  const Icon = isFailed ? AlertTriangle : Hourglass;
  return (
    <div className={`student-result-state${isFailed ? " failed" : ""}`}>
      <Icon size={20} aria-hidden="true" />
      <div>
        <strong>{isFailed ? "Проверка требует внимания" : "Проверка выполняется"}</strong>
        <p>{review
          ? getReviewStatusMessage(review.status)
          : "Ответы приняты. Результат появится после завершения проверки."}</p>
      </div>
    </div>
  );
}

function ReviewTaskDetails({ result, taskIndex }) {
  const findings = [...new Set(
    result.findings
      .filter(finding => typeof finding === "string")
      .map(finding => finding.trim())
      .filter(Boolean))];

  return (
    <section className="student-result-task">
      <header>
        <span className="student-task-number">{taskIndex + 1}</span>
        <div>
          <strong>{result.taskTitle}</strong>
          <span>Задание {taskIndex + 1}</span>
        </div>
        <strong className="student-result-task-score">
          {result.score}/{result.maxScore}
        </strong>
      </header>

      <div className="student-result-copy-block">
        <span>Ваш ответ</span>
        <p>{result.studentAnswer || "Ответ не указан."}</p>
      </div>

      {result.feedback && (
        <div className="student-result-copy-block feedback">
          <span>Комментарий к ответу</span>
          <p>{result.feedback}</p>
        </div>
      )}

      {findings.length > 0 && (
        <details className="student-result-findings">
          <summary>Рекомендации по ответу · {findings.length}</summary>
          <ul>
            {findings.map((finding, index) => (
              <li key={`${result.taskId}-${index}`}>{finding}</li>
            ))}
          </ul>
        </details>
      )}
    </section>
  );
}

function StudentResultCard({ attempt, currentTime, isReviewDeferred, review, test }) {
  const remainingSeconds = attempt
    ? Math.ceil((new Date(attempt.endsAt).getTime() - currentTime) / 1000)
    : 0;
  const attemptStatus = attempt
    ? getEffectiveAttemptStatus(attempt, remainingSeconds)
    : null;
  const displayStatus = review?.status ?? attemptStatus;
  const isChecked = review?.status === "checked";
  const title = test?.title ?? review?.testTitle ?? "Тест";

  return (
    <article
      className={`student-result-card${isChecked ? " checked" : ""}`}
      id={attempt ? `attempt-${attempt.id}` : `review-${review.id}`}
    >
      <header className="student-result-header">
        <div>
          <span className="eyebrow">{test?.subject ?? "Тест"}</span>
          <h3>{title}</h3>
        </div>
        {displayStatus && <StatusBadge status={displayStatus} />}
      </header>

      {isChecked ? (
        <div className="student-result-summary">
          <div className="student-result-score">
            <strong>{review.score}</strong>
            <span>из {review.maxScore}</span>
          </div>
          <div>
            <span>Итог проверки</span>
            <p>{review.summary || "Проверка завершена."}</p>
          </div>
        </div>
      ) : (
        <ReviewProgress
          attemptStatus={attemptStatus}
          isReviewDeferred={isReviewDeferred}
          review={review}
        />
      )}

      <div className="student-result-meta">
        {attempt?.startedAt && (
          <span><Clock3 size={15} aria-hidden="true" />Начат {formatDate(attempt.startedAt)}</span>
        )}
        {(attempt?.submittedAt ?? review?.submittedAt) && (
          <span>
            <Send size={15} aria-hidden="true" />
            Отправлен {formatDate(attempt?.submittedAt ?? review.submittedAt)}
          </span>
        )}
        {review?.completedAt && (
          <span>
            <CheckCircle2 size={15} aria-hidden="true" />
            Проверен {formatDate(review.completedAt)}
          </span>
        )}
        {attemptStatus === "expired" && !attempt?.submittedAt && (
          <span>
            <Clock3 size={15} aria-hidden="true" />
            Завершён {formatDate(attempt.endsAt)}
          </span>
        )}
      </div>

      {isChecked && review.taskResults.length > 0 && (
        <div className="student-result-task-list">
          {review.taskResults.map((result, taskIndex) => (
            <ReviewTaskDetails
              key={result.id ?? result.taskId}
              result={result}
              taskIndex={taskIndex}
            />
          ))}
        </div>
      )}
    </article>
  );
}

export function StudentResults({ attempts, reviews, tests, terminalReviewsNextCursor }) {
  const { hash } = useLocation();
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
  const reviewByAttemptId = new Map(
    visibleReviews.map(review => [review.attemptId, review]));
  const terminalReviewTimes = terminalReviews
    .map(review => new Date(review.submittedAt).getTime())
    .filter(Number.isFinite);
  const oldestLoadedTerminalTime = terminalReviewTimes.length > 0
    ? Math.min(...terminalReviewTimes)
    : null;
  const completedAttemptIds = new Set(completedAttempts.map(attempt => attempt.id));
  const entries = [
    ...completedAttempts.map(attempt => ({
      attempt,
      review: reviewByAttemptId.get(attempt.id) ?? null,
      test: tests.find(test => test.id === attempt.testId) ?? null,
      isReviewDeferred: attempt.status === "submitted"
        && !reviewByAttemptId.has(attempt.id)
        && Boolean(nextCursor)
        && oldestLoadedTerminalTime != null
        && new Date(attempt.submittedAt).getTime() <= oldestLoadedTerminalTime
        && new Date(attempt.submittedAt).getTime() < currentTime - 2 * 60 * 1000
    })),
    ...visibleReviews
      .filter(review => !completedAttemptIds.has(review.attemptId))
      .map(review => ({
        attempt: null,
        review,
        test: tests.find(test => test.id === review.testId) ?? null,
        isReviewDeferred: false
      }))
  ].sort((left, right) => getEntryTime(right) - getEntryTime(left));

  useEffect(() => {
    if (!hash) {
      return;
    }

    const target = document.getElementById(decodeURIComponent(hash.slice(1)));
    if (target) {
      target.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }, [hash, entries.length, terminalReviews.length]);

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

  if (entries.length === 0) {
    return (
      <section className="panel empty-state">
        <CheckCircle2 size={28} aria-hidden="true" />
        <h2>Результатов пока нет</h2>
      </section>
    );
  }

  return (
    <section className="panel student-results-page">
      <header className="student-results-heading">
        <div>
          <span className="eyebrow">История обучения</span>
          <h2>Результаты тестов</h2>
          <p>Здесь собраны завершённые попытки и подробные комментарии к ответам.</p>
        </div>
        <span className="student-result-count">{entries.length}</span>
      </header>

      <div className="student-results-list">
        {entries.map(entry => (
          <StudentResultCard
            key={entry.attempt ? `attempt-${entry.attempt.id}` : `review-${entry.review.id}`}
            attempt={entry.attempt}
            currentTime={currentTime}
            isReviewDeferred={entry.isReviewDeferred}
            review={entry.review}
            test={entry.test}
          />
        ))}
      </div>

      {nextCursor && (
        <button
          type="button"
          className="button secondary student-history-load"
          disabled={isLoadingHistory}
          onClick={handleLoadMore}
        >
          {isLoadingHistory && <Loader2 className="spin" size={16} aria-hidden="true" />}
          {isLoadingHistory ? "Загрузка..." : "Загрузить ещё"}
        </button>
      )}
      {historyError && <p className="form-note error" role="alert">{historyError}</p>}
    </section>
  );
}

export default StudentResults;
