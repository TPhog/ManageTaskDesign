/** Tailwind palette steps, white/black and hex literals: after P16 only semantic tokens may colour admin UI. */
export const RAW_PALETTE =
  /\b(?:bg|text|border|ring|ring-offset|divide|fill|stroke|outline|decoration|from|via|to|accent|caret|shadow)-(?:slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)-\d{2,3}\b|\b(?:bg|text|border|ring|fill|stroke)-(?:white|black)\b|#[0-9a-fA-F]{6}\b/g

export function rawPaletteHits(files: Record<string, string>): string[] {
  return Object.entries(files).flatMap(([path, src]) => (src.match(RAW_PALETTE) ?? []).map(hit => `${path}: ${hit}`))
}
