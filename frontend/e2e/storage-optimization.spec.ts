import { test, expect } from '@playwright/test'
import { apiPost, createProject, createTask, expectToast } from './helpers'
import { pngFile, uploadTaskImage } from './imageHelpers'

test.describe('Storage optimisation (P10)', () => {
  test('duplicate upload is flagged, usage is listed per project and deleting the task frees its images', async ({ page }) => {
    await page.goto('/admin')
    const projectName = `Dự án dung lượng ${Date.now()}`
    const projectId = await createProject(page, projectName)
    const title = `Ảnh trùng ${Date.now()}`
    const taskId = await createTask(page, projectId, { title })
    await uploadTaskImage(page, taskId, 'goc.png')

    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    await page.getByRole('button', { name: title, exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Chi tiết công việc' })
    await drawer.locator('#task-images-input').setInputFiles([pngFile('ban-sao.png')])
    await expectToast(page, 'Đã tải 1 ảnh')
    await expectToast(page, /"ban-sao\.png" trùng với ảnh đã có \(Công việc: /)

    await page.goto('/admin/settings/storage')
    const usage = page.getByRole('region', { name: 'Dung lượng theo dự án' })
    const row = usage.getByRole('listitem').filter({ hasText: projectName })
    await expect(row).toContainText('2 ảnh')
    await row.getByRole('button', { name: `Xem ảnh của ${projectName}` }).click()
    await expect(page).toHaveURL(new RegExp(`projectId=${projectId}`))
    await expect(page.getByRole('region', { name: 'Danh sách ảnh' }).getByRole('checkbox', { name: /^Chọn (goc|ban-sao)\.png$/ }))
      .toHaveCount(2)

    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    await page.getByRole('button', { name: title, exact: true }).click()
    await drawer.getByRole('button', { name: 'Xoá công việc' }).click()
    await expect(drawer.getByText('Ảnh của công việc cũng bị xoá vĩnh viễn')).toBeVisible()
    await drawer.getByRole('button', { name: 'Xác nhận xoá' }).click()
    await expectToast(page, 'Đã xoá công việc')

    await page.goto(`/admin/settings/storage?projectId=${projectId}`)
    await expect(page.getByRole('region', { name: 'Danh sách ảnh' }).getByText('Không có ảnh nào khớp bộ lọc')).toBeVisible()
    await expect(page.getByRole('region', { name: 'Dung lượng theo dự án' }).getByRole('listitem').filter({ hasText: projectName }))
      .toHaveCount(0)
  })

  test('original cleanup previews only projects completed long enough ago', async ({ page }) => {
    await page.goto('/admin')
    const projectName = `Dự án vừa xong ${Date.now()}`
    const projectId = await createProject(page, projectName)
    const taskId = await createTask(page, projectId, { title: 'Ảnh bàn giao' })
    await uploadTaskImage(page, taskId, 'ban-giao.png')
    await apiPost(page.request, page, `/api/admin/projects/${projectId}/status`, { to: 'IN_PROGRESS', confirmWarnings: true })
    await apiPost(page.request, page, `/api/admin/projects/${projectId}/status`, { to: 'COMPLETED', confirmWarnings: true })

    await page.goto('/admin/settings/storage')
    await expect(page.getByRole('region', { name: 'Dung lượng theo dự án' }).getByRole('listitem').filter({ hasText: projectName }))
      .toContainText('Hoàn thành')
    const cleanup = page.getByRole('region', { name: 'Giải phóng bản gốc' })
    await cleanup.getByRole('button', { name: '3 tháng' }).click()
    await expect(cleanup.getByRole('button', { name: '3 tháng' })).toHaveAttribute('aria-pressed', 'true')
    await expect(cleanup.getByText(/^(Có thể giải phóng|Không có bản gốc nào để xoá)/)).toBeVisible()
    await expect(cleanup.getByText(projectName)).toHaveCount(0)
  })
})
