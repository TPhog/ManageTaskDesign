const SAFE_PROTOCOLS = ['http:', 'https:', 'mailto:', 'tel:']

/**
 * react-markdown urlTransform (plan 06b F5): only absolute http(s)/mailto/tel URLs survive.
 * Anything else (javascript:, data:, relative paths, garbage) becomes '' and renders as plain text.
 */
export function safeHref(url: string): string {
  const value = url.trim()
  if (!value) return ''
  try {
    return SAFE_PROTOCOLS.includes(new URL(value).protocol) ? value : ''
  } catch {
    return ''
  }
}
