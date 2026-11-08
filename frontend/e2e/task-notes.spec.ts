import { test, expect } from '@playwright/test'
import { createProject, createTask, expectToast } from './helpers'

test.describe('Task notes', () => {
  test('drawer keeps a markdown description, subtask descriptions and timestamped notes', async ({ page }) => {
    await page.goto('/admin')
    const projectId = await createProject(page)
    const title = `Thiết kế logo ${Date.now()}`
    const taskId = await createTask(page, projectId, { title })

    await page.goto(`/admin/projects/${projectId}?tab=tasks`)
    await page.getByRole('region', { name: 'Cột Cần làm' }).getByRole('button', { name: title, exact: true }).click()
    const drawer = page.getByRole('dialog', { name: 'Chi tiết công việc' })

    // Markdown description: safe link opens in a new tab, javascript: link is plain text
    await drawer.getByLabel('Mô tả', { exact: true }).fill(
      'Xem **moodboard** tại [Pinterest](https://pinterest.com/abc)\n\n[Link xấu](javascript:alert(1))',
    )
    await drawer.getByRole('button', { name: 'Lưu thay đổi' }).click()
    await expectToast(page, 'Đã lưu công việc')
    const link = drawer.getByRole('link', { name: 'Pinterest' })
    await expect(link).toHaveAttribute('href', 'https://pinterest.com/abc')
    await expect(link).toHaveAttribute('target', '_blank')
    await expect(drawer.getByText('Link xấu')).toBeVisible()
    await expect(drawer.getByRole('link', { name: 'Link xấu' })).toHaveCount(0)

    // Subtask description
    await drawer.getByRole('textbox', { name: 'Thêm mục checklist' }).fill('Chọn font')
    await drawer.getByRole('button', { name: 'Thêm', exact: true }).click()
    await drawer.getByRole('button', { name: 'Mở mô tả Chọn font' }).click()
    await drawer.getByLabel('Mô tả của Chọn font').fill('Ưu tiên font không chân')
    await drawer.getByRole('button', { name: 'Lưu mô tả' }).click()
    await expectToast(page, 'Đã lưu mô tả')
    await drawer.getByRole('checkbox', { name: 'Chọn font' }).check()
    await drawer.getByRole('button', { name: 'Mở mô tả Chọn font' }).click()
    await expect(drawer.getByText('Ưu tiên font không chân')).toBeVisible()

    // Notes: newest first, edit, delete
    await drawer.getByLabel('Ghi chú mới').fill('Khách thích tông xanh')
    await drawer.getByRole('button', { name: 'Thêm ghi chú' }).click()
    const notes = drawer.getByRole('list', { name: 'Danh sách ghi chú' })
    await expect(notes.getByRole('listitem')).toHaveCount(1)
    await drawer.getByLabel('Ghi chú mới').fill('Gửi bản phác thảo trước thứ 6')
    await drawer.getByRole('button', { name: 'Thêm ghi chú' }).click()
    await expect(notes.getByRole('listitem')).toHaveCount(2)
    await expect(notes.getByRole('listitem').first()).toContainText('Gửi bản phác thảo trước thứ 6')

    await notes.getByRole('listitem').first().getByRole('button', { name: 'Sửa ghi chú' }).click()
    await notes.getByLabel('Nội dung ghi chú').fill('Gửi bản phác thảo trước thứ 5')
    await notes.getByRole('button', { name: 'Lưu', exact: true }).click()
    await expect(notes.getByRole('listitem').first()).toContainText('thứ 5')
    await expect(notes.getByRole('listitem').first()).toContainText('(đã sửa)')

    await notes.getByRole('listitem').last().getByRole('button', { name: 'Xoá ghi chú' }).click()
    await notes.getByRole('button', { name: 'Xác nhận xoá' }).click()
    await expectToast(page, 'Đã xoá ghi chú')
    await expect(notes.getByRole('listitem')).toHaveCount(1)
    await expect(drawer.getByRole('heading', { name: 'Ghi chú (1)' })).toBeVisible()

    // Kanban card shows the description icon and the note count
    await page.keyboard.press('Escape')
    const card = page.getByTestId(`task-card-${taskId}`)
    await expect(card.getByTitle('Có mô tả')).toBeAttached()
    await expect(card.getByTitle('1 ghi chú')).toContainText('1')
    await expect(card).toContainText('1/1')
  })

  test('services keep an ordered task template', async ({ page }) => {
    const name = `Dịch vụ mẫu ${Date.now()}`
    await page.goto('/admin/services')
    await page.getByRole('button', { name: 'Thêm dịch vụ' }).first().click()
    const sheet = page.getByRole('dialog', { name: 'Thêm dịch vụ' })
    await sheet.getByLabel('Tên dịch vụ').fill(name)
    for (const step of ['Phác thảo', 'Hoàn thiện', 'Moodboard']) {
      await sheet.getByLabel('Bước mới').fill(step)
      await sheet.getByLabel('Bước mới').press('Enter')
    }
    await sheet.getByRole('button', { name: 'Đưa bước 3 lên' }).click()
    await sheet.getByRole('button', { name: 'Đưa bước 2 lên' }).click()
    await expect(sheet.getByLabel('Bước 1', { exact: true })).toHaveValue('Moodboard')
    await sheet.getByRole('button', { name: 'Xoá bước 3' }).click()
    await sheet.getByRole('button', { name: 'Lưu', exact: true }).click()
    await expectToast(page, 'Đã tạo dịch vụ')

    const services = await (await page.request.get('/api/admin/services?includeInactive=true')).json()
    expect(services.find((s: { name: string }) => s.name === name).taskTemplate).toEqual(['Moodboard', 'Phác thảo'])

    await page.getByRole('button', { name: `Sửa ${name}` }).click()
    const edit = page.getByRole('dialog', { name: 'Sửa dịch vụ' })
    await expect(edit.getByLabel('Bước 2', { exact: true })).toHaveValue('Phác thảo')
  })
})
