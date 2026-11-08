import type { ApiError } from '@/lib/api'
import { quotaTone } from '@/lib/revisionLabels'
import type { RevisionItem, RevisionRound, RoundInput } from '@/lib/revisionTypes'
import {
  cleanRounds,
  emptyRound,
  errorText,
  isQuotable,
  needsScopeOverride,
  quotableItems,
  workStatusTransitions,
} from '@/features/admin/revisions/revisionUtils'

function item(overrides: Partial<RevisionItem> = {}): RevisionItem {
  return {
    id: 'i1', roundId: 'r1', description: 'Đổi màu', type: 'MINOR', billing: 'CHARGEABLE', workStatus: 'TODO',
    quotationItemId: null, quotation: null, scopeOverrideReason: null, internalNotes: '', sortOrder: 10,
    rowVersion: 1, suggestedBilling: null, suggestionReason: null, tasks: [], ...overrides,
  }
}

function round(items: RevisionItem[]): RevisionRound {
  return {
    id: 'r1', projectId: 'p1', deliverableId: 'd1', deliverableName: 'Logo', roundNo: 1, receivedAt: '2026-11-08',
    sourceRequestId: null, counted: false, createdAt: '2026-11-08T03:00:00Z', items,
  }
}

const sent = { id: 'q1', number: 'BG-2026-0001', version: 1, status: 'SENT', effectiveStatus: 'EXPIRED' } as const
const accepted = { id: 'q1', number: 'BG-2026-0001', version: 1, status: 'ACCEPTED', effectiveStatus: 'ACCEPTED' } as const

describe('isQuotable', () => {
  it('accepts only unquoted, live, chargeable items (BR-46)', () => {
    expect(isQuotable(item())).toBe(true)
    expect(isQuotable(item({ workStatus: 'IN_PROGRESS' }))).toBe(true)
    expect(isQuotable(item({ billing: 'IN_QUOTA' }))).toBe(false)
    expect(isQuotable(item({ quotationItemId: 'line1' }))).toBe(false)
    expect(isQuotable(item({ workStatus: 'CANCELLED' }))).toBe(false)
  })
})

describe('quotableItems', () => {
  it('collects quotable items across rounds', () => {
    const rounds = [round([item({ id: 'a' }), item({ id: 'b', billing: 'FREE_FIX' })]), round([item({ id: 'c' })])]
    expect(quotableItems(rounds).map(i => i.id)).toEqual(['a', 'c'])
  })
})

describe('needsScopeOverride', () => {
  it('guards chargeable work until the quotation is accepted (BR-60)', () => {
    expect(needsScopeOverride(item(), 'IN_PROGRESS')).toBe(true)
    expect(needsScopeOverride(item({ quotation: sent }), 'DONE')).toBe(true)
    expect(needsScopeOverride(item({ quotation: accepted }), 'IN_PROGRESS')).toBe(false)
    expect(needsScopeOverride(item(), 'CANCELLED')).toBe(false)
    expect(needsScopeOverride(item({ billing: 'IN_QUOTA' }), 'DONE')).toBe(false)
  })
})

describe('workStatusTransitions', () => {
  it('mirrors the BR-60 diagram', () => {
    expect(workStatusTransitions.TODO.map(t => t.to)).toEqual(['IN_PROGRESS', 'CANCELLED'])
    expect(workStatusTransitions.IN_PROGRESS.map(t => t.to)).toEqual(['DONE', 'CANCELLED'])
    expect(workStatusTransitions.DONE.map(t => t.to)).toEqual(['IN_PROGRESS'])
    expect(workStatusTransitions.CANCELLED).toEqual([])
  })
})

describe('cleanRounds', () => {
  it('trims items and drops blank items and rounds left empty', () => {
    const rounds: RoundInput[] = [
      {
        deliverableId: 'd1', receivedAt: '2026-11-08',
        items: [{ description: '  Đổi màu ', type: 'MINOR', billing: null }, { description: '   ', type: 'MAJOR', billing: 'CHARGEABLE' }],
      },
      emptyRound(null, '2026-11-08'),
    ]
    expect(cleanRounds(rounds)).toEqual([
      { deliverableId: 'd1', receivedAt: '2026-11-08', items: [{ description: 'Đổi màu', type: 'MINOR', billing: null }] },
    ])
  })
})

describe('errorText', () => {
  const err = (extra: Partial<ApiError>) => Object.assign(new Error('x'), { status: 400 }, extra) as ApiError

  it('explains conflicts, then field errors, then detail, then the fallback', () => {
    expect(errorText(err({ status: 409, code: 'CONCURRENCY_CONFLICT' }), 'fb')).toBe('Dữ liệu đã thay đổi ở tab khác — vui lòng tải lại')
    expect(errorText(err({ errors: { reason: ['Bắt buộc lý do'] } }), 'fb')).toBe('Bắt buộc lý do')
    expect(errorText(err({ detail: 'BR-41' }), 'fb')).toBe('BR-41')
    expect(errorText(err({}), 'fb')).toBe('fb')
  })
})

describe('quotaTone', () => {
  it('is amber with one free round left and red when none is left (docs/08 §1)', () => {
    expect(quotaTone(0, 3)).toBe('ok')
    expect(quotaTone(2, 3)).toBe('warn')
    expect(quotaTone(3, 3)).toBe('over')
    expect(quotaTone(4, 3)).toBe('over')
    expect(quotaTone(0, 0)).toBe('over')
  })
})
