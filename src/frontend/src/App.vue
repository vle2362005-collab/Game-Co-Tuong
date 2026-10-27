<script setup lang="ts">
import { createAuthStore } from './composables/useAuth'
import AppHeader from './components/AppHeader.vue'
import LoginView from './components/LoginView.vue'
import LobbyView from './components/LobbyView.vue'
import GameView from './components/GameView.vue'
import SurrenderModal from './components/SurrenderModal.vue'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'
const auth = createAuthStore(apiBaseUrl)

async function handleLogin(payload: { username: string; password: string }) {
  await auth.login(payload.username, payload.password)
}
</script>

<template>
  <main class="shell">
    <AppHeader
      :is-authenticated="auth.isAuthenticated.value"
      :is-playing="auth.isPlaying.value"
      :user="auth.user.value"
      @logout="auth.requestLogout"
    />

    <!-- Main Content by View -->
    <section class="main-content">
      <LoginView
        v-if="auth.currentView.value === 'login'"
        :session-expired="auth.sessionExpired.value"
        :is-submitting="auth.isSubmitting.value"
        :error-message="auth.errorMessage.value"
        @login="handleLogin"
      />

      <LobbyView
        v-else-if="auth.currentView.value === 'lobby' && auth.user.value"
        :user="auth.user.value"
        @enter-match="auth.enterMatch"
        @test-expire="auth.handleSessionExpired"
      />

      <GameView
        v-else-if="auth.currentView.value === 'game' && auth.activeMatch.value"
        :match="auth.activeMatch.value"
        @surrender="auth.requestLogout"
        @test-expire="auth.handleSessionExpired"
      />
    </section>

    <!-- Surrender Confirmation Modal (active during gameplay logout attempt) -->
    <SurrenderModal
      :show="auth.showSurrenderModal.value"
      @cancel="auth.cancelLogout"
      @confirm="auth.confirmSurrenderAndLogout"
    />

    <footer>
      <span>Vue 3 · TypeScript · ASP.NET Core</span>
      <span>Dự án Game Cờ Tướng · Nhánh GCT-7-logout</span>
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
