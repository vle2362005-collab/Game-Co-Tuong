<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import type { User, MatchHistoryItem, MatchHistoryPage } from '../types/auth'
import { apiClient, ApiClientError } from '../api/client'

defineProps<{
  user?: User | null
}>()

const router = useRouter()

const filter = ref<'all' | 'win' | 'loss' | 'draw' | 'cancelled' | 'in-progress'>('all')
const modeFilter = ref<'all' | 'pvp' | 'pve'>('all')
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)
const historyList = ref<MatchHistoryItem[]>([])
const page = ref(1)
const pageSize = 10
const totalCount = ref(0)
const totalPages = ref(0)

async function loadHistory() {
  isLoading.value = true
  errorMessage.value = null

  try {
    const query = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize) })
    if (filter.value !== 'all') query.set('outcome', filter.value)
    if (modeFilter.value !== 'all') query.set('mode', modeFilter.value)
    const data = await apiClient.get<MatchHistoryPage>(`/api/v1/matches/history?${query}`, {
      timeoutMs: 5000,
      retries: 1
    })
    historyList.value = data.items
    totalCount.value = data.totalCount
    totalPages.value = data.totalPages
  } catch (err: unknown) {
    if (err instanceof ApiClientError) {
      historyList.value = []
      totalCount.value = 0
      totalPages.value = 0
      errorMessage.value = `Không thể tải lịch sử từ máy chủ (${err.message}).`
    } else {
      historyList.value = []
      totalCount.value = 0
      totalPages.value = 0
      errorMessage.value = 'Không thể tải lịch sử đấu.'
    }
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  loadHistory()
})

watch([filter, modeFilter], () => {
  page.value = 1
  loadHistory()
})

function changePage(nextPage: number) {
  if (nextPage < 1 || nextPage > totalPages.value || nextPage === page.value) return
  page.value = nextPage
  loadHistory()
}

function resultBadgeClass(result: string) {
  if (result === 'win') return 'win'
  if (result === 'loss') return 'loss'
  if (result === 'cancelled') return 'cancelled'
  return 'draw'
}

function resultText(result: string) {
  if (result === 'win') return 'Thắng'
  if (result === 'loss') return 'Thua'
  if (result === 'cancelled') return 'Đã hủy'
  if (result === 'in-progress') return 'Đang đấu'
  return 'Hòa'
}

function reasonText(item: MatchHistoryItem) {
  const labels: Record<string, string> = {
    surrender: 'Đầu hàng',
    'draw-agreement': 'Thỏa thuận hòa',
    checkmate: 'Chiếu bí',
    stalemate: 'Hết nước đi',
    timeout: 'Hết thời gian',
    cancellation: 'Đã hủy'
  }
  return labels[item.reasonCode || ''] || item.resultReason || 'Chưa có kết quả'
}

function playedAt(item: MatchHistoryItem) {
  return new Date(item.startedAt).toLocaleString('vi-VN')
}

function modeText(item: MatchHistoryItem) {
  if (item.mode !== 'ai' && item.mode !== 'pve') return 'Đấu với người'
  const difficulty = (({ easy: 'Dễ', medium: 'Vừa', hard: 'Khó' } as Record<string, string>)[item.aiDifficulty || ''] || item.aiDifficulty)
  return difficulty ? `Đấu với máy · Mức ${difficulty}` : 'Đấu với máy'
}
</script>

<template>
  <div class="history-container">
    <div class="history-header">
      <div>
        <h2>Lịch sử ván đấu</h2>
        <p class="subtitle">Xem lại thành tích và các ván cờ đã tham gia</p>
      </div>

      <button
        type="button"
        class="btn-back"
        id="history-back-btn"
        @click="router.push('/lobby')"
      >
        ← Quay lại sảnh
      </button>
    </div>

    <!-- Notice / Error banner if any -->
    <div v-if="errorMessage" class="notice-banner">
      <span>ℹ️ {{ errorMessage }}</span>
      <button type="button" class="btn-retry" id="history-retry-btn" @click="loadHistory">
        Thử lại 🔄
      </button>
    </div>

    <!-- Filter Bar -->
    <div class="filter-bar">
      <button
        type="button"
        class="filter-tab"
        :class="{ active: filter === 'all' }"
        @click="filter = 'all'"
      >
        Tất cả<span v-if="filter === 'all'"> ({{ totalCount }})</span>
      </button>
      <button
        type="button"
        class="filter-tab"
        :class="{ active: filter === 'win' }"
        @click="filter = 'win'"
      >
        Thắng
      </button>
      <button
        type="button"
        class="filter-tab"
        :class="{ active: filter === 'loss' }"
        @click="filter = 'loss'"
      >
        Thua
      </button>
      <button
        type="button"
        class="filter-tab"
        :class="{ active: filter === 'draw' }"
        @click="filter = 'draw'"
      >
        Hòa
      </button>
      <button
        type="button"
        class="filter-tab"
        :class="{ active: filter === 'cancelled' }"
        @click="filter = 'cancelled'"
      >
        Đã hủy
      </button>
      <button
        type="button"
        class="filter-tab"
        :class="{ active: filter === 'in-progress' }"
        @click="filter = 'in-progress'"
      >
        Đang đấu
      </button>
      <select v-model="modeFilter" class="mode-filter" aria-label="Lọc loại trận">
        <option value="all">Mọi loại trận</option>
        <option value="pvp">Đấu người</option>
        <option value="pve">Đấu máy</option>
      </select>
    </div>

    <!-- Loading State -->
    <div v-if="isLoading" class="loading-state">
      <span class="spinner"></span>
      <p>Đang tải dữ liệu lịch sử ván cờ...</p>
    </div>

    <!-- List of Matches -->
    <div v-else-if="historyList.length > 0" class="history-list">
      <div v-for="item in historyList" :key="item.id" class="history-item">
        <div class="item-status">
          <span class="result-badge" :class="resultBadgeClass(item.outcome)">
            {{ resultText(item.outcome) }}
          </span>
          <span class="side-tag" :class="item.playerSide">
            {{ item.playerSide === 'red' ? 'Phe Đỏ' : 'Phe Đen' }}
          </span>
        </div>

        <div class="item-details">
          <h4>vs {{ item.mode === 'ai' || item.mode === 'pve' ? 'Máy' : item.opponent?.displayName || 'Kỳ thủ' }}</h4>
          <p class="item-meta">
            <span>{{ modeText(item) }}</span> ·
            <span>{{ reasonText(item) }}</span> ·
            <span>{{ item.moveNumber }} nước</span> ·
            <span>{{ playedAt(item) }}</span>
          </p>
          <button type="button" class="btn-review" @click="router.push(item.outcome==='in-progress' ? `/game/${item.id}` : `/history/${item.id}/replay`)">{{ item.outcome==='in-progress' ? 'Tiếp tục ván' : 'Xem lại ván' }}</button>
        </div>
      </div>
    </div>

    <div v-if="!isLoading && totalCount > 0" class="pagination">
      <span>{{ totalCount }} ván · Trang {{ page }}/{{ totalPages }}</span>
      <div>
        <button type="button" :disabled="page <= 1" @click="changePage(page - 1)">Trước</button>
        <button type="button" :disabled="page >= totalPages" @click="changePage(page + 1)">Sau</button>
      </div>
    </div>

    <!-- Empty State -->
    <div v-else-if="!errorMessage" class="empty-state">
      <p>Chưa có ván đấu nào phù hợp với bộ lọc.</p>
    </div>
  </div>
</template>

<style scoped>
.history-container {
  background: #fffef9;
  border: 1px solid #d6dcd0;
  border-radius: 16px;
  padding: 32px 28px;
  max-width: 680px;
  margin: 32px auto;
  box-shadow: 0 4px 16px rgba(23, 43, 38, 0.05);
}

.history-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 20px;
  border-bottom: 1px solid #eaf0e7;
  margin-bottom: 20px;
}

h2 {
  font-family: Georgia, "Times New Roman", serif;
  margin: 0 0 4px;
  color: #172b26;
  font-size: 1.4rem;
}

.subtitle {
  margin: 0;
  font-size: 0.88rem;
  color: #65746d;
}

.btn-back {
  background: #fbfaf6;
  color: #53625b;
  border: 1px solid #d6dcd0;
  border-radius: 8px;
  padding: 8px 14px;
  font-size: 0.84rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-back:hover {
  background: #eaf0e7;
  color: #172b26;
}

.notice-banner {
  background: #fffcf0;
  border: 1px solid #edd99c;
  border-radius: 8px;
  padding: 10px 14px;
  margin-bottom: 18px;
  font-size: 0.82rem;
  color: #795b13;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.btn-retry {
  background: transparent;
  border: 1px solid #795b13;
  border-radius: 6px;
  color: #795b13;
  padding: 4px 8px;
  font-size: 0.78rem;
  font-weight: 600;
  cursor: pointer;
}

.btn-retry:hover {
  background: #edd99c;
}

.filter-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-bottom: 20px;
}

.mode-filter {
  border: 1px solid #d6dcd0;
  border-radius: 20px;
  padding: 6px 12px;
  color: #53625b;
  background: #fffef9;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 20px;
  color: #65746d;
  font-size: 0.82rem;
}

.pagination div {
  display: flex;
  gap: 8px;
}

.pagination button {
  border: 1px solid #d6dcd0;
  border-radius: 6px;
  padding: 6px 10px;
  background: #fffef9;
  color: #53625b;
  cursor: pointer;
}

.pagination button:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.filter-tab {
  background: transparent;
  border: 1px solid #d6dcd0;
  border-radius: 20px;
  padding: 6px 14px;
  font-size: 0.82rem;
  font-weight: 600;
  color: #53625b;
  cursor: pointer;
  transition: all 0.2s;
}

.filter-tab:hover {
  border-color: #9b4036;
  color: #9b4036;
}

.filter-tab.active {
  background: #9b4036;
  border-color: #9b4036;
  color: #ffffff;
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.history-item {
  background: #fbfaf6;
  border: 1px solid #e5ebe0;
  border-radius: 10px;
  padding: 14px 18px;
  display: flex;
  align-items: center;
  gap: 16px;
  transition: border-color 0.2s;
}

.history-item:hover {
  border-color: #b0baa8;
}

.item-status {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  min-width: 64px;
}

.result-badge {
  font-size: 0.76rem;
  font-weight: 700;
  padding: 3px 8px;
  border-radius: 6px;
  text-align: center;
  width: 100%;
}
.result-badge.cancelled { background: #e8e8e8; color: #555; }
.btn-review { margin-top: 8px; padding: 6px 10px; cursor: pointer; }

.result-badge.win {
  background: #eaf6ea;
  color: #276440;
}

.result-badge.loss {
  background: #fdf3f2;
  color: #9b4036;
}

.result-badge.draw {
  background: #f0f0f0;
  color: #555555;
}

.side-tag {
  font-size: 0.7rem;
  font-weight: 600;
}

.side-tag.red {
  color: #9b4036;
}

.side-tag.black {
  color: #172b26;
}

.item-details h4 {
  margin: 0 0 4px;
  font-size: 0.96rem;
  color: #172b26;
}

.item-meta {
  margin: 0;
  font-size: 0.8rem;
  color: #65746d;
}

.loading-state,
.empty-state {
  text-align: center;
  padding: 36px 0;
  color: #65746d;
  font-size: 0.9rem;
}

.spinner {
  display: inline-block;
  width: 24px;
  height: 24px;
  border: 3px solid #eaf0e7;
  border-top-color: #9b4036;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 8px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
