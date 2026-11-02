<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { gameApi, type MatchReplay } from '../api/game'
import XiangqiBoard from './XiangqiBoard.vue'
import { buildReplayFrames, type ReplayFrame } from '../replay/replay'

const route = useRoute()
const router = useRouter()
const replay = ref<MatchReplay | null>(null)
const frames = ref<ReplayFrame[]>([])
const frameIndex = ref(0)
const error = ref('')
const loading = ref(false)
let requestVersion = 0

const frame = computed(() => frames.value[frameIndex.value] ?? null)
const totalMoves = computed(() => Math.max(0, frames.value.length - 1))
const resultLabel = computed(() => {
  const result = replay.value?.result
  if (!result) return replay.value?.outcome === 'cancelled' ? 'Trận đã hủy' : 'Trận đã kết thúc'
  const outcome = ({
    'red-win': 'Đỏ thắng',
    'black-win': 'Đen thắng',
    draw: 'Hòa',
    cancelled: 'Trận đã hủy'
  } as Record<string, string>)[result.outcome] || 'Trận đã kết thúc'
  const reason = ({
    surrender: 'đầu hàng',
    'draw-agreement': 'thỏa thuận hòa',
    checkmate: 'chiếu bí',
    stalemate: 'hết nước đi',
    timeout: 'hết thời gian'
  } as Record<string, string>)[result.reasonCode] || result.reasonCode
  return `${outcome}${reason ? ` · ${reason}` : ''}`
})

async function loadReplay(id: string) {
  const currentRequest = ++requestVersion
  loading.value = true
  error.value = ''
  replay.value = null
  frames.value = []
  frameIndex.value = 0

  try {
    const data = await gameApi.matchReplay(id)
    if (currentRequest !== requestVersion) return
    const terminal = ['finished', 'completed', 'ended', 'cancelled', 'canceled'].includes(data.status.toLowerCase())
    if (!terminal) throw new Error('Chỉ có thể xem lại ván đã kết thúc.')
    const nextFrames = buildReplayFrames(data.initialBoardJson, data.currentBoardJson, data.moves)
    replay.value = data
    frames.value = nextFrames
  } catch (cause) {
    if (currentRequest === requestVersion)
      error.value = cause instanceof Error ? cause.message : 'Không thể tải dữ liệu xem lại trận.'
  } finally {
    if (currentRequest === requestVersion) loading.value = false
  }
}

function goFirst() { frameIndex.value = 0 }
function goPrevious() { frameIndex.value = Math.max(0, frameIndex.value - 1) }
function goNext() { frameIndex.value = Math.min(totalMoves.value, frameIndex.value + 1) }
function goLast() { frameIndex.value = totalMoves.value }
function sideLabel(side: string) { return side.toLowerCase() === 'red' ? 'Đỏ' : 'Đen' }
function pieceLabel(type: string) {
  return ({
    general: 'Tướng',
    advisor: 'Sĩ',
    elephant: 'Tượng',
    horse: 'Mã',
    chariot: 'Xe',
    cannon: 'Pháo',
    soldier: 'Tốt'
  } as Record<string, string>)[type.toLowerCase()] || type
}

watch(() => String(route.params.id), id => { void loadReplay(id) }, { immediate: true })
onUnmounted(() => { requestVersion++ })
</script>

<template>
  <section class="replay" aria-labelledby="replay-title">
    <header class="replay-header">
      <div>
        <h1 id="replay-title">Xem lại ván cờ</h1>
        <p v-if="replay" role="status">{{ resultLabel }} · {{ totalMoves }} nước</p>
      </div>
      <button type="button" @click="router.push('/history')">← Lịch sử</button>
    </header>

    <p v-if="loading" role="status">Đang tải dữ liệu trận…</p>
    <p v-if="error" role="alert" class="error">{{ error }} <button type="button" @click="loadReplay(String(route.params.id))">Thử tải lại</button></p>

    <template v-if="frame">
      <p class="turn" role="status">
        <template v-if="frame.lastMove">
          Nước {{ frame.ply }} · {{ sideLabel(frame.lastMove.side) }} vừa đi
          <span v-if="frame.capturedPiece">· Ăn {{ sideLabel(frame.capturedPiece.side) }} {{ pieceLabel(frame.capturedPiece.type) }}</span>
        </template>
        <template v-else>Vị trí ban đầu</template>
        · Lượt {{ sideLabel(frame.sideToMove) }}
      </p>

      <XiangqiBoard :pieces="frame.pieces" :flipped="replay?.playerSide.toLowerCase() === 'black'"
        :disabled="true" :last-move="frame.lastMove" />

      <nav class="replay-controls" aria-label="Điều khiển xem lại">
        <button type="button" aria-label="Tới đầu" :disabled="frameIndex === 0" @click="goFirst">|◀ Đầu</button>
        <button type="button" aria-label="Lùi một nước" :disabled="frameIndex === 0" @click="goPrevious">◀ Lùi</button>
        <span>Nước {{ frameIndex }}/{{ totalMoves }}</span>
        <button type="button" aria-label="Tiến một nước" :disabled="frameIndex === totalMoves" @click="goNext">Tiến ▶</button>
        <button type="button" aria-label="Tới cuối" :disabled="frameIndex === totalMoves" @click="goLast">Cuối ▶|</button>
      </nav>
    </template>
  </section>
</template>

<style scoped>
.replay{width:100%;max-width:900px;margin:0 auto}.replay-header{display:flex;justify-content:space-between;align-items:center;gap:16px}.replay-header h1{margin-bottom:4px}.replay-header p,.turn{color:#4d554d}.replay button{padding:10px 16px;cursor:pointer}.replay button:disabled{cursor:not-allowed;opacity:.5}.turn{text-align:center;margin:14px 0}.replay-controls{display:flex;justify-content:center;align-items:center;gap:10px;flex-wrap:wrap;margin:18px 0}.replay-controls span{min-width:110px;text-align:center}.error{color:#a12525}
</style>
