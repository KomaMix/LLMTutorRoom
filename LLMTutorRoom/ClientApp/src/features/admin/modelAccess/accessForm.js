export const initialAccessForm = {
  modelKey: "",
  isEnabled: true,
  periodValue: 30,
  periodUnit: "days",
  maxChecks: 100
};

export const unsavedAccessPrompt = "Есть несохранённые настройки доступа. Потерять изменения?";

const periodUnitSeconds = {
  days: 24 * 60 * 60,
  hours: 60 * 60,
  minutes: 60,
  seconds: 1
};

export function createPeriodForm(seconds) {
  const safeSeconds = Math.max(1, Number(seconds) || 1);
  const matchingUnit = Object.entries(periodUnitSeconds)
    .find(([, multiplier]) => Number.isInteger(safeSeconds / multiplier));

  return {
    periodValue: safeSeconds / (matchingUnit?.[1] ?? 1),
    periodUnit: matchingUnit?.[0] ?? "seconds"
  };
}

export function getPeriodSeconds(form) {
  return Number(form.periodValue) * (periodUnitSeconds[form.periodUnit] ?? 1);
}

export function serializeAccessForm(form) {
  return JSON.stringify({
    modelKey: form.modelKey,
    isEnabled: form.isEnabled,
    periodSeconds: getPeriodSeconds(form),
    maxChecks: Number(form.maxChecks)
  });
}
