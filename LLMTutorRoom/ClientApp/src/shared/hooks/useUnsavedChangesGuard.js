import { useCallback, useEffect } from "react";
import { useBlocker } from "react-router-dom";
import { useNavigationGuard } from "../../app/NavigationGuardContext.jsx";

export function useUnsavedChangesGuard(hasChanges, prompt) {
  const { registerBeforeLogout } = useNavigationGuard();
  const shouldBlockNavigation = useCallback(({ currentLocation, nextLocation }) => {
    const currentUrl = `${currentLocation.pathname}${currentLocation.search}${currentLocation.hash}`;
    const nextUrl = `${nextLocation.pathname}${nextLocation.search}${nextLocation.hash}`;
    return currentUrl !== nextUrl && hasChanges;
  }, [hasChanges]);
  const blocker = useBlocker(shouldBlockNavigation);
  const {
    state: blockerState,
    location: blockedLocation,
    proceed: proceedNavigation,
    reset: resetNavigation
  } = blocker;
  const confirmDiscard = useCallback(() => (
    !hasChanges || window.confirm(prompt)
  ), [hasChanges, prompt]);

  useEffect(() => registerBeforeLogout(confirmDiscard), [
    confirmDiscard,
    registerBeforeLogout
  ]);

  useEffect(() => {
    if (blockerState !== "blocked") {
      return;
    }

    if (window.confirm(prompt)) {
      proceedNavigation();
    } else {
      resetNavigation();
    }
  }, [
    blockedLocation?.key,
    blockerState,
    proceedNavigation,
    prompt,
    resetNavigation
  ]);

  useEffect(() => {
    if (!hasChanges) {
      return;
    }

    function handleBeforeUnload(event) {
      event.preventDefault();
      event.returnValue = "";
    }

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [hasChanges]);

  return confirmDiscard;
}
