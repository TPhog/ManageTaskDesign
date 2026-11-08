const BASE = '/api'

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  // multipart: the browser sets Content-Type with the boundary itself
  const isForm = body instanceof FormData
  const headers: Record<string, string> = isForm ? {} : { 'Content-Type': 'application/json' }

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
    body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
  })

  if (!res.ok) {
    const err = await res.json().catch(() => ({ title: res.statusText })) as {
      title?: string; detail?: string; code?: string; errors?: Record<string, string[]>; warnings?: string[]
    }
    throw Object.assign(new Error(err.title ?? res.statusText), {
      status: res.status,
      detail: err.detail,
      code: err.code,
      errors: err.errors,
      warnings: err.warnings,
    }) as ApiError
  }

  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

export type ApiError = Error & {
  status: number
  detail?: string
  /** DomainException code from ProblemDetails extensions */
  code?: string
  /** ValidationProblem field errors, keyed by camelCase field name */
  errors?: Record<string, string[]>
  /** 422 BUSINESS_RULE that the designer may confirm (docs/05 section 3.4 confirmWarnings) */
  warnings?: string[]
}

/** Optional form fields: map '' (empty input) to null so the API never receives blank strings. */
export function emptyToNull<T extends Record<string, unknown>>(values: T): T {
  return Object.fromEntries(
    Object.entries(values).map(([k, v]) => [k, v === '' ? null : v]),
  ) as T
}

export const api = {
  get:    <T>(path: string)                   => request<T>('GET',    path),
  post:   <T>(path: string, body: unknown)    => request<T>('POST',   path, body),
  postForm: <T>(path: string, form: FormData) => request<T>('POST',   path, form),
  put:    <T>(path: string, body: unknown)    => request<T>('PUT',    path, body),
  patch:  <T>(path: string, body: unknown)    => request<T>('PATCH',  path, body),
  delete: <T>(path: string)                   => request<T>('DELETE', path),
}
