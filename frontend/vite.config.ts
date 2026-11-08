import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'

// 07-security §4.3: magic-link pages must not leak the token through Referer.
// Production sets the same header in deploy/Caddyfile.
function portalReferrerPolicy(): Plugin {
  return {
    name: 'portal-referrer-policy',
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        if (req.url?.startsWith('/p/')) res.setHeader('Referrer-Policy', 'no-referrer')
        next()
      })
    },
  }
}

export default defineConfig({
  plugins: [react(), tailwindcss(), portalReferrerPolicy()],
  resolve: {
    alias: {
      '@': `${import.meta.dirname}/src`,
    },
  },
  server: {
    port: 5173,
    strictPort: false,
    // dev server reached through the designer's tunnel domain
    allowedHosts: ['cam.akers.date'],
    proxy: {
      '/api': 'http://localhost:5261',
    },
  },
})
