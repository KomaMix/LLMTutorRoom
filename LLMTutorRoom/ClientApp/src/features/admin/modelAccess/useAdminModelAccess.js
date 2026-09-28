import { useCallback, useEffect, useRef, useState } from "react";
import { getTeachers } from "../../../api/usersApi.js";
import {
  deleteTeacherModelAccess,
  getModelCatalog,
  getTeacherModelAccess,
  saveTeacherModelAccess
} from "../../../api/modelAccessApi.js";
import { useUnsavedChangesGuard } from "../../../shared/hooks/useUnsavedChangesGuard.js";
import { getRequestErrorMessage } from "../requestError.js";
import {
  createPeriodForm,
  getPeriodSeconds,
  initialAccessForm,
  serializeAccessForm,
  unsavedAccessPrompt
} from "./accessForm.js";

export function useAdminModelAccess() {
  const [teachers, setTeachers] = useState([]);
  const [models, setModels] = useState([]);
  const [accessList, setAccessList] = useState([]);
  const [selectedTeacherId, setSelectedTeacherId] = useState("");
  const [loadedTeacherId, setLoadedTeacherId] = useState("");
  const [form, setForm] = useState(initialAccessForm);
  const [editingModelId, setEditingModelId] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isAccessLoading, setIsAccessLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [busyModelId, setBusyModelId] = useState("");
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
      return false;
    }

    accessRequestVersionRef.current += 1;
    selectedTeacherIdRef.current = teacherId;
    setSelectedTeacherId(teacherId);
    setLoadedTeacherId("");
    setAccessList([]);
    setAccessError("");
    setActionMessage(null);
    setEditingModelId("");
    const nextForm = {
      ...initialAccessForm,
      modelId: ""
    };
    formBaselineRef.current = nextForm;
    setForm(nextForm);
    setIsAccessLoading(Boolean(teacherId));
    return true;
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
            modelId: modelsData.some(model => model.id === current.modelId)
              ? current.modelId
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
          const selectedAccess = data.find(access => access.modelId === formRef.current.modelId);
          if (selectedAccess) {
            const nextForm = {
              modelId: selectedAccess.modelId,
              isEnabled: selectedAccess.isEnabled,
              ...createPeriodForm(selectedAccess.periodSeconds),
              maxChecks: selectedAccess.maxChecks
            };
            formBaselineRef.current = nextForm;
            setForm(nextForm);
            setEditingModelId(selectedAccess.modelId);
          } else {
            const nextForm = {
              ...initialAccessForm,
              modelId: ""
            };
            formBaselineRef.current = nextForm;
            setForm(nextForm);
            setEditingModelId("");
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

  async function saveAccess(event) {
    event.preventDefault();

    const teacherId = selectedTeacherId;
    const requestVersion = accessRequestVersionRef.current;
    if (!canMutateAccess || !teacherId || !form.modelId) {
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

    const modelId = form.modelId;
    setIsSaving(true);
    setActionMessage(null);

    try {
      const access = await saveTeacherModelAccess(teacherId, modelId, {
        isEnabled: form.isEnabled,
        periodSeconds,
        maxChecks
      });

      if (isCurrentSelection(teacherId, requestVersion)) {
        setAccessList(current => [
          access,
          ...current.filter(item => item.modelId !== access.modelId)
        ].sort((left, right) => left.displayName.localeCompare(right.displayName, "ru")));
        const savedForm = {
          modelId: access.modelId,
          isEnabled: access.isEnabled,
          ...createPeriodForm(access.periodSeconds),
          maxChecks: access.maxChecks
        };
        formBaselineRef.current = savedForm;
        setForm(savedForm);
        setEditingModelId(access.modelId);
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
    if (!access || !isAccessCurrent || isSaving || busyModelId || !teacherId) {
      return;
    }

    setBusyModelId(access.modelId);
    setActionMessage(null);

    try {
      await deleteTeacherModelAccess(teacherId, access.modelId);

      if (isCurrentSelection(teacherId, requestVersion)) {
        setAccessList(current => current.filter(item => item.modelId !== access.modelId));
        if (editingModelId === access.modelId) {
          const nextForm = {
            ...initialAccessForm,
            modelId: ""
          };
          formBaselineRef.current = nextForm;
          setEditingModelId("");
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
      setBusyModelId("");
      setPendingDeleteAccess(null);
    }
  }

  function updateForm(field, value) {
    setForm(current => ({ ...current, [field]: value }));
    setActionMessage(null);
  }

  function selectModel(modelId) {
    if (modelId !== formRef.current.modelId
      && hasUnsavedAccessChangesRef.current
      && !window.confirm(unsavedAccessPrompt)) {
      return;
    }

    const existingAccess = accessList.find(access => access.modelId === modelId);
    if (existingAccess) {
      const nextForm = {
        modelId: existingAccess.modelId,
        isEnabled: existingAccess.isEnabled,
        ...createPeriodForm(existingAccess.periodSeconds),
        maxChecks: existingAccess.maxChecks
      };
      formBaselineRef.current = nextForm;
      setForm(nextForm);
      setEditingModelId(existingAccess.modelId);
    } else {
      const nextForm = {
        ...initialAccessForm,
        modelId
      };
      formBaselineRef.current = nextForm;
      setForm(nextForm);
      setEditingModelId("");
    }
    setActionMessage(null);
  }

  function editAccess(access) {
    if (!canMutateAccess) {
      return;
    }

    if (access.modelId !== formRef.current.modelId
      && hasUnsavedAccessChangesRef.current
      && !window.confirm(unsavedAccessPrompt)) {
      return;
    }

    const nextForm = {
      modelId: access.modelId,
      isEnabled: access.isEnabled,
      ...createPeriodForm(access.periodSeconds),
      maxChecks: access.maxChecks
    };
    formBaselineRef.current = nextForm;
    setForm(nextForm);
    setEditingModelId(access.modelId);
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
      modelId: ""
    };
    formBaselineRef.current = nextForm;
    setEditingModelId("");
    setForm(nextForm);
    setActionMessage(null);
  }

  const isAccessCurrent = Boolean(selectedTeacherId)
    && loadedTeacherId === selectedTeacherId
    && !isAccessLoading
    && !accessError;
  const isMutationLocked = isSaving
    || Boolean(busyModelId)
    || Boolean(pendingDeleteAccess);
  const isModelSelectDisabled = isMutationLocked
    || !isAccessCurrent
    || models.length === 0;
  const isAccessPayloadDisabled = isModelSelectDisabled || !form.modelId;
  const canMutateAccess = isAccessCurrent
    && !isMutationLocked;
  const selectedTeacher = teachers.find(teacher => teacher.id === selectedTeacherId);
  const selectedModel = models.find(model => model.id === form.modelId);

  return {
    accessError,
    accessList,
    actionBannerRef,
    actionMessage,
    busyModelId,
    cancelDelete,
    cancelEdit,
    canMutateAccess,
    confirmRemoveAccess,
    editingModelId,
    editorRef,
    form,
    initialError,
    isAccessCurrent,
    isAccessLoading,
    isAccessPayloadDisabled,
    isLoading,
    isModelSelectDisabled,
    isMutationLocked,
    isSaving,
    models,
    pendingDeleteAccess,
    requestRemoveAccess,
    retryAccessLoad,
    retryInitialLoad,
    saveAccess,
    selectModel,
    selectTeacher,
    selectedModel,
    selectedTeacher,
    selectedTeacherId,
    teachers,
    updateForm,
    editAccess
  };
}
