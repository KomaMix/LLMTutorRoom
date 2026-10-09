export const feedbackToneOptions = [
  { value: "supportive", label: "Поддерживающий" },
  { value: "neutral", label: "Нейтральный" },
  { value: "formal", label: "Академический" },
  { value: "direct", label: "Прямой" }
];

export const feedbackDetailOptions = [
  { value: "concise", label: "Кратко" },
  { value: "standard", label: "Стандартно" },
  { value: "detailed", label: "Подробно" }
];

export const feedbackDisclosureOptions = [
  {
    value: "hints-only",
    label: "Только подсказки",
    description: "Модель направляет ученика, не раскрывая правильный ответ."
  },
  {
    value: "explain-approach",
    label: "Объяснить подход",
    description: "Модель объясняет ошибки и способ исправления без готового полного ответа."
  },
  {
    value: "may-show-answer",
    label: "Можно раскрыть ответ",
    description: "Модель может показать правильный ответ или пример решения."
  }
];

export function createSection() {
  return {
    id: crypto.randomUUID(),
    title: "Новый раздел",
    instruction: "Опиши полезную для ученика часть обратной связи.",
    format: "text",
    maxItems: 3,
    isRequired: true
  };
}

export function createBlankTemplateForm() {
  return {
    id: "",
    name: "",
    description: "",
    tone: "supportive",
    detailLevel: "standard",
    answerDisclosure: "explain-approach",
    customInstruction: "",
    sections: [createSection()],
    revision: 0,
    isSystem: false
  };
}

export function createTemplateForm(template) {
  if (!template) {
    return createBlankTemplateForm();
  }

  return {
    ...template,
    sections: (template.sections ?? []).map(section => ({ ...section }))
  };
}

export function createTemplateCopyForm(template) {
  return {
    ...createTemplateForm(template),
    id: "",
    name: `Копия — ${template.name}`,
    revision: 0,
    isSystem: false
  };
}

export function createTemplatePayload(form) {
  return {
    name: form.name,
    description: form.description,
    tone: form.tone,
    detailLevel: form.detailLevel,
    answerDisclosure: form.answerDisclosure,
    customInstruction: form.customInstruction,
    sections: form.sections.map(section => ({
      id: section.id,
      title: section.title,
      instruction: section.instruction,
      format: section.format,
      maxItems: section.format === "list" ? Number(section.maxItems) : 1,
      isRequired: section.isRequired
    })),
    revision: form.revision ?? 0
  };
}

export function validateTemplateForm(form) {
  if (!form.name.trim()) {
    return "Укажи название шаблона.";
  }

  if (form.name.trim().length > 120) {
    return "Название шаблона не должно превышать 120 символов.";
  }

  if (form.description.trim().length > 500) {
    return "Описание шаблона не должно превышать 500 символов.";
  }

  if (form.customInstruction.trim().length > 2000) {
    return "Дополнительная инструкция не должна превышать 2000 символов.";
  }

  if (form.sections.length < 1 || form.sections.length > 6) {
    return "В шаблоне должно быть от 1 до 6 разделов.";
  }

  for (const section of form.sections) {
    if (!section.title.trim()) {
      return "Укажи название каждого раздела.";
    }

    if (!section.instruction.trim()) {
      return `Добавь инструкцию для раздела «${section.title}».`;
    }

    if (section.format === "list"
      && (Number(section.maxItems) < 1 || Number(section.maxItems) > 5)) {
      return "В списке может быть от 1 до 5 пунктов.";
    }
  }

  return "";
}

export function getOptionLabel(options, value) {
  return options.find(option => option.value === value)?.label ?? value;
}
