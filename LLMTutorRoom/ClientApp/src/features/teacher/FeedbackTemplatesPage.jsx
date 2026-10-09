import { useEffect, useMemo, useRef, useState } from "react";
import {
  ArrowDown,
  ArrowUp,
  Copy,
  FilePlus2,
  Loader2,
  LockKeyhole,
  MessageSquareText,
  Plus,
  Save,
  Trash2
} from "lucide-react";
import {
  createFeedbackTemplate,
  deleteFeedbackTemplate,
  updateFeedbackTemplate
} from "../../api/teachingApi.js";
import { useUnsavedChangesGuard } from "../../shared/hooks/useUnsavedChangesGuard.js";
import { ConfirmDialog } from "../../shared/ui/ConfirmDialog.jsx";
import { ErrorState } from "../../shared/ui/ErrorState.jsx";
import { LoadingState } from "../../shared/ui/LoadingState.jsx";
import {
  createBlankTemplateForm,
  createSection,
  createTemplateCopyForm,
  createTemplateForm,
  createTemplatePayload,
  feedbackDetailOptions,
  feedbackDisclosureOptions,
  feedbackToneOptions,
  getOptionLabel,
  validateTemplateForm
} from "./feedbackTemplateHelpers.js";
import { getApiErrorMessage } from "./teacherTestControllerHelpers.js";

const unsavedPrompt = "Есть несохранённые изменения шаблона. Покинуть редактор и потерять их?";

function TemplateDirectory({ disabled, onCreate, onSelect, selectedId, templates }) {
  const systemTemplates = templates.filter(template => template.isSystem);
  const personalTemplates = templates.filter(template => !template.isSystem);

  function renderGroup(title, items) {
    return (
      <section className="feedback-template-directory-group">
        <header>
          <h3>{title}</h3>
          <span className="count-badge">{items.length}</span>
        </header>
        <div className="feedback-template-directory-list">
          {items.map(template => (
            <button
              type="button"
              className={selectedId === template.id ? "active" : ""}
              disabled={disabled}
              key={template.id}
              onClick={() => onSelect(template)}
            >
              <span>
                {template.isSystem
                  ? <LockKeyhole size={15} aria-hidden="true" />
                  : <MessageSquareText size={15} aria-hidden="true" />}
                <strong>{template.name}</strong>
              </span>
              <small>{template.description || "Без описания"}</small>
            </button>
          ))}
        </div>
      </section>
    );
  }

  return (
    <aside className="list-panel feedback-template-directory">
      <header className="teacher-section-header">
        <div>
          <span className="eyebrow">Библиотека</span>
          <h2>Шаблоны</h2>
        </div>
        <button
          type="button"
          className="icon-button"
          aria-label="Создать шаблон"
          disabled={disabled}
          onClick={onCreate}
        >
          <Plus size={18} aria-hidden="true" />
        </button>
      </header>
      {renderGroup("Системные", systemTemplates)}
      {renderGroup("Мои шаблоны", personalTemplates)}
      {personalTemplates.length === 0 && (
        <p className="muted feedback-template-directory-empty">
          Создайте свой шаблон или сделайте копию системного.
        </p>
      )}
    </aside>
  );
}

function SectionEditor({ disabled, index, onChange, onMove, onRemove, section, total }) {
  function update(field, value) {
    onChange(index, { ...section, [field]: value });
  }

  return (
    <article className="feedback-section-editor">
      <header>
        <div>
          <span>Раздел {index + 1}</span>
          <strong>{section.title || "Без названия"}</strong>
        </div>
        {!disabled && (
          <div className="feedback-section-actions">
            <button
              type="button"
              className="icon-button"
              aria-label="Переместить раздел выше"
              disabled={index === 0}
              onClick={() => onMove(index, index - 1)}
            >
              <ArrowUp size={16} aria-hidden="true" />
            </button>
            <button
              type="button"
              className="icon-button"
              aria-label="Переместить раздел ниже"
              disabled={index === total - 1}
              onClick={() => onMove(index, index + 1)}
            >
              <ArrowDown size={16} aria-hidden="true" />
            </button>
            <button
              type="button"
              className="icon-button danger"
              aria-label="Удалить раздел"
              disabled={total === 1}
              onClick={() => onRemove(index)}
            >
              <Trash2 size={16} aria-hidden="true" />
            </button>
          </div>
        )}
      </header>

      <div className="form-row">
        <div className="field">
          <label htmlFor={`feedback-section-title-${section.id}`}>Заголовок для ученика</label>
          <input
            id={`feedback-section-title-${section.id}`}
            value={section.title}
            disabled={disabled}
            maxLength={120}
            onChange={event => update("title", event.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor={`feedback-section-format-${section.id}`}>Формат</label>
          <select
            id={`feedback-section-format-${section.id}`}
            value={section.format}
            disabled={disabled}
            onChange={event => update("format", event.target.value)}
          >
            <option value="text">Связный текст</option>
            <option value="list">Список пунктов</option>
          </select>
        </div>
      </div>

      <div className="field">
        <label htmlFor={`feedback-section-instruction-${section.id}`}>
          Что должна написать модель
        </label>
        <textarea
          id={`feedback-section-instruction-${section.id}`}
          className="compact-textarea"
          value={section.instruction}
          disabled={disabled}
          maxLength={1000}
          onChange={event => update("instruction", event.target.value)}
        />
      </div>

      <div className="feedback-section-options">
        {section.format === "list" && (
          <div className="field">
            <label htmlFor={`feedback-section-limit-${section.id}`}>Максимум пунктов</label>
            <input
              id={`feedback-section-limit-${section.id}`}
              type="number"
              min="1"
              max="5"
              value={section.maxItems}
              disabled={disabled}
              onChange={event => update("maxItems", event.target.value)}
            />
          </div>
        )}
        <label className="feedback-required-control">
          <input
            type="checkbox"
            checked={section.isRequired}
            disabled={disabled}
            onChange={event => update("isRequired", event.target.checked)}
          />
          <span>
            <strong>Обязательный раздел</strong>
            <small>Модель должна заполнить его при каждой проверке.</small>
          </span>
        </label>
      </div>
    </article>
  );
}

function TemplatePreview({ form }) {
  const disclosure = feedbackDisclosureOptions.find(
    option => option.value === form.answerDisclosure);

  return (
    <aside className="feedback-template-preview">
      <header>
        <span className="eyebrow">Предпросмотр структуры</span>
        <h3>{form.name || "Новый шаблон"}</h3>
        <p>{form.description || "Описание шаблона появится здесь."}</p>
      </header>

      <div className="feedback-template-preview-meta">
        <span>{getOptionLabel(feedbackToneOptions, form.tone)}</span>
        <span>{getOptionLabel(feedbackDetailOptions, form.detailLevel)}</span>
      </div>

      <div className="feedback-disclosure-preview">
        <strong>{disclosure?.label}</strong>
        <p>{disclosure?.description}</p>
      </div>

      <div className="feedback-template-preview-sections">
        {form.sections.map(section => (
          <section key={section.id}>
            <span>{section.title || "Раздел без названия"}</span>
            {section.format === "list" ? (
              <ul>
                {Array.from({ length: Math.min(Number(section.maxItems) || 1, 3) }, (_, index) => (
                  <li key={index}>Пункт обратной связи {index + 1}</li>
                ))}
              </ul>
            ) : (
              <p>Здесь будет связный текст обратной связи по инструкции раздела.</p>
            )}
          </section>
        ))}
      </div>
    </aside>
  );
}

export function FeedbackTemplatesPage({ error, isLoading, reload, templates }) {
  const [form, setForm] = useState(null);
  const [selectedId, setSelectedId] = useState("");
  const [message, setMessage] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteCandidate, setDeleteCandidate] = useState(null);
  const baselineRef = useRef(null);
  const formSignature = useMemo(() => JSON.stringify(form), [form]);
  const hasUnsavedChanges = form !== null
    && !form.isSystem
    && formSignature !== baselineRef.current;
  const confirmDiscard = useUnsavedChangesGuard(hasUnsavedChanges, unsavedPrompt);

  useEffect(() => {
    if (templates.length === 0 || form) {
      return;
    }

    const initialForm = createTemplateForm(templates[0]);
    baselineRef.current = JSON.stringify(initialForm);
    setForm(initialForm);
    setSelectedId(templates[0].id);
  }, [form, templates]);

  function openTemplate(template) {
    if (!confirmDiscard()) {
      return;
    }

    const nextForm = createTemplateForm(template);
    baselineRef.current = JSON.stringify(nextForm);
    setForm(nextForm);
    setSelectedId(template.id);
    setMessage("");
  }

  function startCreate() {
    if (!confirmDiscard()) {
      return;
    }

    setForm(createBlankTemplateForm());
    baselineRef.current = null;
    setSelectedId("");
    setMessage("");
  }

  function startCopy() {
    if (!form || !confirmDiscard()) {
      return;
    }

    setForm(createTemplateCopyForm(form));
    baselineRef.current = null;
    setSelectedId("");
    setMessage("");
  }

  function updateSection(index, section) {
    setForm(current => ({
      ...current,
      sections: current.sections.map((item, itemIndex) => itemIndex === index ? section : item)
    }));
  }

  function moveSection(from, to) {
    setForm(current => {
      const sections = [...current.sections];
      const [section] = sections.splice(from, 1);
      sections.splice(to, 0, section);
      return { ...current, sections };
    });
  }

  function removeSection(index) {
    setForm(current => ({
      ...current,
      sections: current.sections.filter((_, itemIndex) => itemIndex !== index)
    }));
  }

  async function saveTemplate(event) {
    event.preventDefault();
    if (!form || form.isSystem || isSaving) {
      return;
    }

    const validationError = validateTemplateForm(form);
    if (validationError) {
      setMessage(validationError);
      return;
    }

    setIsSaving(true);
    setMessage("");
    try {
      const payload = createTemplatePayload(form);
      const saved = form.id
        ? await updateFeedbackTemplate(form.id, payload)
        : await createFeedbackTemplate(payload);
      const refreshed = await reload();
      const updated = refreshed?.find(template => template.id === saved.id) ?? saved;
      const nextForm = createTemplateForm(updated);
      baselineRef.current = JSON.stringify(nextForm);
      setForm(nextForm);
      setSelectedId(updated.id);
      setMessage("Шаблон сохранен.");
    } catch (requestError) {
      setMessage(getApiErrorMessage(requestError, "Не удалось сохранить шаблон."));
    } finally {
      setIsSaving(false);
    }
  }

  async function confirmDelete() {
    if (!deleteCandidate || isDeleting) {
      return;
    }

    setIsDeleting(true);
    try {
      await deleteFeedbackTemplate(deleteCandidate.id);
      const refreshed = await reload();
      const nextTemplate = refreshed?.[0] ?? null;
      const nextForm = nextTemplate ? createTemplateForm(nextTemplate) : createBlankTemplateForm();
      baselineRef.current = nextTemplate ? JSON.stringify(nextForm) : null;
      setForm(nextForm);
      setSelectedId(nextTemplate?.id ?? "");
      setDeleteCandidate(null);
      setMessage("Шаблон удален.");
    } catch (requestError) {
      setDeleteCandidate(null);
      setMessage(getApiErrorMessage(requestError, "Не удалось удалить шаблон."));
    } finally {
      setIsDeleting(false);
    }
  }

  if (isLoading && templates.length === 0) {
    return <LoadingState message="Загрузка шаблонов обратной связи" />;
  }

  if (error && templates.length === 0) {
    return <ErrorState message={error} onRetry={() => reload()} />;
  }

  if (!form) {
    return <LoadingState message="Подготовка конструктора" />;
  }

  const readOnly = form.isSystem;
  const controlsDisabled = readOnly || isSaving || isDeleting;

  return (
    <section className="feedback-templates-page teacher-page">
      <TemplateDirectory
        disabled={isSaving || isDeleting}
        onCreate={startCreate}
        onSelect={openTemplate}
        selectedId={selectedId}
        templates={templates}
      />

      <article className="detail-panel feedback-template-workspace">
        <header className="feedback-template-page-header">
          <div>
            <span className="eyebrow">Конструктор обратной связи</span>
            <h2>{readOnly ? "Системный шаблон" : form.id ? "Редактирование шаблона" : "Новый шаблон"}</h2>
            <p>
              Выберите структуру и степень раскрытия ответа. Формат данных для модели задает система.
            </p>
          </div>
          <div className="feedback-template-header-actions">
            <button
              type="button"
              className="button secondary"
              disabled={isSaving || isDeleting}
              onClick={startCopy}
            >
              <Copy size={16} aria-hidden="true" />
              Создать копию
            </button>
            {!readOnly && form.id && (
              <button
                type="button"
                className="button danger"
                disabled={isSaving || isDeleting}
                onClick={() => setDeleteCandidate(form)}
              >
                <Trash2 size={16} aria-hidden="true" />
                Удалить
              </button>
            )}
          </div>
        </header>

        {readOnly && (
          <div className="feedback-template-readonly-note">
            <LockKeyhole size={18} aria-hidden="true" />
            <span>Системный шаблон доступен только для чтения. Сделайте копию, чтобы изменить его.</span>
          </div>
        )}

        <div className="feedback-template-layout">
          <form className="feedback-template-editor" onSubmit={saveTemplate}>
            <section className="feedback-template-settings">
              <div className="form-row">
                <div className="field">
                  <label htmlFor="feedback-template-name">Название</label>
                  <input
                    id="feedback-template-name"
                    value={form.name}
                    disabled={controlsDisabled}
                    maxLength={120}
                    onChange={event => setForm(current => ({ ...current, name: event.target.value }))}
                  />
                </div>
                <div className="field">
                  <label htmlFor="feedback-template-tone">Тон</label>
                  <select
                    id="feedback-template-tone"
                    value={form.tone}
                    disabled={controlsDisabled}
                    onChange={event => setForm(current => ({ ...current, tone: event.target.value }))}
                  >
                    {feedbackToneOptions.map(option => (
                      <option value={option.value} key={option.value}>{option.label}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="field">
                <label htmlFor="feedback-template-description">Описание для библиотеки</label>
                <textarea
                  id="feedback-template-description"
                  className="compact-textarea"
                  value={form.description}
                  disabled={controlsDisabled}
                  maxLength={500}
                  onChange={event => setForm(current => ({ ...current, description: event.target.value }))}
                />
              </div>

              <div className="form-row">
                <div className="field">
                  <label htmlFor="feedback-template-detail">Подробность</label>
                  <select
                    id="feedback-template-detail"
                    value={form.detailLevel}
                    disabled={controlsDisabled}
                    onChange={event => setForm(current => ({ ...current, detailLevel: event.target.value }))}
                  >
                    {feedbackDetailOptions.map(option => (
                      <option value={option.value} key={option.value}>{option.label}</option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label htmlFor="feedback-template-disclosure">Раскрытие ответа</label>
                  <select
                    id="feedback-template-disclosure"
                    value={form.answerDisclosure}
                    disabled={controlsDisabled}
                    onChange={event => setForm(current => ({ ...current, answerDisclosure: event.target.value }))}
                  >
                    {feedbackDisclosureOptions.map(option => (
                      <option value={option.value} key={option.value}>{option.label}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="field">
                <label htmlFor="feedback-template-custom-instruction">
                  Дополнительная инструкция
                </label>
                <textarea
                  id="feedback-template-custom-instruction"
                  value={form.customInstruction}
                  disabled={controlsDisabled}
                  maxLength={2000}
                  placeholder="Например: обращай внимание на точность терминов и связывай замечания с формулировкой задания."
                  onChange={event => setForm(current => ({ ...current, customInstruction: event.target.value }))}
                />
                <small>
                  Инструкция влияет на содержание комментария, но не меняет схему ответа модели и правила оценки.
                </small>
              </div>
            </section>

            <section className="feedback-template-sections-editor">
              <header className="teacher-section-header">
                <div>
                  <span className="eyebrow">Структура</span>
                  <h3>Разделы для ученика</h3>
                </div>
                {!readOnly && (
                  <button
                    type="button"
                    className="button secondary"
                    disabled={controlsDisabled || form.sections.length >= 6}
                    onClick={() => setForm(current => ({
                      ...current,
                      sections: [...current.sections, createSection()]
                    }))}
                  >
                    <Plus size={16} aria-hidden="true" />
                    Добавить раздел
                  </button>
                )}
              </header>

              <div className="feedback-section-editor-list">
                {form.sections.map((section, index) => (
                  <SectionEditor
                    disabled={controlsDisabled}
                    index={index}
                    key={section.id}
                    onChange={updateSection}
                    onMove={moveSection}
                    onRemove={removeSection}
                    section={section}
                    total={form.sections.length}
                  />
                ))}
              </div>
            </section>

            <div className="feedback-template-save-row" role="status" aria-live="polite">
              <span className={message && message !== "Шаблон сохранен." && message !== "Шаблон удален."
                ? "form-note error"
                : "form-note"}
              >
                {message}
              </span>
              {!readOnly && (
                <button type="submit" className="button primary" disabled={controlsDisabled}>
                  {isSaving
                    ? <Loader2 className="spin" size={16} aria-hidden="true" />
                    : form.id
                      ? <Save size={16} aria-hidden="true" />
                      : <FilePlus2 size={16} aria-hidden="true" />}
                  {isSaving ? "Сохранение..." : form.id ? "Сохранить шаблон" : "Создать шаблон"}
                </button>
              )}
            </div>
          </form>

          <TemplatePreview form={form} />
        </div>
      </article>

      {deleteCandidate && (
        <ConfirmDialog
          title="Удалить шаблон?"
          description={`Шаблон «${deleteCandidate.name}» будет удален из библиотеки. Тесты сохранят уже выбранный снимок его настроек.`}
          confirmLabel="Удалить"
          isBusy={isDeleting}
          onCancel={() => setDeleteCandidate(null)}
          onConfirm={confirmDelete}
        />
      )}
    </section>
  );
}

export default FeedbackTemplatesPage;
