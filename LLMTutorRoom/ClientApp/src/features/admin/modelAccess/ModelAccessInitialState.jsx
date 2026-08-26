import { Loader2, Server } from "lucide-react";

export function ModelAccessInitialState({ error, isLoading, onRetry }) {
  if (isLoading) {
    return (
      <div className="panel admin-panel admin-state admin-page-state" role="status">
        <Loader2 className="spin" size={23} aria-hidden="true" />
        <strong>Загружаем каталог и преподавателей</strong>
        <span>Подготавливаем данные для настройки доступов.</span>
      </div>
    );
  }

  if (!error) {
    return null;
  }

  return (
    <div className="panel admin-panel admin-state admin-page-state error">
      <Server size={23} aria-hidden="true" />
      <strong>Данные не загрузились</strong>
      <div>
        <p className="form-error" role="alert">{error}</p>
        <button type="button" className="button secondary" onClick={onRetry}>
          Повторить загрузку
        </button>
      </div>
    </div>
  );
}
