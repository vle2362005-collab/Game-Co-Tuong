import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { createApiClient, ApiClientError } from '../src/api/client'

describe('GCT-32: API Client - Cấu hình, xử lý chờ, lỗi và thử lại', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('đọc đúng địa chỉ base URL và gửi kèm header mặc định', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ data: { message: 'ok' } }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
    )
    vi.stubGlobal('fetch', fetchMock)

    const client = createApiClient({
      baseUrl: 'http://custom-server:5000',
      getToken: () => 'my-jwt-token'
    })

    const res = await client.get<{ message: string }>('/api/v1/test')

    expect(res).toEqual({ message: 'ok' })
    expect(fetchMock).toHaveBeenCalledWith(
      'http://custom-server:5000/api/v1/test',
      expect.objectContaining({
        method: 'GET',
        headers: expect.any(Headers)
      })
    )

    const sentHeaders = fetchMock.mock.calls[0][1].headers as Headers
    expect(sentHeaders.get('Authorization')).toBe('Bearer my-jwt-token')
    expect(sentHeaders.get('Accept')).toBe('application/json')
  })

  it('phân tích đúng định dạng lỗi API chuẩn ApiErrorEnvelope và không bị mắc kẹt', async () => {
    const errorBody = {
      error: {
        code: 'ROOM_NOT_FOUND',
        message: 'Phòng cờ không tồn tại hoặc đã bị đóng.',
        details: { roomId: 'room-123' }
      }
    }

    const fetchMock = vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(JSON.stringify(errorBody), {
          status: 404,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    )
    vi.stubGlobal('fetch', fetchMock)

    const client = createApiClient({ baseUrl: 'http://localhost:5000' })

    await expect(client.get('/api/v1/rooms/room-123')).rejects.toThrow(ApiClientError)

    try {
      await client.get('/api/v1/rooms/room-123')
    } catch (err) {
      expect(err).toBeInstanceOf(ApiClientError)
      const apiErr = err as ApiClientError
      expect(apiErr.status).toBe(404)
      expect(apiErr.code).toBe('ROOM_NOT_FOUND')
      expect(apiErr.message).toBe('Phòng cờ không tồn tại hoặc đã bị đóng.')
      expect(apiErr.details).toEqual({ roomId: 'room-123' })
    }
  })

  it('khi nhận mã 401 thì gọi hàm onUnauthorized để xử lý phiên', async () => {
    const onUnauthorized = vi.fn()
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ error: { code: 'UNAUTHORIZED', message: 'Token đã hết hạn.' } }), {
        status: 401,
        headers: { 'Content-Type': 'application/json' }
      })
    )
    vi.stubGlobal('fetch', fetchMock)

    const client = createApiClient({
      baseUrl: 'http://localhost:5000',
      onUnauthorized
    })

    await expect(client.get('/api/v1/users/me')).rejects.toThrow(ApiClientError)
    expect(onUnauthorized).toHaveBeenCalledTimes(1)
  })

  it('không thử lại khi gặp lỗi client 4xx (400, 401, 404)', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ error: { code: 'BAD_REQUEST', message: 'Dữ liệu không hợp lệ.' } }), {
        status: 400,
        headers: { 'Content-Type': 'application/json' }
      })
    )
    vi.stubGlobal('fetch', fetchMock)

    const client = createApiClient({
      baseUrl: 'http://localhost:5000',
      defaultRetries: 3
    })

    await expect(client.get('/api/v1/test')).rejects.toThrow(ApiClientError)

    // Chỉ gọi đúng 1 lần, không retry vô nghĩa
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('tự động thử lại khi gặp lỗi server 5xx và thành công ở lần thử tiếp theo', async () => {
    const fetchMock = vi.fn()
      // Lần 1: lỗi 503 Service Unavailable
      .mockResolvedValueOnce(
        new Response('Service Unavailable', { status: 503, headers: { 'Content-Type': 'text/plain' } })
      )
      // Lần 2: thành công 200 OK
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ data: { success: true } }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    vi.stubGlobal('fetch', fetchMock)

    const client = createApiClient({
      baseUrl: 'http://localhost:5000',
      defaultRetries: 2,
      retryDelayMs: 10
    })

    const res = await client.get<{ success: boolean }>('/api/v1/retry-test')
    expect(res).toEqual({ success: true })
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('tự động thử lại khi gặp sự cố mạng (network failure) và báo lỗi rõ ràng nếu hết số lần thử', async () => {
    const fetchMock = vi.fn()
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))

    vi.stubGlobal('fetch', fetchMock)

    const client = createApiClient({
      baseUrl: 'http://localhost:5000',
      defaultRetries: 1,
      retryDelayMs: 10
    })

    await expect(client.get('/api/v1/network-fail')).rejects.toThrow(ApiClientError)
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('xử lý chờ (timeout) khi yêu cầu phản hồi quá thời gian quy định', async () => {
    // Mô phỏng fetch bị abort do timeout
    const fetchMock = vi.fn().mockImplementation((_url, options: RequestInit) => {
      return new Promise((_resolve, reject) => {
        if (options.signal) {
          options.signal.addEventListener('abort', () => {
            const err = new DOMException('The user aborted a request.', 'AbortError')
            reject(err)
          })
        }
      })
    })
    vi.stubGlobal('fetch', fetchMock)

    const client = createApiClient({
      baseUrl: 'http://localhost:5000',
      timeoutMs: 30,
      defaultRetries: 0
    })

    try {
      await client.get('/api/v1/slow-request')
      expect.unreachable('Should have timed out')
    } catch (err) {
      expect(err).toBeInstanceOf(ApiClientError)
      const apiErr = err as ApiClientError
      expect(apiErr.isTimeout).toBe(true)
      expect(apiErr.code).toBe('TIMEOUT')
    }
  })

  it('thực hiện các phương thức POST, PUT, DELETE đúng định dạng', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ data: { acknowledged: true } }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
    )
    vi.stubGlobal('fetch', fetchMock)

    const client = createApiClient({ baseUrl: 'http://localhost:5000' })

    await client.post('/api/v1/posts', { title: 'Test' })
    expect(fetchMock).toHaveBeenLastCalledWith(
      'http://localhost:5000/api/v1/posts',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ title: 'Test' })
      })
    )

    await client.put('/api/v1/posts/1', { title: 'Updated' })
    expect(fetchMock).toHaveBeenLastCalledWith(
      'http://localhost:5000/api/v1/posts/1',
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({ title: 'Updated' })
      })
    )

    await client.delete('/api/v1/posts/1')
    expect(fetchMock).toHaveBeenLastCalledWith(
      'http://localhost:5000/api/v1/posts/1',
      expect.objectContaining({
        method: 'DELETE'
      })
    )
  })
})
