import { useEffect, useState } from "react";
import {
  CheckCircle2,
  ChevronDown,
  Clock3,
  FileCheck2,
  GraduationCap,
  Hourglass,
  Loader2,
  Play,
  Save,
  Send
} from "lucide-react";
import { Link } from "react-router-dom";
import { formatDate, formatDuration } from "../../shared/lib/dates.js";
import { formatPoints } from "../../shared/lib/points.js";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";
import {
  getAttemptStatusText,
  getEffectiveAttemptStatus,
  getReviewStatusMessage
} from "./attemptUtils.js";

function SubmissionSummary({ attempt, attemptStatus, isReviewDeferred, review }) {
  if (attemptStatus === "expired") {
    return (
      <section className="student-submission-summary expired">
        <span className="student-submission-icon">
          <Clock3 size={21} aria-hidden="true" />
        </span>
        <div>
          <strong>Время выполнения истекло</strong>
          <p>Ответы доступны только для просмотра.</p>
        </div>
      </section>
    );
  }

  if (review?.status === "checked") {
    return (
      <section className="student-submission-summary ready">
        <span className="student-submission-icon">
          <CheckCircle2 size={21} aria-hidden="true" />
        </span>
        <div>
          <span>Результат готов</span>
          <strong>{formatPoints(review.score)} из {review.maxScore}</strong>
        </div>
        <Link className="button secondary" to={`/student/results#attempt-${attempt.id}`}>
          Открыть результат
        </Link>
      </section>
    );
  }

  if (!review && isReviewDeferred) {
    return (
      <section className="student-submission-summary pending">
        <span className="student-submission-icon">
          <FileCheck2 size={21} aria-hidden="true" />
        </span>
        <div>
          <span>Ответы отправлены {formatDate(attempt.submittedAt)}</span>
          <strong>Состояние этой работы доступно в истории результатов.</strong>
        </div>
        <Link className="button secondary" to={`/student/results#attempt-${attempt.id}`}>
          Открыть историю
        </Link>
      </section>
    );
  }

  const reviewStatus = review?.status ?? "queued";
  return (
    <section className="student-submission-summary pending">
      <span className="student-submission-icon">
        <Hourglass size={21} aria-hidden="true" />
      </span>
      <div>
        <span>Ответы отправлены {formatDate(attempt.submittedAt ?? attempt.endsAt)}</span>
        <strong>{getReviewStatusMessage(reviewStatus)}</strong>
      </div>
      {review && <StatusBadge status={review.status} />}
    </section>
  );
}

export function StudentWorkspace({
  tests,
  selectedTest,
  selectedTestId,
  selectedAttempt,
  selectedReview,
  isReviewDeferred,
  remainingSeconds,
  answers,
  message,
  isStartingAttempt,
  isSavingAttempt,
  isSubmittingAttempt,
  onSelectTest,
  onAnswerChange,
  onStartAttempt,
  onSaveAnswers,
  onSubmitAttempt
}) {
  const [deadlineNow, setDeadlineNow] = useState(Date.now());
  const effectiveAttemptStatus = getEffectiveAttemptStatus(selectedAttempt, remainingSeconds);
  const canEditAnswers = effectiveAttemptStatus === "in-progress"
    && remainingSeconds > 0
    && !isSubmittingAttempt;
  const isReadOnly = Boolean(selectedAttempt) && effectiveAttemptStatus !== "in-progress";
  const deadline = new Date(selectedTest.deadline).getTime();
  const isTestExpired = !selectedAttempt
    && Number.isFinite(deadline)
    && deadline <= deadlineNow;

  useEffect(() => {
    const selectedDeadline = new Date(selectedTest.deadline).getTime();
    if (!Number.isFinite(selectedDeadline)) {
      return;
    }

    let timer = null;
    const maximumTimeout = 2_147_000_000;

    function updateDeadline() {
      const currentTime = Date.now();
      setDeadlineNow(currentTime);

      if (selectedDeadline > currentTime) {
        timer = window.setTimeout(
          updateDeadline,
          Math.min(maximumTimeout, selectedDeadline - currentTime + 25));
      }
    }

    updateDeadline();
    return () => {
      if (timer !== null) {
        window.clearTimeout(timer);
      }
    };
  }, [selectedTest.deadline, selectedTest.id]);

  function toggleMultipleChoiceOption(taskId, optionId, isChecked) {
    const selectedOptionIds = (answers[taskId] ?? "")
      .split("|")
      .filter(Boolean);
    const nextOptionIds = isChecked
      ? [...new Set([...selectedOptionIds, optionId])]
      : selectedOptionIds.filter(selectedOptionId => selectedOptionId !== optionId);

    onAnswerChange(taskId, nextOptionIds.join("|"));
  }

  return (
    <section className="student-layout">
      <aside className="list-panel student-test-list-panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Доступно</span>
            <h2>Тесты</h2>
          </div>
          <GraduationCap size={18} aria-hidden="true" />
        </div>

        <div className="select-list">
          {tests.map(test => (
            <button
              key={test.id}
              type="button"
              className={test.id === selectedTestId ? "active" : ""}
              aria-current={test.id === selectedTestId ? "page" : undefined}
              disabled={isStartingAttempt || isSavingAttempt || isSubmittingAttempt}
              onClick={() => onSelectTest(test.id)}
            >
              <span>{test.title}</span>
              <small>{formatPoints(test.totalPoints)} · {test.timeLimitMinutes} мин</small>
            </button>
          ))}
        </div>
      </aside>

      <div className="detail-panel student-workspace-panel">
        <header className="student-test-header">
          <div>
            <span className="eyebrow">{selectedTest.subject}</span>
            <h2>{selectedTest.title}</h2>
          </div>
          <StatusBadge
            status={effectiveAttemptStatus ?? (isTestExpired ? "expired" : selectedTest.status)}
          />
        </header>

        {selectedTest.summary && <p className="muted student-test-summary">{selectedTest.summary}</p>}
        <div className="student-test-facts" aria-label="Параметры теста">
          <span><Clock3 size={15} aria-hidden="true" />{selectedTest.timeLimitMinutes} мин</span>
          <span><FileCheck2 size={15} aria-hidden="true" />{formatPoints(selectedTest.totalPoints)}</span>
        </div>

        <section className={`attempt-panel ${effectiveAttemptStatus ?? "not-started"}`}>
          <div>
            <span>{selectedAttempt ? "Состояние попытки" : "Тест не начат"}</span>
            <strong>{selectedAttempt
              ? getAttemptStatusText(effectiveAttemptStatus)
              : isTestExpired
                ? "Время вышло"
                : "Можно приступать"}</strong>
          </div>
          {effectiveAttemptStatus === "in-progress" && (
            <div className="attempt-time">
              <span>Осталось</span>
              <strong>{formatDuration(remainingSeconds)}</strong>
            </div>
          )}
          {effectiveAttemptStatus === "submitted" && (
            <div className="attempt-time">
              <span>Отправлено</span>
              <strong>{formatDate(selectedAttempt.submittedAt)}</strong>
            </div>
          )}
          {effectiveAttemptStatus === "expired" && selectedAttempt && (
            <div className="attempt-time">
              <span>Завершено</span>
              <strong>{formatDate(selectedAttempt.endsAt)}</strong>
            </div>
          )}
          {!selectedAttempt && (
            <button
              type="button"
              className="button primary"
              onClick={onStartAttempt}
              disabled={isStartingAttempt || isTestExpired}
            >
              {isStartingAttempt
                ? <Loader2 className="spin" size={16} aria-hidden="true" />
                : isTestExpired
                  ? <Clock3 size={16} aria-hidden="true" />
                  : <Play size={16} aria-hidden="true" />}
              {isStartingAttempt ? "Запуск..." : isTestExpired ? "Время вышло" : "Начать тест"}
            </button>
          )}
        </section>

        <div className="student-attempt-message" role="status" aria-atomic="true" aria-live="polite">
          {message && <p className="form-note">{message}</p>}
        </div>

        {!selectedAttempt && (
          <section className="empty-state compact-empty-state student-empty-attempt">
            <Clock3 size={24} aria-hidden="true" />
            <h2>{isTestExpired
              ? "Срок выполнения теста истёк"
              : "Начните тест, чтобы открыть задания"}</h2>
          </section>
        )}

        {selectedAttempt && (
          <>
            <div className="student-section-heading">
              <h3>{isReadOnly ? "Ваши ответы" : "Текст"}</h3>
              {isReadOnly && <span className="student-readonly-label">Только просмотр</span>}
            </div>

            <div className="answer-stack">
              {selectedTest.tasks.map((task, taskIndex) => {
                const selectedOptionIds = (answers[task.id] ?? "").split("|").filter(Boolean);
                const answerField = task.type === "free-text" ? (
                  canEditAnswers ? (
                    <textarea
                      value={answers[task.id] ?? ""}
                      onChange={event => onAnswerChange(task.id, event.target.value)}
                      placeholder="Введите решение..."
                      rows={6}
                    />
                  ) : (
                    <div className="student-written-answer">
                      {answers[task.id] || "Ответ не указан."}
                    </div>
                  )
                ) : (
                  <div className="student-option-list">
                    {task.options.map(option => {
                      const isSelected = selectedOptionIds.includes(option.id);
                      return (
                        <label
                          className={`student-option${isSelected ? " selected" : ""}${isReadOnly ? " readonly" : ""}`}
                          key={option.id}
                        >
                          <input
                            checked={isSelected}
                            disabled={!canEditAnswers}
                            name={`student-answer-${task.id}`}
                            type={task.type === "single-choice" ? "radio" : "checkbox"}
                            onChange={event => {
                              if (task.type === "single-choice") {
                                onAnswerChange(task.id, option.id);
                                return;
                              }

                              toggleMultipleChoiceOption(
                                task.id,
                                option.id,
                                event.target.checked);
                            }}
                          />
                          <span>{option.text}</span>
                        </label>
                      );
                    })}
                  </div>
                );

                return (
                  <article className={`answer-card${isReadOnly ? " readonly" : ""}`} key={task.id}>
                    <header className="student-answer-card-header">
                      <div className="task-heading">
                        <h4>Задание {taskIndex + 1}</h4>
                        <span>{task.title}</span>
                      </div>
                      <span className="task-points student-task-points">{formatPoints(task.maxPoints)}</span>
                    </header>
                    <p className="student-task-prompt">{task.prompt}</p>
                    {isReadOnly ? (
                      <details className="student-answer-disclosure">
                        <summary>
                          <span className="student-answer-toggle-show">Показать ответ</span>
                          <span className="student-answer-toggle-hide">Скрыть ответ</span>
                          <ChevronDown size={17} aria-hidden="true" />
                        </summary>
                        <div className="student-answer-disclosure-content">
                          <span className="student-answer-label">Ваш ответ</span>
                          {answerField}
                        </div>
                      </details>
                    ) : (
                      <>
                        <span className="student-answer-label">Ваш ответ</span>
                        {answerField}
                      </>
                    )}
                  </article>
                );
              })}
            </div>

            {effectiveAttemptStatus === "in-progress" && (
              <div className="attempt-action-bar">
                <span>Изменения автоматически сохраняются во время работы.</span>
                <div className="attempt-actions">
                  <button
                    type="button"
                    className="button secondary"
                    onClick={onSaveAnswers}
                    disabled={!canEditAnswers || isSavingAttempt || isSubmittingAttempt}
                  >
                    {isSavingAttempt
                      ? <Loader2 className="spin" size={16} aria-hidden="true" />
                      : <Save size={16} aria-hidden="true" />}
                    {isSavingAttempt ? "Сохранение..." : "Сохранить ответы"}
                  </button>
                  <button
                    type="button"
                    className="button primary"
                    onClick={onSubmitAttempt}
                    disabled={!canEditAnswers || isSavingAttempt || isSubmittingAttempt}
                  >
                    {isSubmittingAttempt
                      ? <Loader2 className="spin" size={16} aria-hidden="true" />
                      : <Send size={16} aria-hidden="true" />}
                    {isSubmittingAttempt ? "Завершение..." : "Завершить тест"}
                  </button>
                </div>
              </div>
            )}

            {effectiveAttemptStatus !== "in-progress" && (
              <SubmissionSummary
                attempt={selectedAttempt}
                attemptStatus={effectiveAttemptStatus}
                isReviewDeferred={isReviewDeferred}
                review={selectedReview}
              />
            )}
          </>
        )}
      </div>
    </section>
  );
}

export default StudentWorkspace;
