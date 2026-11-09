import { FORBIDDEN_UI_TERMS, GLOSSARY } from '@/lib/glossary'

const files = import.meta.glob('/src/**/*.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>

/** Imports, line comments and doc-comment lines are code, not UI text. */
const isCode = (line: string) => /^\s*(import\b|\/\/|\*|\/\*)/.test(line)

/**
 * '} deliverable' is meant for JSX text ("{n} deliverable"); as a plain substring it would also hit
 * props like `projectId={id} deliverableId={d}`, so a word boundary follows it.
 */
function contains(line: string, term: string): boolean {
  return term === '} deliverable' ? /\} deliverable(?![\w=])/.test(line) : line.includes(term)
}

/** Known leftovers in files owned by another plan; delete the entry when that plan fixes the string. */
const PENDING = new Set(['/src/features/admin/dashboard/MoneyInsightsBlock.tsx|tin nhắc']) // P16 task 5 redesigns the dashboard: toast 'Đã sao chép tin nhắc thanh toán'

describe('glossary', () => {
  it('names the agreed terms', () => {
    expect(GLOSSARY.deliverable).toBe('hạng mục')
    expect(GLOSSARY.extraRoundPrice).toBe('Giá vòng sửa thêm')
  })

  it('no component renders a forbidden term', () => {
    const hits: string[] = []
    for (const [path, source] of Object.entries(files)) {
      if (path.endsWith('.test.tsx')) continue
      source.split('\n').forEach((line, i) => {
        if (isCode(line)) return
        for (const term of FORBIDDEN_UI_TERMS) {
          if (contains(line, term) && !PENDING.has(`${path}|${term}`)) hits.push(`${path}:${i + 1} "${term}"`)
        }
      })
    }
    expect(hits).toEqual([])
  })
})
