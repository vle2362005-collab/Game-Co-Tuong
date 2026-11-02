import { afterEach, describe, expect, it, vi } from 'vitest'
import { createSSRApp } from 'vue'
import { renderToString } from 'vue/server-renderer'
import { gameApi, type Match } from '../src/api/game'
import { createMatchActions } from '../src/composables/useMatchActions'
import { serverSecondsRemaining } from '../src/composables/useSnapshotClock'
import MatchRecoveryPanel from '../src/components/MatchRecoveryPanel.vue'
import XiangqiBoard from '../src/components/XiangqiBoard.vue'

function state(overrides: Partial<Match> = {}): Match {
  return { id: 'match', roomId: 'room', mode: 'pvp', aiDifficulty: 'medium', version: 1,
    status: 'playing', moveNumber: 0, sideToMove: 'red', pieces: [], winner: null,
    resultReason: null, reasonCode: null, result: null, check: false,
    players: [{ userId: 'me', side: 'red' }, { userId: 'other', side: 'black' }],
    drawOfferId: null, drawOfferedBy: null, drawCreatedAt: null, drawExpiresAt: null,
    serverTime: '2026-11-02T10:00:00Z', ...overrides }
}
afterEach(() => vi.restoreAllMocks())

describe('GCT-16/GCT-87 confirmed match controls', () => {
  it('cancelling surrender sends nothing; confirming sends confirmed=true', async () => {
    const confirm = vi.fn().mockReturnValueOnce(false).mockReturnValueOnce(true)
    const surrender = vi.spyOn(gameApi, 'surrender').mockResolvedValue(state({ status: 'finished' }))
    const actions = createMatchActions('match', () => state(), () => 'red', confirm)
    expect(await actions.surrender()).toBeNull()
    expect(surrender).not.toHaveBeenCalled()
    await actions.surrender()
    expect(surrender).toHaveBeenCalledExactlyOnceWith('match', true)
  })
  it('accepts or rejects the displayed offer id without sending another offer', async () => {
    const match = state({ drawOfferId: 'offer-1', drawOfferedBy: 'black' })
    const accept = vi.spyOn(gameApi, 'acceptDraw').mockResolvedValue(state())
    const reject = vi.spyOn(gameApi, 'rejectDraw').mockResolvedValue(state())
    const offer = vi.spyOn(gameApi, 'offerDraw').mockResolvedValue(state())
    const actions = createMatchActions('match', () => match, () => 'red', () => true)
    await actions.respondDraw('offer-1', true)
    await actions.respondDraw('offer-1', false)
    expect(accept).toHaveBeenCalledExactlyOnceWith('match', 'offer-1')
    expect(reject).toHaveBeenCalledExactlyOnceWith('match', 'offer-1')
    expect(offer).not.toHaveBeenCalled()
  })
  it('ignores stale/self offers, paused/finished actions and draw negotiation with AI', async () => {
    let match = state({ drawOfferId: 'new-offer', drawOfferedBy: 'red' })
    const accept = vi.spyOn(gameApi, 'acceptDraw')
    const offer = vi.spyOn(gameApi, 'offerDraw')
    const surrender = vi.spyOn(gameApi, 'surrender')
    const actions = createMatchActions('match', () => match, () => 'red', () => true)
    expect(await actions.respondDraw('old-offer', true)).toBeNull()
    expect(await actions.respondDraw('new-offer', true)).toBeNull()
    match = state({ status: 'paused' })
    expect(await actions.surrender()).toBeNull()
    match = state({ status: 'finished' })
    expect(await actions.offerDraw()).toBeNull()
    match = state({ mode: 'pve' })
    expect(await actions.offerDraw()).toBeNull()
    expect(accept).not.toHaveBeenCalled(); expect(offer).not.toHaveBeenCalled(); expect(surrender).not.toHaveBeenCalled()
  })
  it('sends a fresh draw offer only for a playing PvP match without a pending offer', async () => {
    let match = state()
    const offer = vi.spyOn(gameApi, 'offerDraw').mockResolvedValue(state())
    const actions = createMatchActions('match', () => match, () => 'red', () => true)
    await actions.offerDraw()
    expect(offer).toHaveBeenCalledExactlyOnceWith('match')
    match = state({ drawOfferId: 'pending' })
    expect(await actions.offerDraw()).toBeNull()
    expect(offer).toHaveBeenCalledOnce()
  })
})

describe('GCT-110 authoritative recovery display', () => {
  it('uses server time despite a wrong device clock; zero never invents a result', () => {
    vi.spyOn(Date, 'now').mockReturnValue(Date.parse('2035-01-01T00:00:00Z'))
    const match = state({ status: 'paused', blackDeadlineAt: '2026-11-02T10:01:00Z' })
    expect(serverSecondsRemaining(match, match.blackDeadlineAt)).toBe(60)
    expect(serverSecondsRemaining(match, match.blackDeadlineAt, 20_100)).toBe(40)
    expect(serverSecondsRemaining(match, match.blackDeadlineAt, 61_000)).toBe(0)
    expect(match.status).toBe('paused'); expect(match.result).toBeNull(); expect(match.winner).toBeNull()
  })
  it('does not guess a countdown when server timing is missing or invalid', () => {
    expect(serverSecondsRemaining(state({ serverTime: undefined }), '2026-11-02T10:01:00Z')).toBeNull()
    expect(serverSecondsRemaining(state(), 'invalid')).toBeNull()
  })
  it('shows the disconnected side and restart recovery without replacing the match', async () => {
    const match = state({ status: 'paused', recoveryReason: 'serverRestart', blackDeadlineAt: '2026-11-02T10:01:00Z' })
    const html = await renderToString(createSSRApp(MatchRecoveryPanel, { match, connected: true, busy: false }))
    expect(html).toContain('Trận đang tạm dừng')
    expect(html).toContain('Máy chủ vừa khởi động lại')
    expect(html).toContain('Đen:')
    expect(html).toContain('60 giây để nối lại')
    expect(html).toContain('chờ kết quả từ máy chủ')
    expect(match.id).toBe('match')
  })
  it('removes the recovery panel after the server resumes the match', async () => {
    const html = await renderToString(createSSRApp(MatchRecoveryPanel, { match: state(), connected: true, busy: false }))
    expect(html).not.toContain('Trận đang tạm dừng')
    expect(html).not.toContain('Kết nối lại và đồng bộ')
  })
})

describe('GCT-83 board check and hint rendering', () => {
  it('renders selected piece, server hints, last move and checked General in either orientation', async () => {
    for (const flipped of [false, true]) {
      const html = await renderToString(createSSRApp(XiangqiBoard, {
        pieces: [{ id: 'red-general-1', type: 'General', side: 'Red', position: { col: 4, row: 9 } }],
        flipped, selected: 'red-general-1', checkedGeneralId: 'red-general-1', destinations: [{ col: 4, row: 8 }],
        lastMove: { requestId: 'move', ply: 1, side: 'black', pieceId: 'black-chariot-1', fromCol: 0, fromRow: 0, toCol: 0, toRow: 1, capturedPieceId: null }
      }))
      expect(html).toContain('data-selected="red-general-1"')
      expect(html).toContain('data-legal="4-8"')
      expect(html).toContain('data-check="true"')
      expect(html).toContain('data-last-to="true"')
    }
  })
  it('clears check, selection and hints from the next confirmed state', async () => {
    const html = await renderToString(createSSRApp(XiangqiBoard, { pieces: [], selected: null, checkedGeneralId: null, destinations: [], disabled: true }))
    expect(html).not.toContain('data-check=')
    expect(html).not.toContain('data-legal=')
    expect(html).not.toContain('data-selected=')
  })
})
