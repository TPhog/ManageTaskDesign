import { render, screen, within } from '@testing-library/react'
import { TemplateAnswersView } from '@/components/TemplateAnswersView'

describe('TemplateAnswersView', () => {
  it('lists each answered question with its label, then the note', () => {
    render(<TemplateAnswersView title="Poster" note="Gửi trước thứ 6" answers={[
      { key: 'size', label: 'Kích thước', type: 'SIZE', display: 'A3 (297 × 420 mm)' },
      { key: 'details', label: 'Nội dung chi tiết', type: 'LONG_TEXT', display: 'Giảm 20%\nTặng ly' },
    ]} />)

    const region = screen.getByRole('region', { name: 'Câu trả lời mẫu Poster' })
    expect(within(region).getByText('Kích thước')).toBeInTheDocument()
    expect(within(region).getByText('A3 (297 × 420 mm)')).toBeInTheDocument()
    expect(within(region).getByText(/Giảm 20%/)).toHaveClass('whitespace-pre-wrap')
    expect(within(region).getByText('Gửi trước thứ 6')).toBeInTheDocument()
  })

  it('renders nothing without answers or note', () => {
    const { container } = render(<TemplateAnswersView answers={[]} />)
    expect(container).toBeEmptyDOMElement()
  })
})
