import { lazy, Suspense } from "react";
import { Loader2 } from "lucide-react";
import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext.jsx";
import { LoginScreen } from "../auth/LoginScreen.jsx";
import { normalizeRole } from "../shared/lib/roles.js";
import { ErrorState } from "../shared/ui/ErrorState.jsx";
import { LoadingState } from "../shared/ui/LoadingState.jsx";
import { AppShell } from "./AppShell.jsx";
import { RouteErrorBoundary } from "./RouteErrorBoundary.jsx";
import { useOverview } from "./useOverview.js";

const AdminTeachers = lazy(() => import("../features/admin/AdminTeachers.jsx")
  .then(module => ({ default: module.AdminTeachers })));
const AdminModelAccess = lazy(() => import("../features/admin/AdminModelAccess.jsx")
  .then(module => ({ default: module.AdminModelAccess })));
const TeacherRoutes = lazy(() => import("../features/teacher/TeacherRoutes.jsx")
  .then(module => ({ default: module.TeacherRoutes })));
const StudentRoutes = lazy(() => import("../features/student/StudentRoutes.jsx")
  .then(module => ({ default: module.StudentRoutes })));

function RouteLoading() {
  return (
    <div className="route-loading">
      <Loader2 className="spin" size={22} aria-hidden="true" />
      <span>Загрузка раздела</span>
    </div>
  );
}

function AdminRoutes() {
  return (
    <Routes>
      <Route path="/admin/teachers" element={<AdminTeachers />} />
      <Route path="/admin/model-access" element={<AdminModelAccess />} />
      <Route path="*" element={<Navigate to="/admin/teachers" replace />} />
    </Routes>
  );
}

function getAuthorizedRequestedLocation(state, role) {
  const requestedLocation = state?.from;
  const rolePrefix = `/${role}`;

  if (!requestedLocation
    || typeof requestedLocation.pathname !== "string"
    || (requestedLocation.pathname !== rolePrefix
      && !requestedLocation.pathname.startsWith(`${rolePrefix}/`))) {
    return null;
  }

  return {
    pathname: requestedLocation.pathname,
    search: typeof requestedLocation.search === "string" ? requestedLocation.search : "",
    hash: typeof requestedLocation.hash === "string" ? requestedLocation.hash : ""
  };
}

function AuthenticatedApp() {
  const { currentUser, logout } = useAuth();
  const location = useLocation();
  const role = normalizeRole(currentUser?.role);
  const overview = useOverview({
    enabled: role === "teacher" || role === "student",
    identityKey: `${currentUser?.id ?? ""}:${role ?? ""}`
  });

  if (!role) {
    return (
      <ErrorState message="Для этой учетной записи назначена неподдерживаемая роль.">
        <button type="button" className="button secondary" onClick={() => logout()}>
          Выйти
        </button>
      </ErrorState>
    );
  }

  const requestedLocation = getAuthorizedRequestedLocation(location.state, role);
  if (location.pathname === "/login" && requestedLocation) {
    return <Navigate to={requestedLocation} replace />;
  }

  if (role !== "admin" && overview.isLoading && !overview.data) {
    return <LoadingState message="Загрузка учебных данных" />;
  }

  if (role !== "admin" && overview.error && !overview.data) {
    return <ErrorState message={overview.error} onRetry={() => overview.refresh({ clear: true })} />;
  }

  return (
    <AppShell currentUser={currentUser} role={role}>
      {overview.error && overview.data && (
        <div className="inline-error-banner" role="alert">
          <span>{overview.error}</span>
          <button type="button" className="button secondary" onClick={() => overview.refresh()}>
            Повторить
          </button>
        </div>
      )}

      <RouteErrorBoundary resetKey={`${role}:${location.pathname}`}>
        <Suspense fallback={<RouteLoading />}>
          {role === "admin" && <AdminRoutes />}
          {role === "teacher" && (
            <TeacherRoutes overview={overview.data} refresh={overview.refresh} />
          )}
          {role === "student" && (
            <StudentRoutes
              overview={overview.data}
              refresh={overview.refresh}
              updateAttempt={overview.updateAttempt}
            />
          )}
        </Suspense>
      </RouteErrorBoundary>
    </AppShell>
  );
}

export function App() {
  const { status, retrySession, logout } = useAuth();
  const location = useLocation();

  if (status === "checking") {
    return <LoadingState message="Проверка сессии" />;
  }

  if (status === "error") {
    return (
      <ErrorState
        message="Не удалось проверить сессию. Проверьте доступность AuthService."
        onRetry={retrySession}
      >
        <button type="button" className="button secondary" onClick={() => logout()}>
          Вернуться ко входу
        </button>
      </ErrorState>
    );
  }

  if (status !== "authenticated") {
    const requestedLocation = {
      pathname: location.pathname,
      search: location.search,
      hash: location.hash
    };

    return (
      <Routes>
        <Route path="/login" element={<LoginScreen />} />
        <Route
          path="*"
          element={<Navigate to="/login" replace state={{ from: requestedLocation }} />}
        />
      </Routes>
    );
  }

  return <AuthenticatedApp />;
}
