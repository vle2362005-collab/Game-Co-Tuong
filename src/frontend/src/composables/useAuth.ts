import { ref, computed } from 'vue'
import type { User, AuthSession, MatchInfo, AppView } from '../types/auth'
import { loginApi, registerApi, logoutApi, AuthApiError, type RegisterPayload } from '../api/auth'
import { setApiAuthHandlers } from '../api/client'
import { createSignalRClient, type SignalRClient } from '../api/signalr'

// Clean up any legacy localStorage tokens on module load to guarantee zero persistent tokens
try {
  localStorage.removeItem('gct_token')
  localStorage.removeItem('gct_user')
} catch {
  // Storage unavailable
}

export function createAuthStore(apiBaseUrl: string = 'http://localhost:5000') {
  // Strictly in-memory access token & user profile (never written to localStorage)
  const token = ref<string | null>(null)
  const user = ref<User | null>(null)
  const currentView = ref<AppView>('login')
  const sessionExpired = ref<boolean>(false)
  const isSubmitting = ref<boolean>(false)
  const errorMessage = ref<string | null>(null)
  const fieldErrors = ref<Record<string, string[]>>({})

  // In-game state
  const activeMatch = ref<MatchInfo | null>(null)
  const showSurrenderModal = ref<boolean>(false)

  // Guard against redirect loops
  let isHandlingExpiry = false
  let expiryTimer: ReturnType<typeof setTimeout> | null = null

  const isAuthenticated = computed(() => !!token.value && !!user.value)
  const isPlaying = computed(() => !!activeMatch.value)

  // SignalR client tied to in-memory token
  const signalR: SignalRClient = createSignalRClient({
    baseUrl: apiBaseUrl,
    getToken: () => token.value,
    onUnauthorized: () => handleSessionExpired()
  })

  // Hook in-memory token into global ApiClient
  setApiAuthHandlers(
    () => token.value,
    () => handleSessionExpired()
  )

  function saveSession(session: AuthSession) {
    // Save ONLY into memory
    if (expiryTimer) clearTimeout(expiryTimer)
    const delay = Math.max(0, Date.parse(session.expiresAt) - Date.now())
    if (Number.isFinite(delay)) expiryTimer = setTimeout(handleSessionExpired, Math.min(delay, 2147483647))
    token.value = session.accessToken
    user.value = session.user
    sessionExpired.value = false
    fieldErrors.value = {}
    errorMessage.value = null

    // Ensure localStorage never contains access token or credentials
    try {
      localStorage.removeItem('gct_token')
      localStorage.removeItem('gct_user')
    } catch {
      // Storage unavailable
    }
  }

  function clearSession() {
    if (expiryTimer) clearTimeout(expiryTimer)
    expiryTimer = null
    token.value = null
    user.value = null
    activeMatch.value = null
    showSurrenderModal.value = false

    // Close SignalR connection immediately on logout or session expiry
    signalR.disconnect()

    // Clean up any localStorage remnant
    try {
      localStorage.removeItem('gct_token')
      localStorage.removeItem('gct_user')
    } catch {
      // Storage unavailable
    }
  }

  async function login(username: string, password: string): Promise<boolean> {
    if (isSubmitting.value) return false
    isSubmitting.value = true
    errorMessage.value = null
    fieldErrors.value = {}
    try {
      const session = await loginApi(apiBaseUrl, username, password)
      saveSession(session)
      currentView.value = 'lobby'
      return true
    } catch (err: unknown) {
      if (err instanceof AuthApiError) {
        errorMessage.value = err.message
        fieldErrors.value = err.fieldErrors || {}
      } else {
        errorMessage.value = err instanceof Error ? err.message : 'Đăng nhập không thành công.'
      }
      return false
    } finally {
      isSubmitting.value = false
    }
  }

  async function register(payload: RegisterPayload): Promise<boolean> {
    if (isSubmitting.value) return false
    isSubmitting.value = true
    errorMessage.value = null
    fieldErrors.value = {}
    try {
      await registerApi(apiBaseUrl, payload)
      // Tự động đăng nhập vào sảnh sau khi đăng ký thành công
      const session = await loginApi(apiBaseUrl, payload.username, payload.password)
      saveSession(session)
      currentView.value = 'lobby'
      return true
    } catch (err: unknown) {
      if (err instanceof AuthApiError) {
        errorMessage.value = err.message
        fieldErrors.value = err.fieldErrors || {}
      } else {
        errorMessage.value = err instanceof Error ? err.message : 'Đăng ký không thành công.'
      }
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
    fieldErrors,
    activeMatch,
    showSurrenderModal,
    isAuthenticated,
    isPlaying,
    signalR,
    login,
    register,
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

let globalAuthStore: AuthStore | null = null

export function useAuth(apiBaseUrl?: string): AuthStore {
  if (!globalAuthStore) {
    const defaultUrl = apiBaseUrl || (typeof import.meta !== 'undefined' && import.meta.env?.VITE_API_BASE_URL) || 'http://localhost:5000'
    globalAuthStore = createAuthStore(defaultUrl)
  }
  return globalAuthStore
}
