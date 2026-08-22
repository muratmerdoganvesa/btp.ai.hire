import { ApiClient } from "@hirelens/api-client";
import { useAuthStore } from "./auth-store";

export const api = new ApiClient("", () => useAuthStore.getState().session?.token ?? null);
