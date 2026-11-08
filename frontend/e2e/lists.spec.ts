import { test, expect } from '@playwright/test'
import { apiPost, createClient, createProjectForClient, createTask, expectToast } from './helpers'
import { archiveClient } from './cleanup'
import { pickOption } from './comboboxHelpers'

test.describe('Lists — search, filters, pickers (QA D-06/D-07/D-18/D-21)', () => {
  test('finds a project by its client and opens a new project right after creating it', async ({ page }) => {
    await page.goto('/admin')
    const tag = `${Date.now()}`
    const clientName = `E2E Tiệm bánh ${tag}`
    const clientId = await createClient(page, clientName)
    await createProjectForClient(page, clientId, `E2E Menu ${tag}`)

    await page.goto('/admin/projects')
    await page.getByRole('textbox', { name: 'Tìm dự án' }).fill(tag)
    await expect(page.getByRole('link', { name: `E2E Menu ${tag}` })).toBeVisible()

    await page.getByRole('button', { name: 'Tạo dự án' }).first().click()
    const dialog = page.getByRole('dialog', { name: 'Tạo dự án mới' })
    await pickOption(page, /Khách hàng/, tag, new RegExp(clientName))
    await dialog.getByLabel(/Tên dự án/).fill(`E2E Logo ${tag}`)
    await dialog.getByLabel('Deadline').fill('30/11/2026')
    await expect(dialog.getByText('Thứ Hai, 30/11/2026')).toBeVisible()
    await dialog.getByRole('button', { name: 'Tạo dự án' }).click()

    await expectToast(page, 'Đã tạo dự án')
    await expect(page).toHaveURL(/\/admin\/projects\/[0-9a-f-]{36}\?tab=deliverables$/)
    await expect(page.getByRole('heading', { name: `E2E Logo ${tag}`, level: 1 })).toBeVisible()
    await archiveClient(page, clientId)
  })

  test('long project lists are paged', async ({ page }) => {
    await page.goto('/admin/projects')
    const total = (await (await page.request.get('/api/admin/projects?pageSize=1')).json()).total as number
    test.skip(total <= 20, 'needs more than 20 projects')
    const nav = page.getByRole('navigation', { name: 'Phân trang' })
    await expect(nav).toContainText(`1–20 / ${total} dự án`)
    await nav.getByRole('button', { name: 'Trang sau' }).click()
    await expect(nav).toContainText(`21–${Math.min(40, total)} / ${total} dự án`)
  })

  test('tasks page searches across projects and counts each quick filter', async ({ page }) => {
    await page.goto('/admin')
    const tag = `${Date.now()}`
    const clientId = await createClient(page, `E2E Task KH ${tag}`)
    const projectId = await createProjectForClient(page, clientId, `E2E Task DA ${tag}`)
    await createTask(page, projectId, { title: `Phác thảo ${tag}` })
    await createTask(page, projectId, { title: `Chốt màu ${tag}` })

    await page.goto('/admin/tasks')
    await expect(page.getByText('Công việc của mọi dự án (trừ dự án đã huỷ)')).toBeVisible()
    await page.getByRole('textbox', { name: 'Tìm công việc' }).fill(tag)
    await expect(page.getByRole('row', { name: new RegExp(`Phác thảo ${tag}`) })).toBeVisible()
    await expect(page.getByRole('row', { name: new RegExp(`Chốt màu ${tag}`) })).toBeVisible()
    await expect(page.getByRole('group', { name: 'Lọc nhanh' }).getByRole('button', { name: /^Tất cả/ })).toContainText('2')
    await archiveClient(page, clientId)
  })

  test('requests inbox filters by text', async ({ page }) => {
    await page.goto('/admin')
    const tag = `${Date.now()}`
    const clientId = await createClient(page, `E2E Req KH ${tag}`)
    const projectId = await createProjectForClient(page, clientId, `E2E Req DA ${tag}`)
    await apiPost(page.request, page, `/api/admin/projects/${projectId}/requests`, { content: `Đổi màu ${tag}`, source: 'ZALO' })

    await page.goto('/admin/requests')
    await page.getByRole('textbox', { name: 'Tìm yêu cầu' }).fill(tag)
    await expect(page.getByRole('link', { name: new RegExp(`Đổi màu ${tag}`) })).toBeVisible()
    await page.getByRole('textbox', { name: 'Tìm yêu cầu' }).fill(`khong-co-${tag}`)
    await expect(page.getByText('Không có yêu cầu khớp bộ lọc')).toBeVisible()
    await archiveClient(page, clientId)
  })
})
