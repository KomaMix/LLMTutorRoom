export function getRussianPluralForm(value, one, few, many) {
  const numericValue = Number(value);
  if (!Number.isInteger(numericValue)) {
    return few;
  }

  const absoluteValue = Math.abs(numericValue);
  const lastTwoDigits = absoluteValue % 100;
  if (lastTwoDigits >= 11 && lastTwoDigits <= 14) {
    return many;
  }

  const lastDigit = absoluteValue % 10;
  if (lastDigit === 1) {
    return one;
  }

  if (lastDigit >= 2 && lastDigit <= 4) {
    return few;
  }

  return many;
}
