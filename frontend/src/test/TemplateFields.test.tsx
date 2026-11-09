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
    await userEvent.click(screen.getByRole('radio', { name: 'Tuỳ chỉnh (rộng × cao)' }))
    await userEvent.type(screen.getByRole('textbox', { name: 'Chiều rộng' }), '60')
    await userEvent.type(screen.getByRole('textbox', { name: 'Chiều cao' }), '160')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Đơn vị' }), 'cm')

    expect(screen.getByTestId('drafts').textContent).toContain('"size":{"mode":"custom","preset":"","width":"60","height":"160","unit":"cm"}')
  })

  it('gives every question a focusable element with the question id, choice groups included', () => {
    render(<Harness errors={{ size: 'Bắt buộc' }} />)
    for (const key of ['headline', 'style', 'size']) {
      const el = document.getElementById(`t-${key}`)
      expect(el, key).not.toBeNull()
      el!.focus()
      expect(document.activeElement).toBe(el)
    }
    expect(document.getElementById('t-size')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByRole('group', { name: /Kích thước/ })).toHaveAccessibleDescription(expect.stringContaining('Bắt buộc'))
  })

  it('shows the error under the question and marks the input invalid', () => {
    render(<Harness errors={{ headline: 'Bắt buộc' }} />)
    const input = screen.getByRole('textbox', { name: /Tiêu đề chính/ })
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(input).toHaveAccessibleDescription(expect.stringContaining('Bắt buộc'))
  })

  it('keeps the photo remove button a 44px target and tints radios and checkboxes with the theme token', async () => {
    const extra: TemplateField[] = [
      { key: 'tags', label: 'Gam màu', type: 'MULTI_CHOICE', required: false, help: null, options: ['Ấm', 'Lạnh'] },
      { key: 'refs', label: 'Ảnh mẫu', type: 'IMAGES', required: false, help: null, options: [] },
    ]
    const photo = new File(['x'], 'mau.png', { type: 'image/png' })
    const onFilesChange = vi.fn()
    render(<TemplateFields idPrefix="x" fields={[...fields, ...extra]} drafts={emptyDrafts([...fields, ...extra])}
      onChange={() => {}} files={[photo]} onFilesChange={onFilesChange} maxImages={3} />)

    const remove = screen.getByRole('button', { name: 'Bỏ mau.png' })
    expect(remove).toHaveClass('min-h-11', 'min-w-11')
    for (const input of [screen.getByRole('radio', { name: 'Sang trọng' }), screen.getByRole('checkbox', { name: 'Ấm' })])
      expect(input).toHaveClass('accent-primary')
    await userEvent.click(remove)
    expect(onFilesChange).toHaveBeenCalledWith([])
  })
})
