import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

// The repo root — where the shared .env lives (same file the API and docker compose read).
const rootDir = resolve(dirname(fileURLToPath(import.meta.url)), '..')

// https://vite.dev/config/
// Dev: proxy /api to the local API so the SPA is same-origin (mirrors production, where Nginx
// proxies /api → the API container). This avoids cross-origin/CORS quirks in the browser — set
// VITE_API_BASE empty so the client uses relative /api paths that this proxy forwards.
export default defineConfig(({ mode }) => {
  // Read the shared root .env (all keys, no VITE_ prefix filter — server-side only, never shipped).
  const env = loadEnv(mode, rootDir, '')
  const webPort = Number(env.WEB_PORT ?? 5173)
  // Proxy target: explicit override, else derived from the API's port in the same .env.
  const apiTarget = env.VITE_API_PROXY_TARGET ?? `http://localhost:${env.API_PORT ?? 5253}`

  return {
    plugins: [react()],
    server: {
      port: webPort,
      strictPort: true,
      proxy: {
        '/api': { target: apiTarget, changeOrigin: true },
      },
    },
  }
})
