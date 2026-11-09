import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router-dom'
import { z } from 'zod'
import { Toaster } from '@/components/ui/sonner'
import { router } from './router'
// Self-hosted fonts (P16): every family ships a `vietnamese` unicode-range subset with font-display: swap.
import '@fontsource-variable/bricolage-grotesque/opsz.css'
import '@fontsource/be-vietnam-pro/400.css'
import '@fontsource/be-vietnam-pro/500.css'
import '@fontsource/be-vietnam-pro/600.css'
import '@fontsource/be-vietnam-pro/700.css'
import '@fontsource-variable/fraunces/opsz.css'
import '@fontsource-variable/fraunces/opsz-italic.css'
import '@fontsource-variable/public-sans/wght.css'
import './index.css'

// The production CSP has no 'unsafe-eval': skip zod's `new Function` probe so it does not report a CSP violation
z.config({ jitless: true })

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 30,
      retry: 1,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      {/* bottom-right (DF-21), lifted so toasts clear sticky primary buttons; on mobile they sit above the bottom action bar */}
      <Toaster richColors position="bottom-right" offset={{ bottom: 88, right: 24 }} mobileOffset={{ bottom: 96, left: 12, right: 12 }} visibleToasts={3} />
    </QueryClientProvider>
  </StrictMode>,
)
