import { useId } from "react";
import { Plus, Trash2 } from "lucide-react";
import { createGradingExample, gradingExampleLimits } from "./gradingExamples.js";

export function GradingExamplesEditor({
  examples = [],
  onChange,
  scope = "task",
  defaultMaxScore = 1,
  disabled = false
}) {
  const idPrefix = useId();
  const isTest = scope === "test";

  function updateExample(index, field, value) {
    onChange(examples.map((example, exampleIndex) => exampleIndex === index
      ? { ...example, [field]: value }
      : example));
  }

  return (
    <section className="option-editor grading-examples" aria-labelledby={`${idPrefix}-title`}>
      <div className="panel-header">
        <h3 id={`${idPrefix}-title`}>{isTest ? "Общие примеры оценивания" : "Примеры оценивания задания"}</h3>
        <button
          type="button"
          className="button secondary"
          disabled={disabled || examples.length >= gradingExampleLimits.count}
          onClick={() => onChange([...examples, createGradingExample(defaultMaxScore)])}
        >
          <Plus size={16} aria-hidden="true" />
          Добавить пример
        </button>
      </div>
      <p className="muted">
        {isTest
          ? "Эти примеры будут учитываться при каждой LLM-проверке в тесте вместе с примерами текущего задания."
          : "При LLM-проверке эти примеры дополнят общие примеры теста. Условие берётся из текущего задания."}
        {" "}Примеры видны только преподавателю. Можно добавить до {gradingExampleLimits.count}.
      </p>
      <div className="answer-stack">
        {examples.map((example, index) => {
          const exampleId = `${idPrefix}-${index}`;
          return (
            <article className="answer-card grading-example-card" key={index}>
              <div className="panel-header">
                <h4>Пример {index + 1}</h4>
                <button
                  type="button"
                  className="icon-button danger"
                  aria-label={`Удалить пример ${index + 1}`}
                  title="Удалить пример"
                  disabled={disabled}
                  onClick={() => onChange(examples.filter((_, exampleIndex) => exampleIndex !== index))}
                >
                  <Trash2 size={16} aria-hidden="true" />
                </button>
              </div>
              {isTest && (
                <div className="field">
                  <label htmlFor={`${exampleId}-prompt`}>Условие задания в примере</label>
                  <textarea
                    id={`${exampleId}-prompt`}
                    className="compact-textarea"
                    value={example.taskPrompt}
                    maxLength={gradingExampleLimits.taskPrompt}
                    disabled={disabled}
                    onChange={event => updateExample(index, "taskPrompt", event.target.value)}
                    required
                  />
                </div>
              )}
              <div className="field">
                <label htmlFor={`${exampleId}-answer`}>Пример ответа ученика</label>
                <textarea
                  id={`${exampleId}-answer`}
                  className="compact-textarea"
                  value={example.studentAnswer}
                  maxLength={gradingExampleLimits.studentAnswer}
                  disabled={disabled}
                  onChange={event => updateExample(index, "studentAnswer", event.target.value)}
                  required
                />
              </div>
              <div className="form-row">
                <div className="field">
                  <label htmlFor={`${exampleId}-score`}>Оценка преподавателя</label>
                  <input
                    id={`${exampleId}-score`}
                    type="number"
                    min="0"
                    max={example.maxScore}
                    step="any"
                    value={example.score}
                    disabled={disabled}
                    onChange={event => updateExample(index, "score", event.target.value)}
                    required
                  />
                </div>
                <div className="field">
                  <label htmlFor={`${exampleId}-max-score`}>Максимальный балл в примере</label>
                  <input
                    id={`${exampleId}-max-score`}
                    type="number"
                    min="0"
                    step="any"
                    value={example.maxScore}
                    disabled={disabled}
                    onChange={event => updateExample(index, "maxScore", event.target.value)}
                    required
                  />
                </div>
              </div>
              <div className="field">
                <label htmlFor={`${exampleId}-feedback`}>За что начислены и сняты баллы</label>
                <textarea
                  id={`${exampleId}-feedback`}
                  className="compact-textarea"
                  value={example.feedback}
                  maxLength={gradingExampleLimits.feedback}
                  disabled={disabled}
                  onChange={event => updateExample(index, "feedback", event.target.value)}
                  required
                />
              </div>
            </article>
          );
        })}
      </div>
    </section>
  );
}
