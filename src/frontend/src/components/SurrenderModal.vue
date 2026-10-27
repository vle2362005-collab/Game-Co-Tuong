<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'

const props = defineProps<{
  show: boolean
}>()

const emit = defineEmits<{
  (e: 'cancel'): void
  (e: 'confirm'): void
}>()

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && props.show) {
    emit('cancel')
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleKeydown)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <Teleport to="body">
    <Transition name="modal-fade">
      <div
        v-if="show"
        class="modal-backdrop"
        role="presentation"
        @click.self="emit('cancel')"
      >
        <div
          class="modal-card"
          role="dialog"
          aria-modal="true"
          aria-labelledby="surrender-title"
          aria-describedby="surrender-desc"
        >
          <div class="modal-badge" aria-hidden="true">⚠️</div>
          <h2 id="surrender-title">Xác nhận đầu hàng & đăng xuất</h2>
          <p id="surrender-desc" class="modal-desc">
            Bạn đang trong ván cờ đang diễn ra. Nếu đăng xuất lúc này, bạn sẽ bị tính là
            <strong>đầu hàng</strong> và xử thua trận đấu.
          </p>
          <p class="modal-subtext">
            Bạn có chắc chắn muốn đầu hàng để kết thúc phiên và đăng xuất không?
          </p>

          <div class="policy-note">
            <span class="policy-icon" aria-hidden="true">🔒</span>
            <span>Chính sách phiên: Token JWT của bạn sẽ được thu hồi trên máy chủ. Trường hợp không có bảng thu hồi (stateless JWT), JWT cũ vẫn có hiệu lực cho đến thời điểm hết hạn (exp).</span>
          </div>

          <div class="modal-actions">
            <button
              type="button"
              class="btn-secondary"
              id="cancel-surrender-btn"
              @click="emit('cancel')"
            >
              Hủy, tiếp tục ván cờ
            </button>
            <button
              type="button"
              class="btn-danger"
              id="confirm-surrender-btn"
              @click="emit('confirm')"
            >
              Xác nhận đầu hàng & Đăng xuất
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(23, 43, 38, 0.65);
  backdrop-filter: blur(4px);
  display: grid;
  place-items: center;
  z-index: 1000;
  padding: 16px;
}

.modal-card {
  background: #fffef9;
  border: 1px solid #d6dcd0;
  border-radius: 16px;
  max-width: 480px;
  width: 100%;
  padding: 32px 28px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.22);
  text-align: center;
}

.modal-badge {
  font-size: 2.2rem;
  margin-bottom: 12px;
}

h2 {
  font-family: Georgia, "Times New Roman", serif;
  color: #9b4036;
  font-size: 1.45rem;
  margin: 0 0 14px;
}

.modal-desc {
  font-size: 0.98rem;
  color: #172b26;
  line-height: 1.5;
  margin-bottom: 8px;
}

.modal-subtext {
  font-size: 0.88rem;
  color: #53625b;
  margin-bottom: 16px;
}

.policy-note {
  background: #fbfaf6;
  border: 1px dashed #d6dcd0;
  border-radius: 8px;
  padding: 10px 14px;
  font-size: 0.78rem;
  color: #65746d;
  line-height: 1.45;
  margin-bottom: 22px;
  display: flex;
  align-items: flex-start;
  gap: 8px;
  text-align: left;
}

.policy-icon {
  font-size: 0.95rem;
  flex-shrink: 0;
}

.modal-actions {
  display: flex;
  gap: 12px;
  justify-content: center;
  flex-wrap: wrap;
}

.btn-secondary {
  background: #eaf0e7;
  color: #172b26;
  border: 1px solid #d6dcd0;
  padding: 11px 18px;
  font-weight: 600;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-secondary:hover {
  background: #d8e4d4;
}

.btn-danger {
  background: #9b4036;
  color: #ffffff;
  border: none;
  padding: 11px 18px;
  font-weight: 600;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-danger:hover {
  background: #b84f43;
}

.modal-fade-enter-active,
.modal-fade-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.modal-fade-enter-from,
.modal-fade-leave-to {
  opacity: 0;
  transform: scale(0.96);
}
</style>
