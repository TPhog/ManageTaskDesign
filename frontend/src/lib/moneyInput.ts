/** Digits only (a trailing ,decimal part is dropped) → whole đồng (BR-02); anything else is ignored, '' → 0. */
export function parseMoneyInput(text: string): number {
  const digits = text.replace(/,\d*\s*$/, '').replace(/\D/g, '')
  return digits === '' ? 0 : Math.min(Number(digits), Number.MAX_SAFE_INTEGER)
}

/** 2000000 → '2.000.000' (vi-VN grouping, no currency sign). */
export function formatMoneyInput(n: number): string {
  return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(n)
}
