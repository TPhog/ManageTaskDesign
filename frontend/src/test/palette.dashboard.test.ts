import { rawPaletteHits } from './paletteGuard'

const files = import.meta.glob<string>('../features/admin/dashboard/*.{ts,tsx}', { query: '?raw', import: 'default', eager: true })

it('dashboard uses semantic tokens only', () => {
  expect(Object.keys(files).length).toBeGreaterThan(10)
  expect(rawPaletteHits(files)).toEqual([])
})
