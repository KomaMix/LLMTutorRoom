import { useState } from "react";
import { Bot, CalendarClock, Gauge, RefreshCw, Sparkles } from "lucide-react";
import { formatDate, formatPeriod } from "../../shared/lib/dates.js";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";

export function ModelPanel({ models, onRefresh }) {
  const [isRefreshing, setIsRefreshing] = useState(false);

  async function handleRefresh() {
    if (isRefreshing) {
      return;
    }

    setIsRefreshing(true);
    try {
      await onRefresh?.();
    } finally {
      setIsRefreshing(false);
    }
  }

  return (
    <section className="panel teacher-page teacher-model-page">
      <header className="teacher-page-heading">
        <div>
          <h2>Модели проверки</h2>
          <p>Здесь показаны модели, доступные вам для автоматической проверки письменных ответов.</p>
        </div>
        <button
          type="button"
          className="button secondary"
          disabled={isRefreshing}
          onClick={handleRefresh}
        >
          <RefreshCw className={isRefreshing ? "spin" : undefined} size={16} aria-hidden="true" />
          {isRefreshing ? "Обновление..." : "Обновить данные"}
        </button>
      </header>

      {models.length === 0 && (
        <div className="teacher-model-empty">
          <Bot size={24} aria-hidden="true" />
          <div>
            <strong>Доступных моделей пока нет</strong>
            <span>Обратитесь к администратору, чтобы получить доступ.</span>
          </div>
        </div>
      )}

      <div className="model-grid teacher-model-grid">
        {models.map(model => {
          const maxChecks = Math.max(0, Number(model.maxChecks) || 0);
          const usedChecks = Math.max(0, Number(model.usedChecks) || 0);
          const remainingChecks = Math.max(0, Number(model.remainingChecks) || 0);

          return (
            <article className="model-card teacher-model-card" key={model.key}>
              <header className="teacher-model-header">
                <div className="teacher-model-identity">
                  <span className="model-icon">
                    <Sparkles size={18} aria-hidden="true" />
                  </span>
                  <div>
                    <strong>{model.displayName || model.key}</strong>
                  </div>
                </div>
                <StatusBadge status={model.status} />
              </header>

              <div className="teacher-model-key">{model.key}</div>

              <section className="teacher-model-quota">
                <div>
                  <span><Gauge size={16} aria-hidden="true" />Квота проверок</span>
                  <strong>{remainingChecks} из {maxChecks}</strong>
                </div>
                <progress
                  aria-label={`Осталось проверок: ${remainingChecks} из ${maxChecks}`}
                  max={Math.max(1, maxChecks)}
                  value={Math.min(maxChecks, remainingChecks)}
                />
              </section>

              <dl className="teacher-model-facts">
                <div>
                  <dt>Использовано</dt>
                  <dd>{usedChecks}</dd>
                </div>
                <div>
                  <dt>Период квоты</dt>
                  <dd>{formatPeriod(model.periodSeconds)}</dd>
                </div>
                <div>
                  <dt><CalendarClock size={14} aria-hidden="true" />Обновится</dt>
                  <dd>{model.periodEndsAt ? formatDate(model.periodEndsAt) : "—"}</dd>
                </div>
              </dl>
            </article>
          );
        })}
      </div>
    </section>
  );
}

export default ModelPanel;
