import { Link } from 'react-router-dom'
import { Banknote, CheckCircle2, ChevronRight, FileText, type LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { EditorialSection } from './kit/editorial'
import type { TodoItem } from './todo'

const kindIcon: Record<TodoItem['kind'], LucideIcon> = { QUOTATION: FileText, PAYMENT: Banknote, REVIEW: CheckCircle2 }

/** CF-14 "Việc bạn cần làm": numbered rows like the editorial table of contents, each a large link. */
export function PortalTodoList({ items, number }: { items: TodoItem[]; number?: string }) {
  return (
    <EditorialSection id="portal-todo" number={number} title="Việc bạn cần làm" aside={items.length > 0 ? `${items.length} việc` : undefined}>
      {items.length === 0 ? (
        <p className="border-b border-border py-3 font-serif text-base italic text-muted-foreground">
          Hiện chưa có việc gì cần bạn làm. Nhà thiết kế sẽ báo khi có bản mới.
        </p>
      ) : (
        <ol className="m-0 list-none border-b border-border p-0 [&>li+li]:border-t [&>li+li]:border-border">
          {items.map((t, i) => {
            const Icon = kindIcon[t.kind]
            return (
              <li key={t.key}>
                <Link
                  to={t.to}
                  className="link-hover grid min-h-11 w-full grid-cols-[2rem_minmax(0,1fr)_1.125rem] items-center gap-1.5 py-3.5 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <span aria-hidden="true" className={cn('font-serif text-lg font-medium italic', i === 0 ? 'text-highlight-text' : 'text-muted-foreground')}>
                    {String(i + 1).padStart(2, '0')}
                  </span>
                  <span className="flex min-w-0 items-start gap-2">
                    <Icon className="mt-1 h-4 w-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                    <span className="font-display text-[1.0625rem] font-medium leading-snug text-foreground">{t.label}</span>
                  </span>
                  <ChevronRight className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
                </Link>
              </li>
            )
          })}
        </ol>
      )}
    </EditorialSection>
  )
}
