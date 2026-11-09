import { test, expect, type Page } from '@playwright/test'
import { createDeliverable, createProject, expectToast } from './helpers'

async function projectStatus(page: Page, id: string): Promise<string> {
  return (await (await page.request.get(`/api/admin/projects/${id}`)).json()).status
}

async function chooseStatus(page: Page, label: string) {
  await page.getByRole('button', { name: 'Đổi trạng thái' }).click()
  await page.getByRole('menuitem', { name: label }).click()
}

test.describe('Project detail (QA D-01, D-08, D-27)', () => {
  test('shows the server warnings, lets the designer confirm and asks a reason to reopen', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page, `E2E Trạng thái ${Date.now()}`)
    await createDeliverable(page, projectId, 'Logo')
    await page.goto(`/admin/projects/${projectId}`)

    await chooseStatus(page, 'Bắt đầu làm')
    const warn = page.getByRole('dialog', { name: /Cần xác nhận/ })
    await expect(warn).toContainText('Chưa có báo giá gốc được khách chấp nhận.')
    await warn.getByRole('button', { name: 'Vẫn tiếp tục' }).click()
    await expectToast(page, 'Đã chuyển dự án')
    await expect.poll(() => projectStatus(page, projectId)).toBe('IN_PROGRESS')

    await chooseStatus(page, 'Hoàn thành')
    await expect(warn).toContainText('Còn hạng mục chưa bàn giao')
    await warn.getByRole('button', { name: 'Vẫn tiếp tục' }).click()
    await expect.poll(() => projectStatus(page, projectId)).toBe('COMPLETED')

    await chooseStatus(page, 'Mở lại dự án')
    const reason = page.getByRole('dialog', { name: 'Mở lại dự án' })
    await expect(reason.getByRole('button', { name: 'Mở lại dự án' })).toBeDisabled()
    await reason.getByLabel('Lý do mở lại').fill('Khách cần thêm một bản in')
    await reason.getByRole('button', { name: 'Mở lại dự án' }).click()
    // reopening a completed project no longer warns: it goes straight through with the reason
    await expect.poll(() => projectStatus(page, projectId)).toBe('IN_PROGRESS')
  })

  test('the selected tab is kept in the URL', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    await page.goto(`/admin/projects/${projectId}`)
    await page.getByRole('tab', { name: 'Báo giá' }).click()
    await expect(page).toHaveURL(new RegExp(`/admin/projects/${projectId}\\?tab=quotations$`))
    await page.reload()
    await expect(page.getByRole('tab', { name: 'Báo giá' })).toHaveAttribute('aria-selected', 'true')
  })

  test('an empty brief cannot be confirmed; a filled one asks first', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    await page.goto(`/admin/projects/${projectId}?tab=brief`)
    await expect(page.getByRole('button', { name: 'Xác nhận brief' })).toBeDisabled()
    await page.getByLabel('Nội dung brief').fill('Logo tông xanh, phong cách tối giản')
    await page.getByRole('button', { name: 'Xác nhận brief' }).click()
    await page.getByRole('dialog', { name: /Xác nhận brief phiên bản 1/ }).getByRole('button', { name: 'Xác nhận', exact: true }).click()
    await expectToast(page, 'Đã xác nhận brief')
  })
})
