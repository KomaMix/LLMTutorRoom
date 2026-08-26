export function serializeForm(form) {
  return JSON.stringify(form ?? null);
}

export function getApiErrorMessage(error, fallback) {
  const response = error?.data;
  const detail = typeof response === "string"
    ? response
    : typeof response?.detail === "string"
      ? response.detail
      : "";

  if (detail.trim()) {
    return detail.trim();
  }

  if (error?.status === 401) {
    return "Сессия завершилась. Войдите снова.";
  }

  if (error?.status === 403) {
    return "Недостаточно прав для этого действия.";
  }

  if (!error?.status) {
    return "Сервис недоступен. Проверьте подключение и повторите попытку.";
  }

  return fallback;
}

export function mergeCatalogVersionMetadata(version, catalogTest) {
  if (!version || !catalogTest || version.id !== catalogTest.id) {
    return version;
  }

  const versionNumber = version.versionNumber;
  const catalogVersionNumber = catalogTest.versionNumber;
  if (catalogVersionNumber === versionNumber
    && catalogTest.contentRevision >= version.contentRevision) {
    return catalogTest;
  }

  const versionSummary = catalogTest.versions?.find(
    item => item.versionNumber === versionNumber);
  if (Array.isArray(catalogTest.versions) && !versionSummary) {
    return catalogTest;
  }

  return {
    ...version,
    status: versionSummary?.status ?? version.status,
    publishedVersionNumber: catalogTest.publishedVersionNumber,
    hasDraft: catalogTest.hasDraft,
    versions: catalogTest.versions ?? version.versions ?? []
  };
}
