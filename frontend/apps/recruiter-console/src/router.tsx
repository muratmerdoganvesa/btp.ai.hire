import { Outlet, createRootRoute, createRoute, createRouter, redirect } from "@tanstack/react-router";
import { isDevAuth } from "./auth-mode";
import { useAuthStore } from "./auth-store";
import { CandidatesPage } from "./pages/candidates-page";
import { DashboardPage } from "./pages/dashboard-page";
import { EvaluationPage } from "./pages/evaluation-page";
import { LoginPage } from "./pages/login-page";
import { SessionErrorPage } from "./pages/session-error-page";
import { PositionsPage } from "./pages/positions-page";
import { PositionCreatePage, PositionEditPage } from "./pages/position-form-page";
import { ApplyJobPage } from "./pages/apply/apply-job-page";
import { ApplyConsentPage } from "./pages/apply/apply-consent-page";
import { ApplyFormPage } from "./pages/apply/apply-form-page";
import { ApplyDonePage, ApplyUnreadablePage } from "./pages/apply/apply-done-page";

const rootRoute = createRootRoute({
  component: Outlet
});

const requireSession = () => {
  if (useAuthStore.getState().session) {
    return;
  }

  if (isDevAuth) {
    throw redirect({ to: "/login" });
  }

  throw redirect({ to: "/session-error" });
};

const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/login",
  component: LoginPage
});

const sessionErrorRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/session-error",
  component: SessionErrorPage
});

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/",
  beforeLoad: requireSession,
  component: DashboardPage
});

const positionsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/positions",
  beforeLoad: requireSession,
  component: PositionsPage
});

const positionCreateRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/positions/new",
  beforeLoad: requireSession,
  component: PositionCreatePage
});

const positionEditRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/positions/$positionId/edit",
  beforeLoad: requireSession,
  component: PositionEditPage
});

const positionDetailRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/positions/$positionId",
  beforeLoad: requireSession,
  component: CandidatesPage
});

const evaluationRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/candidates/$candidateId",
  beforeLoad: requireSession,
  component: EvaluationPage
});

const applyJobRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/apply/$slug",
  component: ApplyJobPage
});

const applyConsentRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/apply/$slug/consent",
  component: ApplyConsentPage
});

const applyFormRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/apply/$slug/form",
  component: ApplyFormPage
});

const applyDoneRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/apply/$slug/done",
  validateSearch: (search: Record<string, unknown>) => ({
    ref: typeof search.ref === "string" ? search.ref : ""
  }),
  component: ApplyDonePage
});

const applyUnreadableRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/apply/$slug/unreadable",
  validateSearch: (search: Record<string, unknown>) => ({
    ref: typeof search.ref === "string" ? search.ref : ""
  }),
  component: ApplyUnreadablePage
});

const routeTree = rootRoute.addChildren([
  indexRoute,
  loginRoute,
  sessionErrorRoute,
  positionsRoute,
  positionCreateRoute,
  positionEditRoute,
  positionDetailRoute,
  evaluationRoute,
  applyJobRoute,
  applyConsentRoute,
  applyFormRoute,
  applyDoneRoute,
  applyUnreadableRoute
]);

export const router = createRouter({ routeTree });

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}
