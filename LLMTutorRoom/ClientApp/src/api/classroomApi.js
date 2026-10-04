import { apiRequest } from "./httpClient.js";

export function getOverview({ signal } = {}) {
  return apiRequest("/api/classroom/overview", { signal });
}

export function getReviewHistory({
  cursor = null,
  includeHistoricalVersions = true,
  pageSize = 25,
  signal
} = {}) {
  const search = new URLSearchParams({
    includeHistoricalVersions: includeHistoricalVersions ? "true" : "false",
    pageSize: String(pageSize)
  });
  if (cursor) {
    search.set("cursor", cursor);
  }

  return apiRequest(`/api/classroom/reviews/history?${search}`, { signal });
}

export function startAttempt(assignmentId) {
  return apiRequest(`/api/attempts/assignments/${encodeURIComponent(assignmentId)}/start`, {
    method: "POST"
  });
}

export function saveAttemptAnswers(attemptId, answers, options = {}) {
  return apiRequest(`/api/attempts/${attemptId}/answers`, {
    ...options,
    method: "PUT",
    body: { answers }
  });
}

export function submitAttempt(attemptId) {
  return apiRequest(`/api/attempts/${attemptId}/submit`, {
    method: "POST"
  });
}
