import { test, expect } from '@playwright/test'
import { apiPost, createClient, createDeliverable, createProjectForClient } from './helpers'
import { configureBank, createService, issuePortalLink, openAsClient, sendQuotation } from './portalHelpers'

// POST /api/portal/session is limited to 10/min per IP: this file opens 3 sessions (portal.spec opens 7) —
// wait at least 65 s between runs of portal specs.

test.describe('Client portal — quotations and requests', () => {
  test('client opens the magic link and sees the sent quotation with its VietQR', async ({ page, browser }) => {
    await page.goto('/admin')
    await configureBank(page)
    const stamp = Date.now()
    const clientId = await createClient(page, `Portal Quote E2E ${stamp}`)
    const projectName = `Nhận diện báo giá ${stamp}`
    const projectId = await createProjectForClient(page, clientId, projectName)
    const serviceId = await createService(page, `E2E Portal Logo ${stamp}`, 2_000_000)
    await apiPost(page.request, page, `/api/admin/projects/${projectId}/deliverables`, { name: 'Logo', quantity: 1, serviceId })
    const { id, number } = await sendQuotation(page, projectId, 'E2E-INTERNAL-NOTE')
    const link = await issuePortalLink(page, clientId)

    const { context, page: portal } = await openAsClient(browser, link)
    await expect(portal).toHaveURL(/\/portal$/)
    await portal.getByRole('link', { name: `Xem báo giá mới của ${projectName}` }).click()

    await expect(portal).toHaveURL(new RegExp(`/portal/quotations/${id}$`))
    await expect(portal.getByText(number).first()).toBeVisible()
    await expect(portal.getByTestId('portal-total')).toContainText('2.000.000')
    await expect(portal.getByTestId('vietqr')).toBeVisible()
    await expect(portal.getByTestId('qr-amount')).toContainText('1.000.000')
    await expect(portal.getByTestId('qr-memo')).toHaveText(`${number.replace(/-/g, '')} COC`)
    await portal.getByRole('radio', { name: 'Thanh toán toàn bộ' }).click()
    await expect(portal.getByTestId('qr-amount')).toContainText('2.000.000')
    await expect(portal.getByRole('link', { name: 'Tải ảnh QR' }))
      .toHaveAttribute('href', `/api/portal/quotations/${id}/qr/full.png`)
    await expect(portal.locator('body')).not.toContainText('E2E-INTERNAL-NOTE')
    await expect(portal.getByRole('button', { name: /chấp nhận|từ chối/i })).toHaveCount(0)   // ADR-004

    await portal.getByRole('navigation', { name: 'Điều hướng portal' }).getByRole('link', { name: 'Báo giá' }).click()
    await expect(portal).toHaveURL(/\/portal\/quotations$/)
    await expect(portal.getByRole('link', { name: new RegExp(number) })).toBeVisible()
    await context.close()
  })

  test('client sends a change request that the designer sees in the inbox', async ({ page, browser }) => {
    await page.goto('/admin')
    const stamp = Date.now()
    const clientId = await createClient(page, `Portal Request E2E ${stamp}`)
    const projectName = `Dự án yêu cầu ${stamp}`
    const projectId = await createProjectForClient(page, clientId, projectName)
    await createDeliverable(page, projectId, 'Logo chính', 3)
    const link = await issuePortalLink(page, clientId)
    const content = `Đổi màu chữ sang xanh navy ${stamp}`

    const { context, page: portal } = await openAsClient(browser, link)
    await expect(portal).toHaveURL(/\/portal$/)
    await portal.getByRole('link', { name: new RegExp(projectName) }).click()
    await portal.getByRole('link', { name: 'Gửi yêu cầu chỉnh sửa' }).click()

    await expect(portal).toHaveURL(new RegExp(`/portal/requests/new\\?projectId=${projectId}$`))
    await expect(portal.getByLabel('Dự án', { exact: true })).toHaveValue(projectId)
    await portal.getByLabel('Hạng mục (tuỳ chọn)').selectOption({ label: 'Logo chính' })
    await portal.getByLabel('Nội dung yêu cầu').fill(content)
    await portal.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click()
    await expect(portal.getByText('Đã gửi yêu cầu', { exact: true })).toBeVisible()

    await portal.getByRole('link', { name: 'Xem yêu cầu của tôi' }).click()
    await expect(portal).toHaveURL(/\/portal\/requests$/)
    const item = portal.getByRole('listitem').filter({ hasText: content })
    await expect(item).toContainText('Mới')
    await expect(item).toContainText('Logo chính')
    await context.close()

    await page.goto('/admin/requests')
    await expect(page.getByText(content).first()).toBeVisible()
  })

  test('client follows revision rounds and the free-revision quota on the project page', async ({ page, browser }) => {
    await page.goto('/admin')
    const stamp = Date.now()
    const clientId = await createClient(page, `Portal Revisions E2E ${stamp}`)
    const projectName = `Dự án lần sửa ${stamp}`
    const projectId = await createProjectForClient(page, clientId, projectName)
    const logo = await createDeliverable(page, projectId, 'Logo chính', 3)
    await apiPost(page.request, page, `/api/admin/projects/${projectId}/rounds`, {
      deliverableId: logo,
      receivedAt: null,
      items: [
        { description: 'Đổi màu chữ sang xanh navy', type: 'MINOR', billing: 'IN_QUOTA' },
        { description: 'Thêm tagline dưới logo', type: 'MAJOR', billing: 'CHARGEABLE' },
      ],
    })
    const link = await issuePortalLink(page, clientId)

    const { context, page: portal } = await openAsClient(browser, link)
    await expect(portal).toHaveURL(/\/portal$/)
    await portal.getByRole('link', { name: new RegExp(projectName) }).click()

    const revisions = portal.getByRole('region', { name: 'Lần sửa', exact: true })
    await expect(revisions).toContainText('Logo chính')
    await expect(revisions).toContainText('1/3 lần sửa miễn phí')
    await expect(revisions).toContainText('Lần sửa 1')
    const inQuota = revisions.getByRole('listitem').filter({ hasText: 'Đổi màu chữ sang xanh navy' }).last()
    await expect(inQuota).toContainText('Trong số lần sửa')
    await expect(inQuota).toContainText('Chờ làm')
    await expect(revisions.getByRole('listitem').filter({ hasText: 'Thêm tagline dưới logo' }).last()).toContainText('Tính phí')
    await context.close()
  })
})
