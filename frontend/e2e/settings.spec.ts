import { test, expect } from '@playwright/test'
import { expectToast } from './helpers'
import { readSettings, restoreSettings, type SettingsSnapshot } from './settingsHelpers'

test.describe('Settings', () => {
  let saved: SettingsSnapshot

  test.beforeEach(async ({ page }) => {
    await page.goto('/admin/settings')
    await expect(page.getByRole('heading', { name: 'Cài đặt', level: 1 })).toBeVisible()
    saved = await readSettings(page)
  })

  // e2e runs against the designer's dev DB: put the studio name, bank account and terms back (QA D-26)
  test.afterEach(async ({ page }) => {
    await restoreSettings(page, saved)
  })

  test('settings page loads with labelled form fields', async ({ page }) => {
    await expect(page.getByLabel('Tên hiển thị')).toBeVisible()
    await expect(page.getByLabel('Số tài khoản')).toBeVisible()
    await expect(page.getByLabel('Mật khẩu hiện tại')).toBeVisible()
  })

  test('can update display name with bank fields left empty', async ({ page }) => {
    await page.getByLabel('Số tài khoản').fill('')
    const field = page.getByLabel('Tên hiển thị')
    await field.fill(`CamCam Design Studio ${Date.now()}`)
    await page.getByRole('button', { name: 'Lưu thay đổi' }).click()
    await expectToast(page, 'Đã lưu cài đặt')
  })

  test('shows a field error for an invalid bank account number', async ({ page }) => {
    await page.getByLabel('Số tài khoản').fill('12')
    await page.getByRole('button', { name: 'Lưu thay đổi' }).click()
    await expect(page.getByText('4–19 ký tự chữ hoặc số')).toBeVisible()
  })

  test('wrong current password shows a field error', async ({ page }) => {
    await page.getByLabel('Mật khẩu hiện tại').fill('definitely-wrong-password')
    await page.getByLabel('Mật khẩu mới').fill('newtestpassword123')
    await page.getByLabel('Xác nhận mật khẩu').fill('newtestpassword123')
    await page.getByRole('button', { name: 'Đổi mật khẩu' }).click()
    await expect(page.getByText('Mật khẩu hiện tại không đúng')).toBeVisible()
  })
})

test.describe('Services', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/admin/services')
    await expect(page.getByRole('heading', { name: 'Dịch vụ', level: 1 })).toBeVisible()
  })

  test('services page lists seeded services', async ({ page }) => {
    await expect(page.getByRole('button', { name: 'Thêm dịch vụ' }).first()).toBeVisible()
    await expect(page.locator('tbody tr').first()).toBeVisible()
  })

  test('can create a new service', async ({ page }) => {
    const name = `Logo Design E2E ${Date.now()}`
    await page.getByRole('button', { name: 'Thêm dịch vụ' }).first().click()
    await page.getByLabel('Tên dịch vụ').fill(name)
    await page.getByLabel('Giá gốc').fill('2000000')
    await page.getByLabel('Số vòng sửa miễn phí').fill('3')
    await page.getByLabel('Giá vòng thêm').fill('200000')
    await page.getByRole('button', { name: 'Lưu', exact: true }).click()

    await expectToast(page, 'Đã tạo dịch vụ')
    const row = page.locator('tbody tr').filter({ hasText: name })
    await expect(row).toContainText('2.000.000')
    await expect(row).toContainText('3')
  })

  test('can hide and restore a service', async ({ page }) => {
    const name = `Hide E2E ${Date.now()}`
    await page.getByRole('button', { name: 'Thêm dịch vụ' }).first().click()
    await page.getByLabel('Tên dịch vụ').fill(name)
    await page.getByRole('button', { name: 'Lưu', exact: true }).click()
    await expectToast(page, 'Đã tạo dịch vụ')

    await page.getByRole('button', { name: `Ẩn ${name}` }).click()
    await expectToast(page, 'Đã ẩn dịch vụ')
    await expect(page.locator('tbody tr').filter({ hasText: name })).toHaveCount(0)

    await page.getByLabel(/Hiện dịch vụ đã ẩn/).check()
    await page.getByRole('button', { name: `Hiện ${name}` }).click()
    await expectToast(page, 'Đã hiện lại dịch vụ')
  })
})
