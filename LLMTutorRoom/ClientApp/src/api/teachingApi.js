import { apiRequest } from "./httpClient.js";

export function createTest(payload) {
  return apiRequest("/api/teaching/tests", {
    method: "POST",
    body: payload
  });
}

export function updateTest(testId, payload) {
  return apiRequest(`/api/teaching/tests/${encodeURIComponent(testId)}`, {
    method: "PUT",
    body: payload
  });
}

export function createTask(testId, payload) {
  return apiRequest(`/api/teaching/tests/${encodeURIComponent(testId)}/tasks`, {
    method: "POST",
    body: payload
  });
}

export function updateTask(testId, taskId, payload) {
  return apiRequest(
    `/api/teaching/tests/${encodeURIComponent(testId)}/tasks/${encodeURIComponent(taskId)}`,
    {
      method: "PUT",
      body: payload
    }
  );
}

export function setTaskVisibility(testId, taskId, isHidden) {
  return apiRequest(
    `/api/teaching/tests/${encodeURIComponent(testId)}/tasks/${encodeURIComponent(taskId)}/visibility`,
    {
      method: "PATCH",
      body: { isHidden }
    }
  );
}

export function deleteTask(testId, taskId) {
  return apiRequest(
    `/api/teaching/tests/${encodeURIComponent(testId)}/tasks/${encodeURIComponent(taskId)}`,
    { method: "DELETE" }
  );
}
