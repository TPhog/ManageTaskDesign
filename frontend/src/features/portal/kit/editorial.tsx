/**
 * Editorial kit for the client portal (P16 decision 2): rules and dotted leaders instead of boxes, serif headings,
 * a page that reads like a letter plus an invoice. Every part uses semantic tokens only, so it renders correctly under
 * `data-theme="editorial"` (set by `usePortalTheme`). No `rounded-*`, no shadows: the theme forces both to zero.
 *
 * @example
 * <Letter kicker="Thư gửi khách · 09/11" greeting={<>Chào {clientName}<i>,</i></>}>Tuần này có hai việc cần bạn.</Letter>
 * <EditorialSection number="I." title="Việc bạn cần làm">
 *   <NumberedList>
 *     <NumberedListItem index={1} title="Duyệt hai hạng mục" meta="Logo chính v2" to="/portal/projects/p1" hot />
 *   </NumberedList>
 * </EditorialSection>
 * <EditorialSection number="III." title="Thanh toán">
 *   <LeaderList>
 *     <LeaderRow label="Báo giá BG-2026-031" value={<MoneyText value={24000000} />} />
 *     <LeaderTotal label="Đợt 2 · hạn 12/11" value={<MoneyText value={12000000} />} />
 *   </LeaderList>
 * </EditorialSection>
 */
import { useId, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'

export interface KickerProps {
  children: ReactNode
  /** `muted` (default) = caption grey; `highlight` = oxblood, for the letter kicker or an urgent label. */
  tone?: 'muted' | 'highlight'
  className?: string
}

/** Small uppercase label (11px, tracked) — "THƯ GỬI KHÁCH · 09/11", table-like captions. Renders a `<span>`. */
export function Kicker({ children, tone = 'muted', className }: KickerProps) {
  return <span className={cn('kicker', tone === 'highlight' ? 'text-highlight-text' : 'text-muted-foreground', className)}>{children}</span>
}

export interface EditorialSectionProps {
  /** Visible `h2` text AND the accessible name of the `region` — tests find the section with `getByRole('region', { name: title })`. */
  title: string
  /** Optional chapter numeral ("I.", "II."), italic oxblood, hidden from screen readers. */
  number?: string
  /** Id of the `h2`; pass one when something links to the section (`#portal-contact`), otherwise an id is generated. */
  id?: string
  /** Small right-aligned note on the heading line (e.g. "3 việc", a link). */
  aside?: ReactNode
  className?: string
  children: ReactNode
}

/** A chapter of the page: 3px ink rule, italic numeral, serif heading; the region is named by `title`. */
export function EditorialSection({ title, number, id, aside, className, children }: EditorialSectionProps) {
  const auto = useId()
  const headingId = id ?? auto
  return (
    <section aria-labelledby={headingId} className={cn('mt-8 border-t-[3px] border-rule pt-2.5 first:mt-0', className)}>
      <div className="mb-1 flex items-baseline gap-2.5">
        {number && <span aria-hidden="true" className="font-serif text-base font-medium italic text-highlight-text">{number}</span>}
        <h2 id={headingId} className="font-display text-[1.375rem] font-medium leading-tight text-foreground">{title}</h2>
        {aside && <div className="ml-auto text-xs text-muted-foreground">{aside}</div>}
      </div>
      {children}
    </section>
  )
}

/** Invoice-style list: a `<dl>` whose children are `LeaderRow` / `LeaderTotal`. */
export function LeaderList({ children, className }: { children: ReactNode; className?: string }) {
  return <dl className={cn('text-sm', className)}>{children}</dl>
}

export interface LeaderRowProps {
  /** Left side (`<dt>`), e.g. "Báo giá BG-2026-031". May wrap; the dotted leader fills the gap. */
  label: ReactNode
  /** Right side (`<dd>`), semibold tabular figures — pass `<MoneyText />` for money. */
  value: ReactNode
  /** `danger` = oxblood row (overdue); `muted` = caption grey (already paid, informational). */
  tone?: 'default' | 'danger' | 'muted'
}

/** "Báo giá BG-2026-031 ........ 24.000.000 ₫" — one `<dt>`/`<dd>` pair with a hairline under it. */
export function LeaderRow({ label, value, tone = 'default' }: LeaderRowProps) {
  return (
    <div className={cn('flex items-baseline gap-2 border-b border-border py-2.5', tone === 'danger' && 'text-danger', tone === 'muted' && 'text-muted-foreground')}>
      <dt className="flex min-w-0 flex-1 items-baseline gap-2">
        <span className="min-w-0">{label}</span>
        <span aria-hidden="true" className="leader" />
      </dt>
      <dd className="m-0 shrink-0 font-semibold tabular-nums">{value}</dd>
    </div>
  )
}

/** The amount due: bold label, large serif figure, closed by a 3px double ink rule. Use once, last in a `LeaderList`. */
export function LeaderTotal({ label, value }: { label: ReactNode; value: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-3 border-b-[3px] border-double border-rule py-3.5">
      <dt className="font-semibold">{label}</dt>
      <dd className="m-0 font-display text-[2rem] font-medium leading-none tracking-[-0.02em] tabular-nums">{value}</dd>
    </div>
  )
}

/** An `<ol>` of `NumberedListItem`s with hairlines between rows. `label` becomes the list's `aria-label`. */
export function NumberedList({ children, label }: { children: ReactNode; label?: string }) {
  return <ol aria-label={label} className="m-0 list-none border-b border-border p-0 [&>li+li]:border-t [&>li+li]:border-border">{children}</ol>
}

export interface NumberedListItemProps {
  /** 1-based position, shown as an italic "01" — decorative, not part of the accessible name. */
  index: number
  /** Serif row title; it (plus `meta`) is the link/button name. */
  title: ReactNode
  /** Small grey second line ("Logo chính v2 · Bảng màu", "12.000.000 ₫ · trước 12/11"). */
  meta?: ReactNode
  /** Router path → the row is a `<Link>`. Takes precedence over `onClick`. */
  to?: string
  /** Click handler → the row is a `<button type="button">` (when there is no `to`). */
  onClick?: () => void
  /** Urgent: the numeral turns oxblood. */
  hot?: boolean
}

/** "01  Duyệt hai hạng mục ›" — row ≥ 64px; link when `to`, button when `onClick`, plain row otherwise. */
export function NumberedListItem({ index, title, meta, to, onClick, hot = false }: NumberedListItemProps) {
  const body = (
    <>
      <span aria-hidden="true" className={cn('font-serif text-lg font-medium italic', hot ? 'text-highlight-text' : 'text-muted-foreground')}>
        {String(index).padStart(2, '0')}
      </span>
      <span className="min-w-0">
        <span className="block font-display text-[1.0625rem] font-medium leading-snug text-foreground">{title}</span>
        {meta && <span className="block text-xs text-muted-foreground">{meta}</span>}
      </span>
      {(to || onClick) && <ChevronRight className="h-4 w-4 text-muted-foreground" aria-hidden="true" />}
    </>
  )
  const row = 'grid min-h-16 w-full grid-cols-[2rem_minmax(0,1fr)_1.125rem] items-center gap-1.5 py-2.5 text-left'
  return (
    <li>
      {to ? <Link to={to} className={cn(row, 'link-hover')}>{body}</Link>
        : onClick ? <button type="button" onClick={onClick} className={cn(row, 'link-hover')}>{body}</button>
          : <div className={row}>{body}</div>}
    </li>
  )
}

export interface LetterProps {
  /** Oxblood kicker above the greeting, e.g. "Thư gửi khách · 09/11". */
  kicker?: ReactNode
  /** Large serif greeting; wrap a word in `<i>` to set it in italic oxblood (`<>Chào chị <i>Hạnh</i>,</>`). Rendered as a `<p>`. */
  greeting: ReactNode
  /** Short serif paragraph under the greeting (max 60ch). */
  children?: ReactNode
}

/** Letter-style intro: kicker, large serif greeting (not a heading — the page owns its h1), then a short paragraph. */
export function Letter({ kicker, greeting, children }: LetterProps) {
  return (
    <div className="space-y-2.5">
      {kicker && <Kicker tone="highlight">{kicker}</Kicker>}
      <p className="font-display text-[2.125rem] font-medium leading-[1.1] tracking-[-0.02em] text-foreground [&_i]:text-highlight-text">{greeting}</p>
      {children && <div className="max-w-[60ch] font-serif text-[1.03rem] leading-relaxed text-foreground">{children}</div>}
    </div>
  )
}

/** Images with an italic serif caption ("Hình 1–3. …") and a hairline below. No frame, no radius, no shadow. */
export function Figure({ caption, children }: { caption: ReactNode; children: ReactNode }) {
  return (
    <figure className="m-0">
      {children}
      <figcaption className="mt-2 border-b border-border pb-2.5 font-serif text-[0.8125rem] italic text-muted-foreground">{caption}</figcaption>
    </figure>
  )
}

/** The designer's note, quoted: italic serif with a thin left rule; `cite` appends " — Linh" upright. */
export function PullQuote({ children, cite }: { children: ReactNode; cite?: string }) {
  return (
    <blockquote className="m-0 border-l-2 border-border pl-3.5 font-serif text-base italic leading-relaxed text-foreground">
      {children}{cite && <span className="not-italic text-muted-foreground"> — {cite}</span>}
    </blockquote>
  )
}

const statusTextTone = {
  default: 'text-foreground', highlight: 'text-highlight-text', success: 'text-success',
  warning: 'text-warning', danger: 'text-danger', muted: 'text-muted-foreground',
} as const

export interface StatusTextProps {
  /** `highlight` = waiting for the client ("chờ duyệt"), `success` = done/paid, `warning` = due soon, `danger` = overdue. */
  tone?: keyof typeof statusTextTone
  /** The status wording itself (lower-case reads best: "chờ duyệt"). The small square is decorative. */
  children: ReactNode
}

/** Inline status as italic serif text with a small square — the editorial alternative to a pill badge. */
export function StatusText({ tone = 'default', children }: StatusTextProps) {
  return (
    <span className={cn('inline-flex items-center gap-1.5 font-serif text-sm font-medium italic', statusTextTone[tone])}>
      <span aria-hidden="true" className="h-1.5 w-1.5 bg-current" />
      {children}
    </span>
  )
}
