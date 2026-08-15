import { Bot, Sparkles } from "lucide-react";
import { formatPeriod } from "../../shared/lib/dates.js";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";

export function ModelPanel({ models }) {
  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">LLMGateway</span>
          <h2>Модели проверки</h2>
        </div>
        <Bot size={18} aria-hidden="true" />
      </div>

      {models.length === 0 && (
        <p className="muted">Доступные LLM пока не подключены.</p>
      )}

      <div className="model-grid">
        {models.map(model => (
          <article className="model-card" key={model.key}>
            <div className="model-icon">
              <Sparkles size={18} aria-hidden="true" />
            </div>
            <div>
              <strong>{model.displayName || model.key}</strong>
              <span>{model.provider}</span>
            </div>
            <StatusBadge status={model.status} />
            <dl>
              <div>
                <dt>лимит</dt>
                <dd>{model.remainingChecks}/{model.maxChecks}</dd>
              </div>
              <div>
                <dt>период</dt>
                <dd>{formatPeriod(model.periodSeconds)}</dd>
              </div>
            </dl>
          </article>
        ))}
      </div>
    </section>
  );
}

export default ModelPanel;
