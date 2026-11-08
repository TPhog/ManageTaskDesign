import { liveQuoteRefs, quoteRefLabel } from '@/lib/quoteRefs'
import type { DeliverableQuoteRef } from '@/lib/types'

const ref = (p: Partial<DeliverableQuoteRef> = {}): DeliverableQuoteRef => ({
  quotationId: 'q1', number: 'BG-2026-0028', version: 1, status: 'SENT', kind: 'INITIAL', ...p,
})

describe('quoteRefLabel', () => {
  it('names a sent quotation by number and status', () => expect(quoteRefLabel(ref())).toBe('BG-2026-0028 · đã gửi'))
  it('adds the version from v2', () =>
    expect(quoteRefLabel(ref({ version: 2, status: 'ACCEPTED' }))).toBe('BG-2026-0028 v2 · đã chấp nhận'))
  it('calls a numberless draft a draft', () => expect(quoteRefLabel(ref({ number: null, status: 'DRAFT' }))).toBe('Báo giá nháp'))
})

describe('liveQuoteRefs', () => {
  it('keeps only sent and accepted quotations', () =>
    expect(liveQuoteRefs([ref({ quotationId: 'd', status: 'DRAFT' }), ref({ quotationId: 's' })]).map(r => r.quotationId)).toEqual(['s']))
  it('accepts a missing list', () => expect(liveQuoteRefs(undefined)).toEqual([]))
})
