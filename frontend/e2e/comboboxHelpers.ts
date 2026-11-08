import type { Page } from '@playwright/test'

/** Open a SearchCombobox by its accessible name, type into its search box and pick the matching option. */
export async function pickOption(page: Page, combobox: string | RegExp, search: string, option: string | RegExp = search) {
  await page.getByRole('combobox', { name: combobox }).click()
  await page.locator('[cmdk-input]').fill(search)
  await page.getByRole('option', { name: option }).first().click()
}
