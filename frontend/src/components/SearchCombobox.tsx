import { useState } from 'react'
import { Check, ChevronsUpDown, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { cn } from '@/lib/utils'

export interface ComboOption {
  value: string
  label: string
  hint?: string
}

interface SearchComboboxProps {
  id: string
  /** selected value, '' = none */
  value: string
  /** trigger text for the selected value — it may not be in the current page of options */
  selectedLabel?: string
  options: ComboOption[]
  onSelect: (option: ComboOption | null) => void
  search: string
  onSearchChange: (q: string) => void
  loading?: boolean
  placeholder: string
  searchPlaceholder?: string
  emptyText?: string
  /** first item that clears the selection, e.g. "Tất cả khách hàng" */
  clearLabel?: string
  /** accessible name when no <Label htmlFor={id}> exists */
  ariaLabel?: string
  invalid?: boolean
  describedBy?: string
  disabled?: boolean
  className?: string
}

/** QA D-07: searchable picker. The caller filters (server or client side); cmdk does no filtering of its own. */
export function SearchCombobox(p: SearchComboboxProps) {
  const [open, setOpen] = useState(false)
  const selected = p.value ? (p.selectedLabel || p.options.find(o => o.value === p.value)?.label || '') : (p.clearLabel ?? '')

  function choose(option: ComboOption | null) {
    p.onSelect(option)
    p.onSearchChange('')
    setOpen(false)
  }

  return (
    // modal: scrolling the list works inside a Dialog/Sheet
    <Popover open={open} onOpenChange={setOpen} modal>
      <PopoverTrigger asChild>
        <Button
          id={p.id}
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          aria-label={p.ariaLabel}
          aria-invalid={p.invalid || undefined}
          aria-describedby={p.describedBy}
          disabled={p.disabled}
          className={cn('w-full justify-between font-normal', !selected && 'text-muted-foreground', p.className)}
        >
          <span className="truncate">{selected || p.placeholder}</span>
          <ChevronsUpDown className="h-4 w-4 shrink-0 opacity-50" aria-hidden="true" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[var(--radix-popover-trigger-width)] min-w-64 p-0" align="start">
        <Command shouldFilter={false}>
          <CommandInput
            placeholder={p.searchPlaceholder ?? 'Tìm…'}
            aria-label={p.searchPlaceholder ?? 'Tìm'}
            value={p.search}
            onValueChange={p.onSearchChange}
          />
          <CommandList>
            {p.loading ? (
              <div className="flex items-center gap-2 px-3 py-4 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                Đang tải…
              </div>
            ) : (
              <CommandEmpty>{p.emptyText ?? 'Không có kết quả'}</CommandEmpty>
            )}
            <CommandGroup>
              {p.clearLabel && (
                <CommandItem value="__clear__" onSelect={() => choose(null)}>
                  <Check className={cn('h-4 w-4', p.value ? 'opacity-0' : 'opacity-100')} aria-hidden="true" />
                  {p.clearLabel}
                </CommandItem>
              )}
              {p.options.map(o => (
                <CommandItem key={o.value} value={o.value} onSelect={() => choose(o)}>
                  <Check className={cn('h-4 w-4', p.value === o.value ? 'opacity-100' : 'opacity-0')} aria-hidden="true" />
                  <span className="min-w-0 flex-1 truncate">{o.label}</span>
                  {o.hint && <span className="ml-2 max-w-[45%] truncate text-xs text-muted-foreground">{o.hint}</span>}
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
}
