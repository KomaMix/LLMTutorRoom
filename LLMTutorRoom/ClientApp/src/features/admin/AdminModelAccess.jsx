import { useCallback, useEffect, useRef, useState } from "react";
import { Loader2, Pencil, Save, Server, ShieldCheck, Trash2 } from "lucide-react";
import { getTeachers } from "../../api/usersApi.js";
import {
  deleteTeacherModelAccess,
  getModelCatalog,
  getTeacherModelAccess,
  saveTeacherModelAccess
} from "../../api/modelAccessApi.js";
import { ConfirmDialog } from "../../shared/ui/ConfirmDialog.jsx";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";
import { formatPeriod } from "../../shared/lib/dates.js";
import { getRequestErrorMessage } from "./requestError.js";

const initialAccessForm = {
  modelKey: "",
  isEnabled: true,
  periodSeconds: 30 * 24 * 60 * 60,
  maxChecks: 100
};

export function AdminModelAccess() {
  const [teachers, setTeachers] = useState([]);
  const [models, setModels] = useState([]);
  const [accessList, setAccessList] = useState([]);
  const [selectedTeacherId, setSelectedTeacherId] = useState("");
  const [loadedTeacherId, setLoadedTeacherId] = useState("");
  const [form, setForm] = useState(initialAccessForm);
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

  const selectTeacher = useCallback(teacherId => {
    accessRequestVersionRef.current += 1;
    selectedTeacherIdRef.current = teacherId;
    setSelectedTeacherId(teacherId);
    setLoadedTeacherId("");
    setAccessList([]);
    setAccessError("");
    setActionMessage(null);
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

        setTeachers(teachersData);
        setModels(modelsData);
        selectTeacher(teachersData[0]?.id ?? "");
        setForm(current => ({
          ...current,
          modelKey: modelsData.some(model => model.key === current.modelKey)
            ? current.modelKey
            : modelsData[0]?.key ?? ""
        }));
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

    const periodSeconds = Number(form.periodSeconds);
    const maxChecks = Number(form.maxChecks);
    if (!Number.isInteger(periodSeconds) || periodSeconds < 1
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
        setActionMessage({ type: "success", text: "Доступ сохранен." });
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
        setActionMessage({ type: "success", text: "Доступ удален." });
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
  }

  function editAccess(access) {
    if (!canMutateAccess) {
      return;
    }

    setForm({
      modelKey: access.modelKey,
      isEnabled: access.isEnabled,
      periodSeconds: access.periodSeconds,
      maxChecks: access.maxChecks
    });
    setActionMessage(null);
  }

  const isAccessCurrent = Boolean(selectedTeacherId)
    && loadedTeacherId === selectedTeacherId
    && !isAccessLoading
    && !accessError;
  const isMutationLocked = isSaving
    || Boolean(busyModelKey)
    || Boolean(pendingDeleteAccess);
  const isAccessPayloadDisabled = isMutationLocked || !isAccessCurrent;
  const canMutateAccess = isAccessCurrent
    && !isMutationLocked;
  const selectedTeacher = teachers.find(teacher => teacher.id === selectedTeacherId);

  return (
    <>
      <section className="admin-layout">
        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Лимит модели</h2>
            </div>
            <Server size={18} aria-hidden="true" />
          </div>

          {isLoading ? (
            <p className="muted">Загрузка моделей.</p>
          ) : initialError ? (
            <div className="stack">
              <p className="form-error" role="alert">{initialError}</p>
              <button type="button" className="button secondary" onClick={retryInitialLoad}>
                Повторить загрузку
              </button>
            </div>
          ) : (
            <form className="login-form" onSubmit={handleSaveAccess}>
              <div className="field">
                <label htmlFor="access-teacher">Преподаватель</label>
                <select
                  id="access-teacher"
                  value={selectedTeacherId}
                  onChange={event => selectTeacher(event.target.value)}
                  disabled={teachers.length === 0 || isMutationLocked}
                >
                  {teachers.map(teacher => (
                    <option key={teacher.id} value={teacher.id}>
                      {teacher.displayName}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label htmlFor="access-model">Модель</label>
                <select
                  id="access-model"
                  value={form.modelKey}
                  onChange={event => updateForm("modelKey", event.target.value)}
                  disabled={models.length === 0 || isAccessPayloadDisabled}
                >
                  {models.map(model => (
                    <option key={model.key} value={model.key}>
                      {model.displayName || model.key}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-row">
                <div className="field">
                  <label htmlFor="access-period">Период, секунд</label>
                  <input
                    id="access-period"
                    min="1"
                    step="1"
                    type="number"
                    value={form.periodSeconds}
                    onChange={event => updateForm("periodSeconds", event.target.value)}
                    disabled={isAccessPayloadDisabled}
                    required
                  />
                </div>
                <div className="field">
                  <label htmlFor="access-limit">Проверок</label>
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
              </div>
              <label className="inline-toggle">
                <input
                  type="checkbox"
                  checked={form.isEnabled}
                  onChange={event => updateForm("isEnabled", event.target.checked)}
                  disabled={isAccessPayloadDisabled}
                />
                Доступ включен
              </label>

              {teachers.length === 0 && (
                <p className="form-note">Сначала добавьте преподавателя.</p>
              )}
              {models.length === 0 && (
                <p className="form-note">Каталог моделей пуст.</p>
              )}
              {actionMessage && (
                <p
                  className={actionMessage.type === "error" ? "form-error" : "form-note"}
                  role={actionMessage.type === "error" ? "alert" : "status"}
                >
                  {actionMessage.text}
                </p>
              )}

              <button
                type="submit"
                className="button primary"
                disabled={!canMutateAccess || !form.modelKey}
              >
                {isSaving ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Save size={16} aria-hidden="true" />}
                {isSaving ? "Сохранение..." : isAccessLoading ? "Загрузка лимитов..." : "Сохранить лимит"}
              </button>
            </form>
          )}
        </div>

        <div className="panel">
          <div className="panel-header">
            <div>
              <span className="eyebrow">{selectedTeacher?.userName ?? "teacher"}</span>
              <h2>Выданный доступ</h2>
            </div>
            <ShieldCheck size={18} aria-hidden="true" />
          </div>

          {!selectedTeacherId ? (
            <p className="muted">Выберите преподавателя.</p>
          ) : isAccessLoading ? (
            <p className="muted">Загрузка лимитов преподавателя.</p>
          ) : accessError ? (
            <div className="stack">
              <p className="form-error" role="alert">{accessError}</p>
              <button type="button" className="button secondary" onClick={retryAccessLoad}>
                Повторить загрузку
              </button>
            </div>
          ) : accessList.length === 0 ? (
            <p className="muted">Доступ к моделям пока не выдан.</p>
          ) : (
            <div className="review-table">
              {accessList.map(access => (
                <article className="review-row" key={access.modelKey}>
                  <div>
                    <strong>{access.displayName || access.modelKey}</strong>
                    <span>{access.remainingChecks} из {access.maxChecks} проверок, период {formatPeriod(access.periodSeconds)}</span>
                  </div>
                  <div className="row-actions">
                    <StatusBadge
                      status={access.isEnabled && access.hasEnabledDeployment ? "available" : "standby"}
                    />
                    <button
                      type="button"
                      className="icon-button"
                      title="Редактировать"
                      disabled={!canMutateAccess}
                      onClick={() => editAccess(access)}
                    >
                      <Pencil size={16} aria-hidden="true" />
                    </button>
                    <button
                      type="button"
                      className="icon-button danger"
                      title="Удалить доступ"
                      disabled={!canMutateAccess || busyModelKey === access.modelKey}
                      onClick={() => requestRemoveAccess(access)}
                    >
                      {busyModelKey === access.modelKey
                        ? <Loader2 className="spin" size={16} aria-hidden="true" />
                        : <Trash2 size={16} aria-hidden="true" />}
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </div>
      </section>

      {pendingDeleteAccess && (
        <ConfirmDialog
          title="Удалить доступ к модели?"
          description={`Доступ «${pendingDeleteAccess.displayName || pendingDeleteAccess.modelKey}» для ${selectedTeacher?.displayName ?? "преподавателя"} будет удален.`}
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
