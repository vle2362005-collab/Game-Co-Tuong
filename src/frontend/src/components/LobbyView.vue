<script setup lang="ts">
import { useRouter } from 'vue-router'
import type { User, MatchInfo } from '../types/auth'

defineProps<{
  user: User
}>()

const emit = defineEmits<{
  (e: 'enterMatch', match: MatchInfo): void
  (e: 'testExpire'): void
}>()

const router = useRouter()

function startDemoMatch() {
  emit('enterMatch', {
    id: crypto.randomUUID(),
    roomId: crypto.randomUUID(),
    side: 'red',
    opponentName: 'Kỳ thủ phương Bắc'
  })
}

function goToRoom() {
  if (router) {
    router.push('/rooms/room-demo-01')
  }
}

function goToHistory() {
  if (router) {
    router.push('/history')
  }
}
</script>

<template>
  <div class="lobby-card">
    <div class="user-greeting">
      <span class="user-avatar" aria-hidden="true">♟️</span>
      <div>
        <h2>Xin chào, {{ user.displayName }}!</h2>
        <p class="user-handle">@{{ user.username }} · Trạng thái: Sảnh chờ</p>
      </div>
    </div>

    <div class="action-grid">
      <div class="action-box">
        <h3>Vào bàn thi đấu</h3>
        <p>Bắt đầu hoặc tham gia một ván cờ mới để kiểm tra luồng đầu hàng trước khi đăng xuất.</p>
        <button
          type="button"
          class="btn-primary"
          id="enter-match-btn"
          @click="startDemoMatch"
        >
          Tham gia ván cờ ngay ⚔️
        </button>
      </div>

      <div class="action-box">
        <h3>Phòng cờ & Ghép bàn</h3>
        <p>Tạo phòng riêng hoặc tham gia phòng cờ có sẵn để chọn phe Đỏ/Đen.</p>
        <button
          type="button"
          class="btn-primary"
          id="enter-room-btn"
          @click="goToRoom"
        >
          Vào phòng cờ 🏰
        </button>
      </div>

      <div class="action-box subtle">
        <h3>Lịch sử thi đấu</h3>
        <p>Xem lại kết quả các ván đấu đã qua, tỷ lệ thắng/thua và số nước cờ.</p>
        <button
          type="button"
          class="btn-outline"
          id="view-history-btn"
          @click="goToHistory"
        >
          Xem lịch sử đấu 📜
        </button>
      </div>

      <div class="action-box subtle">
        <h3>Kiểm tra hết hạn phiên</h3>
        <p>Mô phỏng sự cố token hết hạn hoặc nhận mã lỗi 401 để kiểm tra việc đóng kết nối an toàn.</p>
        <button
          type="button"
          class="btn-outline"
          id="simulate-expire-btn"
          @click="emit('testExpire')"
        >
          Mô phỏng phiên hết hạn (401) ⏱️
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.lobby-card {
  background: #fffef9;
  border: 1px solid #d6dcd0;
  border-radius: 16px;
  padding: 32px 28px;
  max-width: 680px;
  margin: 32px auto;
  box-shadow: 0 4px 16px rgba(23, 43, 38, 0.05);
}

.user-greeting {
  display: flex;
  align-items: center;
  gap: 16px;
  padding-bottom: 24px;
  border-bottom: 1px solid #eaf0e7;
  margin-bottom: 24px;
}

.user-avatar {
  font-size: 2.2rem;
  background: #f5f3ec;
  border: 1px solid #d6dcd0;
  border-radius: 50%;
  width: 56px;
  height: 56px;
  display: grid;
  place-items: center;
}

h2 {
  font-family: Georgia, "Times New Roman", serif;
  margin: 0 0 4px;
  color: #172b26;
  font-size: 1.4rem;
}

.user-handle {
  margin: 0;
  font-size: 0.88rem;
  color: #53625b;
}

.action-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}

@media (max-width: 600px) {
  .action-grid {
    grid-template-columns: 1fr;
  }
}

.action-box {
  background: #fbfaf6;
  border: 1px solid #d6dcd0;
  border-radius: 12px;
  padding: 20px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.action-box h3 {
  margin: 0 0 8px;
  font-size: 1.05rem;
  color: #172b26;
}

.action-box p {
  font-size: 0.85rem;
  color: #65746d;
  margin: 0 0 16px;
  flex-grow: 1;
}

.btn-primary {
  background: #9b4036;
  color: #fffef9;
  border: none;
  border-radius: 8px;
  padding: 10px 16px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-primary:hover {
  background: #b84f43;
}

.btn-outline {
  background: transparent;
  color: #53625b;
  border: 1px solid #d6dcd0;
  border-radius: 8px;
  padding: 10px 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-outline:hover {
  background: #eaf0e7;
  color: #172b26;
}
</style>
