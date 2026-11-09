import { draftsFromPreview, toStartTasks } from '@/features/admin/flow/startProject'

const preview = [
  { deliverableId: 'd1', name: 'Logo', serviceName: 'Thiết kế Logo', titles: ['Moodboard', 'Phác thảo', 'Hoàn thiện'], suggestedDueDate: '2026-12-01' },
  { deliverableId: 'd2', name: 'Danh thiếp', serviceName: null, titles: [], suggestedDueDate: null },
]

describe('draftsFromPreview', () => {
  it('ticks every template step', () => {
    const drafts = draftsFromPreview(preview)
    expect(drafts[0].titles).toEqual([
      { title: 'Moodboard', checked: true },
      { title: 'Phác thảo', checked: true },
      { title: 'Hoàn thiện', checked: true },
    ])
    expect(drafts[1]).toEqual({ deliverableId: 'd2', name: 'Danh thiếp', serviceName: null, titles: [], dueDate: '' })
  })

  it('seeds the shared due date from the suggestion', () => {
    expect(draftsFromPreview(preview)[0].dueDate).toBe('2026-12-01')
  })
})

describe('toStartTasks', () => {
  it('keeps ticked, trimmed, non-empty titles and drops deliverables left without one', () => {
    const drafts = draftsFromPreview(preview)
    drafts[0].titles[0].checked = false
    drafts[0].titles[1].title = '  Phác thảo 3 phương án  '
    drafts[0].titles[2].title = '   '

    expect(toStartTasks(drafts)).toEqual([{ deliverableId: 'd1', titles: ['Phác thảo 3 phương án'], dueDate: '2026-12-01' }])
  })

  it('sends dueDate null when the shared due date is cleared', () => {
    const drafts = draftsFromPreview(preview)
    drafts[0].dueDate = ''
    expect(toStartTasks(drafts)).toEqual([{ deliverableId: 'd1', titles: ['Moodboard', 'Phác thảo', 'Hoàn thiện'], dueDate: null }])
  })

  it('sends nothing when every step is unticked', () => {
    const drafts = draftsFromPreview(preview).map(d => ({ ...d, titles: d.titles.map(t => ({ ...t, checked: false })) }))
    expect(toStartTasks(drafts)).toEqual([])
  })
})
