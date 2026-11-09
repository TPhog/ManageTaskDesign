import { compactVnd, greetingFor, isSetupVisible, weekdayLong, weekdayShort, weekSummary } from '@/features/admin/dashboard/dashboardUtils'
import {
  DASHBOARD_FLOW_LIMIT, barPercent, dashboardLinks, dayHeading, monthLabel, projectsNeedingAction, removeTaskFromSchedule,
  restoreTaskInSchedule, scheduleIsEmpty, deadlineLabel, deltaLabel, receivableReminder,
} from '@/features/admin/dashboard/dashboardUtils'
import type { DashboardSchedule, ReceivableItem, ScheduleTask } from '@/lib/dashboardTypes'
import type { FlowAction, FlowActionKind, FlowProjectActions, FlowWarning } from '@/lib/flowTypes'

const act = (kind: FlowActionKind, primary = false): FlowAction =>
  ({ kind, label: kind, primary, target: { deliverableId: null, quotationId: null, requestId: null } })
const warn = (severity: FlowWarning['severity']): FlowWarning => ({ kind: 'BRIEF_NOT_CONFIRMED', message: 'x', severity })
const project = (id: string, actions: FlowAction[], warnings: FlowWarning[] = []): FlowProjectActions =>
  ({ projectId: id, projectName: id, clientName: 'Khách', stage: 'PREPARE', actions, warnings })

describe('monthLabel', () => {
  it('formats yyyy-MM as T{month}/{year}', () => {
    expect(monthLabel('2026-06')).toBe('T6/2026')
    expect(monthLabel('2030-12')).toBe('T12/2030')
  })
})

describe('barPercent', () => {
  it('scales against the largest month', () => {
    expect(barPercent(50, 100)).toBe(50)
    expect(barPercent(100, 100)).toBe(100)
  })

  it('keeps a small but non-zero month visible', () => {
    expect(barPercent(1, 1000)).toBe(4)
  })

  it('draws nothing for zero or an empty chart', () => {
    expect(barPercent(0, 100)).toBe(0)
    expect(barPercent(5, 0)).toBe(0)
  })
})

describe('dashboardLinks', () => {
  it('points every counter at an existing filtered list', () => {
    expect(dashboardLinks.overdueTasks).toBe('/admin/tasks?due=overdue')
    expect(dashboardLinks.dueTodayTasks).toBe('/admin/tasks?due=today')
    expect(dashboardLinks.awaitingQuotations).toBe('/admin/quotations?status=SENT')
    expect(dashboardLinks.inProgress).toBe('/admin/projects?status=IN_PROGRESS')
    expect(dashboardLinks.outstanding).toBe('/admin/quotations?status=ACCEPTED')
  })
})

describe('projectsNeedingAction', () => {
  it('keeps projects with an action or a real warning, in server order', () => {
    const { items } = projectsNeedingAction([
      project('a', [act('ADD_DELIVERABLE', true)]),
      project('b', [], [warn('info')]),           // only an info chip: nothing to do now
      project('c', [], [warn('warning')]),        // e.g. waiting for the client but money is due
      project('d', []),
    ], DASHBOARD_FLOW_LIMIT)
    expect(items.map(p => p.projectId)).toEqual(['a', 'c'])
  })

  it('puts the primary action first', () => {
    const { items } = projectsNeedingAction([project('a', [act('CONFIRM_BRIEF'), act('ADD_DELIVERABLE', true)])], 8)
    expect(items[0].actions.map(a => a.kind)).toEqual(['ADD_DELIVERABLE', 'CONFIRM_BRIEF'])
  })

  it('caps the list and reports how many are hidden', () => {
    const many = Array.from({ length: 10 }, (_, i) => project(`p${i}`, [act('CREATE_QUOTATION', true)]))
    expect(projectsNeedingAction(many, 8)).toMatchObject({ hidden: 2 })
    expect(projectsNeedingAction(many, 8).items).toHaveLength(8)
    expect(projectsNeedingAction(many, Infinity)).toMatchObject({ hidden: 0 })
  })
})

const task = (id: string, dueDate: string): ScheduleTask => ({
  id, title: `Task ${id}`, status: 'TODO', priority: 'MEDIUM', dueDate, sortOrder: 1024,
  projectId: 'p1', projectName: 'Logo ABC', clientName: 'Khách A',
})
const day = (date: string, tasks: ScheduleTask[] = []) => ({ date, tasks, hiddenTasks: 0, deadlines: [] })
const schedule = (): DashboardSchedule => ({
  today: '2030-05-15', undatedOpenTasks: 0,
  overdue: { total: 2, items: [task('a', '2030-05-10'), task('b', '2030-05-14')] },
  days: [day('2030-05-15', [task('c', '2030-05-15')]), day('2030-05-16'), day('2030-05-17'), day('2030-05-18'), day('2030-05-19'), day('2030-05-20'), day('2030-05-21')],
})

describe('dayHeading', () => {
  it('names today and tomorrow, otherwise the weekday', () => {
    expect(dayHeading('2030-05-15', '2030-05-15')).toBe('Hôm nay · 15/05')
    expect(dayHeading('2030-05-16', '2030-05-15')).toBe('Ngày mai · 16/05')
    expect(dayHeading('2030-05-17', '2030-05-15')).toBe('T6 · 17/05')
  })
  it('rolls tomorrow over a month end', () => {
    expect(dayHeading('2030-06-01', '2030-05-31')).toBe('Ngày mai · 01/06')
  })
})

describe('removeTaskFromSchedule', () => {
  it('drops the task from the overdue bucket and decrements its total', () => {
    const next = removeTaskFromSchedule(schedule(), 'a')
    expect(next.overdue.items.map(t => t.id)).toEqual(['b'])
    expect(next.overdue.total).toBe(1)
  })
  it('drops the task from its day without touching other days', () => {
    const next = removeTaskFromSchedule(schedule(), 'c')
    expect(next.days[0].tasks).toEqual([])
    expect(next.days).toHaveLength(7)
  })
  it('returns an equal schedule for an unknown id', () => {
    expect(removeTaskFromSchedule(schedule(), 'zzz')).toEqual(schedule())
  })
})

describe('scheduleIsEmpty', () => {
  it('is false while anything is scheduled and true when nothing is', () => {
    expect(scheduleIsEmpty(schedule())).toBe(false)
    const empty: DashboardSchedule = { ...schedule(), overdue: { total: 0, items: [] }, days: schedule().days.map(d => ({ ...d, tasks: [] })) }
    expect(scheduleIsEmpty(empty)).toBe(true)
  })
})

describe('deadlineLabel', () => {
  it.each([[null, 'Chưa có hạn'], [-3, 'Trễ 3 ngày'], [0, 'Hôm nay'], [1, 'Ngày mai'], [5, 'Còn 5 ngày']])('%s -> %s', (days, label) =>
    expect(deadlineLabel(days)).toBe(label))
})

describe('deltaLabel', () => {
  it('formats sign and one decimal in Vietnamese and explains a missing base', () => {
    expect(deltaLabel(50)).toBe('+50,0% so với cùng kỳ trước')
    expect(deltaLabel(-66.7)).toBe('−66,7% so với cùng kỳ trước')
    expect(deltaLabel(null)).toBe('Chưa có kỳ trước để so sánh')
  })
})

describe('receivableReminder', () => {
  it('asks for the deposit while nothing is paid and for the remainder after the deposit', () => {
    const base = { clientName: 'Chị Lan', projectName: 'Logo ABC', number: 'BG-2030-0003', version: 1, depositPercent: 50, depositAmount: 5_000_000, outstanding: 10_000_000 }
    expect(receivableReminder({ ...base, paymentStatus: 'UNPAID' } as ReceivableItem)).toContain('tiền cọc')
    expect(receivableReminder({ ...base, paymentStatus: 'DEPOSIT_PAID', outstanding: 5_000_000 } as ReceivableItem)).toContain('số tiền còn lại')
  })
})

describe('restoreTaskInSchedule', () => {
  it('puts a removed overdue task back and counts it again', () => {
    const removed = removeTaskFromSchedule(schedule(), 'a')
    const back = restoreTaskInSchedule(removed, task('a', '2030-05-10'))
    expect(back.overdue.items.map(t => t.id)).toEqual(['a', 'b'])
    expect(back.overdue.total).toBe(2)
  })
  it('puts a removed day task back on its day, only once', () => {
    const removed = removeTaskFromSchedule(schedule(), 'c')
    const back = restoreTaskInSchedule(removed, task('c', '2030-05-15'))
    expect(back.days[0].tasks.map(t => t.id)).toEqual(['c'])
    expect(restoreTaskInSchedule(back, task('c', '2030-05-15'))).toBe(back)
  })
})

describe('dashboard header helpers (P16)', () => {
  it('greets by the hour', () => {
    expect(greetingFor(7)).toBe('Chào buổi sáng')
    expect(greetingFor(12)).toBe('Chào buổi trưa')
    expect(greetingFor(15)).toBe('Chào buổi chiều')
    expect(greetingFor(21)).toBe('Chào buổi tối')
    expect(greetingFor(2)).toBe('Chào buổi tối')
  })
  it('spells the server VN date with its weekday', () => {
    expect(weekdayLong('2026-11-09')).toBe('Thứ Hai, 09/11/2026')
    expect(weekdayLong('2026-11-15')).toBe('Chủ nhật, 15/11/2026')
  })
  it('names the short weekday of a calendar date', () => {
    expect(weekdayShort('2026-11-09')).toBe('T2')
    expect(weekdayShort('2026-11-15')).toBe('CN')
  })
  it('summarises the week', () => {
    const day = (tasks: number, hiddenTasks = 0) => ({ date: '2026-11-09', tasks: Array.from({ length: tasks }, () => ({}) as never), hiddenTasks, deadlines: [] })
    const base = { today: '2026-11-09', undatedOpenTasks: 0, overdue: { total: 0, items: [] } }
    expect(weekSummary({ ...base, days: [day(0)] })).toBe('Tuần này còn trống.')
    expect(weekSummary({ ...base, days: [day(2, 1), day(1)] })).toBe('4 việc trong 7 ngày tới.')
    expect(weekSummary({ ...base, overdue: { total: 2, items: [] }, days: [day(1)] })).toBe('1 việc trong 7 ngày tới, 2 việc quá hạn.')
  })
  it('shows the setup card only while it is neither dismissed nor complete', () => {
    expect(isSetupVisible(undefined)).toBe(false)
    expect(isSetupVisible({ dismissed: false, complete: false, doneCount: 1, total: 6, items: [] })).toBe(true)
    expect(isSetupVisible({ dismissed: true, complete: false, doneCount: 1, total: 6, items: [] })).toBe(false)
    expect(isSetupVisible({ dismissed: false, complete: true, doneCount: 6, total: 6, items: [] })).toBe(false)
  })
  it('shortens VND for chart labels', () => {
    expect(compactVnd(0)).toBe('0')
    expect(compactVnd(850_000)).toBe('850 N')
    expect(compactVnd(52_000_000)).toBe('52 tr')
    expect(compactVnd(38_450_000)).toBe('38,5 tr')
    expect(compactVnd(1_250_000_000)).toBe('1,3 tỷ')
  })
})
