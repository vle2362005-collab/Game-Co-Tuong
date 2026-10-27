import {
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'

export interface MatchPiece {
  id: string
  type: 'general' | 'advisor' | 'elephant' | 'horse' | 'chariot' | 'cannon' | 'soldier'
  side: 'red' | 'black'
  position: { col: number; row: number }
}

export interface MatchReconnectState {
  matchId: string
  side: 'red' | 'black'
  pieces: MatchPiece[]
  sideToMove: 'red' | 'black'
  moveNumber: number
  version: number
  status: string
  redRemainingMs: number
  blackRemainingMs: number
}

const pieceTypes = new Set<string>([
  'general',
  'advisor',
  'elephant',
  'horse',
  'chariot',
  'cannon',
  'soldier',
])

function isPieceType(value: unknown): value is MatchPiece['type'] {
  return typeof value === 'string' && pieceTypes.has(value)
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function isInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value)
}

export function parseMatchReconnectState(value: unknown): MatchReconnectState {
  if (!isRecord(value)
    || typeof value.matchId !== 'string'
    || (value.side !== 'red' && value.side !== 'black')
    || typeof value.currentBoardJson !== 'string'
    || (value.sideToMove !== 'red' && value.sideToMove !== 'black')
    || !isInteger(value.moveNumber)
    || !isInteger(value.version)
    || typeof value.status !== 'string'
    || !isInteger(value.redRemainingMs)
    || !isInteger(value.blackRemainingMs)) {
    throw new Error('Dữ liệu khôi phục trận đấu không đúng định dạng.')
  }

  const parsedBoard: unknown = JSON.parse(value.currentBoardJson)
  if (!Array.isArray(parsedBoard)) {
    throw new Error('Snapshot bàn cờ không đúng định dạng.')
  }

  const pieces = parsedBoard.map((piece): MatchPiece => {
    if (!isRecord(piece)
      || typeof piece.id !== 'string'
      || !isPieceType(piece.type)
      || (piece.side !== 'red' && piece.side !== 'black')
      || !isRecord(piece.position)
      || !isInteger(piece.position.col)
      || !isInteger(piece.position.row)
      || piece.position.col < 0
      || piece.position.col > 8
      || piece.position.row < 0
      || piece.position.row > 9) {
      throw new Error('Snapshot chứa quân cờ hoặc tọa độ không hợp lệ.')
    }

    return {
      id: piece.id,
      type: piece.type,
      side: piece.side,
      position: {
        col: piece.position.col,
        row: piece.position.row,
      },
    }
  })

  return {
    matchId: value.matchId,
    side: value.side,
    pieces,
    sideToMove: value.sideToMove,
    moveNumber: value.moveNumber,
    version: value.version,
    status: value.status,
    redRemainingMs: value.redRemainingMs,
    blackRemainingMs: value.blackRemainingMs,
  }
}

export async function connectToMatch(
  matchId: string,
  getAccessToken: () => string,
  onStateRestored: (state: MatchReconnectState) => void,
  onError: (error: Error) => void,
): Promise<HubConnection> {
  const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'
  const hubUrl = new URL('/hubs/matches', baseUrl).toString()
  const connection = new HubConnectionBuilder()
    .withUrl(hubUrl, { accessTokenFactory: getAccessToken })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  const restore = async () => {
    try {
      const state: unknown = await connection.invoke('Reconnect', matchId)
      onStateRestored(parseMatchReconnectState(state))
    } catch (error) {
      onError(error instanceof Error ? error : new Error('Không thể khôi phục trận đấu.'))
    }
  }

  connection.on('MatchStateRestored', (state: unknown) => {
    try {
      onStateRestored(parseMatchReconnectState(state))
    } catch (error) {
      onError(error instanceof Error ? error : new Error('Không thể đọc trạng thái trận đấu.'))
    }
  })
  connection.on('MatchReconnectExpired', () => {
    onError(new Error('Thời hạn kết nối lại trận đấu đã hết.'))
  })
  connection.onreconnected(() => {
    void restore()
  })
  connection.onclose((error) => {
    if (error) onError(error)
  })

  try {
    await connection.start()
    await restore()
    return connection
  } catch (error) {
    await connection.stop()
    throw error
  }
}
