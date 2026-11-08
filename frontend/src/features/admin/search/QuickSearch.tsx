import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { FileText, FolderKanban, Search, Users } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Command, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog'
import { MoneyText } from '@/components/MoneyText'
import { StatusBadge } from '@/components/StatusBadge'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { SearchResult } from '@/lib/searchTypes'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import {
  canSearch, clientHref, projectHref, quotationHref, quotationLabel, resultCount, searchTerm, shortcutLabel,
} from './searchUtils'

const messageClass = 'px-4 py-6 text-center text-sm text-muted-foreground'

/** Sidebar entry point of the docs/08 §2.1 header search. */
export function QuickSearchTrigger({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-keyshortcuts="Control+K Meta+K"
      className="flex h-10 w-full items-center gap-2 rounded-lg border bg-background px-3 text-sm text-muted-foreground transition-colors duration-150 hover:border-primary/40 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <Search className="h-4 w-4 shrink-0" aria-hidden="true" />
      <span className="flex-1 text-left">Tìm kiếm nhanh…</span>
      <kbd className="rounded border bg-muted px-1.5 py-0.5 font-mono text-[11px]">{shortcutLabel(navigator.platform)}</kbd>
    </button>
  )
}

/**
 * Command palette over GET /api/admin/search (clients · projects · quotation numbers). The server filters and ranks,
 * so cmdk's own filter is off; arrows move, Enter opens, Esc closes. Ctrl+K / ⌘+K toggles it from anywhere in admin.
 */
export function QuickSearch({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const navigate = useNavigate()
  const [text, setText] = useState('')
  const term = useDebouncedValue(searchTerm(text))
  const ready = canSearch(term)

  const { data, isError, refetch } = useQuery({
    queryKey: qk.search(term),
    queryFn: () => api.get<SearchResult>(`/admin/search?q=${encodeURIComponent(term)}`),
    enabled: open && ready,
  })

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        if (open) setText('')
        onOpenChange(!open)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [open, onOpenChange])

  function change(next: boolean) {
    if (!next) setText('')
    onOpenChange(next)
  }

  function go(to: string) {
    change(false)
    navigate(to)
  }

  const results = ready ? data : undefined

  return (
    <Dialog open={open} onOpenChange={change}>
      <DialogContent className="gap-0 overflow-hidden p-0 sm:max-w-xl">
        <DialogTitle className="sr-only">Tìm kiếm nhanh</DialogTitle>
        <DialogDescription className="sr-only">
          Tìm khách hàng, dự án hoặc số báo giá. Dùng phím mũi tên để chọn, Enter để mở.
        </DialogDescription>
        <Command shouldFilter={false} label="Tìm kiếm nhanh">
          <CommandInput value={text} onValueChange={setText} placeholder="Tìm khách, dự án, số báo giá…" />
          <CommandList className="max-h-[60vh]">
            {!canSearch(searchTerm(text)) ? (
              <p className={messageClass}>Nhập ít nhất 2 ký tự để tìm.</p>
            ) : isError ? (
              <div className={messageClass}>
                Không tìm được.{' '}
                <Button variant="link" className="h-auto px-0" onClick={() => refetch()}>Thử lại</Button>
              </div>
            ) : !results ? (
              <p className={messageClass}>Đang tìm…</p>
            ) : resultCount(results) === 0 ? (
              <p className={messageClass}>Không tìm thấy kết quả cho “{term}”.</p>
            ) : (
              <>
                {results.clients.length > 0 && (
                  <CommandGroup heading="Khách hàng">
                    {results.clients.map(c => (
                      <CommandItem key={c.id} value={`client-${c.id}`} onSelect={() => go(clientHref(c))}>
                        <Users aria-hidden="true" />
                        <span className="min-w-0 flex-1 truncate">
                          {c.name}
                          {(c.company || c.phone) && (
                            <span className="ml-2 text-xs text-muted-foreground">{[c.company, c.phone].filter(Boolean).join(' · ')}</span>
                          )}
                        </span>
                        {c.status === 'INACTIVE' && <StatusBadge status="INACTIVE" />}
                      </CommandItem>
                    ))}
                  </CommandGroup>
                )}
                {results.projects.length > 0 && (
                  <CommandGroup heading="Dự án">
                    {results.projects.map(p => (
                      <CommandItem key={p.id} value={`project-${p.id}`} onSelect={() => go(projectHref(p))}>
                        <FolderKanban aria-hidden="true" />
                        <span className="min-w-0 flex-1 truncate">
                          {p.name}
                          <span className="ml-2 text-xs text-muted-foreground">{p.clientName}</span>
                        </span>
                        <StatusBadge status={p.status} />
                      </CommandItem>
                    ))}
                  </CommandGroup>
                )}
                {results.quotations.length > 0 && (
                  <CommandGroup heading="Báo giá">
                    {results.quotations.map(q => (
                      <CommandItem key={q.id} value={`quotation-${q.id}`} onSelect={() => go(quotationHref(q))}>
                        <FileText aria-hidden="true" />
                        <span className="min-w-0 flex-1 truncate">
                          {quotationLabel(q)}
                          <span className="ml-2 text-xs text-muted-foreground">{q.title} · {q.clientName}</span>
                        </span>
                        <span className="text-xs"><MoneyText amount={q.total} /></span>
                        <StatusBadge status={q.effectiveStatus} />
                      </CommandItem>
                    ))}
                  </CommandGroup>
                )}
              </>
            )}
          </CommandList>
        </Command>
      </DialogContent>
    </Dialog>
  )
}
