import { copyWhenReady } from '@/lib/clipboard'

afterEach(() => { vi.unstubAllGlobals() })

test('uses ClipboardItem with the pending promise when supported', async () => {
  const write = vi.fn().mockResolvedValue(undefined)
  vi.stubGlobal('ClipboardItem', class { items: unknown; constructor(items: unknown) { this.items = items } })
  vi.stubGlobal('navigator', { clipboard: { write, writeText: vi.fn() } })
  const r = await copyWhenReady(Promise.resolve('xin chào'))
  expect(write).toHaveBeenCalledTimes(1)
  expect(r).toEqual({ text: 'xin chào', copied: true })
})

test('falls back to writeText without ClipboardItem', async () => {
  const writeText = vi.fn().mockResolvedValue(undefined)
  vi.stubGlobal('ClipboardItem', undefined)
  vi.stubGlobal('navigator', { clipboard: { writeText } })
  const r = await copyWhenReady(Promise.resolve('abc'))
  expect(writeText).toHaveBeenCalledWith('abc')
  expect(r).toEqual({ text: 'abc', copied: true })
})

test('null text copies nothing, a clipboard failure still returns the text', async () => {
  vi.stubGlobal('ClipboardItem', undefined)
  vi.stubGlobal('navigator', { clipboard: { writeText: vi.fn().mockRejectedValue(new Error('denied')) } })
  expect(await copyWhenReady(Promise.resolve(null))).toEqual({ text: null, copied: false })
  expect(await copyWhenReady(Promise.resolve('x'))).toEqual({ text: 'x', copied: false })
})
