export interface User {
  id: string
  username: string
  displayName: string
}

export interface AuthSession {
  accessToken: string
  tokenType: string
  expiresAt: string
  user: User
}

export interface MatchInfo {
  id: string
  roomId: string
  side: 'red' | 'black'
  opponentName: string
}

export type AppView = 'login' | 'lobby' | 'room' | 'game' | 'history'

export interface RoomInfo {
  id: string
  code: string
  name: string
  hostId: string
  hostName: string
  status: 'waiting' | 'playing' | 'closed'
  membersCount: number
}

export interface MatchHistoryItem {
  id: string
  roomId: string
  mode: string
  status: string
  outcome: 'win' | 'loss' | 'draw' | 'cancelled' | 'in-progress'
  winner: string | null
  resultReason: string | null
  reasonCode: string | null
  playerSide: 'red' | 'black'
  opponent: { userId: string | null; displayName: string; side: string } | null
  startedAt: string
  endedAt: string | null
  durationSeconds: number
  moveNumber: number
}

export interface MatchHistoryPage {
  items: MatchHistoryItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}
