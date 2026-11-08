import { test, expect, type Page } from '@playwright/test'
import { apiPost, createProject, createTask, expectToast } from './helpers'
import { pngFile, uploadTaskImage } from './imageHelpers'

async function projectRow(page: Page, projectName: string) {
  const usage = page.getByRole('region', { name: 'Dung lượng theo dự án' })
  await expect(usage.getByRole('listitem').first()).toBeVisible()
  const more = usage.getByRole('button', { name: /^Hiện thêm/ })
  if (await more.count()) await more.click()
  return usage.getByRole('listitem').filter({ hasText: projectName })
}

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
    const row = await projectRow(page, projectName)
    await expect(row).toContainText('2 ảnh')
    await row.getByRole('button', { name: `Xem ảnh của ${projectName}` }).click()
    await expect(page).toHaveURL(new RegExp(`projectId=${projectId}`))
    await expect(page.getByRole('region', { name: 'Danh sách ảnh' }).getByRole('checkbox', { name: /^Chọn (goc|ban-sao)\.png$/ }))
      .toHaveCount(2)

    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    await page.getByRole('button', { name: title, exact: true }).click()
    await drawer.getByRole('button', { name: 'Xoá công việc' }).click()
    await expect(drawer.getByText('2 ảnh của công việc cũng bị xoá vĩnh viễn')).toBeVisible()
    await drawer.getByRole('button', { name: 'Xác nhận xoá' }).click()
    await expectToast(page, 'Đã xoá công việc')

    await page.goto(`/admin/settings/storage?projectId=${projectId}`)
    await expect(page.getByRole('region', { name: 'Danh sách ảnh' }).getByText('Không có ảnh nào khớp bộ lọc')).toBeVisible()
    await expect(await projectRow(page, projectName)).toHaveCount(0)
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
    await expect(await projectRow(page, projectName)).toContainText('Hoàn thành')
    const cleanup = page.getByRole('region', { name: 'Giải phóng bản gốc' })
    await cleanup.getByRole('button', { name: '3 tháng' }).click()
    await expect(cleanup.getByRole('button', { name: '3 tháng' })).toHaveAttribute('aria-pressed', 'true')
    await expect(cleanup.getByText(projectName)).toHaveCount(0)
  })

  test('cleanup dialog needs the acknowledgement and a changed list asks for a re-check', async ({ page }) => {
    const preview = {
      olderThanMonths: 6, cutoff: '2026-05-12T03:00:00Z', projectCount: 1, imageCount: 3, freedBytes: 3 * 1024 ** 2,
      digest: 'digest-1',
      projects: [{ projectId: '00000000-0000-0000-0000-000000000001', projectName: 'Dự án cũ', completedAt: '2026-01-01T00:00:00Z', imageCount: 3, freedBytes: 3 * 1024 ** 2 }],
    }
    let postBody: Record<string, unknown> | null = null
    await page.route('**/api/admin/storage/original-cleanup**', async route => {
      if (route.request().method() === 'POST') {
        postBody = route.request().postDataJSON()
        await route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify({ status: 409, code: 'CONCURRENCY_CONFLICT', title: 'Conflict' }) })
      } else {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(preview) })
      }
    })

    await page.goto('/admin/settings/storage')
    const cleanup = page.getByRole('region', { name: 'Giải phóng bản gốc' })
    await expect(cleanup.getByText('Có thể giải phóng 3,0 MB: 3 bản gốc ở 1 dự án.')).toBeVisible()
    await cleanup.getByRole('button', { name: 'Xoá bản gốc…' }).click()
    const dialog = page.getByRole('dialog', { name: 'Xoá bản gốc của 3 ảnh?' })
    const confirm = dialog.getByRole('button', { name: 'Xoá vĩnh viễn bản gốc' })
    await expect(confirm).toBeDisabled()
    await dialog.getByRole('checkbox', { name: 'Tôi hiểu bản gốc sẽ bị xoá vĩnh viễn' }).check()
    await confirm.click()
    await expectToast(page, 'Danh sách đã thay đổi, xem lại')
    await expect(dialog).toBeHidden()
    expect(postBody).toMatchObject({ olderThanMonths: 6, expectedImageCount: 3, expectedDigest: 'digest-1' })
  })
})
