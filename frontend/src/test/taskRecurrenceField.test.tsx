import { render, screen } from '@testing-library/react'
import { TaskRecurrenceField } from '@/features/admin/tasks/TaskRecurrenceField'

describe('TaskRecurrenceField', () => {
  it('previews the next occurrence counted from the due date', () => {
    render(<TaskRecurrenceField value={{ kind: 'DAILY', weekdays: 0, monthDay: null, until: null }} onChange={() => {}} baseDate="2099-01-10" />)
    expect(screen.getByText('Lần tiếp theo: 11/01/2099')).toBeInTheDocument()
  })

  it('flags an end date before the due date and hides the preview', () => {
    render(<TaskRecurrenceField value={{ kind: 'DAILY', weekdays: 0, monthDay: null, until: '2099-01-01' }} onChange={() => {}} baseDate="2099-01-10" />)
    expect(screen.getByRole('alert')).toHaveTextContent('Ngày kết thúc phải từ 10/01/2099 trở đi')
    expect(screen.queryByText(/Lần tiếp theo/)).toBeNull()
  })
})
