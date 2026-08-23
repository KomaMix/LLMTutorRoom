import {
  endOfLocalDayToIso,
  toInputDate
} from "../../shared/lib/dates.js";

export const taskTypes = [
  { value: "single-choice", label: "Один ответ" },
  { value: "multiple-choice", label: "Несколько ответов" },
  { value: "free-text", label: "Письменный ответ" }
];

export const taskCheckModes = [
  { value: "llm", label: "LLM" },
  { value: "manual", label: "Вручную" }
];

export function createInitialTestForm(defaultModelKey = "") {
  return {
    title: "",
    subject: "",
    summary: "",
    deadline: toInputDate(new Date(Date.now() + 7 * 24 * 60 * 60 * 1000)),
    timeLimitMinutes: 45,
    llmModelKey: defaultModelKey
  };
}

export function createInitialTaskForm() {
  return {
    type: "single-choice",
    title: "",
    prompt: "",
    checkMode: "auto",
    maxPoints: 1,
    wrongAnswerPenalty: 0,
    options: ["", "", "", ""],
    correctOptionIndexes: []
  };
}

export function createTestFormFromTest(test) {
  if (!test) {
    return createInitialTestForm();
  }

  return {
    title: test.title,
    subject: test.subject,
    summary: test.summary,
    deadline: toInputDate(test.deadline),
    timeLimitMinutes: test.timeLimitMinutes,
    llmModelKey: test.llmModelKey ?? ""
  };
}

export function createTestPayload(form) {
  return {
    title: form.title,
    subject: form.subject,
    summary: form.summary,
    deadline: endOfLocalDayToIso(form.deadline),
    timeLimitMinutes: Number(form.timeLimitMinutes),
    llmModelKey: form.llmModelKey
  };
}

export function createTaskFormFromTask(task) {
  return {
    type: task.type,
    title: task.title,
    prompt: task.prompt,
    checkMode: task.checkMode ?? (task.type === "free-text" ? "llm" : "auto"),
    maxPoints: task.maxPoints,
    wrongAnswerPenalty: task.wrongAnswerPenalty ?? 0,
    options: task.options.length === 0 ? ["", ""] : task.options.map(option => option.text),
    correctOptionIndexes: task.options
      .map((option, index) => task.correctOptionIds.includes(option.id) ? index : -1)
      .filter(index => index >= 0)
  };
}

export function createTaskPayload(form, hasLlmModel = true) {
  const isChoiceTask = form.type !== "free-text";
  const choiceData = getChoiceTaskData(form);

  return {
    type: form.type,
    checkMode: isChoiceTask ? "auto" : hasLlmModel ? form.checkMode : "manual",
    title: form.title,
    prompt: form.prompt,
    maxPoints: Number(form.maxPoints),
    wrongAnswerPenalty: form.type === "multiple-choice"
      ? Number(form.wrongAnswerPenalty || 0)
      : 0,
    options: isChoiceTask ? choiceData.options : [],
    correctOptionIndexes: isChoiceTask ? choiceData.correctOptionIndexes : []
  };
}

export function getModelOptions(models, currentModelKey) {
  if (!currentModelKey || models.some(model => model.key === currentModelKey)) {
    return models;
  }

  return [
    ...models,
    {
      key: currentModelKey,
      displayName: currentModelKey,
      remainingChecks: 0,
      maxChecks: 0
    }
  ];
}

export function getModelOptionLabel(model) {
  const name = model.displayName || model.key;
  if (typeof model.remainingChecks !== "number" || typeof model.maxChecks !== "number") {
    return name;
  }

  return `${name} (${model.remainingChecks}/${model.maxChecks})`;
}

export function validateTaskForm(form) {
  const choiceData = getChoiceTaskData(form);

  if (!form.title.trim()) {
    return "Укажи название задания.";
  }

  if (!form.prompt.trim()) {
    return "Укажи текст задания.";
  }

  if (Number(form.maxPoints) <= 0) {
    return "Максимальный балл должен быть больше нуля.";
  }

  if (Number(form.wrongAnswerPenalty || 0) < 0) {
    return "Штраф не может быть отрицательным.";
  }

  if (form.type === "free-text") {
    return "";
  }

  if (choiceData.options.length < 2) {
    return "Добавь минимум два варианта ответа.";
  }

  if (form.type === "single-choice" && choiceData.correctOptionIndexes.length !== 1) {
    return "Для задания с одним ответом выбери один правильный вариант.";
  }

  if (form.type === "multiple-choice" && choiceData.correctOptionIndexes.length === 0) {
    return "Для задания с несколькими ответами выбери хотя бы один правильный вариант.";
  }

  return "";
}

export function getChoiceTaskData(taskForm) {
  const optionIndexMap = new Map();
  const options = [];

  taskForm.options.forEach((option, index) => {
    const text = option.trim();
    if (!text) {
      return;
    }

    optionIndexMap.set(index, options.length);
    options.push(text);
  });

  return {
    options,
    correctOptionIndexes: [...new Set(taskForm.correctOptionIndexes
      .map(index => optionIndexMap.get(index))
      .filter(index => index !== undefined))]
  };
}

export function ensureChoiceOptions(options) {
  if (options.length >= 2) {
    return options;
  }

  return [...options, ...Array.from({ length: 2 - options.length }, () => "")];
}

export function createTestListSummary(test) {
  const visibleTaskCount = test.tasks.filter(task => !task.isHidden).length;
  const hiddenTaskCount = test.tasks.length - visibleTaskCount;
  const hiddenText = hiddenTaskCount > 0
    ? ` · скрыто ${hiddenTaskCount}`
    : "";

  return `Версия ${test.versionNumber} · ${test.subject} · ${visibleTaskCount} заданий${hiddenText} · ${test.timeLimitMinutes} мин`;
}

export function createTaskCountSummary(test) {
  const visibleTaskCount = test.tasks.filter(task => !task.isHidden).length;
  const hiddenTaskCount = test.tasks.length - visibleTaskCount;

  if (hiddenTaskCount === 0) {
    return visibleTaskCount;
  }

  return `${visibleTaskCount} + ${hiddenTaskCount} скрыто`;
}
