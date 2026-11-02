<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '../composables/useAuth'
import { pveApi, type PveMatch, type Difficulty } from '../api/pve'
import XiangqiBoard from './XiangqiBoard.vue'

// ── state ──────────────────────────────────────────────────────────────────
const router = useRouter()
const auth = useAuth()

const phase = ref<'select' | 'playing' | 'finished'>('select')
const difficulty = ref<Difficulty>('medium')
const match = ref<PveMatch | null>(null)
const selected = ref<string | null>(null)
const error = ref('')
const busy = ref(false)
const botThinking = ref(false)
let offSignalR: (() => void) | null = null

// ── computed ───────────────────────────────────────────────────────────────
const destinations = computed(() => {
  if (!match.value || match.value.sideToMove !== 'red' || busy.value) return []
  return (match.value.legalMoves || [])
    .filter(m => m.pieceId === selected.value)
    .map(m => m.to)
})

const checkedGeneralId = computed(() => {
  if (!match.value?.check) return null
  return match.value.pieces.find(p =>
    p.type.toLowerCase() === 'general' && p.side.toLowerCase() === match.value!.sideToMove
  )?.id ?? null
})

const canMove = computed(() =>
  !!match.value &&
  match.value.status === 'playing' &&
  match.value.sideToMove === 'red' &&
  !busy.value &&
  !botThinking.value
)

const resultLabel = computed(() => {
  const r = match.value?.result
  if (!r) return ''
  const outcome = ({ 'red-win': '🏆 Bạn thắng!', 'black-win': '🤖 Máy thắng!', draw: '🤝 Hòa!' } as Record<string, string>)[r.outcome] ?? 'Kết thúc'
  const reason = ({
    surrender: 'đầu hàng', checkmate: 'chiếu bí', stalemate: 'hết nước đi', 'draw-agreement': 'thỏa thuận hòa'
  } as Record<string, string>)[r.reasonCode] ?? r.reasonCode
  return `${outcome} · ${reason}`
})

const difficultyLabel = computed(() => ({
  easy: '😊 Dễ', medium: '🧠 Trung bình', hard: '🔥 Khó'
}[difficulty.value]))

// ── actions ────────────────────────────────────────────────────────────────
async function startGame() {
  busy.value = true; error.value = ''
  try {
    const res = await pveApi.create(difficulty.value, crypto.randomUUID())
    match.value = res.data
    phase.value = 'playing'
    // Listen for bot moves pushed via SignalR
    offSignalR = auth.signalR.on('PvEMatchChanged', (payload: unknown) => {
      const next = payload as PveMatch
      if (next?.id === match.value?.id && next.version > (match.value?.version ?? 0)) {
        match.value = next
        botThinking.value = false
        if (next.result) phase.value = 'finished'
      }
    })
    if (!auth.signalR.isConnected.value) await auth.signalR.connect()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Không thể tạo trận. Thử lại.'
  } finally { busy.value = false }
}

async function cell(col: number, row: number) {
  if (!canMove.value || !match.value) return
  const piece = match.value.pieces.find(p => p.position.col === col && p.position.row === row)
  // Select own piece
  if (piece?.side.toLowerCase() === 'red') {
    selected.value = selected.value === piece.id ? null : piece.id
    error.value = ''
    return
  }
  if (!selected.value) return
  // Validate legal move
  const isLegal = match.value.legalMoves?.some(
    m => m.pieceId === selected.value && m.to.col === col && m.to.row === row
  )
  if (!isLegal) { error.value = 'Nước đi không hợp lệ.'; return }

  error.value = ''
  busy.value = true
  botThinking.value = true
  const pieceId = selected.value
  selected.value = null
  try {
    const res = await pveApi.move(
      match.value.id, pieceId, col, row,
      match.value.version, crypto.randomUUID(), difficulty.value
    )
    match.value = res.data
    if (res.data.result) { phase.value = 'finished'; botThinking.value = false }
    // botThinking stays true until SignalR pushes the bot move (or timeout below)
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Lỗi gửi nước đi.'
    botThinking.value = false
  } finally { busy.value = false }
}

async function surrender() {
  if (!match.value || !window.confirm('Bạn xác nhận đầu hàng?')) return
  busy.value = true; error.value = ''
  try {
    const res = await pveApi.surrender(match.value.id)
    match.value = res.data
    phase.value = 'finished'
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Lỗi đầu hàng.'
  } finally { busy.value = false }
}

async function refresh() {
  if (!match.value) return
  try {
    const res = await pveApi.match(match.value.id)
    match.value = res.data
    botThinking.value = false
    if (res.data.result) phase.value = 'finished'
  } catch { /* silent */ }
}

function backToLobby() { router.push('/lobby') }
function playAgain() { phase.value = 'select'; match.value = null; selected.value = null; error.value = '' }

// Fallback polling: if SignalR bot move not received within 12s, poll manually
let botTimeout: ReturnType<typeof setTimeout> | null = null
function scheduleBotTimeout() {
  if (botTimeout) clearTimeout(botTimeout)
  if (botThinking.value) {
    botTimeout = setTimeout(async () => { if (botThinking.value) await refresh() }, 12000)
  }
}
import { watch } from 'vue'
watch(botThinking, val => { if (val) scheduleBotTimeout(); else { if (botTimeout) clearTimeout(botTimeout) } })

onMounted(async () => {
  if (!auth.signalR.isConnected.value) await auth.signalR.connect()
})
onUnmounted(() => { offSignalR?.(); if (botTimeout) clearTimeout(botTimeout) })
</script>

<template>
  <section class="bot-game">
    <!-- ── Phase: SELECT DIFFICULTY ── -->
    <div v-if="phase === 'select'" class="select-screen">
      <div class="select-card">
        <div class="select-header">
          <button class="btn-back" @click="backToLobby">← Sảnh</button>
          <h1>🤖 Chơi với Máy</h1>
          <p class="subtitle">Người cầm <span class="side-red">Đỏ</span> · Máy cầm <span class="side-black">Đen</span></p>
        </div>

        <div class="difficulty-group">
          <p class="label">Chọn mức độ khó:</p>
          <div class="difficulty-cards">
            <button
              v-for="d in (['easy', 'medium', 'hard'] as Difficulty[])"
              :key="d"
              class="diff-card"
              :class="{ active: difficulty === d }"
              @click="difficulty = d"
            >
              <span class="diff-icon">{{ d === 'easy' ? '😊' : d === 'medium' ? '🧠' : '🔥' }}</span>
              <span class="diff-name">{{ d === 'easy' ? 'Dễ' : d === 'medium' ? 'Trung bình' : 'Khó' }}</span>
              <span class="diff-desc">{{ d === 'easy' ? 'Máy đi ngẫu nhiên' : d === 'medium' ? 'Máy suy nghĩ 2 nước' : 'Máy suy nghĩ 3 nước' }}</span>
            </button>
          </div>
        </div>

        <p v-if="error" role="alert" class="error">{{ error }}</p>
        <button class="btn-start" :disabled="busy" @click="startGame" id="bot-start-btn">
          <span v-if="busy">Đang tạo trận…</span>
          <span v-else>▶ Bắt đầu</span>
        </button>
      </div>
    </div>

    <!-- ── Phase: PLAYING ── -->
    <div v-else-if="phase === 'playing' && match" class="play-screen">
      <div class="game-header">
        <div class="header-left">
          <span class="badge-mode">🤖 vs Máy · {{ difficultyLabel }}</span>
          <span v-if="match.sideToMove === 'red'" class="turn-indicator red">Lượt của bạn ♟</span>
          <span v-else class="turn-indicator black">
            <span v-if="botThinking" class="thinking-dots">Máy đang suy nghĩ<span class="dot">.</span><span class="dot">.</span><span class="dot">.</span></span>
            <span v-else>Lượt máy 🤖</span>
          </span>
          <strong v-if="match.check" class="check-alert">⚠ Chiếu Tướng!</strong>
        </div>
        <div class="header-right">
          <span class="move-count">Nước {{ match.moveNumber }}</span>
        </div>
      </div>

      <p v-if="error" role="alert" class="error">{{ error }} <button @click="refresh">Tải lại</button></p>

      <div class="board-wrapper">
        <XiangqiBoard
          :pieces="match.pieces"
          :flipped="false"
          :selected="selected"
          :disabled="!canMove"
          :destinations="destinations"
          :last-move="match.lastMove"
          :checked-general-id="checkedGeneralId"
          @cell="cell"
        />
      </div>

      <div class="controls">
        <button class="btn-surrender" :disabled="busy || botThinking" @click="surrender" id="bot-surrender-btn">
          🏳 Đầu hàng
        </button>
        <button class="btn-refresh" :disabled="busy" @click="refresh">
          🔄 Đồng bộ
        </button>
      </div>

      <div class="legend">
        <span class="legend-you">■ Bạn (Đỏ)</span>
        <span class="legend-bot">■ Máy (Đen)</span>
      </div>
    </div>

    <!-- ── Phase: FINISHED ── -->
    <div v-else-if="phase === 'finished' && match" class="result-screen">
      <div class="result-card">
        <div class="result-emoji">
          {{ match.result?.outcome === 'red-win' ? '🏆' : match.result?.outcome === 'black-win' ? '😔' : '🤝' }}
        </div>
        <h2 class="result-title">{{ resultLabel }}</h2>
        <p class="result-moves">Ván cờ kết thúc sau {{ match.moveNumber }} nước · {{ difficultyLabel }}</p>
        <div class="result-actions">
          <button class="btn-start" @click="playAgain" id="bot-play-again-btn">🔁 Chơi lại</button>
          <button class="btn-history" @click="router.push('/history')" id="bot-history-btn">📋 Xem lịch sử</button>
          <button class="btn-back-lobby" @click="backToLobby" id="bot-lobby-btn">🏠 Về sảnh</button>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.bot-game { width: 100%; min-height: 60vh; display: flex; flex-direction: column; align-items: center; padding: 24px 16px; }

/* ── SELECT ── */
.select-screen { width: 100%; max-width: 520px; }
.select-card { background: #fffef9; border: 1px solid #d6dcd0; border-radius: 20px; padding: 36px 32px; box-shadow: 0 4px 20px rgba(23,43,38,.07); }
.select-header { text-align: center; margin-bottom: 28px; }
.select-header h1 { margin: 8px 0 4px; font-size: 1.7rem; color: #172b26; }
.subtitle { color: #65746d; margin: 0; font-size: .9rem; }
.side-red { color: #9b4036; font-weight: 700; }
.side-black { color: #172b26; font-weight: 700; }
.btn-back { background: transparent; border: 1px solid #d6dcd0; border-radius: 8px; padding: 6px 12px; font-size: .82rem; color: #53625b; cursor: pointer; }
.btn-back:hover { background: #eaf0e7; }

.label { font-weight: 600; color: #172b26; margin-bottom: 12px; font-size: .9rem; }
.difficulty-cards { display: flex; gap: 12px; margin-bottom: 28px; }
.diff-card { flex: 1; display: flex; flex-direction: column; align-items: center; gap: 4px; padding: 16px 8px; border: 2px solid #d6dcd0; border-radius: 14px; background: #fbfaf6; cursor: pointer; transition: all .18s; }
.diff-card:hover { border-color: #9b4036; }
.diff-card.active { border-color: #9b4036; background: #fdf3f2; }
.diff-icon { font-size: 1.6rem; }
.diff-name { font-weight: 700; font-size: .9rem; color: #172b26; }
.diff-desc { font-size: .74rem; color: #65746d; text-align: center; }

.btn-start { width: 100%; padding: 14px; background: #9b4036; color: #fff; border: none; border-radius: 12px; font-size: 1rem; font-weight: 700; cursor: pointer; transition: background .2s; }
.btn-start:hover:not(:disabled) { background: #7d3229; }
.btn-start:disabled { opacity: .55; cursor: default; }

/* ── PLAYING ── */
.play-screen { width: 100%; max-width: 560px; }
.game-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; padding: 12px 16px; background: #fffef9; border: 1px solid #e5ebe0; border-radius: 12px; }
.header-left { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
.badge-mode { font-size: .78rem; font-weight: 600; background: #172b26; color: #fff; padding: 3px 10px; border-radius: 20px; }
.turn-indicator { font-size: .85rem; font-weight: 600; }
.turn-indicator.red { color: #9b4036; }
.turn-indicator.black { color: #2d5246; }
.check-alert { color: #c0392b; font-size: .85rem; animation: pulse .7s infinite alternate; }
@keyframes pulse { from { opacity: 1; } to { opacity: .5; } }
.move-count { font-size: .78rem; color: #65746d; }

/* Bot thinking dots */
.thinking-dots .dot { display: inline-block; animation: blink 1.2s infinite; }
.thinking-dots .dot:nth-child(2) { animation-delay: .4s; }
.thinking-dots .dot:nth-child(3) { animation-delay: .8s; }
@keyframes blink { 0%,80%,100% { opacity: 0; } 40% { opacity: 1; } }

.board-wrapper { width: 100%; }
.controls { display: flex; gap: 12px; justify-content: center; margin-top: 16px; }
.btn-surrender { padding: 10px 20px; background: #fdf3f2; border: 1px solid #d99; border-radius: 10px; color: #9b4036; font-weight: 600; cursor: pointer; transition: all .18s; }
.btn-surrender:hover:not(:disabled) { background: #9b4036; color: #fff; }
.btn-surrender:disabled { opacity: .5; cursor: default; }
.btn-refresh { padding: 10px 16px; background: #fbfaf6; border: 1px solid #d6dcd0; border-radius: 10px; color: #53625b; cursor: pointer; font-size: .84rem; }
.btn-refresh:hover { background: #eaf0e7; }

.legend { display: flex; gap: 20px; justify-content: center; margin-top: 10px; font-size: .8rem; }
.legend-you { color: #9b4036; font-weight: 600; }
.legend-bot { color: #172b26; font-weight: 600; }

/* ── FINISHED ── */
.result-screen { width: 100%; max-width: 440px; display: flex; justify-content: center; }
.result-card { background: #fffef9; border: 1px solid #d6dcd0; border-radius: 20px; padding: 44px 36px; text-align: center; box-shadow: 0 4px 24px rgba(23,43,38,.09); width: 100%; }
.result-emoji { font-size: 3.5rem; margin-bottom: 12px; }
.result-title { margin: 0 0 8px; font-size: 1.4rem; color: #172b26; }
.result-moves { color: #65746d; font-size: .88rem; margin-bottom: 28px; }
.result-actions { display: flex; flex-direction: column; gap: 12px; }
.btn-history { padding: 12px; border: 1px solid #d6dcd0; border-radius: 12px; background: #fbfaf6; color: #53625b; font-size: .9rem; cursor: pointer; }
.btn-history:hover { background: #eaf0e7; }
.btn-back-lobby { padding: 12px; border: 1px solid #d6dcd0; border-radius: 12px; background: transparent; color: #65746d; font-size: .88rem; cursor: pointer; }
.btn-back-lobby:hover { background: #f5f5f5; }

.error { color: #a12525; font-size: .88rem; margin-bottom: 8px; }
.error button { margin-left: 8px; padding: 4px 10px; font-size: .8rem; cursor: pointer; }

.difficulty-group { margin-bottom: 8px; }

@media (max-width: 480px) {
  .difficulty-cards { flex-direction: column; }
  .select-card { padding: 24px 18px; }
}
</style>
