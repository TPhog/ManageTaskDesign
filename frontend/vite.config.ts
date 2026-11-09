import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'

// 07-security §4.3/§4.5: magic-link and public intake pages must not leak the token/slug through Referer.
// Production sets the same header in deploy/Caddyfile.
function portalReferrerPolicy(): Plugin {
  return {
    name: 'portal-referrer-policy',
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        if (req.url?.startsWith('/p/') || req.url?.startsWith('/f/')) res.setHeader('Referrer-Policy', 'no-referrer')
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
