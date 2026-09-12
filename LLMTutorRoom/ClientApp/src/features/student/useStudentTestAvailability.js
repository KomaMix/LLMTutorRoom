import { useEffect, useState } from "react";

function isTestAvailable(test, attempt, currentTime) {
  if (attempt) {
    return attempt.status === "in-progress"
      && new Date(attempt.endsAt).getTime() > currentTime;
  }

  return test.status === "published"
    && test.versionNumber > 0
    && new Date(test.deadline).getTime() > currentTime;
}

export function getUnavailableTestStatus(test, currentTime) {
  return new Date(test.deadline).getTime() <= currentTime
    ? "expired"
    : test.status === "published" ? "hidden" : test.status;
}

export function useStudentTestAvailability(tests, attempts) {
  const [clockTime, setClockTime] = useState(Date.now);
  const currentTime = Math.max(clockTime, Date.now());
  const attemptByTestId = new Map(attempts.map(attempt => [attempt.testId, attempt]));
  const availableTests = [];
  const unavailableTests = [];

  for (const test of tests) {
    const target = isTestAvailable(test, attemptByTestId.get(test.id), currentTime)
      ? availableTests
      : unavailableTests;
    target.push(test);
  }

  useEffect(() => {
    const now = Date.now();
    const deadlines = [
      ...tests.map(test => new Date(test.deadline).getTime()),
      ...attempts
        .filter(attempt => attempt.status === "in-progress")
        .map(attempt => new Date(attempt.endsAt).getTime())
    ].filter(Number.isFinite);

    if (deadlines.some(deadline => deadline > clockTime && deadline <= now)) {
      setClockTime(now);
      return;
    }

    const nextDeadline = deadlines
      .filter(deadline => deadline > now)
      .sort((left, right) => left - right)[0];

    if (nextDeadline == null) {
      return;
    }

    const maximumTimeout = 2_147_000_000;
    const timer = window.setTimeout(
      () => setClockTime(Date.now()),
      Math.min(maximumTimeout, Math.max(25, nextDeadline - now + 25)));
    return () => window.clearTimeout(timer);
  }, [tests, attempts, clockTime]);

  return { availableTests, unavailableTests, attemptByTestId, currentTime };
}
