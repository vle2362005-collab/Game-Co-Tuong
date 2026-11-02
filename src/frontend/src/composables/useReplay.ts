import { computed, ref } from 'vue'
import type { ConfirmedMove, Piece } from '../api/game'

export interface ReplayMove extends ConfirmedMove { boardSnapshotJson: string }
export interface ReplayData {
  id: string; status: string; playerSide: string; moveNumber: number
  initialBoardJson: string; currentBoardJson: string; moves: ReplayMove[]
}
const fail = () => { throw new Error('Dữ liệu phát lại thiếu hoặc không khớp. Không thể dựng bàn cờ.') }
function board(json: string): Piece[] {
  let data: unknown
  try { data = JSON.parse(json) } catch { return fail() }
  if (!Array.isArray(data)) return fail()
  const ids = new Set<string>(), cells = new Set<string>()
  return data.map((piece: Piece) => {
    if (!piece || typeof piece.id !== 'string' || typeof piece.type !== 'string' || typeof piece.side !== 'string') return fail()
    const { col, row } = piece.position || {}
    if (!Number.isInteger(col) || !Number.isInteger(row) || col < 0 || col > 8 || row < 0 || row > 9 ||
      !['red', 'black'].includes(piece.side.toLowerCase()) || !['general','advisor','elephant','horse','chariot','cannon','soldier'].includes(piece.type.toLowerCase()) ||
      ids.has(piece.id) || cells.has(`${col}-${row}`)) return fail()
    ids.add(piece.id); cells.add(`${col}-${row}`)
    return { id: piece.id, type: piece.type.toLowerCase(), side: piece.side.toLowerCase(), position: { col, row } }
  })
}
function signature(pieces: Piece[]) {
  return JSON.stringify([...pieces].sort((a, b) => a.id.localeCompare(b.id)))
}
export function createReplay(data: ReplayData) {
  if (!data || !['finished','cancelled'].includes(data.status) || !Array.isArray(data.moves) ||
    !Number.isSafeInteger(data.moveNumber) || data.moveNumber !== data.moves.length) return fail()
  const frames: Piece[][] = [board(data.initialBoardJson)]
  for (const [index, move] of data.moves.entries()) {
    if (!move || typeof move !== 'object') return fail()
    const before = frames[index]!, piece = before.find(p => p.id === move.pieceId)
    const target = before.find(p => p.position.col === move.toCol && p.position.row === move.toRow)
    const side = index % 2 ? 'black' : 'red'
    if (move.ply !== index + 1 || move.side !== side || !piece || piece.side !== side ||
      piece.position.col !== move.fromCol || piece.position.row !== move.fromRow ||
      !Number.isInteger(move.toCol) || !Number.isInteger(move.toRow) || move.toCol < 0 || move.toCol > 8 || move.toRow < 0 || move.toRow > 9 ||
      (target?.id || null) !== move.capturedPieceId || target?.side === side) return fail()
    const next = before.filter(p => p.id !== move.capturedPieceId).map(p => p.id === move.pieceId ? { ...p, position: { col: move.toCol, row: move.toRow } } : p)
    if (signature(next) !== signature(board(move.boardSnapshotJson))) return fail()
    frames.push(next)
  }
  if (signature(frames[frames.length - 1]!) !== signature(board(data.currentBoardJson))) return fail()
  const cursor = ref(0), total = data.moves.length
  function seek(ply: number) { if (Number.isFinite(ply)) cursor.value = Math.max(0, Math.min(total, Math.trunc(ply))) }
  return {
    cursor, total, seek,
    pieces: computed(() => frames[cursor.value]!),
    sideToMove: computed(() => cursor.value % 2 ? 'black' : 'red'),
    lastMove: computed(() => cursor.value ? data.moves[cursor.value - 1]! : null),
    captured: computed(() => frames[0]!.filter(p => !frames[cursor.value]!.some(current => current.id === p.id)))
  }
}
