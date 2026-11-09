/**
 * Copies text that is only known after an async step (e.g. a POST that creates a portal link).
 * Where supported, `ClipboardItem` takes the promise itself so the write starts inside the click and keeps the user
 * gesture (Safari/Firefox); otherwise it waits for the text and uses writeText. `text` is null when producing it failed.
 */
export async function copyWhenReady(textPromise: Promise<string | null>): Promise<{ text: string | null; copied: boolean }> {
  const settled = textPromise.catch(() => null)
  if (typeof ClipboardItem !== 'undefined' && navigator.clipboard?.write) {
    try {
      const blob = settled.then(t => {
        if (t === null) throw new Error('nothing to copy')
        return new Blob([t], { type: 'text/plain' })
      })
      await navigator.clipboard.write([new ClipboardItem({ 'text/plain': blob })])
      return { text: await settled, copied: true }
    } catch {
      // fall back below
    }
  }
  const text = await settled
  if (text === null) return { text: null, copied: false }
  try {
    await navigator.clipboard.writeText(text)
    return { text, copied: true }
  } catch {
    return { text, copied: false }
  }
}
