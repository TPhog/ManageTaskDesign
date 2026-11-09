import { test, expect } from '@playwright/test'
import { createClient, createDeliverable, createProjectForClient } from './helpers'
import { archiveClient } from './cleanup'
import { issuePortalLink, openAsClient } from './portalHelpers'

test.describe('Client portal — request by template (plan 13)', () => {
  test('client picks Poster, answers the questions and the designer receives the rendered request', async ({ page, browser }) => {
    await page.goto('/admin')
    const stamp = Date.now()
    const clientId = await createClient(page, `Portal Template E2E ${stamp}`)
    const projectId = await createProjectForClient(page, clientId, `Dự án mẫu ${stamp}`)
    await createDeliverable(page, projectId, 'Poster khai trương')
    const { context, page: portal } = await openAsClient(browser, await issuePortalLink(page, clientId))
    try {
      await expect(portal).toHaveURL(/\/portal$/)
      await portal.goto(`/portal/requests/new?projectId=${projectId}`)

      const kind = portal.getByRole('group', { name: 'Loại yêu cầu' })
      await expect(kind.getByRole('radio', { name: /^Góp ý \/ yêu cầu khác/ })).toBeChecked()
      await expect(portal.getByLabel('Nội dung cần sửa')).toBeVisible()

      await kind.getByRole('radio', { name: /^Poster/ }).check()
      await expect(portal.getByLabel('Nội dung cần sửa')).toHaveCount(0)
      // required answers are checked before anything is sent
      await portal.getByRole('button', { name: 'Gửi yêu cầu' }).click()
      await expect(portal.getByText('Bắt buộc').first()).toBeVisible()

      await portal.getByRole('textbox', { name: 'Mục đích poster' }).fill('Khai trương chi nhánh 2')
      await portal.getByRole('radio', { name: 'In ấn', exact: true }).check()
      await portal.getByRole('radio', { name: 'A3 (297 × 420 mm)', exact: true }).check()
      await portal.getByRole('textbox', { name: 'Tiêu đề chính' }).fill(`Khai trương ${stamp}`)
      await portal.getByLabel('Ghi chú thêm (tuỳ chọn)').fill('Gửi trước thứ 6')
      await portal.getByRole('button', { name: 'Gửi yêu cầu' }).click()

      await expect(portal.getByRole('heading', { name: 'Đã gửi yêu cầu' })).toBeFocused()

      const requests = await (await page.request.get(`/api/admin/requests?projectId=${projectId}`)).json()
      const items = (requests.items ?? requests) as { request?: { content: string; templateName: string | null }; content?: string; templateName?: string | null }[]
      const sent = items.map(i => i.request ?? i).find(r => r.content?.includes(`Khai trương ${stamp}`))
      expect(sent?.templateName).toBe('Poster')
      expect(sent?.content).toContain('Gửi trước thứ 6')
    } finally {
      await context.close()
      await archiveClient(page, clientId)
    }
  })
})
