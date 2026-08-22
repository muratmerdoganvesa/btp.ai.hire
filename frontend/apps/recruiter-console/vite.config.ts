import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:5080",
      "/dev": "http://localhost:5080",
      "/health": "http://localhost:5080",
      "/compliance": "http://localhost:5080"
    }
  }
});
