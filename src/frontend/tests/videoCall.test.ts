import { describe, it, expect, vi, afterEach } from 'vitest'
import { createVideoCall, type VideoSignal } from '../src/composables/useVideoCall'
import { boardPoint, displayCell } from '../src/components/boardLayout'
function fixture() {
  const tracks = [{ kind: 'audio', enabled: true, stop: vi.fn() }, { kind: 'video', enabled: true, stop: vi.fn() }]
  const stream = { getTracks: () => tracks } as unknown as MediaStream
  const peer = { close: vi.fn(), addTrack: vi.fn(), addIceCandidate: vi.fn(), remoteDescription: null as unknown,
    localDescription: { type: 'offer', sdp: 'test' }, createOffer: async () => ({}), createAnswer: async () => ({}),
    setLocalDescription: vi.fn(), setRemoteDescription: vi.fn(async (value: unknown) => { peer.remoteDescription = value }),
    onicecandidate: null, ontrack: null, onconnectionstatechange: null }
  const send = vi.fn().mockResolvedValue(undefined), media = vi.fn().mockResolvedValue(stream)
  const call = createVideoCall({ send, media, peer: () => peer as unknown as RTCPeerConnection })
  return { call, tracks, send, media, peer }
}
const offer: VideoSignal = { roomId: 'room', senderId: 'other', recipientId: 'me', callId: 'call', type: 'offer', data: '{"type":"offer","sdp":"test"}' }
afterEach(() => vi.restoreAllMocks())
describe('Video call lifecycle', () => {
  it('waits for explicit acceptance, buffers ICE, and stops every track on hangup', async () => {
    const { call, tracks, send, media, peer } = fixture()
    await call.receive(offer); expect(media).not.toHaveBeenCalled(); expect(call.status.value).toBe('incoming')
    await call.receive({ ...offer, type: 'ice', data: '{"candidate":"candidate-test"}' }); expect(peer.addIceCandidate).not.toHaveBeenCalled()
    await call.accept(); expect(media).toHaveBeenCalledOnce(); expect(peer.addIceCandidate).toHaveBeenCalledOnce()
    expect(send.mock.calls[0][0].type).toBe('answer')
    call.toggle('audio'); expect(tracks[0].enabled).toBe(false)
    call.toggle('video'); expect(tracks[1].enabled).toBe(false)
    await call.hangup(); expect(peer.close).toHaveBeenCalledOnce(); tracks.forEach(t => expect(t.stop).toHaveBeenCalledOnce())
    expect(call.local.value).toBeNull(); expect(call.status.value).toBe('idle')
  })
  it('declining and stale ICE never request camera permission', async () => {
    const { call, send, media } = fixture()
    await call.receive(offer); await call.decline()
    expect(send.mock.calls[0][0].type).toBe('reject')
    await call.receive({ ...offer, type: 'ice', data: '{}' })
    expect(media).not.toHaveBeenCalled(); expect(call.status.value).toBe('idle')
  })
  it('cancelling a pending permission request stops the stream when it eventually arrives', async () => {
    const { call, media, tracks } = fixture()
    let resolve!: (stream: MediaStream) => void
    media.mockImplementation(() => new Promise<MediaStream>(r => { resolve = r }))
    const pending = call.start('other'); await call.hangup()
    resolve({ getTracks: () => tracks } as unknown as MediaStream); await pending
    expect(call.status.value).toBe('idle'); tracks.forEach(t => expect(t.stop).toHaveBeenCalledOnce())
  })
  it('maps all 90 cells correctly in both views and after CSS scaling', () => {
    for (const flipped of [false,true]) for (let col=0; col<9; col++) for (let row=0; row<10; row++) {
      const visible=displayCell(col,row,flipped), point=boardPoint(visible.col,visible.row,flipped)
      expect(point).toEqual({ x:40+col*50,y:40+row*50 })
      for (const scale of [.5,1,2]) expect((point.x*scale-40*scale)/(50*scale)).toBe(col)
    }
  })
})
