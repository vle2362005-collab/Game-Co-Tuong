<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'
import { gameApi, type Room } from '../api/game'
import RoomChat from './RoomChat.vue'
import VideoCall from './VideoCall.vue'
const route = useRoute(), router = useRouter(), auth = useAuth(), room = ref<Room | null>(null)
const error = ref(''), busy = ref(false), id = String(route.params.id)
const me = computed(() => room.value?.members.find(m => m.userId === auth.user.value?.id))
const opponent = computed(() => room.value?.members.find(m => m.userId !== auth.user.value?.id))
const host = computed(() => room.value?.hostUserId === auth.user.value?.id)
const canStart = computed(() => host.value && room.value?.members.length === 2 && room.value.members.every(m => m.ready && m.connected))
const off: (() => void)[] = []
const startRequest = crypto.randomUUID()
async function update(next: Room) {
  if (next.id !== id) return
  if (room.value && next.version < room.value.version) return
  room.value = next
  if (next.status === 'closed' || !next.members.some(m => m.userId === auth.user.value?.id)) { await router.push('/lobby'); return }
  if (next.matchId && (next.status === 'playing' || next.status === 'finished')) await router.push(`/game/${next.matchId}`)
}
async function run(action: () => Promise<Room>, leave = false) {
  if (busy.value) return
  busy.value = true; error.value = ''
  try { const next = await action(); if (leave) await router.push('/lobby'); else await update(next) }
  catch (e) { error.value = e instanceof Error ? e.message : String(e) }
  finally { busy.value = false }
}
function toggleReady() { return run(() => gameApi.ready(id, !me.value?.ready)) }
function start() { return run(() => gameApi.start(id, startRequest)) }
function leave() { return run(() => gameApi.leave(id), true) }
onMounted(async () => {
  off.push(auth.signalR.on('RoomStateChanged', r => { void update(r as Room) }))
  off.push(auth.signalR.on('Reconnected', () => { void run(async () => { await auth.signalR.invoke('JoinRoom', id); return gameApi.room(id) }) }))
  try {
    if (!await auth.signalR.connect()) throw new Error('Chưa kết nối máy chủ trực tuyến.')
    await auth.signalR.invoke('JoinRoom', id)
    await update(await gameApi.room(id))
  } catch (e) { error.value = String(e) }
})
onUnmounted(() => { off.forEach(fn => fn()); void auth.signalR.invoke('LeaveRoom', id).catch(() => {}) })
</script>
<template>
  <section v-if="room" class="room">
    <h1>{{ room.name }} · {{ room.code }}</h1><p>Đủ hai người sẵn sàng, chủ phòng có thể bắt đầu.</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <div class="seats"><article v-for="member in room.members" :key="member.userId"><h2>{{ member.displayName }}</h2><p>{{ member.side === 'red' ? 'Quân Đỏ' : 'Quân Đen' }}</p><p>{{ member.connected ? 'Trực tuyến' : 'Mất kết nối' }} · {{ member.ready ? 'Sẵn sàng' : 'Đang chờ' }}</p></article><article v-if="room.members.length < 2">Đang chờ đối thủ…</article></div>
    <div class="controls"><button :disabled="busy || !auth.signalR.isConnected.value" @click="toggleReady">{{ me?.ready ? 'Hủy sẵn sàng' : 'Sẵn sàng' }}</button>
    <button v-if="host" :disabled="busy || !canStart" @click="start">Bắt đầu</button>
    <button :disabled="busy" @click="leave">{{ host ? 'Đóng phòng' : 'Rời phòng' }}</button></div>
    <div class="communication"><RoomChat :room-id="id"/><VideoCall :room-id="id" :opponent-id="opponent?.userId || null"/></div>
  </section><section v-else><p>{{ error || 'Đang tải phòng…' }}</p><button @click="router.push('/lobby')">Về sảnh</button></section>
</template>
<style scoped>
.room{width:100%}.seats,.communication{display:grid;grid-template-columns:1fr 1fr;gap:18px}.seats article{padding:18px;border:1px solid #c9d7cd;border-radius:12px}.controls{display:flex;gap:12px;margin:20px 0}button{padding:12px 18px;cursor:pointer}button:disabled{opacity:.5}.error{color:#a12525}@media(max-width:700px){.seats,.communication{grid-template-columns:1fr}.controls{flex-wrap:wrap}}
</style>
