<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted } from 'vue'
import { useAuth } from '../composables/useAuth'
import { createVideoCall, type VideoSignal } from '../composables/useVideoCall'
const props = defineProps<{ roomId: string; opponentId: string | null }>()
const auth = useAuth(), localVideo = ref<HTMLVideoElement | null>(null), remoteVideo = ref<HTMLVideoElement | null>(null)
const call = createVideoCall({ send: signal => auth.signalR.invoke('Signal', props.roomId, signal) })
const off: (() => void)[] = []
const labels: Record<string,string> = { idle: 'Chưa gọi', incoming: 'Có cuộc gọi đến', preparing: 'Đang mở thiết bị…', calling: 'Đang chờ đối thủ…', connecting: 'Đang kết nối…', connected: 'Đã kết nối' }
watch([call.local, localVideo], () => { if (localVideo.value) localVideo.value.srcObject = call.local.value })
watch([call.remote, remoteVideo], () => { if (remoteVideo.value) remoteVideo.value.srcObject = call.remote.value })
watch(() => props.opponentId, () => { void call.hangup() })
watch(auth.signalR.isConnected, connected => { if (!connected) void call.hangup() })
onMounted(() => {
  off.push(auth.signalR.on('VideoSignalReceived', data => { const s = data as VideoSignal; if (s.roomId === props.roomId && s.recipientId === auth.user.value?.id) void call.receive(s) }))
  off.push(auth.signalR.on('CallEnded', () => { void call.hangup(false) }))
  off.push(auth.signalR.on('RoomStateChanged', data => { if ((data as {status:string}).status === 'closed') void call.hangup(false) }))
})
onUnmounted(() => { off.forEach(fn => fn()); void call.hangup() })
</script>
<template>
  <section class="video-call"><h2>Gọi video</h2><p role="status">{{ labels[call.status.value] }}</p>
    <div class="videos"><div><video ref="localVideo" autoplay muted playsinline aria-label="Video của bạn"/><span>Bạn {{ call.camera.value ? '' : '(tắt camera)' }}</span></div><div><video ref="remoteVideo" autoplay playsinline aria-label="Video đối thủ"/><span>Đối thủ</span></div></div>
    <p v-if="call.error.value" role="alert" class="error">{{ call.error.value }}</p>
    <div v-if="call.status.value==='idle'"><button :disabled="!opponentId || !auth.signalR.isConnected.value" @click="opponentId && call.start(opponentId)">Gọi video</button></div>
    <div v-else-if="call.status.value==='incoming'" class="controls"><button @click="call.accept">Nhận cuộc gọi</button><button @click="call.decline">Từ chối</button></div>
    <div v-else class="controls"><button :disabled="!call.local.value" @click="call.toggle('audio')">{{ call.mic.value ? 'Tắt mic' : 'Bật mic' }}</button><button :disabled="!call.local.value" @click="call.toggle('video')">{{ call.camera.value ? 'Tắt camera' : 'Bật camera' }}</button><button @click="call.hangup()">Kết thúc</button></div>
  </section>
</template>
<style scoped>
.video-call{padding:16px;border:1px solid #c9d7cd;border-radius:12px}h2{font-size:18px;margin-top:0}.videos{display:grid;grid-template-columns:1fr 1fr;gap:8px}.videos div{min-width:0}video{width:100%;aspect-ratio:4/3;background:#203a2d;border-radius:8px;object-fit:cover}.videos span{font-size:12px}.controls{display:flex;flex-wrap:wrap;gap:8px;margin-top:12px}button{padding:9px;cursor:pointer}button:disabled{opacity:.5}.error{color:#a12525;font-size:13px}
</style>
