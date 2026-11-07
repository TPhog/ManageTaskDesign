import { test, expect, type Page } from '@playwright/test'
import { expectToast } from './helpers'

/** Navigate to the first available project detail page */
async function goToFirstProject(page: Page) {
  const res = await page.request.get('/api/admin/projects?pageSize=1')
  const data = await res.json()
  if (!data.items?.length) return null
  const projectId = data.items[0].id
  await page.goto(`/admin/projects/${projectId}`)
  return projectId
}

// The deliverable/brief APIs exist (M2) but the admin UI for adding deliverables and
// editing/confirming briefs has not been built yet. Tests marked fixme until that UI lands.

test.describe('Deliverables', () => {
  test('deliverables tab renders', async ({ page }) => {
    const id = await goToFirstProject(page)
    test.skip(!id, 'No projects available')

    await page.getByRole('tab', { name: /Deliverables/ }).click()
    await expect(page.getByRole('tabpanel')).toBeVisible()
  })

  test.fixme('can add a deliverable to a project', async ({ page }) => {
    const id = await goToFirstProject(page)
    test.skip(!id, 'No projects available')

    await page.getByRole('tab', { name: /Deliverables/ }).click()
    await page.getByRole('button', { name: /thêm deliverable/i }).click()
    await page.getByLabel(/tên/i).fill('Logo chính E2E')
    await page.getByLabel(/số vòng/i).fill('3')
    await page.getByLabel(/giá vòng thêm/i).fill('200000')
    await page.getByRole('button', { name: /thêm/i }).click()

    await expectToast(page, /Đã thêm/)
    await expect(page.getByText('Logo chính E2E')).toBeVisible()
  })
})

test.describe('Brief', () => {
  test('brief tab renders', async ({ page }) => {
    const id = await goToFirstProject(page)
    test.skip(!id, 'No projects available')

    await page.getByRole('tab', { name: 'Brief' }).click()
    await expect(page.getByRole('tabpanel')).toBeVisible()
  })

  test.fixme('can edit and save brief content', async ({ page }) => {
    const id = await goToFirstProject(page)
    test.skip(!id, 'No projects available')

    await page.getByRole('tab', { name: 'Brief' }).click()
    const editor = page.getByRole('textbox')
    await editor.fill('Nội dung brief E2E test - khách cần logo chuyên nghiệp.')
    await page.getByRole('button', { name: /lưu/i }).click()
    await expectToast(page, 'Đã lưu')
  })

  test.fixme('can confirm a brief', async ({ page }) => {
    const id = await goToFirstProject(page)
    test.skip(!id, 'No projects available')

    await page.getByRole('tab', { name: 'Brief' }).click()
    const confirmBtn = page.getByRole('button', { name: /xác nhận brief/i })
    await confirmBtn.click()
    await expectToast(page, 'Đã xác nhận')
    await expect(confirmBtn).not.toBeVisible()
  })
})
