/** One vocabulary for the designer UI (docs/08-ui-spec.md). Keep user-visible strings in these terms. */
export const GLOSSARY = {
  deliverable: 'hạng mục',
  task: 'công việc',
  revisionRound: 'vòng sửa',
  freeRounds: 'vòng sửa miễn phí',
  extraRoundPrice: 'Giá vòng sửa thêm',
  paymentReminder: 'tin nhắn thanh toán',
} as const

/** Phrases that must not appear in rendered UI text; glossary.test.ts scans every .tsx for them. */
export const FORBIDDEN_UI_TERMS: readonly string[] = [
  'Thêm deliverable',
  'Sửa deliverable',
  'Tên deliverable',
  'Chưa có deliverable',
  ' deliverable<',
  '} deliverable',
  'Giá vòng thêm',
  'Vòng thêm:',
  'Giá/vòng vượt',
  'Yêu cầu & Revision',
  'tin nhắc',
  '(BR-',
]
