import { rawPaletteHits } from './paletteGuard'

const files = import.meta.glob<string>(
  ['../features/admin/projects/**/*.{ts,tsx}', '../features/admin/flow/**/*.{ts,tsx}', '../features/admin/revisions/**/*.{ts,tsx}',
    '../features/admin/tasks/**/*.{ts,tsx}', '../components/QuotaMeter.tsx'],
  { query: '?raw', import: 'default', eager: true },
)

it('projects, project detail, flow, revisions and tasks use semantic tokens only', () => {
  expect(Object.keys(files).length).toBeGreaterThan(40)
  expect(rawPaletteHits(files)).toEqual([])
})
