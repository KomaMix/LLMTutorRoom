import { getRussianPluralForm } from "./russianPlural.js";

export function formatPoints(value) {
  return `${value} ${getRussianPluralForm(value, "балл", "балла", "баллов")}`;
}
