import { useEffect } from "react";
import { Navigate, Route, Routes, useNavigate, useParams } from "react-router-dom";
import {
  EmptyTeacherState,
  ModelPanel,
  ReviewQueue,
  TeacherDashboard,
  TeacherTests
} from "./index.js";

function TeacherDashboardRoute({ overview }) {
  const navigate = useNavigate();
  const selectedTest = overview.tests[0] ?? null;
  const activeReviews = overview.reviews.filter(item => item.status !== "checked");

  if (!selectedTest) {
    return <EmptyTeacherState onOpenTests={() => navigate("/teacher/tests")} />;
  }

  return (
    <TeacherDashboard
      overview={overview}
      selectedTest={selectedTest}
      activeReviews={activeReviews}
      onOpenTests={() => navigate(`/teacher/tests/${selectedTest.id}`)}
    />
  );
}

function TeacherTestsRoute({ overview, refresh }) {
  const { testId = "" } = useParams();
  const navigate = useNavigate();
  const selectedTest = overview.tests.find(test => test.id === testId)
    ?? overview.tests[0]
    ?? null;

  useEffect(() => {
    if (selectedTest && selectedTest.id !== testId) {
      navigate(`/teacher/tests/${selectedTest.id}`, { replace: true });
    }
  }, [navigate, selectedTest, testId]);

  async function handleTestsChanged(nextTestId) {
    const refreshed = await refresh();
    if (refreshed && nextTestId && nextTestId !== testId) {
      navigate(`/teacher/tests/${nextTestId}`);
    }
  }

  return (
    <TeacherTests
      tests={overview.tests}
      models={overview.models}
      selectedTest={selectedTest}
      selectedTestId={selectedTest?.id ?? ""}
      onSelectTest={nextTestId => navigate(`/teacher/tests/${nextTestId}`)}
      onTestsChanged={handleTestsChanged}
    />
  );
}

export function TeacherRoutes({ overview, refresh }) {
  return (
    <Routes>
      <Route path="/teacher/dashboard" element={<TeacherDashboardRoute overview={overview} />} />
      <Route path="/teacher/tests/:testId?" element={<TeacherTestsRoute overview={overview} refresh={refresh} />} />
      <Route
        path="/teacher/reviews"
        element={<ReviewQueue reviews={overview.reviews} onReviewsChanged={refresh} />}
      />
      <Route path="/teacher/models" element={<ModelPanel models={overview.models} />} />
      <Route path="*" element={<Navigate to="/teacher/dashboard" replace />} />
    </Routes>
  );
}
