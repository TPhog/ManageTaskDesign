import { test, expect } from '@playwright/test'
import { expectToast } from './helpers'

test.describe('Request templates (Settings)', () => {
  test('lists the presets and the new-client form section', async ({ page }) => {
    await page.goto('/admin/settings')
    await page.getByRole('tab', { name: 'Khách hàng & Portal' }).click()
    await expect(page.getByRole('heading', { name: 'Form nhận yêu cầu khách mới' })).toBeVisible()
    await page.getByRole('link', { name: /Quản lý mẫu yêu cầu/ }).click()
    await expect(page.getByRole('heading', { name: 'Mẫu yêu cầu', level: 1 })).toBeVisible()
    // the back link returns to the tab the list was opened from
    await page.getByRole('main').getByRole('link', { name: 'Cài đặt', exact: true }).click()
    await expect(page).toHaveURL(/\/admin\/settings\?tab=portal$/)
    await expect(page.getByRole('heading', { name: 'Form nhận yêu cầu khách mới' })).toBeVisible()
    await page.getByRole('link', { name: /Quản lý mẫu yêu cầu/ }).click()
    await expect(page.getByRole('heading', { name: 'Mẫu yêu cầu', level: 1 })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Poster', exact: true })).toBeVisible()
  })

  test('creates a template with a question, previews it live, edits and deletes it', async ({ page }) => {
    const name = `E2E mẫu ${Date.now()}`
    await page.goto('/admin/settings/request-templates/new')
    await expect(page.getByRole('heading', { name: 'Mẫu yêu cầu mới', level: 1 })).toBeVisible()

    await page.getByLabel('Tên mẫu').fill(name)
    await page.getByLabel('Tiêu đề câu hỏi 1').fill('Tên cửa hàng')
    await page.getByRole('checkbox', { name: /Bắt buộc/ }).check()
    // live preview: same TemplateFields the client sees, required marker included
    await expect(page.getByRole('textbox', { name: /Tên cửa hàng/ })).toBeVisible()

    await page.getByRole('button', { name: 'Thêm câu hỏi' }).click()
    await page.getByRole('menuitem', { name: 'Chọn 1' }).click()
    await page.getByLabel('Tiêu đề câu hỏi 2').fill('Phong cách')
    await expect(page.getByRole('radio', { name: 'Lựa chọn 1' })).toBeVisible()

    await page.getByRole('button', { name: 'Đưa câu hỏi 2 lên' }).click()
    await expect(page.getByLabel('Tiêu đề câu hỏi 1')).toHaveValue('Phong cách')

    await page.getByRole('button', { name: 'Lưu mẫu' }).click()
    await expectToast(page, 'Đã lưu mẫu')
    await expect(page).toHaveURL(/\/admin\/settings\/request-templates\/[0-9a-f-]{36}$/)
    await expect(page.getByRole('heading', { name: 'Sửa mẫu yêu cầu', level: 1 })).toBeVisible()

    // edit + reload keeps it
    await page.getByLabel('Mô tả ngắn cho khách').fill('Mẫu thử')
    await page.getByRole('button', { name: 'Lưu mẫu' }).click()
    await expectToast(page, 'Đã lưu mẫu')
    await page.reload()
    await expect(page.getByLabel('Mô tả ngắn cho khách')).toHaveValue('Mẫu thử')
    await expect(page.getByLabel('Tiêu đề câu hỏi 1')).toHaveValue('Phong cách')

    // cleanup: the e2e runs against the dev DB
    await page.getByRole('button', { name: 'Xoá mẫu' }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Xoá mẫu' }).click()
    await expect(page).toHaveURL(/\/admin\/settings\/request-templates$/)
    await expect(page.getByRole('link', { name, exact: true })).toHaveCount(0)
  })

  test('refuses a template with no name or a choice question without options', async ({ page }) => {
    await page.goto('/admin/settings/request-templates/new')
    await page.getByRole('button', { name: 'Lưu mẫu' }).click()
    await expect(page.getByText('Nhập tên mẫu')).toBeVisible()
    await page.getByRole('button', { name: 'Thêm câu hỏi' }).click()
    await page.getByRole('menuitem', { name: 'Chọn nhiều' }).click()
    await page.getByLabel('Lựa chọn — mỗi dòng một lựa chọn').fill('')
    await expect(page.getByText('Câu hỏi 2: cần 1–20 lựa chọn')).toBeVisible()
  })
})
