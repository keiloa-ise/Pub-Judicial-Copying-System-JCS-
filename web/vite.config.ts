import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
// Dev: proxy /api to the local API so the SPA is same-origin (mirrors production, where Nginx
// proxies /api → the API container). This avoids cross-origin/CORS quirks in the browser — set
// VITE_API_BASE empty so the client uses relative /api paths that this proxy forwards.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': { target: 'http://localhost:5253', changeOrigin: true },
    },
  },
})
