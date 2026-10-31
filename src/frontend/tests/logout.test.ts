import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createAuthStore } from '../src/composables/useAuth'
import type { AuthSession, MatchInfo } from '../src/types/auth'

const mockSession: AuthSession = {
  accessToken: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test-payload.signature',
  tokenType: 'Bearer',
  expiresAt: new Date(Date.now() + 3600000).toISOString(),
  user: {
    id: '11111111-2222-3333-4444-555555555555',
    username: 'testplayer',
    displayName: 'Kỳ Thủ Thử Nghiệm'
  }
}

const mockMatch: MatchInfo = {
  id: 'match-1234',
  roomId: 'room-5678',
  side: 'red',
  opponentName: 'Đối thủ phương Bắc'
}

function createMockStorage() {
  let store: Record<string, string> = {}
  return {
    getItem: (key: string) => store[key] ?? null,
    setItem: (key: string, value: string) => {
      store[key] = String(value)
    },
    removeItem: (key: string) => {
      delete store[key]
    },
    clear: () => {
      store = {}
    }
  }
}

describe('GCT-7: Đăng xuất và xử lý phiên hết hạn', () => {
  let mockStorage = createMockStorage()

  beforeEach(() => {
    mockStorage = createMockStorage()
    vi.stubGlobal('localStorage', mockStorage)
  })

  afterEach(() => {
    mockStorage.clear()
    vi.unstubAllGlobals()
  })

  it('đăng xuất khi ở sảnh thì xóa phiên và chuyển về màn hình đăng nhập', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ acknowledged: true }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)

    const auth = createAuthStore('http://localhost:5000')
    auth.saveSession(mockSession)
    auth.currentView.value = 'lobby'

    expect(auth.isAuthenticated.value).toBe(true)
    expect(auth.token.value).toBe(mockSession.accessToken)
    // Theo chính sách GCT-34: token chỉ lưu trong bộ nhớ, tuyệt đối không lưu trong localStorage
    expect(localStorage.getItem('gct_token')).toBeNull()

    // Thực hiện đăng xuất
    await auth.requestLogout()

    // Sau đăng xuất, phiên bị xóa hoàn toàn
    expect(auth.token.value).toBeNull()
    expect(auth.user.value).toBeNull()
    expect(auth.isAuthenticated.value).toBe(false)
    expect(auth.currentView.value).toBe('login')
    expect(auth.sessionExpired.value).toBe(false)
    expect(localStorage.getItem('gct_token')).toBeNull()
    expect(localStorage.getItem('gct_user')).toBeNull()

    // Xác nhận đã gọi API logout với token cũ
    expect(fetchMock).toHaveBeenCalledWith(
      new URL('http://localhost:5000/api/v1/auth/logout'),
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({
          Authorization: `Bearer ${mockSession.accessToken}`
        })
      })
    )
  })

  it('đang trong ván cờ, bấm đăng xuất phải yêu cầu xác nhận đầu hàng trước', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)

    const auth = createAuthStore('http://localhost:5000')
    auth.saveSession(mockSession)
    auth.enterMatch(mockMatch)

    expect(auth.isPlaying.value).toBe(true)
    expect(auth.currentView.value).toBe('game')

    // Bấm đăng xuất
    auth.requestLogout()

    // Bắt buộc hiện modal xác nhận đầu hàng, CHƯA đăng xuất
    expect(auth.showSurrenderModal.value).toBe(true)
    expect(auth.token.value).toBe(mockSession.accessToken)
    expect(auth.isPlaying.value).toBe(true)
    expect(auth.currentView.value).toBe('game')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('hủy xác nhận đầu hàng thì vẫn giữ trận hiện tại và không chuyển trang', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)

    const auth = createAuthStore('http://localhost:5000')
    auth.saveSession(mockSession)
    auth.enterMatch(mockMatch)

    // Bấm đăng xuất để mở modal
    auth.requestLogout()
    expect(auth.showSurrenderModal.value).toBe(true)

    // Người dùng bấm HỦY
    auth.cancelLogout()

    // Modal đóng, giữ nguyên trận đấu
    expect(auth.showSurrenderModal.value).toBe(false)
    expect(auth.isPlaying.value).toBe(true)
    expect(auth.activeMatch.value).toEqual(mockMatch)
    expect(auth.currentView.value).toBe('game')
    expect(auth.isAuthenticated.value).toBe(true)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('xác nhận đầu hàng thì kết thúc trận, gọi API đăng xuất và chuyển về màn hình đăng nhập', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ acknowledged: true }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)

    const auth = createAuthStore('http://localhost:5000')
    auth.saveSession(mockSession)
    auth.enterMatch(mockMatch)

    // Mở modal
    auth.requestLogout()
    expect(auth.showSurrenderModal.value).toBe(true)

    // Xác nhận đầu hàng & đăng xuất
    await auth.confirmSurrenderAndLogout()

    // Modal đóng, trận cờ bị dọn, phiên bị xóa, chuyển về login
    expect(auth.showSurrenderModal.value).toBe(false)
    expect(auth.isPlaying.value).toBe(false)
    expect(auth.activeMatch.value).toBeNull()
    expect(auth.token.value).toBeNull()
    expect(auth.user.value).toBeNull()
    expect(auth.currentView.value).toBe('login')
    expect(localStorage.getItem('gct_token')).toBeNull()

    expect(fetchMock).toHaveBeenCalledWith(
      new URL('http://localhost:5000/api/v1/auth/logout'),
      expect.objectContaining({ method: 'POST' })
    )
  })

  it('hết phiên thì ngắt kết nối, dọn trận đấu và yêu cầu đăng nhập lại với cờ sessionExpired', () => {
    const auth = createAuthStore('http://localhost:5000')
    auth.saveSession(mockSession)
    auth.enterMatch(mockMatch)
    auth.showSurrenderModal.value = true

    expect(auth.isAuthenticated.value).toBe(true)
    expect(auth.isPlaying.value).toBe(true)

    // Sự kiện hết phiên xảy ra (do token hết hạn hoặc nhận mã lỗi 401)
    auth.handleSessionExpired()

    // Đã dọn dẹp sạch sẽ
    expect(auth.token.value).toBeNull()
    expect(auth.user.value).toBeNull()
    expect(auth.activeMatch.value).toBeNull()
    expect(auth.showSurrenderModal.value).toBe(false)
    expect(auth.currentView.value).toBe('login')
    expect(auth.sessionExpired.value).toBe(true)
    expect(localStorage.getItem('gct_token')).toBeNull()
  })

  it('gọi xử lý hết phiên nhiều lần không bị lặp chuyển trang hay lỗi', () => {
    const auth = createAuthStore('http://localhost:5000')
    auth.saveSession(mockSession)

    // Gọi lặp liên tiếp 3 lần
    auth.handleSessionExpired()
    auth.handleSessionExpired()
    auth.handleSessionExpired()

    expect(auth.currentView.value).toBe('login')
    expect(auth.sessionExpired.value).toBe(true)
    expect(auth.token.value).toBeNull()
  })

  it('sau đăng xuất, khởi tạo ứng dụng mới không tiếp tục dùng phiên cũ', () => {
    const auth1 = createAuthStore('http://localhost:5000')
    auth1.saveSession(mockSession)
    expect(auth1.token.value).toBe(mockSession.accessToken)
    expect(localStorage.getItem('gct_token')).toBeNull()

    auth1.clearSession()

    // Khởi tạo instance mới sau khi phiên đã xóa
    const auth2 = createAuthStore('http://localhost:5000')
    expect(auth2.isAuthenticated.value).toBe(false)
    expect(auth2.token.value).toBeNull()
    expect(auth2.user.value).toBeNull()
    expect(auth2.currentView.value).toBe('login')
  })
})
