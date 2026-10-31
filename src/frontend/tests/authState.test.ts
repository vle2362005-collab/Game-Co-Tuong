import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { createMemoryHistory } from 'vue-router'
import { createAuthStore } from '../src/composables/useAuth'
import { apiClient } from '../src/api/client'
import { createAppRouter } from '../src/router'
import type { AuthSession } from '../src/types/auth'

const hub = vi.hoisted(() => ({ url: vi.fn(), stop: vi.fn().mockResolvedValue(undefined) }))
vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Connected: 'Connected' }, LogLevel: { None: 6 },
  HubConnectionBuilder: class {
    withUrl(...args: unknown[]) { hub.url(...args); return this }
    configureLogging() { return this }
    withAutomaticReconnect() { return this }
    build() { return { state: 'Connected', connectionId: 'real-client-mock', start: async () => {}, stop: hub.stop, on() {}, off() {}, onreconnecting() {}, onreconnected() {}, onclose() {}, invoke: async () => {} } }
  }
}))

const mockSession: AuthSession = {
  accessToken: 'jwt-in-memory-token-12345',
  tokenType: 'Bearer',
  expiresAt: new Date(Date.now() + 3600000).toISOString(),
  user: {
    id: 'user-001',
    username: 'kythu_hn',
    displayName: 'Kỳ Thủ Hà Nội'
  }
}

describe('GCT-34: Quản lý phiên trên giao diện (In-memory token & SignalR & Router Guard)', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
    vi.stubGlobal('localStorage', {
      getItem: vi.fn().mockReturnValue(null),
      setItem: vi.fn(),
      removeItem: vi.fn(),
      clear: vi.fn()
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('access token được lưu trong bộ nhớ và tuyệt đối không lưu trong localStorage', () => {
    const auth = createAuthStore('http://localhost:5000')

    // Lưu phiên
    auth.saveSession(mockSession)

    // Token và user tồn tại trong bộ nhớ (reactive state)
    expect(auth.isAuthenticated.value).toBe(true)
    expect(auth.token.value).toBe(mockSession.accessToken)
    expect(auth.user.value?.username).toBe('kythu_hn')

    // Tuyệt đối không lưu vào localStorage
    expect(localStorage.getItem('gct_token')).toBeNull()
    expect(localStorage.getItem('gct_user')).toBeNull()
    expect(localStorage.setItem).not.toHaveBeenCalled()
  })

  it('token trong bộ nhớ được tự động gắn vào các yêu cầu của ApiClient', async () => {
    const fetchMock = vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(JSON.stringify({ data: { status: 'ok' } }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    )
    vi.stubGlobal('fetch', fetchMock)

    const auth = createAuthStore('http://localhost:5000')
    auth.saveSession(mockSession)

    // Gọi API qua ApiClient
    const res = await apiClient.get<{ status: string }>('/api/v1/profile')
    expect(res).toEqual({ status: 'ok' })

    // Xác nhận header Authorization Bearer có token trong bộ nhớ
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5000/api/v1/profile',
      expect.objectContaining({
        headers: expect.any(Headers)
      })
    )
    const headers = fetchMock.mock.calls[0][1].headers as Headers
    expect(headers.get('Authorization')).toBe(`Bearer ${mockSession.accessToken}`)
  })

  it('SignalR dùng token trong bộ nhớ và dừng kết nối khi đăng xuất', async () => {
    const auth = createAuthStore('http://localhost:5000')
    auth.saveSession(mockSession)
    expect(await auth.signalR.connect()).toBe(true)
    expect(auth.signalR.isConnected.value).toBe(true)
    expect(hub.url.mock.calls.at(-1)?.[1].accessTokenFactory()).toBe(mockSession.accessToken)
    await auth.performLogout()
    expect(auth.token.value).toBeNull()
    expect(auth.signalR.isConnected.value).toBe(false)
    expect(hub.stop).toHaveBeenCalled()
  })

  it('chặn màn hình riêng khi chưa đăng nhập và chuyển về /login kèm redirect query', async () => {
    const router = createAppRouter({
      history: createMemoryHistory(),
      isAuthenticated: () => false
    })

    // Truy cập màn hình phòng riêng
    await router.push('/rooms/room-sg-102')
    await router.isReady()

    // Bị chặn và chuyển về /login kèm redirect query
    expect(router.currentRoute.value.path).toBe('/login')
    expect(router.currentRoute.value.query.redirect).toBe('/rooms/room-sg-102')
  })

  it('đăng nhập xong về đúng màn hình cần mở theo query redirect', async () => {
    const router = createAppRouter({
      history: createMemoryHistory(),
      isAuthenticated: () => false
    })

    // 1. Người dùng chưa đăng nhập cố gắng vào lịch sử đấu
    await router.push('/history')
    await router.isReady()
    expect(router.currentRoute.value.path).toBe('/login')
    expect(router.currentRoute.value.query.redirect).toBe('/history')

    // 2. Mô phỏng đăng nhập thành công
    let isUserLoggedIn = false
    const authenticatedRouter = createAppRouter({
      history: createMemoryHistory(),
      isAuthenticated: () => isUserLoggedIn
    })

    // Set đăng nhập thành công
    isUserLoggedIn = true
    const redirectTarget = (router.currentRoute.value.query.redirect as string) || '/lobby'
    await authenticatedRouter.push(redirectTarget)
    await authenticatedRouter.isReady()

    // Điều hướng chuẩn xác về /history
    expect(authenticatedRouter.currentRoute.value.path).toBe('/history')
  })

  it('tải lại trang yêu cầu đăng nhập lại (in-memory state reset)', () => {
    // Phiên làm việc 1
    const appSession1 = createAuthStore('http://localhost:5000')
    appSession1.saveSession(mockSession)
    expect(appSession1.isAuthenticated.value).toBe(true)

    // Mô phỏng tải lại trang (F5): khởi tạo store mới trong bộ nhớ
    const reloadedApp = createAuthStore('http://localhost:5000')
    expect(reloadedApp.isAuthenticated.value).toBe(false)
    expect(reloadedApp.token.value).toBeNull()
    expect(reloadedApp.user.value).toBeNull()
    expect(reloadedApp.currentView.value).toBe('login')
  })

  it('hết phiên ngắt kết nối an toàn và không gây lặp chuyển trang', () => {
    const auth = createAuthStore('http://localhost:5000')
    auth.saveSession(mockSession)

    expect(auth.isAuthenticated.value).toBe(true)

    // Kích hoạt hết phiên
    auth.handleSessionExpired()

    expect(auth.isAuthenticated.value).toBe(false)
    expect(auth.sessionExpired.value).toBe(true)
    expect(auth.currentView.value).toBe('login')

    // Kích hoạt lại liên tiếp nhiều lần -> không bị lỗi hay lặp
    auth.handleSessionExpired()
    auth.handleSessionExpired()
    expect(auth.currentView.value).toBe('login')
  })
})
