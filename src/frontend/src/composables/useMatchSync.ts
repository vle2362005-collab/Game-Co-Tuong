import { ref, shallowRef } from 'vue'
import type { Match } from '../api/game'

// Every message is a complete, versioned snapshot. A gap is repaired from the API.
export function createMatchSync(id: string, load: () => Promise<Match>, onChange: (match: Match) => void, onError: (error: unknown) => void) {
  const snapshot = shallowRef<Match | null>(null)
  const refreshing = ref(false), needsRefresh = ref(false)
  let targetVersion = 0, disposed = false, pending: Promise<boolean> | null = null

  function valid(next: Match) {
    return next?.id === id && Number.isSafeInteger(next.version) && next.version > 0 && Array.isArray(next.pieces)
  }
  function apply(next: Match) {
    if (disposed || !valid(next) || (snapshot.value && next.version <= snapshot.value.version)) return false
    snapshot.value = next
    onChange(next)
    return true
  }
  function accept(next: Match, authoritative = false) {
    if (disposed || !valid(next) || (snapshot.value && next.version <= snapshot.value.version)) return false
    targetVersion = Math.max(targetVersion, next.version)
    if (!authoritative && snapshot.value && next.version > snapshot.value.version + 1) {
      needsRefresh.value = true
      void refresh()
      return false
    }
    const changed = apply(next)
    needsRefresh.value = (snapshot.value?.version || 0) < targetVersion
    return changed
  }
  function refresh(): Promise<boolean> {
    if (disposed) return Promise.resolve(false)
    if (pending) return pending
    refreshing.value = true
    pending = (async () => {
      try {
        // One retry covers a newer event arriving while the first read is in flight.
        for (let attempt = 0; attempt < 2; attempt++) {
          const next = await load()
          if (disposed) return false
          if (!valid(next)) throw new Error('Không nhận được trạng thái trận hợp lệ.')
          apply(next)
          needsRefresh.value = (snapshot.value?.version || 0) < targetVersion
          if (!needsRefresh.value) return true
        }
        throw new Error('Bàn cờ đang thay đổi. Hãy đồng bộ lại.')
      } catch (error) {
        if (!disposed) { needsRefresh.value = true; onError(error) }
        return false
      } finally { refreshing.value = false; pending = null }
    })()
    return pending
  }
  function dispose() { disposed = true }
  return { snapshot, refreshing, needsRefresh, accept, refresh, dispose }
}
