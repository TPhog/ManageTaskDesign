import { test, expect, type Page } from '@playwright/test'
import { apiPost, createClient, createProject, createProjectForClient } from './helpers'
import { quotedProject } from './flowHelpers'

/** Same formatting as lib/format.ts formatVnd */
const vnd = (n: number) =>
  new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', minimumFractionDigits: 0, maximumFractionDigits: 0 }).format(n)

/** The project's row in "Việc cần làm" (expands the list first: the dev DB holds many open projects). */
async function flowRow(page: Page, projectId: string) {
  const block = page.getByRole('region', { name: 'Việc cần làm' })
  await expect(block.getByTestId('dsh-flow')).toBeVisible()
  const more = block.getByRole('button', { name: /^Hiện thêm/ })
  if (await more.isVisible()) await more.click()
  return block.getByTestId(`dsh-flow-${projectId}`)
}

test.describe('Dashboard', () => {
  test('shows the server numbers once and links a counter to its filtered list', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page, `E2E Dashboard ${Date.now()}`)
    await apiPost(page.request, page, `/api/admin/projects/${projectId}/requests`,
      { content: 'Yêu cầu E2E cho dashboard', source: 'ZALO' })
    const dsh = await (await page.request.get('/api/admin/dashboard')).json()
    expect(dsh.actionRequired.newRequests).toBeGreaterThan(0)

    await page.reload()
    await expect(page.getByRole('heading', { name: 'Tổng quan', level: 1 })).toBeVisible()
    const counters = page.getByRole('list', { name: 'Cần xử lý' })
    await expect(counters.getByTestId('dsh-newRequests')).toHaveText(String(dsh.actionRequired.newRequests))
    await expect(counters.getByTestId('dsh-overdueTasks')).toHaveText(String(dsh.actionRequired.overdueTasks))
    await expect(counters.getByTestId('dsh-awaitingQuotations')).toHaveText(String(dsh.actionRequired.awaitingQuotations))
    await expect(page.getByTestId('dsh-newRequests')).toHaveCount(1)   // each DSH-01 counter appears once
    await expect(page.getByTestId('dsh-inProgress')).toHaveText(String(dsh.projects.inProgress))
    await expect(page.getByTestId('dsh-outstanding')).toHaveText(vnd(dsh.money.outstanding))
    await expect(page.getByTestId('dsh-acceptedThisMonth')).toHaveText(vnd(dsh.money.acceptedThisMonth))
    // screen-reader table of the chart: header row + 6 months
    await expect(page.getByTestId('dsh-chart').getByRole('row')).toHaveCount(7)

    await counters.getByTestId('dsh-newRequests').click()
    await expect(page).toHaveURL(/\/admin\/requests$/)
  })

  test('project and quotation counters open lists already filtered by status', async ({ page }) => {
    await page.goto('/admin')
    await page.getByTestId('dsh-inProgress').click()
    await expect(page).toHaveURL(/\/admin\/projects\?status=IN_PROGRESS$/)
    await expect(page.getByRole('combobox', { name: 'Lọc theo trạng thái' })).toHaveText('Đang làm')

    await page.goto('/admin')
    await page.getByTestId('dsh-awaitingQuotations').click()
    await expect(page).toHaveURL(/\/admin\/quotations\?status=SENT$/)
    await expect(page.getByRole('combobox').first()).toHaveText('Đã gửi')
  })

  test('"Việc cần làm" shows a new project with its first step first and its warning as a chip', async ({ page }) => {
    await page.goto('/admin')
    const stamp = Date.now()
    const clientId = await createClient(page, `Khách việc cần làm ${stamp}`)
    const projectId = await createProjectForClient(page, clientId, `Dự án việc cần làm ${stamp}`)

    await page.reload()
    const row = await flowRow(page, projectId)
    await expect(row).toContainText(`Dự án việc cần làm ${stamp}`)
    await expect(row).toContainText('Chuẩn bị')
    await expect(row.getByRole('list', { name: 'Cảnh báo' })).toContainText('Brief chưa được xác nhận')
    const actions = row.getByRole('link', { name: /^(Thêm hạng mục|Xác nhận brief|Tạo báo giá)$/ })
    await expect(actions.first()).toHaveText('Thêm hạng mục')   // primary first

    await actions.first().click()
    await expect(page).toHaveURL(new RegExp(`/admin/projects/${projectId}\\?tab=deliverables$`))
  })

  test('a dialog step on the dashboard opens the same dialog on the project page', async ({ page }) => {
    const projectId = await quotedProject(page, Date.now())

    await page.goto('/admin')
    const row = await flowRow(page, projectId)
    await expect(row.getByRole('list', { name: 'Cảnh báo' })).toContainText('Chưa nhận cọc')
    await row.getByRole('link', { name: 'Bắt đầu dự án' }).click()

    await expect(page.getByRole('dialog', { name: 'Bắt đầu dự án' })).toBeVisible()
    await expect(page).toHaveURL(new RegExp(`/admin/projects/${projectId}$`))
  })
})
