import { useId, useRef } from "react";
import {
  AlignLeft,
  Bot,
  CheckSquare,
  CircleDot,
  Loader2,
  Plus,
  Save,
  UserRoundCheck,
  X
} from "lucide-react";
import {
  ensureChoiceOptions,
  taskCheckModes,
  taskTypes
} from "./teacherTestHelpers.js";

const taskTypeIcons = {
  "single-choice": CircleDot,
  "multiple-choice": CheckSquare,
  "free-text": AlignLeft
};

const checkModeIcons = {
  llm: Bot,
  manual: UserRoundCheck
};

export function TaskEditorForm({
  form,
  formId,
  title,
  submitLabel,
  submittingLabel,
  message,
  isDisabled = false,
  isSubmitting,
  hasLlmModel,
  onChange,
  onSubmit
}) {
  const isChoiceTask = form.type !== "free-text";
  const controlsDisabled = isDisabled || isSubmitting;
  const optionKeyPrefix = useId();
  const optionKeySequence = useRef(form.options.length);
  const optionKeys = useRef(form.options.map((_, index) => `${optionKeyPrefix}-${index + 1}`));

  function createOptionKey() {
    optionKeySequence.current += 1;
    return `${optionKeyPrefix}-${optionKeySequence.current}`;
  }

  function updateForm(field, value) {
    onChange({
      ...form,
      [field]: value
    });
  }

  function changeTaskType(type) {
    const options = type === "free-text"
      ? form.options
      : ensureChoiceOptions(form.options);

    onChange({
      ...form,
      type,
      checkMode: type === "free-text" && hasLlmModel ? "llm" : type === "free-text" ? "manual" : "auto",
      options,
      correctOptionIndexes: type === "free-text"
        ? []
        : form.correctOptionIndexes.slice(0, type === "single-choice" ? 1 : form.correctOptionIndexes.length)
    });
  }

  function updateOption(index, value) {
    onChange({
      ...form,
      options: form.options.map((option, optionIndex) =>
        optionIndex === index ? value : option)
    });
  }

  function addOption() {
    optionKeys.current.push(createOptionKey());
    onChange({
      ...form,
      options: [...form.options, ""]
    });
  }

  function removeOption(index) {
    optionKeys.current.splice(index, 1);
    onChange({
      ...form,
      options: form.options.filter((_, optionIndex) => optionIndex !== index),
      correctOptionIndexes: form.correctOptionIndexes
        .filter(optionIndex => optionIndex !== index)
        .map(optionIndex => optionIndex > index ? optionIndex - 1 : optionIndex)
    });
  }

  function toggleCorrectOption(index, isChecked) {
    if (form.type === "single-choice") {
      onChange({
        ...form,
        correctOptionIndexes: [index]
      });
      return;
    }

    onChange({
      ...form,
      correctOptionIndexes: isChecked
        ? [...form.correctOptionIndexes, index].sort((left, right) => left - right)
        : form.correctOptionIndexes.filter(optionIndex => optionIndex !== index)
    });
  }

  return (
    <form
      className="editor-form task-create-form teacher-task-editor"
      onSubmit={onSubmit}
      aria-busy={isSubmitting}
    >
      <div className="panel-header">
        <div>
          <span className="eyebrow">Конструктор</span>
          <h3>{title}</h3>
        </div>
        <Save size={18} aria-hidden="true" />
      </div>

      <div className="task-type-grid" role="group" aria-label="Тип задания">
        {taskTypes.map(type => {
          const Icon = taskTypeIcons[type.value];
          return (
            <button
              key={type.value}
              type="button"
              disabled={controlsDisabled}
              className={form.type === type.value ? "active" : ""}
              aria-pressed={form.type === type.value}
              onClick={() => changeTaskType(type.value)}
            >
              <Icon size={17} aria-hidden="true" />
              {type.label}
            </button>
          );
        })}
      </div>

      <div className="form-row">
        <div className="field">
          <label htmlFor={`${formId}-title`}>Название задания</label>
          <input
            id={`${formId}-title`}
            value={form.title}
            disabled={controlsDisabled}
            onChange={event => updateForm("title", event.target.value)}
            required
          />
        </div>
        <div className="field">
          <label htmlFor={`${formId}-points`}>Максимальный балл</label>
          <input
            id={`${formId}-points`}
            min="0.5"
            step="0.5"
            type="number"
            value={form.maxPoints}
            disabled={controlsDisabled}
            onChange={event => updateForm("maxPoints", event.target.value)}
            required
          />
        </div>
      </div>

      {form.type === "multiple-choice" && (
        <div className="field">
          <label htmlFor={`${formId}-wrong-answer-penalty`}>Штраф за неверный вариант</label>
          <input
            id={`${formId}-wrong-answer-penalty`}
            min="0"
            step="0.1"
            type="number"
            value={form.wrongAnswerPenalty}
            disabled={controlsDisabled}
            onChange={event => updateForm("wrongAnswerPenalty", event.target.value)}
          />
        </div>
      )}

      <div className="field">
        <label htmlFor={`${formId}-prompt`}>Текст задания</label>
        <textarea
          id={`${formId}-prompt`}
          value={form.prompt}
          disabled={controlsDisabled}
          onChange={event => updateForm("prompt", event.target.value)}
          required
        />
      </div>

      {form.type === "free-text" && (
        <div className="field">
          <label>Проверка</label>
          <div className="task-type-grid check-mode-grid" role="group" aria-label="Режим проверки">
            {taskCheckModes.map(mode => {
              const Icon = checkModeIcons[mode.value];
              const isUnavailable = mode.value === "llm" && !hasLlmModel;
              return (
                <button
                  key={mode.value}
                  type="button"
                  disabled={controlsDisabled || isUnavailable}
                  className={form.checkMode === mode.value && !isUnavailable ? "active" : ""}
                  aria-pressed={form.checkMode === mode.value && !isUnavailable}
                  onClick={() => updateForm("checkMode", mode.value)}
                >
                  <Icon size={17} aria-hidden="true" />
                  {mode.label}
                </button>
              );
            })}
          </div>
        </div>
      )}

      {isChoiceTask && (
        <div className="option-editor">
          <div className="panel-header">
            <div>
              <span className="eyebrow">Ответы</span>
              <h3>Варианты</h3>
            </div>
            <button type="button" className="button secondary" onClick={addOption} disabled={controlsDisabled}>
              <Plus size={16} aria-hidden="true" />
              Добавить
            </button>
          </div>

          <div className="option-editor-list">
            {form.options.map((option, index) => (
              <div className="option-editor-row" key={optionKeys.current[index]}>
                <input
                  aria-label={`Отметить вариант ${index + 1} правильным`}
                  checked={form.correctOptionIndexes.includes(index)}
                  className="option-control"
                  name={`${formId}-correct-option`}
                  type={form.type === "single-choice" ? "radio" : "checkbox"}
                  disabled={controlsDisabled}
                  onChange={event => toggleCorrectOption(index, event.target.checked)}
                />
                <input
                  aria-label={`Текст варианта ${index + 1}`}
                  value={option}
                  disabled={controlsDisabled}
                  onChange={event => updateOption(index, event.target.value)}
                  placeholder={`Вариант ${index + 1}`}
                />
                <button
                  type="button"
                  className="icon-button"
                  title="Удалить вариант"
                  aria-label={`Удалить вариант ${index + 1}`}
                  disabled={controlsDisabled || form.options.length <= 2}
                  onClick={() => removeOption(index)}
                >
                  <X size={16} aria-hidden="true" />
                </button>
              </div>
            ))}
          </div>
        </div>
      )}

      <div role="status" aria-atomic="true" aria-live="polite">
        {message && <p className="form-note">{message}</p>}
      </div>

      <button type="submit" className="button primary" disabled={controlsDisabled}>
        {isSubmitting ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Save size={16} aria-hidden="true" />}
        {isSubmitting ? submittingLabel : submitLabel}
      </button>
    </form>
  );
}

export default TaskEditorForm;
