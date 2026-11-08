/** Mirrors Service.MaxTaskTemplateSteps / MaxTaskTemplateStepLength in the backend domain. */
export const MAX_TEMPLATE_STEPS = 20
export const MAX_STEP_LENGTH = 200

/** Swap a step with its neighbour (delta -1 = up, 1 = down); out of range returns the same array. */
export function moveStep(steps: string[], index: number, delta: -1 | 1): string[] {
  const target = index + delta
  if (index < 0 || index >= steps.length || target < 0 || target >= steps.length) return steps
  const next = steps.slice()
  const moved = next[index]
  next[index] = next[target]
  next[target] = moved
  return next
}

/** Append a trimmed step; blank text or a full template returns the same array. */
export function addStep(steps: string[], text: string): string[] {
  const value = text.trim()
  if (!value || steps.length >= MAX_TEMPLATE_STEPS) return steps
  return [...steps, value.slice(0, MAX_STEP_LENGTH)]
}

/** Client-side copy of the server rule (400 errors.taskTemplate). */
export function templateError(steps: string[]): string | null {
  if (steps.length > MAX_TEMPLATE_STEPS) return `Tối đa ${MAX_TEMPLATE_STEPS} bước`
  if (steps.some(s => s.trim().length === 0)) return 'Bước mẫu không được để trống'
  if (steps.some(s => s.trim().length > MAX_STEP_LENGTH)) return `Mỗi bước tối đa ${MAX_STEP_LENGTH} ký tự`
  return null
}
