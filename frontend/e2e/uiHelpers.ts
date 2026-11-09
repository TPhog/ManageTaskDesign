import type { Page, TestInfo } from '@playwright/test'

/** Every admin list page; later tasks add assertions, never rows. */
export const ADMIN_ROUTES = [
  { name: 'dashboard', path: '/admin' },
  { name: 'requests', path: '/admin/requests' },
  { name: 'projects', path: '/admin/projects' },
  { name: 'tasks', path: '/admin/tasks' },
  { name: 'quotations', path: '/admin/quotations' },
  { name: 'clients', path: '/admin/clients' },
  { name: 'services', path: '/admin/services' },
  { name: 'settings', path: '/admin/settings' },
] as const

export const WIDTHS = [1440, 390] as const

export async function capture(page: Page, testInfo: TestInfo, name: string) {
  const file = testInfo.outputPath(`${name}.png`)
  await page.screenshot({ path: file, fullPage: true })
  await testInfo.attach(name, { path: file, contentType: 'image/png' })
}

export async function overflowPx(page: Page): Promise<number> {
  return page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
}
