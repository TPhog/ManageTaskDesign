import Markdown, { type Components } from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { safeHref } from '@/lib/markdown'

const components: Components = {
  // safeHref turned unsafe URLs into '': show the text, not a dead or dangerous link
  a: ({ href, children }) =>
    href ? (
      <a
        href={href}
        target="_blank"
        rel="noopener noreferrer"
        className="font-medium text-primary underline underline-offset-2 hover:text-primary/80"
      >
        {children}
      </a>
    ) : (
      <span>{children}</span>
    ),
}

/** Task description (plan 06b F5): Markdown + GFM autolinks, raw HTML skipped (no rehype-raw). */
export function MarkdownView({ source }: { source: string }) {
  return (
    <div className="space-y-2 break-words text-sm leading-relaxed text-foreground [&_blockquote]:border-l-2 [&_blockquote]:pl-3 [&_blockquote]:text-muted-foreground [&_code]:rounded [&_code]:bg-muted [&_code]:px-1 [&_h1]:text-base [&_h1]:font-semibold [&_h2]:font-semibold [&_h3]:font-semibold [&_ol]:list-decimal [&_ol]:pl-5 [&_ul]:list-disc [&_ul]:pl-5">
      <Markdown remarkPlugins={[remarkGfm]} urlTransform={safeHref} skipHtml components={components}>
        {source}
      </Markdown>
    </div>
  )
}
