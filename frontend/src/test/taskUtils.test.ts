import {
  addDays,
  compareForList,
  deleteWarnings,
  dueInfo,
  planMove,
  sortOrderBetween,
  todayVn,
  type BoardTask,
} from '@/features/admin/tasks/taskUtils'

describe('todayVn', () => {
  it('uses the Vietnam calendar day, not UTC', () => {
    expect(todayVn(new Date('2026-11-08T17:30:00Z'))).toBe('2026-11-09') // 00:30 in Hanoi
    expect(todayVn(new Date('2026-11-08T16:59:00Z'))).toBe('2026-11-08') // 23:59 in Hanoi
  })
})

describe('addDays', () => {
  it('crosses month and year boundaries', () => {
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01')
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01')
    expect(addDays('2026-11-08', -1)).toBe('2026-11-07')
  })
})

describe('dueInfo', () => {
  const today = '2026-11-08'

  it('handles tasks without a due date', () => {
    expect(dueInfo(null, 'TODO', today)).toEqual({ label: 'Chưa có hạn', tone: 'none' })
  })

  it('marks past due dates as overdue unless the task is done (BR-81)', () => {
    expect(dueInfo('2026-11-07', 'TODO', today)).toEqual({ label: 'Quá hạn · 07/11', tone: 'overdue' })
    expect(dueInfo('2026-11-07', 'DONE', today)).toEqual({ label: '07/11', tone: 'normal' })
  })

  it('labels today and tomorrow', () => {
    expect(dueInfo('2026-11-08', 'IN_PROGRESS', today)).toEqual({ label: 'Hôm nay', tone: 'today' })
    expect(dueInfo('2026-11-09', 'TODO', today)).toEqual({ label: 'Ngày mai', tone: 'soon' })
    expect(dueInfo('2027-01-01', 'TODO', '2026-12-31')).toEqual({ label: 'Ngày mai', tone: 'soon' })
  })

  it('shows dd/MM in the current year and dd/MM/yyyy otherwise', () => {
    expect(dueInfo('2026-11-20', 'TODO', today)).toEqual({ label: '20/11', tone: 'normal' })
    expect(dueInfo('2027-01-05', 'TODO', today)).toEqual({ label: '05/01/2027', tone: 'normal' })
  })
})

describe('sortOrderBetween', () => {
  it('takes the midpoint between neighbours', () => {
    expect(sortOrderBetween(1024, 2048)).toBe(1536)
  })

  it('steps past the edge when one neighbour is missing', () => {
    expect(sortOrderBetween(undefined, 1024)).toBe(0)
    expect(sortOrderBetween(2048, undefined)).toBe(3072)
    expect(sortOrderBetween(undefined, undefined)).toBe(1024)
  })
})

describe('planMove', () => {
  const tasks: BoardTask[] = [
    { id: 'a', status: 'TODO', sortOrder: 1024 },
    { id: 'b', status: 'TODO', sortOrder: 2048 },
    { id: 'c', status: 'TODO', sortOrder: 3072 },
    { id: 'd', status: 'IN_PROGRESS', sortOrder: 1024 },
  ]

  it('dragging down in the same column lands below the hovered card', () => {
    expect(planMove(tasks, 'a', 'b')).toEqual({ status: 'TODO', sortOrder: 2560 })
    expect(planMove(tasks, 'a', 'c')).toEqual({ status: 'TODO', sortOrder: 4096 })
  })

  it('dragging up in the same column lands above the hovered card', () => {
    expect(planMove(tasks, 'c', 'a')).toEqual({ status: 'TODO', sortOrder: 0 })
  })

  it('dropping on a card of another column lands above that card', () => {
    expect(planMove(tasks, 'd', 'b')).toEqual({ status: 'TODO', sortOrder: 1536 })
  })

  it('dropping on a column appends to its bottom', () => {
    expect(planMove(tasks, 'a', 'column:IN_PROGRESS')).toEqual({ status: 'IN_PROGRESS', sortOrder: 2048 })
    expect(planMove(tasks, 'a', 'column:DONE')).toEqual({ status: 'DONE', sortOrder: 1024 })
  })

  it('returns null when nothing changes', () => {
    expect(planMove(tasks, 'a', 'a')).toBeNull()
    expect(planMove(tasks, 'a', 'column:TODO')).toBeNull()
    expect(planMove(tasks, 'missing', 'b')).toBeNull()
  })
})

describe('compareForList', () => {
  it('orders by board column, then position in the column', () => {
    const rows: BoardTask[] = [
      { id: 'c', status: 'TODO', sortOrder: 3072 },
      { id: 'd', status: 'IN_PROGRESS', sortOrder: 1024 },
      { id: 'a', status: 'TODO', sortOrder: 1024 },
    ]
    expect(rows.sort(compareForList).map(r => r.id)).toEqual(['a', 'c', 'd'])
  })
})

describe('deleteWarnings (R2 M8)', () => {
  const rule = { kind: 'WEEKLY', weekdays: 1, monthDay: null, until: null } as const
  const plain = { recurrence: null, nextOccurrence: null, blockingCount: 0 }

  it('says nothing extra for a plain task without images', () => {
    expect(deleteWarnings(plain, 0)).toEqual([])
  })

  it('warns that deleting the current occurrence of a recurring task ends the series', () => {
    expect(deleteWarnings({ ...plain, recurrence: rule }, 0)).toEqual(['Đây là việc lặp lại: xoá sẽ dừng chuỗi, không tạo lần tiếp theo.'])
    // already spawned its next copy: the series goes on without it
    expect(deleteWarnings({ ...plain, recurrence: rule, nextOccurrence: { id: 't2', dueDate: null } }, 0)).toEqual([])
  })

  it('lists images and the tasks it blocks', () => {
    expect(deleteWarnings({ ...plain, blockingCount: 2 }, 3)).toEqual([
      '3 ảnh của công việc cũng bị xoá vĩnh viễn (trừ ảnh là chứng từ báo giá).',
      '2 công việc đang bị chặn bởi việc này vẫn ở trạng thái “Bị chặn” — nhớ bỏ chặn khi cần.',
    ])
  })
})
