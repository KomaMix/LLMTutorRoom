const dateTimeFormatter = new Intl.DateTimeFormat("ru-RU", {
  day: "2-digit",
  month: "short",
  hour: "2-digit",
  minute: "2-digit"
});

function padDatePart(value) {
  return value.toString().padStart(2, "0");
}

export function formatDuration(totalSeconds) {
  const safeSeconds = Math.max(0, Number(totalSeconds) || 0);
  const minutes = Math.floor(safeSeconds / 60);
  const seconds = safeSeconds % 60;

  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

export function formatDate(value) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : dateTimeFormatter.format(date);
}

export function formatPeriod(seconds) {
  const safeSeconds = Number(seconds);
  if (!safeSeconds || safeSeconds <= 0) {
    return "не задан";
  }

  const days = safeSeconds / 86400;
  if (Number.isInteger(days) && days >= 1) {
    return `${days} дн.`;
  }

  const hours = safeSeconds / 3600;
  if (Number.isInteger(hours) && hours >= 1) {
    return `${hours} ч.`;
  }

  const minutes = safeSeconds / 60;
  if (Number.isInteger(minutes) && minutes >= 1) {
    return `${minutes} мин.`;
  }

  return `${safeSeconds} сек.`;
}

function toInputDate(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  return [
    date.getFullYear(),
    padDatePart(date.getMonth() + 1),
    padDatePart(date.getDate())
  ].join("-");
}

export function toInputDateTime(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  return `${toInputDate(date)}T${padDatePart(date.getHours())}:${padDatePart(date.getMinutes())}`;
}

export function localDateTimeToIso(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    throw new Error("Некорректные дата и время.");
  }

  return date.toISOString();
}
