import { rawPaletteHits } from './paletteGuard'

const files = import.meta.glob<string>(
  ['../components/Tile.tsx', '../components/StatTile.tsx', '../components/PageHeader.tsx', '../components/dataTable.ts',
    '../features/admin/AdminLayout.tsx', '../features/admin/auth/LoginPage.tsx', '../features/admin/NotFoundPage.tsx'],
  { query: '?raw', import: 'default', eager: true },
)

it('admin shell uses semantic tokens only', () => {
  expect(rawPaletteHits(files)).toEqual([])
})
