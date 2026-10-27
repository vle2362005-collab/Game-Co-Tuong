<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import {
  HubConnectionBuilder,
  HubConnectionState,
  type HubConnection,
} from '@microsoft/signalr'

interface Piece {
  id: string
  type: 'general' | 'advisor' | 'elephant' | 'horse' | 'chariot' | 'cannon' | 'soldier'
  side: 'red' | 'black'
  position: { col: number; row: number }
}

interface MatchReconnectState {
  matchId: string
  status: string
  yourSide: 'red' | 'black'
  version: number
  moveNumber: number
  sideToMove: 'red' | 'black'
  board: Piece[]
  isPaused: boolean
  redDisconnected: boolean
  redRecoveryRemainingMs: number
  blackDisconnected: boolean
  blackRecoveryRemainingMs: number
  winner: string | null
  resultReason: string | null
}

const props = defineProps<{
  matchId: string
  accessTokenFactory: () => Promise<string>
  apiBaseUrl?: string
}>()

const connectionStatus = ref<'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'error'>('connecting')
const matchState = ref<MatchReconnectState | null>(null)
const errorMessage = ref('')
let connection: HubConnection | undefined
let countdownTimer: ReturnType<typeof setInterval> | undefined

const yourRemainingMs = computed(() => {
  if (!matchState.value) return 0
  return matchState.value.yourSide === 'red'
    ? matchState.value.redRecoveryRemainingMs
    : matchState.value.blackRecoveryRemainingMs
})

const pauseMessage = computed(() => {
  if (!matchState.value?.isPaused) return ''
  if (matchState.value.redDisconnected && matchState.value.blackDisconnected) {
    return 'Cả hai người chơi đã mất kết nối. Trận đấu đang tạm dừng.'
  }
  return matchState.value.redDisconnected
    ? 'Người chơi Đỏ mất kết nối. Trận đấu đang tạm dừng.'
    : 'Người chơi Đen mất kết nối. Trận đấu đang tạm dừng.'
})

function updateState(next: MatchReconnectState) {
  matchState.value = {
    ...next,
    yourSide: matchState.value?.yourSide ?? next.yourSide,
  }
}

function markLocalDisconnect() {
  const state = matchState.value
  if (!state) return

  state.isPaused = true
  if (state.yourSide === 'red') {
    state.redDisconnected = true
  } else {
    state.blackDisconnected = true
  }
}

function formatCountdown(milliseconds: number) {
  const totalSeconds = Math.ceil(Math.max(0, milliseconds) / 1000)
  const minutes = Math.floor(totalSeconds / 60).toString().padStart(2, '0')
  const seconds = (totalSeconds % 60).toString().padStart(2, '0')
  return `${minutes}:${seconds}`
}

function pieceLabel(piece: Piece) {
  const labels: Record<Piece['type'], string> = {
    general: '將',
    advisor: '士',
    elephant: '象',
    horse: '馬',
    chariot: '車',
    cannon: '砲',
    soldier: '卒',
  }
  return labels[piece.type]
}

function tickCountdown() {
  const state = matchState.value
  if (!state?.isPaused) return
  if (state.redDisconnected) {
    state.redRecoveryRemainingMs = Math.max(0, state.redRecoveryRemainingMs - 1000)
  }
  if (state.blackDisconnected) {
    state.blackRecoveryRemainingMs = Math.max(0, state.blackRecoveryRemainingMs - 1000)
  }
}

async function rejoin() {
  if (!connection || connection.state !== HubConnectionState.Connected) return
  const state = await connection.invoke<MatchReconnectState>('ReconnectMatch', props.matchId)
  updateState(state)
  connectionStatus.value = 'connected'
  errorMessage.value = ''
}

async function startConnection() {
  const baseUrl = props.apiBaseUrl ?? import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'
  connection = new HubConnectionBuilder()
    .withUrl(`${baseUrl.replace(/\/$/, '')}/hubs/match`, {
      accessTokenFactory: props.accessTokenFactory,
    })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 20_000])
    .build()

  connection.on('MatchPaused', updateState)
  connection.on('PlayerReconnected', updateState)
  connection.on('MatchEnded', (state: MatchReconnectState) => {
    updateState(state)
    connectionStatus.value = 'connected'
  })
  connection.onreconnecting(() => {
    markLocalDisconnect()
    connectionStatus.value = 'reconnecting'
  })
  connection.onreconnected(async () => {
    connectionStatus.value = 'connected'
    try {
      await rejoin()
    } catch (error) {
      connectionStatus.value = 'error'
      errorMessage.value = error instanceof Error ? error.message : 'Không thể khôi phục trạng thái trận đấu.'
    }
  })
  connection.onclose(() => {
    markLocalDisconnect()
    connectionStatus.value = 'disconnected'
  })

  try {
    await connection.start()
    await rejoin()
  } catch (error) {
    connectionStatus.value = 'error'
    errorMessage.value = error instanceof Error ? error.message : 'Không thể kết nối tới máy chủ trận đấu.'
  }
}

async function retryConnection() {
  if (connection?.state === HubConnectionState.Connected) {
    try {
      await rejoin()
    } catch (error) {
      connectionStatus.value = 'error'
      errorMessage.value = error instanceof Error ? error.message : 'Không thể khôi phục trạng thái trận đấu.'
    }
    return
  }

  await connection?.stop()
  await startConnection()
}

onMounted(() => {
  countdownTimer = setInterval(tickCountdown, 1000)
  void startConnection()
})

onUnmounted(() => {
  if (countdownTimer) clearInterval(countdownTimer)
  void connection?.stop()
})
</script>

<template>
  <section class="reconnect-panel" aria-live="polite">
    <header class="match-heading">
      <div>
        <p class="eyebrow">TRẬN ĐẤU</p>
        <h2>Cờ tướng</h2>
      </div>
      <span class="connection-badge" :data-status="connectionStatus">
        {{ connectionStatus === 'connected' ? 'Đã kết nối' : connectionStatus === 'reconnecting' ? 'Đang nối lại…' : connectionStatus === 'connecting' ? 'Đang kết nối…' : connectionStatus === 'error' ? 'Lỗi kết nối' : 'Mất kết nối' }}
      </span>
    </header>

    <p v-if="errorMessage" class="error-message" role="alert">{{ errorMessage }}</p>
    <button
      v-if="connectionStatus === 'error' || connectionStatus === 'disconnected'"
      class="retry-button"
      type="button"
      @click="retryConnection"
    >Thử kết nối lại</button>
    <template v-if="matchState">
      <aside v-if="matchState.isPaused" class="pause-banner" role="status">
        <strong>{{ pauseMessage }}</strong>
        <span>Thời gian nối lại của bạn còn {{ formatCountdown(yourRemainingMs) }}.</span>
      </aside>
      <aside v-else-if="matchState.status !== 'playing'" class="result-banner" role="status">
        {{ matchState.status === 'cancelled' ? 'Trận đấu đã bị hủy; không có người thắng.' : `Trận đấu đã kết thúc${matchState.winner ? ` — ${matchState.winner === 'red' ? 'Đỏ' : 'Đen'} thắng` : ''}.` }}
      </aside>

      <div class="game-summary">
        <span>Lượt {{ matchState.moveNumber + 1 }}</span>
        <span>Đến lượt: <strong>{{ matchState.sideToMove === 'red' ? 'Đỏ' : 'Đen' }}</strong></span>
        <span>Bạn cầm quân: <strong>{{ matchState.yourSide === 'red' ? 'Đỏ' : 'Đen' }}</strong></span>
      </div>
      <div class="board" role="img" :aria-label="`Bàn cờ, lượt đi ${matchState.sideToMove === 'red' ? 'Đỏ' : 'Đen'}`">
        <span
          v-for="piece in matchState.board"
          :key="piece.id"
          class="piece"
          :class="`piece-${piece.side}`"
          :style="{ gridColumn: piece.position.col + 1, gridRow: piece.position.row + 1 }"
          :aria-label="`${piece.side === 'red' ? 'Đỏ' : 'Đen'} ${piece.type}`"
        >{{ pieceLabel(piece) }}</span>
      </div>
    </template>
  </section>
</template>

<style scoped>
.reconnect-panel { max-width: 700px; margin: 0 auto; padding: 24px; border: 1px solid #d6dcd0; border-radius: 16px; background: #fffef9; color: #172b26; }
.match-heading, .game-summary { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.eyebrow { margin: 0 0 4px; color: #9b4036; font-size: .7rem; font-weight: 700; letter-spacing: .14em; }
h2 { margin: 0; font-size: 1.15rem; }
.connection-badge { border-radius: 99px; padding: 5px 10px; background: #eaf0e7; color: #276440; font-size: .78rem; }
.connection-badge[data-status="reconnecting"], .connection-badge[data-status="connecting"] { background: #fff1d2; color: #80540b; }
.connection-badge[data-status="disconnected"], .connection-badge[data-status="error"] { background: #f8e6e2; color: #9b4036; }
.pause-banner, .result-banner { display: grid; gap: 5px; margin: 18px 0; padding: 14px 16px; border-radius: 9px; background: #fff1d2; color: #68460b; }
.result-banner { background: #eaf0e7; color: #276440; }
.error-message { color: #9b4036; }
.retry-button { margin-bottom: 12px; border: 0; border-radius: 6px; padding: 8px 12px; background: #172b26; color: #fffef9; font: inherit; cursor: pointer; }
.game-summary { justify-content: flex-start; flex-wrap: wrap; margin: 18px 0 12px; color: #53625b; font-size: .85rem; }
.board { position: relative; display: grid; grid-template-columns: repeat(9, minmax(0, 1fr)); grid-template-rows: repeat(10, minmax(30px, 1fr)); aspect-ratio: 9 / 10; max-height: 580px; border: 2px solid #8b5a35; background-color: #f3dfb9; }
.board::before { position: absolute; inset: 5% 5.5556%; background-image: linear-gradient(#9b704b 1px, transparent 1px), linear-gradient(90deg, #9b704b 1px, transparent 1px); background-size: calc(100% / 8) calc(100% / 9); content: ""; pointer-events: none; }
.piece { z-index: 1; align-self: center; justify-self: center; display: grid; place-items: center; width: min(80%, 42px); aspect-ratio: 1; border: 2px solid currentColor; border-radius: 50%; background: #fff9eb; font-family: serif; font-size: clamp(.85rem, 3vw, 1.2rem); font-weight: 700; }
.piece-red { color: #a6322b; }
.piece-black { color: #252c29; }
@media (max-width: 520px) { .reconnect-panel { padding: 16px; } .board { grid-template-rows: repeat(10, minmax(24px, 1fr)); } }
</style>
