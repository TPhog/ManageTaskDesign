import { defaultConvertValues, telHref, templateNote, toConvertBody, zaloHref } from '@/features/admin/intake/intakeUtils'
import type { IntakeDetail } from '@/lib/requestTemplateTypes'

const detail: IntakeDetail = {
  id: 'i1', createdAt: '2026-11-13T03:00:00Z', status: 'NEW', contactName: 'Chị Lan', contactPhone: '0909 123 456',
  contactEmail: null, templateName: 'Menu cửa hàng', excerpt: '…', imageCount: 1, templateId: 't1',
  answers: [{ key: 'shop_type', label: 'Loại cửa hàng', type: 'SINGLE_CHOICE', display: 'Quán cà phê' }],
  note: '', body: 'Mẫu: Menu cửa hàng\n- Loại cửa hàng: Quán cà phê', images: [],
  suggestedDesignType: 'PRINT', suggestedServiceId: 's1', suggestedServiceName: 'Thiết kế menu', suggestedDueDate: '2026-12-01',
  rejectReason: null, rejectedAt: null, convertedClientId: null, convertedProjectId: null, convertedAt: null,
  matchingClients: [{ id: 'c1', name: 'Lan cũ', phone: '+84909123456', email: null }], rowVersion: 7,
}

describe('convert dialog values (BR-136)', () => {
  it('prefills from the submission and its template', () => {
    expect(defaultConvertValues(detail)).toEqual({
      clientMode: 'new', existingClientId: 'c1', clientName: 'Chị Lan', projectName: 'Menu cửa hàng — Chị Lan',
      designType: 'PRINT', dueDate: '2026-12-01', serviceId: 's1', deliverableName: 'Thiết kế menu',
    })
    expect(defaultConvertValues({ ...detail, templateName: null, suggestedServiceId: null, suggestedServiceName: null, suggestedDueDate: null }))
      .toMatchObject({ projectName: 'Yêu cầu — Chị Lan', serviceId: '', deliverableName: '', dueDate: '' })
  })

  it('builds the request body for a new or an existing client', () => {
    const values = defaultConvertValues(detail)
    expect(toConvertBody(values, 7)).toEqual({
      existingClientId: null, clientName: 'Chị Lan', projectName: 'Menu cửa hàng — Chị Lan', designType: 'PRINT',
      dueDate: '2026-12-01', serviceId: 's1', deliverableName: 'Thiết kế menu', rowVersion: 7,
    })
    expect(toConvertBody({ ...values, clientMode: 'existing', serviceId: '', dueDate: '' }, 7)).toMatchObject({
      existingClientId: 'c1', clientName: null, serviceId: null, deliverableName: null, dueDate: null,
    })
  })
})

describe('contact links and notes', () => {
  it('normalises Vietnamese numbers for tel: and Zalo', () => {
    expect(telHref('+84 909.123.456')).toBe('tel:+84909123456')
    expect(zaloHref('+84 909 123 456')).toBe('https://zalo.me/0909123456')
    expect(zaloHref('0909 123 456')).toBe('https://zalo.me/0909123456')
  })

  it('extracts the "Ghi chú thêm" of a rendered template request', () => {
    expect(templateNote('Mẫu: Poster\n- Tiêu đề chính: A\n\nGhi chú thêm:\nGửi trước thứ 6\nCảm ơn')).toBe('Gửi trước thứ 6\nCảm ơn')
    expect(templateNote('Mẫu: Poster\n- Tiêu đề chính: A')).toBeNull()
  })
})
