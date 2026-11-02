import { describe, expect, it } from 'vitest'
import { buildReplayFrames } from '../src/replay/replay'
import type { MatchReplayMove, Piece } from '../src/api/game'

const initialPieces: Piece[] = [
  { id: 'red-general-1', type: 'General', side: 'Red', position: { col: 4, row: 9 } },
  { id: 'black-general-1', type: 'General', side: 'Black', position: { col: 4, row: 0 } },
  { id: 'red-chariot-1', type: 'Chariot', side: 'Red', position: { col: 0, row: 8 } },
  { id: 'black-horse-1', type: 'Horse', side: 'Black', position: { col: 0, row: 3 } }
]

function serialized(pieces: Piece[]) { return JSON.stringify(pieces) }

const captureMove: MatchReplayMove = {
  requestId: 'move-1',
  ply: 1,
  side: 'red',
  pieceId: 'red-chariot-1',
  fromCol: 0,
  fromRow: 8,
  toCol: 0,
  toRow: 3,
  capturedPieceId: 'black-horse-1',
  createdAt: '2026-11-02T10:00:00Z',
  boardSnapshotJson: serialized([
    initialPieces[0],
    initialPieces[1],
    { ...initialPieces[2], position: { col: 0, row: 3 } }
  ])
}
const blackGeneralMove: MatchReplayMove = {
  requestId: 'move-2',
  ply: 2,
  side: 'black',
  pieceId: 'black-general-1',
  fromCol: 4,
  fromRow: 0,
  toCol: 3,
  toRow: 0,
  capturedPieceId: null,
  createdAt: '2026-11-02T10:00:01Z',
  boardSnapshotJson: serialized([
    initialPieces[0],
    { ...initialPieces[1], position: { col: 3, row: 0 } },
    { ...initialPieces[2], position: { col: 0, row: 3 } }
  ])
}

describe('GCT-114 match replay', () => {
  it('builds the initial and move frames with captured piece and current turn', () => {
    const frames = buildReplayFrames(serialized(initialPieces), blackGeneralMove.boardSnapshotJson!, [captureMove, blackGeneralMove])

    expect(frames).toHaveLength(3)
    expect(frames[0]).toMatchObject({ ply: 0, sideToMove: 'red', lastMove: null, capturedPiece: null })
    expect(frames[0].pieces).toHaveLength(4)
    expect(frames[1]).toMatchObject({
      ply: 1,
      sideToMove: 'black',
      capturedPiece: initialPieces[3],
      lastMove: { requestId: 'move-1', side: 'red', capturedPieceId: 'black-horse-1' }
    })
    expect(frames[1].pieces).toHaveLength(3)
    expect(frames[2]).toMatchObject({
      ply: 2,
      sideToMove: 'red',
      capturedPiece: null,
      lastMove: { requestId: 'move-2', side: 'black' }
    })
  })

  it('rejects a gap or wrong turn in saved move history', () => {
    expect(() => buildReplayFrames(serialized(initialPieces), blackGeneralMove.boardSnapshotJson!, [
      { ...captureMove, ply: 2 }
    ])).toThrow('Thứ tự hoặc lượt đi trong lịch sử trận không hợp lệ.')
    expect(() => buildReplayFrames(serialized(initialPieces), blackGeneralMove.boardSnapshotJson!, [
      { ...captureMove, side: 'black' }
    ])).toThrow('Thứ tự hoặc lượt đi trong lịch sử trận không hợp lệ.')
  })

  it('reconstructs from the saved move sequence when per-move snapshots are absent', () => {
    const frames = buildReplayFrames(serialized(initialPieces), blackGeneralMove.boardSnapshotJson!, [
      { ...captureMove, boardSnapshotJson: null },
      { ...blackGeneralMove, boardSnapshotJson: null }
    ])

    expect(frames[1].capturedPiece).toEqual(initialPieces[3])
    const finalPieces: Piece[] = JSON.parse(blackGeneralMove.boardSnapshotJson!)
    expect(frames[2].pieces).toHaveLength(finalPieces.length)
    for (const piece of finalPieces)
      expect(frames[2].pieces.find(candidate => candidate.id === piece.id)).toEqual(piece)
  })

  it('rejects a per-move snapshot that differs from the saved move', () => {
    expect(() => buildReplayFrames(serialized(initialPieces), captureMove.boardSnapshotJson!, [
      { ...captureMove, boardSnapshotJson: serialized(initialPieces) }
    ])).toThrow('Snapshot không khớp nước 1.')
  })

  it('rejects a final frame that differs from the persisted final board', () => {
    const differentBoard = serialized(initialPieces)
    expect(() => buildReplayFrames(serialized(initialPieces), differentBoard, [captureMove]))
      .toThrow('Bàn cờ phát lại không khớp snapshot cuối của trận.')
  })
})
