import { rawPaletteHits } from './paletteGuard'

const files = import.meta.glob<string>(
  [
    '../features/admin/**/*.{ts,tsx}',
    '../components/**/*.{ts,tsx}',
    '../features/portal/**/*.{ts,tsx}',
    '../features/public/**/*.{ts,tsx}',
  ],
  { query: '?raw', import: 'default', eager: true },
)

it('globs the admin, shared, portal and public folders', () => {
  const paths = Object.keys(files)
  for (const part of ['/features/admin/', '/components/', '/features/portal/', '/features/public/']) {
    expect(paths.some(p => p.includes(part)), part).toBe(true)
  }
})

it('no admin, portal or public page and no shared component colours itself outside the design tokens', () => {
  expect(rawPaletteHits(files)).toEqual([])
})
