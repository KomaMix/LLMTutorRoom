import { useEffect, useState } from "react";

function isTestAvailable(test, attempt, currentTime) {
  if (attempt) {
    return attempt.status === "in-progress"
      && new Date(attempt.endsAt).getTime() > currentTime;
  }

  return test.status === "published"
    && test.versionNumber > 0
    && new Date(test.availableFrom).getTime() <= currentTime
    && new Date(test.deadline).getTime() > currentTime;
}

export function getUnavailableTestStatus(test, currentTime) {
  if (new Date(test.deadline).getTime() <= currentTime) {
    return "expired";
  }

  if (new Date(test.availableFrom).getTime() > currentTime) {
    return "scheduled";
  }

  return test.status === "published" ? "hidden" : test.status;
}

export function useStudentTestAvailability(tests, attempts) {
  const [clockTime, setClockTime] = useState(Date.now);
  const currentTime = Math.max(clockTime, Date.now());
  const attemptByAssignmentId = new Map(attempts.map(attempt => [attempt.assignmentId, attempt]));
  const availableTests = [];
  const unavailableTests = [];

  for (const test of tests) {
    const target = isTestAvailable(test, attemptByAssignmentId.get(test.id), currentTime)
      ? availableTests
      : unavailableTests;
    target.push(test);
  }

  useEffect(() => {
    const now = Date.now();
    const deadlines = [
      ...tests.map(test => new Date(test.deadline).getTime()),
      ...tests.map(test => new Date(test.availableFrom).getTime()),
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

  return { availableTests, unavailableTests, attemptByAssignmentId, currentTime };
}
