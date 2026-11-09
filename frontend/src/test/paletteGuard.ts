/** After P16 only semantic tokens may colour admin UI. The guard flags, per line:
 *  - any utility on a Tailwind palette step: bg-red-50, border-t-slate-200, divide-y-red-200, placeholder-slate-400, from-sky-500, shadow-slate-900/10
 *  - white/black on any utility: text-white, bg-black/80, from-white, shadow-black/20
 *  - colour literals: #rgb/#rgba/#rrggbb/#rrggbbaa (also inside bg-[#fff]), rgb()/rgba()/hsl()/hsla()/oklch()/oklab()/hwb()/lab()/lch()
 *  - inline style colour props set to a literal: style={{ color: 'red' }} (var(--…), currentColor, transparent, inherit are fine)
 * Escape hatch: put `palette-ok: <reason>` in a comment on the same line (e.g. the QR code must stay black on white). */

const COLORS = 'slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose'
const STYLE_PROPS = 'color|background|backgroundColor|border(?:Top|Right|Bottom|Left)?Color|fill|stroke|outlineColor|textDecorationColor|caretColor|accentColor|boxShadow'
const SAFE_STYLE_VALUE = String.raw`\s*(?:var\(|(?:currentColor|transparent|inherit|initial|unset|none)\b)`

const SOURCES = [
  String.raw`\b[a-z][a-z-]*-(?:${COLORS})-\d{2,3}\b`,
  String.raw`\b[a-z][a-z-]*-(?:white|black)\b`,
  String.raw`(?<![&\w])#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})\b`,
  String.raw`(?<![a-zA-Z])(?:rgba?|hsla?|oklch|oklab|hwb|lab|lch)\(`,
  String.raw`\b(?:${STYLE_PROPS})\s*:\s*['"\x60](?!${SAFE_STYLE_VALUE})[^'"\x60]*['"\x60]`,
]

const ESCAPE_HATCH = /palette-ok:\s*\S/

/** A fresh global regex on every call, so callers never share `lastIndex`. */
export function rawPaletteRegex(): RegExp {
  return new RegExp(SOURCES.join('|'), 'g')
}

/** `path:line: hit` for every raw colour outside a `palette-ok:` line. */
export function rawPaletteHits(files: Record<string, string>): string[] {
  return Object.entries(files).flatMap(([path, src]) =>
    src.split(/\r?\n/).flatMap((line, i) =>
      ESCAPE_HATCH.test(line) ? [] : (line.match(rawPaletteRegex()) ?? []).map(hit => `${path}:${i + 1}: ${hit}`),
    ),
  )
}
