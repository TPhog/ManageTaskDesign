import { test, expect } from '@playwright/test'
import { createProject, createTask, expectToast } from './helpers'
import { issuePortalLink, openAsClient } from './portalHelpers'
import { quotedProject } from './flowHelpers'

// Plan 14 (BR-139..145). Money input, brief Markdown and the revision-line prefill are covered by Vitest
// (moneyInput.test, briefTab.test); the glossary by glossary.test.
test.describe('Designer UX round 2 (plan 14)', () => {
  test('ready-to-start, shared due date, open-task warning and a delivery link the client sees', async ({ page, browser }) => {
    test.setTimeout(120_000)
    const stamp = Date.now()
    const projectId = await quotedProject(page, stamp)
    const detail = await (await page.request.get(`/api/admin/projects/${projectId}`)).json()
    await page.goto(`/admin/projects/${projectId}`)

    await expect(page.getByText('Sẵn sàng bắt đầu').first()).toBeVisible()   // AC-UX14-06
    const nextStep = page.getByRole('region', { name: 'Bước tiếp theo' })
    await nextStep.getByRole('button', { name: 'Bắt đầu dự án' }).click()
    const start = page.getByRole('dialog', { name: 'Bắt đầu dự án' })
    await start.getByLabel(/Hạn chung · Logo E2E/).fill('01/12/2030')          // AC-UX14-10
    await start.getByRole('button', { name: /^Bắt đầu dự án · tạo 3 việc/ }).click()
    await expectToast(page, 'Đã bắt đầu dự án')
    const tasks = (await (await page.request.get(`/api/admin/tasks?projectId=${projectId}&pageSize=100`)).json())
      .items as { dueDate: string | null }[]
    expect(tasks).toHaveLength(3)
    expect(tasks.every(t => t.dueDate === '2030-12-01')).toBe(true)
    await expect(nextStep.getByRole('link', { name: 'Xem công việc' })).toBeVisible()   // VIEW_TASKS
    await expect(nextStep.getByRole('button', { name: 'Đánh dấu đã nhận cọc' })).toBeVisible()   // AC-UX14-08

    // open tasks do not block "Gửi duyệt" - the dialog names them (AC-UX14-01)
    await page.goto(`/admin/projects/${projectId}?tab=deliverables`)
    await page.getByRole('tabpanel').getByRole('button', { name: 'Gửi duyệt Logo E2E' }).click()
    const submit = page.getByRole('dialog', { name: 'Gửi hạng mục cho khách duyệt?' })
    await expect(submit).toContainText(/Còn 3 công việc chưa xong/)
    await submit.getByRole('button', { name: 'Vẫn gửi duyệt' }).click()
    await expectToast(page, 'Đã gửi khách duyệt')

    await page.getByRole('tabpanel').getByRole('button', { name: 'Đổi trạng thái Logo E2E' }).click()
    await page.getByRole('menuitem', { name: /Ghi nhận khách đã duyệt/ }).click()
    await page.getByRole('dialog', { name: 'Xác nhận hạng mục đã được duyệt?' }).getByRole('button', { name: 'Xác nhận đã duyệt' }).click()
    await expectToast(page, 'Đã xác nhận khách duyệt')

    await page.getByRole('tabpanel').getByRole('button', { name: 'Bàn giao Logo E2E' }).click()
    const deliver = page.getByRole('dialog', { name: 'Bàn giao hạng mục?' })
    await deliver.getByLabel('Link file bàn giao (tuỳ chọn)').fill('javascript:alert(1)')
    await deliver.getByRole('button', { name: 'Bàn giao', exact: true }).click()
    await expect(deliver.getByText('Link phải bắt đầu bằng http:// hoặc https://')).toBeVisible()   // AC-UX14-03
    const link = `https://drive.google.com/drive/folders/e2e-${stamp}`
    await deliver.getByLabel('Link file bàn giao (tuỳ chọn)').fill(link)
    await deliver.getByLabel('Lời nhắn cho khách (tuỳ chọn)').fill(`Bản final đây ạ ${stamp}`)
    await deliver.getByRole('button', { name: 'Bàn giao', exact: true }).click()
    await expectToast(page, 'Đã cập nhật trạng thái')

    // AC-UX14-02: the message and the link reach the client's timeline, as a safe link
    const clientId = detail.clientId as string
    const { context, page: portal } = await openAsClient(browser, await issuePortalLink(page, clientId))
    await expect(portal).toHaveURL(/\/portal$/)
    await portal.goto(`/portal/projects/${projectId}`)
    const activity = portal.getByRole('region', { name: 'Hoạt động gần đây' })
    await expect(activity).toContainText(`Bản final đây ạ ${stamp}`)
    const anchor = activity.getByRole('link', { name: link })
    await expect(anchor).toHaveAttribute('href', link)
    await expect(anchor).toHaveAttribute('rel', /noopener/)
    await context.close()
  })

  test('kanban fits 1440px with no Medium badge, admin pages do not overflow at 390px, quick search finds tasks', async ({ page }) => {
    const stamp = Date.now()
    await page.goto('/admin')
    const projectId = await createProject(page, `E2E UX14 ${stamp}`)
    await createTask(page, projectId, { title: `Phác thảo sticker ${stamp}` })

    await page.setViewportSize({ width: 1440, height: 900 })
    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    const blocked = page.getByRole('region', { name: 'Cột Bị chặn' })
    const box = await blocked.boundingBox()
    expect(box!.x + box!.width).toBeLessThanOrEqual(1440)                       // AC-UX14-16
    await expect(page.getByText('Trung bình')).toHaveCount(0)

    await page.setViewportSize({ width: 390, height: 844 })
    for (const path of ['/admin', '/admin/tasks', '/admin/services', `/admin/projects/${projectId}`]) {   // AC-UX14-14
      await page.goto(path)
      await expect(page.locator('main')).toBeVisible()
      expect(await page.evaluate(() => document.documentElement.scrollWidth), path).toBeLessThanOrEqual(390)
    }

    await page.setViewportSize({ width: 1440, height: 900 })
    await page.goto('/admin')
    await expect(page.locator('aside').getByRole('button', { name: /Tìm kiếm nhanh/ })).toBeVisible()
    await page.keyboard.press('Control+k')
    await page.getByPlaceholder('Tìm khách, dự án, báo giá, công việc, yêu cầu…').fill(`phac thao sticker ${stamp}`)
    await expect(page.getByRole('option', { name: new RegExp(`Phác thảo sticker ${stamp}`) })).toBeVisible()   // AC-UX14-15
  })
})
