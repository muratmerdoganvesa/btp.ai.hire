import { api } from "./api";
import { useAuthStore } from "./auth-store";

export const isDevAuth = import.meta.env.DEV;

export async function bootstrapSession(): Promise<void> {
  if (isDevAuth) {
    return;
  }

  const me = await api.getMe();
  if (!me.tenantId || !me.subject) {
    throw new Error("me_incomplete");
  }

  useAuthStore.getState().setSession({
    tenantId: me.tenantId,
    subject: me.subject,
    roles: me.roles
  });
}

export function logout(): void {
  useAuthStore.getState().clear();
  if (isDevAuth) {
    return;
  }

  window.location.assign("/logout");
}
