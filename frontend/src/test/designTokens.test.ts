import css from '../index.css?raw'
import mainTsx from '../main.tsx?raw'
import indexHtml from '../../index.html?raw'

type Vars = Record<string, string>

const TEXT = 4.5
const UI = 3

function varsOf(text: string): Vars {
  const out: Vars = {}
  for (const m of text.matchAll(/--([\w-]+)\s*:\s*([^;]+);/g)) out[m[1]] = m[2].trim()
  return out
}

function blockOf(re: RegExp): Vars {
  const m = css.match(re)
  if (!m) throw new Error(`index.css is missing ${re}`)
  return varsOf(m[1])
}

const root: Vars = Object.assign({}, ...[...css.matchAll(/:root\s*\{([^}]*)\}/g)].map(m => varsOf(m[1])))
const editorial = { ...root, ...blockOf(/\[data-theme="editorial"\]\s*\{([^}]*)\}/) }
const inverse = { ...root, ...blockOf(/@utility tone-inverse\s*\{([^}]*)\}/) }
const highlight = { ...root, ...blockOf(/@utility tone-highlight\s*\{([^}]*)\}/) }
const editorialInverse = { ...editorial, ...blockOf(/@utility tone-inverse\s*\{([^}]*)\}/) }

function hex(scope: Vars, name: string): string {
  let v = scope[name]
  for (let i = 0; v?.startsWith('var(') && i < 10; i++) v = scope[v.slice(6, -1)]
  if (!v || !/^#[0-9a-f]{6}$/i.test(v)) throw new Error(`--${name} does not resolve to a #rrggbb colour (got ${v})`)
  return v
}

function luminance(h: string): number {
  const [r, g, b] = [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16) / 255)
    .map(c => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4))
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function contrast(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (hi + 0.05) / (lo + 0.05)
}

type Pair = [fg: string, bg: string, min: number]

const bento: Pair[] = [
  ['foreground', 'background', TEXT], ['foreground', 'card', TEXT], ['foreground', 'surface-sunk', TEXT], ['foreground', 'surface-hover', TEXT],
  ['muted-foreground', 'background', TEXT], ['muted-foreground', 'card', TEXT], ['muted-foreground', 'surface-sunk', TEXT], ['muted-foreground', 'muted', TEXT],
  ['primary-foreground', 'primary', TEXT], ['secondary-foreground', 'secondary', TEXT], ['accent-foreground', 'accent', TEXT],
  ['highlight-foreground', 'highlight', TEXT], ['highlight-text', 'background', TEXT], ['highlight-text', 'card', TEXT], ['highlight-text', 'highlight-soft', TEXT],
  ['success', 'success-soft', TEXT], ['success', 'card', TEXT], ['success-foreground', 'success', TEXT],
  ['warning', 'warning-soft', TEXT], ['warning', 'card', TEXT], ['warning-foreground', 'warning', TEXT],
  ['danger', 'danger-soft', TEXT], ['danger', 'card', TEXT], ['destructive-foreground', 'destructive', TEXT],
  ['on-inverse', 'surface-inverse', TEXT], ['on-inverse-muted', 'surface-inverse', TEXT], ['on-inverse-muted', 'surface-inverse-raised', TEXT], ['on-inverse-accent', 'surface-inverse', TEXT],
  ['input', 'card', UI], ['input', 'background', UI], ['ring', 'background', UI], ['ring', 'card', UI],
]

const editorialPairs: Pair[] = [
  ['foreground', 'background', TEXT], ['foreground', 'card', TEXT], ['foreground', 'muted', TEXT],
  ['muted-foreground', 'background', TEXT], ['muted-foreground', 'card', TEXT], ['muted-foreground', 'muted', TEXT],
  ['primary-foreground', 'primary', TEXT], ['highlight-foreground', 'highlight', TEXT],
  ['highlight-text', 'background', TEXT], ['highlight-text', 'card', TEXT], ['highlight-text', 'highlight-soft', TEXT],
  ['success', 'background', TEXT], ['success', 'success-soft', TEXT], ['success-foreground', 'success', TEXT],
  ['warning', 'background', TEXT], ['warning', 'warning-soft', TEXT], ['warning-foreground', 'warning', TEXT],
  ['danger', 'background', TEXT], ['danger', 'danger-soft', TEXT], ['destructive-foreground', 'destructive', TEXT],
  ['on-inverse', 'surface-inverse', TEXT], ['on-inverse-muted', 'surface-inverse-raised', TEXT],
  ['input', 'background', UI], ['input', 'card', UI], ['ring', 'background', UI],
]

const scoped: Pair[] = [
  ['foreground', 'background', TEXT], ['muted-foreground', 'background', TEXT], ['muted-foreground', 'card', TEXT],
  ['primary-foreground', 'primary', TEXT], ['highlight-text', 'background', TEXT], ['ring', 'background', UI],
]

const highlightPairs: Pair[] = [['foreground', 'background', TEXT], ['muted-foreground', 'background', TEXT], ['ring', 'background', UI]]

function check(scope: Vars, pairs: Pair[]) {
  for (const [fg, bg, min] of pairs) {
    const ratio = contrast(hex(scope, fg), hex(scope, bg))
    expect(ratio, `--${fg} on --${bg} = ${ratio.toFixed(2)}:1 (min ${min})`).toBeGreaterThanOrEqual(min)
  }
}

describe('design tokens (P16)', () => {
  it('reads index.css as text', () => {
    expect(css.length).toBeGreaterThan(2000)
  })
  it('Studio Bento pairs meet WCAG AA', () => check(root, bento))
  it('Editorial pairs meet WCAG AA', () => check(editorial, editorialPairs))
  it('ink tiles (tone-inverse) stay readable in both themes', () => {
    check(inverse, scoped)
    check(editorialInverse, scoped)
  })
  it('vermilion tiles (tone-highlight) keep ink text', () => check(highlight, highlightPairs))
  it('self-hosts the four families with a Vietnamese subset and drops Plus Jakarta Sans', () => {
    for (const entry of [
      '@fontsource-variable/bricolage-grotesque/opsz.css',
      '@fontsource/be-vietnam-pro/400.css',
      '@fontsource/be-vietnam-pro/700.css',
      '@fontsource-variable/fraunces/opsz.css',
      '@fontsource-variable/fraunces/opsz-italic.css',
      '@fontsource-variable/public-sans/wght.css',
    ]) expect(mainTsx).toContain(entry)
    expect(indexHtml).not.toMatch(/fonts\.(googleapis|gstatic)\.com/)
    expect(css).not.toMatch(/Plus Jakarta/)
  })
})
