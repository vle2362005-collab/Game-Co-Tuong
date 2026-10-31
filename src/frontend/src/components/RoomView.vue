<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { User, RoomInfo } from '../types/auth'

const props = defineProps<{
  user?: User | null
}>()

const emit = defineEmits<{
  (e: 'startMatch', payload: { roomId: string; side: 'red' | 'black'; opponentName: string }): void
  (e: 'leaveRoom'): void
}>()

const route = useRoute()
const router = useRouter()

const roomId = computed(() => (route.params.id as string) || 'demo-room-01')
const roomCode = computed(() => roomId.value.slice(0, 6).toUpperCase())

const selectedSide = ref<'red' | 'black'>('red')
const isReady = ref(true)

const roomInfo = ref<RoomInfo>({
  id: roomId.value,
  code: roomCode.value,
  name: 'Kỳ đài đàm đạo',
  hostId: props.user?.id || 'host-user-id',
  hostName: props.user?.displayName || 'Kỳ Thủ Chủ Nhà',
  status: 'waiting',
  membersCount: 2
})

function handleStartMatch() {
  const opponentName = selectedSide.value === 'red' ? 'Kỳ thủ Đen (Đối thủ)' : 'Kỳ thủ Đỏ (Đối thủ)'
  emit('startMatch', {
    roomId: roomId.value,
    side: selectedSide.value,
    opponentName
  })
  router.push(`/game/${roomId.value}`)
}

function handleLeaveRoom() {
  emit('leaveRoom')
  router.push('/lobby')
}
</script>

<template>
  <div class="room-container">
    <!-- Room Header -->
    <div class="room-card-header">
      <div class="room-meta">
        <span class="room-badge">Phòng cờ #{{ roomInfo.code }}</span>
        <h2>{{ roomInfo.name }}</h2>
        <p class="room-host">Chủ phòng: <strong>{{ roomInfo.hostName }}</strong> · Người tham gia: {{ roomInfo.membersCount }}/2</p>
      </div>

      <button
        type="button"
        class="btn-leave"
        id="room-leave-btn"
        title="Rời khỏi phòng cờ"
        @click="handleLeaveRoom"
      >
        🚪 Rời phòng
      </button>
    </div>

    <!-- Seats / Slots -->
    <div class="seats-grid">
      <!-- Red Seat -->
      <div
        class="seat-box"
        :class="{ selected: selectedSide === 'red' }"
        @click="selectedSide = 'red'"
      >
        <div class="seat-badge red">
          <span class="piece-icon">帥</span>
          <span>Phe Đỏ (Đi Tiên)</span>
        </div>
        <p class="seat-occupant">
          {{ selectedSide === 'red' ? (user?.displayName || 'Bạn') : 'Kỳ thủ chờ ghép' }}
        </p>
        <span v-if="selectedSide === 'red'" class="tag-you">Vị trí của bạn</span>
      </div>

      <!-- Black Seat -->
      <div
        class="seat-box"
        :class="{ selected: selectedSide === 'black' }"
        @click="selectedSide = 'black'"
      >
        <div class="seat-badge black">
          <span class="piece-icon">將</span>
          <span>Phe Đen (Đi Hậu)</span>
        </div>
        <p class="seat-occupant">
          {{ selectedSide === 'black' ? (user?.displayName || 'Bạn') : 'Kỳ thủ chờ ghép' }}
        </p>
        <span v-if="selectedSide === 'black'" class="tag-you">Vị trí của bạn</span>
      </div>
    </div>

    <!-- Action Section -->
    <div class="room-actions">
      <button
        type="button"
        class="btn-start"
        id="room-start-btn"
        :disabled="!isReady"
        @click="handleStartMatch"
      >
        ⚔️ Bắt đầu ván cờ ngay
      </button>
    </div>
  </div>
</template>

<style scoped>
.room-container {
  background: #fffef9;
  border: 1px solid #d6dcd0;
  border-radius: 16px;
  padding: 32px 28px;
  max-width: 680px;
  margin: 32px auto;
  box-shadow: 0 4px 16px rgba(23, 43, 38, 0.05);
}

.room-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding-bottom: 24px;
  border-bottom: 1px solid #eaf0e7;
  margin-bottom: 24px;
}

.room-badge {
  display: inline-block;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  color: #9b4036;
  background: #fdf3f2;
  border: 1px solid #eccbc8;
  padding: 3px 10px;
  border-radius: 12px;
  margin-bottom: 8px;
}

h2 {
  font-family: Georgia, "Times New Roman", serif;
  margin: 0 0 6px;
  color: #172b26;
  font-size: 1.4rem;
}

.room-host {
  margin: 0;
  font-size: 0.88rem;
  color: #53625b;
}

.btn-leave {
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

.btn-leave:hover {
  background: #fdf3f2;
  color: #9b4036;
  border-color: #eccbc8;
}

.seats-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  margin-bottom: 28px;
}

@media (max-width: 600px) {
  .seats-grid {
    grid-template-columns: 1fr;
  }
}

.seat-box {
  background: #fbfaf6;
  border: 2px solid #e5ebe0;
  border-radius: 12px;
  padding: 20px;
  cursor: pointer;
  transition: all 0.2s;
  text-align: center;
  position: relative;
}

.seat-box:hover {
  border-color: #b0baa8;
}

.seat-box.selected {
  border-color: #9b4036;
  background: #fffcfb;
  box-shadow: 0 0 12px rgba(155, 64, 54, 0.12);
}

.seat-badge {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  font-weight: 700;
  font-size: 0.92rem;
  margin-bottom: 12px;
}

.seat-badge.red {
  color: #9b4036;
}

.seat-badge.black {
  color: #172b26;
}

.piece-icon {
  font-size: 1.3rem;
  border-radius: 50%;
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  border: 1px solid currentColor;
  background: #fffef9;
}

.seat-occupant {
  font-size: 0.95rem;
  font-weight: 600;
  color: #172b26;
  margin: 0;
}

.tag-you {
  display: inline-block;
  margin-top: 10px;
  font-size: 0.72rem;
  font-weight: 600;
  color: #9b4036;
  background: #fdf3f2;
  border-radius: 10px;
  padding: 2px 8px;
}

.room-actions {
  text-align: center;
}

.btn-start {
  background: #9b4036;
  color: #fffef9;
  border: none;
  border-radius: 8px;
  padding: 12px 28px;
  font-size: 1rem;
  font-weight: 700;
  cursor: pointer;
  transition: background 0.2s;
  box-shadow: 0 2px 8px rgba(155, 64, 54, 0.25);
}

.btn-start:hover {
  background: #b84f43;
}
</style>
