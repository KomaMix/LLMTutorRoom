export function getRequestErrorMessage(error, fallback, statusMessages = {}) {
  const statusMessage = statusMessages[error?.status];
  if (statusMessage) {
    return statusMessage;
  }

  if (error?.status === 401) {
    return "Сессия истекла. Войдите снова.";
  }

  if (error?.status === 403) {
    return "Недостаточно прав для этой операции.";
  }

  if (error?.status >= 500) {
    return `${fallback} Сервис временно недоступен.`;
  }

  if (!error?.status) {
    return `${fallback} Проверьте подключение к серверу.`;
  }

  return fallback;
}
