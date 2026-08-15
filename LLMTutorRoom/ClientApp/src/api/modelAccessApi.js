import { apiRequest } from "./httpClient.js";

export function getModelCatalog({ signal } = {}) {
  return apiRequest("/api/classroom/model-access/models", { signal });
}

export function getTeacherModelAccess(teacherId, { signal } = {}) {
  return apiRequest(
    `/api/classroom/model-access/teachers/${encodeURIComponent(teacherId)}`,
    { signal }
  );
}

export function saveTeacherModelAccess(teacherId, modelKey, payload) {
  return apiRequest(
    `/api/classroom/model-access/models/${encodeURIComponent(modelKey)}/teachers/${encodeURIComponent(teacherId)}`,
    {
      method: "PUT",
      body: payload
    }
  );
}

export function deleteTeacherModelAccess(teacherId, modelKey) {
  return apiRequest(
    `/api/classroom/model-access/models/${encodeURIComponent(modelKey)}/teachers/${encodeURIComponent(teacherId)}`,
    { method: "DELETE" }
  );
}
