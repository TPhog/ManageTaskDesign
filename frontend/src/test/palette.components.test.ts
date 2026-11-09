import { rawPaletteHits } from './paletteGuard'

const files = import.meta.glob<string>(
  ['../components/ui/*.tsx', '../components/StatusBadge.tsx', '../components/BillingChip.tsx', '../components/EmptyState.tsx'],
  { query: '?raw', import: 'default', eager: true },
)

it('shared primitives use semantic tokens only', () => {
  expect(Object.keys(files).length).toBeGreaterThan(10)
  expect(rawPaletteHits(files)).toEqual([])
})
