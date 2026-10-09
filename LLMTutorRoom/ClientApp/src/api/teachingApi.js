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

export function getFeedbackTemplates({ signal } = {}) {
  return apiRequest("/api/teaching/feedback-templates", { signal });
}

export function createFeedbackTemplate(payload) {
  return apiRequest("/api/teaching/feedback-templates", {
    method: "POST",
    body: payload
  });
}

export function updateFeedbackTemplate(templateId, payload) {
  return apiRequest(`/api/teaching/feedback-templates/${encodeURIComponent(templateId)}`, {
    method: "PUT",
    body: payload
  });
}

export function deleteFeedbackTemplate(templateId) {
  return apiRequest(`/api/teaching/feedback-templates/${encodeURIComponent(templateId)}`, {
    method: "DELETE"
  });
}

export function getStudyGroups({ signal } = {}) {
  return apiRequest("/api/teaching/groups", { signal });
}

export function createStudyGroup(payload) {
  return apiRequest("/api/teaching/groups", {
    method: "POST",
    body: payload
  });
}

export function updateStudyGroup(groupId, payload) {
  return apiRequest(`/api/teaching/groups/${encodeURIComponent(groupId)}`, {
    method: "PUT",
    body: payload
  });
}

export function getAssignableTests(groupId, { signal } = {}) {
  return apiRequest(
    `/api/teaching/groups/${encodeURIComponent(groupId)}/assignable-tests`,
    { signal }
  );
}

export function createTestAssignment(groupId, payload) {
  return apiRequest(
    `/api/teaching/groups/${encodeURIComponent(groupId)}/assignments`,
    {
      method: "POST",
      body: payload
    }
  );
}
