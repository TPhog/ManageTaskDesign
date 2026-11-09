import { Fragment } from 'react'
import type { TemplateAnswer } from '@/lib/requestTemplateTypes'

interface Props {
  answers: TemplateAnswer[]
  title?: string | null
  note?: string | null
}

/** Plan 13: a template request / intake submission as a clean label → answer list (designer side). */
export function TemplateAnswersView({ answers, title, note }: Props) {
  if (answers.length === 0 && !note) return null
  return (
    <section aria-label={title ? `Câu trả lời mẫu ${title}` : 'Câu trả lời'} className="rounded-lg border bg-muted/40 p-3">
      {title && <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Mẫu: {title}</p>}
      {answers.length > 0 && (
        <dl className="grid gap-x-4 gap-y-2 text-sm sm:grid-cols-[minmax(0,12rem)_minmax(0,1fr)]">
          {answers.map(a => (
            <Fragment key={a.key}>
              <dt className="text-muted-foreground">{a.label}</dt>
              <dd className="whitespace-pre-wrap break-words text-foreground">{a.display}</dd>
            </Fragment>
          ))}
        </dl>
      )}
      {note && (
        <div className="mt-3 border-t pt-3">
          <p className="text-xs font-medium text-muted-foreground">Ghi chú thêm</p>
          <p className="whitespace-pre-wrap break-words text-sm text-foreground">{note}</p>
        </div>
      )}
    </section>
  )
}
