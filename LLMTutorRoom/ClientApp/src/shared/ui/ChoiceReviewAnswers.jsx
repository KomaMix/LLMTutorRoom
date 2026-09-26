import { formatReviewAnswer } from "../lib/answers.js";
import { AnswerOption } from "./AnswerOption.jsx";

export function ChoiceReviewAnswers({ result }) {
  const options = result.answerOptions ?? [];
  const selectedIds = new Set((result.studentAnswer ?? "")
    .split("|")
    .map(id => id.trim())
    .filter(Boolean));
  const isReviewed = result.status === "succeeded";
  const hasUnavailableSelection = [...selectedIds].some(id =>
    !options.some(option => option.id === id));

  if (options.length === 0) {
    return (
      <div className="review-choice-answers">
        <span className="review-choice-label">Ответ</span>
        <p className="review-choice-note">
          {formatReviewAnswer(result.studentAnswer, options) || "Ответ не указан."}
        </p>
      </div>
    );
  }

  return (
    <div className="review-choice-answers">
      <span className="review-choice-label">Варианты ответа</span>
      <ul className="answer-option-list review-choice-options" aria-label="Варианты ответа" role="list">
        {options.map(option => {
          const isSelected = selectedIds.has(option.id);
          const isCorrect = isReviewed && option.isCorrect === true;
          const isIncorrect = isReviewed && isSelected && option.isCorrect === false;
          const state = isCorrect ? "correct" : isIncorrect ? "incorrect" : isSelected ? "selected" : "";
          const accessibleLabel = isCorrect
            ? isSelected ? "Выбран правильный ответ. " : "Правильный ответ не выбран. "
            : isIncorrect ? "Выбран неверный ответ. " : isSelected ? "Выбранный ответ. " : "Не выбран. ";

          return (
            <AnswerOption
              key={option.id}
              text={option.text}
              state={state}
              showMarker={isSelected}
              accessibleLabel={accessibleLabel}
            />
          );
        })}
      </ul>
      {selectedIds.size === 0 && (
        <p className="review-choice-note">Ответ не указан.</p>
      )}
      {hasUnavailableSelection && (
        <p className="review-choice-note">Некоторые выбранные варианты больше недоступны.</p>
      )}
    </div>
  );
}
