import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  resolve: { alias: { "monaco-editor/esm/vs/editor/editor.api.js": new URL("./node_modules/monaco-editor/esm/vs/editor/editor.api.js", import.meta.url).pathname } },
  server: { port: 5173, strictPort: true, proxy: {
    "/api": { target: "http://localhost:5192", changeOrigin: true },
    "/health": { target: "http://localhost:5192", changeOrigin: true },
    "/uploads": { target: "http://localhost:5192", changeOrigin: true },
    "/hubs": { target: "http://localhost:5192", changeOrigin: true, ws: true },
  } },
  preview: { port: 4173, strictPort: true },
});
