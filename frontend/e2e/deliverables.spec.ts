import { test, expect } from '@playwright/test'
import { login, API_BASE, expectToast } from './helpers'

/** Navigate to the first available project detail page */
async function goToFirstProject(page: import('@playwright/test').Page) {
  const res = await page.request.get(`${API_BASE}/api/admin/projects?pageSize=1`)
  const data = await res.json()
  if (!data.items?.length) return null
  const projectId = data.items[0].id
  await page.goto(`/admin/projects/${projectId}`)
  return projectId
}

test.describe('Deliverables', () => {
  test.beforeEach(async ({ page }) => {
    await login(page)
  })

  test('deliverables tab shows add button', async ({ page }) => {
    const id = await goToFirstProject(page)
    test.skip(!id, 'No projects available')

    await page.getByRole('tab', { name: /deliverable/i }).click()
    await expect(page.getByRole('button', { name: /thêm deliverable/i })).toBeVisible()
  })

  test('can add a deliverable to a project', async ({ page }) => {
    const id = await goToFirstProject(page)
    test.skip(!id, 'No projects available')

    await page.getByRole('tab', { name: /deliverable/i }).click()
    await page.getByRole('button', { name: /thêm deliverable/i }).click()

    await page.getByLabel(/tên/i).fill('Logo chính E2E')
    await page.getByLabel(/số vòng/i).fill('3')
    await page.getByLabel(/giá vòng thêm/i).fill('200000')
    await page.getByRole('button', { name: /thêm/i }).click()

    await expectToast(page, 'thành công')
    await expect(page.getByText('Logo chính E2E')).toBeVisible()
  })
})

test.describe('Brief', () => {
  test.beforeEach(async ({ page }) => {
    await login(page)
  })

  test('brief tab shows editor', async ({ page }) => {
    const id = await goToFirstProject(page)
    test.skip(!id, 'No projects available')

    await page.getByRole('tab', { name: /brief/i }).click()
    // Brief draft editor should be visible
    await expect(page.getByRole('textbox')).toBeVisible()
  })

  test('can edit and save brief content', async ({ page }) => {
    const id = await goToFirstProject(page)
    test.skip(!id, 'No projects available')

    await page.getByRole('tab', { name: /brief/i }).click()

    const editor = page.getByRole('textbox')
    await editor.clear()
    await editor.fill('Nội dung brief E2E test - khách cần logo chuyên nghiệp.')

    await page.getByRole('button', { name: /lưu/i }).click()
    await expectToast(page, 'Đã lưu')
  })

  test('can confirm a brief', async ({ page }) => {
    const id = await goToFirstProject(page)
    test.skip(!id, 'No projects available')

    await page.getByRole('tab', { name: /brief/i }).click()

    // Brief must be in DRAFT status to confirm
    const confirmBtn = page.getByRole('button', { name: /xác nhận brief/i })
    if (await confirmBtn.isVisible()) {
      await confirmBtn.click()
      await expectToast(page, 'Đã xác nhận')
      // Confirm button should disappear (brief now CONFIRMED)
      await expect(confirmBtn).not.toBeVisible()
    }
  })
})
