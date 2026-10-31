import { apiClient } from './client'
export interface Member { userId: string; displayName: string; side: 'red' | 'black'; ready: boolean; connected: boolean }
export interface Room { id: string; code: string; name: string; hostUserId: string; status: string; version: number; matchId: string | null; members: Member[] }
export interface Piece { id: string; type: string; side: string; position: { col: number; row: number } }
export interface LegalMove { pieceId: string; to: { col: number; row: number } }
export interface ConfirmedMove { requestId: string; ply: number; side: string; pieceId: string; fromCol: number; fromRow: number; toCol: number; toRow: number; capturedPieceId: string | null }
export interface Match { id: string; roomId: string; status: string; version: number; moveNumber: number; sideToMove: string; pieces: Piece[]; winner: string | null; resultReason: string | null; check: boolean; players: { userId: string; side: string }[]; drawOfferedBy: string | null; legalMoves?: LegalMove[]; lastMove?: ConfirmedMove | null }
export interface Message { id: string; roomId: string; senderId: string; senderName: string; content: string; sentAt: string; sequence: number }
export const gameApi = {
  rooms: () => apiClient.get<Room[]>('/api/v1/rooms'),
  room: (id: string) => apiClient.get<Room>(`/api/v1/rooms/${id}`),
  create: (name: string, requestId: string) => apiClient.post<Room>('/api/v1/rooms', { name, requestId }),
  join: (code: string) => apiClient.post<Room>(`/api/v1/rooms/join/${encodeURIComponent(code)}`),
  ready: (id: string, ready: boolean) => apiClient.post<Room>(`/api/v1/rooms/${id}/ready`, { ready }),
  start: (id: string, requestId: string) => apiClient.post<Room>(`/api/v1/rooms/${id}/start`, { requestId }),
  leave: (id: string) => apiClient.post<Room>(`/api/v1/rooms/${id}/leave`),
  match: (id: string) => apiClient.get<Match>(`/api/v1/matches/${id}`),
  move: (id: string, pieceId: string, col: number, row: number, expectedVersion: number, requestId: string) => apiClient.post<Match>(`/api/v1/matches/${id}/moves`, { pieceId, toCol: col, toRow: row, expectedVersion, requestId }),
  end: (id: string, draw = false) => apiClient.post<Match>(`/api/v1/matches/${id}/${draw ? 'draw' : 'resign'}`),
  messages: (id: string) => apiClient.get<Message[]>(`/api/v1/rooms/${id}/messages`)
}
