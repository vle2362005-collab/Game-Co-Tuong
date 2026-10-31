export const boardPoint = (col: number, row: number, flipped = false) => ({ x: 40 + (flipped ? 8 - col : col) * 50, y: 40 + (flipped ? 9 - row : row) * 50 })
export const displayCell = (col: number, row: number, flipped = false) => ({ col: flipped ? 8 - col : col, row: flipped ? 9 - row : row })
