import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { qk } from '@/lib/queryKeys'
import type { PublicIntakeForm } from '@/lib/requestTemplateTypes'

export function usePublicIntakeForm(slug: string) {
  return useQuery({
    queryKey: qk.publicIntake(slug),
    queryFn: () => api.get<PublicIntakeForm>(`/public/intake/${encodeURIComponent(slug)}`),
    retry: false,
    refetchOnWindowFocus: false,   // a refetch would replace the form token mid-typing
    staleTime: Infinity,
  })
}

export function useSubmitIntake(slug: string) {
  return useMutation({ mutationFn: (form: FormData) => api.postForm<void>(`/public/intake/${encodeURIComponent(slug)}`, form) })
}
