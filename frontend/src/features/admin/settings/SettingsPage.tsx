import { useCallback, useEffect, useRef, useState } from 'react'
import { useBlocker, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { PageHeader } from '@/components/PageHeader'
import { WatermarkSection } from './WatermarkSection'
import { DigestSection } from './DigestSection'
import { StorageSummarySection } from './StorageSummarySection'
import { IntakeLinkSection } from './IntakeLinkSection'
import { SettingsDirtyContext, type Settings } from './settingsForm'
import { SETTINGS_TABS, parseSettingsTab } from './settingsTabs'
import {
  PasswordSection, PaymentSection, PortalResponseSection, QuotationDefaultsSection, RequestTemplatesCard, StudioSection,
} from './SettingsSections'

const tabOf = (search: string) => parseSettingsTab(new URLSearchParams(search).get('tab'))

export default function SettingsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const tab = parseSettingsTab(searchParams.get('tab'))

  // a deep link to a later tab (e.g. ?tab=account) must show that tab in the scrolling tab bar on phones
  const tabListRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const centerActive = () => {
      const list = tabListRef.current
      const active = list?.querySelector<HTMLElement>('[data-state=active]')
      if (list && active) list.scrollLeft = active.offsetLeft - (list.clientWidth - active.offsetWidth) / 2
    }
    centerActive()
    // tab widths change once the web font has loaded
    document.fonts?.ready.then(centerActive)
  }, [tab])

  const { data: settings, isLoading, isError, refetch } = useQuery({
    queryKey: qk.settings,
    queryFn: () => api.get<Settings>('/admin/settings'),
  })

  // which sections hold unsaved edits (only the open tab is mounted, so only its sections can be dirty)
  const [dirty, setDirty] = useState<Record<string, boolean>>({})
  const reportDirty = useCallback((key: string, value: boolean) => {
    setDirty(prev => (!!prev[key] === value ? prev : { ...prev, [key]: value }))
  }, [])
  const anyDirty = Object.values(dirty).some(Boolean)

  // one guard for both switching tab (a ?tab= navigation) and leaving the page
  const blocker = useBlocker(({ currentLocation, nextLocation }) =>
    anyDirty && (currentLocation.pathname !== nextLocation.pathname || tabOf(currentLocation.search) !== tabOf(nextLocation.search)))

  useEffect(() => {
    if (!anyDirty) return
    const handler = (e: BeforeUnloadEvent) => { e.preventDefault() }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [anyDirty])

  function selectTab(value: string) {
    setSearchParams(prev => {
      const next = new URLSearchParams(prev)
      next.set('tab', value)
      return next
    }, { replace: true })
  }

  return (
    <SettingsDirtyContext.Provider value={reportDirty}>
      <div className="space-y-6">
        <PageHeader eyebrow="Studio" title="Cài đặt" description="Mỗi mục có nút lưu riêng — lưu mục này không đổi các mục khác." />

        {/* manual: arrow keys only move focus, Enter/Space opens the tab, so browsing the tab list never triggers the discard dialog */}
        <Tabs value={tab} onValueChange={selectTab} activationMode="manual">
          <div className="relative">
            <TabsList ref={tabListRef} aria-label="Nhóm cài đặt" className="flex w-full justify-start overflow-x-auto pr-10 [scrollbar-width:none] lg:inline-flex lg:w-auto lg:max-w-full lg:pr-1">
              {SETTINGS_TABS.map(({ key, label, icon: Icon }) => (
                <TabsTrigger key={key} value={key} className="min-h-10 shrink-0 gap-2 px-3.5">
                  <Icon className="h-4 w-4" aria-hidden="true" />
                  {label}
                </TabsTrigger>
              ))}
            </TabsList>
            {/* the fade hints that the tab bar scrolls on narrow screens */}
            <div aria-hidden="true" className="pointer-events-none absolute inset-y-0 right-0 w-10 rounded-r-lg bg-linear-to-l from-surface-hover lg:hidden" />
          </div>

          {isLoading ? (
            <div className="mt-6 space-y-6">
              <Skeleton className="h-56 w-full rounded-xl" />
              <Skeleton className="h-40 w-full rounded-xl" />
            </div>
          ) : isError || !settings ? (
            <div role="alert" className="mt-6 flex flex-wrap items-center gap-3 rounded-xl border border-border bg-card p-6 text-sm text-muted-foreground">
              Không tải được cài đặt.
              <Button type="button" variant="outline" size="sm" onClick={() => refetch()}>Thử lại</Button>
            </div>
          ) : (
            <>
              <TabsContent value="studio" className="mt-6 space-y-6"><StudioSection settings={settings} /></TabsContent>
              <TabsContent value="payment" className="mt-6 space-y-6"><PaymentSection settings={settings} /></TabsContent>
              <TabsContent value="quotation" className="mt-6 space-y-6"><QuotationDefaultsSection settings={settings} /></TabsContent>
              <TabsContent value="portal" className="mt-6 space-y-6">
                <PortalResponseSection settings={settings} />
                <IntakeLinkSection />
                <RequestTemplatesCard />
              </TabsContent>
              <TabsContent value="images" className="mt-6 space-y-6">
                <WatermarkSection />
                <StorageSummarySection />
              </TabsContent>
              <TabsContent value="notifications" className="mt-6 space-y-6"><DigestSection /></TabsContent>
              <TabsContent value="account" className="mt-6 space-y-6"><PasswordSection /></TabsContent>
            </>
          )}
        </Tabs>
      </div>

      <ConfirmDialog
        open={blocker.state === 'blocked'}
        onOpenChange={open => { if (!open) blocker.reset?.() }}
        title="Bỏ thay đổi chưa lưu?"
        description="Các thay đổi chưa bấm Lưu ở mục này sẽ mất."
        confirmLabel="Bỏ thay đổi"
        cancelLabel="Tiếp tục sửa"
        destructive
        onConfirm={() => blocker.proceed?.()}
      />
    </SettingsDirtyContext.Provider>
  )
}
