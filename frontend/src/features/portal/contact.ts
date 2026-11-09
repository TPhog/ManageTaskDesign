/** QA C-04: Zalo opens a chat from the phone number (digits only). */
export const zaloUrl = (phone: string) => `https://zalo.me/${phone.replace(/\D/g, '')}`

/** CF-14: a two-letter "logo" from the studio name — "Minh Anh Design Studio" → "MA"; blank → "?". */
export function studioInitials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean)
  if (words.length === 0) return '?'
  return words.slice(0, 2).map(w => w[0]).join('').toLocaleUpperCase('vi-VN')
}
