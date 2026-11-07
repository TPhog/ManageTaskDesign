import { test, expect, type Page } from '@playwright/test'
import { createProject, expectToast } from './helpers'

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
  test.beforeEach(async ({ page }) => {
    await page.goto('/admin')
    const id = await createProject(page)
    await page.goto(`/admin/projects/${id}`)
    await page.getByRole('tab', { name: /Deliverables/ }).click()
  })

  test('can add a deliverable to a project', async ({ page }) => {
    await page.getByRole('button', { name: 'Thêm deliverable' }).first().click()
    const dialog = page.getByRole('dialog', { name: 'Thêm deliverable' })
    await dialog.getByLabel('Tên deliverable').fill('Logo chính E2E')
    await dialog.getByLabel('Số vòng sửa miễn phí').fill('3')
    await dialog.getByLabel('Giá vòng thêm (₫)').fill('200000')
    await dialog.getByRole('button', { name: 'Thêm', exact: true }).click()

    await expectToast(page, 'Đã thêm deliverable')
    await expect(page.getByText('Logo chính E2E')).toBeVisible()
    await expect(page.getByText('0/3 vòng')).toBeVisible()
  })

  test('can edit a deliverable and move it through statuses', async ({ page }) => {
    await page.getByRole('button', { name: 'Thêm deliverable' }).first().click()
    let dialog = page.getByRole('dialog', { name: 'Thêm deliverable' })
    await dialog.getByLabel('Tên deliverable').fill('Banner E2E')
    await dialog.getByLabel('Số vòng sửa miễn phí').fill('1')
    await dialog.getByRole('button', { name: 'Thêm', exact: true }).click()
    await expectToast(page, 'Đã thêm deliverable')

    await page.getByRole('button', { name: 'Sửa Banner E2E' }).click()
    dialog = page.getByRole('dialog', { name: 'Sửa deliverable' })
    await dialog.getByLabel('Tên deliverable').fill('Banner E2E v2')
    await dialog.getByRole('button', { name: 'Lưu', exact: true }).click()
    await expectToast(page, 'Đã cập nhật deliverable')

    await page.getByRole('button', { name: 'Đổi trạng thái Banner E2E v2' }).click()
    await page.getByRole('menuitem', { name: 'Bắt đầu làm' }).click()
    await expectToast(page, 'Đã cập nhật trạng thái')
    await expect(page.getByRole('tabpanel')).toContainText('Đang làm')
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
