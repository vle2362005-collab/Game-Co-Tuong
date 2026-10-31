import { ref } from 'vue'

export interface SignalRClientOptions {
  baseUrl?: string
  getToken?: () => string | null
  onUnauthorized?: () => void
}

export function createSignalRClient(options: SignalRClientOptions = {}) {
  const isConnected = ref(false)
  const connectionId = ref<string | null>(null)
  let activeAbortController: AbortController | null = null

  const getBaseUrl = (): string => {
    if (options.baseUrl) return options.baseUrl
    try {
      return (import.meta.env?.VITE_API_BASE_URL as string) || 'http://localhost:5000'
    } catch {
      return 'http://localhost:5000'
    }
  }

  async function connect(hubPath: string = '/hubs/game'): Promise<boolean> {
    const token = options.getToken ? options.getToken() : null
    if (!token) {
      isConnected.value = false
      return false
    }

    // Close any previous connection
    disconnect()

    activeAbortController = new AbortController()
    const baseUrl = getBaseUrl()
    const url = new URL(`${hubPath}/negotiate`, baseUrl)
    url.searchParams.set('negotiateVersion', '1')
    url.searchParams.set('access_token', token)

    try {
      const response = await fetch(url.toString(), {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${token}`,
          Accept: 'application/json'
        },
        signal: activeAbortController.signal
      })

      if (response.status === 401) {
        disconnect()
        if (options.onUnauthorized) {
          options.onUnauthorized()
        }
        return false
      }

      if (!response.ok) {
        disconnect()
        return false
      }

      const data = await response.json()
      connectionId.value = data?.connectionId || crypto.randomUUID()
      isConnected.value = true
      return true
    } catch {
      disconnect()
      return false
    }
  }

  function disconnect() {
    if (activeAbortController) {
      activeAbortController.abort()
      activeAbortController = null
    }
    isConnected.value = false
    connectionId.value = null
  }

  return {
    isConnected,
    connectionId,
    connect,
    disconnect
  }
}

export type SignalRClient = ReturnType<typeof createSignalRClient>
