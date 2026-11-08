const WEEKDAYS = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy']

const pad = (n: number) => String(n).padStart(2, '0')

/** 'YYYY-MM-DD' → 'dd/mm/yyyy'; '' or a malformed value → ''. */
export function isoToVn(iso: string | null | undefined): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso ?? '')
  return m ? `${m[3]}/${m[2]}/${m[1]}` : ''
}

function isRealDate(y: number, m: number, d: number): boolean {
  if (y < 1900 || y > 2100 || m < 1 || m > 12 || d < 1) return false
  return d <= new Date(Date.UTC(y, m, 0)).getUTCDate()
}

/**
 * QA D-05: day-first Vietnamese input → 'YYYY-MM-DD', or null when it is not a real date.
 * Accepts d/m/yyyy with '/', '-', '.' or a space, 8 digits ddmmyyyy, and a pasted ISO date.
 */
export function vnToIso(text: string): string | null {
  const t = text.trim()
  const iso = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(t)
  const vn = /^(\d{1,2})[-./\s](\d{1,2})[-./\s](\d{4})$/.exec(t)
  const digits = /^(\d{2})(\d{2})(\d{4})$/.exec(t)
  const [y, m, d] = iso
    ? [Number(iso[1]), Number(iso[2]), Number(iso[3])]
    : vn
      ? [Number(vn[3]), Number(vn[2]), Number(vn[1])]
      : digits
        ? [Number(digits[3]), Number(digits[2]), Number(digits[1])]
        : [0, 0, 0]
  return isRealDate(y, m, d) ? `${y}-${pad(m)}-${pad(d)}` : null
}

/** 'YYYY-MM-DD' → 'Chủ Nhật, 08/11/2026' — shown under the input. */
export function weekdayLabel(iso: string): string {
  const vn = isoToVn(iso)
  if (!vn) return ''
  const [y, m, d] = iso.split('-').map(Number)
  return `${WEEKDAYS[new Date(Date.UTC(y, m - 1, d)).getUTCDay()]}, ${vn}`
}
