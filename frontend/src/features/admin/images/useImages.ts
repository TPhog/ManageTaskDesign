import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import { toImageForm } from '@/lib/images'
import type { AdminImage, ImageOwnerType } from '@/lib/imageTypes'

/** Every image list and the storage usage/banner change together. */
export function invalidateImageQueries(qc: QueryClient) {
  qc.invalidateQueries({ queryKey: qk.images.all })
  qc.invalidateQueries({ queryKey: qk.storage.all })
}

export function useTaskImages(taskId: string) {
  return useQuery({
    queryKey: qk.images.task(taskId),
    queryFn: () => api.get<AdminImage[]>(`/admin/tasks/${taskId}/images`),
  })
}

export function useProjectImages(projectId: string, ownerType?: ImageOwnerType) {
  return useQuery({
    queryKey: qk.images.project(projectId, ownerType),
    queryFn: () => api.get<AdminImage[]>(
      `/admin/projects/${projectId}/images${ownerType ? `?ownerType=${ownerType}` : ''}`),
  })
}

export function useUploadTaskImages(taskId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ files, visibleToClient }: { files: File[]; visibleToClient: boolean }) =>
      api.postForm<AdminImage[]>(`/admin/tasks/${taskId}/images`,
        toImageForm(files, { visibleToClient: String(visibleToClient) })),
    onSettled: () => invalidateImageQueries(qc),
  })
}

export function useSetImageVisibility() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, visibleToClient }: { id: string; visibleToClient: boolean }) =>
      api.patch<AdminImage>(`/admin/images/${id}`, { visibleToClient }),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.images.all }),
  })
}

export function useDeleteImage() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/admin/images/${id}`),
    onSettled: () => invalidateImageQueries(qc),
  })
}
