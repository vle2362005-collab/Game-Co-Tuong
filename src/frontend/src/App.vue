<script setup lang="ts">
import { computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuth } from './composables/useAuth'
import AppHeader from './components/AppHeader.vue'
import SurrenderModal from './components/SurrenderModal.vue'
import type { MatchInfo } from './types/auth'

const route = useRoute()
const router = useRouter()

const apiBaseUrl = (typeof import.meta !== 'undefined' && import.meta.env?.VITE_API_BASE_URL) || 'http://localhost:5000'
const auth = useAuth(apiBaseUrl)

const defaultMatch = computed<MatchInfo>(() => auth.activeMatch.value || {
  id: 'demo-match-01',
  roomId: 'demo-room-01',
  side: 'red',
  opponentName: 'Kỳ thủ phương Bắc'
})

async function handleLogin(payload: { username: string; password: string }) {
  const ok = await auth.login(payload.username, payload.password)
  if (ok && router) {
    const redirect = (route?.query?.redirect as string) || '/lobby'
    router.push(redirect)
  }
}

async function handleLogout() {
  await auth.requestLogout()
  if (!auth.showSurrenderModal.value && router && router.currentRoute.value.path !== '/login') {
    router.push('/login')
  }
}

async function handleConfirmSurrender() {
  await auth.confirmSurrenderAndLogout()
  if (router && router.currentRoute.value.path !== '/login') {
    router.push('/login')
  }
}

function handleEnterMatch(match: MatchInfo) {
  auth.enterMatch(match)
  if (router) {
    router.push(`/game/${match.roomId}`)
  }
}

function handleStartMatch(payload: { roomId: string; side: 'red' | 'black'; opponentName: string }) {
  auth.enterMatch({
    id: crypto.randomUUID(),
    roomId: payload.roomId,
    side: payload.side,
    opponentName: payload.opponentName
  })
  if (router) {
    router.push(`/game/${payload.roomId}`)
  }
}

function handleLeaveMatch() {
  auth.leaveMatch()
  if (router) {
    router.push('/lobby')
  }
}

watch(
  () => auth.sessionExpired.value,
  (expired) => {
    if (expired && router && router.currentRoute.value.path !== '/login') {
      router.push('/login')
    }
  }
)
</script>

<template>
  <main class="shell">
    <AppHeader
      :is-authenticated="auth.isAuthenticated.value"
      :is-playing="auth.isPlaying.value"
      :user="auth.user.value"
      @logout="handleLogout"
    />

    <!-- Main Content rendered via Vue Router Shell -->
    <section class="main-content">
      <router-view v-slot="{ Component }">
        <component
          :is="Component"
          :user="auth.user.value"
          :session-expired="auth.sessionExpired.value"
          :is-submitting="auth.isSubmitting.value"
          :error-message="auth.errorMessage.value"
          :match="defaultMatch"
          @login="handleLogin"
          @enter-match="handleEnterMatch"
          @start-match="handleStartMatch"
          @leave-room="handleLeaveMatch"
          @surrender="handleLogout"
          @test-expire="auth.handleSessionExpired"
        />
      </router-view>
    </section>

    <!-- Surrender Confirmation Modal (active during gameplay logout attempt) -->
    <SurrenderModal
      :show="auth.showSurrenderModal.value"
      @cancel="auth.cancelLogout"
      @confirm="handleConfirmSurrender"
    />

    <footer>
      <span>Vue 3 · Vue Router 4 · TypeScript · ASP.NET Core</span>
      <span>Dự án Game Cờ Tướng · Nhánh GCT-32-vue-shell</span>
    </footer>
  </main>
</template>

<style scoped>
.shell {
  max-width: 1080px;
  min-height: 100vh;
  margin: 0 auto;
  padding: 0 24px 24px;
  display: flex;
  flex-direction: column;
}

.main-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  padding: 24px 0;
}

footer {
  border-top: 1px solid #d6dcd0;
  padding: 20px 0;
  display: flex;
  justify-content: space-between;
  gap: 12px;
  font-size: 0.75rem;
  color: #65746d;
}

@media (max-width: 600px) {
  .shell {
    padding: 0 16px 16px;
  }
  footer {
    flex-direction: column;
    text-align: center;
  }
}
</style>
