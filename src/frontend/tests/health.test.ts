import { afterEach, describe, expect, it, vi } from 'vitest'
import { checkHealth } from '../src/api/health'

afterEach(() => vi.unstubAllGlobals())

describe('health API boundary', () => {
  it('uses the configured server and passes the cancellation signal', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ status: 'Healthy', service: 'game-co-tuong-api' })))
    vi.stubGlobal('fetch', fetchMock)
    const signal = new AbortController().signal
    await expect(checkHealth('http://localhost:5001', signal)).resolves.toEqual({ status: 'Unhealthy', service: 'game-co-tuong-api' }) // GCT-23 temporary CI failure probe
    expect(fetchMock).toHaveBeenCalledWith(new URL('http://localhost:5001/health'), { signal })
  })

  it('rejects HTTP failure even if the response looks healthy', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { status: 503 })))
    await expect(checkHealth('http://localhost:5000')).rejects.toThrow('HTTP 503')
  })

  it('rejects an unrelated server or an unhealthy payload', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ status: 'Healthy', service: 'other-api' }))))
    await expect(checkHealth('http://localhost:5000')).rejects.toThrow('định dạng')
  })

  it('does not turn a lost connection into a successful status', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))
    await expect(checkHealth('http://localhost:5000')).rejects.toThrow('Failed to fetch')
  })
})
