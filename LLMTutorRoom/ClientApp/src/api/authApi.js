import { apiRequest } from "./httpClient.js";

export function login(credentials, { signal } = {}) {
  return apiRequest("/api/auth/login", {
    method: "POST",
    auth: false,
    body: credentials,
    signal
  });
}

export function registerStudent(account, { signal } = {}) {
  return apiRequest("/api/auth/register", {
    method: "POST",
    auth: false,
    body: account,
    signal
  });
}

export function getCurrentUser({ signal } = {}) {
  return apiRequest("/api/auth/me", { signal });
}
