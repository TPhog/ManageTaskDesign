export function formatVnd(amount: number): string {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(amount)
}

export const formatMoney = formatVnd

const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/

export function formatDate(value: string | Date | null | undefined): string {
  if (!value) return '—'
  // 'yyyy-MM-dd' is a calendar date: build it in local time so no timezone shifts the day
  const m = typeof value === 'string' ? DATE_ONLY.exec(value) : null
  const date = m ? new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3])) : new Date(value)
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date)
}

export function formatDatetime(value: string | Date | null | undefined): string {
  if (!value) return '—'
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}
