<script setup lang="ts">
import type { User } from '../types/auth'

defineProps<{
  isAuthenticated: boolean
  isPlaying: boolean
  user: User | null
}>()

const emit = defineEmits<{
  (e: 'logout'): void
}>()
</script>

<template>
  <header class="app-header">
    <div class="brand">
      <span class="mark" aria-hidden="true">帥</span>
      <div class="brand-text">
        <span class="title">GAME CỜ TƯỚNG</span>
        <span class="version-tag">Trực tuyến</span>
      </div>
    </div>

    <div v-if="isAuthenticated && user" class="user-nav">
      <span class="match-indicator" :class="{ active: isPlaying }">
        {{ isPlaying ? '⚔️ Đang thi đấu' : '🟢 Sảnh chờ' }}
      </span>

      <span class="display-name">{{ user.displayName }}</span>

      <button
        type="button"
        class="logout-btn"
        id="header-logout-btn"
        title="Đăng xuất khỏi tài khoản"
        @click="emit('logout')"
      >
        <span>Đăng xuất</span>
        <span aria-hidden="true">🚪</span>
      </button>
    </div>
  </header>
</template>

<style scoped>
.app-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 0;
  border-bottom: 1px solid #d6dcd0;
}

.brand {
  display: flex;
  align-items: center;
  gap: 12px;
}

.mark {
  border: 1px solid #9b4036;
  border-radius: 50%;
  width: 40px;
  height: 40px;
  display: grid;
  place-items: center;
  color: #9b4036;
  font-size: 1.3rem;
  font-weight: 700;
  background: #fffef9;
}

.brand-text {
  display: flex;
  flex-direction: column;
}

.title {
  font-weight: 700;
  letter-spacing: 0.12em;
  font-size: 0.85rem;
  color: #172b26;
}

.version-tag {
  font-size: 0.68rem;
  color: #9b4036;
  letter-spacing: 0.08em;
}

.user-nav {
  display: flex;
  align-items: center;
  gap: 14px;
}

.match-indicator {
  font-size: 0.76rem;
  padding: 4px 10px;
  border-radius: 20px;
  background: #eaf0e7;
  color: #276440;
  font-weight: 600;
}

.match-indicator.active {
  background: #fdf3f2;
  color: #9b4036;
  animation: pulse 2s infinite;
}

.display-name {
  font-size: 0.88rem;
  font-weight: 600;
  color: #172b26;
}

.logout-btn {
  background: #fffef9;
  color: #9b4036;
  border: 1px solid #eccbc8;
  border-radius: 6px;
  padding: 7px 12px;
  font-size: 0.82rem;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.logout-btn:hover {
  background: #fdf3f2;
  border-color: #9b4036;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.7; }
}

@media (max-width: 600px) {
  .display-name {
    display: none;
  }
}
</style>
