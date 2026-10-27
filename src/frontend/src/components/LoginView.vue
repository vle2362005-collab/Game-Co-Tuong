<script setup lang="ts">
import { ref } from 'vue'

defineProps<{
  sessionExpired: boolean
  isSubmitting: boolean
  errorMessage: string | null
}>()

const emit = defineEmits<{
  (e: 'login', payload: { username: string; password: string }): void
}>()

const username = ref('')
const password = ref('')

function handleSubmit() {
  if (!username.value || !password.value) return
  emit('login', { username: username.value, password: password.value })
}
</script>

<template>
  <div class="login-wrapper">
    <!-- Session Expired Alert Banner -->
    <div
      v-if="sessionExpired"
      class="session-expired-alert"
      role="alert"
      aria-live="assertive"
    >
      <div class="alert-icon" aria-hidden="true">⏱️</div>
      <div class="alert-body">
        <strong>Phiên đăng nhập đã hết hạn</strong>
        <p>Kết nối đã được đóng an toàn. Vui lòng đăng nhập lại để tiếp tục chơi.</p>
      </div>
    </div>

    <div class="login-card">
      <div class="login-header">
        <span class="mark-small" aria-hidden="true">帥</span>
        <h2>Đăng nhập tài khoản</h2>
        <p class="subtitle">Nhập thông tin tài khoản để truy cập phòng cờ</p>
      </div>

      <div v-if="errorMessage" class="error-banner" role="alert">
        {{ errorMessage }}
      </div>

      <form @submit.prevent="handleSubmit">
        <div class="form-group">
          <label for="login-username">Tên đăng nhập</label>
          <input
            id="login-username"
            v-model="username"
            type="text"
            required
            placeholder="Ví dụ: kythucotuong"
            autocomplete="username"
            :disabled="isSubmitting"
          />
        </div>

        <div class="form-group">
          <label for="login-password">Mật khẩu</label>
          <input
            id="login-password"
            v-model="password"
            type="password"
            required
            placeholder="••••••••"
            autocomplete="current-password"
            :disabled="isSubmitting"
          />
        </div>

        <button
          type="submit"
          class="submit-btn"
          id="login-submit-btn"
          :disabled="isSubmitting || !username.trim() || !password"
        >
          <span v-if="isSubmitting">Đang đăng nhập…</span>
          <span v-else>Đăng nhập vào game</span>
        </button>
      </form>
    </div>
  </div>
</template>

<style scoped>
.login-wrapper {
  max-width: 440px;
  margin: 32px auto;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.session-expired-alert {
  background: #fdf3f2;
  border: 1px solid #eccbc8;
  border-left: 4px solid #9b4036;
  border-radius: 8px;
  padding: 14px 18px;
  display: flex;
  align-items: flex-start;
  gap: 12px;
  color: #78261e;
  animation: slideDown 0.3s ease;
}

.alert-icon {
  font-size: 1.3rem;
  line-height: 1;
}

.alert-body strong {
  display: block;
  font-size: 0.95rem;
  margin-bottom: 2px;
}

.alert-body p {
  margin: 0;
  font-size: 0.85rem;
  color: #8b3a32;
}

.login-card {
  background: #fffef9;
  border: 1px solid #d6dcd0;
  border-radius: 16px;
  padding: 32px 28px;
  box-shadow: 0 4px 16px rgba(23, 43, 38, 0.05);
}

.login-header {
  text-align: center;
  margin-bottom: 24px;
}

.mark-small {
  border: 1px solid #9b4036;
  border-radius: 50%;
  width: 44px;
  height: 44px;
  display: inline-grid;
  place-items: center;
  color: #9b4036;
  font-size: 1.4rem;
  font-weight: bold;
  margin-bottom: 12px;
}

h2 {
  font-family: Georgia, "Times New Roman", serif;
  font-weight: 400;
  font-size: 1.5rem;
  color: #172b26;
  margin: 0 0 6px;
}

.subtitle {
  font-size: 0.88rem;
  color: #65746d;
  margin: 0;
}

.error-banner {
  background: #fdf3f2;
  color: #9b4036;
  border: 1px solid #eccbc8;
  border-radius: 6px;
  padding: 10px 14px;
  font-size: 0.86rem;
  margin-bottom: 16px;
}

.form-group {
  margin-bottom: 18px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  text-align: left;
}

label {
  font-size: 0.82rem;
  font-weight: 600;
  color: #172b26;
}

input {
  padding: 11px 14px;
  font-size: 0.95rem;
  border: 1px solid #d6dcd0;
  border-radius: 8px;
  background: #faf9f5;
  color: #172b26;
  transition: border-color 0.2s;
}

input:focus {
  outline: none;
  border-color: #9b4036;
  background: #ffffff;
}

.submit-btn {
  width: 100%;
  background: #172b26;
  color: #fffef9;
  border: none;
  border-radius: 8px;
  padding: 13px;
  font-size: 0.95rem;
  font-weight: 600;
  cursor: pointer;
  display: grid;
  place-items: center;
  transition: background 0.2s;
  margin-top: 8px;
}

.submit-btn:hover:not(:disabled) {
  background: #2d4940;
}

.submit-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

@keyframes slideDown {
  from {
    opacity: 0;
    transform: translateY(-8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>
