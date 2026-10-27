<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { RegisterPayload } from '../api/auth'

const props = withDefaults(
  defineProps<{
    sessionExpired?: boolean
    isSubmitting?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
    initialMode?: 'login' | 'register'
  }>(),
  {
    sessionExpired: false,
    isSubmitting: false,
    errorMessage: null,
    fieldErrors: () => ({}),
    initialMode: 'login'
  }
)

const emit = defineEmits<{
  (e: 'login', payload: { username: string; password: string }): void
  (e: 'register', payload: RegisterPayload): void
}>()

const route = useRoute()
const router = useRouter()

const activeTab = ref<'login' | 'register'>('login')

// Login form model
const loginUsername = ref('')
const loginPassword = ref('')
const loginValidationErrors = ref<{ username?: string; password?: string }>({})

// Register form model
const regUsername = ref('')
const regDisplayName = ref('')
const regEmail = ref('')
const regPassword = ref('')
const regConfirmPassword = ref('')
const regValidationErrors = ref<{
  username?: string
  displayName?: string
  email?: string
  password?: string
  confirmPassword?: string
}>({})

// Local submission guard preventing double clicks
const localSubmitting = ref(false)

onMounted(() => {
  if (props.initialMode === 'register' || route?.path === '/register' || route?.query?.mode === 'register') {
    activeTab.value = 'register'
  }
})

watch(
  () => route?.path,
  (path) => {
    if (path === '/register') {
      activeTab.value = 'register'
    } else if (path === '/login') {
      activeTab.value = 'login'
    }
  }
)

function switchTab(tab: 'login' | 'register') {
  activeTab.value = tab
  loginValidationErrors.value = {}
  regValidationErrors.value = {}
  if (router && route) {
    const targetPath = tab === 'register' ? '/register' : '/login'
    if (route.path !== targetPath) {
      router.replace(targetPath)
    }
  }
}

function handleLoginSubmit() {
  if (props.isSubmitting || localSubmitting.value) return

  loginValidationErrors.value = {}
  const u = loginUsername.value.trim()
  const p = loginPassword.value

  let hasError = false
  if (!u) {
    loginValidationErrors.value.username = 'Vui lòng nhập tên đăng nhập.'
    hasError = true
  }
  if (!p) {
    loginValidationErrors.value.password = 'Vui lòng nhập mật khẩu.'
    hasError = true
  }

  if (hasError) return

  localSubmitting.value = true
  emit('login', { username: u, password: p })

  // Re-enable in next tick or when prop changes
  setTimeout(() => {
    localSubmitting.value = false
  }, 1000)
}

function handleRegisterSubmit() {
  if (props.isSubmitting || localSubmitting.value) return

  regValidationErrors.value = {}
  const u = regUsername.value.trim()
  const d = regDisplayName.value.trim()
  const e = regEmail.value.trim()
  const p = regPassword.value
  const cp = regConfirmPassword.value

  let hasError = false

  if (!u) {
    regValidationErrors.value.username = 'Vui lòng nhập tên đăng nhập.'
    hasError = true
  } else if (u.length < 3 || u.length > 30) {
    regValidationErrors.value.username = 'Tên đăng nhập phải từ 3 đến 30 ký tự.'
    hasError = true
  } else if (!/^[a-zA-Z0-9_]+$/.test(u)) {
    regValidationErrors.value.username = 'Tên đăng nhập chỉ chứa chữ cái, số và dấu gạch dưới.'
    hasError = true
  }

  if (!d) {
    regValidationErrors.value.displayName = 'Vui lòng nhập biệt danh hiển thị.'
    hasError = true
  }

  if (!e) {
    regValidationErrors.value.email = 'Vui lòng nhập địa chỉ email.'
    hasError = true
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e)) {
    regValidationErrors.value.email = 'Địa chỉ email không đúng định dạng.'
    hasError = true
  }

  if (!p) {
    regValidationErrors.value.password = 'Vui lòng nhập mật khẩu.'
    hasError = true
  } else if (p.length < 6) {
    regValidationErrors.value.password = 'Mật khẩu phải có ít nhất 6 ký tự.'
    hasError = true
  }

  if (!cp) {
    regValidationErrors.value.confirmPassword = 'Vui lòng xác nhận lại mật khẩu.'
    hasError = true
  } else if (p !== cp) {
    regValidationErrors.value.confirmPassword = 'Mật khẩu xác nhận không khớp.'
    hasError = true
  }

  if (hasError) return

  localSubmitting.value = true
  emit('register', {
    username: u,
    displayName: d,
    email: e,
    password: p
  })

  setTimeout(() => {
    localSubmitting.value = false
  }, 1000)
}

function getFieldError(fieldName: string): string | null {
  if (props.fieldErrors && props.fieldErrors[fieldName]?.length) {
    return props.fieldErrors[fieldName][0]
  }
  return null
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

    <div class="auth-card">
      <div class="auth-header">
        <span class="mark-small" aria-hidden="true">帥</span>
        <h2>Cờ Tướng Trực Tuyến</h2>
        <p class="subtitle">Đấu trí kỳ nghệ · Giao lưu kỳ thủ bốn phương</p>
      </div>

      <!-- Tab Switcher -->
      <div class="tab-switcher" role="tablist">
        <button
          type="button"
          class="tab-btn"
          :class="{ active: activeTab === 'login' }"
          id="tab-login"
          role="tab"
          :aria-selected="activeTab === 'login'"
          @click="switchTab('login')"
        >
          Đăng nhập
        </button>
        <button
          type="button"
          class="tab-btn"
          :class="{ active: activeTab === 'register' }"
          id="tab-register"
          role="tab"
          :aria-selected="activeTab === 'register'"
          @click="switchTab('register')"
        >
          Đăng ký tài khoản
        </button>
      </div>

      <!-- General Error Banner -->
      <div v-if="errorMessage" class="error-banner" role="alert">
        <span>⚠️ {{ errorMessage }}</span>
      </div>

      <!-- ================= LOGIN FORM ================= -->
      <form v-if="activeTab === 'login'" id="login-form" @submit.prevent="handleLoginSubmit">
        <div class="form-group">
          <label for="login-username">Tên đăng nhập <span class="required">*</span></label>
          <input
            id="login-username"
            v-model="loginUsername"
            type="text"
            placeholder="Ví dụ: kythucotuong"
            autocomplete="username"
            :disabled="isSubmitting || localSubmitting"
            :class="{ 'input-error': loginValidationErrors.username || getFieldError('username') }"
          />
          <span
            v-if="loginValidationErrors.username || getFieldError('username')"
            class="field-error"
            id="login-username-error"
            role="alert"
          >
            {{ loginValidationErrors.username || getFieldError('username') }}
          </span>
        </div>

        <div class="form-group">
          <label for="login-password">Mật khẩu <span class="required">*</span></label>
          <input
            id="login-password"
            v-model="loginPassword"
            type="password"
            placeholder="••••••••"
            autocomplete="current-password"
            :disabled="isSubmitting || localSubmitting"
            :class="{ 'input-error': loginValidationErrors.password || getFieldError('password') }"
          />
          <span
            v-if="loginValidationErrors.password || getFieldError('password')"
            class="field-error"
            id="login-password-error"
            role="alert"
          >
            {{ loginValidationErrors.password || getFieldError('password') }}
          </span>
        </div>

        <button
          type="submit"
          class="submit-btn"
          id="login-submit-btn"
          :disabled="isSubmitting || localSubmitting || !loginUsername.trim() || !loginPassword"
        >
          <span v-if="isSubmitting || localSubmitting" class="btn-loading">
            <span class="spinner-small"></span> Đang đăng nhập…
          </span>
          <span v-else>Đăng nhập vào game ⚔️</span>
        </button>
      </form>

      <!-- ================= REGISTER FORM ================= -->
      <form v-else id="register-form" @submit.prevent="handleRegisterSubmit">
        <div class="form-group">
          <label for="register-username">Tên đăng nhập <span class="required">*</span></label>
          <input
            id="register-username"
            v-model="regUsername"
            type="text"
            placeholder="Từ 3-30 ký tự (chữ cái, số, gạch dưới)"
            autocomplete="username"
            :disabled="isSubmitting || localSubmitting"
            :class="{ 'input-error': regValidationErrors.username || getFieldError('username') }"
          />
          <span
            v-if="regValidationErrors.username || getFieldError('username')"
            class="field-error"
            id="register-username-error"
            role="alert"
          >
            {{ regValidationErrors.username || getFieldError('username') }}
          </span>
        </div>

        <div class="form-group">
          <label for="register-displayName">Biệt danh hiển thị <span class="required">*</span></label>
          <input
            id="register-displayName"
            v-model="regDisplayName"
            type="text"
            placeholder="Ví dụ: Kỳ Thánh Sông Hàn"
            autocomplete="nickname"
            :disabled="isSubmitting || localSubmitting"
            :class="{ 'input-error': regValidationErrors.displayName || getFieldError('displayName') }"
          />
          <span
            v-if="regValidationErrors.displayName || getFieldError('displayName')"
            class="field-error"
            id="register-displayName-error"
            role="alert"
          >
            {{ regValidationErrors.displayName || getFieldError('displayName') }}
          </span>
        </div>

        <div class="form-group">
          <label for="register-email">Địa chỉ Email <span class="required">*</span></label>
          <input
            id="register-email"
            v-model="regEmail"
            type="email"
            placeholder="name@example.com"
            autocomplete="email"
            :disabled="isSubmitting || localSubmitting"
            :class="{ 'input-error': regValidationErrors.email || getFieldError('email') }"
          />
          <span
            v-if="regValidationErrors.email || getFieldError('email')"
            class="field-error"
            id="register-email-error"
            role="alert"
          >
            {{ regValidationErrors.email || getFieldError('email') }}
          </span>
        </div>

        <div class="form-group">
          <label for="register-password">Mật khẩu <span class="required">*</span></label>
          <input
            id="register-password"
            v-model="regPassword"
            type="password"
            placeholder="Tối thiểu 6 ký tự"
            autocomplete="new-password"
            :disabled="isSubmitting || localSubmitting"
            :class="{ 'input-error': regValidationErrors.password || getFieldError('password') }"
          />
          <span
            v-if="regValidationErrors.password || getFieldError('password')"
            class="field-error"
            id="register-password-error"
            role="alert"
          >
            {{ regValidationErrors.password || getFieldError('password') }}
          </span>
        </div>

        <div class="form-group">
          <label for="register-confirmPassword">Xác nhận mật khẩu <span class="required">*</span></label>
          <input
            id="register-confirmPassword"
            v-model="regConfirmPassword"
            type="password"
            placeholder="Nhập lại mật khẩu"
            autocomplete="new-password"
            :disabled="isSubmitting || localSubmitting"
            :class="{ 'input-error': regValidationErrors.confirmPassword }"
          />
          <span
            v-if="regValidationErrors.confirmPassword"
            class="field-error"
            id="register-confirmPassword-error"
            role="alert"
          >
            {{ regValidationErrors.confirmPassword }}
          </span>
        </div>

        <button
          type="submit"
          class="submit-btn"
          id="register-submit-btn"
          :disabled="isSubmitting || localSubmitting || !regUsername.trim() || !regPassword || !regEmail.trim()"
        >
          <span v-if="isSubmitting || localSubmitting" class="btn-loading">
            <span class="spinner-small"></span> Đang tạo tài khoản…
          </span>
          <span v-else>Tạo tài khoản & Vào sảnh 📜</span>
        </button>
      </form>
    </div>
  </div>
</template>

<style scoped>
.login-wrapper {
  max-width: 460px;
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

.auth-card {
  background: #fffef9;
  border: 1px solid #d6dcd0;
  border-radius: 16px;
  padding: 32px 28px;
  box-shadow: 0 4px 16px rgba(23, 43, 38, 0.05);
}

.auth-header {
  text-align: center;
  margin-bottom: 20px;
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
  background: #fffdfa;
}

h2 {
  font-family: Georgia, "Times New Roman", serif;
  font-weight: 400;
  font-size: 1.5rem;
  color: #172b26;
  margin: 0 0 6px;
}

.subtitle {
  font-size: 0.86rem;
  color: #65746d;
  margin: 0;
}

.tab-switcher {
  display: grid;
  grid-template-columns: 1fr 1fr;
  background: #f1ede3;
  border-radius: 10px;
  padding: 4px;
  margin-bottom: 22px;
}

.tab-btn {
  background: transparent;
  border: none;
  border-radius: 8px;
  padding: 9px 12px;
  font-size: 0.88rem;
  font-weight: 600;
  color: #53625b;
  cursor: pointer;
  transition: all 0.2s;
}

.tab-btn.active {
  background: #fffef9;
  color: #9b4036;
  box-shadow: 0 2px 6px rgba(23, 43, 38, 0.08);
}

.error-banner {
  background: #fdf3f2;
  color: #9b4036;
  border: 1px solid #eccbc8;
  border-radius: 8px;
  padding: 10px 14px;
  font-size: 0.86rem;
  margin-bottom: 18px;
}

.form-group {
  margin-bottom: 16px;
  display: flex;
  flex-direction: column;
  gap: 5px;
  text-align: left;
}

label {
  font-size: 0.82rem;
  font-weight: 600;
  color: #172b26;
}

.required {
  color: #9b4036;
}

input {
  padding: 11px 14px;
  font-size: 0.95rem;
  border: 1px solid #d6dcd0;
  border-radius: 8px;
  background: #faf9f5;
  color: #172b26;
  transition: all 0.2s;
}

input:focus {
  outline: none;
  border-color: #9b4036;
  background: #ffffff;
  box-shadow: 0 0 0 3px rgba(155, 64, 54, 0.1);
}

input.input-error {
  border-color: #9b4036;
  background: #fffcfb;
}

.field-error {
  color: #9b4036;
  font-size: 0.78rem;
  font-weight: 500;
  margin-top: 2px;
}

.submit-btn {
  width: 100%;
  background: #9b4036;
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
  margin-top: 10px;
}

.submit-btn:hover:not(:disabled) {
  background: #b84f43;
}

.submit-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-loading {
  display: flex;
  align-items: center;
  gap: 8px;
}

.spinner-small {
  width: 14px;
  height: 14px;
  border: 2px solid #fffef9;
  border-top-color: transparent;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  display: inline-block;
}

@keyframes spin {
  to { transform: rotate(360deg); }
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
