import { apiRequest } from "./httpClient.js";

export function login(credentials) {
  return apiRequest("/api/auth/login", {
    method: "POST",
    auth: false,
    body: credentials
  });
}

export function getCurrentUser({ signal } = {}) {
  return apiRequest("/api/auth/me", { signal });
}
