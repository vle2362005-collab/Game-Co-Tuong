import { ref } from 'vue'
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'

export interface SignalRClientOptions {
  baseUrl?: string
  getToken?: () => string | null
  onUnauthorized?: () => void
}
type Handler = (...args: unknown[]) => void
export function createSignalRClient(options: SignalRClientOptions = {}) {
  const isConnected = ref(false)
  const connectionId = ref<string | null>(null)
  let connection: HubConnection | null = null
  let starting: Promise<boolean> | null = null
  const handlers = new Map<string, Set<Handler>>()
  function on(event: string, handler: Handler) {
    if (!handlers.has(event)) handlers.set(event, new Set())
    handlers.get(event)!.add(handler)
    connection?.on(event, handler)
    return () => { handlers.get(event)?.delete(handler); connection?.off(event, handler) }
  }
  async function connect(hubPath = '/hubs/game'): Promise<boolean> {
    if (!options.getToken?.()) return false
    if (connection?.state === HubConnectionState.Connected) return true
    if (starting) return starting
    const active = new HubConnectionBuilder()
      .withUrl(new URL(hubPath, options.baseUrl || import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000').toString(), {
        accessTokenFactory: () => options.getToken?.() || ''
      }).configureLogging(LogLevel.None).withAutomaticReconnect([0, 1000, 3000, 5000]).build()
    connection = active
    for (const [event, list] of handlers) for (const handler of list) active.on(event, handler)
    active.onreconnecting(() => { isConnected.value = false })
    active.onreconnected(id => {
      isConnected.value = true; connectionId.value = id || null
      for (const handler of handlers.get('Reconnected') || []) handler()
    })
    active.onclose(() => { isConnected.value = false; connectionId.value = null })
    starting = (async () => {
      try {
        await active.start()
        if (connection !== active) { await active.stop(); return false }
        isConnected.value = true; connectionId.value = active.connectionId
        return true
      } catch (error) {
        isConnected.value = false
        if (String(error).includes('401')) options.onUnauthorized?.()
        return false
      } finally { starting = null }
    })()
    return starting
  }
  function disconnect() {
    const old = connection; connection = null
    isConnected.value = false; connectionId.value = null
    void old?.stop()
  }
  async function invoke<T = void>(method: string, ...args: unknown[]): Promise<T> {
    if (!connection || connection.state !== HubConnectionState.Connected) throw new Error('Kết nối trực tuyến chưa sẵn sàng.')
    return connection.invoke<T>(method, ...args)
  }
  return { isConnected, connectionId, connect, disconnect, invoke, on }
}
export type SignalRClient = ReturnType<typeof createSignalRClient>
