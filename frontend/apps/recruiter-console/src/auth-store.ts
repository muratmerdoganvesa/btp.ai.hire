import { create } from "zustand";

export interface Session {
  token: string;
  tenantId: string;
  subject: string;
  roles: string[];
}

interface AuthState {
  session: Session | null;
  setSession: (session: Session) => void;
  clear: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  session: null,
  setSession: (session) => {
    set({ session });
  },
  clear: () => {
    set({ session: null });
  }
}));
