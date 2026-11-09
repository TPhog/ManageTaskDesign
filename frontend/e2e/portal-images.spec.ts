import { test, expect } from '@playwright/test'
import { apiPost, createClient, createDeliverable, createProjectForClient } from './helpers'
import { issuePortalLink, openAsClient } from './portalHelpers'
import { pngFile, postProgressUpdateWithImage, uploadTaskImage } from './imageHelpers'

test.describe('Client portal — images', () => {
  test('client sees watermarked task and progress photos, never the original, and attaches a photo to a request', async ({ page, browser }) => {
    await page.goto('/admin')
    const stamp = Date.now()
    const clientId = await createClient(page, `Portal Images E2E ${stamp}`)
    const projectName = `Dự án ảnh ${stamp}`
    const projectId = await createProjectForClient(page, clientId, projectName)
    const deliverableId = await createDeliverable(page, projectId, 'Logo chính')
    const task = await apiPost(page.request, page, `/api/admin/projects/${projectId}/tasks`,
      { title: `SECRET-E2E-TASK ${stamp}`, deliverableId })
    await uploadTaskImage(page, task.id, 'phac-thao.png')
    const message = `Đã xong phác thảo ${stamp}`
    await postProgressUpdateWithImage(page, projectId, message, 'tien-do.png')
    const link = await issuePortalLink(page, clientId)

    const { context, page: portal } = await openAsClient(browser, link)
    await expect(portal).toHaveURL(/\/portal$/)
    await portal.goto(`/portal/projects/${projectId}`)

    const gallery = portal.getByRole('region', { name: 'Hình ảnh' })
    await expect(gallery).toContainText('Logo chính')
    await expect(gallery).toContainText('Cập nhật tiến độ')
    const thumbs = gallery.locator('img')
    await expect(thumbs).toHaveCount(2)
    for (const src of await thumbs.evaluateAll(els => els.map(e => e.getAttribute('src')))) {
      expect(src).toMatch(/^\/api\/portal\/images\/[0-9a-f-]+\/thumb$/)
    }
    await expect(portal.locator('body')).not.toContainText('SECRET-E2E-TASK')
    await expect(portal.locator('body')).not.toContainText('phac-thao.png')

    await gallery.getByRole('button', { name: 'Xem ảnh Ảnh Logo chính' }).click()
    const viewer = portal.getByRole('dialog', { name: 'Ảnh Logo chính' })
    const previewSrc = await viewer.getByRole('img', { name: 'Ảnh Logo chính' }).getAttribute('src')   // the blurred thumb under it is aria-hidden
    expect(previewSrc).toMatch(/\/preview$/)
    const preview = await portal.request.get(previewSrc!)
    expect(preview.status()).toBe(200)
    expect(preview.headers()['content-type']).toContain('image/webp')
    expect(preview.headers()['cache-control']).toContain('private')
    expect((await portal.request.get(previewSrc!.replace(/\/preview$/, '/original'))).status()).toBe(404)
    await portal.keyboard.press('Escape')

    const timeline = portal.getByRole('region', { name: 'Hoạt động gần đây' })
    const entry = timeline.getByRole('listitem').filter({ hasText: message }).first()
    await expect(entry.locator('img')).toHaveCount(1)

    await portal.goto(`/portal/requests/new?projectId=${projectId}`)
    await portal.getByLabel('Nội dung cần sửa').fill('Đổi màu theo ảnh mẫu')
    await portal.locator('#request-images').setInputFiles([pngFile('mau-mau.png')])
    await expect(portal.getByRole('list', { name: 'Ảnh đính kèm' }).getByRole('img', { name: 'mau-mau.png' })).toBeVisible()
    await portal.getByRole('button', { name: 'Gửi yêu cầu' }).click()
    await expect(portal.getByText('Đã gửi yêu cầu')).toBeVisible()
    await portal.goto(`/portal/projects/${projectId}`)
    await expect(portal.getByRole('region', { name: 'Yêu cầu chỉnh sửa của bạn' }).locator('img[alt="mau-mau.png"]')).toBeVisible()
    await context.close()

    const requests = await (await page.request.get(`/api/admin/requests?projectId=${projectId}`)).json()
    await page.goto(`/admin/requests/${requests.items[0].id}`)
    await expect(page.locator('img[alt="mau-mau.png"]')).toHaveAttribute('src', /^\/api\/admin\/images\/[0-9a-f-]+\/thumb$/)
  })
})
