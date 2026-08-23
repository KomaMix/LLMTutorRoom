import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: "../wwwroot",
    emptyOutDir: true
  },
  server: {
    port: 5173,
    proxy: {
      "/api/auth": "http://localhost:5210",
      "/api/users": "http://localhost:5210",
      "/api/teaching": "http://localhost:5212",
      "/api/attempts": "http://localhost:5216",
      "/api/classroom": "http://localhost:5206",
      "/api/reviews": "http://127.0.0.1:5214",
      "/api/model-access": "http://127.0.0.1:5214",
      "/api/llm": {
        target: "http://localhost:5200",
        rewrite: path => path.replace(/^\/api\/llm/, "/api")
      }
    }
  }
});
