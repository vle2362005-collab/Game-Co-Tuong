import { describe, expect, it } from 'vitest'
import { parseMatchReconnectState } from '../src/api/matchReconnect'

const restoredState = {
  matchId: '4a697e72-fb86-42eb-b254-4745525d9c31',
  side: 'red',
  currentBoardJson: JSON.stringify([
    {
      id: 'red-general-1',
      type: 'general',
      side: 'red',
      position: { col: 4, row: 9 },
    },
  ]),
  sideToMove: 'black',
  moveNumber: 12,
  version: 13,
  status: 'playing',
  redRemainingMs: 53000,
  blackRemainingMs: 42000,
}

describe('parseMatchReconnectState', () => {
  it('parses the persisted board and match turn metadata', () => {
    expect(parseMatchReconnectState(restoredState)).toEqual({
      matchId: restoredState.matchId,
      side: 'red',
      pieces: [{
        id: 'red-general-1',
        type: 'general',
        side: 'red',
        position: { col: 4, row: 9 },
      }],
      sideToMove: 'black',
      moveNumber: 12,
      version: 13,
      status: 'playing',
      redRemainingMs: 53000,
      blackRemainingMs: 42000,
    })
  })

  it('rejects invalid board coordinates rather than rendering a partial snapshot', () => {
    const invalidState = {
      ...restoredState,
      currentBoardJson: JSON.stringify([{
        id: 'red-general-1',
        type: 'general',
        side: 'red',
        position: { col: 9, row: 9 },
      }]),
    }

    expect(() => parseMatchReconnectState(invalidState)).toThrow('tọa độ không hợp lệ')
  })
})
