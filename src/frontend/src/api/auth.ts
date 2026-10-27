import type { AuthSession, User } from '../types/auth'

export async function loginApi(
  baseUrl: string,
  username: string,
  password: string,
  signal?: AbortSignal
): Promise<AuthSession> {
  const url = new URL('/api/v1/auth/login', baseUrl)
  const response = await fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json'
    },
    body: JSON.stringify({
      requestId: crypto.randomUUID(),
      username,
      password
    }),
    signal
  })

  if (!response.ok) {
    let errorMessage = `Đăng nhập thất bại (${response.status})`
    try {
      const errorData = await response.json()
      if (errorData?.error?.message) {
        errorMessage = errorData.error.message
      }
    } catch {
      // Ignore json parse error
    }
    throw new Error(errorMessage)
  }

  const result = await response.json()
  return result.data as AuthSession
}

export async function logoutApi(
  baseUrl: string,
  token: string | null,
  signal?: AbortSignal
): Promise<boolean> {
  if (!token) return true

  const url = new URL('/api/v1/auth/logout', baseUrl)
  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: 'application/json'
      },
      signal
    })
    return response.ok
  } catch {
    // Graceful offline/network failure handling
    return false
  }
}

export async function fetchMeApi(
  baseUrl: string,
  token: string,
  signal?: AbortSignal
): Promise<User> {
  const url = new URL('/api/v1/users/me', baseUrl)
  const response = await fetch(url, {
    method: 'GET',
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json'
    },
    signal
  })

  if (!response.ok) {
    throw new Error(`Phiên không hợp lệ hoặc đã hết hạn (${response.status})`)
  }

  const result = await response.json()
  return result.data as User
}
