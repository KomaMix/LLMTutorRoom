export const gradingExampleLimits = {
  count: 5,
  taskPrompt: 4000,
  studentAnswer: 8000,
  feedback: 4000
};

export function createGradingExample(maxScore = 1) {
  return { taskPrompt: "", studentAnswer: "", score: 0, maxScore, feedback: "" };
}

export function copyGradingExamples(examples = []) {
  return (examples ?? []).map(example => ({ ...example }));
}

export function createGradingExamplesPayload(examples = []) {
  return examples.map(example => ({
    taskPrompt: example.taskPrompt?.trim() ?? "",
    studentAnswer: example.studentAnswer.trim(),
    score: Number(example.score),
    maxScore: Number(example.maxScore),
    feedback: example.feedback.trim()
  }));
}

export function validateGradingExamples(examples = [], requireTaskPrompt = false) {
  if (examples.length > gradingExampleLimits.count) {
    return `Можно добавить до ${gradingExampleLimits.count} примеров на каждом уровне.`;
  }

  for (const [index, example] of examples.entries()) {
    const prefix = `Пример ${index + 1}:`;
    if (requireTaskPrompt && !example.taskPrompt?.trim()) {
      return `${prefix} укажи условие задания.`;
    }
    if (!example.studentAnswer.trim() || !example.feedback.trim()) {
      return `${prefix} заполни ответ ученика и объяснение оценки.`;
    }
    const score = Number(example.score);
    const maxScore = Number(example.maxScore);
    if (String(example.score).trim() === "" || String(example.maxScore).trim() === ""
      || !Number.isFinite(score) || !Number.isFinite(maxScore)
      || maxScore <= 0 || score < 0 || score > maxScore) {
      return `${prefix} укажи положительный максимум и оценку от нуля до этого максимума.`;
    }
    if ((example.taskPrompt?.length ?? 0) > gradingExampleLimits.taskPrompt
      || example.studentAnswer.length > gradingExampleLimits.studentAnswer
      || example.feedback.length > gradingExampleLimits.feedback) {
      return `${prefix} сократи условие и комментарий до 4000 символов, ответ — до 8000.`;
    }
  }

  return "";
}
