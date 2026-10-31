<script setup lang="ts">
import type { MatchInfo } from '../types/auth'

defineProps<{
  match: MatchInfo
}>()

const emit = defineEmits<{
  (e: 'surrender'): void
  (e: 'testExpire'): void
}>()
</script>

<template>
  <div class="game-board-container">
    <div class="match-header">
      <div class="player-badge">
        <span class="side-indicator" :class="match.side"></span>
        <div>
          <span class="role">Bạn ({{ match.side === 'red' ? 'Đỏ' : 'Đen' }})</span>
          <strong>Đang thi đấu</strong>
        </div>
      </div>

      <div class="versus">VS</div>

      <div class="player-badge right">
        <div>
          <span class="role">Đối thủ ({{ match.side === 'red' ? 'Đen' : 'Đỏ' }})</span>
          <strong>{{ match.opponentName }}</strong>
        </div>
        <span class="side-indicator" :class="match.side === 'red' ? 'black' : 'red'"></span>
      </div>
    </div>

    <!-- Xiangqi Mini Board Graphic -->
    <div class="board-frame" aria-label="Bàn cờ Tướng">
      <div class="board-grid">
        <div class="territory top">
          <span class="piece black" style="top: 8%; left: 47%">將</span>
          <span class="piece black" style="top: 8%; left: 10%">車</span>
          <span class="piece black" style="top: 8%; left: 84%">車</span>
          <span class="piece black" style="top: 28%; left: 22%">包</span>
          <span class="piece black" style="top: 28%; left: 72%">包</span>
        </div>

        <div class="river">
          <span>楚 河</span>
          <span>漢 界</span>
        </div>

        <div class="territory bottom">
          <span class="piece red" style="bottom: 28%; left: 22%">炮</span>
          <span class="piece red" style="bottom: 28%; left: 72%">炮</span>
          <span class="piece red" style="bottom: 8%; left: 10%">俥</span>
          <span class="piece red" style="bottom: 8%; left: 84%">俥</span>
          <span class="piece red" style="bottom: 8%; left: 47%">帥</span>
        </div>
      </div>
    </div>

    <div class="match-actions">
      <button
        type="button"
        class="btn-resign"
        id="game-resign-btn"
        @click="emit('surrender')"
      >
        🏳️ Đầu hàng ván cờ
      </button>

      <button
        type="button"
        class="btn-test-expire"
        id="game-expire-btn"
        @click="emit('testExpire')"
      >
        ⏱️ Thử hết hạn phiên trong trận
      </button>
    </div>

    <p class="in-game-notice">
      ℹ️ Chú ý: Nếu bạn chọn <strong>Đăng xuất</strong> trên thanh điều hướng, hệ thống sẽ yêu cầu xác nhận đầu hàng trước khi thoát phiên.
    </p>
  </div>
</template>

<style scoped>
.game-board-container {
  background: #fffef9;
  border: 1px solid #d6dcd0;
  border-radius: 16px;
  padding: 24px;
  max-width: 580px;
  margin: 24px auto;
  box-shadow: 0 4px 20px rgba(23, 43, 38, 0.08);
  text-align: center;
}

.match-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
  background: #f8f6f0;
  border: 1px solid #e5ebe0;
  border-radius: 12px;
  padding: 12px 18px;
}

.player-badge {
  display: flex;
  align-items: center;
  gap: 10px;
  text-align: left;
}

.player-badge.right {
  text-align: right;
}

.side-indicator {
  width: 14px;
  height: 14px;
  border-radius: 50%;
  display: inline-block;
}

.side-indicator.red {
  background: #9b4036;
  box-shadow: 0 0 8px rgba(155, 64, 54, 0.5);
}

.side-indicator.black {
  background: #172b26;
  box-shadow: 0 0 8px rgba(23, 43, 38, 0.5);
}

.role {
  display: block;
  font-size: 0.75rem;
  color: #65746d;
}

.versus {
  font-family: Georgia, serif;
  font-weight: bold;
  color: #b0baa8;
  font-size: 1.1rem;
}

.board-frame {
  background: #dfc8a2;
  border: 4px solid #784c25;
  border-radius: 12px;
  padding: 14px;
  margin: 0 auto 20px;
  max-width: 380px;
  box-shadow: inset 0 0 10px rgba(0, 0, 0, 0.2);
}

.board-grid {
  border: 1px solid #784c25;
  height: 380px;
  position: relative;
  background-image: 
    linear-gradient(to right, #784c25 1px, transparent 1px),
    linear-gradient(to bottom, #784c25 1px, transparent 1px);
  background-size: 44px 44px;
}

.river {
  position: absolute;
  top: 45%;
  left: 0;
  right: 0;
  height: 10%;
  background: #ebd8b7;
  display: flex;
  justify-content: space-around;
  align-items: center;
  font-family: "Noto Serif", Georgia, serif;
  font-size: 0.95rem;
  letter-spacing: 0.25em;
  color: #784c25;
  font-weight: 700;
  border-top: 1px solid #784c25;
  border-bottom: 1px solid #784c25;
}

.piece {
  position: absolute;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  display: grid;
  place-items: center;
  font-weight: bold;
  font-size: 1.1rem;
  cursor: pointer;
  box-shadow: 0 2px 5px rgba(0,0,0,0.3);
  user-select: none;
}

.piece.red {
  background: #fbf8f0;
  color: #b82c21;
  border: 2px solid #b82c21;
}

.piece.black {
  background: #fbf8f0;
  color: #1a1a1a;
  border: 2px solid #1a1a1a;
}

.match-actions {
  display: flex;
  gap: 12px;
  justify-content: center;
  margin-bottom: 14px;
}

.btn-resign {
  background: #9b4036;
  color: #ffffff;
  border: none;
  border-radius: 8px;
  padding: 10px 18px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-resign:hover {
  background: #b84f43;
}

.btn-test-expire {
  background: #f1ede3;
  color: #53625b;
  border: 1px solid #d6dcd0;
  border-radius: 8px;
  padding: 10px 16px;
  font-weight: 600;
  cursor: pointer;
}

.btn-test-expire:hover {
  background: #e2dbce;
  color: #172b26;
}

.in-game-notice {
  font-size: 0.8rem;
  color: #65746d;
  margin: 0;
  line-height: 1.4;
}
</style>
