const supportedRoles = new Set(["admin", "teacher", "student"]);

export function normalizeRole(role) {
  const normalizedRole = typeof role === "string" ? role.toLowerCase() : "";
  return supportedRoles.has(normalizedRole) ? normalizedRole : null;
}

export function getPageTitle(role) {
  if (role === "admin") {
    return "Администрирование";
  }

  return role === "teacher" ? "Рабочее место преподавателя" : "Кабинет ученика";
}
