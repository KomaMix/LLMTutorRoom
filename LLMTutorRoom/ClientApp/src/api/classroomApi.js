import { apiRequest } from "./httpClient.js";

export function getOverview({ signal } = {}) {
  return apiRequest("/api/classroom/overview", { signal });
}

export function startAttempt(testId) {
  return apiRequest(`/api/classroom/tests/${encodeURIComponent(testId)}/attempts/start`, {
    method: "POST"
  });
}

export function saveAttemptAnswers(attemptId, answers, options = {}) {
  return apiRequest(`/api/classroom/attempts/${attemptId}/answers`, {
    ...options,
    method: "PUT",
    body: { answers }
  });
}

export function submitAttempt(attemptId) {
  return apiRequest(`/api/classroom/attempts/${attemptId}/submit`, {
    method: "POST"
  });
}

export function saveManualReview(reviewId, taskId, payload) {
  return apiRequest(
    `/api/classroom/reviews/${reviewId}/tasks/${encodeURIComponent(taskId)}/manual`,
    {
      method: "PUT",
      body: payload
    }
  );
}
