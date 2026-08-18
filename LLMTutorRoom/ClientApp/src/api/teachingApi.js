import { apiRequest } from "./httpClient.js";

function createConcurrencyQuery(versionNumber, contentRevision) {
  return `versionNumber=${encodeURIComponent(versionNumber)}&contentRevision=${encodeURIComponent(contentRevision)}`;
}

export function createTest(payload) {
  return apiRequest("/api/teaching/tests", {
    method: "POST",
    body: payload
  });
}

export function updateTest(testId, versionNumber, contentRevision, payload) {
  const query = createConcurrencyQuery(versionNumber, contentRevision);
  return apiRequest(`/api/teaching/tests/${encodeURIComponent(testId)}?${query}`, {
    method: "PUT",
    body: payload
  });
}

export function getTestVersion(testId, versionNumber, { signal } = {}) {
  return apiRequest(
    `/api/teaching/tests/${encodeURIComponent(testId)}/versions/${encodeURIComponent(versionNumber)}`,
    { signal }
  );
}

export function createDraftVersion(testId, expectedPublishedVersionNumber) {
  return apiRequest(`/api/teaching/tests/${encodeURIComponent(testId)}/versions?publishedVersionNumber=${encodeURIComponent(expectedPublishedVersionNumber)}`, {
    method: "POST"
  });
}

export function publishTestVersion(testId, versionNumber, contentRevision) {
  return apiRequest(
    `/api/teaching/tests/${encodeURIComponent(testId)}/versions/${encodeURIComponent(versionNumber)}/publish?contentRevision=${encodeURIComponent(contentRevision)}`,
    { method: "POST" }
  );
}

export function deleteDraftVersion(testId, versionNumber, contentRevision) {
  return apiRequest(
    `/api/teaching/tests/${encodeURIComponent(testId)}/versions/${encodeURIComponent(versionNumber)}?contentRevision=${encodeURIComponent(contentRevision)}`,
    { method: "DELETE" }
  );
}

export function createTask(testId, versionNumber, contentRevision, payload) {
  const query = createConcurrencyQuery(versionNumber, contentRevision);
  return apiRequest(`/api/teaching/tests/${encodeURIComponent(testId)}/tasks?${query}`, {
    method: "POST",
    body: payload
  });
}

export function updateTask(testId, versionNumber, contentRevision, taskId, payload) {
  const query = createConcurrencyQuery(versionNumber, contentRevision);
  return apiRequest(
    `/api/teaching/tests/${encodeURIComponent(testId)}/tasks/${encodeURIComponent(taskId)}?${query}`,
    {
      method: "PUT",
      body: payload
    }
  );
}

export function setTaskVisibility(testId, versionNumber, contentRevision, taskId, isHidden) {
  const query = createConcurrencyQuery(versionNumber, contentRevision);
  return apiRequest(
    `/api/teaching/tests/${encodeURIComponent(testId)}/tasks/${encodeURIComponent(taskId)}/visibility?${query}`,
    {
      method: "PATCH",
      body: { isHidden }
    }
  );
}

export function deleteTask(testId, versionNumber, contentRevision, taskId) {
  const query = createConcurrencyQuery(versionNumber, contentRevision);
  return apiRequest(
    `/api/teaching/tests/${encodeURIComponent(testId)}/tasks/${encodeURIComponent(taskId)}?${query}`,
    { method: "DELETE" }
  );
}
