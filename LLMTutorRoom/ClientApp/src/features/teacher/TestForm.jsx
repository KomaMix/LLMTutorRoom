import { Loader2, Plus, Save } from "lucide-react";
import {
  getModelOptionLabel,
  getModelOptions
} from "./teacherTestHelpers.js";

export function TestForm({
  form,
  isSubmitting,
  message,
  mode,
  models,
  onChange,
  onSubmit
}) {
  const isEditing = mode === "edit";
  const idPrefix = isEditing ? "edit-test" : "test";
  const modelOptions = getModelOptions(models, form.llmModelKey);

  function updateForm(field, value) {
    onChange(current => ({ ...current, [field]: value }));
  }

  const titleField = (
    <div className="field">
      <label htmlFor={`${idPrefix}-title`}>Название</label>
      <input
        id={`${idPrefix}-title`}
        value={form.title}
        disabled={isSubmitting}
        onChange={event => updateForm("title", event.target.value)}
        required
      />
    </div>
  );

  const subjectField = (
    <div className="field">
      <label htmlFor={`${idPrefix}-subject`}>Предмет</label>
      <input
        id={`${idPrefix}-subject`}
        value={form.subject}
        disabled={isSubmitting}
        onChange={event => updateForm("subject", event.target.value)}
        required
      />
    </div>
  );

  return (
    <form
      className={isEditing ? "editor-form test-edit-form" : "editor-form test-create-form"}
      onSubmit={onSubmit}
      aria-busy={isSubmitting}
    >
      {isEditing ? (
        <div className="panel-header">
          <div>
            <span className="eyebrow">Настройки</span>
            <h3>Редактирование теста</h3>
          </div>
          <Save size={18} aria-hidden="true" />
        </div>
      ) : (
        <h3>Новый тест</h3>
      )}

      {isEditing ? (
        <div className="form-row">
          {titleField}
          {subjectField}
        </div>
      ) : (
        <>
          {titleField}
          {subjectField}
        </>
      )}

      <div className="form-row">
        <div className="field">
          <label htmlFor={`${idPrefix}-time-limit`}>Время, мин</label>
          <input
            id={`${idPrefix}-time-limit`}
            min="1"
            type="number"
            value={form.timeLimitMinutes}
            disabled={isSubmitting}
            onChange={event => updateForm("timeLimitMinutes", event.target.value)}
            required
          />
        </div>
        <div className="field">
          <label htmlFor={`${idPrefix}-status`}>Статус</label>
          <select
            id={`${idPrefix}-status`}
            value={form.status}
            disabled={isSubmitting}
            onChange={event => updateForm("status", event.target.value)}
          >
            <option value="draft">Черновик</option>
            <option value="published">Опубликован</option>
          </select>
        </div>
      </div>

      <div className="field">
        <label htmlFor={`${idPrefix}-deadline`}>Дедлайн</label>
        <input
          id={`${idPrefix}-deadline`}
          type="date"
          value={form.deadline}
          disabled={isSubmitting}
          onChange={event => updateForm("deadline", event.target.value)}
          required
        />
      </div>

      <div className="field">
        <label htmlFor={`${idPrefix}-llm-model`}>LLM-модель проверки</label>
        <select
          id={`${idPrefix}-llm-model`}
          value={form.llmModelKey}
          disabled={isSubmitting}
          onChange={event => updateForm("llmModelKey", event.target.value)}
        >
          <option value="">Без LLM-модели</option>
          {modelOptions.map(model => (
            <option key={model.key} value={model.key}>
              {getModelOptionLabel(model)}
            </option>
          ))}
        </select>
      </div>

      <div className="field">
        <label htmlFor={`${idPrefix}-summary`}>Краткое описание</label>
        <textarea
          id={`${idPrefix}-summary`}
          className="compact-textarea"
          value={form.summary}
          disabled={isSubmitting}
          onChange={event => updateForm("summary", event.target.value)}
        />
      </div>

      {message && <p className="form-note">{message}</p>}

      <button type="submit" className="button primary" disabled={isSubmitting}>
        {isSubmitting
          ? <Loader2 className="spin" size={16} aria-hidden="true" />
          : isEditing
            ? <Save size={16} aria-hidden="true" />
            : <Plus size={16} aria-hidden="true" />}
        {isSubmitting
          ? isEditing ? "Сохранение..." : "Создание..."
          : isEditing ? "Сохранить тест" : "Создать тест"}
      </button>
    </form>
  );
}

export default TestForm;
