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

export type AppView = 'login' | 'lobby' | 'game'
