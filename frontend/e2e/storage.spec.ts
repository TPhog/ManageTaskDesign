import { test, expect } from '@playwright/test'
import { createProject, createTask, expectToast } from './helpers'
import { uploadTaskImage } from './imageHelpers'

test.describe('Settings › Dung lượng and watermark', () => {
  test('designer filters by project, selects images and deletes them to free space', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    const taskId = await createTask(page, projectId, { title: `Ảnh dọn dẹp ${Date.now()}` })
    await uploadTaskImage(page, taskId, 'don-a.png')
    await uploadTaskImage(page, taskId, 'don-b.png')

    await page.goto(`/admin/settings/storage?projectId=${projectId}`)
    await expect(page.getByRole('progressbar', { name: 'Dung lượng đã dùng' })).toBeVisible()
    const list = page.getByRole('region', { name: 'Danh sách ảnh' })
    await expect(list.getByRole('checkbox', { name: /^Chọn don-/ })).toHaveCount(2)

    await list.getByRole('checkbox', { name: 'Chọn don-a.png' }).check()
    await list.getByRole('button', { name: /^Xoá 1 ảnh/ }).click()
    await page.getByRole('dialog', { name: 'Xoá 1 ảnh?' }).getByRole('button', { name: 'Xoá vĩnh viễn' }).click()
    await expectToast(page, 'Đã xoá 1 ảnh')
    await expect(list.getByRole('checkbox', { name: 'Chọn don-a.png' })).toHaveCount(0)

    await list.getByRole('checkbox', { name: 'Chọn tất cả ảnh trang này' }).check()
    await list.getByRole('button', { name: /^Xoá 1 ảnh/ }).click()
    await page.getByRole('dialog', { name: 'Xoá 1 ảnh?' }).getByRole('button', { name: 'Xoá vĩnh viễn' }).click()
    await expect(list.getByText('Không có ảnh nào khớp bộ lọc')).toBeVisible()

    await page.getByRole('button', { name: /Gợi ý: ảnh của dự án đã hoàn thành/ }).click()
    await expect(page).toHaveURL(/completedOnly=1&olderThanMonths=6&sort=oldest/)
  })

  test('designer changes the watermark from the settings page', async ({ page }) => {
    await page.goto('/admin/settings')
    const section = page.getByRole('region', { name: 'Watermark ảnh' })
    await section.getByLabel('Nội dung').fill('Studio E2E · Bản xem trước')
    await section.getByRole('button', { name: 'Lưu watermark' }).click()
    await expectToast(page, 'Đã lưu watermark')
    await page.reload()
    await expect(page.getByRole('region', { name: 'Watermark ảnh' }).getByLabel('Nội dung')).toHaveValue('Studio E2E · Bản xem trước')
    // restore the default text so other specs see the studio name
    await page.getByRole('region', { name: 'Watermark ảnh' }).getByLabel('Nội dung').fill('')
    await page.getByRole('region', { name: 'Watermark ảnh' }).getByRole('button', { name: 'Lưu watermark' }).click()
    await expectToast(page, 'Đã lưu watermark')
  })
})
