import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TemplateFields } from '@/components/TemplateFields'
import { emptyDrafts, type AnswerDrafts } from '@/lib/requestTemplates'
import type { TemplateField } from '@/lib/requestTemplateTypes'

const fields: TemplateField[] = [
  { key: 'headline', label: 'Tiêu đề chính', type: 'SHORT_TEXT', required: true, help: 'Chữ lớn nhất', options: [] },
  { key: 'style', label: 'Phong cách', type: 'SINGLE_CHOICE', required: false, help: null, options: ['Hiện đại', 'Sang trọng'] },
  { key: 'size', label: 'Kích thước', type: 'SIZE', required: true, help: null, options: ['A3 (297 × 420 mm)'] },
]

function Harness({ errors }: { errors?: Record<string, string> }) {
  const [drafts, setDrafts] = useState<AnswerDrafts>(() => emptyDrafts(fields))
  const [files, setFiles] = useState<File[]>([])
  return (
    <>
      <TemplateFields idPrefix="t" fields={fields} drafts={drafts} errors={errors}
        onChange={(key, d) => setDrafts(cur => ({ ...cur, [key]: d }))} files={files} onFilesChange={setFiles} maxImages={3} />
      <output data-testid="drafts">{JSON.stringify(drafts)}</output>
    </>
  )
}

describe('TemplateFields', () => {
  it('renders each question with its help text and records answers', async () => {
    render(<Harness />)
    await userEvent.type(screen.getByRole('textbox', { name: /Tiêu đề chính/ }), 'Khai trương')
    await userEvent.click(screen.getByRole('radio', { name: 'Sang trọng' }))

    expect(screen.getByText('Chữ lớn nhất')).toBeInTheDocument()
    expect(screen.getByTestId('drafts').textContent).toContain('"headline":"Khai trương"')
    expect(screen.getByTestId('drafts').textContent).toContain('"style":"Sang trọng"')
  })

  it('offers a custom width × height with a unit', async () => {
    render(<Harness />)
    await userEvent.click(screen.getByRole('radio', { name: 'Kích thước tuỳ chỉnh' }))
    await userEvent.type(screen.getByRole('textbox', { name: 'Chiều rộng' }), '60')
    await userEvent.type(screen.getByRole('textbox', { name: 'Chiều cao' }), '160')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Đơn vị' }), 'cm')

    expect(screen.getByTestId('drafts').textContent).toContain('"size":{"mode":"custom","preset":"","width":"60","height":"160","unit":"cm"}')
  })

  it('shows the error under the question and marks the input invalid', () => {
    render(<Harness errors={{ headline: 'Bắt buộc' }} />)
    const input = screen.getByRole('textbox', { name: /Tiêu đề chính/ })
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(input).toHaveAccessibleDescription(expect.stringContaining('Bắt buộc'))
  })
})
