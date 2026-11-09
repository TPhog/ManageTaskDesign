import { studioInitials } from './contact'
import { Kicker } from './kit/editorial'

interface Props { clientName: string; studioName: string }

/** CF-14/16: a letter-style greeting with the studio's initials as its logo, plus a tip to keep the link. */
export function PortalWelcome({ clientName, studioName }: Props) {
  return (
    <div className="space-y-2.5">
      <div className="flex items-center gap-3">
        <span
          aria-hidden="true"
          className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-primary font-display text-lg font-medium italic text-primary-foreground"
        >
          {studioInitials(studioName)}
        </span>
        <Kicker tone="highlight">Thư gửi khách</Kicker>
      </div>
      <h1 className="break-words font-display text-[2.125rem] font-medium leading-[1.1] tracking-[-0.02em] text-foreground">Chào {clientName}!</h1>
      <p className="max-w-[60ch] font-serif text-[1.03rem] leading-relaxed text-foreground">
        {studioName} gửi bạn trang theo dõi dự án. Ảnh thiết kế, báo giá và các việc cần bạn làm đều ở đây.
      </p>
      <p className="max-w-[60ch] text-xs leading-relaxed text-muted-foreground">
        Mẹo: giữ lại tin nhắn chứa link này để mở lại lần sau. Link dùng được cho tới khi nhà thiết kế gửi link mới.
      </p>
    </div>
  )
}
