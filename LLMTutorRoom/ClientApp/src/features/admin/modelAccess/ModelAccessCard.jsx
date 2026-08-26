import { Bot, CalendarClock, Gauge, Loader2, Pencil, Trash2 } from "lucide-react";
import { formatDate, formatPeriod } from "../../../shared/lib/dates.js";

export function ModelAccessCard({
  access,
  busyModelKey,
  canMutateAccess,
  isEditing,
  onEdit,
  onRemove
}) {
  const maxChecks = Math.max(0, Number(access.maxChecks) || 0);
  const remainingChecks = Math.max(0, Number(access.remainingChecks) || 0);
  const usedChecks = Math.max(0, Number(access.usedChecks) || 0);
  const accessState = access.isEnabled
    ? { className: "active", label: "Доступ включён" }
    : { className: "disabled", label: "Доступ выключен" };

  return (
    <article className={`admin-access-card${isEditing ? " editing" : ""}`}>
      <header className="admin-access-card-header">
        <div className="admin-model-identity">
          <span className="admin-model-icon">
            <Bot size={18} aria-hidden="true" />
          </span>
          <div>
            <strong>{access.displayName || access.modelKey}</strong>
            <span>{access.modelKey}</span>
          </div>
        </div>
        <div className="admin-access-state-group">
          <span className={`admin-access-state ${accessState.className}`}>
            {accessState.label}
          </span>
          {!access.hasEnabledDeployment && (
            <span className="admin-access-state warning">Модель недоступна</span>
          )}
        </div>
      </header>

      <section className="admin-access-quota">
        <div>
          <span><Gauge size={15} aria-hidden="true" />Осталось проверок</span>
          <strong>{remainingChecks} из {maxChecks}</strong>
        </div>
        <progress
          aria-label={`Осталось проверок: ${remainingChecks} из ${maxChecks}`}
          max={Math.max(1, maxChecks)}
          value={Math.min(maxChecks, remainingChecks)}
        />
      </section>

      <dl className="admin-access-meta">
        <div>
          <dt>Использовано</dt>
          <dd>{usedChecks}</dd>
        </div>
        <div>
          <dt>Период</dt>
          <dd>{formatPeriod(access.periodSeconds)}</dd>
        </div>
        <div>
          <dt><CalendarClock size={14} aria-hidden="true" />Обновится</dt>
          <dd>{formatDate(access.periodEndsAt)}</dd>
        </div>
      </dl>

      <footer className="admin-access-actions">
        <button
          type="button"
          className="button secondary"
          aria-label={`Изменить доступ к модели ${access.displayName || access.modelKey}`}
          disabled={!canMutateAccess}
          onClick={() => onEdit(access)}
        >
          <Pencil size={15} aria-hidden="true" />
          Изменить
        </button>
        <button
          type="button"
          className="icon-button danger"
          title="Удалить доступ"
          aria-label={`Удалить доступ к модели ${access.displayName || access.modelKey}`}
          disabled={!canMutateAccess || busyModelKey === access.modelKey}
          onClick={() => onRemove(access)}
        >
          {busyModelKey === access.modelKey
            ? <Loader2 className="spin" size={16} aria-hidden="true" />
            : <Trash2 size={16} aria-hidden="true" />}
        </button>
      </footer>
    </article>
  );
}
