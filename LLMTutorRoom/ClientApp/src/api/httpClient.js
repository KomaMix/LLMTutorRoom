const authTokenStorageKey = "llmtutorroom.accessToken";
const unauthorizedListeners = new Set();

export class ApiError extends Error {
  constructor(message, { status = 0, data = null, cause = null, url = "" } = {}) {
    super(message, cause ? { cause } : undefined);
    this.name = "ApiError";
    this.status = status;
    this.data = data;
    this.url = url;
  }
}

export function getAccessToken() {
  return localStorage.getItem(authTokenStorageKey);
}

export function setAccessToken(accessToken) {
  localStorage.setItem(authTokenStorageKey, accessToken);
}

export function clearAccessToken() {
  localStorage.removeItem(authTokenStorageKey);
}

export function subscribeToUnauthorized(listener) {
  unauthorizedListeners.add(listener);
  return () => unauthorizedListeners.delete(listener);
}

function notifyUnauthorized() {
  unauthorizedListeners.forEach(listener => listener());
}

function isJsonBody(body) {
  return body !== undefined
    && body !== null
    && typeof body !== "string"
    && !(body instanceof Blob)
    && !(body instanceof FormData)
    && !(body instanceof URLSearchParams);
}

async function readResponseBody(response) {
  if (response.status === 204) {
    return null;
  }

  const contentType = response.headers.get("Content-Type") ?? "";
  if (contentType.includes("json")) {
    return response.json();
  }

  const text = await response.text();
  return text || null;
}

export async function apiRequest(url, options = {}) {
  const {
    auth = true,
    body,
    headers: providedHeaders,
    ...fetchOptions
  } = options;
  const headers = new Headers(providedHeaders ?? {});
  const requestAccessToken = auth ? getAccessToken() : null;
  let requestBody = body;

  if (!headers.has("Accept")) {
    headers.set("Accept", "application/json");
  }

  if (isJsonBody(body)) {
    headers.set("Content-Type", "application/json");
    requestBody = JSON.stringify(body);
  }

  if (requestAccessToken) {
    headers.set("Authorization", `Bearer ${requestAccessToken}`);
  }

  let response;
  try {
    response = await fetch(url, {
      ...fetchOptions,
      body: requestBody,
      headers
    });
  } catch (error) {
    if (error?.name === "AbortError") {
      throw error;
    }

    throw new ApiError("Сервис недоступен.", { cause: error, url });
  }

  if (response.status === 401
    && requestAccessToken
    && getAccessToken() === requestAccessToken) {
    notifyUnauthorized();
  }

  let data;
  try {
    data = await readResponseBody(response);
  } catch (error) {
    throw new ApiError("Сервис вернул некорректный ответ.", {
      status: response.status,
      cause: error,
      url
    });
  }

  if (!response.ok) {
    throw new ApiError(`HTTP ${response.status}`, {
      status: response.status,
      data,
      url
    });
  }

  return data;
}
