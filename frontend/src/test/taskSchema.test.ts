import { dueBeforeStart, taskFormSchema } from '@/features/admin/tasks/taskUtils'

const base = { title: 'A', description: '', status: 'TODO', priority: 'MEDIUM', deliverableId: '' } as const

test('due before start is rejected on the due field', () => {
  const r = taskFormSchema.safeParse({ ...base, startDate: '2026-11-09', dueDate: '2026-11-08' })
  expect(r.success).toBe(false)
  expect(r.error!.issues[0]).toMatchObject({ path: ['dueDate'], message: 'Hạn hoàn thành phải từ ngày bắt đầu trở đi' })
})

test.each([['2026-11-08', '2026-11-08'], ['', '2026-11-01'], ['2026-11-01', '']])('accepts %s / %s', (s, d) => {
  expect(taskFormSchema.safeParse({ ...base, startDate: s, dueDate: d }).success).toBe(true)
})

test('dueBeforeStart needs both dates', () => {
  expect(dueBeforeStart('2026-11-09', '2026-11-08')).toBe(true)
  expect(dueBeforeStart('', '2026-11-08')).toBe(false)
})
