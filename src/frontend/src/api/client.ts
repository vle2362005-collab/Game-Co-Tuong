export interface ApiClientConfig {
  baseUrl?: string
  timeoutMs?: number
  defaultRetries?: number
  retryDelayMs?: number
  getToken?: () => string | null
  onUnauthorized?: () => void
}

export interface RequestOptions extends Omit<RequestInit, 'body'> {
  body?: unknown
  timeoutMs?: number
  retries?: number
  retryDelayMs?: number
  skipAuth?: boolean
}

export class ApiClientError extends Error {
  constructor(
    message: string,
    public status?: number,
    public code?: string,
    public details?: unknown,
    public isNetworkError: boolean = false,
    public isTimeout: boolean = false
  ) {
    super(message)
    this.name = 'ApiClientError'
  }
}

export function createApiClient(config: ApiClientConfig = {}) {
  const getBaseUrl = (): string => {
    if (config.baseUrl) return config.baseUrl
    try {
      return (import.meta.env?.VITE_API_BASE_URL as string) || 'http://localhost:5000'
    } catch {
      return 'http://localhost:5000'
    }
  }

  const defaultTimeoutMs = config.timeoutMs ?? 10000
  const defaultRetries = config.defaultRetries ?? 2
  const defaultRetryDelayMs = config.retryDelayMs ?? 300

  async function delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms))
  }

  async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
    const baseUrl = getBaseUrl()
    const url = new URL(path, baseUrl).toString()
    const method = (options.method || 'GET').toUpperCase()

    // Default: defaultRetries for GET, 0 for others unless explicitly specified
    const maxRetries = options.retries !== undefined
      ? options.retries
      : (method === 'GET' ? defaultRetries : 0)

    const timeoutMs = options.timeoutMs ?? defaultTimeoutMs
    const retryDelay = options.retryDelayMs ?? defaultRetryDelayMs

    let lastError: ApiClientError | null = null

    for (let attempt = 0; attempt <= maxRetries; attempt++) {
      const controller = new AbortController()
      let timeoutId: ReturnType<typeof setTimeout> | null = null

      if (timeoutMs > 0) {
        timeoutId = setTimeout(() => {
          controller.abort()
        }, timeoutMs)
      }

      // If external signal is provided, forward its abort
      if (options.signal) {
        options.signal.addEventListener('abort', () => controller.abort(), { once: true })
      }

      try {
        const headers = new Headers(options.headers || {})
        if (!headers.has('Accept')) {
          headers.set('Accept', 'application/json')
        }

        // Attach Authorization header if token exists and skipAuth is not set
        if (!options.skipAuth && !headers.has('Authorization')) {
          const token = config.getToken ? config.getToken() : null
          if (token) {
            headers.set('Authorization', `Bearer ${token}`)
          }
        }

        let body: BodyInit | undefined = undefined
        if (options.body !== undefined && options.body !== null) {
          if (typeof options.body === 'string' || options.body instanceof FormData || options.body instanceof Blob) {
            body = options.body as BodyInit
          } else {
            headers.set('Content-Type', 'application/json')
            body = JSON.stringify(options.body)
          }
        }

        const fetchOptions: RequestInit = {
          ...options,
          method,
          headers,
          body,
          signal: controller.signal
        }

        const response = await fetch(url, fetchOptions)

        if (timeoutId) clearTimeout(timeoutId)

        // Handle 401 Unauthorized
        if (response.status === 401) {
          if (config.onUnauthorized) {
            config.onUnauthorized()
          }
        }

        // Parse response body
        const contentType = response.headers.get('content-type') || ''
        let data: unknown = null
        if (contentType.includes('application/json')) {
          try {
            data = await response.json()
          } catch {
            data = null
          }
        } else {
          try {
            data = await response.text()
          } catch {
            data = null
          }
        }

        if (!response.ok) {
          let errorMessage = `Yêu cầu thất bại (${response.status})`
          let errorCode = `HTTP_${response.status}`
          let errorDetails: unknown = null

          if (data && typeof data === 'object') {
            const errObj = (data as { error?: { message?: string; code?: string; details?: unknown } }).error
            if (errObj) {
              if (errObj.message) errorMessage = errObj.message
              if (errObj.code) errorCode = errObj.code
              if (errObj.details) errorDetails = errObj.details
            }
          }

          const clientError = new ApiClientError(
            errorMessage,
            response.status,
            errorCode,
            errorDetails,
            false,
            false
          )

          // Only retry 5xx server errors, NEVER retry 4xx client errors
          if (response.status >= 500 && attempt < maxRetries) {
            lastError = clientError
            await delay(retryDelay * Math.pow(2, attempt))
            continue
          }

          throw clientError
        }

        // Return parsed data or unpack standard ApiSuccessResponse
        if (data && typeof data === 'object' && 'data' in data) {
          return (data as { data: T }).data
        }

        return data as T
      } catch (err: unknown) {
        if (timeoutId) clearTimeout(timeoutId)

        if (err instanceof ApiClientError) {
          throw err
        }

        const isAbort = (err instanceof DOMException && err.name === 'AbortError') ||
          (typeof err === 'object' && err !== null && 'name' in err && (err as { name: string }).name === 'AbortError')

        const clientError = isAbort
          ? new ApiClientError('Yêu cầu vượt quá thời gian chờ (timeout).', undefined, 'TIMEOUT', null, false, true)
          : new ApiClientError(
              err instanceof Error ? err.message : 'Lỗi kết nối mạng đến máy chủ.',
              undefined,
              'NETWORK_ERROR',
              err,
              true,
              false
            )

        // Retry network failures if retries remain and it wasn't an external abort
        if (attempt < maxRetries && !options.signal?.aborted) {
          lastError = clientError
          await delay(retryDelay * Math.pow(2, attempt))
          continue
        }

        throw lastError || clientError
      }
    }

    throw lastError || new ApiClientError('Không thể hoàn tất yêu cầu.', undefined, 'REQUEST_FAILED')
  }

  return {
    getBaseUrl,
    request,
    get<T>(path: string, options?: RequestOptions): Promise<T> {
      return request<T>(path, { ...options, method: 'GET' })
    },
    post<T>(path: string, body?: unknown, options?: RequestOptions): Promise<T> {
      return request<T>(path, { ...options, method: 'POST', body })
    },
    put<T>(path: string, body?: unknown, options?: RequestOptions): Promise<T> {
      return request<T>(path, { ...options, method: 'PUT', body })
    },
    delete<T>(path: string, options?: RequestOptions): Promise<T> {
      return request<T>(path, { ...options, method: 'DELETE' })
    }
  }
}

// Global default singleton
export const apiClient = createApiClient()
