import { useCallback, useEffect, useRef, useState } from "react";
import {
  Bot,
  CalendarClock,
  Gauge,
  Loader2,
  Pencil,
  Save,
  Server,
  ShieldCheck,
  Trash2,
  UserRound,
  X
} from "lucide-react";
import { getTeachers } from "../../api/usersApi.js";
import {
  deleteTeacherModelAccess,
  getModelCatalog,
  getTeacherModelAccess,
  saveTeacherModelAccess
} from "../../api/modelAccessApi.js";
import { ConfirmDialog } from "../../shared/ui/ConfirmDialog.jsx";
import { formatDate, formatPeriod } from "../../shared/lib/dates.js";
import { useUnsavedChangesGuard } from "../../shared/hooks/useUnsavedChangesGuard.js";
import { getRequestErrorMessage } from "./requestError.js";

const initialAccessForm = {
  modelKey: "",
  isEnabled: true,
  periodValue: 30,
  periodUnit: "days",
  maxChecks: 100
};

const periodUnitSeconds = {
  days: 24 * 60 * 60,
  hours: 60 * 60,
  minutes: 60,
  seconds: 1
};

const unsavedAccessPrompt = "Есть несохранённые настройки доступа. Потерять изменения?";

function createPeriodForm(seconds) {
  const safeSeconds = Math.max(1, Number(seconds) || 1);
  const matchingUnit = Object.entries(periodUnitSeconds)
    .find(([, multiplier]) => Number.isInteger(safeSeconds / multiplier));

  return {
    periodValue: safeSeconds / (matchingUnit?.[1] ?? 1),
    periodUnit: matchingUnit?.[0] ?? "seconds"
  };
}

function getPeriodSeconds(form) {
  return Number(form.periodValue) * (periodUnitSeconds[form.periodUnit] ?? 1);
}

function serializeAccessForm(form) {
  return JSON.stringify({
    modelKey: form.modelKey,
    isEnabled: form.isEnabled,
    periodSeconds: getPeriodSeconds(form),
    maxChecks: Number(form.maxChecks)
  });
}

export function AdminModelAccess() {
  const [teachers, setTeachers] = useState([]);
  const [models, setModels] = useState([]);
  const [accessList, setAccessList] = useState([]);
  const [selectedTeacherId, setSelectedTeacherId] = useState("");
  const [loadedTeacherId, setLoadedTeacherId] = useState("");
  const [form, setForm] = useState(initialAccessForm);
  const [editingModelKey, setEditingModelKey] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isAccessLoading, setIsAccessLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [busyModelKey, setBusyModelKey] = useState("");
  const [pendingDeleteAccess, setPendingDeleteAccess] = useState(null);
  const [initialError, setInitialError] = useState("");
  const [accessError, setAccessError] = useState("");
  const [actionMessage, setActionMessage] = useState(null);
  const [initialLoadVersion, setInitialLoadVersion] = useState(0);
  const [accessLoadVersion, setAccessLoadVersion] = useState(0);
  const selectedTeacherIdRef = useRef("");
  const accessRequestVersionRef = useRef(0);
  const editorRef = useRef(null);
  const actionBannerRef = useRef(null);
  const focusActionBannerAfterDeleteRef = useRef(false);
  const formRef = useRef(form);
  const formBaselineRef = useRef(form);
  formRef.current = form;
  const hasUnsavedAccessChanges = serializeAccessForm(form)
    !== serializeAccessForm(formBaselineRef.current);
  const hasUnsavedAccessChangesRef = useRef(hasUnsavedAccessChanges);
  hasUnsavedAccessChangesRef.current = hasUnsavedAccessChanges;
  useUnsavedChangesGuard(hasUnsavedAccessChanges, unsavedAccessPrompt);

  useEffect(() => {
    if (pendingDeleteAccess || !actionMessage || !focusActionBannerAfterDeleteRef.current) {
      return;
    }

    focusActionBannerAfterDeleteRef.current = false;
    actionBannerRef.current?.focus();
  }, [actionMessage, pendingDeleteAccess]);

  const selectTeacher = useCallback(teacherId => {
    if (teacherId !== selectedTeacherIdRef.current
      && hasUnsavedAccessChangesRef.current
      && !window.confirm(unsavedAccessPrompt)) {
      return;
    }

    accessRequestVersionRef.current += 1;
    selectedTeacherIdRef.current = teacherId;
    setSelectedTeacherId(teacherId);
    setLoadedTeacherId("");
    setAccessList([]);
    setAccessError("");
    setActionMessage(null);
    setEditingModelKey("");
    const nextForm = {
      ...initialAccessForm,
      modelKey: ""
    };
    formBaselineRef.current = nextForm;
    setForm(nextForm);
    setIsAccessLoading(Boolean(teacherId));
  }, []);
  const cancelDelete = useCallback(() => setPendingDeleteAccess(null), []);

  useEffect(() => {
    const controller = new AbortController();

    async function loadInitialData() {
      setIsLoading(true);
      setInitialError("");

      try {
        const [teachersData, modelsData] = await Promise.all([
          getTeachers({ signal: controller.signal }),
          getModelCatalog({ signal: controller.signal })
        ]);

        if (!Array.isArray(teachersData) || !Array.isArray(modelsData)) {
          throw new Error("Model access response must contain arrays.");
        }

        if (controller.signal.aborted) {
          return;
        }

        const sortedTeachers = [...teachersData]
          .sort((left, right) => left.userName.localeCompare(right.userName, "ru"));
        setTeachers(sortedTeachers);
        setModels(modelsData);
        selectTeacher(sortedTeachers[0]?.id ?? "");
        setForm(current => {
          const nextForm = {
            ...current,
            modelKey: modelsData.some(model => model.key === current.modelKey)
              ? current.modelKey
              : ""
          };
          formBaselineRef.current = nextForm;
          return nextForm;
        });
      } catch (error) {
        if (!controller.signal.aborted) {
          setInitialError(getRequestErrorMessage(error, "Не удалось загрузить преподавателей и каталог моделей."));
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      }
    }

    loadInitialData();

    return () => controller.abort();
  }, [initialLoadVersion, selectTeacher]);

  useEffect(() => {
    if (!selectedTeacherId) {
      setIsAccessLoading(false);
      return undefined;
    }

    const controller = new AbortController();
    const teacherId = selectedTeacherId;
    const requestVersion = ++accessRequestVersionRef.current;

    async function loadAccess() {
      setIsAccessLoading(true);
      setAccessError("");
      setLoadedTeacherId("");
      setAccessList([]);

      try {
        const data = await getTeacherModelAccess(teacherId, { signal: controller.signal });

        if (!Array.isArray(data)) {
          throw new Error("Teacher model access response must be an array.");
        }

        if (isCurrentSelection(teacherId, requestVersion)) {
          setAccessList(data);
          setLoadedTeacherId(teacherId);
          const selectedAccess = data.find(access => access.modelKey === formRef.current.modelKey);
          if (selectedAccess) {
            const nextForm = {
              modelKey: selectedAccess.modelKey,
              isEnabled: selectedAccess.isEnabled,
              ...createPeriodForm(selectedAccess.periodSeconds),
              maxChecks: selectedAccess.maxChecks
            };
            formBaselineRef.current = nextForm;
            setForm(nextForm);
            setEditingModelKey(selectedAccess.modelKey);
          } else {
            const nextForm = {
              ...initialAccessForm,
              modelKey: ""
            };
            formBaselineRef.current = nextForm;
            setForm(nextForm);
            setEditingModelKey("");
          }
        }
      } catch (error) {
        if (!controller.signal.aborted && isCurrentSelection(teacherId, requestVersion)) {
          setAccessError(getRequestErrorMessage(error, "Не удалось загрузить лимиты преподавателя."));
        }
      } finally {
        if (isCurrentSelection(teacherId, requestVersion)) {
          setIsAccessLoading(false);
        }
      }
    }

    loadAccess();

    return () => controller.abort();
  }, [accessLoadVersion, selectedTeacherId]);

  function isCurrentSelection(teacherId, requestVersion) {
    return selectedTeacherIdRef.current === teacherId
      && accessRequestVersionRef.current === requestVersion;
  }

  function retryInitialLoad() {
    selectTeacher("");
    setTeachers([]);
    setModels([]);
    setInitialLoadVersion(current => current + 1);
  }

  function retryAccessLoad() {
    accessRequestVersionRef.current += 1;
    setLoadedTeacherId("");
    setAccessList([]);
    setAccessError("");
    setIsAccessLoading(true);
    setAccessLoadVersion(current => current + 1);
  }

  async function handleSaveAccess(event) {
    event.preventDefault();

    const teacherId = selectedTeacherId;
    const requestVersion = accessRequestVersionRef.current;
    if (!canMutateAccess || !teacherId || !form.modelKey) {
      return;
    }

    const periodSeconds = getPeriodSeconds(form);
    const maxChecks = Number(form.maxChecks);
    if (!Number.isInteger(periodSeconds) || periodSeconds < 1
      || periodSeconds > 2147483647
      || !Number.isInteger(maxChecks) || maxChecks < 1) {
      setActionMessage({
        type: "error",
        text: "Период и количество проверок должны быть целыми числами больше нуля."
      });
      return;
    }

    const modelKey = form.modelKey;
    setIsSaving(true);
    setActionMessage(null);

    try {
      const access = await saveTeacherModelAccess(teacherId, modelKey, {
        isEnabled: form.isEnabled,
        periodSeconds,
        maxChecks
      });

      if (isCurrentSelection(teacherId, requestVersion)) {
        setAccessList(current => [
          access,
          ...current.filter(item => item.modelKey !== access.modelKey)
        ].sort((left, right) => left.modelKey.localeCompare(right.modelKey)));
        const savedForm = {
          modelKey: access.modelKey,
          isEnabled: access.isEnabled,
          ...createPeriodForm(access.periodSeconds),
          maxChecks: access.maxChecks
        };
        formBaselineRef.current = savedForm;
        setForm(savedForm);
        setEditingModelKey(access.modelKey);
        setActionMessage({ type: "success", text: "Доступ сохранён." });
      }
    } catch (error) {
      if (isCurrentSelection(teacherId, requestVersion)) {
        setActionMessage({
          type: "error",
          text: getRequestErrorMessage(error, "Не удалось сохранить доступ.")
        });
      }
    } finally {
      setIsSaving(false);
    }
  }

  function requestRemoveAccess(access) {
    if (!canMutateAccess) {
      return;
    }

    setPendingDeleteAccess(access);
  }

  async function confirmRemoveAccess() {
    const access = pendingDeleteAccess;
    const teacherId = selectedTeacherId;
    const requestVersion = accessRequestVersionRef.current;
    if (!access || !isAccessCurrent || isSaving || busyModelKey || !teacherId) {
      return;
    }

    setBusyModelKey(access.modelKey);
    setActionMessage(null);

    try {
      await deleteTeacherModelAccess(teacherId, access.modelKey);

      if (isCurrentSelection(teacherId, requestVersion)) {
        setAccessList(current => current.filter(item => item.modelKey !== access.modelKey));
        if (editingModelKey === access.modelKey) {
          const nextForm = {
            ...initialAccessForm,
            modelKey: ""
          };
          formBaselineRef.current = nextForm;
          setEditingModelKey("");
          setForm(nextForm);
        }
        focusActionBannerAfterDeleteRef.current = true;
        setActionMessage({ type: "success", text: "Доступ удалён." });
      }
    } catch (error) {
      if (isCurrentSelection(teacherId, requestVersion)) {
        setActionMessage({
          type: "error",
          text: getRequestErrorMessage(error, "Не удалось удалить доступ.")
        });
      }
    } finally {
      setBusyModelKey("");
      setPendingDeleteAccess(null);
    }
  }

  function updateForm(field, value) {
    setForm(current => ({ ...current, [field]: value }));
    setActionMessage(null);
  }

  function selectModel(modelKey) {
    if (modelKey !== formRef.current.modelKey
      && hasUnsavedAccessChangesRef.current
      && !window.confirm(unsavedAccessPrompt)) {
      return;
    }

    const existingAccess = accessList.find(access => access.modelKey === modelKey);
    if (existingAccess) {
      const nextForm = {
        modelKey: existingAccess.modelKey,
        isEnabled: existingAccess.isEnabled,
        ...createPeriodForm(existingAccess.periodSeconds),
        maxChecks: existingAccess.maxChecks
      };
      formBaselineRef.current = nextForm;
      setForm(nextForm);
      setEditingModelKey(existingAccess.modelKey);
    } else {
      const nextForm = {
        ...initialAccessForm,
        modelKey
      };
      formBaselineRef.current = nextForm;
      setForm(nextForm);
      setEditingModelKey("");
    }
    setActionMessage(null);
  }

  function editAccess(access) {
    if (!canMutateAccess) {
      return;
    }

    if (access.modelKey !== formRef.current.modelKey
      && hasUnsavedAccessChangesRef.current
      && !window.confirm(unsavedAccessPrompt)) {
      return;
    }

    const nextForm = {
      modelKey: access.modelKey,
      isEnabled: access.isEnabled,
      ...createPeriodForm(access.periodSeconds),
      maxChecks: access.maxChecks
    };
    formBaselineRef.current = nextForm;
    setForm(nextForm);
    setEditingModelKey(access.modelKey);
    setActionMessage(null);
    window.requestAnimationFrame(() => {
      const editor = editorRef.current;
      const reduceMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
      editor?.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth", block: "start" });
      editor?.querySelector("#access-model")?.focus({ preventScroll: true });
    });
  }

  function cancelEdit() {
    const nextForm = {
      ...initialAccessForm,
      modelKey: ""
    };
    formBaselineRef.current = nextForm;
    setEditingModelKey("");
    setForm(nextForm);
    setActionMessage(null);
  }

  const isAccessCurrent = Boolean(selectedTeacherId)
    && loadedTeacherId === selectedTeacherId
    && !isAccessLoading
    && !accessError;
  const isMutationLocked = isSaving
    || Boolean(busyModelKey)
    || Boolean(pendingDeleteAccess);
  const isModelSelectDisabled = isMutationLocked
    || !isAccessCurrent
    || models.length === 0;
  const isAccessPayloadDisabled = isModelSelectDisabled || !form.modelKey;
  const canMutateAccess = isAccessCurrent
    && !isMutationLocked;
  const selectedTeacher = teachers.find(teacher => teacher.id === selectedTeacherId);
  const selectedModel = models.find(model => model.key === form.modelKey);

  return (
    <>
      <section className="admin-page admin-model-access-page">
        <header className="admin-page-heading">
          <div className="admin-page-heading-copy">
            <span className="admin-page-icon">
              <Server size={21} aria-hidden="true" />
            </span>
            <div>
              <h2>Доступ и квоты</h2>
              <p>Назначайте преподавателям модели и ограничивайте количество автоматических проверок.</p>
            </div>
          </div>
          <div className="admin-page-summary-group">
            <div className="admin-page-summary">
              <strong>{isLoading ? "—" : teachers.length}</strong>
              <span>преподавателей</span>
            </div>
            <div className="admin-page-summary">
              <strong>{isLoading ? "—" : models.length}</strong>
              <span>моделей</span>
            </div>
          </div>
        </header>

        {isLoading ? (
          <div className="panel admin-panel admin-state admin-page-state" role="status">
            <Loader2 className="spin" size={23} aria-hidden="true" />
            <strong>Загружаем каталог и преподавателей</strong>
            <span>Подготавливаем данные для настройки доступов.</span>
          </div>
        ) : initialError ? (
          <div className="panel admin-panel admin-state admin-page-state error">
            <Server size={23} aria-hidden="true" />
            <strong>Данные не загрузились</strong>
            <div>
              <p className="form-error" role="alert">{initialError}</p>
              <button type="button" className="button secondary" onClick={retryInitialLoad}>
                Повторить загрузку
              </button>
            </div>
          </div>
        ) : (
          <>
            <section className="panel admin-panel admin-teacher-picker">
              <div className="admin-teacher-picker-copy">
                <span className="admin-section-icon">
                  <UserRound size={19} aria-hidden="true" />
                </span>
                <div>
                  <h2>Преподаватель</h2>
                  <p>
                    {selectedTeacher
                      ? `${selectedTeacher.userName} · ${selectedTeacher.email}`
                      : "Сначала создайте учётную запись преподавателя."}
                  </p>
                </div>
              </div>
              <div className="field admin-teacher-select">
                <label htmlFor="access-teacher">Выбрать преподавателя</label>
                <select
                  id="access-teacher"
                  value={selectedTeacherId}
                  onChange={event => selectTeacher(event.target.value)}
                  disabled={teachers.length === 0 || isMutationLocked}
                >
                  {teachers.length === 0 && <option value="">Нет преподавателей</option>}
                  {teachers.map(teacher => (
                    <option key={teacher.id} value={teacher.id}>
                      {teacher.userName} · {teacher.email}
                    </option>
                  ))}
                </select>
              </div>
            </section>

            {actionMessage && (
              <div
                ref={actionBannerRef}
                className={`admin-action-banner ${actionMessage.type}`}
                role={actionMessage.type === "error" ? "alert" : "status"}
                tabIndex={-1}
              >
                <ShieldCheck size={18} aria-hidden="true" />
                <p
                  className={actionMessage.type === "error" ? "form-error" : "form-note"}
                >
                  {actionMessage.text}
                </p>
              </div>
            )}

            <div className="admin-model-access-layout">
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
                  {isAccessCurrent && <span className="admin-count">{accessList.length}</span>}
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
                    <button type="button" className="button secondary" onClick={retryAccessLoad}>
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
                    {accessList.map(access => {
                      const maxChecks = Math.max(0, Number(access.maxChecks) || 0);
                      const remainingChecks = Math.max(0, Number(access.remainingChecks) || 0);
                      const usedChecks = Math.max(0, Number(access.usedChecks) || 0);
                      const accessState = access.isEnabled
                        ? { className: "active", label: "Доступ включён" }
                        : { className: "disabled", label: "Доступ выключен" };

                      return (
                        <article
                          className={`admin-access-card${editingModelKey === access.modelKey ? " editing" : ""}`}
                          key={access.modelKey}
                        >
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
                              onClick={() => editAccess(access)}
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
                              onClick={() => requestRemoveAccess(access)}
                            >
                              {busyModelKey === access.modelKey
                                ? <Loader2 className="spin" size={16} aria-hidden="true" />
                                : <Trash2 size={16} aria-hidden="true" />}
                            </button>
                          </footer>
                        </article>
                      );
                    })}
                  </div>
                )}
              </section>

              <aside ref={editorRef} className="panel admin-panel admin-access-editor">
                <header className="admin-section-header">
                  <span className="admin-section-icon">
                    <ShieldCheck size={19} aria-hidden="true" />
                  </span>
                  <div>
                    <h2>{editingModelKey ? "Изменить доступ" : "Настроить доступ"}</h2>
                    <p>
                      {editingModelKey
                        ? "Параметры выбранной модели загружены в форму."
                        : "Выберите модель и задайте квоту проверок."}
                    </p>
                  </div>
                  {editingModelKey && (
                    <button
                      type="button"
                      className="icon-button admin-editor-close"
                      title="Отменить редактирование"
                      aria-label="Отменить редактирование"
                      disabled={isMutationLocked}
                      onClick={cancelEdit}
                    >
                      <X size={16} aria-hidden="true" />
                    </button>
                  )}
                </header>

                <form className="admin-form" onSubmit={handleSaveAccess}>
                  <div className="field">
                    <label htmlFor="access-model">Модель</label>
                    <select
                      id="access-model"
                      value={form.modelKey}
                      onChange={event => selectModel(event.target.value)}
                      disabled={isModelSelectDisabled}
                    >
                      <option value="">
                        {models.length === 0 ? "Каталог моделей пуст" : "Выберите модель"}
                      </option>
                      {models.map(model => (
                        <option key={model.key} value={model.key}>
                          {model.displayName || model.key}
                          {model.hasEnabledDeployment ? "" : " — недоступна"}
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
                        onChange={event => updateForm("periodValue", event.target.value)}
                        disabled={isAccessPayloadDisabled}
                        required
                      />
                      <select
                        aria-label="Единица периода квоты"
                        value={form.periodUnit}
                        onChange={event => updateForm("periodUnit", event.target.value)}
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
                      onChange={event => updateForm("maxChecks", event.target.value)}
                      disabled={isAccessPayloadDisabled}
                      required
                    />
                  </div>

                  <label className="admin-switch">
                    <input
                      type="checkbox"
                      checked={Boolean(form.modelKey) && form.isEnabled}
                      onChange={event => updateForm("isEnabled", event.target.checked)}
                      disabled={isAccessPayloadDisabled}
                    />
                    <span aria-hidden="true" />
                    <div>
                      <strong>
                        {!form.modelKey
                          ? "Состояние доступа"
                          : form.isEnabled
                            ? "Доступ включён"
                            : "Доступ выключен"}
                      </strong>
                      <small>
                        {!form.modelKey
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
                    disabled={!canMutateAccess || !form.modelKey}
                  >
                    {isSaving
                      ? <Loader2 className="spin" size={16} aria-hidden="true" />
                      : <Save size={16} aria-hidden="true" />}
                    {isSaving
                      ? "Сохранение..."
                      : isAccessLoading
                        ? "Загрузка доступов..."
                        : editingModelKey
                          ? "Сохранить изменения"
                          : "Выдать доступ"}
                  </button>
                </form>
              </aside>
            </div>
          </>
        )}
      </section>

      {pendingDeleteAccess && (
        <ConfirmDialog
          title="Удалить доступ к модели?"
          description={`Доступ «${pendingDeleteAccess.displayName || pendingDeleteAccess.modelKey}» для ${selectedTeacher?.userName ?? "преподавателя"} будет удалён.`}
          confirmLabel="Удалить доступ"
          isBusy={busyModelKey === pendingDeleteAccess.modelKey}
          onCancel={cancelDelete}
          onConfirm={confirmRemoveAccess}
        />
      )}
    </>
  );
}

export default AdminModelAccess;
