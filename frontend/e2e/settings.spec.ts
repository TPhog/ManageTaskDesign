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

  test('settings page loads with labelled form fields on each tab', async ({ page }) => {
    await expect(page.getByRole('tab', { name: 'Studio' })).toHaveAttribute('aria-selected', 'true')
    await expect(page.getByLabel('Tên hiển thị')).toBeVisible()
    await page.getByRole('tab', { name: 'Thanh toán' }).click()
    await expect(page).toHaveURL(/\?tab=payment$/)
    await expect(page.getByLabel('Số tài khoản')).toBeVisible()
    await page.getByRole('tab', { name: 'Tài khoản' }).click()
    await expect(page.getByLabel('Mật khẩu hiện tại')).toBeVisible()
  })

  test('deep link ?tab=payment opens the bank form', async ({ page }) => {
    await page.goto('/admin/settings?tab=payment')
    await expect(page.getByRole('tab', { name: 'Thanh toán' })).toHaveAttribute('aria-selected', 'true')
    await expect(page.getByLabel('Số tài khoản')).toBeVisible()
    await expect(page.getByLabel('Tên hiển thị')).toHaveCount(0)
  })

  test('can update display name on its own', async ({ page }) => {
    const save = page.getByRole('button', { name: 'Lưu hồ sơ' })
    await expect(save).toBeDisabled()
    const name = `CamCam Design Studio ${Date.now()}`
    await page.getByLabel('Tên hiển thị').fill(name)
    await save.click()
    await expectToast(page, 'Đã lưu hồ sơ')
    const after = await readSettings(page)
    expect(after.displayName).toBe(name)
    expect(after.bankAccountNo).toBe(saved.bankAccountNo)
  })

  test('saving the bank account does not change the display name', async ({ page }) => {
    await page.getByRole('tab', { name: 'Thanh toán' }).click()
    await page.getByLabel('Số tài khoản').fill('0011223344')
    await page.getByRole('button', { name: 'Lưu tài khoản' }).click()
    await expectToast(page, 'Đã lưu tài khoản ngân hàng')
    const after = await readSettings(page)
    expect(after.bankAccountNo).toBe('0011223344')
    expect(after.displayName).toBe(saved.displayName)
    expect(after.defaultTerms).toBe(saved.defaultTerms)
  })

  test('shows a field error for an invalid bank account number', async ({ page }) => {
    await page.getByRole('tab', { name: 'Thanh toán' }).click()
    await page.getByLabel('Số tài khoản').fill('12')
    await page.getByRole('button', { name: 'Lưu tài khoản' }).click()
    await expect(page.getByText('4–19 ký tự chữ hoặc số')).toBeVisible()
  })

  test('asks before switching tab with unsaved changes', async ({ page }) => {
    await page.getByLabel('Tên hiển thị').fill('Chưa lưu')
    await page.getByRole('tab', { name: 'Báo giá' }).click()
    const dialog = page.getByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' })
    await expect(dialog).toBeVisible()
    await dialog.getByRole('button', { name: 'Tiếp tục sửa' }).click()
    await expect(dialog).toHaveCount(0)
    await expect(page.getByLabel('Tên hiển thị')).toHaveValue('Chưa lưu')
    await page.getByRole('tab', { name: 'Báo giá' }).click()
    await page.getByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' }).getByRole('button', { name: 'Bỏ thay đổi' }).click()
    await expect(page).toHaveURL(/\?tab=quotation$/)
    await expect(page.getByLabel('Tiền tố số BG')).toBeVisible()
    expect((await readSettings(page)).displayName).toBe(saved.displayName)
  })

  test('arrow keys browse the tabs without the discard dialog; Enter opens one', async ({ page }) => {
    await page.getByLabel('Tên hiển thị').fill('Chưa lưu')
    await page.getByRole('tab', { name: 'Studio' }).focus()
    await page.keyboard.press('ArrowRight')
    await expect(page.getByRole('tab', { name: 'Thanh toán' })).toBeFocused()
    await expect(page.getByRole('dialog')).toHaveCount(0)
    await page.keyboard.press('Enter')
    const dialog = page.getByRole('dialog', { name: 'Bỏ thay đổi chưa lưu?' })
    await expect(dialog).toBeVisible()
    await dialog.getByRole('button', { name: 'Bỏ thay đổi' }).click()
    await expect(page).toHaveURL(/\?tab=payment$/)
  })

  test('wrong current password shows a field error', async ({ page }) => {
    await page.getByRole('tab', { name: 'Tài khoản' }).click()
    await page.getByLabel('Mật khẩu hiện tại').fill('definitely-wrong-password')
    await page.getByLabel('Mật khẩu mới').fill('newtestpassword123')
    await page.getByLabel('Xác nhận mật khẩu').fill('newtestpassword123')
    await page.getByRole('button', { name: 'Đổi mật khẩu' }).click()
    await expect(page.getByText('Mật khẩu hiện tại không đúng')).toBeVisible()
  })

  test('offers a terms template when the default terms are empty', async ({ page }) => {
    await page.getByRole('tab', { name: 'Báo giá' }).click()
    await page.getByLabel('Điều khoản mặc định').fill('')
    await page.getByRole('button', { name: 'Dùng điều khoản mẫu' }).click()
    await expect(page.getByLabel('Điều khoản mặc định')).toHaveValue(/^1\. Báo giá có hiệu lực/)
  })

  test('tab list scrolls instead of overflowing the page at 390px', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 })
    for (const tab of ['studio', 'payment', 'quotation', 'portal', 'images', 'notifications', 'account']) {
      await page.goto(`/admin/settings?tab=${tab}`)
      await expect(page.getByRole('tab', { selected: true })).toBeVisible()
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), tab).toBe(true)
    }
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
    await page.getByLabel('Giá vòng sửa thêm').fill('200000')
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
