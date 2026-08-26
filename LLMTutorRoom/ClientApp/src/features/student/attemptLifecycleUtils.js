import { ApiError } from "../../api/httpClient.js";

export const autosaveDelayMilliseconds = 900;

export function copyAnswers(answers) {
  if (!answers || typeof answers !== "object" || Array.isArray(answers)) {
    return {};
  }

  return { ...answers };
}

export function getRemainingSeconds(attempt, now) {
  if (!attempt || attempt.status !== "in-progress") {
    return 0;
  }

  const endsAt = new Date(attempt.endsAt).getTime();
  if (!Number.isFinite(endsAt)) {
    return 0;
  }

  return Math.max(0, Math.ceil((endsAt - now) / 1000));
}

export function getConflictAttempt(error) {
  if (!(error instanceof ApiError) || error.status !== 409) {
    return null;
  }

  const attempt = error.data;
  const hasAnswers = attempt?.answers
    && typeof attempt.answers === "object"
    && !Array.isArray(attempt.answers);

  return typeof attempt?.id === "string"
    && attempt.id.length > 0
    && typeof attempt.testId === "string"
    && typeof attempt.status === "string"
    && typeof attempt.startedAt === "string"
    && typeof attempt.endsAt === "string"
    && hasAnswers
    ? attempt
    : null;
}

export function canAttemptAcceptAnswers(attempt, blockedAttemptIds) {
  return Boolean(
    attempt
    && attempt.status === "in-progress"
    && !blockedAttemptIds.has(attempt.id)
  );
}

export function isAttemptEditable(attempt, blockedAttemptIds) {
  if (!canAttemptAcceptAnswers(attempt, blockedAttemptIds)) {
    return false;
  }

  const endsAt = new Date(attempt.endsAt).getTime();
  return Number.isFinite(endsAt) && endsAt > Date.now();
}

export function createFinalSaveOptions(accessToken) {
  const headers = accessToken
    ? { Authorization: `Bearer ${accessToken}` }
    : {};

  return {
    auth: false,
    headers,
    keepalive: true
  };
}
