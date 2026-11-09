import { test, expect } from '@playwright/test'
import { createProject, expectToast } from './helpers'

test.describe('Deliverables', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/admin')
    const id = await createProject(page)
    await page.goto(`/admin/projects/${id}`)
    await page.getByRole('tab', { name: /Hạng mục/ }).click()
  })

  test('can add a deliverable to a project', async ({ page }) => {
    await page.getByRole('button', { name: 'Thêm hạng mục' }).first().click()
    const dialog = page.getByRole('dialog', { name: 'Thêm hạng mục' })
    await dialog.getByLabel('Tên hạng mục').fill('Logo chính E2E')
    await dialog.getByLabel('Số vòng sửa miễn phí').fill('3')
    await dialog.getByLabel('Giá vòng sửa thêm (₫)').fill('200000')
    await dialog.getByRole('button', { name: 'Thêm', exact: true }).click()

    await expectToast(page, 'Đã thêm hạng mục')
    await expect(page.getByText('Logo chính E2E')).toBeVisible()
    await expect(page.getByText('0/3 vòng')).toBeVisible()
  })

  test('can edit a deliverable and move it through statuses', async ({ page }) => {
    await page.getByRole('button', { name: 'Thêm hạng mục' }).first().click()
    let dialog = page.getByRole('dialog', { name: 'Thêm hạng mục' })
    await dialog.getByLabel('Tên hạng mục').fill('Banner E2E')
    await dialog.getByLabel('Số vòng sửa miễn phí').fill('1')
    await dialog.getByRole('button', { name: 'Thêm', exact: true }).click()
    await expectToast(page, 'Đã thêm hạng mục')

    await page.getByRole('button', { name: 'Sửa Banner E2E' }).click()
    dialog = page.getByRole('dialog', { name: 'Sửa hạng mục' })
    await dialog.getByLabel('Tên hạng mục').fill('Banner E2E v2')
    await dialog.getByRole('button', { name: 'Lưu', exact: true }).click()
    await expectToast(page, 'Đã cập nhật hạng mục')

    await page.getByRole('button', { name: 'Đổi trạng thái Banner E2E v2' }).click()
    await page.getByRole('menuitem', { name: 'Bắt đầu làm' }).click()
    await expectToast(page, 'Đã cập nhật trạng thái')
    await expect(page.getByRole('tabpanel')).toContainText('Đang làm')
  })
})

test.describe('Brief', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/admin')
    const id = await createProject(page)
    await page.goto(`/admin/projects/${id}`)
    await page.getByRole('tab', { name: 'Brief' }).click()
  })

  test('can edit and save brief content', async ({ page }) => {
    await page.locator('#brief-content').fill('Khách cần logo chuyên nghiệp, tông xanh.')
    await page.getByRole('button', { name: 'Lưu brief' }).click()
    await expectToast(page, 'Đã lưu brief')
    await page.reload()
    await page.getByRole('tab', { name: 'Brief' }).click()
    // a saved brief opens in the Markdown preview
    await expect(page.getByRole('tabpanel', { name: 'Brief' })).toContainText('Khách cần logo chuyên nghiệp, tông xanh.')
  })

  test('keeps unsaved brief edits when switching tabs', async ({ page }) => {
    await page.locator('#brief-content').fill('Bản nháp chưa lưu')
    await page.getByRole('tab', { name: /Hạng mục/ }).click()
    await page.getByRole('tab', { name: 'Brief' }).click()
    await expect(page.locator('#brief-content')).toHaveValue('Bản nháp chưa lưu')
  })

  test('can confirm a brief and start a new version', async ({ page }) => {
    await page.locator('#brief-content').fill('Brief v1 E2E')
    await page.getByRole('button', { name: 'Xác nhận brief' }).click()
    await page.getByRole('dialog', { name: /Xác nhận brief phiên bản 1/ }).getByRole('button', { name: 'Xác nhận', exact: true }).click()
    await expectToast(page, 'Đã xác nhận brief')
    await expect(page.locator('#brief-content')).toHaveCount(0)
    await expect(page.getByRole('tabpanel')).toContainText('Brief v1 E2E')

    await page.getByRole('button', { name: 'Tạo phiên bản mới' }).click()
    await expectToast(page, 'Đã tạo phiên bản mới')
    await expect(page.getByRole('tabpanel', { name: 'Brief' })).toContainText('Brief v1 E2E')
    await expect(page.getByRole('tabpanel')).toContainText('Phiên bản 2')
  })
})
