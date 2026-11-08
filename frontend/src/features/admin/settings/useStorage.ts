import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { BulkDeleteResult, StorageImagePage, StorageImageParams, StorageUsage, WatermarkSettings } from '@/lib/imageTypes'
import { toStorageQuery } from './storage'

const PAGE_SIZE = 50

/** Shared by the banner (every page) and the storage page; refreshed every 5 minutes. */
export function useStorageUsage() {
  return useQuery({
    queryKey: qk.storage.usage,
    queryFn: () => api.get<StorageUsage>('/admin/storage/usage'),
    staleTime: 60_000,
    refetchInterval: 5 * 60_000,
  })
}

export function useStorageImages(params: StorageImageParams) {
  const query = toStorageQuery(params).replace('completedOnly=1', 'completedOnly=true')
  const sep = query ? '&' : '?'
  return useQuery({
    queryKey: qk.storage.images(params),
    queryFn: () => api.get<StorageImagePage>(`/admin/storage/images${query}${sep}pageSize=${PAGE_SIZE}`),
    placeholderData: keepPreviousData,
  })
}

export function useBulkDeleteImages() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (ids: string[]) => api.post<BulkDeleteResult>('/admin/images/bulk-delete', { ids }),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: qk.storage.all })
      qc.invalidateQueries({ queryKey: qk.images.all })
    },
  })
}

export function useWatermark() {
  return useQuery({ queryKey: qk.watermark, queryFn: () => api.get<WatermarkSettings>('/admin/settings/watermark') })
}

export function useUpdateWatermark() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { enabled: boolean; text: string | null; opacityPercent: number }) =>
      api.put<WatermarkSettings>('/admin/settings/watermark', body),
    onSuccess: data => {
      qc.setQueryData(qk.watermark, data)
      qc.invalidateQueries({ queryKey: qk.settings })   // settings rowVersion (xmin) changed too
    },
  })
}
