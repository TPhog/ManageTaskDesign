const BASE = '/api'

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }

  if (method !== 'GET' && method !== 'HEAD') {
    const csrfToken = document.cookie
      .split('; ')
      .find(row => row.startsWith('ff_csrf='))
      ?.split('=')[1]
    if (csrfToken) headers['X-CSRF-Token'] = decodeURIComponent(csrfToken)
  }

  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    credentials: 'same-origin',
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (!res.ok) {
    const err = await res.json().catch(() => ({ title: res.statusText })) as { title?: string; detail?: string }
    throw Object.assign(new Error(err.title ?? res.statusText), {
      status: res.status,
      detail: err.detail,
    })
  }

  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

export const api = {
  get:    <T>(path: string)                   => request<T>('GET',    path),
  post:   <T>(path: string, body: unknown)    => request<T>('POST',   path, body),
  put:    <T>(path: string, body: unknown)    => request<T>('PUT',    path, body),
  patch:  <T>(path: string, body: unknown)    => request<T>('PATCH',  path, body),
  delete: <T>(path: string)                   => request<T>('DELETE', path),
}
