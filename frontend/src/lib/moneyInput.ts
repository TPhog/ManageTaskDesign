/**
 * Whole đồng (BR-02) from typed or pasted text: "." and "," are thousands separators ("2.500.000", "2,500,000").
 * Anything that looks like decimals (a separator followed by 1-2 digits at the end, "1,5") has no meaning in VND
 * and gives null so the caller can reject it. Empty → 0.
 */
export function parseMoneyInput(text: string): number | null {
  if (/[.,]\d{1,2}\s*(?:₫|đ|vnd)?\s*$/i.test(text)) return null
  const digits = text.replace(/\D/g, '')
  return digits === '' ? 0 : Math.min(Number(digits), Number.MAX_SAFE_INTEGER)
}

/** 2000000 → '2.000.000' (vi-VN grouping, no currency sign). */
export function formatMoneyInput(n: number): string {
  return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(n)
}
