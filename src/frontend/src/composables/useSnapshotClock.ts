import { onMounted, onUnmounted, ref, watch, type Ref } from 'vue'
import type { Match } from '../api/game'

export function serverSecondsRemaining(match: Match | null, deadline: string | null | undefined, elapsedMs = 0) {
  const serverTime = Date.parse(match?.serverTime || '')
  const end = Date.parse(deadline || '')
  if (!Number.isFinite(serverTime) || !Number.isFinite(end)) return null
  return Math.max(0, Math.ceil((end - serverTime - Math.max(0, elapsedMs)) / 1000))
}

// Measure elapsed time monotonically; the user's wall clock cannot extend a deadline.
// Reaching zero only changes the label. The server decides whether the match ends.
export function useSnapshotClock(match: Readonly<Ref<Match | null>>) {
  const elapsed = ref(0)
  let observedAt = performance.now(), timer: ReturnType<typeof setInterval> | undefined
  watch(match, () => { observedAt = performance.now(); elapsed.value = 0 }, { flush: 'sync' })
  onMounted(() => { timer = setInterval(() => { elapsed.value = performance.now() - observedAt }, 250) })
  onUnmounted(() => { if (timer) clearInterval(timer) })
  return (deadline: string | null | undefined) => serverSecondsRemaining(match.value, deadline, elapsed.value)
}
