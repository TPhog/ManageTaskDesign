import { test, expect, type Page } from '@playwright/test'
import { apiPost, createClient, createProjectForClient, expectToast, moveDeliverable } from './helpers'
import { configureBank, issuePortalLink, openAsClient, sendQuotation, apiPut } from './portalHelpers'

interface QuotedProject {
  clientId: string
  projectId: string
  deliverableId: string
}

/** Service (3.000.000 ₫, 2 template steps) → project → deliverable "Logo E2E" → brief confirmed → INITIAL sent + accepted. */
async function quotedProject(page: Page, stamp: number): Promise<QuotedProject> {
  await page.goto('/admin')
  await configureBank(page)
  const service = await apiPost(page.request, page, '/api/admin/services', {
    name: `Logo trọn luồng ${stamp}`, description: '', basePrice: 3_000_000, defaultIncludedRounds: 2, extraRoundPrice: 200_000,
    taskTemplate: ['Phác thảo', 'Hoàn thiện'],
  })
  const clientId = await createClient(page, `Khách trọn luồng ${stamp}`)
  const projectId = await createProjectForClient(page, clientId, `Dự án trọn luồng ${stamp}`)
  const deliverable = await apiPost(page.request, page, `/api/admin/projects/${projectId}/deliverables`,
    { name: 'Logo E2E', quantity: 1, serviceId: service.id })
  const detail = await (await page.request.get(`/api/admin/projects/${projectId}`)).json()
  // an empty brief can no longer be confirmed (422) — give it content first
  await apiPut(page, `/api/admin/briefs/${detail.brief.id}`, { content: 'Brief E2E: logo tối giản, tông ấm.' })
  await apiPost(page.request, page, `/api/admin/briefs/${detail.brief.id}/confirm`, {})
  const quotation = await sendQuotation(page, projectId, 'SECRET-FULL-FLOW')
  const q = await (await page.request.get(`/api/admin/quotations/${quotation.id}`)).json()
  await apiPost(page.request, page, `/api/admin/quotations/${quotation.id}/accept`, { rowVersion: q.rowVersion })
  return { clientId, projectId, deliverableId: deliverable.id }
}

test.describe('Project flow end to end', () => {
  test('designer follows the next steps to completion while the client approves on the portal', async ({ page, browser }) => {
    test.setTimeout(120_000)
    const { clientId, projectId } = await quotedProject(page, Date.now())

    // Bắt đầu dự án với task mẫu
    await page.goto(`/admin/projects/${projectId}`)
    const stepper = page.getByRole('navigation', { name: 'Giai đoạn dự án' })
    const nextStep = page.getByRole('region', { name: 'Bước tiếp theo' })
    await expect(stepper.locator('[aria-current="step"]')).toContainText('Thực hiện')
    await nextStep.getByRole('button', { name: 'Bắt đầu dự án' }).click()
    const dialog = page.getByRole('dialog', { name: 'Bắt đầu dự án' })
    await expect(dialog.getByRole('textbox', { name: 'Tên công việc 1 của Logo E2E' })).toHaveValue('Phác thảo')
    await expect(dialog.getByRole('textbox', { name: 'Tên công việc 2 của Logo E2E' })).toHaveValue('Hoàn thiện')
    await dialog.getByRole('button', { name: /^Bắt đầu dự án/ }).click()
    await expectToast(page, 'Đã bắt đầu dự án')

    // Xong task → gửi duyệt
    const tasks = (await (await page.request.get(`/api/admin/tasks?projectId=${projectId}&pageSize=100`)).json())
      .items as { id: string; title: string }[]
    expect(tasks.map(t => t.title).sort()).toEqual(['Hoàn thiện', 'Phác thảo'])
    for (const [i, t] of tasks.entries())
      await apiPost(page.request, page, `/api/admin/tasks/${t.id}/move`, { status: 'DONE', sortOrder: 1024 * (i + 1) })
    await page.reload()
    await nextStep.getByRole('button', { name: 'Gửi duyệt: Logo E2E' }).click()
    await page.getByRole('dialog', { name: 'Gửi hạng mục cho khách duyệt?' }).getByRole('button', { name: 'Gửi duyệt', exact: true }).click()
    await expectToast(page, 'Đã gửi khách duyệt')
    await expect(stepper.locator('[aria-current="step"]')).toContainText('Duyệt & bàn giao')

    // Khách duyệt trên portal
    const link = await issuePortalLink(page, clientId)
    const client = await openAsClient(browser, link)
    await expect(client.page).toHaveURL(/\/portal$/)
    await client.page.goto(`/portal/projects/${projectId}`)
    const row = client.page.getByRole('region', { name: 'Sản phẩm' }).getByRole('listitem').filter({ hasText: 'Logo E2E' })
    await expect(row).toContainText('Chờ bạn duyệt')
    await expect(row).toContainText('Tiến độ 100%')
    await row.getByRole('button', { name: 'Duyệt Logo E2E' }).click()
    await client.page.getByRole('dialog', { name: 'Duyệt Logo E2E?' }).getByRole('button', { name: 'Duyệt', exact: true }).click()
    await expect(row).toContainText('Bạn đã duyệt — đang chờ nhà thiết kế xác nhận')
    await expect(row.getByRole('button', { name: 'Duyệt Logo E2E' })).toHaveCount(0)
    await expect(client.page.getByRole('region', { name: 'Hoạt động gần đây' })).toContainText('Khách đã duyệt Logo E2E')
    await client.context.close()

    // Designer xác nhận → bàn giao → hoàn thành (cảnh báo còn nợ, không chặn)
    await page.goto(`/admin/projects/${projectId}?tab=deliverables`)
    await expect(page.getByRole('tabpanel')).toContainText('Khách đã duyệt trên portal')
    await nextStep.getByRole('button', { name: 'Xác nhận khách đã duyệt: Logo E2E' }).click()
    await page.getByRole('dialog', { name: 'Xác nhận hạng mục đã được duyệt?' }).getByRole('button', { name: 'Xác nhận đã duyệt' }).click()
    await expectToast(page, 'Đã xác nhận khách duyệt')
    await nextStep.getByRole('button', { name: 'Bàn giao: Logo E2E' }).click()
    await page.getByRole('dialog', { name: 'Bàn giao hạng mục?' }).getByRole('button', { name: 'Bàn giao', exact: true }).click()
    await expectToast(page, 'Đã cập nhật trạng thái')
    await expect(stepper.locator('[aria-current="step"]')).toContainText('Hoàn tất')
    await expect(nextStep).toContainText('Khách còn nợ 3.000.000 ₫')
    await nextStep.getByRole('button', { name: 'Hoàn thành dự án' }).click()
    // the server's warnings come in the shared status dialog (money still due), then the designer confirms
    const confirm = page.getByRole('dialog', { name: /Cần xác nhận: Hoàn thành/ })
    await expect(confirm).toContainText('chưa thu đủ tiền')
    await confirm.getByRole('button', { name: 'Vẫn tiếp tục' }).click()
    await expectToast(page, 'Đã chuyển dự án: Hoàn thành')
    await expect(stepper.getByRole('listitem').filter({ hasText: 'Hoàn tất' })).toContainText('đã xong')
    await expect(nextStep).toContainText('Khách còn nợ 3.000.000 ₫')
    await expect(nextStep.getByRole('button', { name: 'Sao chép tin nhắn nhắc thanh toán' })).toBeVisible()
  })

  test('client asks for a change on a deliverable under review and the designer is pointed to it', async ({ page, browser }) => {
    const { clientId, projectId, deliverableId } = await quotedProject(page, Date.now())
    await moveDeliverable(page, deliverableId, 'IN_PROGRESS', 'IN_REVIEW')

    const link = await issuePortalLink(page, clientId)
    const client = await openAsClient(browser, link)
    await expect(client.page).toHaveURL(/\/portal$/)
    await client.page.goto(`/portal/projects/${projectId}`)
    const row = client.page.getByRole('region', { name: 'Sản phẩm' }).getByRole('listitem').filter({ hasText: 'Logo E2E' })
    await row.getByRole('button', { name: 'Yêu cầu sửa Logo E2E' }).click()
    const dialog = client.page.getByRole('dialog', { name: 'Yêu cầu sửa Logo E2E' })
    await dialog.getByLabel('Nội dung cần sửa').fill('Đổi màu chữ sang xanh navy')
    await dialog.getByRole('button', { name: 'Gửi yêu cầu' }).click()
    await expect(dialog).toContainText('Đã gửi yêu cầu')
    await client.context.close()

    const requests = await (await page.request.get(`/api/admin/requests?status=NEW&projectId=${projectId}`)).json()
    const items = requests.items as { content: string; deliverableId: string | null }[]
    expect(items.some(r => r.content === 'Đổi màu chữ sang xanh navy' && r.deliverableId === deliverableId)).toBe(true)

    await page.goto(`/admin/projects/${projectId}`)
    const nextStep = page.getByRole('region', { name: 'Bước tiếp theo' })
    await nextStep.getByRole('link', { name: 'Xử lý yêu cầu mới của khách' }).click()
    await expect(page).toHaveURL(/\/admin\/requests\/[0-9a-f-]{36}$/)
  })
})
