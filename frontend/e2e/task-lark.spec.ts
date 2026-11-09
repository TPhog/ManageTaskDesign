import { test, expect, type Page } from '@playwright/test'
import { apiPost, createClient, createProjectForClient, createTask, csrfToken, expectToast, selectOption } from './helpers'
import { apiPut, issuePortalLink, openAsClient } from './portalHelpers'
import { archiveClient } from './cleanup'

// P15 Lark-style task upgrades (AC-TSK2): recurrence, blocked-by, links, calendar, saved views.

const vnToday = () => new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10)
const add = (iso: string, n: number) => new Date(Date.parse(`${iso}T00:00:00Z`) + n * 86_400_000).toISOString().slice(0, 10)

const DEFAULT_VIEW = { mode: 'list', groupBy: 'none', due: null, status: null, clientId: null, clientLabel: null, projectId: null, projectLabel: null, q: null, calendarUnit: 'month' }

const clientIds: string[] = []

async function seedProject(page: Page, label: string) {
  await page.goto('/admin')
  const stamp = Date.now()
  const clientId = await createClient(page, `E2E Lark ${label} ${stamp}`)
  clientIds.push(clientId)
  const projectId = await createProjectForClient(page, clientId, `E2E Lark ${label} ${stamp}`)
  return { clientId, projectId, stamp }
}

async function dragTo(page: Page, chip: string, day: string) {
  const src = page.locator(chip).first()
  await src.evaluate(el => el.scrollIntoView({ block: 'center' }))
  const from = (await src.boundingBox())!
  const to = (await page.locator(`[data-day="${day}"]`).first().boundingBox())!
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
  await page.mouse.down()
  await page.mouse.move(from.x + from.width / 2 + 10, from.y + from.height / 2 + 10, { steps: 5 })
  await page.mouse.move(to.x + to.width / 2, to.y + to.height - 10, { steps: 15 })
  await page.mouse.up()
}

async function csrfHeaders(page: Page) {
  return { 'X-CSRF-Token': await csrfToken(page), Origin: new URL(page.url()).origin }
}

test.describe('Lark-style task upgrades', () => {
  test.afterAll(async ({ browser }) => {
    const ctx = await browser.newContext({ storageState: 'e2e/.auth/designer.json' })
    const page = await ctx.newPage()
    await page.goto('/admin')
    for (const id of clientIds.splice(0)) await archiveClient(page, id)
    await ctx.close()
  })

  test('recurring task: marking Done creates the next occurrence (AC-TSK2-1)', async ({ page }) => {
    const { projectId, stamp } = await seedProject(page, 'recur')
    const title = `Báo cáo tháng ${stamp}`
    const taskId = await createTask(page, projectId, { title, dueDate: vnToday() })

    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    await page.getByRole('button', { name: title, exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Chi tiết công việc' })
    await drawer.getByRole('combobox', { name: 'Lặp lại' }).click()
    await page.getByRole('option', { name: 'Hằng ngày' }).click()
    await drawer.getByRole('combobox', { name: 'Trạng thái' }).click()
    await page.getByRole('option', { name: 'Xong' }).click()
    await drawer.getByRole('button', { name: 'Lưu thay đổi' }).click()
    await expectToast(page, /Đã tạo công việc kế tiếp/)
    await page.keyboard.press('Escape')

    const list = await (await page.request.get(`/api/admin/tasks?projectId=${projectId}&pageSize=50`)).json()
    const items = (list.items ?? list) as { id: string; title: string; status: string; dueDate: string }[]
    const same = items.filter(t => t.title === title)
    expect(same).toHaveLength(2)
    const next = same.find(t => t.id !== taskId)!
    expect(next.status).toBe('TODO')
    expect(next.dueDate).toBe(add(vnToday(), 1))
  })

  test('blocked task keeps reason + blocker and clears both when unblocked (AC-TSK2-5)', async ({ page }) => {
    const { projectId, stamp } = await seedProject(page, 'blocked')
    const blocker = `Chờ brief ${stamp}`
    const title = `Phác thảo ${stamp}`
    const blockerId = await createTask(page, projectId, { title: blocker })
    const taskId = await createTask(page, projectId, { title })

    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    await page.getByRole('button', { name: title, exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Chi tiết công việc' })
    await drawer.getByRole('combobox', { name: 'Trạng thái' }).click()
    await page.getByRole('option', { name: 'Bị chặn' }).click()
    await drawer.getByLabel('Lý do bị chặn').fill('Chờ khách gửi logo')
    await drawer.getByRole('combobox', { name: 'Bị chặn bởi công việc' }).click()
    await page.getByRole('option', { name: blocker }).click()
    await drawer.getByRole('button', { name: 'Lưu thay đổi' }).click()
    await expectToast(page, 'Đã lưu công việc')
    let t = await (await page.request.get(`/api/admin/tasks/${taskId}`)).json()
    expect(t.blocked.reason).toBe('Chờ khách gửi logo')
    expect(t.blocked.byTaskId).toBe(blockerId)

    // leaving BLOCKED clears reason and blocker (BR-147)
    await drawer.getByRole('combobox', { name: 'Trạng thái' }).click()
    await page.getByRole('option', { name: 'Đang làm' }).click()
    await drawer.getByRole('button', { name: 'Lưu thay đổi' }).click()
    await expectToast(page, 'Đã lưu công việc')
    t = await (await page.request.get(`/api/admin/tasks/${taskId}`)).json()
    expect(t.status).toBe('IN_PROGRESS')
    expect(t.blocked ?? null).toBeNull()
  })

  test('calendar: dragging a task to another day changes its due date (AC-TSK2-11)', async ({ page }) => {
    const { projectId, stamp } = await seedProject(page, 'cal')
    const today = vnToday()
    const taskId = await createTask(page, projectId, { title: `Lịch ${stamp}`, dueDate: today })
    const target = add(today, 3)

    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    await page.getByRole('button', { name: 'Lịch', exact: true }).click()
    await expect(page.getByRole('group', { name: 'Kiểu lịch' })).toBeVisible()
    await expect(page.getByTestId(`calendar-task-${taskId}`).first()).toBeVisible()
    await dragTo(page, `[data-testid="calendar-task-${taskId}"]`, target)
    await expectToast(page, 'Đã đổi hạn')
    const detail = await (await page.request.get(`/api/admin/tasks/${taskId}`)).json()
    expect(detail.dueDate).toBe(target)
  })

  test.describe('on a touch phone', () => {
    test.use({ hasTouch: true, isMobile: true, viewport: { width: 390, height: 844 } })

    test('calendar: a long press picks the chip up, a quick swipe does not (AC-TSK2-11)', async ({ page }) => {
      const { projectId, stamp } = await seedProject(page, 'touch')
      const today = vnToday()
      const taskId = await createTask(page, projectId, { title: `Chạm ${stamp}`, dueDate: today })
      // stay inside today's Monday-first week
      const target = (new Date(`${today}T00:00:00Z`).getUTCDay() + 6) % 7 <= 4 ? add(today, 2) : add(today, -2)

      await page.goto(`/admin/projects/${projectId}?tab=tasks`)
      await page.getByRole('button', { name: 'Lịch', exact: true }).click()
      await page.getByRole('button', { name: 'Tuần', exact: true }).click()
      const chip = page.getByTestId(`calendar-task-${taskId}`)
      const cdp = await page.context().newCDPSession(page)
      const touch = (type: 'touchStart' | 'touchMove' | 'touchEnd', x = 0, y = 0) =>
        cdp.send('Input.dispatchTouchEvent', { type, touchPoints: type === 'touchEnd' ? [] : [{ x, y }] })
      async function gesture(holdMs: number) {
        await chip.evaluate(el => el.scrollIntoView({ block: 'center' }))
        const a = (await chip.boundingBox())!
        const b = (await page.locator(`[data-day="${target}"]`).first().boundingBox())!
        const [x0, y0, x1, y1] = [a.x + a.width / 2, a.y + a.height / 2, b.x + b.width / 2, b.y + b.height / 2]
        await touch('touchStart', x0, y0)
        await page.waitForTimeout(holdMs)
        for (let i = 1; i <= 12; i++) await touch('touchMove', x0 + ((x1 - x0) * i) / 12, y0 + ((y1 - y0) * i) / 12)
        await touch('touchEnd')
      }

      await gesture(0)
      await page.waitForTimeout(800)
      expect((await (await page.request.get(`/api/admin/tasks/${taskId}`)).json()).dueDate).toBe(today)

      await gesture(400) // TouchSensor delay is 250 ms
      await expectToast(page, 'Đã đổi hạn')
      expect((await (await page.request.get(`/api/admin/tasks/${taskId}`)).json()).dueDate).toBe(target)
    })
  })

  test('task link: javascript: is rejected, https is saved with safe attributes (AC-TSK2-7)', async ({ page }) => {
    const { projectId, stamp } = await seedProject(page, 'link')
    const title = `Link ${stamp}`
    await createTask(page, projectId, { title })

    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    await page.getByRole('button', { name: title, exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Chi tiết công việc' })
    await drawer.getByLabel('Địa chỉ liên kết').fill('javascript:alert(1)')
    await drawer.getByRole('button', { name: 'Thêm liên kết' }).click()
    await expect(drawer.getByText('Chỉ chấp nhận địa chỉ http hoặc https')).toBeVisible()
    await drawer.getByLabel('Tên liên kết').fill('Figma')
    await drawer.getByLabel('Địa chỉ liên kết').fill('https://www.figma.com/file/e2e')
    await drawer.getByRole('button', { name: 'Thêm liên kết' }).click()
    await drawer.getByRole('button', { name: 'Lưu thay đổi' }).click()
    await expectToast(page, 'Đã lưu công việc')
    const chip = drawer.getByRole('link', { name: /Figma/ })
    await expect(chip).toHaveAttribute('rel', 'noopener noreferrer')
    await expect(chip).toHaveAttribute('target', '_blank')
  })

  test('deliverable links: only the flagged one reaches the portal (AC-TSK2-8)', async ({ page, browser }) => {
    const { clientId, projectId } = await seedProject(page, 'dlink')
    await apiPost(page.request, page, `/api/admin/projects/${projectId}/deliverables`,
      { name: 'Logo chính', quantity: 1, includedRounds: 2, extraRoundPrice: 0 })

    await page.goto(`/admin/projects/${projectId}?tab=deliverables`)
    await page.getByRole('button', { name: 'Sửa Logo chính' }).click()
    const dlg = page.getByRole('dialog', { name: 'Sửa hạng mục' })
    for (const [name, url] of [['Figma Logo', 'https://figma.com/file/logo'], ['Ghi chú nội bộ', 'https://drive.google.com/drive/folders/noi-bo']]) {
      await dlg.getByLabel('Tên liên kết').fill(name)
      await dlg.getByLabel('Địa chỉ liên kết').fill(url)
      await dlg.getByRole('button', { name: 'Thêm liên kết' }).click()
    }
    await dlg.getByLabel('Hiện cho khách: Figma Logo').check()
    await dlg.getByRole('button', { name: 'Lưu liên kết' }).click()
    await expectToast(page, 'Đã lưu liên kết')

    const { context, page: portal } = await openAsClient(browser, await issuePortalLink(page, clientId))
    await expect(portal).toHaveURL(/\/portal$/)
    await portal.goto(`/portal/projects/${projectId}`)
    const item = portal.getByRole('region', { name: 'Sản phẩm' }).getByRole('listitem').filter({ hasText: 'Logo chính' })
    const link = item.getByRole('link', { name: /Figma Logo/ })
    await expect(link).toHaveAttribute('rel', 'noopener noreferrer')
    await expect(item.getByText('Ghi chú nội bộ')).toHaveCount(0)
    await context.close()
  })

  test('saved view: save, 409 on duplicate name, apply; last view restored (AC-TSK2-13/14)', async ({ page }) => {
    await page.goto('/admin/tasks?groupBy=project')
    await expect(page.getByRole('button', { name: /^Chế độ xem/ })).toBeVisible()
    const name = `Theo dự án ${Date.now()}`
    try {
      await page.getByRole('button', { name: /^Chế độ xem/ }).click()
      await page.getByRole('button', { name: 'Lưu chế độ xem hiện tại…' }).click()
      const dlg = page.getByRole('dialog', { name: 'Lưu chế độ xem' })
      await dlg.getByLabel('Tên chế độ xem').fill(name)
      await dlg.getByRole('button', { name: 'Lưu' }).click()
      await expectToast(page, 'Đã lưu chế độ xem')

      // same name in a different case → 409
      const res = await page.request.post('/api/admin/task-views', {
        data: { name: name.toUpperCase(), config: DEFAULT_VIEW },
        headers: await csrfHeaders(page),
      })
      expect(res.status()).toBe(409)

      // apply the saved view after switching grouping
      await selectOption(page, 'Nhóm theo', 'Nhóm theo ưu tiên')
      await expect(page).toHaveURL(/groupBy=priority/)
      await page.getByRole('button', { name: /^Chế độ xem/ }).click()
      await page.getByRole('button', { name, exact: true }).click()
      await expect(page).toHaveURL(/groupBy=project/)

      // last view re-applied on a bare /admin/tasks
      await page.goto('/admin/tasks?groupBy=status&due=week')
      await page.waitForTimeout(1500)
      await page.goto('/admin/tasks')
      await expect(page).toHaveURL(/groupBy=status/)
      await expect(page).toHaveURL(/due=week/)
    } finally {
      const views = await (await page.request.get('/api/admin/task-views')).json()
      const mine = (views.views as { id: string; name: string }[]).find(v => v.name === name)
      if (mine) await page.request.delete(`/api/admin/task-views/${mine.id}`, { headers: await csrfHeaders(page) })
      await apiPut(page, '/api/admin/task-views/last', { config: DEFAULT_VIEW })
    }
  })
})
