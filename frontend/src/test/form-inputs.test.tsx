import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DateInput } from '@/components/DateInput'
import { MoneyInput } from '@/components/MoneyInput'

function DateHarness({ initial = '' }: { initial?: string }) {
  const [value, setValue] = useState(initial)
  return (
    <>
      <label htmlFor="d">Ngày</label>
      <DateInput id="d" value={value} onChange={setValue} />
      <output data-testid="iso">{value}</output>
    </>
  )
}

function MoneyHarness() {
  const [value, setValue] = useState(500_000)
  return (
    <>
      <label htmlFor="m">Đơn giá</label>
      <MoneyInput id="m" value={value} onChange={setValue} />
      <output data-testid="money">{value}</output>
    </>
  )
}

describe('DateInput', () => {
  it('shows an ISO value day-first with its weekday', () => {
    render(<DateHarness initial="2026-11-08" />)
    expect(screen.getByLabelText('Ngày')).toHaveValue('08/11/2026')
    expect(screen.getByText('Chủ Nhật, 08/11/2026')).toBeInTheDocument()
  })

  it('turns typed d/m/yyyy into ISO and tidies it on blur', async () => {
    render(<DateHarness />)
    const input = screen.getByLabelText('Ngày')
    await userEvent.type(input, '8/11/2026')
    expect(screen.getByTestId('iso')).toHaveTextContent('2026-11-08')
    await userEvent.tab()
    expect(input).toHaveValue('08/11/2026')
  })

  it('flags an impossible date, keeps the old value and restores its text on blur', async () => {
    render(<DateHarness initial="2026-11-08" />)
    const input = screen.getByLabelText('Ngày')
    await userEvent.tripleClick(input)
    await userEvent.paste('31/11/2026')
    await userEvent.tab()
    expect(screen.getByTestId('iso')).toHaveTextContent('2026-11-08')
    expect(input).toHaveValue('08/11/2026')
    expect(screen.getByText('Ngày không hợp lệ — giữ ngày cũ')).toBeInTheDocument()
  })

  it('reports invalid text through onInvalid until it is fixed', async () => {
    const calls: boolean[] = []
    function H() {
      const [v, setV] = useState('')
      return <><label htmlFor="d2">Ngày</label><DateInput id="d2" value={v} onChange={setV} onInvalid={b => calls.push(b)} /></>
    }
    render(<H />)
    const input = screen.getByLabelText('Ngày')
    await userEvent.type(input, '31/1')
    expect(calls.at(-1)).toBe(true)
    await userEvent.type(input, '/2026')
    expect(calls.at(-1)).toBe(false)
  })
})

describe('MoneyInput', () => {
  it('shows thousands separators until focused', async () => {
    render(<MoneyHarness />)
    const input = screen.getByLabelText('Đơn giá')
    expect(input).toHaveValue('500.000')
    await userEvent.click(input)
    expect(input).toHaveValue('500000')
    await userEvent.clear(input)
    await userEvent.type(input, '2000000')
    expect(screen.getByTestId('money')).toHaveTextContent('2000000')
    await userEvent.tab()
    expect(input).toHaveValue('2.000.000')
  })
})
