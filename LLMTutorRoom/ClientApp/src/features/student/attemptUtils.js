const attemptStatusText = {
  "in-progress": "Выполняется",
  submitted: "Отправлено",
  expired: "Время вышло"
};

export function getEffectiveAttemptStatus(attempt, remainingSeconds) {
  if (!attempt) {
    return null;
  }

  if (attempt.status === "in-progress" && remainingSeconds <= 0) {
    return "expired";
  }

  return attempt.status;
}

export function getAttemptStatusText(status) {
  return attemptStatusText[status] ?? status;
}

export function getReviewStatusMessage(status) {
  if (status === "manual-review") {
    return "Проверка ожидает преподавателя.";
  }

  if (status === "paused") {
    return "Автоматическая проверка временно приостановлена. Она продолжится позже.";
  }

  if (status === "failed") {
    return "Проверка остановлена и требует внимания преподавателя.";
  }

  return "Проверка выполняется. Результат появится после завершения.";
}
