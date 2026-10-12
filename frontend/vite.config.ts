import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.VITE_API_PROXY_TARGET || 'http://localhost:5080'

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: { '@': path.resolve(import.meta.dirname, './src') },
    },
    build: {
      rolldownOptions: {
        output: {
          // Long-lived vendor chunks: cached across deploys and kept under the size warning.
          codeSplitting: {
            groups: [
              // Module ids always use "/" (also on Windows).
              { name: "react", test: /node_modules\/(react|react-dom|react-router|scheduler)\// },
              { name: "ui", test: /node_modules\/(@base-ui|@floating-ui|lucide-react|sonner)\// },
              { name: "forms", test: /node_modules\/(zod|react-hook-form|@hookform)\// },
              { name: "charts", test: /node_modules\/(recharts|d3-|victory-vendor|@reduxjs|immer|reselect|redux)/ },
            ],
          },
        },
      },
    },
    server: {
      port: 5173,
      strictPort: true,
      // Same-origin proxy: the auth cookie never crosses origins in development.
      proxy: {
        '/api': { target: apiTarget, changeOrigin: false },
      },
    },
  }
})
