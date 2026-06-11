import { defineConfig } from "vite";

export default defineConfig({
  server: {
    port: 5173,
    proxy: {
      // Forward API calls to the FastAPI backend during development.
      "/game": "http://localhost:8000",
      // Live game-state feed.
      "/ws": { target: "ws://localhost:8000", ws: true },
    },
  },
  build: {
    outDir: "dist",
  },
});
