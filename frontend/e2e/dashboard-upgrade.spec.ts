import { test, expect } from '@playwright/test'
import { apiPost, createProject, createTask } from './helpers'
import { quotedProject } from './flowHelpers'

const isoDay = (offsetDays: number) => new Date(Date.now() + offsetDays * 24 * 3600 * 1000).toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' })

/** The overdue group shows the oldest tasks first and is capped, so e2e tasks use a date older than any real one. */
const OLDEST = '1990-01-01'

test.describe('Dashboard upgrade (P11)', () => {
  /** Tasks a test created: marked DONE afterwards so the capped overdue group never fills up on the shared dev DB. */
  const created: string[] = []
  test.afterEach(async ({ page }) => {
    for (const id of created.splice(0)) {
      await apiPost(page.request, page, `/api/admin/tasks/${id}/move`, { status: 'DONE', sortOrder: 1 })
    }
  })

  test('ticks an overdue task done from the 7-day schedule, undo puts it back', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page, `E2E Lịch ${Date.now()}`)
    const title = `Việc quá hạn ${Date.now()}`
    created.push(await createTask(page, projectId, { title, dueDate: OLDEST }))
    await page.reload()

    const schedule = page.getByRole('region', { name: 'Lịch làm việc 7 ngày' })
    const overdue = schedule.getByRole('region', { name: 'Quá hạn' })
    await overdue.getByRole('checkbox', { name: `Đánh dấu xong: ${title}` }).click()
    await expect(overdue.getByText(title)).toHaveCount(0)
    await expect(page.getByText(`Đã hoàn thành "${title}"`)).toBeVisible()
    await page.getByRole('button', { name: 'Hoàn tác' }).click()
    await expect(overdue.getByText(title)).toBeVisible()
  })

  test('opens the task drawer from a schedule row', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page, `E2E Lịch drawer ${Date.now()}`)
    const title = `Việc mở drawer ${Date.now()}`
    created.push(await createTask(page, projectId, { title, dueDate: OLDEST }))
    await page.reload()

    const schedule = page.getByRole('region', { name: 'Lịch làm việc 7 ngày' })
    await schedule.getByRole('button', { name: title }).click()
    await expect(page.getByRole('dialog', { name: 'Chi tiết công việc' }).locator('input').first()).toHaveValue(title)
    await page.keyboard.press('Escape')
  })

  test('shows collected money for the period, switches period and copies a payment reminder', async ({ page }) => {
    const projectId = await quotedProject(page, Date.now())
    const list = await (await page.request.get(`/api/admin/quotations?projectId=${projectId}&pageSize=100`)).json()
    const q = await (await page.request.get(`/api/admin/quotations/${list.items[0].id}`)).json()
    await apiPost(page.request, page, `/api/admin/quotations/${q.id}/payment-status`,
      { to: 'DEPOSIT_PAID', date: isoDay(0), rowVersion: q.rowVersion })

    await page.goto('/admin')
    const money = page.getByRole('region', { name: 'Tiền đã thu và công nợ' })
    await expect(money.getByTestId('dsh-collected')).toBeVisible()
    await money.getByTestId('dsh-period-quarter').click()
    await expect(money.getByTestId('dsh-period-quarter')).toHaveAttribute('aria-pressed', 'true')
    await page.context().grantPermissions(['clipboard-read', 'clipboard-write'])
    await money.getByRole('button', { name: /Copy nhắc thanh toán/ }).first().click()
    expect(await page.evaluate(() => navigator.clipboard.readText())).toContain('Mình nhắc')
  })

  test('project health lists at most 12 rows; storage widget links to the storage page', async ({ page }) => {
    await page.goto('/admin')
    const health = await (await page.request.get('/api/admin/dashboard/project-health')).json()
    expect(health.items.length).toBeLessThanOrEqual(12)

    await page.reload()
    const block = page.getByRole('region', { name: 'Sức khỏe dự án' })
    await expect(block).toBeVisible()
    await expect(block.getByRole('list').first()).toBeVisible()

    const storage = page.getByRole('region', { name: 'Dung lượng ảnh' })
    await storage.getByRole('link', { name: 'Quản lý dung lượng' }).click()
    await expect(page).toHaveURL(/\/admin\/settings\/storage$/)
  })

  test('first-run checklist can be dismissed and stays hidden after reload', async ({ page }) => {
    await page.goto('/admin')
    await expect(page.getByRole('heading', { name: 'Tổng quan', level: 1 })).toBeVisible()
    const card = page.getByRole('region', { name: 'Bắt đầu thiết lập' })
    test.skip(!(await card.isVisible()), 'checklist already complete or dismissed')
    await card.getByRole('button', { name: 'Ẩn hướng dẫn' }).click()
    await expect(card).toHaveCount(0)
    await page.reload()
    await expect(page.getByRole('heading', { name: 'Tổng quan', level: 1 })).toBeVisible()
    await expect(page.getByRole('region', { name: 'Bắt đầu thiết lập' })).toHaveCount(0)
  })

  test('digest settings save and survive reload', async ({ page }) => {
    await page.goto('/admin/settings')
    const section = page.getByRole('region', { name: 'Nhắc việc hằng ngày' })
    const hour = section.getByLabel('Giờ gửi (giờ Việt Nam)')
    await hour.selectOption('7')
    await section.getByRole('button', { name: 'Lưu' }).click()
    await page.reload()
    await expect(hour).toHaveValue('7')
    // restore the default so other specs are unaffected
    await hour.selectOption('8')
    await section.getByRole('button', { name: 'Lưu' }).click()
    await expect(page.getByText('Đã lưu nhắc việc hằng ngày').last()).toBeVisible()
  })

  test('dashboard does not overflow horizontally at 390px', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto('/admin')
    await expect(page.getByRole('heading', { name: 'Tổng quan', level: 1 })).toBeVisible()
    await expect(page.getByRole('region', { name: 'Lịch làm việc 7 ngày' })).toBeVisible()
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
  })
})
