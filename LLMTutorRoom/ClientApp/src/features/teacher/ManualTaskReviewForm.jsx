import { useEffect, useRef, useState } from "react";
import { Loader2, Save } from "lucide-react";
import { saveManualReview } from "../../api/classroomApi.js";

function createFormSnapshot(result) {
  return {
    score: String(result.score ?? 0),
    feedback: result.feedback ?? "",
    findings: (result.findings ?? []).join("\n")
  };
}

export function ManualTaskReviewForm({ reviewId, result, onDirtyChange, onSaved }) {
  const initialSnapshot = createFormSnapshot(result);
  const baselineRef = useRef(initialSnapshot);
  const dirtyKey = `${reviewId}:${result.taskId}`;
  const [score, setScore] = useState(initialSnapshot.score);
  const [feedback, setFeedback] = useState(initialSnapshot.feedback);
  const [findings, setFindings] = useState(initialSnapshot.findings);
  const [message, setMessage] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const isDirty = score !== baselineRef.current.score
    || feedback !== baselineRef.current.feedback
    || findings !== baselineRef.current.findings;

  useEffect(() => {
    onDirtyChange?.(dirtyKey, isDirty);
  }, [dirtyKey, isDirty, onDirtyChange]);

  useEffect(() => () => {
    onDirtyChange?.(dirtyKey, false);
  }, [dirtyKey, onDirtyChange]);

  async function handleSubmit(event) {
    event.preventDefault();
    const numericScore = Number(score);
    const maxScore = Number(result.maxScore);

    if (score === ""
      || !Number.isFinite(numericScore)
      || !Number.isFinite(maxScore)
      || numericScore < 0
      || numericScore > maxScore) {
      setMessage(`Укажите балл от 0 до ${result.maxScore}.`);
      return;
    }

    setIsSaving(true);
    setMessage("");

    try {
      await saveManualReview(reviewId, result.taskId, {
        score: numericScore,
        feedback,
        findings: findings
          .split("\n")
          .map(item => item.trim())
          .filter(Boolean)
      });

      baselineRef.current = { score, feedback, findings };
      setMessage("Оценка сохранена.");
      await onSaved?.();
    } catch (error) {
      setMessage("Не удалось сохранить оценку.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <form className="result-card manual-review-form teacher-manual-review-form" onSubmit={handleSubmit} aria-busy={isSaving}>
      <header className="teacher-manual-task-header">
        <strong>{result.taskTitle}</strong>
        <span>{result.maxScore} баллов</span>
      </header>
      <div className="manual-review-context">
        <div>
          <span>Задание</span>
          <p>{result.taskPrompt}</p>
        </div>
        <div>
          <span>Ответ ученика</span>
          <p>{result.studentAnswer?.trim() || "Ответ не указан."}</p>
        </div>
      </div>
      <div className="form-row">
        <div className="field">
          <label htmlFor={`manual-score-${reviewId}-${result.taskId}`}>Балл</label>
          <input
            id={`manual-score-${reviewId}-${result.taskId}`}
            min="0"
            max={result.maxScore}
            step="0.1"
            type="number"
            value={score}
            disabled={isSaving}
            onChange={event => setScore(event.target.value)}
            required
          />
        </div>
        <div className="field">
          <label htmlFor={`manual-feedback-${reviewId}-${result.taskId}`}>Комментарий</label>
          <input
            id={`manual-feedback-${reviewId}-${result.taskId}`}
            value={feedback}
            disabled={isSaving}
            onChange={event => setFeedback(event.target.value)}
          />
        </div>
      </div>
      <div className="field">
        <label htmlFor={`manual-findings-${reviewId}-${result.taskId}`}>Выводы</label>
        <textarea
          id={`manual-findings-${reviewId}-${result.taskId}`}
          className="compact-textarea"
          value={findings}
          disabled={isSaving}
          onChange={event => setFindings(event.target.value)}
        />
      </div>
      <button type="submit" className="button primary" disabled={isSaving}>
        {isSaving ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Save size={16} aria-hidden="true" />}
        {isSaving ? "Сохранение..." : "Сохранить оценку"}
      </button>
      {message && <p className="form-note" role="status">{message}</p>}
    </form>
  );
}

export default ManualTaskReviewForm;
