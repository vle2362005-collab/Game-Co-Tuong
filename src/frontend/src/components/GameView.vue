<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'
import { createMatchSync } from '../composables/useMatchSync'
import { gameApi, type Match, type Room } from '../api/game'
import XiangqiBoard from './XiangqiBoard.vue'
import RoomChat from './RoomChat.vue'
import VideoCall from './VideoCall.vue'
const auth = useAuth(), route = useRoute(), router = useRouter(), id = String(route.params.id)
const room = ref<Room | null>(null), selected = ref<string | null>(null), error = ref(''), busy = ref(false)
const pendingMove = ref<{ pieceId: string; col: number; row: number; version: number; requestId: string } | null>(null)
const off: (() => void)[] = []
let active = true
let poll: ReturnType<typeof setInterval> | null = null
const sync = createMatchSync(id, () => gameApi.match(id), onSnapshot, e => { error.value = e instanceof Error ? e.message : String(e) })
const match = sync.snapshot
const side = computed(() => match.value?.players.find(p => p.userId === auth.user.value?.id)?.side || 'red')
const opponent = computed(() => room.value?.members.find(p => p.userId !== auth.user.value?.id))
const canMove = computed(() => !busy.value && !sync.needsRefresh.value && auth.signalR.isConnected.value && match.value?.status === 'playing' && match.value.sideToMove === side.value && room.value?.members.every(m => m.connected))
const destinations = computed(() => canMove.value ? (match.value?.legalMoves || []).filter(m => m.pieceId === selected.value).map(m => m.to) : [])
const checkedGeneralId = computed(() => match.value?.check ? match.value.pieces.find(p => p.type.toLowerCase() === 'general' && p.side.toLowerCase() === match.value?.sideToMove)?.id : null)
function onSnapshot(next: Match) {
  selected.value = null
  if (next.lastMove?.requestId === pendingMove.value?.requestId) pendingMove.value = null
  if (next.status === 'finished') auth.activeMatch.value = null
  else auth.enterMatch({ id, roomId: next.roomId, side: (next.players.find(p => p.userId === auth.user.value?.id)?.side || 'red') as 'red' | 'black', opponentName: opponent.value?.displayName || 'Đối thủ' })
}
async function load() {
  const ok = await sync.refresh()
  if (ok && active && match.value) room.value = await gameApi.room(match.value.roomId)
  return ok
}
async function retrySync() {
  error.value = ''
  try {
    if (!await load() || !active) return
    if (!await auth.signalR.connect()) throw new Error('Chưa kết nối máy chủ trực tuyến.')
    if (!active || !match.value) return
    await auth.signalR.invoke('JoinRoom', match.value.roomId)
    if (!active) { void auth.signalR.invoke('LeaveRoom', match.value.roomId).catch(() => {}); return }
    await load()
  } catch (e) { if (active) error.value = String(e) }
}
function checkSnapshot() { if (active && match.value && !busy.value && auth.signalR.isConnected.value) void sync.refresh() }
async function cell(col: number, row: number) {
  if (!canMove.value || !match.value) return
  const piece = match.value.pieces.find(p => p.position.col === col && p.position.row === row)
  if (piece?.side.toLowerCase() === side.value) { selected.value = selected.value === piece.id ? null : piece.id; return }
  if (!selected.value) return
  pendingMove.value = { pieceId: selected.value, col, row, version: match.value.version, requestId: crypto.randomUUID() }
  await sendMove()
}
async function sendMove() {
  if (!pendingMove.value || busy.value) return
  busy.value = true; error.value = ''
  const input = pendingMove.value
  try { sync.accept(await gameApi.move(id, input.pieceId, input.col, input.row, input.version, input.requestId), true); pendingMove.value = null }
  catch (e) {
    if (pendingMove.value) {
      error.value = e instanceof Error ? e.message : String(e)
      await load().catch(() => {})
      if (!pendingMove.value) error.value = ''
    }
    selected.value = null
  } finally { busy.value = false }
}
async function end(draw = false) {
  if (busy.value || (!draw && !window.confirm('Bạn xác nhận đầu hàng?'))) return
  busy.value = true; error.value = ''
  try { sync.accept(await gameApi.end(id, draw), true); await load() } catch (e) { error.value = String(e) } finally { busy.value = false }
}
async function leave() {
  if (!match.value) return
  try { await gameApi.leave(match.value.roomId); auth.leaveMatch(); await router.push('/lobby') } catch (e) { error.value = String(e) }
}
onMounted(async () => {
  off.push(auth.signalR.on('MatchChanged', m => { sync.accept(m as Match) }))
  off.push(auth.signalR.on('RoomStateChanged', r => { const next = r as Room; if (active && next.id === match.value?.roomId && (!room.value || next.version > room.value.version)) room.value = next }))
  off.push(auth.signalR.on('Reconnected', () => { if (active && match.value) void (async () => { await auth.signalR.invoke('JoinRoom', match.value!.roomId); if (active) await load() })().catch(e => { if (active) error.value = String(e) }) }))
  // Periodic/focus reads also recover a final event that was lost with no later event to expose the gap.
  poll = setInterval(checkSnapshot, 15000)
  window.addEventListener('focus', checkSnapshot)
  await retrySync()
})
onUnmounted(() => { active = false; if (poll) clearInterval(poll); window.removeEventListener('focus', checkSnapshot); sync.dispose(); off.forEach(fn => fn()); if (match.value) void auth.signalR.invoke('LeaveRoom', match.value.roomId).catch(() => {}) })
</script>
<template>
  <section class="game" :data-match-id="id" :data-version="match?.version">
    <p v-if="error" role="alert" class="error">{{ error }} <button v-if="pendingMove" :disabled="busy" @click="sendMove">Gửi lại cùng mã nước</button> <button :disabled="sync.refreshing.value" @click="retrySync">Đồng bộ lại</button></p>
    <p v-if="sync.refreshing.value && (!match || sync.needsRefresh.value)" role="status">Đang đồng bộ bàn cờ…</p>
    <template v-if="match">
      <div class="heading"><h1>Ván cờ · {{ room?.code }}</h1><span>Bạn cầm {{ side==='red' ? 'Đỏ' : 'Đen' }}</span></div>
      <p v-if="match.status==='finished'" role="status"><strong>Kết thúc: {{ match.winner ? (match.winner===side ? 'Bạn thắng' : 'Bạn thua') : 'Hòa' }} · {{ match.resultReason }}</strong></p>
      <p v-else>Lượt {{ match.sideToMove==='red' ? 'Đỏ' : 'Đen' }} · Nước {{ match.moveNumber }} <strong v-if="match.check">· Chiếu Tướng!</strong> <span v-if="!auth.signalR.isConnected.value">· Đang nối lại…</span></p>
      <div class="game-layout"><div><XiangqiBoard :pieces="match.pieces" :flipped="side==='black'" :selected="selected" :disabled="!canMove" :destinations="destinations" :last-move="match.lastMove" :checked-general-id="checkedGeneralId" @cell="cell"/>
        <div class="controls" v-if="match.status==='playing'"><button :disabled="busy" @click="end()">Đầu hàng</button><button :disabled="busy" @click="end(true)">{{ match.drawOfferedBy && match.drawOfferedBy!==side ? 'Đồng ý hòa' : 'Đề nghị hòa' }}</button></div>
        <button v-else @click="leave">Rời phòng, về sảnh</button>
      </div><aside><RoomChat :room-id="match.roomId"/><VideoCall :room-id="match.roomId" :opponent-id="opponent?.userId || null"/></aside></div>
    </template><p v-else>Đang tải ván cờ…</p>
  </section>
</template>
<style scoped>
.game{width:100%}.game-layout{display:grid;grid-template-columns:minmax(0,1.6fr) minmax(260px,1fr);gap:20px}.heading{display:flex;justify-content:space-between;align-items:center}.controls{display:flex;gap:12px;justify-content:center;margin-top:16px}button{padding:11px 18px;cursor:pointer}.error{color:#a12525}aside{display:grid;align-content:start;gap:18px}@media(max-width:760px){.game-layout{grid-template-columns:1fr}.heading{display:block}}
</style>
