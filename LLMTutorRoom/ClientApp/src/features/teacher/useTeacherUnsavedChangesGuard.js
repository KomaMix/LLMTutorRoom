import { useCallback, useEffect } from "react";
import { useBlocker } from "react-router-dom";
import { useNavigationGuard } from "../../app/NavigationGuardContext.jsx";

const unsavedChangesPrompt = "Есть несохранённые изменения теста или задания. Покинуть страницу и потерять их?";

export function useTeacherUnsavedChangesGuard(hasUnsavedChanges) {
  const { registerBeforeLogout } = useNavigationGuard();
  const shouldBlockNavigation = useCallback(({ currentLocation, nextLocation }) => {
    const currentUrl = `${currentLocation.pathname}${currentLocation.search}${currentLocation.hash}`;
    const nextUrl = `${nextLocation.pathname}${nextLocation.search}${nextLocation.hash}`;
    return currentUrl !== nextUrl && hasUnsavedChanges;
  }, [hasUnsavedChanges]);
  const blocker = useBlocker(shouldBlockNavigation);
  const {
    state: blockerState,
    location: blockedLocation,
    proceed: proceedNavigation,
    reset: resetNavigation
  } = blocker;
  const confirmDiscardChanges = useCallback(() => (
    !hasUnsavedChanges || window.confirm(unsavedChangesPrompt)
  ), [hasUnsavedChanges]);

  useEffect(() => registerBeforeLogout(confirmDiscardChanges), [
    confirmDiscardChanges,
    registerBeforeLogout
  ]);

  useEffect(() => {
    if (blockerState !== "blocked") {
      return;
    }

    if (window.confirm(unsavedChangesPrompt)) {
      proceedNavigation();
    } else {
      resetNavigation();
    }
  }, [blockedLocation?.key, blockerState, proceedNavigation, resetNavigation]);

  useEffect(() => {
    if (!hasUnsavedChanges) {
      return;
    }

    function handleBeforeUnload(event) {
      event.preventDefault();
      event.returnValue = "";
    }

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [hasUnsavedChanges]);
}
