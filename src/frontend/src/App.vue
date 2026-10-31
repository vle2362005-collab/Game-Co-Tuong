<script setup lang="ts">
import { watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuth } from './composables/useAuth'
import AppHeader from './components/AppHeader.vue'
import SurrenderModal from './components/SurrenderModal.vue'
import type { RegisterPayload } from './api/auth'

const route = useRoute()
const router = useRouter()

const apiBaseUrl = (typeof import.meta !== 'undefined' && import.meta.env?.VITE_API_BASE_URL) || 'http://localhost:5000'
const auth = useAuth(apiBaseUrl)

async function handleLogin(payload: { username: string; password: string }) {
  const ok = await auth.login(payload.username, payload.password)
  if (ok && router) {
    const redirect = (route?.query?.redirect as string) || '/lobby'
    router.push(redirect)
  }
}

async function handleRegister(payload: RegisterPayload) {
  const ok = await auth.register(payload)
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
          :key="route.fullPath"
          :user="auth.user.value"
          :session-expired="auth.sessionExpired.value"
          :is-submitting="auth.isSubmitting.value"
          :error-message="auth.errorMessage.value"
          :field-errors="auth.fieldErrors.value"
          @login="handleLogin"
          @register="handleRegister"
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
      <span>Game Cờ Tướng Trực Tuyến</span>
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
