import { useCallback, useEffect, useRef } from "react";
import { FileText } from "lucide-react";
import {
  Navigate,
  Route,
  Routes,
  useBlocker,
  useNavigate,
  useParams
} from "react-router-dom";
import { useNavigationGuard } from "../../app/NavigationGuardContext.jsx";
import { StudentResults, StudentWorkspace, useStudentAttempt } from "./index.js";

const completedReviewStatuses = new Set(["checked", "failed"]);

function hasPendingResults(overview) {
  const reviewedAttemptIds = new Set(
    overview.reviews
      .map(review => review.attemptId)
      .filter(attemptId => attemptId != null));

  return overview.reviews.some(review => !completedReviewStatuses.has(review.status))
    || overview.attempts.some(attempt =>
      attempt.status === "submitted" && !reviewedAttemptIds.has(attempt.id));
}

function getNextAttemptDeadline(attempts) {
  return attempts
    .filter(attempt => attempt.status === "in-progress")
    .map(attempt => new Date(attempt.endsAt).getTime())
    .filter(Number.isFinite)
    .sort((left, right) => left - right)[0] ?? null;
}

function StudentTestsRoute({ overview, refresh, updateAttempt }) {
  const { testId = "" } = useParams();
  const navigate = useNavigate();
  const { registerBeforeLogout } = useNavigationGuard();
  const blockerRef = useRef(null);
  const allowSubmittedNavigationRef = useRef(false);
  const selectedTest = overview.tests.find(test => test.id === testId)
    ?? overview.tests[0]
    ?? null;
  const selectedAttempt = selectedTest
    ? overview.attempts.find(attempt => attempt.testId === selectedTest.id) ?? null
    : null;
  const attempt = useStudentAttempt({
    selectedTest,
    selectedAttempt,
    updateAttempt,
    onSubmitted: () => {
      if (refresh) {
        Promise.resolve(refresh()).catch(() => undefined);
      }

      const activeBlocker = blockerRef.current;
      if (activeBlocker?.state === "blocked") {
        return;
      }

      allowSubmittedNavigationRef.current = true;
      navigate("/student/results", { replace: true });
    }
  });
  const { prepareForNavigation, shouldBlockNavigation } = attempt;
  const shouldBlock = useCallback(({ currentLocation, nextLocation }) => {
    const currentUrl = `${currentLocation.pathname}${currentLocation.search}${currentLocation.hash}`;
    const nextUrl = `${nextLocation.pathname}${nextLocation.search}${nextLocation.hash}`;
    return currentUrl !== nextUrl
      && !allowSubmittedNavigationRef.current
      && shouldBlockNavigation();
  }, [shouldBlockNavigation]);
  const blocker = useBlocker(shouldBlock);
  const {
    state: blockerState,
    location: blockedLocation,
    proceed: proceedNavigation,
    reset: resetNavigation
  } = blocker;
  blockerRef.current = blocker;

  useEffect(() => registerBeforeLogout(prepareForNavigation), [
    prepareForNavigation,
    registerBeforeLogout
  ]);

  useEffect(() => {
    if (blockerState !== "blocked") {
      return;
    }

    let isActive = true;
    prepareForNavigation()
      .then(canNavigate => {
        if (!isActive) {
          return;
        }

        if (canNavigate) {
          proceedNavigation();
        } else {
          resetNavigation();
        }
      })
      .catch(() => {
        if (isActive) {
          resetNavigation();
        }
      });

    return () => {
      isActive = false;
    };
  }, [
    prepareForNavigation,
    blockedLocation?.key,
    blockerState,
    proceedNavigation,
    resetNavigation
  ]);

  useEffect(() => {
    if (selectedTest && selectedTest.id !== testId) {
      navigate(`/student/tests/${selectedTest.id}`, { replace: true });
    }
  }, [navigate, selectedTest, testId]);

  if (!selectedTest) {
    return (
      <section className="panel empty-state">
        <FileText size={28} aria-hidden="true" />
        <h2>Доступных тестов пока нет</h2>
      </section>
    );
  }

  function handleSelectTest(nextTestId) {
    if (nextTestId === selectedTest.id) {
      return;
    }

    navigate(`/student/tests/${nextTestId}`);
  }

  return (
    <StudentWorkspace
      tests={overview.tests}
      selectedTest={selectedTest}
      selectedTestId={selectedTest.id}
      selectedAttempt={selectedAttempt}
      remainingSeconds={attempt.remainingSeconds}
      answers={attempt.answers}
      message={attempt.message}
      isStartingAttempt={attempt.isStartingAttempt}
      isSavingAttempt={attempt.isSavingAttempt}
      isSubmittingAttempt={attempt.isSubmittingAttempt}
      onSelectTest={handleSelectTest}
      onAnswerChange={attempt.onAnswerChange}
      onStartAttempt={attempt.onStartAttempt}
      onSaveAnswers={attempt.onSaveAnswers}
      onSubmitAttempt={attempt.onSubmitAttempt}
    />
  );
}

function StudentResultsRoute({ overview, refresh }) {
  const isRefreshingRef = useRef(false);
  const shouldPoll = hasPendingResults(overview);
  const nextAttemptDeadline = getNextAttemptDeadline(overview.attempts);
  const runRefresh = useCallback(async () => {
    if (!refresh || isRefreshingRef.current) {
      return false;
    }

    isRefreshingRef.current = true;
    try {
      const refreshed = await refresh();
      return refreshed !== false;
    } finally {
      isRefreshingRef.current = false;
    }
  }, [refresh]);

  useEffect(() => {
    if (!shouldPoll) {
      return;
    }

    let isActive = true;

    async function poll() {
      if (!isActive) {
        return;
      }

      await runRefresh();
    }

    poll();
    const timer = window.setInterval(poll, 5000);
    return () => {
      isActive = false;
      window.clearInterval(timer);
    };
  }, [runRefresh, shouldPoll]);

  useEffect(() => {
    if (nextAttemptDeadline == null || !refresh || shouldPoll) {
      return;
    }

    let isActive = true;
    let timer = null;
    const maximumTimeout = 2_147_000_000;

    function scheduleDeadlineRefresh() {
      if (!isActive) {
        return;
      }

      const remainingMilliseconds = nextAttemptDeadline - Date.now();
      if (remainingMilliseconds > 0) {
        timer = window.setTimeout(
          scheduleDeadlineRefresh,
          Math.min(maximumTimeout, remainingMilliseconds + 25));
        return;
      }

      runRefresh()
        .catch(() => false)
        .finally(() => {
          if (isActive) {
            timer = window.setTimeout(scheduleDeadlineRefresh, 5000);
          }
        });
    }

    scheduleDeadlineRefresh();
    return () => {
      isActive = false;
      if (timer !== null) {
        window.clearTimeout(timer);
      }
    };
  }, [nextAttemptDeadline, refresh, runRefresh, shouldPoll]);

  return (
    <StudentResults
      attempts={overview.attempts}
      reviews={overview.reviews}
      tests={overview.tests}
    />
  );
}

export function StudentRoutes({ overview, refresh, updateAttempt }) {
  return (
    <Routes>
      <Route
        path="/student/tests/:testId?"
        element={(
          <StudentTestsRoute
            overview={overview}
            refresh={refresh}
            updateAttempt={updateAttempt}
          />
        )}
      />
      <Route
        path="/student/results"
        element={(
          <StudentResultsRoute overview={overview} refresh={refresh} />
        )}
      />
      <Route path="*" element={<Navigate to="/student/tests" replace />} />
    </Routes>
  );
}
