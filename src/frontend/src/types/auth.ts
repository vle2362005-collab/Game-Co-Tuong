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
  opponentName: string
  side: 'red' | 'black'
  result: 'win' | 'loss' | 'draw'
  resultReason: string
  playedAt: string
  movesCount: number
}

