<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { checkHealth } from './api/health'
import {
  connectToMatch,
  parseMatchReconnectState,
  type MatchPiece,
  type MatchReconnectState,
} from './api/matchReconnect'
import { HubConnectionState, type HubConnection } from '@microsoft/signalr'

const state = ref<'idle' | 'loading' | 'healthy' | 'error'>('idle')
const message = ref('Chưa kiểm tra kết nối máy chủ.')
const matchId = ref('')
const matchConnectionState = ref<'idle' | 'connecting' | 'restored' | 'error'>('idle')
const matchMessage = ref('Chưa khôi phục trận đấu.')
const restoredMatch = ref<MatchReconnectState | null>(null)
let matchConnection: HubConnection | null = null

const pieceLabels: Record<MatchPiece['type'], string> = {
  general: 'Tướng',
  advisor: 'Sĩ',
  elephant: 'Tượng',
  horse: 'Mã',
  chariot: 'Xe',
  cannon: 'Pháo',
  soldier: 'Tốt',
}

const boardRows = Array.from({ length: 10 }, (_, row) =>
  Array.from({ length: 9 }, (_, col) => ({ row, col })))

function pieceAt(row: number, col: number) {
  return restoredMatch.value?.pieces.find(
    (piece) => piece.position.row === row && piece.position.col === col,
  )
}

function displayPiece(piece: MatchPiece) {
  return pieceLabels[piece.type]
}

function pieceLabelAt(row: number, col: number) {
  const piece = pieceAt(row, col)
  return piece ? displayPiece(piece) : ''
}

function pieceSideAt(row: number, col: number) {
  return pieceAt(row, col)?.side
}

function cellLabel(row: number, col: number) {
  const piece = pieceAt(row, col)
  return piece
    ? `${piece.side === 'red' ? 'Đỏ' : 'Đen'} ${displayPiece(piece)}`
    : `Ô ${row + 1}, ${col + 1}`
}

async function restoreMatch() {
  const accessToken = sessionStorage.getItem('gct.accessToken')
  if (!matchId.value || !accessToken) {
    matchConnectionState.value = 'error'
    matchMessage.value = !matchId.value
      ? 'Thiếu MatchId. Mở ứng dụng bằng URL có tham số ?matchId=<id>.'
      : 'Chưa có token xác thực trong phiên hiện tại.'
    return
  }

  matchConnectionState.value = 'connecting'
  matchMessage.value = 'Đang xác thực và đồng bộ trạng thái trận đấu…'
  if (matchConnection?.state === HubConnectionState.Connected) {
    try {
      const state: unknown = await matchConnection.invoke('Reconnect', matchId.value)
      applyRestoredState(parseMatchReconnectState(state))
    } catch (error) {
      showReconnectError(error instanceof Error ? error : new Error('Không thể khôi phục trận đấu.'))
    }
    return
  }

  if (matchConnection) {
    await matchConnection.stop()
    matchConnection = null
  }

  try {
    matchConnection = await connectToMatch(
      matchId.value,
      () => sessionStorage.getItem('gct.accessToken') ?? '',
      applyRestoredState,
      showReconnectError,
    )
  } catch (error) {
    showReconnectError(error instanceof Error ? error : new Error('Không thể kết nối SignalR.'))
  }
}

function applyRestoredState(snapshot: MatchReconnectState) {
  restoredMatch.value = snapshot
  matchConnectionState.value = 'restored'
  matchMessage.value = `Đã khôi phục trận ${snapshot.matchId} · phe ${snapshot.side === 'red' ? 'Đỏ' : 'Đen'}.`
  localStorage.setItem('gct.matchId', snapshot.matchId)
}

function showReconnectError(error: Error) {
  matchConnectionState.value = 'error'
  matchMessage.value = error.message
}

onMounted(() => {
  const queryMatchId = new URLSearchParams(window.location.search).get('matchId')
  matchId.value = queryMatchId ?? localStorage.getItem('gct.matchId') ?? ''
  if (queryMatchId) localStorage.setItem('gct.matchId', queryMatchId)
  if (matchId.value) void restoreMatch()
})

onBeforeUnmount(() => {
  if (matchConnection) void matchConnection.stop()
})

async function connect() {
  state.value = 'loading'
  message.value = 'Đang kết nối…'
  try {
    await checkHealth(import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000', AbortSignal.timeout(5000))
    state.value = 'healthy'
    message.value = 'Kết nối thành công. Máy chủ đang hoạt động.'
  } catch {
    state.value = 'error'
    message.value = 'Chưa kết nối được. Kiểm tra backend, địa chỉ API và cấu hình CORS.'
  }
}
</script>

<template>
  <main class="shell">
    <header><span class="mark" aria-hidden="true">帥</span><span>GAME CỜ TƯỚNG</span></header>
    <section class="intro" aria-labelledby="page-title">
      <p class="eyebrow">KHỞI ĐẦU MỘT VÁN CỜ</p>
      <h1 id="page-title">Mỗi nước đi,<br />một chiến lược.</h1>
      <p class="description">Bộ khung dự án đã sẵn sàng để nhóm phát triển. Tài khoản, phòng chơi và luật cờ sẽ được bổ sung ở các phần tiếp theo.</p>
    </section>
    <section class="connection" aria-labelledby="connection-title">
      <div><span class="tag">GCT-21</span><h2 id="connection-title">Kiểm tra môi trường</h2></div>
      <p class="status" :data-state="state" role="status" aria-live="polite">{{ message }}</p>
      <button type="button" :disabled="state === 'loading'" @click="connect">
        {{ state === 'loading' ? 'Đang kiểm tra…' : 'Kiểm tra máy chủ' }} <span aria-hidden="true">↗</span>
      </button>
      <p class="hint">Kết quả này xác nhận kết nối API; chưa xác nhận cơ sở dữ liệu hoặc chức năng chơi cờ.</p>
    </section>
    <section v-if="matchId" class="match-reconnect" aria-labelledby="match-title">
      <div class="match-heading">
        <div><span class="tag">GCT-75</span><h2 id="match-title">Khôi phục trận đấu</h2></div>
        <button type="button" :disabled="matchConnectionState === 'connecting'" @click="restoreMatch">
          {{ matchConnectionState === 'connecting' ? 'Đang kết nối…' : 'Kết nối lại' }}
        </button>
      </div>
      <p class="status" :data-state="matchConnectionState" role="status" aria-live="polite">{{ matchMessage }}</p>
      <div v-if="restoredMatch" class="match-details">
        <p>
          Lượt {{ restoredMatch.moveNumber }} · đang đi:
          {{ restoredMatch.sideToMove === 'red' ? 'Đỏ' : 'Đen' }} · phiên bản {{ restoredMatch.version }}
          · trạng thái {{ restoredMatch.status === 'paused' ? 'tạm dừng' : 'đang chơi' }}
        </p>
        <div class="chessboard" role="grid" aria-label="Bàn cờ được khôi phục từ máy chủ">
          <div v-for="row in boardRows" :key="row[0].row" class="board-row" role="row">
            <div
              v-for="cell in row"
              :key="`${cell.row}-${cell.col}`"
              class="board-cell"
              role="gridcell"
              :aria-label="cellLabel(cell.row, cell.col)"
            >
              <span
                v-if="pieceAt(cell.row, cell.col)"
                class="chess-piece"
                :data-side="pieceSideAt(cell.row, cell.col)"
              >{{ pieceLabelAt(cell.row, cell.col) }}</span>
            </div>
          </div>
        </div>
      </div>
    </section>
    <footer>Vue · TypeScript · ASP.NET Core <span>Dự án Game Cờ Tướng</span></footer>
  </main>
</template>
