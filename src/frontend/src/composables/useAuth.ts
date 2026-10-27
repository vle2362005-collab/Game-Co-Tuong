import { ref, computed } from 'vue'
import type { User, AuthSession, MatchInfo, AppView } from '../types/auth'
import { loginApi, logoutApi } from '../api/auth'

const STORAGE_TOKEN_KEY = 'gct_token'
const STORAGE_USER_KEY = 'gct_user'

function loadSavedToken(): string | null {
  try {
    return localStorage.getItem(STORAGE_TOKEN_KEY)
  } catch {
    return null
  }
}

function loadSavedUser(): User | null {
  try {
    const raw = localStorage.getItem(STORAGE_USER_KEY)
    if (!raw) return null
    return JSON.parse(raw) as User
  } catch {
    return null
  }
}

export function createAuthStore(apiBaseUrl: string = 'http://localhost:5000') {
  const token = ref<string | null>(loadSavedToken())
  const user = ref<User | null>(loadSavedUser())
  const currentView = ref<AppView>(token.value && user.value ? 'lobby' : 'login')
  const sessionExpired = ref<boolean>(false)
  const isSubmitting = ref<boolean>(false)
  const errorMessage = ref<string | null>(null)

  // In-game state
  const activeMatch = ref<MatchInfo | null>(null)
  const showSurrenderModal = ref<boolean>(false)

  // Guard against redirect loops
  let isHandlingExpiry = false

  const isAuthenticated = computed(() => !!token.value && !!user.value)
  const isPlaying = computed(() => !!activeMatch.value)

  function saveSession(session: AuthSession) {
    token.value = session.accessToken
    user.value = session.user
    sessionExpired.value = false
    try {
      localStorage.setItem(STORAGE_TOKEN_KEY, session.accessToken)
      localStorage.setItem(STORAGE_USER_KEY, JSON.stringify(session.user))
    } catch {
      // Storage unavailable in private browsing
    }
  }

  function clearSession() {
    token.value = null
    user.value = null
    activeMatch.value = null
    showSurrenderModal.value = false
    try {
      localStorage.removeItem(STORAGE_TOKEN_KEY)
      localStorage.removeItem(STORAGE_USER_KEY)
    } catch {
      // Storage unavailable
    }
  }

  async function login(username: string, password: string): Promise<boolean> {
    isSubmitting.value = true
    errorMessage.value = null
    try {
      const session = await loginApi(apiBaseUrl, username, password)
      saveSession(session)
      currentView.value = 'lobby'
      return true
    } catch (err: unknown) {
      errorMessage.value = err instanceof Error ? err.message : 'Đăng nhập không thành công.'
      return false
    } finally {
      isSubmitting.value = false
    }
  }

  function requestLogout() {
    // Nếu đang trong ván chơi, bắt buộc xác nhận đầu hàng trước
    if (activeMatch.value !== null) {
      showSurrenderModal.value = true
      return
    }

    // Nếu không chơi, đăng xuất trực tiếp
    return performLogout()
  }

  function cancelLogout() {
    showSurrenderModal.value = false
    // Vẫn giữ trận hiện tại, không chuyển trang
  }

  async function confirmSurrenderAndLogout() {
    showSurrenderModal.value = false
    // Resign match cleanup
    activeMatch.value = null
    await performLogout()
  }

  async function performLogout() {
    const currentToken = token.value
    clearSession()
    sessionExpired.value = false
    currentView.value = 'login'

    // Gửi yêu cầu thu hồi token lên backend
    if (currentToken) {
      await logoutApi(apiBaseUrl, currentToken)
    }
  }

  function handleSessionExpired() {
    if (isHandlingExpiry) return
    isHandlingExpiry = true

    try {
      clearSession()
      sessionExpired.value = true
      currentView.value = 'login'
    } finally {
      isHandlingExpiry = false
    }
  }

  function enterMatch(match: MatchInfo) {
    activeMatch.value = match
    currentView.value = 'game'
  }

  function leaveMatch() {
    activeMatch.value = null
    currentView.value = 'lobby'
  }

  return {
    token,
    user,
    currentView,
    sessionExpired,
    isSubmitting,
    errorMessage,
    activeMatch,
    showSurrenderModal,
    isAuthenticated,
    isPlaying,
    login,
    requestLogout,
    cancelLogout,
    confirmSurrenderAndLogout,
    performLogout,
    handleSessionExpired,
    enterMatch,
    leaveMatch,
    saveSession,
    clearSession
  }
}

export type AuthStore = ReturnType<typeof createAuthStore>
