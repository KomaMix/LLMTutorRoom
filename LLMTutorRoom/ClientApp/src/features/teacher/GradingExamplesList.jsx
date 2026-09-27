import { ChevronDown } from "lucide-react";

export function GradingExamplesList({ examples = [], title = "Примеры оценивания задания" }) {
  if (!examples?.length) {
    return null;
  }

  return (
    <details className="teacher-task-disclosure grading-examples">
      <summary>
        <span>{title} · {examples.length}</span>
        <ChevronDown size={16} aria-hidden="true" />
      </summary>
      <div className="answer-stack grading-examples-content">
        {examples.map((example, index) => (
          <article className="answer-card grading-example-card" key={index}>
            <div className="panel-header">
              <h4>Пример {index + 1}</h4>
              <span className="task-points">{example.score} из {example.maxScore}</span>
            </div>
            {example.taskPrompt && (
              <div className="field">
                <span className="muted">Условие задания</span>
                <p className="grading-example-text">{example.taskPrompt}</p>
              </div>
            )}
            <div className="field">
              <span className="muted">Пример ответа ученика</span>
              <p className="grading-example-text">{example.studentAnswer}</p>
            </div>
            <div className="field">
              <span className="muted">Объяснение оценки</span>
              <p className="grading-example-text">{example.feedback}</p>
            </div>
          </article>
        ))}
      </div>
    </details>
  );
}
