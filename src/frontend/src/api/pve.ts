import { apiClient } from './client'
import type { LegalMove, ConfirmedMove, Piece, MatchResult } from './game'

export interface PveMatch {
  id: string
  roomId: string
  mode: string
  status: string
  version: number
  moveNumber: number
  sideToMove: string
  pieces: Piece[]
  winner: string | null
  resultReason: string | null
  reasonCode: string | null
  result: MatchResult | null
  check: boolean
  legalMoves: LegalMove[]
  lastMove: ConfirmedMove | null
  players: { userId: string; side: string }[]
  playerSide: 'red'
}

export type Difficulty = 'easy' | 'medium' | 'hard'

export const pveApi = {
  /** Create a new PvE match. Human = Red, Bot = Black. */
  create: (difficulty: Difficulty, requestId: string) =>
    apiClient.post<PveMatch>('/api/v1/pve/matches', { difficulty, requestId }),

  /** Fetch current match snapshot. */
  match: (id: string) =>
    apiClient.get<PveMatch>(`/api/v1/pve/matches/${id}`),

  /** Submit human's move. */
  move: (id: string, pieceId: string, toCol: number, toRow: number, expectedVersion: number, requestId: string, difficulty: Difficulty) =>
    apiClient.post<PveMatch>(`/api/v1/pve/matches/${id}/moves`, {
      pieceId, toCol, toRow, expectedVersion, requestId, difficulty
    }),

  /** Human surrenders. */
  surrender: (id: string) =>
    apiClient.post<PveMatch>(`/api/v1/pve/matches/${id}/surrender`)
}

