/** QA C-04: Zalo opens a chat from the phone number (digits only). */
export const zaloUrl = (phone: string) => `https://zalo.me/${phone.replace(/\D/g, '')}`
