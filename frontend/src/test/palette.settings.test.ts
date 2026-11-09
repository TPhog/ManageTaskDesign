import { rawPaletteHits } from './paletteGuard'

const files = import.meta.glob<string>(
  ['../features/admin/settings/SettingsPage.tsx', '../features/admin/settings/SettingsSections.tsx', '../features/admin/settings/settingsForm.ts', '../features/admin/settings/SettingsSection.tsx',
    '../features/admin/settings/WatermarkSection.tsx', '../features/admin/settings/DigestSection.tsx',
    '../features/admin/settings/StorageSummarySection.tsx', '../features/admin/settings/IntakeLinkSection.tsx'],
  { query: '?raw', import: 'default', eager: true },
)

it('settings tabs use semantic tokens only', () => {
  expect(Object.keys(files)).toHaveLength(8)
  expect(rawPaletteHits(files)).toEqual([])
})
