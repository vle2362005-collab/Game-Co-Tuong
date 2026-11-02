import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from '../src/api/client'
import { createReplay, type ReplayData } from '../src/composables/useReplay'
import type { Piece } from '../src/api/game'
import { createAppRouter } from '../src/router'
import { createMemoryHistory } from 'vue-router'

function fixture(): ReplayData {
  const initial: Piece[] = [
    { id: 'red-chariot-1', type: 'Chariot', side: 'Red', position: { col: 0, row: 5 } },
    { id: 'black-horse-1', type: 'Horse', side: 'Black', position: { col: 0, row: 2 } },
    { id: 'black-soldier-1', type: 'Soldier', side: 'Black', position: { col: 8, row: 3 } }
  ]
  const first = [ { ...initial[0]!, position: { col: 0, row: 2 } }, initial[2]! ]
  const last = [ first[0]!, { ...initial[2]!, position: { col: 8, row: 4 } } ]
  return { id: 'match', status: 'finished', playerSide: 'black', moveNumber: 2,
    initialBoardJson: JSON.stringify(initial), currentBoardJson: JSON.stringify(last), moves: [
      { requestId: 'r1', ply: 1, side: 'red', pieceId: 'red-chariot-1', fromCol: 0, fromRow: 5, toCol: 0, toRow: 2, capturedPieceId: 'black-horse-1', boardSnapshotJson: JSON.stringify(first) },
      { requestId: 'r2', ply: 2, side: 'black', pieceId: 'black-soldier-1', fromCol: 8, fromRow: 3, toCol: 8, toRow: 4, capturedPieceId: null, boardSnapshotJson: JSON.stringify(last) }
    ] }
}
afterEach(() => vi.restoreAllMocks())
describe('GCT-114/GCT-92 read-only replay', () => {
  it('steps capture/turn forwards and backwards, clamps controls and never writes', () => {
    const data = fixture(), original = JSON.stringify(data), post = vi.spyOn(apiClient, 'post')
    const replay = createReplay(data)
    expect(replay.pieces.value).toHaveLength(3); expect(replay.sideToMove.value).toBe('red')
    replay.seek(1)
    expect(replay.sideToMove.value).toBe('black'); expect(replay.captured.value.map(p => p.id)).toEqual(['black-horse-1'])
    expect(replay.lastMove.value?.requestId).toBe('r1')
    replay.seek(99); expect(replay.cursor.value).toBe(2)
    expect(replay.pieces.value.find(p => p.id === 'black-soldier-1')?.position.row).toBe(4)
    replay.seek(1); replay.seek(-1); expect(replay.cursor.value).toBe(0)
    expect(replay.captured.value).toEqual([]); expect(replay.lastMove.value).toBeNull()
    expect(JSON.stringify(data)).toBe(original); expect(post).not.toHaveBeenCalled()
  })
  it.each(['missing', 'gap', 'capture', 'snapshot', 'final', 'from', 'turn', 'bounds'])(
    'rejects corrupt history: %s', kind => {
      const data = fixture()
      if (kind === 'missing') data.moves.pop()
      if (kind === 'gap') data.moves[0]!.ply = 2
      if (kind === 'capture') data.moves[0]!.capturedPieceId = null
      if (kind === 'snapshot') data.moves[0]!.boardSnapshotJson = 'invalid'
      if (kind === 'final') data.currentBoardJson = data.initialBoardJson
      if (kind === 'from') data.moves[0]!.fromRow = 6
      if (kind === 'turn') data.moves[0]!.side = 'black'
      if (kind === 'bounds') data.moves[0]!.toCol = 9
      expect(() => createReplay(data)).toThrow('Dữ liệu phát lại')
    })
  it('accepts a finished zero-move match but rejects an active match or duplicate squares', () => {
    const data = fixture(); data.moves = []; data.moveNumber = 0; data.currentBoardJson = data.initialBoardJson
    expect(createReplay(data).total).toBe(0)
    expect(() => createReplay({ ...data, status: 'playing' })).toThrow()
    const pieces = JSON.parse(data.initialBoardJson) as Piece[]
    pieces[1]!.position = pieces[0]!.position; data.initialBoardJson = JSON.stringify(pieces)
    expect(() => createReplay(data)).toThrow()
  })
  it('requires login before opening a replay route', async () => {
    const router = createAppRouter({ history: createMemoryHistory(), isAuthenticated: () => false })
    await router.push('/history/match/replay')
    expect(router.currentRoute.value.path).toBe('/login')
    expect(router.currentRoute.value.query.redirect).toBe('/history/match/replay')
  })
})
