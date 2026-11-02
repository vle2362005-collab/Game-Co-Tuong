<script setup lang="ts">
import { onUnmounted, shallowRef, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { apiClient } from '../api/client'
import { createReplay, type ReplayData } from '../composables/useReplay'
import XiangqiBoard from './XiangqiBoard.vue'
const route = useRoute(), router = useRouter()
const replay = shallowRef<ReturnType<typeof createReplay> | null>(null), flipped = ref(false), error = ref(''), loading = ref(false)
let generation = 0
async function load() {
  const current = ++generation, id = String(route.params.id)
  loading.value = true; error.value = ''; replay.value = null
  try {
    const data = await apiClient.get<ReplayData>(`/api/v1/matches/${encodeURIComponent(id)}/history`)
    if (current !== generation) return
    if (data.id !== id) throw new Error('Máy chủ trả dữ liệu của trận khác.')
    replay.value = createReplay(data); flipped.value = data.playerSide === 'black'
  } catch (e) { if (current === generation) error.value = e instanceof Error ? e.message : String(e) }
  finally { if (current === generation) loading.value = false }
}
watch(() => route.params.id, load, { immediate: true })
onUnmounted(() => { generation++ })
</script>
<template>
  <section class="replay"><h1>Xem lại trận đấu</h1><button @click="router.push('/history')">Về lịch sử</button>
    <p v-if="loading" role="status">Đang tải nước đi…</p>
    <p v-if="error" role="alert">{{ error }} <button @click="load">Thử lại</button></p>
    <template v-if="replay">
      <p role="status">Nước {{ replay.cursor.value }}/{{ replay.total }} · Lượt {{ replay.sideToMove.value==='red' ? 'Đỏ' : 'Đen' }} · Chế độ chỉ xem</p>
      <XiangqiBoard :pieces="replay.pieces.value" :flipped="flipped" :disabled="true" :last-move="replay.lastMove.value"/>
      <nav aria-label="Điều khiển phát lại">
        <button :disabled="replay.cursor.value===0" @click="replay.seek(0)">Về đầu</button>
        <button :disabled="replay.cursor.value===0" @click="replay.seek(replay.cursor.value-1)">Lùi một nước</button>
        <button :disabled="replay.cursor.value===replay.total" @click="replay.seek(replay.cursor.value+1)">Tiến một nước</button>
        <button :disabled="replay.cursor.value===replay.total" @click="replay.seek(replay.total)">Tới cuối</button>
      </nav>
      <p>Quân đã bị ăn: {{ replay.captured.value.length }}</p>
      <div class="captured"><img v-for="piece in replay.captured.value" :key="piece.id" :src="`/pieces/${piece.side}-${piece.type}.svg`" :alt="piece.id"/></div>
    </template>
  </section>
</template>
<style scoped>
.replay{max-width:760px;margin:auto}nav{display:flex;flex-wrap:wrap;gap:8px;margin-top:12px}button{padding:9px 12px;cursor:pointer}.captured{display:flex;gap:6px;flex-wrap:wrap}.captured img{width:36px;height:36px}
</style>
