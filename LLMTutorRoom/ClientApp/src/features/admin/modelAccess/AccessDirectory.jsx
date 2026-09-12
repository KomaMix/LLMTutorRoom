import { Bot, Loader2, ShieldCheck, UserRound } from "lucide-react";
import { ModelAccessCard } from "./ModelAccessCard.jsx";

export function AccessDirectory({
  accessError,
  accessList,
  busyModelKey,
  canMutateAccess,
  editingModelKey,
  isAccessCurrent,
  isAccessLoading,
  onEdit,
  onRemove,
  onRetry,
  selectedTeacher,
  selectedTeacherId
}) {
  return (
    <section className="panel admin-panel admin-access-directory" aria-busy={isAccessLoading}>
      <header className="admin-section-header admin-access-list-header">
        <div>
          <h2>Выданные модели</h2>
          <p>
            {selectedTeacher
              ? `Текущие доступы для ${selectedTeacher.userName}.`
              : "Выберите преподавателя, чтобы увидеть доступы."}
          </p>
        </div>
        {isAccessCurrent && <span className="count-badge">{accessList.length}</span>}
      </header>

      {!selectedTeacherId ? (
        <div className="admin-state empty">
          <UserRound size={23} aria-hidden="true" />
          <strong>Преподаватель не выбран</strong>
          <span>После создания преподавателя здесь можно будет назначить модели.</span>
        </div>
      ) : isAccessLoading ? (
        <div className="admin-state" role="status">
          <Loader2 className="spin" size={22} aria-hidden="true" />
          <strong>Загружаем доступы</strong>
          <span>Получаем актуальные квоты преподавателя.</span>
        </div>
      ) : accessError ? (
        <div className="admin-state error">
          <ShieldCheck size={22} aria-hidden="true" />
          <strong>Доступы не загрузились</strong>
          <p className="form-error" role="alert">{accessError}</p>
          <button type="button" className="button secondary" onClick={onRetry}>
            Повторить загрузку
          </button>
        </div>
      ) : accessList.length === 0 ? (
        <div className="admin-state empty">
          <Bot size={24} aria-hidden="true" />
          <strong>Модели ещё не назначены</strong>
          <span>Настройте первый доступ в панели настройки.</span>
        </div>
      ) : (
        <div className="admin-access-list">
          {accessList.map(access => (
            <ModelAccessCard
              key={access.modelKey}
              access={access}
              busyModelKey={busyModelKey}
              canMutateAccess={canMutateAccess}
              isEditing={editingModelKey === access.modelKey}
              onEdit={onEdit}
              onRemove={onRemove}
            />
          ))}
        </div>
      )}
    </section>
  );
}
