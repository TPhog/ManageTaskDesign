import { useCallback, useEffect, useRef } from 'react'

/**
 * D-30: collects "has unsaved changes" flags from several children and forwards the aggregate to the parent synchronously
 * (no state hops, so a keypress right after an edit already sees the flag).
 */
export function useDirtyMap(onDirtyChange?: (dirty: boolean) => void) {
  const map = useRef<Record<string, boolean>>({})
  const last = useRef(false)
  const notify = useRef(onDirtyChange)
  useEffect(() => {
    notify.current = onDirtyChange
  })

  return useCallback((key: string, value: boolean) => {
    map.current[key] = value
    const dirty = Object.values(map.current).some(Boolean)
    if (dirty !== last.current) {
      last.current = dirty
      notify.current?.(dirty)
    }
  }, [])
}

/** Reports `dirty` to the parent and clears the flag when the component unmounts. */
export function useReportDirty(dirty: boolean, report: (dirty: boolean) => void) {
  useEffect(() => {
    report(dirty)
    return () => report(false)
  }, [dirty, report])
}
