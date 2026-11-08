import {
  DASHBOARD_FLOW_LIMIT, barPercent, dashboardLinks, monthLabel, projectsNeedingAction,
} from '@/features/admin/dashboard/dashboardUtils'
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
