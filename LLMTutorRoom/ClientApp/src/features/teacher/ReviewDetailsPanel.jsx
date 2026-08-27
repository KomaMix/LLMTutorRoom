import { formatReviewAnswer } from "../../shared/lib/answers.js";
import { formatDate } from "../../shared/lib/dates.js";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";

const completedTaskStatuses = new Set(["succeeded", "failed"]);

const reviewStatusMessages = {
  checked: "Проверка завершена.",
  failed: "Не удалось завершить проверку работы.",
  "manual-review": "Часть заданий ожидает оценки преподавателя.",
  processing: "Проверка работы продолжается.",
  queued: "Работа ожидает начала проверки.",
  "retry-scheduled": "Для работы запланирована повторная проверка."
};

function getStudentName(review) {
  return review.studentName || review.studentUserId || "Студент";
}

function getReviewSummary(review) {
  return review.summary?.trim()
    || reviewStatusMessages[review.status]
    || "Результат проверки ещё не сформирован.";
}

function getFindings(result) {
  return [...new Set(
    result.findings
      .filter(finding => typeof finding === "string")
      .map(finding => finding.trim())
      .filter(Boolean))];
}

function ReviewTaskResult({ result, taskIndex }) {
  const isLlm = result.checkMode === "llm";
  const isManual = result.checkMode === "manual";
  const isFailed = result.status === "failed";
  const findings = getFindings(result);
  const studentAnswer = formatReviewAnswer(result.studentAnswer, result.answerOptions);
  const checkModeLabel = isLlm
    ? "Проверка LLM"
    : isManual ? "Ручная проверка" : "Автопроверка";
  const feedbackLabel = isLlm
    ? "Комментарий LLM"
    : isManual ? "Комментарий преподавателя" : "Комментарий проверки";
  const findingsLabel = isLlm
    ? "Выводы LLM"
    : isManual ? "Выводы преподавателя" : "Выводы проверки";

  return (
    <article className={`teacher-review-task-result ${result.checkMode}${isFailed ? " failed" : ""}`}>
      <header className="teacher-review-task-result-header">
        <span className="student-task-number teacher-review-task-number">{taskIndex + 1}</span>
        <div>
          <strong>{result.taskTitle}</strong>
          <span>{checkModeLabel}</span>
        </div>
        <div className="teacher-review-task-score">
          <strong>{isFailed ? "—" : result.score}</strong>
          <span>из {result.maxScore}</span>
        </div>
      </header>

      <div className="teacher-review-copy-block">
        <span>Задание</span>
        <p>{result.taskPrompt?.trim() || "Текст задания не указан."}</p>
      </div>

      <div className="teacher-review-copy-block answer">
        <span>Ответ ученика</span>
        <p>{studentAnswer || "Ответ не указан."}</p>
      </div>

      {isFailed ? (
        <div className="teacher-review-copy-block error">
          <span>Результат проверки</span>
          <p>Автоматическую проверку этого задания завершить не удалось.</p>
        </div>
      ) : (
        <>
          {result.feedback?.trim() && (
            <div className={`teacher-review-copy-block feedback${isLlm ? " llm" : ""}`}>
              <span>{feedbackLabel}</span>
              <p>{result.feedback.trim()}</p>
            </div>
          )}

          {findings.length > 0 && (
            <div className={`teacher-review-findings${isLlm ? " llm" : ""}`}>
              <span>{findingsLabel}</span>
              <ul>
                {findings.map((finding, index) => (
                  <li key={`${result.taskId}-${index}`}>{finding}</li>
                ))}
              </ul>
            </div>
          )}
        </>
      )}
    </article>
  );
}

export function ReviewDetailsPanel({ review }) {
  const completedResults = review.taskResults
    .map((result, taskIndex) => ({ result, taskIndex }))
    .filter(({ result }) => completedTaskStatuses.has(result.status));
  const hasFinalScore = review.status === "checked";

  return (
    <section
      className="teacher-review-details"
      id={`teacher-review-details-${review.id}`}
      aria-labelledby={`teacher-review-details-title-${review.id}`}
    >
      <header className="teacher-section-header teacher-review-details-header">
        <div>
          <span className="eyebrow">Подробности проверки</span>
          <h3 id={`teacher-review-details-title-${review.id}`}>{review.testTitle}</h3>
          <p>{getStudentName(review)} · версия {review.testRevision} · отправлено {formatDate(review.submittedAt)}</p>
        </div>
        <StatusBadge status={review.status} />
      </header>

      <div className="teacher-review-details-summary">
        <div className="teacher-review-total-score">
          <strong>{hasFinalScore ? review.score : "—"}</strong>
          <span>из {review.maxScore}</span>
        </div>
        <div>
          <span>{hasFinalScore ? "Итог проверки" : "Состояние проверки"}</span>
          <p>{getReviewSummary(review)}</p>
        </div>
      </div>

      <div className="teacher-review-completed-results">
        <div className="teacher-review-results-heading">
          <h4>Завершённые задания</h4>
          <span>{completedResults.length}</span>
        </div>
        {completedResults.length === 0 ? (
          <p className="muted teacher-review-results-empty">
            Завершённых заданий пока нет.
          </p>
        ) : (
          <div className="teacher-review-task-results">
            {completedResults.map(({ result, taskIndex }) => (
              <ReviewTaskResult
                key={result.id ?? result.taskId}
                result={result}
                taskIndex={taskIndex}
              />
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

export default ReviewDetailsPanel;
