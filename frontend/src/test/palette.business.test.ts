import { rawPaletteHits } from './paletteGuard'

const files = import.meta.glob<string>(
  ['../features/admin/quotations/**/*.{ts,tsx}', '../features/admin/clients/**/*.{ts,tsx}', '../features/admin/requests/**/*.{ts,tsx}',
    '../features/admin/services/**/*.{ts,tsx}', '../features/admin/settings/**/*.{ts,tsx}', '../features/admin/search/**/*.{ts,tsx}',
    '../features/admin/intake/**/*.{ts,tsx}', '../features/admin/request-templates/**/*.{ts,tsx}', '../components/*.{ts,tsx}',
    '../features/public/**/*.{ts,tsx}',
    '!../components/QuotaMeter.tsx'], // QuotaMeter belongs to Task 6 (runs in parallel)
  { query: '?raw', import: 'default', eager: true },
)

it('quotations, clients, requests, services, settings and shared components use semantic tokens only', () => {
  expect(Object.keys(files).length).toBeGreaterThan(40)
  expect(rawPaletteHits(files)).toEqual([])
})
