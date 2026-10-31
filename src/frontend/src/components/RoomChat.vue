<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick } from 'vue'
import { gameApi, type Message } from '../api/game'
import { useAuth } from '../composables/useAuth'
const props = defineProps<{ roomId: string }>()
const auth = useAuth(), messages = ref<Message[]>([]), text = ref(''), error = ref(''), busy = ref(false), list = ref<HTMLElement | null>(null)
let requestId = crypto.randomUUID(), previous = ''
const off: (() => void)[] = []
function receive(message: Message) {
  if (message.roomId !== props.roomId || messages.value.some(m => m.id === message.id)) return
  messages.value = [...messages.value, message].sort((a,b) => a.sequence-b.sequence).slice(-100)
  void nextTick(() => { if (list.value) list.value.scrollTop = list.value.scrollHeight })
}
async function load() { try { (await gameApi.messages(props.roomId)).forEach(receive) } catch (e) { error.value = String(e) } }
async function send() {
  if (busy.value || !text.value.trim()) return
  const content = text.value.trim()
  if (content !== previous) { requestId = crypto.randomUUID(); previous = content }
  busy.value = true; error.value = ''
  try { await auth.signalR.invoke('SendRoomMessage', props.roomId, content, requestId); text.value = ''; previous = ''; requestId = crypto.randomUUID() }
  catch (e) { error.value = e instanceof Error ? e.message : String(e) }
  finally { busy.value = false }
}
onMounted(() => { off.push(auth.signalR.on('RoomMessageReceived', m => receive(m as Message))); off.push(auth.signalR.on('Reconnected', () => { void load() })); void load() })
onUnmounted(() => off.forEach(fn => fn()))
</script>
<template>
  <section class="chat"><h2>Trò chuyện</h2><div ref="list" class="messages" aria-live="polite"><p v-if="!messages.length">Chưa có tin nhắn.</p><p v-for="m in messages" :key="m.id"><strong>{{ m.senderName }}</strong> <small>{{ new Date(m.sentAt).toLocaleTimeString('vi-VN',{hour:'2-digit',minute:'2-digit'}) }}</small><br>{{ m.content }}</p></div>
  <p v-if="error" role="alert" class="error">Gửi thất bại: {{ error }}</p><form @submit.prevent="send"><label class="sr-only" :for="`chat-${roomId}`">Nội dung tin nhắn</label><input :id="`chat-${roomId}`" v-model="text" maxlength="500" placeholder="Nhắn với đối thủ…"><button :disabled="busy || !text.trim() || !auth.signalR.isConnected.value">{{ busy ? 'Đang gửi…' : 'Gửi' }}</button></form></section>
</template>
<style scoped>
.chat{padding:16px;border:1px solid #c9d7cd;border-radius:12px;min-width:0}h2{font-size:18px;margin-top:0}.messages{height:200px;overflow:auto;overflow-wrap:anywhere}small{color:#65746d}form{display:flex;gap:8px}input{min-width:0;flex:1;padding:10px;font:inherit}button{padding:10px;cursor:pointer}.error{color:#a12525;font-size:13px}.sr-only{position:absolute;width:1px;height:1px;overflow:hidden;clip-path:inset(50%)}
</style>
