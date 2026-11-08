import { type Page, expect } from '@playwright/test'
import { csrfToken } from './helpers'

/** A valid 1×1 PNG (checked: IHDR/IDAT/IEND CRCs) — the API turns it into three WebP variants. */
export const PNG_1X1 = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
)

export const pngFile = (name: string) => ({ name, mimeType: 'image/png', buffer: PNG_1X1 })

async function headers(page: Page) {
  return { 'X-CSRF-Token': await csrfToken(page), Origin: new URL(page.url()).origin }
}

/** Upload one image to a task through the API; returns the AdminImage JSON. page must be on an app URL. */
export async function uploadTaskImage(page: Page, taskId: string, name = 'anh.png') {
  const res = await page.request.post(`/api/admin/tasks/${taskId}/images`, {
    headers: await headers(page),
    multipart: { files: pngFile(name), visibleToClient: 'true' },
  })
  expect(res.status(), `upload → ${res.status()}`).toBe(201)
  return (await res.json())[0]
}

/** Progress update with one photo through the API; returns { id, images }. */
export async function postProgressUpdateWithImage(page: Page, projectId: string, message: string, name = 'tien-do.png') {
  const res = await page.request.post(`/api/admin/projects/${projectId}/progress-updates`, {
    headers: await headers(page),
    multipart: { message, files: pngFile(name) },
  })
  expect(res.status(), `progress update → ${res.status()}`).toBe(201)
  return res.json()
}
