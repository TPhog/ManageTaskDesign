import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { DigestSettings } from '@/lib/types'

export function useDigestSettings() {
  return useQuery({ queryKey: qk.digest, queryFn: () => api.get<DigestSettings>('/admin/settings/digest') })
}

export function useUpdateDigestSettings() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { enabled: boolean; hourVn: number }) => api.put<DigestSettings>('/admin/settings/digest', body),
    onSuccess: data => {
      qc.setQueryData(qk.digest, data)
      qc.invalidateQueries({ queryKey: qk.settings })   // settings rowVersion (xmin) changed too
    },
  })
}
