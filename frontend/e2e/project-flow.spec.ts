import { test, expect, type Page } from '@playwright/test'
import { apiPost, createClient, createProjectForClient, expectToast } from './helpers'
import { configureBank, sendQuotation, apiPut } from './portalHelpers'

/** Service with a 3-step template, one deliverable on it, brief confirmed, INITIAL quotation sent and accepted. */
async function quotedProject(page: Page, stamp: number): Promise<string> {
  await page.goto('/admin')
  await configureBank(page)
  const service = await apiPost(page.request, page, '/api/admin/services', {
    name: `Logo mẫu ${stamp}`, description: '', basePrice: 2_000_000, defaultIncludedRounds: 2, extraRoundPrice: 200_000,
    taskTemplate: ['Moodboard', 'Phác thảo', 'Hoàn thiện'],
  })
  const clientId = await createClient(page, `Khách luồng ${stamp}`)
  const projectId = await createProjectForClient(page, clientId, `Dự án luồng ${stamp}`)
  await apiPost(page.request, page, `/api/admin/projects/${projectId}/deliverables`, { name: 'Logo E2E', quantity: 1, serviceId: service.id })
  const detail = await (await page.request.get(`/api/admin/projects/${projectId}`)).json()
  // an empty brief can no longer be confirmed (422) — give it content first
  await apiPut(page, `/api/admin/briefs/${detail.brief.id}`, { content: 'Brief E2E: logo tối giản, tông ấm.' })
  await apiPost(page.request, page, `/api/admin/briefs/${detail.brief.id}/confirm`, {})
  const quotation = await sendQuotation(page, projectId, 'SECRET-FLOW')
  const q = await (await page.request.get(`/api/admin/quotations/${quotation.id}`)).json()
  await apiPost(page.request, page, `/api/admin/quotations/${quotation.id}/accept`, { rowVersion: q.rowVersion })
  return projectId
}

test.describe('Project flow', () => {
  test('a new project points to its first step', async ({ page }) => {
    await page.goto('/admin')
    const clientId = await createClient(page, `Khách bước ${Date.now()}`)
    const projectId = await createProjectForClient(page, clientId, 'Dự án mới E2E')

    await page.goto(`/admin/projects/${projectId}`)

    const stepper = page.getByRole('navigation', { name: 'Giai đoạn dự án' })
    await expect(stepper.locator('[aria-current="step"]')).toContainText('Chuẩn bị')
    const nextStep = page.getByRole('region', { name: 'Bước tiếp theo' })
    await expect(nextStep.getByRole('list', { name: 'Cảnh báo' })).toContainText('Brief chưa được xác nhận')
    await nextStep.getByRole('link', { name: 'Thêm hạng mục' }).click()
    await expect(page).toHaveURL(/tab=deliverables/)
    await expect(page.getByRole('tab', { name: /Deliverables/ })).toHaveAttribute('aria-selected', 'true')
  })

  test('starting the project creates the template tasks and the deliverable shows its progress', async ({ page }) => {
    const projectId = await quotedProject(page, Date.now())
    await page.goto(`/admin/projects/${projectId}`)
    const stepper = page.getByRole('navigation', { name: 'Giai đoạn dự án' })
    const nextStep = page.getByRole('region', { name: 'Bước tiếp theo' })
    await expect(stepper.locator('[aria-current="step"]')).toContainText('Thực hiện')
    await expect(nextStep).toContainText('Chưa nhận cọc')

    await nextStep.getByRole('button', { name: 'Bắt đầu dự án' }).click()
    const dialog = page.getByRole('dialog', { name: 'Bắt đầu dự án' })
    await expect(dialog).toContainText('Chưa nhận cọc')
    await dialog.getByRole('checkbox', { name: 'Tạo công việc 1 của Logo E2E' }).uncheck()
    await dialog.getByRole('textbox', { name: 'Tên công việc 2 của Logo E2E' }).fill('Phác thảo 3 phương án')
    await dialog.getByRole('button', { name: 'Bắt đầu dự án · tạo 2 việc' }).click()
    await expectToast(page, 'Đã bắt đầu dự án · tạo 2 công việc')
    await expect(nextStep.getByRole('button', { name: 'Bắt đầu dự án' })).toHaveCount(0)

    const tasks = (await (await page.request.get(`/api/admin/tasks?projectId=${projectId}&pageSize=100`)).json())
      .items as { id: string; title: string }[]
    expect(tasks.map(t => t.title).sort()).toEqual(['Hoàn thiện', 'Phác thảo 3 phương án'])

    await apiPost(page.request, page, `/api/admin/tasks/${tasks[0].id}/move`, { status: 'DONE', sortOrder: 1024 })
    await page.goto(`/admin/projects/${projectId}?tab=deliverables`)
    const panel = page.getByRole('tabpanel')
    await expect(panel.getByRole('progressbar', { name: 'Tiến độ công việc Logo E2E' })).toHaveAttribute('aria-valuenow', '50')
    await expect(panel).toContainText('1/2 việc · 50%')
    await expect(panel).toContainText('Đang làm')

    await apiPost(page.request, page, `/api/admin/tasks/${tasks[1].id}/move`, { status: 'DONE', sortOrder: 2048 })
    await page.reload()
    await expect(nextStep.getByRole('button', { name: 'Gửi duyệt: Logo E2E' })).toBeVisible()
    await panel.getByRole('button', { name: 'Gửi duyệt Logo E2E' }).click()
    await expectToast(page, 'Đã gửi khách duyệt')
    await expect(panel).toContainText('Chờ khách duyệt')

    const visible = await (await page.request.get(`/api/admin/projects/${projectId}/activity?visibleOnly=true&pageSize=50`)).json()
    expect(visible.items.map((a: { clientMessage: string }) => a.clientMessage)).toContain('Hạng mục Logo E2E đã sẵn sàng để bạn duyệt')
  })
})
