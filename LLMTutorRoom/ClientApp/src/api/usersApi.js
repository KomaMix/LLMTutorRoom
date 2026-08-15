import { apiRequest } from "./httpClient.js";

export function getTeachers({ signal } = {}) {
  return apiRequest("/api/users/teachers", { signal });
}

export function createTeacher(form) {
  return apiRequest("/api/users/teachers", {
    method: "POST",
    body: form
  });
}
