import { useEffect, useState } from "react";
import {
  Clock3,
  GraduationCap,
  Loader2,
  Play,
  Save,
  Send
} from "lucide-react";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";
import { formatDuration } from "../../shared/lib/dates.js";
import {
  getAttemptStatusText,
  getEffectiveAttemptStatus
} from "./attemptUtils.js";

export function StudentWorkspace({
  tests,
  selectedTest,
  selectedTestId,
  selectedAttempt,
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
      <div className="list-panel">
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
              <small>{test.totalPoints} баллов · {test.timeLimitMinutes} мин</small>
            </button>
          ))}
        </div>
      </div>

      <div className="detail-panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">{selectedTest.subject}</span>
            <h2>{selectedTest.title}</h2>
          </div>
          <StatusBadge status={effectiveAttemptStatus ?? (isTestExpired ? "expired" : selectedTest.status)} />
        </div>

        <p className="muted">{selectedTest.summary}</p>

        <div className="attempt-panel">
          <div>
            <span>{selectedAttempt ? "Состояние" : "Тест не начат"}</span>
            <strong>{selectedAttempt
              ? getAttemptStatusText(effectiveAttemptStatus)
              : isTestExpired
                ? "Время вышло"
                : `${selectedTest.timeLimitMinutes} мин`}</strong>
          </div>
          {selectedAttempt && (
            <div>
              <span>Осталось</span>
              <strong>{formatDuration(remainingSeconds)}</strong>
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
        </div>

        <div role="status" aria-atomic="true" aria-live="polite">
          {message && <p className="form-note">{message}</p>}
        </div>

        {!selectedAttempt && (
          <section className="empty-state compact-empty-state">
            <Clock3 size={24} aria-hidden="true" />
            <h2>{isTestExpired
              ? "Срок выполнения теста истёк"
              : "Начни тест, чтобы открыть ответы"}</h2>
          </section>
        )}

        {selectedAttempt && (
          <>
            <div className="answer-stack">
              {selectedTest.tasks.map(task => (
                <article className="answer-card" key={task.id}>
                  <div>
                    <strong>{task.title}</strong>
                    <span>{task.maxPoints} баллов</span>
                  </div>
                  <p>{task.prompt}</p>
                  {task.type === "free-text" ? (
                    <textarea
                      value={answers[task.id] ?? ""}
                      disabled={!canEditAnswers}
                      onChange={event => onAnswerChange(task.id, event.target.value)}
                      placeholder="Введите решение..."
                      rows={7}
                    />
                  ) : (
                    <div className="student-option-list">
                      {task.options.map(option => (
                        <label className="student-option" key={option.id}>
                          <input
                            checked={(answers[task.id] ?? "").split("|").includes(option.id)}
                            disabled={!canEditAnswers}
                            name={`student-answer-${task.id}`}
                            type={task.type === "single-choice" ? "radio" : "checkbox"}
                            onChange={event => {
                              if (task.type === "single-choice") {
                                onAnswerChange(task.id, option.id);
                                return;
                              }

                              toggleMultipleChoiceOption(task.id, option.id, event.target.checked);
                            }}
                          />
                          <span>{option.text}</span>
                        </label>
                      ))}
                    </div>
                  )}
                </article>
              ))}
            </div>

            <div className="attempt-actions">
              <button
                type="button"
                className="button secondary"
                onClick={onSaveAnswers}
                disabled={!canEditAnswers || isSavingAttempt || isSubmittingAttempt}
              >
                {isSavingAttempt ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Save size={16} aria-hidden="true" />}
                {isSavingAttempt ? "Сохранение..." : "Сохранить ответы"}
              </button>
              <button
                type="button"
                className="button primary"
                onClick={onSubmitAttempt}
                disabled={!canEditAnswers || isSavingAttempt || isSubmittingAttempt}
              >
                {isSubmittingAttempt ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Send size={16} aria-hidden="true" />}
                {isSubmittingAttempt ? "Завершение..." : "Завершить тест"}
              </button>
            </div>

            {effectiveAttemptStatus === "expired" && (
              <p className="form-note">Время выполнения истекло. Ответы заблокированы.</p>
            )}
            {effectiveAttemptStatus === "submitted" && (
              <p className="form-note">Ответы отправлены. Результаты станут доступны позже.</p>
            )}
          </>
        )}
      </div>
    </section>
  );
}

export default StudentWorkspace;
