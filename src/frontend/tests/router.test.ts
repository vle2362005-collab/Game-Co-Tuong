import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { createMemoryHistory } from 'vue-router'
import { createAppRouter } from '../src/router'

describe('GCT-32: Cấu hình Vue Router và điều hướng màn hình', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', {
      getItem: vi.fn(),
      setItem: vi.fn(),
      removeItem: vi.fn(),
      clear: vi.fn()
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('chưa đăng nhập truy cập /lobby thì chuyển hướng về /login kèm redirect query', async () => {
    const router = createAppRouter({
      history: createMemoryHistory(),
      isAuthenticated: () => false
    })

    await router.push('/lobby')
    await router.isReady()

    expect(router.currentRoute.value.path).toBe('/login')
    expect(router.currentRoute.value.query.redirect).toBe('/lobby')
  })

  it('đã đăng nhập truy cập /login thì tự động chuyển tiếp vào /lobby', async () => {
    const router = createAppRouter({
      history: createMemoryHistory(),
      isAuthenticated: () => true
    })

    await router.push('/login')
    await router.isReady()

    expect(router.currentRoute.value.path).toBe('/lobby')
  })

  it('chuyển đúng màn hình phòng cờ theo mã id và hỗ trợ alias /room/:id', async () => {
    const router = createAppRouter({
      history: createMemoryHistory(),
      isAuthenticated: () => true
    })

    await router.push('/rooms/room-vip-888')
    await router.isReady()

    expect(router.currentRoute.value.name).toBe('room')
    expect(router.currentRoute.value.params.id).toBe('room-vip-888')

    // Alias /room/:id chuyển hướng về /rooms/:id
    await router.push('/room/room-alias-123')
    expect(router.currentRoute.value.path).toBe('/rooms/room-alias-123')
    expect(router.currentRoute.value.params.id).toBe('room-alias-123')
  })

  it('chuyển đúng màn hình ván đấu và hỗ trợ alias /match/:id', async () => {
    const router = createAppRouter({
      history: createMemoryHistory(),
      isAuthenticated: () => true
    })

    await router.push('/game/match-777')
    await router.isReady()

    expect(router.currentRoute.value.name).toBe('game')
    expect(router.currentRoute.value.params.id).toBe('match-777')

    // Alias /match/:id chuyển hướng về /game/:id
    await router.push('/match/match-888')
    expect(router.currentRoute.value.path).toBe('/game/match-888')
    expect(router.currentRoute.value.params.id).toBe('match-888')
  })

  it('chuyển đúng màn hình lịch sử đấu /history khi đã đăng nhập', async () => {
    const router = createAppRouter({
      history: createMemoryHistory(),
      isAuthenticated: () => true
    })

    await router.push('/history')
    await router.isReady()

    expect(router.currentRoute.value.name).toBe('history')
    expect(router.currentRoute.value.path).toBe('/history')
  })

  it('truy cập root / điều hướng về /lobby nếu đã đăng nhập và /login nếu chưa đăng nhập', async () => {
    // Trường hợp chưa đăng nhập
    const routerGuest = createAppRouter({
      history: createMemoryHistory(),
      isAuthenticated: () => false
    })
    await routerGuest.push('/')
    await routerGuest.isReady()
    expect(routerGuest.currentRoute.value.path).toBe('/login')

    // Trường hợp đã đăng nhập
    const routerUser = createAppRouter({
      history: createMemoryHistory(),
      isAuthenticated: () => true
    })
    await routerUser.push('/')
    await routerUser.isReady()
    expect(routerUser.currentRoute.value.path).toBe('/lobby')
  })
})
