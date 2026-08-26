import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import { createI18n } from "@hirelens/i18n";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { ApiError } from "@hirelens/api-client";
import { bootstrapSession, isDevAuth } from "./auth-mode";
import { router } from "./router";
import { SessionErrorPage } from "./pages/session-error-page";
import "./styles.css";

createI18n();

const queryClient = new QueryClient();
const root = document.getElementById("root");

if (!root) {
  throw new Error("Root element is missing.");
}

/** Candidate-facing entry points must not call /api/me (recruiter auth). */
function isPublicEntryPath(pathname: string): boolean {
  return pathname.startsWith("/interview/") || pathname.startsWith("/apply/");
}

const renderApp = () => {
  createRoot(root).render(
    <StrictMode>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>
  );
};

const renderSessionError = () => {
  createRoot(root).render(
    <StrictMode>
      <SessionErrorPage />
    </StrictMode>
  );
};

void (async () => {
  try {
    if (!isPublicEntryPath(window.location.pathname)) {
      await bootstrapSession();
    }
    renderApp();
  } catch (error) {
    if (isDevAuth) {
      renderApp();
      return;
    }

    const detail = error instanceof ApiError ? `${error.message}` : "me_failed";
    sessionStorage.setItem("hirelens.apiError", detail);
    renderSessionError();
  }
})();
