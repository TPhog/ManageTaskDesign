import { createContext, useContext, useEffect, useEffectEvent } from 'react'
import { useForm, type DefaultValues, type FieldValues, type Path, type Resolver, type UseFormSetError } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import type { z } from 'zod'
import { api, emptyToNull, type ApiError } from '@/lib/api'
import { qk } from '@/lib/queryKeys'

/** GET /api/admin/settings */
export interface Settings {
  displayName: string
  contactEmail: string | null
  contactPhone: string | null
  logoFileId: string | null
  bankBin: string | null
  bankAccountNo: string | null
  bankAccountName: string | null
  quotationPrefix: string
  defaultValidityDays: number
  defaultDepositPercent: number
  defaultTerms: string
  portalResponseTime: string
  rowVersion: number
}

/** The fields a settings section may change; everything else in the PUT comes from the server copy. */
export type SettingsFields = Omit<Settings, 'rowVersion' | 'logoFileId'>

/**
 * PUT /api/admin/settings replaces every field, so each section sends the LATEST server settings with only its own
 * fields overridden (saving the bank account never resets the studio name). Same blank semantics as the old single form:
 * '' → null (e.g. '' fails the bank account format check; null quotation defaults mean "keep"), except
 * portalResponseTime, which must reach the API as a trimmed string ('' = back to "trong 24 giờ"; null would mean "keep").
 */
export function buildSettingsPayload(server: Settings, overrides: Partial<SettingsFields>) {
  const merged = {
    displayName: server.displayName,
    contactEmail: server.contactEmail,
    contactPhone: server.contactPhone,
    logoFileId: server.logoFileId ?? null,
    bankBin: server.bankBin,
    bankAccountNo: server.bankAccountNo,
    bankAccountName: server.bankAccountName,
    quotationPrefix: server.quotationPrefix,
    defaultValidityDays: server.defaultValidityDays,
    defaultDepositPercent: server.defaultDepositPercent,
    defaultTerms: server.defaultTerms,
    portalResponseTime: server.portalResponseTime,
    ...overrides,
  }
  return { ...emptyToNull(merged), portalResponseTime: (merged.portalResponseTime ?? '').trim(), rowVersion: server.rowVersion }
}

/** Copy ValidationProblem field errors from the API onto the matching form fields. Returns true if any matched. */
export function applyServerErrors<T extends FieldValues>(err: ApiError, setError: UseFormSetError<T>): boolean {
  if (!err.errors) return false
  for (const [field, messages] of Object.entries(err.errors)) {
    setError(field as Path<T>, { message: messages[0] })
  }
  return Object.keys(err.errors).length > 0
}

/** Each section reports whether it holds unsaved edits; the page guards tab switches and leaving on that. */
export const SettingsDirtyContext = createContext<(key: string, dirty: boolean) => void>(() => {})

export function useReportDirty(key: string, dirty: boolean) {
  const report = useContext(SettingsDirtyContext)
  useEffect(() => { report(key, dirty) }, [report, key, dirty])
  useEffect(() => () => report(key, false), [report, key])
}

export const CONFLICT_MESSAGE = 'Cài đặt vừa được thay đổi ở nơi khác — đã tải lại'

/** One section of the settings PUT: its own form, Save, toast, field errors and 409 handling. */
export function useSettingsSection<TIn extends FieldValues, TOut extends Partial<SettingsFields>>({ dirtyKey, settings, schema, pick, savedMessage }: {
  dirtyKey: string
  settings: Settings
  schema: z.ZodType<TOut, TIn>
  pick: (s: Settings) => TIn
  savedMessage: string
}) {
  const qc = useQueryClient()
  const form = useForm<TIn, unknown, TOut>({
    resolver: zodResolver(schema as never) as unknown as Resolver<TIn, unknown, TOut>,
    defaultValues: pick(settings) as DefaultValues<TIn>,
  })
  // new server copy (after any section saved): take it as the baseline, but never wipe what is being typed here
  const takeServerCopy = useEffectEvent((s: Settings) => form.reset(pick(s), { keepDirtyValues: true }))
  useEffect(() => { takeServerCopy(settings) }, [settings])

  const mutation = useMutation({
    mutationFn: (values: TOut) =>
      api.put('/admin/settings', buildSettingsPayload(qc.getQueryData<Settings>(qk.settings) ?? settings, values)),
    onSuccess: async () => {
      form.reset(form.getValues())
      toast.success(savedMessage)
      await qc.invalidateQueries({ queryKey: qk.settings, exact: true })
    },
    onError: async (err: ApiError) => {
      if (err.status === 409) {
        toast.error(CONFLICT_MESSAGE)
        await qc.refetchQueries({ queryKey: qk.settings, exact: true })
        const fresh = qc.getQueryData<Settings>(qk.settings)
        if (fresh) form.reset(pick(fresh))
      } else if (applyServerErrors(err, form.setError)) toast.error('Vui lòng kiểm tra lại các trường được đánh dấu.')
      else toast.error('Lưu thất bại. Vui lòng thử lại.')
    },
  })

  useReportDirty(dirtyKey, form.formState.isDirty)

  return {
    form,
    onSubmit: form.handleSubmit(values => mutation.mutate(values)),
    saving: mutation.isPending,
    canSave: form.formState.isDirty && !mutation.isPending,
  }
}
