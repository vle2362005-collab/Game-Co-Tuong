<script setup lang="ts">
import type { Piece, ConfirmedMove } from '../api/game'
import { boardPoint, displayCell } from './boardLayout'
const props = defineProps<{ pieces: Piece[]; flipped?: boolean; selected?: string | null; disabled?: boolean;
  destinations?: { col: number; row: number }[]; lastMove?: ConfirmedMove | null; checkedGeneralId?: string | null }>()
const emit = defineEmits<{ cell: [col: number, row: number] }>()
const cells = Array.from({ length: 90 }, (_, n) => ({ col: n % 9, row: Math.floor(n / 9) }))
function choose(col: number, row: number) { if (!props.disabled) { const p = displayCell(col, row, props.flipped); emit('cell', p.col, p.row) } }
</script>
<template>
  <svg class="board" viewBox="0 0 480 530" role="group" aria-label="Bàn cờ Tướng 9 cột 10 hàng">
    <rect width="480" height="530" rx="16" fill="#f6e9c9"/>
    <g stroke="#664d35" stroke-width="1.5">
      <path v-for="r in 10" :key="`row${r}`" :d="`M40 ${40+(r-1)*50} H440`"/>
      <path v-for="c in 9" :key="`col${c}`" :d="(c===1||c===9) ? `M${40+(c-1)*50} 40 V490` : `M${40+(c-1)*50} 40 V240 M${40+(c-1)*50} 290 V490`"/>
      <path d="M190 40L290 140M290 40L190 140M190 390L290 490M290 390L190 490"/>
    </g>
    <g fill="#806043" font-size="20" text-anchor="middle"><text x="140" y="272">SỞ HÀ</text><text x="340" y="272">HÁN GIỚI</text></g>
    <g v-if="lastMove" pointer-events="none" fill="#cb9225" opacity=".35">
      <circle data-last-from="true" :cx="boardPoint(lastMove.fromCol,lastMove.fromRow,flipped).x" :cy="boardPoint(lastMove.fromCol,lastMove.fromRow,flipped).y" r="24"/>
      <circle data-last-to="true" :cx="boardPoint(lastMove.toCol,lastMove.toRow,flipped).x" :cy="boardPoint(lastMove.toCol,lastMove.toRow,flipped).y" r="24"/>
    </g>
    <circle v-for="to in destinations || []" :key="`legal-${to.col}-${to.row}`" :data-legal="`${to.col}-${to.row}`" :cx="boardPoint(to.col,to.row,flipped).x" :cy="boardPoint(to.col,to.row,flipped).y" r="11" fill="#297349" opacity=".65" pointer-events="none"/>
    <g v-for="cell in cells" :key="`${cell.col}-${cell.row}`" :transform="`translate(${40+cell.col*50},${40+cell.row*50})`" role="button" tabindex="0"
       :aria-label="`Cột ${displayCell(cell.col,cell.row,flipped).col}, hàng ${displayCell(cell.col,cell.row,flipped).row}`" :data-pos="`${displayCell(cell.col,cell.row,flipped).col}-${displayCell(cell.col,cell.row,flipped).row}`"
       @click="choose(cell.col,cell.row)" @keydown.enter.prevent="choose(cell.col,cell.row)" @keydown.space.prevent="choose(cell.col,cell.row)">
      <circle r="24" fill="transparent"/>
    </g>
    <g v-for="piece in pieces" :key="piece.id" :transform="`translate(${boardPoint(piece.position.col,piece.position.row,flipped).x},${boardPoint(piece.position.col,piece.position.row,flipped).y})`" pointer-events="none">
      <circle v-if="selected===piece.id" :data-selected="piece.id" r="24" fill="#a2c88e" stroke="#297349" stroke-width="3"/>
      <image :href="`/pieces/${piece.side.toLowerCase()}-${piece.type.toLowerCase()}.svg`" x="-22" y="-22" width="44" height="44"/>
      <circle v-if="checkedGeneralId===piece.id" data-check="true" r="24" fill="none" stroke="#b92323" stroke-width="4"/>
    </g>
  </svg>
</template>
<style scoped>
.board{display:block;width:100%;max-width:590px;height:auto;margin:auto;touch-action:manipulation}.board g[role=button]{cursor:pointer}.board g[role=button]:focus-visible{outline:none}.board g[role=button]:focus-visible circle{stroke:#227a51;stroke-width:3}
</style>
