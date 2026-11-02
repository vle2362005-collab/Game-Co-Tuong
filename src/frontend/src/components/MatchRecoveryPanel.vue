<script setup lang="ts">
import { computed, toRef } from 'vue'
import type { Match } from '../api/game'
import { useSnapshotClock } from '../composables/useSnapshotClock'
const props = defineProps<{ match: Match; connected: boolean; busy: boolean }>()
defineEmits<{ retry: [] }>()
const remaining = useSnapshotClock(toRef(props, 'match'))
const players = computed(() => props.match.players.map(player => ({
  ...player,
  deadline: player.side === 'red' ? props.match.redDeadlineAt : props.match.blackDeadlineAt
})))
</script>
<template>
  <section v-if="match.status==='paused' || !connected" class="recovery" role="status" aria-live="polite">
    <strong>{{ match.status==='paused' ? 'Trận đang tạm dừng' : 'Đang nối lại máy chủ' }}</strong>
    <p v-if="match.recoveryReason==='serverRestart'">Máy chủ vừa khởi động lại. Các người chơi cần xác thực lại để tiếp tục ván cũ.</p>
    <p v-else>Chờ khôi phục kết nối. Bàn cờ được khóa tới khi máy chủ cho phép tiếp tục.</p>
    <ul v-if="match.status==='paused'"><li v-for="player in players" :key="player.userId">
      {{ player.side==='red' ? 'Đỏ' : 'Đen' }}:
      <template v-if="player.deadline">{{ remaining(player.deadline) === null ? 'đang lấy thời hạn từ máy chủ' : `${remaining(player.deadline)} giây để nối lại` }}</template>
      <template v-else>đã kết nối</template>
    </li></ul>
    <p>Hết thời gian hiển thị vẫn chờ kết quả từ máy chủ.</p>
    <button :disabled="busy" @click="$emit('retry')">Kết nối lại và đồng bộ</button>
  </section>
</template>
<style scoped>
.recovery{padding:14px;border:1px solid #cb9225;border-radius:10px;background:#fff7df;margin:12px 0}.recovery p{margin:8px 0}.recovery button{padding:9px 14px;cursor:pointer}
</style>
