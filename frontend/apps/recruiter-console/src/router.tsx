import { Outlet, createRootRoute, createRoute, createRouter, redirect } from "@tanstack/react-router";
import { AppLayout } from "./components/app-shell";
import { isDevAuth } from "./auth-mode";
import { useAuthStore } from "./auth-store";
import { CandidatesBoardPage } from "./pages/candidates-board-page";
import { CandidatesPage } from "./pages/candidates-page";
import { DashboardPage } from "./pages/dashboard-page";
import { EvaluationPage } from "./pages/evaluation-page";
import { LoginPage } from "./pages/login-page";
import { PipelinePage } from "./pages/pipeline-page";
import { SessionErrorPage } from "./pages/session-error-page";
import { PositionsPage } from "./pages/positions-page";
import { PositionCreatePage, PositionEditPage } from "./pages/position-form-page";
import { PositionQuestionsPage } from "./pages/position-questions-page";
import { ApplyJobPage } from "./pages/apply/apply-job-page";
import { ApplyConsentPage } from "./pages/apply/apply-consent-page";
import { ApplyFormPage } from "./pages/apply/apply-form-page";
import { ApplyDonePage, ApplyUnreadablePage } from "./pages/apply/apply-done-page";
import { OffersPage } from "./pages/offers-page";
import { InterviewsPage } from "./pages/interviews-page";
import { InterviewDetailPage } from "./pages/interview-detail-page";
import { PublicInterviewPage } from "./pages/interview/public-interview-page";

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

const appRoute = createRoute({
  getParentRoute: () => rootRoute,
  id: "/_app",
  beforeLoad: requireSession,
  component: AppLayout
});

const indexRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/",
  component: DashboardPage
});

const positionsRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/positions",
  component: PositionsPage
});

const positionCreateRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/positions/new",
  component: PositionCreatePage
});

const positionEditRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/positions/$positionId/edit",
  component: PositionEditPage
});

const positionQuestionsRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/positions/$positionId/questions",
  component: PositionQuestionsPage
});

const positionDetailRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/positions/$positionId",
  component: CandidatesPage
});

const evaluationRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/candidates/$candidateId",
  component: EvaluationPage
});

const candidatesBoardRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/candidates",
  component: CandidatesBoardPage
});

const pipelineRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/pipeline",
  component: PipelinePage
});

const interviewsRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/interviews",
  component: InterviewsPage
});

const offersRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/offers",
  component: OffersPage
});

const interviewDetailRoute = createRoute({
  getParentRoute: () => appRoute,
  path: "/interviews/$sessionId",
  component: InterviewDetailPage
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

const interviewRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/interview/$token",
  component: PublicInterviewPage
});

const routeTree = rootRoute.addChildren([
  loginRoute,
  sessionErrorRoute,
  appRoute.addChildren([
    indexRoute,
    positionsRoute,
    positionCreateRoute,
    positionEditRoute,
    positionQuestionsRoute,
    positionDetailRoute,
    candidatesBoardRoute,
    pipelineRoute,
    interviewsRoute,
    interviewDetailRoute,
    offersRoute,
    evaluationRoute
  ]),
  applyJobRoute,
  applyConsentRoute,
  applyFormRoute,
  applyDoneRoute,
  applyUnreadableRoute,
  interviewRoute
]);

export const router = createRouter({ routeTree });

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}
