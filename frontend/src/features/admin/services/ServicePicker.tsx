import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { SearchCombobox } from '@/components/SearchCombobox'
import { api } from '@/lib/api'
import { formatVnd } from '@/lib/format'

export interface ActiveService {
  id: string
  name: string
  basePrice: number
  defaultIncludedRounds: number
  extraRoundPrice: number
}

/** Same cache entry as DeliverableDialog (GET /admin/services = active services, by name). */
export function useActiveServices(enabled = true) {
  return useQuery({
    queryKey: ['services', 'active'],
    queryFn: () => api.get<ActiveService[]>('/admin/services'),
    enabled,
  })
}

/** Case- and accent-insensitive ("thiet ke" finds "Thiết kế Logo"). */
const fold = (s: string) =>
  s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase()

interface Props {
  id: string
  /** selected service id, '' = none */
  value: string
  selectedLabel?: string
  onPick: (service: ActiveService | null) => void
  placeholder: string
  clearLabel?: string
  ariaLabel: string
  className?: string
}

/** Plan 09: searchable service list showing the base price; the caller decides what a pick fills in. */
export function ServicePicker({ id, value, selectedLabel, onPick, placeholder, clearLabel, ariaLabel, className }: Props) {
  const { data: services = [], isLoading } = useActiveServices()
  const [search, setSearch] = useState('')
  const options = services
    .filter(s => fold(s.name).includes(fold(search.trim())))
    .map(s => ({ value: s.id, label: s.name, hint: formatVnd(s.basePrice) }))
  return (
    <SearchCombobox
      id={id}
      value={value}
      selectedLabel={selectedLabel}
      options={options}
      search={search}
      onSearchChange={setSearch}
      loading={isLoading}
      onSelect={o => onPick(o ? services.find(s => s.id === o.value) ?? null : null)}
      placeholder={placeholder}
      clearLabel={clearLabel}
      searchPlaceholder="Tìm dịch vụ…"
      emptyText="Không có dịch vụ khớp"
      ariaLabel={ariaLabel}
      className={className}
    />
  )
}
