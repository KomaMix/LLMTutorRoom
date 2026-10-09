import { useEffect } from "react";
import { Navigate, Route, Routes, useNavigate, useParams } from "react-router-dom";
import {
  ModelPanel,
  ReviewQueue,
  TeacherDashboard,
  TeacherTests
} from "./index.js";
import { StudyGroupsPage } from "../groups/StudyGroupsPage.jsx";
import { FeedbackTemplatesPage } from "./FeedbackTemplatesPage.jsx";
import { useFeedbackTemplates } from "./useFeedbackTemplates.js";

function TeacherDashboardRoute({ overview }) {
  const navigate = useNavigate();
  const activeReviews = overview.reviews.filter(item => item.status !== "checked");

  return (
    <TeacherDashboard
      overview={overview}
      activeReviews={activeReviews}
      onOpenReviews={() => navigate("/teacher/reviews")}
    />
  );
}

function TeacherTestsRoute({ feedbackTemplates, overview, refresh }) {
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

    return refreshed;
  }

  return (
    <TeacherTests
      tests={overview.tests}
      models={overview.models}
      feedbackTemplates={feedbackTemplates}
      selectedTest={selectedTest}
      selectedTestId={selectedTest?.id ?? ""}
      onSelectTest={nextTestId => navigate(`/teacher/tests/${nextTestId}`)}
      onTestsChanged={handleTestsChanged}
    />
  );
}

export function TeacherRoutes({ overview, refresh }) {
  const feedbackTemplateCatalog = useFeedbackTemplates();

  return (
    <Routes>
      <Route path="/teacher/dashboard" element={<TeacherDashboardRoute overview={overview} />} />
      <Route
        path="/teacher/tests/:testId?"
        element={(
          <TeacherTestsRoute
            feedbackTemplates={feedbackTemplateCatalog.templates}
            overview={overview}
            refresh={refresh}
          />
        )}
      />
      <Route
        path="/teacher/feedback-templates"
        element={<FeedbackTemplatesPage {...feedbackTemplateCatalog} />}
      />
      <Route path="/teacher/groups" element={<StudyGroupsPage attempts={overview.attempts} onChanged={refresh} />} />
      <Route
        path="/teacher/reviews"
        element={(
          <ReviewQueue
            reviews={overview.reviews}
            tests={overview.tests}
            terminalReviewsNextCursor={overview.terminalReviewsNextCursor}
            onReviewsChanged={refresh}
          />
        )}
      />
      <Route
        path="/teacher/models"
        element={<ModelPanel models={overview.models} onRefresh={refresh} />}
      />
      <Route path="*" element={<Navigate to="/teacher/dashboard" replace />} />
    </Routes>
  );
}
