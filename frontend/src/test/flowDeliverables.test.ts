import {
  actionHref,
  isDialogAction,
  isHttpUrl,
  openTaskSummary,
  projectDateRange,
  statusBadgeOverride,
  stepFromParam,
} from '@/features/admin/flow/flowUtils'

const none = { deliverableId: null, quotationId: null, requestId: null }

test('VIEW_TASKS links to the tasks tab and ADD_DELIVERABLE opens the create dialog', () => {
  expect(actionHref('VIEW_TASKS', 'p1', none)).toBe('/admin/projects/p1?tab=tasks')
  expect(actionHref('ADD_DELIVERABLE', 'p1', none)).toBe('/admin/projects/p1?tab=deliverables&new=1')
})

test('MARK_QUOTED opens a dialog, RECORD_DEPOSIT runs in place', () => {
  expect(isDialogAction('MARK_QUOTED')).toBe(true)
  expect(stepFromParam('MARK_QUOTED')).toBe('MARK_QUOTED')
  expect(isDialogAction('RECORD_DEPOSIT')).toBe(false)
  expect(actionHref('RECORD_DEPOSIT', 'p1', none)).toBeNull()
})

test.each([
  ['https://drive.google.com/x', true],
  ['http://a.b', true],
  ['', true],
  ['javascript:alert(1)', false],
  ['ftp://a.b', false],
  ['drive.google.com', false],
  ['https://a.b/c d', false],
  [`https://a.b/${'x'.repeat(500)}`, false],
])('isHttpUrl(%s)=%s', (v, ok) => expect(isHttpUrl(v)).toBe(ok))

test('project date range never shows a dangling dash', () => {
  expect(projectDateRange(null, '2026-11-25')).toBe('Deadline 25/11/2026')
  expect(projectDateRange('2026-11-01', '2026-11-25')).toBe('01/11/2026 → 25/11/2026')
  expect(projectDateRange('2026-11-01', null)).toBe('Bắt đầu 01/11/2026')
  expect(projectDateRange(null, null)).toBeNull()
})

test('accepted-but-not-started project is badged "Sẵn sàng bắt đầu"', () => {
  expect(statusBadgeOverride({ status: 'DRAFT' }, { stage: 'EXECUTE' }).label).toBe('Sẵn sàng bắt đầu')
  expect(statusBadgeOverride({ status: 'IN_PROGRESS' }, { stage: 'EXECUTE' }).label).toBeUndefined()
  expect(statusBadgeOverride({ status: 'DRAFT' }, { stage: 'PREPARE' }).label).toBeUndefined()
  expect(statusBadgeOverride({ status: 'DRAFT' }, undefined)).toEqual({ status: 'DRAFT' })
})

test('open task summary caps the list and ignores done tasks and other deliverables', () => {
  const tasks = Array.from({ length: 8 }, (_, i) => ({ title: `T${i}`, status: i === 6 ? 'DONE' : 'TODO', deliverableId: i === 7 ? 'other' : 'd1' }))
  expect(openTaskSummary(tasks, 'd1', 5)).toEqual({ total: 6, shown: ['T0', 'T1', 'T2', 'T3', 'T4'], more: 1 })
  expect(openTaskSummary(tasks, 'd1', 3)).toEqual({ total: 6, shown: ['T0', 'T1', 'T2'], more: 3 })
})
