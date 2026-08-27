const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function normalizeAnswer(value) {
  if (value == null) {
    return "";
  }

  return String(value).trim();
}

function isGuid(value) {
  return guidPattern.test(value);
}

export function formatReviewAnswer(studentAnswer, answerOptions) {
  const fallbackAnswer = studentAnswer == null ? "" : String(studentAnswer);
  const normalizedAnswer = fallbackAnswer.trim();
  if (!normalizedAnswer) {
    return "";
  }

  const answerIds = normalizedAnswer
    .split("|")
    .map(answerId => answerId.trim())
    .filter(Boolean);

  if (!Array.isArray(answerOptions) || answerOptions.length === 0) {
    if (answerIds.length > 0 && answerIds.every(isGuid)) {
      return answerIds.length === 1
        ? "Выбранный вариант больше недоступен."
        : "Выбранные варианты больше недоступны.";
    }

    return fallbackAnswer;
  }

  const optionTextById = new Map(answerOptions
    .filter(option => option && option.id != null)
    .map(option => [String(option.id).trim().toLowerCase(), normalizeAnswer(option.text)]));

  return answerIds
    .map(answerId => {
      const optionText = optionTextById.get(answerId.toLowerCase());
      if (optionText) {
        return optionText;
      }

      return isGuid(answerId) ? "Недоступный вариант" : answerId;
    })
    .join(", ");
}
