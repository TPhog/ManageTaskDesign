import { rawPaletteHits, rawPaletteRegex } from './paletteGuard'

const hitsOf = (line: string) => rawPaletteHits({ 'x.tsx': line }).map(h => h.replace(/^x\.tsx:\d+: /, ''))

describe('paletteGuard', () => {
  it.each([
    ['bg-red-50', 'bg-red-50'],
    ['border-t-red-500', 'border-t-red-500'],
    ['border-x-slate-200', 'border-x-slate-200'],
    ['divide-y-red-200', 'divide-y-red-200'],
    ['placeholder-slate-400', 'placeholder-slate-400'],
    ['hover:text-emerald-700', 'text-emerald-700'],
    ['bg-linear-to-r from-sky-500 via-blue-500 to-indigo-600', 'from-sky-500'],
    ['from-white', 'from-white'],
    ['shadow-black/20', 'shadow-black'],
    ['shadow-slate-900/10', 'shadow-slate-900'],
    ['bg-black/80 text-white', 'bg-black'],
    ['bg-[#fff]', '#fff'],
    ['bg-[#ff000080]', '#ff000080'],
    ['text-[#1c1a17]', '#1c1a17'],
    ['bg-[rgb(0,0,0)]', 'rgb('],
    ['shadow-[0_1px_2px_rgba(0,0,0,.1)]', 'rgba('],
    ['bg-[hsl(0_0%_100%)]', 'hsl('],
    ["style={{ color: 'red' }}", "color: 'red'"],
    ['style={{ backgroundColor: "#fff" }}', 'backgroundColor: "#fff"'],
  ])('flags %s', (line, hit) => {
    expect(hitsOf(line)).toContain(hit)
  })

  it.each([
    'bg-highlight-soft text-highlight-text ring-highlight/30',
    'bg-surface-sunk text-muted-foreground ring-border border-border-strong',
    'bg-overlay shadow-pop decoration-highlight decoration-[1.5px] rounded-t-[4px] text-[0.9375rem]',
    'whitespace-nowrap editorial:rounded-none',
    "style={{ fill: 'var(--highlight)', color: 'currentColor', background: 'transparent' }}",
    'Báo giá #12 · &#123;',
  ])('leaves semantic classes alone: %s', line => {
    expect(hitsOf(line)).toEqual([])
  })

  it('skips a line with a palette-ok reason, but not a bare marker', () => {
    expect(hitsOf("<QRCodeSVG bgColor=\"#ffffff\" /> {/* palette-ok: QR must stay black on white */}")).toEqual([])
    expect(hitsOf('bg-white // palette-ok:')).toEqual(['bg-white'])
  })

  it('reports path and line number', () => {
    expect(rawPaletteHits({ 'a.tsx': 'ok\nbg-red-50' })).toEqual(['a.tsx:2: bg-red-50'])
  })

  it('returns a fresh regex each call (no shared lastIndex)', () => {
    const a = rawPaletteRegex()
    a.test('bg-red-50')
    expect(rawPaletteRegex().lastIndex).toBe(0)
    expect(rawPaletteRegex()).not.toBe(a)
  })
})
