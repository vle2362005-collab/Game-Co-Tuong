<script setup lang="ts">
import { ref } from 'vue'
import { checkHealth } from './api/health'

const state = ref<'idle' | 'loading' | 'healthy' | 'error'>('idle')
const message = ref('Chưa kiểm tra kết nối máy chủ.')

async function connect() {
  state.value = 'loading'
  message.value = 'Đang kết nối…'
  try {
    await checkHealth(import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000', AbortSignal.timeout(5000))
    state.value = 'healthy'
    message.value = 'Kết nối thành công. Máy chủ đang hoạt động.'
  } catch {
    state.value = 'error'
    message.value = 'Chưa kết nối được. Kiểm tra backend, địa chỉ API và cấu hình CORS.'
  }
}
</script>

<template>
  <main class="shell">
    <header><span class="mark" aria-hidden="true">帥</span><span>GAME CỜ TƯỚNG</span></header>
    <section class="intro" aria-labelledby="page-title">
      <p class="eyebrow">KHỞI ĐẦU MỘT VÁN CỜ</p>
      <h1 id="page-title">Mỗi nước đi,<br />một chiến lược.</h1>
      <p class="description">Bộ khung dự án đã sẵn sàng để nhóm phát triển. Tài khoản, phòng chơi và luật cờ sẽ được bổ sung ở các phần tiếp theo.</p>
    </section>
    <section class="connection" aria-labelledby="connection-title">
      <div><span class="tag">GCT-21</span><h2 id="connection-title">Kiểm tra môi trường</h2></div>
      <p class="status" :data-state="state" role="status" aria-live="polite">{{ message }}</p>
      <button type="button" :disabled="state === 'loading'" @click="connect">
        {{ state === 'loading' ? 'Đang kiểm tra…' : 'Kiểm tra máy chủ' }} <span aria-hidden="true">↗</span>
      </button>
      <p class="hint">Kết quả này xác nhận kết nối API; chưa xác nhận cơ sở dữ liệu hoặc chức năng chơi cờ.</p>
    </section>
    <footer>Vue · TypeScript · ASP.NET Core <span>Dự án Game Cờ Tướng</span></footer>
  </main>
</template>
