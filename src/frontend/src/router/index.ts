import {
  createRouter,
  createWebHistory,
  createMemoryHistory,
  type RouteRecordRaw,
  type Router,
  type RouterHistory
} from 'vue-router'
import LoginView from '../components/LoginView.vue'
import LobbyView from '../components/LobbyView.vue'
import RoomView from '../components/RoomView.vue'
import GameView from '../components/GameView.vue'
import HistoryView from '../components/HistoryView.vue'
import { useAuth } from '../composables/useAuth'

export const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: LoginView,
    meta: { title: 'Đăng nhập' }
  },
  {
    path: '/register',
    name: 'register',
    component: LoginView,
    props: { initialMode: 'register' },
    meta: { title: 'Đăng ký tài khoản' }
  },
  {
    path: '/lobby',
    name: 'lobby',
    component: LobbyView,
    meta: { requiresAuth: true, title: 'Sảnh chờ' }
  },
  {
    path: '/rooms/:id',
    name: 'room',
    component: RoomView,
    meta: { requiresAuth: true, title: 'Phòng cờ' }
  },
  {
    path: '/room/:id',
    redirect: to => `/rooms/${to.params.id}`
  },
  {
    path: '/game/:id?',
    name: 'game',
    component: GameView,
    meta: { requiresAuth: true, title: 'Ván đấu' }
  },
  {
    path: '/match/:id',
    redirect: to => `/game/${to.params.id}`
  },
  {
    path: '/history',
    name: 'history',
    component: HistoryView,
    meta: { requiresAuth: true, title: 'Lịch sử ván đấu' }
  },
  {
    path: '/',
    name: 'root',
    redirect: () => {
      const hasToken = checkAuthToken()
      return hasToken ? '/lobby' : '/login'
    }
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    redirect: '/'
  }
]

export function checkAuthToken(): boolean {
  try {
    const auth = useAuth()
    return auth.isAuthenticated.value
  } catch {
    return false
  }
}

export interface RouterOptions {
  history?: RouterHistory
  isAuthenticated?: () => boolean
}

export function createAppRouter(options: RouterOptions = {}): Router {
  const history = options.history ?? (
    typeof window !== 'undefined' && window.history
      ? createWebHistory()
      : createMemoryHistory()
  )

  const isAuth = options.isAuthenticated ?? checkAuthToken

  const router = createRouter({
    history,
    routes
  })

  router.beforeEach((to, _from, next) => {
    const authenticated = isAuth()

    if (to.meta.requiresAuth && !authenticated) {
      if (to.path === '/login') {
        next()
        return
      }
      next({
        path: '/login',
        query: { redirect: to.fullPath }
      })
      return
    }

    if ((to.path === '/login' || to.path === '/register') && authenticated) {
      next('/lobby')
      return
    }

    next()
  })

  return router
}

export const router = createAppRouter()
