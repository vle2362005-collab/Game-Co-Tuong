import type { ConfirmedMove, MatchReplayMove, Piece } from '../api/game'

export interface ReplayFrame {
  ply: number
  sideToMove: 'red' | 'black'
  pieces: Piece[]
  lastMove: ConfirmedMove | null
  capturedPiece: Piece | null
}

function parseBoard(json: string): Piece[] {
  const value: unknown = JSON.parse(json)
  if (!Array.isArray(value)) throw new Error('Dữ liệu snapshot bàn cờ không hợp lệ.')

  return value.map((piece): Piece => {
    if (piece === null || typeof piece !== 'object') throw new Error('Dữ liệu quân cờ trong snapshot không hợp lệ.')
    const candidate = piece as Record<string, unknown>
    const position = candidate.position
    if (
      typeof candidate.id !== 'string' ||
      typeof candidate.type !== 'string' ||
      typeof candidate.side !== 'string' ||
      position === null ||
      typeof position !== 'object'
    ) throw new Error('Dữ liệu quân cờ trong snapshot không hợp lệ.')

    const coordinates = position as Record<string, unknown>
    if (
      !Number.isInteger(coordinates.col) || (coordinates.col as number) < 0 || (coordinates.col as number) > 8 ||
      !Number.isInteger(coordinates.row) || (coordinates.row as number) < 0 || (coordinates.row as number) > 9
    ) throw new Error('Tọa độ quân cờ trong snapshot không hợp lệ.')

    return {
      id: candidate.id,
      type: candidate.type,
      side: candidate.side,
      position: { col: coordinates.col as number, row: coordinates.row as number }
    }
  })
}

function boardKey(pieces: Piece[]): string {
  return JSON.stringify(pieces
    .map(piece => [piece.id, piece.type.toLowerCase(), piece.side.toLowerCase(), piece.position.col, piece.position.row])
    .sort(([leftId], [rightId]) => String(leftId).localeCompare(String(rightId))))
}

export function buildReplayFrames(initialBoardJson: string, currentBoardJson: string, moves: MatchReplayMove[]): ReplayFrame[] {
  const initialPieces = parseBoard(initialBoardJson)
  const frames: ReplayFrame[] = [{
    ply: 0,
    sideToMove: 'red',
    pieces: initialPieces,
    lastMove: null,
    capturedPiece: null
  }]

  for (const [index, move] of moves.entries()) {
    const expectedPly = index + 1
    const side = move.side.toLowerCase()
    const previous = frames[index]
    if (move.ply !== expectedPly || side !== previous.sideToMove)
      throw new Error('Thứ tự hoặc lượt đi trong lịch sử trận không hợp lệ.')
    const movingPiece = previous.pieces.find(piece => piece.id === move.pieceId)
    const validCoordinates = (Number.isInteger(move.fromCol) && move.fromCol >= 0 && move.fromCol <= 8 &&
      Number.isInteger(move.fromRow) && move.fromRow >= 0 && move.fromRow <= 9 &&
      Number.isInteger(move.toCol) && move.toCol >= 0 && move.toCol <= 8 &&
      Number.isInteger(move.toRow) && move.toRow >= 0 && move.toRow <= 9)
    if (
      !movingPiece ||
      movingPiece.side.toLowerCase() !== side ||
      movingPiece.position.col !== move.fromCol ||
      movingPiece.position.row !== move.fromRow ||
      !validCoordinates
    ) throw new Error(`Snapshot không khớp nước ${move.ply}.`)

    const target = previous.pieces.find(piece => piece.position.col === move.toCol && piece.position.row === move.toRow)
    if ((target?.id ?? null) !== move.capturedPieceId || target?.side.toLowerCase() === side)
      throw new Error(`Dữ liệu quân bị ăn không khớp nước ${move.ply}.`)
    const capturedPiece = target ?? null
    const pieces = previous.pieces
      .filter(piece => piece.id !== movingPiece.id && piece.id !== capturedPiece?.id)
      .concat({ ...movingPiece, position: { col: move.toCol, row: move.toRow } })

    if (move.boardSnapshotJson && boardKey(parseBoard(move.boardSnapshotJson)) !== boardKey(pieces))
      throw new Error(`Snapshot không khớp nước ${move.ply}.`)

    frames.push({
      ply: move.ply,
      sideToMove: side === 'red' ? 'black' : 'red',
      pieces,
      lastMove: {
        requestId: move.requestId,
        ply: move.ply,
        side: move.side,
        pieceId: move.pieceId,
        fromCol: move.fromCol,
        fromRow: move.fromRow,
        toCol: move.toCol,
        toRow: move.toRow,
        capturedPieceId: move.capturedPieceId
      },
      capturedPiece
    })
  }

  if (boardKey(frames.at(-1)!.pieces) !== boardKey(parseBoard(currentBoardJson)))
    throw new Error('Bàn cờ phát lại không khớp snapshot cuối của trận.')

  return frames
}
