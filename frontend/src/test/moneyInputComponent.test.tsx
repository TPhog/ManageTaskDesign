import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MoneyInput } from '@/components/MoneyInput'

function Harness({ initial = 0, onValue }: { initial?: number; onValue?: (n: number) => void }) {
  const [value, setValue] = useState(initial)
  return (
    <MoneyInput
      aria-label="Giá"
      value={value}
      onChange={n => { setValue(n); onValue?.(n) }}
    />
  )
}

describe('MoneyInput typing', () => {
  it('shows the thousands dots after every keystroke', async () => {
    const onValue = vi.fn()
    render(<Harness onValue={onValue} />)
    const input = screen.getByLabelText('Giá')
    const seen: string[] = []
    await userEvent.click(input)
    for (const ch of '2000000') {
      await userEvent.keyboard(ch)
      seen.push((input as HTMLInputElement).value)
    }
    expect(seen).toEqual(['2', '20', '200', '2.000', '20.000', '200.000', '2.000.000'])
    expect(onValue).toHaveBeenLastCalledWith(2_000_000)
  })

  it('clearing the field gives 0', async () => {
    const onValue = vi.fn()
    render(<Harness initial={500_000} onValue={onValue} />)
    const input = screen.getByLabelText('Giá')
    await userEvent.clear(input)
    expect(onValue).toHaveBeenLastCalledWith(0)
    expect(input).toHaveValue('')
  })

  it('a pasted formatted amount is parsed', async () => {
    const onValue = vi.fn()
    render(<Harness onValue={onValue} />)
    await userEvent.click(screen.getByLabelText('Giá'))
    await userEvent.paste('2.500.000 ₫')
    expect(onValue).toHaveBeenLastCalledWith(2_500_000)
    expect(screen.getByLabelText('Giá')).toHaveValue('2.500.000')
  })

  it('keeps the caret on the digit being edited when a dot is inserted', async () => {
    render(<Harness initial={1000} />)
    const input = screen.getByLabelText('Giá') as HTMLInputElement
    await userEvent.click(input)
    input.setSelectionRange(1, 1) // after the "1" of "1.000"
    await userEvent.keyboard('2')
    expect(input).toHaveValue('12.000')
    expect(input.selectionStart).toBe(2)
  })
})
