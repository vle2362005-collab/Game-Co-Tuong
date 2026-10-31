<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'
import { gameApi, type Room } from '../api/game'
const auth = useAuth(), router = useRouter()
const rooms = ref<Room[]>([]), name = ref('Phòng cờ của tôi'), code = ref(''), error = ref(''), busy = ref(false), loading = ref(true)
const disposers: (() => void)[] = []
let createRequest = crypto.randomUUID()
async function load() {
  try { rooms.value = await gameApi.rooms() } catch (e) { error.value = String(e) } finally { loading.value = false }
}
async function act(action: () => Promise<Room>) {
  if (busy.value) return
  busy.value = true; error.value = ''
  try { const room = await action(); createRequest = crypto.randomUUID(); await router.push(`/rooms/${room.id}`) }
  catch (e) { error.value = e instanceof Error ? e.message : String(e) }
  finally { busy.value = false }
}
function createRoom() { return act(() => gameApi.create(name.value, createRequest)) }
function joinCode(value: string) { return act(() => gameApi.join(value.trim())) }
onMounted(async () => {
  await load()
  disposers.push(auth.signalR.on('LobbyChanged', () => { void load() }))
  disposers.push(auth.signalR.on('Reconnected', () => { void auth.signalR.invoke('JoinLobby'); void load() }))
  if (await auth.signalR.connect()) await auth.signalR.invoke('JoinLobby')
  else error.value = 'Chưa kết nối trực tuyến. Nhấn tải lại để thử lại.'
})
onUnmounted(() => { disposers.forEach(fn => fn()); void auth.signalR.invoke('LeaveLobby').catch(() => {}) })
</script>
<template>
  <section class="lobby">
    <h1>Sảnh cờ</h1><p>Tạo phòng hoặc nhập mã để chơi với bạn.</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <form class="actions" @submit.prevent="createRoom">
      <label>Tên phòng <input v-model="name" maxlength="80" required></label><button :disabled="busy">Tạo phòng</button>
    </form>
    <form class="actions" @submit.prevent="joinCode(code)">
      <label>Mã phòng <input v-model="code" inputmode="numeric" pattern="[0-9]{6}" maxlength="6" required></label><button :disabled="busy">Tham gia</button>
    </form>
    <button :disabled="busy" @click="load">Tải lại</button>
    <p v-if="loading">Đang tải phòng…</p><p v-else-if="!rooms.length">Chưa có phòng. Hãy tạo phòng đầu tiên.</p>
    <ul class="room-list"><li v-for="room in rooms" :key="room.id"><div><strong>{{ room.name }}</strong><br>Mã {{ room.code }} · {{ room.members.length }}/2 · {{ room.status }}</div>
      <button :disabled="busy || room.members.length === 2 || room.status !== 'waiting'" @click="joinCode(room.code)">Vào phòng</button>
      <button v-if="room.members.some(m => m.userId === auth.user.value?.id)" @click="router.push(`/rooms/${room.id}`)">Trở lại</button>
    </li></ul>
  </section>
</template>
<style scoped>
.lobby{max-width:760px;width:100%;margin:auto}.actions{display:flex;gap:12px;align-items:end;margin:18px 0}label{display:grid;gap:6px;flex:1}input{padding:12px;border:1px solid #bccbc3;border-radius:8px;font:inherit}.room-list{padding:0;list-style:none}.room-list li{display:flex;gap:12px;align-items:center;padding:18px;border:1px solid #d1ddd5;border-radius:12px;margin:12px 0}.room-list li div{flex:1}.error{color:#a12525}button{padding:11px 18px;cursor:pointer}button:disabled{opacity:.5;cursor:default}
</style>
