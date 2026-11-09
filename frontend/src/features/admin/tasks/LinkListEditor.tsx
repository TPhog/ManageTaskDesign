import { useId, useState, type KeyboardEvent } from 'react'
import { Link2, Plus, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { ExternalLink } from '@/lib/types'

const MAX_LINKS = 10
const MAX_TITLE = 100
const MAX_URL = 2000

/** A trimmed http(s) URL, or the error to show (the API applies the same rule: absolute http/https, no spaces). */
function checkUrl(raw: string): { url: string } | { error: string } {
  const url = raw.trim()
  if (!url) return { error: 'Vui lòng nhập địa chỉ liên kết' }
  if (url.length > MAX_URL) return { error: `Tối đa ${MAX_URL} ký tự` }
  if (/\s/.test(url)) return { error: 'Địa chỉ liên kết không được có khoảng trắng' }
  try {
    const parsed = new URL(url)
    if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') return { error: 'Chỉ chấp nhận địa chỉ http hoặc https' }
    return { url }
  } catch {
    return { error: 'Chỉ chấp nhận địa chỉ http hoặc https' }
  }
}

/** Only http(s) links are ever rendered as anchors — defensive, the API already refuses anything else. */
const isSafeUrl = (url: string) => /^https?:\/\//i.test(url)

interface Props {
  value: ExternalLink[]
  onChange: (links: ExternalLink[]) => void
  /** Deliverables: each link can be shown to the client on the portal */
  allowClientVisible?: boolean
  disabled?: boolean
}

/** External links (Figma, Drive…): list with remove buttons and an add row validated inline. */
export function LinkListEditor({ value, onChange, allowClientVisible = false, disabled = false }: Props) {
  const [title, setTitle] = useState('')
  const [url, setUrl] = useState('')
  const [error, setError] = useState<string | null>(null)
  const errorId = useId()
  const full = value.length >= MAX_LINKS

  function add() {
    const checked = checkUrl(url)
    if ('error' in checked) { setError(checked.error); return }
    setError(null)
    // an empty title becomes the host, as the server would do
    const name = title.trim() || new URL(checked.url).host
    onChange([...value, { title: name.slice(0, MAX_TITLE), url: checked.url, visibleToClient: false }])
    setTitle('')
    setUrl('')
  }

  // Enter adds the link instead of submitting the surrounding form (the task drawer)
  const onEnter = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') { e.preventDefault(); if (!full) add() }
  }

  return (
    <div className="space-y-3">
      {value.length > 0 && (
        <ul className="m-0 list-none space-y-2 p-0">
          {value.map((l, i) => (
            <li key={`${l.url}-${i}`} className="flex flex-wrap items-center gap-x-3 gap-y-1">
              <LinkChip title={l.title} url={l.url} />
              {allowClientVisible && (
                <label className="inline-flex min-h-8 cursor-pointer items-center gap-1.5 text-xs text-muted-foreground">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-primary"
                    aria-label={`Hiện cho khách: ${l.title}`}
                    checked={l.visibleToClient}
                    disabled={disabled}
                    onChange={e => onChange(value.map((x, j) => (j === i ? { ...x, visibleToClient: e.target.checked } : x)))}
                  />
                  Khách xem được
                </label>
              )}
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="ml-auto h-8 w-8 text-muted-foreground hover:text-destructive"
                aria-label={`Xoá liên kết ${l.title}`}
                disabled={disabled}
                onClick={() => onChange(value.filter((_, j) => j !== i))}
              >
                <X aria-hidden="true" />
              </Button>
            </li>
          ))}
        </ul>
      )}
      {allowClientVisible && value.length > 0 && (
        <p className="text-xs text-muted-foreground">Liên kết được đánh dấu “Khách xem được”: khách sẽ thấy liên kết này trong cổng khách hàng.</p>
      )}

      <div className="grid gap-2 sm:grid-cols-[minmax(0,10rem)_minmax(0,1fr)_auto]">
        <Input
          aria-label="Tên liên kết"
          placeholder="Tên (tuỳ chọn)"
          value={title}
          maxLength={MAX_TITLE}
          disabled={disabled || full}
          onChange={e => setTitle(e.target.value)}
          onKeyDown={onEnter}
        />
        <Input
          aria-label="Địa chỉ liên kết"
          placeholder="https://…"
          inputMode="url"
          value={url}
          disabled={disabled || full}
          aria-invalid={!!error}
          aria-describedby={error ? errorId : undefined}
          onChange={e => { setUrl(e.target.value); if (error) setError(null) }}
          onKeyDown={onEnter}
        />
        <Button type="button" variant="outline" disabled={disabled || full} onClick={add}>
          <Plus aria-hidden="true" />
          Thêm liên kết
        </Button>
      </div>
      {error && <p id={errorId} role="alert" className="text-sm text-destructive">{error}</p>}
      {full && <p className="text-xs text-muted-foreground">Tối đa {MAX_LINKS} liên kết.</p>}
    </div>
  )
}

function LinkChip({ title, url }: { title: string; url: string }) {
  if (!isSafeUrl(url)) return <span className="min-w-0 break-words text-xs text-muted-foreground">{title}</span>
  return (
    <a
      href={url}
      target="_blank"
      rel="noopener noreferrer"
      title={url}
      className="inline-flex min-h-8 min-w-0 max-w-full items-center gap-1 rounded-full border bg-card px-2.5 text-xs font-medium text-foreground transition-colors duration-150 hover:bg-surface-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <Link2 className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
      <span className="min-w-0 break-words [overflow-wrap:anywhere]">{title}</span>
    </a>
  )
}

/** Read-only link chips; nothing when the list is empty. */
export function LinkChips({ links }: { links: Pick<ExternalLink, 'title' | 'url'>[] }) {
  if (links.length === 0) return null
  return (
    <ul className="m-0 flex list-none flex-wrap gap-2 p-0">
      {links.map((l, i) => <li key={`${l.url}-${i}`} className="min-w-0 max-w-full"><LinkChip title={l.title} url={l.url} /></li>)}
    </ul>
  )
}
