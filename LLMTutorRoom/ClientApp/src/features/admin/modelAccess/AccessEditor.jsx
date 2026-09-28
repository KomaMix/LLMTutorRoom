import { Loader2, Save, ShieldCheck, X } from "lucide-react";
import { formatPeriod } from "../../../shared/lib/dates.js";
import { getPeriodSeconds } from "./accessForm.js";

export function AccessEditor({
  canMutateAccess,
  editingModelId,
  editorRef,
  form,
  isAccessLoading,
  isAccessPayloadDisabled,
  isModelSelectDisabled,
  isMutationLocked,
  isSaving,
  models,
  onCancel,
  onSave,
  onSelectModel,
  onUpdateForm,
  selectedModel,
  teachers
}) {
  return (
    <aside ref={editorRef} className="panel admin-panel admin-access-editor">
      <header className="admin-section-header">
        <span className="admin-section-icon">
          <ShieldCheck size={19} aria-hidden="true" />
        </span>
        <div>
          <h2>{editingModelId ? "Изменить доступ" : "Настроить доступ"}</h2>
          <p>
            {editingModelId
              ? "Параметры выбранной модели загружены в форму."
              : "Выберите модель и задайте квоту проверок."}
          </p>
        </div>
        {editingModelId && (
          <button
            type="button"
            className="icon-button admin-editor-close"
            title="Отменить редактирование"
            aria-label="Отменить редактирование"
            disabled={isMutationLocked}
            onClick={onCancel}
          >
            <X size={16} aria-hidden="true" />
          </button>
        )}
      </header>

      <form className="admin-form" onSubmit={onSave}>
        <div className="field">
          <label htmlFor="access-model">Модель</label>
          <select
            id="access-model"
            value={form.modelId}
            onChange={event => onSelectModel(event.target.value ? Number(event.target.value) : "")}
            disabled={isModelSelectDisabled}
          >
            <option value="">
              {models.length === 0 ? "Каталог моделей пуст" : "Выберите модель"}
            </option>
            {models.map(model => (
              <option key={model.id} value={model.id} disabled={model.isArchived}>
                {model.displayName || model.id}
                {model.isArchived ? " – в архиве" : model.hasEnabledDeployment ? "" : " – нет активного деплоймента"}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="access-period">Период квоты</label>
          <div className="admin-period-field">
            <input
              id="access-period"
              min="1"
              step="1"
              type="number"
              value={form.periodValue}
              onChange={event => onUpdateForm("periodValue", event.target.value)}
              disabled={isAccessPayloadDisabled}
              required
            />
            <select
              aria-label="Единица периода квоты"
              value={form.periodUnit}
              onChange={event => onUpdateForm("periodUnit", event.target.value)}
              disabled={isAccessPayloadDisabled}
            >
              <option value="days">дн.</option>
              <option value="hours">ч.</option>
              <option value="minutes">мин.</option>
              <option value="seconds">сек.</option>
            </select>
          </div>
          <small>Итого: {formatPeriod(getPeriodSeconds(form))}.</small>
        </div>
        <div className="field">
          <label htmlFor="access-limit">Проверок за период</label>
          <input
            id="access-limit"
            min="1"
            step="1"
            type="number"
            value={form.maxChecks}
            onChange={event => onUpdateForm("maxChecks", event.target.value)}
            disabled={isAccessPayloadDisabled}
            required
          />
        </div>

        <label className="admin-switch">
          <input
            type="checkbox"
            checked={Boolean(form.modelId) && form.isEnabled}
            onChange={event => onUpdateForm("isEnabled", event.target.checked)}
            disabled={isAccessPayloadDisabled}
          />
          <span aria-hidden="true" />
          <div>
            <strong>
              {!form.modelId
                ? "Состояние доступа"
                : form.isEnabled
                  ? "Доступ включён"
                  : "Доступ выключен"}
            </strong>
            <small>
              {!form.modelId
                ? "Сначала выберите модель."
                : !form.isEnabled
                ? "Модель будет скрыта в кабинете преподавателя."
                : selectedModel?.hasEnabledDeployment
                  ? "Преподаватель сможет выбрать модель в тесте."
                  : "Доступ сохранится, но модель появится после её подключения."}
            </small>
          </div>
        </label>

        {teachers.length === 0 && (
          <p className="form-note">Сначала добавьте преподавателя.</p>
        )}
        {models.length === 0 && (
          <p className="form-note">Каталог моделей пуст.</p>
        )}

        <button
          type="submit"
          className="button primary admin-submit"
          disabled={!canMutateAccess || !form.modelId}
        >
          {isSaving
            ? <Loader2 className="spin" size={16} aria-hidden="true" />
            : <Save size={16} aria-hidden="true" />}
          {isSaving
            ? "Сохранение..."
            : isAccessLoading
              ? "Загрузка доступов..."
              : editingModelId
                ? "Сохранить изменения"
                : "Выдать доступ"}
        </button>
      </form>
    </aside>
  );
}
