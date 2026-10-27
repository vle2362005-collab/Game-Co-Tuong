import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { createAuthStore } from '../src/composables/useAuth'

describe('GCT-33: Hiển thị màn hình đăng ký và đăng nhập', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
    vi.stubGlobal('localStorage', {
      getItem: vi.fn(),
      setItem: vi.fn(),
      removeItem: vi.fn(),
      clear: vi.fn()
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('đăng nhập thành công: gọi API thật, lưu phiên và chuyển tới sảnh', async () => {
    const mockSession = {
      accessToken: 'token-abc-xyz',
      tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
      user: {
        id: 'u-1',
        username: 'kythu1',
        displayName: 'Kỳ Thủ 1'
      }
    }

    const fetchMock = vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(JSON.stringify({ data: mockSession }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    )
    vi.stubGlobal('fetch', fetchMock)

    const auth = createAuthStore('http://localhost:5000')
    const ok = await auth.login('kythu1', 'SecretPass123!')

    expect(ok).toBe(true)
    expect(auth.isAuthenticated.value).toBe(true)
    expect(auth.user.value?.username).toBe('kythu1')
    expect(auth.currentView.value).toBe('lobby')
    expect(auth.errorMessage.value).toBeNull()

    // Kiểm tra API được gọi đúng endpoint và body
    expect(fetchMock).toHaveBeenCalledWith(
      new URL('http://localhost:5000/api/v1/auth/login'),
      expect.objectContaining({
        method: 'POST'
      })
    )
  })

  it('sai mật khẩu hoặc tên đăng nhập: nhận thông báo lỗi từ máy chủ và mở khóa nút', async () => {
    const errorEnvelope = {
      error: {
        code: 'INVALID_CREDENTIALS',
        message: 'Tên đăng nhập hoặc mật khẩu không chính xác.'
      }
    }

    const fetchMock = vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(JSON.stringify(errorEnvelope), {
          status: 401,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    )
    vi.stubGlobal('fetch', fetchMock)

    const auth = createAuthStore('http://localhost:5000')
    const ok = await auth.login('kythu1', 'WrongPassword')

    expect(ok).toBe(false)
    expect(auth.isAuthenticated.value).toBe(false)
    expect(auth.errorMessage.value).toBe('Tên đăng nhập hoặc mật khẩu không chính xác.')
    // Nút phải được mở khóa lại
    expect(auth.isSubmitting.value).toBe(false)
  })

  it('đăng ký tài khoản mới thành công: tự động đăng nhập và lưu phiên sảnh', async () => {
    const registeredUser = {
      id: 'new-u-888',
      username: 'tan_ky_thu',
      displayName: 'Tân Kỳ Thủ'
    }

    const mockSession = {
      accessToken: 'token-new-888',
      tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
      user: registeredUser
    }

    const fetchMock = vi.fn()
      // 1. POST /api/v1/auth/register -> 201 Created
      .mockImplementationOnce(() =>
        Promise.resolve(
          new Response(JSON.stringify({ data: registeredUser }), {
            status: 201,
            headers: { 'Content-Type': 'application/json' }
          })
        )
      )
      // 2. POST /api/v1/auth/login -> 200 OK
      .mockImplementationOnce(() =>
        Promise.resolve(
          new Response(JSON.stringify({ data: mockSession }), {
            status: 200,
            headers: { 'Content-Type': 'application/json' }
          })
        )
      )

    vi.stubGlobal('fetch', fetchMock)

    const auth = createAuthStore('http://localhost:5000')
    const ok = await auth.register({
      username: 'tan_ky_thu',
      displayName: 'Tân Kỳ Thủ',
      email: 'tankythu@example.com',
      password: 'SecurePassword123'
    })

    expect(ok).toBe(true)
    expect(auth.isAuthenticated.value).toBe(true)
    expect(auth.user.value?.displayName).toBe('Tân Kỳ Thủ')
    expect(auth.currentView.value).toBe('lobby')
    expect(auth.isSubmitting.value).toBe(false)
  })

  it('đăng ký trùng tài khoản (USERNAME_TAKEN): hiển thị thông báo lỗi từng trường từ API', async () => {
    const errorEnvelope = {
      error: {
        code: 'USERNAME_TAKEN',
        message: 'Tên đăng nhập đã được sử dụng.',
        fieldErrors: {
          username: ['Tên đăng nhập đã tồn tại trong hệ thống.']
        }
      }
    }

    const fetchMock = vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(JSON.stringify(errorEnvelope), {
          status: 409,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    )
    vi.stubGlobal('fetch', fetchMock)

    const auth = createAuthStore('http://localhost:5000')
    const ok = await auth.register({
      username: 'trung_ten',
      displayName: 'Kỳ Thủ Trùng Tên',
      email: 'unique@example.com',
      password: 'Password123'
    })

    expect(ok).toBe(false)
    expect(auth.isAuthenticated.value).toBe(false)
    expect(auth.errorMessage.value).toBe('Tên đăng nhập đã được sử dụng.')
    expect(auth.fieldErrors.value.username).toEqual(['Tên đăng nhập đã tồn tại trong hệ thống.'])
    expect(auth.isSubmitting.value).toBe(false)
  })

  it('đăng ký trùng email (EMAIL_TAKEN): hiển thị thông báo lỗi từng trường cho email', async () => {
    const errorEnvelope = {
      error: {
        code: 'EMAIL_TAKEN',
        message: 'Địa chỉ email đã được đăng ký.',
        fieldErrors: {
          email: ['Email này đã được liên kết với một tài khoản khác.']
        }
      }
    }

    const fetchMock = vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(JSON.stringify(errorEnvelope), {
          status: 409,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    )
    vi.stubGlobal('fetch', fetchMock)

    const auth = createAuthStore('http://localhost:5000')
    const ok = await auth.register({
      username: 'kythumoi',
      displayName: 'Kỳ Thủ Mới',
      email: 'trung_email@example.com',
      password: 'Password123'
    })

    expect(ok).toBe(false)
    expect(auth.errorMessage.value).toBe('Địa chỉ email đã được đăng ký.')
    expect(auth.fieldErrors.value.email).toEqual(['Email này đã được liên kết với một tài khoản khác.'])
    expect(auth.isSubmitting.value).toBe(false)
  })

  it('chống gửi nhiều yêu cầu khi bấm liên tục (debounce / submission lock)', async () => {
    let callCount = 0
    const fetchMock = vi.fn().mockImplementation(() => {
      callCount++
      return new Promise(resolve => {
        setTimeout(() => {
          resolve(
            new Response(JSON.stringify({ data: { acknowledged: true } }), {
              status: 200,
              headers: { 'Content-Type': 'application/json' }
            })
          )
        }, 50)
      })
    })
    vi.stubGlobal('fetch', fetchMock)

    const auth = createAuthStore('http://localhost:5000')

    // Bấm liên tiếp 4 lần khi đang gửi
    const p1 = auth.login('user1', 'Pass123')
    const p2 = auth.login('user1', 'Pass123')
    const p3 = auth.login('user1', 'Pass123')
    const p4 = auth.login('user1', 'Pass123')

    await Promise.all([p1, p2, p3, p4])

    // Chỉ có đúng 1 yêu cầu mạng duy nhất được gửi đi
    expect(callCount).toBe(1)
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('không ghi mật khẩu thô vào console log hoặc nhật ký hệ thống', async () => {
    const consoleSpy = vi.spyOn(console, 'log').mockImplementation(() => {})
    const consoleWarn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})

    const fetchMock = vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(JSON.stringify({ error: { code: 'AUTH_FAIL', message: 'Thất bại' } }), {
          status: 400,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    )
    vi.stubGlobal('fetch', fetchMock)

    const sensitivePass = 'SuperSecretPlainPassword_999!'
    const auth = createAuthStore('http://localhost:5000')
    await auth.login('player', sensitivePass)

    const allLoggedArgs = [
      ...consoleSpy.mock.calls.flat(),
      ...consoleWarn.mock.calls.flat(),
      ...consoleError.mock.calls.flat()
    ].map(String)

    const leaked = allLoggedArgs.some(arg => arg.includes(sensitivePass))
    expect(leaked).toBe(false)

    consoleSpy.mockRestore()
    consoleWarn.mockRestore()
    consoleError.mockRestore()
  })
})
