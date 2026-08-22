import { Outlet, createRootRoute, createRoute, createRouter, redirect } from "@tanstack/react-router";
import { isDevAuth } from "./auth-mode";
import { useAuthStore } from "./auth-store";
import { CandidatesPage } from "./pages/candidates-page";
import { DashboardPage } from "./pages/dashboard-page";
import { EvaluationPage } from "./pages/evaluation-page";
import { LoginPage } from "./pages/login-page";
import { PositionsPage } from "./pages/positions-page";

const rootRoute = createRootRoute({
  component: Outlet
});

const requireSession = () => {
  if (!useAuthStore.getState().session) {
    throw redirect({ to: isDevAuth ? "/login" : "/" });
  }
};

const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/login",
  component: LoginPage
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

const routeTree = rootRoute.addChildren([
  indexRoute,
  loginRoute,
  positionsRoute,
  positionDetailRoute,
  evaluationRoute
]);

export const router = createRouter({ routeTree });

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}
