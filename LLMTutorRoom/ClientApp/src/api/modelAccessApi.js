import { apiRequest } from "./httpClient.js";

export function getModelCatalog({ signal } = {}) {
  return apiRequest("/api/model-access/models", { signal });
}

export function getTeacherModelAccess(teacherId, { signal } = {}) {
  return apiRequest(
    `/api/model-access/teachers/${encodeURIComponent(teacherId)}`,
    { signal }
  );
}

export function saveTeacherModelAccess(teacherId, modelId, payload) {
  return apiRequest(
    `/api/model-access/models/${encodeURIComponent(modelId)}/teachers/${encodeURIComponent(teacherId)}`,
    {
      method: "PUT",
      body: payload
    }
  );
}

export function deleteTeacherModelAccess(teacherId, modelId) {
  return apiRequest(
    `/api/model-access/models/${encodeURIComponent(modelId)}/teachers/${encodeURIComponent(teacherId)}`,
    { method: "DELETE" }
  );
}
