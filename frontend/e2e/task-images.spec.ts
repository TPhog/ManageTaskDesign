import { test, expect } from '@playwright/test'
import { createProject, createTask, expectToast } from './helpers'
import { pngFile } from './imageHelpers'

test.describe('Images — task drawer and progress updates', () => {
  test('designer adds images to a task, hides one, views and deletes another', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    const title = `Ảnh task E2E ${Date.now()}`
    await createTask(page, projectId, { title })
    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    await page.getByRole('button', { name: title, exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Chi tiết công việc' })

    await drawer.locator('#task-images-input').setInputFiles([pngFile('mau-1.png'), pngFile('mau-2.png')])
    await expectToast(page, 'Đã tải 2 ảnh')
    const section = drawer.getByRole('region', { name: /Hình ảnh/ })
    // thumbnails sit inside buttons (children presentational) → locate the <img> by alt
    await expect(section.locator('img[alt="mau-1.png"]')).toHaveAttribute('src', /\/api\/admin\/images\/[0-9a-f-]+\/thumb$/)

    await section.getByRole('button', { name: 'Ẩn mau-1.png khỏi khách' }).click()
    await expect(section.getByRole('button', { name: 'Cho khách xem mau-1.png' })).toBeVisible()

    await section.getByRole('button', { name: 'Xem ảnh mau-2.png' }).click()
    const viewer = page.getByRole('dialog', { name: 'mau-2.png' })
    await expect(viewer.getByRole('img', { name: 'mau-2.png' })).toHaveAttribute('src', /\/preview$/)
    await page.keyboard.press('Escape')
    await expect(viewer).toBeHidden()

    await section.getByRole('button', { name: 'Xoá mau-2.png' }).click()
    await page.getByRole('dialog', { name: 'Xoá ảnh?' }).getByRole('button', { name: 'Xoá ảnh' }).click()
    await expectToast(page, 'Đã xoá ảnh')
    await expect(section.getByRole('button', { name: 'Xem ảnh mau-2.png' })).toHaveCount(0)
  })

  test('designer posts a progress update with a photo and sees it under "Khách đang thấy"', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    const message = `Đã xong phác thảo ${Date.now()}`
    await page.goto(`/admin/projects/${projectId}`)

    await page.getByLabel('Cập nhật tiến độ cho khách').fill(message)
    await page.locator('#progress-update-images').setInputFiles([pngFile('tien-do.png')])
    await expect(page.getByRole('list', { name: 'Ảnh sẽ gửi' })).toContainText('tien-do.png')
    await page.getByRole('button', { name: 'Gửi cập nhật' }).click()
    await expectToast(page, 'Đã gửi cập nhật cho khách')

    const entry = page.getByRole('listitem').filter({ hasText: message })
    await expect(entry.locator('img[alt="tien-do.png"]')).toHaveAttribute('src', /\/thumb$/)
  })
})
