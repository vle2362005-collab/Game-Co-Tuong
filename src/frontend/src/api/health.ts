export interface HealthResponse {
  status: 'Healthy'
  service: 'game-co-tuong-api'
}

export async function checkHealth(baseUrl: string, signal?: AbortSignal): Promise<HealthResponse> {
  const url = new URL('/health', baseUrl)
  if (url.protocol !== 'http:' && url.protocol !== 'https:') {
    throw new Error('Địa chỉ API phải dùng HTTP hoặc HTTPS.')
  }
  const response = await fetch(url, { signal })
  if (!response.ok) throw new Error(`Máy chủ phản hồi HTTP ${response.status}.`)
  const data: unknown = await response.json()
  if (typeof data !== 'object' || data === null
    || !('status' in data) || data.status !== 'Healthy'
    || !('service' in data) || data.service !== 'game-co-tuong-api') {
    throw new Error('Phản hồi không đúng định dạng của máy chủ Cờ Tướng.')
  }
  return { status: 'Healthy', service: 'game-co-tuong-api' }
}
