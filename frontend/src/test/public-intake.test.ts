import { contactSchema, intakeErrorMessage, isFormExpired, toIntakeForm } from '@/features/public/intake'

const contact = { contactName: '  Chị Lan ', contactPhone: '0909 123 456', contactEmail: '' }

describe('contactSchema', () => {
  it('needs a name and a usable phone; email is optional', () => {
    expect(contactSchema.safeParse(contact).success).toBe(true)
    const bad = contactSchema.safeParse({ contactName: '', contactPhone: '123', contactEmail: 'lan@' })
    expect(bad.error?.issues.map(i => [i.path[0], i.message])).toEqual([
      ['contactName', 'Nhập tên của bạn'],
      ['contactPhone', 'Nhập số điện thoại / Zalo hợp lệ'],
      ['contactEmail', 'Email không hợp lệ'],
    ])
  })
})

describe('toIntakeForm', () => {
  it('builds the multipart body of POST /api/public/intake/{slug}', () => {
    const photo = new File(['x'], 'menu.png', { type: 'image/png' })
    const form = toIntakeForm({ formToken: 'tok', honeypot: '', contact, templateId: 't1', answers: { shop_type: 'Quán cà phê' }, note: ' Gấp ', files: [photo] })

    expect(form.get('formToken')).toBe('tok')
    expect(form.get('website')).toBe('')
    expect(form.get('contactName')).toBe('Chị Lan')
    expect(form.get('contactPhone')).toBe('0909 123 456')
    expect(form.has('contactEmail')).toBe(false)
    expect(form.get('templateId')).toBe('t1')
    expect(JSON.parse(form.get('answers') as string)).toEqual({ shop_type: 'Quán cà phê' })
    expect(form.get('note')).toBe('Gấp')
    expect(form.getAll('files')).toHaveLength(1)
  })

  it('free text sends neither templateId nor answers', () => {
    const form = toIntakeForm({ formToken: 'tok', honeypot: '', contact, templateId: '', note: 'Thiệp cưới', files: [] })
    expect(form.has('templateId')).toBe(false)
    expect(form.has('answers')).toBe(false)
  })
})

describe('intakeErrorMessage', () => {
  it('maps the public errors to plain Vietnamese', () => {
    expect(isFormExpired({ status: 400, code: 'FORM_EXPIRED' })).toBe(true)
    expect(intakeErrorMessage({ status: 400, code: 'FORM_EXPIRED' })).toBe('Biểu mẫu đã hết hạn. Trang vừa được tải lại — bạn bấm Gửi thêm lần nữa nhé.')
    expect(intakeErrorMessage({ status: 429, code: 'RATE_LIMITED' })).toBe('Hôm nay studio đã nhận nhiều yêu cầu — bạn vui lòng liên hệ trực tiếp hoặc gửi lại vào ngày mai.')
    expect(intakeErrorMessage({ status: 429 })).toBe('Bạn gửi hơi nhanh — vui lòng đợi một phút rồi thử lại.')
    expect(intakeErrorMessage({ status: 507, code: 'STORAGE_FULL' })).toBe('Hiện chưa nhận thêm ảnh được — bạn bỏ ảnh ra rồi gửi lại, hoặc gửi ảnh qua Zalo cho studio.')
    expect(intakeErrorMessage({ status: 404 })).toBe('Link này không còn hoạt động. Vui lòng liên hệ trực tiếp studio.')
    expect(intakeErrorMessage({ status: 400, errors: { contactPhone: ['Nhập số điện thoại / Zalo hợp lệ'] } })).toBe('Nhập số điện thoại / Zalo hợp lệ')
  })
})
