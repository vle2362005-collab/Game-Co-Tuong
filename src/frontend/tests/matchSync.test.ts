import { describe, it, expect, vi } from 'vitest'
import { createMatchSync } from '../src/composables/useMatchSync'
import type { Match } from '../src/api/game'
function state(version: number, id = 'match'): Match {
  return { id, roomId: 'room', version, status: 'playing', moveNumber: version - 1, sideToMove: version % 2 ? 'red' : 'black', pieces: [], winner: null, resultReason: null, check: false, players: [], drawOfferedBy: null }
}
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(r => { resolve = r }); return { promise, resolve } }
describe('GCT-82 confirmed snapshots', () => {
  it('ignores duplicate, older, wrong-match and malformed events without clearing selection', async () => {
    const changed = vi.fn(), selected = { value: 'red-horse-1' }
    const sync = createMatchSync('match', async () => state(1), () => { selected.value = ''; changed() }, vi.fn())
    await sync.refresh(); selected.value = 'red-horse-1'
    expect(sync.accept(state(1))).toBe(false); expect(sync.accept(state(0))).toBe(false)
    expect(sync.accept(state(2, 'another'))).toBe(false); expect(sync.accept(state(Number.NaN))).toBe(false)
    expect(changed).toHaveBeenCalledOnce(); expect(selected.value).toBe('red-horse-1'); expect(sync.snapshot.value?.version).toBe(1)
  })
  it('reloads an event gap once and coalesces duplicate events while loading', async () => {
    const response = deferred<Match>(), load = vi.fn().mockResolvedValueOnce(state(1)).mockReturnValue(response.promise)
    const sync = createMatchSync('match', load, vi.fn(), vi.fn()); await sync.refresh()
    sync.accept(state(3)); sync.accept(state(3)); expect(sync.needsRefresh.value).toBe(true); expect(sync.snapshot.value?.version).toBe(1)
    const pending = sync.refresh(); response.resolve(state(3)); await pending
    expect(load).toHaveBeenCalledTimes(2); expect(sync.snapshot.value?.version).toBe(3); expect(sync.needsRefresh.value).toBe(false)
  })
  it('does not let an older API response overwrite a newer live event', async () => {
    const response = deferred<Match>(), changed = vi.fn()
    const sync = createMatchSync('match', () => response.promise, changed, vi.fn())
    const pending = sync.refresh(); sync.accept(state(4)); response.resolve(state(1)); await pending
    expect(sync.snapshot.value?.version).toBe(4); expect(changed).toHaveBeenCalledOnce()
  })
  it('performs one further read when a newer event arrives during gap recovery', async () => {
    const response = deferred<Match>(), load = vi.fn().mockResolvedValueOnce(state(1)).mockReturnValueOnce(response.promise).mockResolvedValueOnce(state(4))
    const sync = createMatchSync('match', load, vi.fn(), vi.fn()); await sync.refresh()
    sync.accept(state(3)); sync.accept(state(4)); const pending = sync.refresh(); response.resolve(state(3)); await pending
    expect(load).toHaveBeenCalledTimes(3); expect(sync.snapshot.value?.version).toBe(4); expect(sync.needsRefresh.value).toBe(false)
  })
  it('keeps the last confirmed board on read failure and supports retry', async () => {
    const failure = vi.fn(), load = vi.fn().mockResolvedValueOnce(state(1)).mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce(state(3))
    const sync = createMatchSync('match', load, vi.fn(), failure); await sync.refresh(); sync.accept(state(3)); await sync.refresh()
    expect(sync.snapshot.value?.version).toBe(1); expect(sync.needsRefresh.value).toBe(true); expect(failure).toHaveBeenCalledOnce()
    await sync.refresh(); expect(sync.snapshot.value?.version).toBe(3); expect(sync.needsRefresh.value).toBe(false)
  })
  it('disposal ignores late fetches and later events from a departed screen', async () => {
    const response = deferred<Match>(), changed = vi.fn(), failure = vi.fn()
    const sync = createMatchSync('match', () => response.promise, changed, failure)
    const pending = sync.refresh(); sync.dispose(); response.resolve(state(1)); await pending
    expect(sync.accept(state(2))).toBe(false); expect(changed).not.toHaveBeenCalled(); expect(failure).not.toHaveBeenCalled()
  })
  it('updates captured pieces, last move, turn and check together from one accepted snapshot', () => {
    const sync = createMatchSync('match', async () => state(1), vi.fn(), vi.fn())
    sync.accept({ ...state(1), pieces: [{ id: 'black-horse-1', type: 'Horse', side: 'Black', position: {col:0,row:2} }] })
    const next = { ...state(2), pieces: [], check: true, lastMove: { requestId: 'r1', ply: 1, side: 'red', pieceId: 'red-chariot-1', fromCol: 0, fromRow: 5, toCol: 0, toRow: 2, capturedPieceId: 'black-horse-1' } }
    sync.accept(next); sync.accept(next)
    expect(sync.snapshot.value).toEqual(next); expect(sync.snapshot.value?.pieces).toHaveLength(0)
  })
})
