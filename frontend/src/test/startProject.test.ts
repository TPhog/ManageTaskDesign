import { draftsFromPreview, toStartTasks } from '@/features/admin/flow/startProject'

const preview = [
  { deliverableId: 'd1', name: 'Logo', serviceName: 'Thiết kế Logo', titles: ['Moodboard', 'Phác thảo', 'Hoàn thiện'] },
  { deliverableId: 'd2', name: 'Danh thiếp', serviceName: null, titles: [] },
]

describe('draftsFromPreview', () => {
  it('ticks every template step', () => {
    const drafts = draftsFromPreview(preview)
    expect(drafts[0].titles).toEqual([
      { title: 'Moodboard', checked: true },
      { title: 'Phác thảo', checked: true },
      { title: 'Hoàn thiện', checked: true },
    ])
    expect(drafts[1]).toEqual({ deliverableId: 'd2', name: 'Danh thiếp', serviceName: null, titles: [] })
  })
})

describe('toStartTasks', () => {
  it('keeps ticked, trimmed, non-empty titles and drops deliverables left without one', () => {
    const drafts = draftsFromPreview(preview)
    drafts[0].titles[0].checked = false
    drafts[0].titles[1].title = '  Phác thảo 3 phương án  '
    drafts[0].titles[2].title = '   '

    expect(toStartTasks(drafts)).toEqual([{ deliverableId: 'd1', titles: ['Phác thảo 3 phương án'] }])
  })

  it('sends nothing when every step is unticked', () => {
    const drafts = draftsFromPreview(preview).map(d => ({ ...d, titles: d.titles.map(t => ({ ...t, checked: false })) }))
    expect(toStartTasks(drafts)).toEqual([])
  })
})
