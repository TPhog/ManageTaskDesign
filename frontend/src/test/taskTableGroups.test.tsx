import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { TaskTable } from '@/features/admin/tasks/TaskTable'
import type { TaskListItem } from '@/lib/types'

const task = (id: string, groupKey: string): TaskListItem => ({
  id,
  projectId: 'p1',
  projectName: 'Dự án A',
  clientName: 'Khách A',
  deliverableId: null,
  deliverableName: null,
  title: `Việc ${id}`,
  status: 'TODO',
  priority: 'MEDIUM',
  startDate: null,
  dueDate: null,
  sortOrder: 1024,
  completedAt: null,
  isOverdue: false,
  checklistTotal: 0,
  checklistDone: 0,
  createdAt: '2026-11-01T00:00:00Z',
  updatedAt: '2026-11-01T00:00:00Z',
  hasDescription: false,
  notesCount: 0,
  isRecurring: false,
  linksCount: 0,
  blockedReason: null,
  blockedByTaskId: null,
  blockedByTitle: null,
  blockedByDone: false,
  groupKey,
})

describe('TaskTable groups', () => {
  it('inserts one header row per group with its label and total', () => {
    render(
      <MemoryRouter>
        <TaskTable
          tasks={[task('1', 'overdue'), task('2', 'overdue'), task('3', 'today')]}
          groups={[{ key: 'overdue', label: 'Quá hạn', count: 2 }, { key: 'today', label: 'Hôm nay', count: 1 }]}
          onOpen={() => {}}
        />
      </MemoryRouter>,
    )
    const headers = screen.getAllByRole('rowheader')
    expect(headers).toHaveLength(2)
    expect(headers[0]).toHaveTextContent('Quá hạn')
    expect(within(headers[0]).getByText('2')).toBeInTheDocument()
    expect(headers[1]).toHaveTextContent('Hôm nay')
  })

  it('renders no group rows without groups', () => {
    render(
      <MemoryRouter>
        <TaskTable tasks={[task('1', 'overdue')]} onOpen={() => {}} />
      </MemoryRouter>,
    )
    expect(screen.queryAllByRole('rowheader')).toHaveLength(0)
  })
})
