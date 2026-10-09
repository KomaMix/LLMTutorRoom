import { apiRequest } from "./httpClient.js";

export function getStudyGroups({ signal } = {}) {
  return apiRequest("/api/groups", { signal });
}

export function createStudyGroup(payload) {
  return apiRequest("/api/groups", {
    method: "POST",
    body: payload
  });
}

export function updateStudyGroup(groupId, payload) {
  return apiRequest(`/api/groups/${encodeURIComponent(groupId)}`, {
    method: "PUT",
    body: payload
  });
}
