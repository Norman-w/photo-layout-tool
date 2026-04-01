import type { SheetLayout } from '../types/layout'

/**
 * 在**用户指定的相纸宽高**上排**固定竖向单张**（宽×高 = 证件照短边×长边），
 * 不再自动把相纸或单张横过来换张数，避免二寸等出现「头横过来」。
 */
export function computeBestLayout(
  paperWidthMm: number,
  paperHeightMm: number,
  photoWidthMm: number,
  photoHeightMm: number,
): SheetLayout {
  const paperWIn = paperWidthMm / 25.4
  const paperHIn = paperHeightMm / 25.4
  const slotWmm = photoWidthMm
  const slotHmm = photoHeightMm
  const cols = Math.floor(paperWidthMm / photoWidthMm)
  const rows = Math.floor(paperHeightMm / photoHeightMm)

  if (cols < 1 || rows < 1) {
    return {
      paperLabel: `${paperWidthMm}×${paperHeightMm} mm`,
      paperWIn,
      paperHIn,
      slotWmm,
      slotHmm,
      cols: 0,
      rows: 0,
      count: 0,
      sheetWmm: paperWidthMm,
      sheetHmm: paperHeightMm,
      gapCol: 0,
      gapRow: 0,
      gapMm: 0,
      padXMm: 0,
      padYMm: 0,
    }
  }

  const count = cols * rows
  const sheetWmm = paperWidthMm
  const sheetHmm = paperHeightMm
  const gapCol = cols > 1 ? (sheetWmm - cols * slotWmm) / (cols - 1) : 0
  const gapRow = rows > 1 ? (sheetHmm - rows * slotHmm) / (rows - 1) : 0

  let gapMm: number
  if (cols > 1 && rows > 1) gapMm = Math.min(gapCol, gapRow)
  else if (cols > 1) gapMm = gapCol
  else gapMm = gapRow

  const gridWmm = cols * slotWmm + (cols > 1 ? (cols - 1) * gapMm : 0)
  const gridHmm = rows * slotHmm + (rows > 1 ? (rows - 1) * gapMm : 0)
  const padXMm = Math.max(0, (sheetWmm - gridWmm) / 2)
  const padYMm = Math.max(0, (sheetHmm - gridHmm) / 2)

  return {
    paperLabel: `${paperWidthMm}×${paperHeightMm} mm`,
    paperWIn,
    paperHIn,
    slotWmm,
    slotHmm,
    cols,
    rows,
    count,
    sheetWmm,
    sheetHmm,
    gapCol,
    gapRow,
    gapMm,
    padXMm,
    padYMm,
  }
}
