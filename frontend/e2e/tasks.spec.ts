import { test, expect, type Locator, type Page } from '@playwright/test'
import { createProject, createTask, expectToast, selectOption } from './helpers'

/** Today in Vietnam as YYYY-MM-DD (the app compares due dates with today(VN), BR-81). */
function vnToday(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date())
}

function addDaysIso(isoDate: string, days: number): string {
  const d = new Date(`${isoDate}T00:00:00Z`)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

/** Mouse drag in small steps so dnd-kit's PointerSensor (5px threshold) activates. */
async function dragTo(page: Page, source: Locator, target: Locator) {
  const from = await source.boundingBox()
  const to = await target.boundingBox()
  if (!from || !to) throw new Error('drag source or target not visible')
  const startX = from.x + from.width / 2
  const startY = from.y + from.height / 2
  await page.mouse.move(startX, startY)
  await page.mouse.down()
  await page.mouse.move(startX + 12, startY + 4, { steps: 4 })
  await page.mouse.move(to.x + to.width / 2, to.y + Math.min(to.height / 2, 140), { steps: 15 })
  await page.mouse.up()
}

const column = (page: Page, label: string) => page.getByRole('region', { name: `Cột ${label}` })

async function quickCreate(page: Page, columnLabel: string, title: string) {
  const col = column(page, columnLabel)
  await col.getByRole('button', { name: `Thêm vào ${columnLabel}` }).click()
  const input = col.getByRole('textbox', { name: `Thêm vào ${columnLabel}` })
  await input.fill(title)
  await input.press('Enter')
  await expect(col.getByRole('button', { name: title, exact: true })).toBeVisible()
}

test.describe('Project tasks tab', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
  })

  test('quick-creates a task, drags it to another column and ticks its checklist', async ({ page }) => {
    const title = `Phác thảo logo E2E ${Date.now()}`
    await quickCreate(page, 'Cần làm', title)

    await dragTo(page, column(page, 'Cần làm').getByRole('button', { name: title, exact: true }), column(page, 'Đang làm'))
    await expect(column(page, 'Đang làm').getByRole('button', { name: title, exact: true })).toBeVisible()

    await page.reload()
    const card = column(page, 'Đang làm').getByRole('button', { name: title, exact: true })
    await expect(card).toBeVisible()

    await card.click()
    const drawer = page.getByRole('dialog', { name: 'Chi tiết công việc' })
    await drawer.getByRole('textbox', { name: 'Thêm mục checklist' }).fill('Chọn màu chủ đạo')
    await drawer.getByRole('button', { name: 'Thêm', exact: true }).click()
    const item = drawer.getByRole('checkbox', { name: 'Chọn màu chủ đạo' })
    await expect(item).not.toBeChecked()
    await item.click()
    await expect(item).toBeChecked()
    await expect(drawer.getByText('1/1', { exact: true })).toBeVisible()

    await page.keyboard.press('Escape')
    await expect(drawer).toBeHidden()
    await expect(column(page, 'Đang làm')).toContainText('1/1')
  })

  test('moves a card with the keyboard-friendly card menu', async ({ page }) => {
    const title = `Gửi bản nháp E2E ${Date.now()}`
    await quickCreate(page, 'Cần làm', title)

    await column(page, 'Cần làm').getByRole('button', { name: `Tuỳ chọn ${title}` }).click()
    await page.getByRole('menuitem', { name: 'Chuyển sang Bị chặn' }).click()

    await expect(column(page, 'Bị chặn').getByRole('button', { name: title, exact: true })).toBeVisible()
    await expect(column(page, 'Cần làm').getByRole('button', { name: title, exact: true })).toHaveCount(0)
  })

  test('edits a task in the drawer and shows it in the list view', async ({ page }) => {
    const title = `Viết brief E2E ${Date.now()}`
    await quickCreate(page, 'Cần làm', title)

    await column(page, 'Cần làm').getByRole('button', { name: title, exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Chi tiết công việc' })
    await drawer.getByLabel('Tiêu đề').fill(`${title} chi tiết`)
    await selectOption(page, /^Trạng thái$/, 'Chờ duyệt')
    await selectOption(page, /^Ưu tiên$/, 'Khẩn cấp')
    await drawer.getByLabel('Hạn hoàn thành').fill(vnToday())
    await drawer.getByRole('button', { name: 'Lưu thay đổi' }).click()
    await expectToast(page, 'Đã lưu công việc')

    await page.keyboard.press('Escape')
    await page.getByRole('button', { name: 'Danh sách', exact: true }).click()
    const row = page.getByRole('row', { name: new RegExp(`${title} chi tiết`) })
    await expect(row).toContainText('Chờ duyệt')
    await expect(row).toContainText('Khẩn cấp')
    await expect(row).toContainText('Hôm nay')
  })
})

test.describe('All tasks page', () => {
  test('sidebar opens the tasks page', async ({ page }) => {
    await page.goto('/admin')
    await page.getByRole('navigation', { name: 'Điều hướng chính' }).getByRole('link', { name: 'Công việc' }).click()
    await expect(page).toHaveURL(/\/admin\/tasks$/)
    await expect(page.getByRole('heading', { name: 'Công việc', level: 1 })).toBeVisible()
  })

  test('shows overdue and due-today tasks under their quick filters', async ({ page }) => {
    await page.goto('/admin')
    const stamp = Date.now()
    const projectName = `E2E Tasks ${stamp}`
    const projectId = await createProject(page, projectName)
    const today = vnToday()
    const dueToday = `Gửi file in E2E ${stamp}`
    const overdue = `Sửa banner E2E ${stamp}`
    await createTask(page, projectId, { title: dueToday, dueDate: today })
    await createTask(page, projectId, { title: overdue, dueDate: addDaysIso(today, -1) })

    await page.goto('/admin/tasks')
    const filters = page.getByRole('group', { name: 'Lọc nhanh' })

    await filters.getByRole('button', { name: 'Hôm nay', exact: true }).click()
    await expect(page).toHaveURL(/due=today/)
    await expect(page.getByRole('row', { name: new RegExp(dueToday) })).toContainText('Hôm nay')
    await expect(page.getByRole('row', { name: new RegExp(overdue) })).toHaveCount(0)

    await filters.getByRole('button', { name: 'Quá hạn', exact: true }).click()
    await expect(page).toHaveURL(/due=overdue/)
    const overdueRow = page.getByRole('row', { name: new RegExp(overdue) })
    await expect(overdueRow).toContainText('Quá hạn')
    await expect(page.getByRole('row', { name: new RegExp(dueToday) })).toHaveCount(0)

    // the title opens the drawer in place
    await overdueRow.getByRole('button', { name: overdue, exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Chi tiết công việc' })
    await expect(drawer.getByLabel('Tiêu đề')).toHaveValue(overdue)
    await page.keyboard.press('Escape')
    await expect(drawer).toBeHidden()

    // the project link goes to the project's task tab
    await overdueRow.getByRole('link', { name: projectName }).click()
    await expect(page).toHaveURL(new RegExp(`/admin/projects/${projectId}\\?tab=tasks`))
    await expect(page.getByRole('region', { name: 'Cột Cần làm' })).toContainText(overdue)
  })
})
