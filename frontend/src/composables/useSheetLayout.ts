import type { SheetLayout } from '../types/layout'

export function computeBestLayout(
  paperWidthMm: number,
  paperHeightMm: number,
  photoWidthMm: number,
  photoHeightMm: number,
): SheetLayout {
  const variants = [
    {
      paperLabel: `${paperWidthMm}×${paperHeightMm} mm`,
      paperWIn: paperWidthMm / 25.4,
      paperHIn: paperHeightMm / 25.4,
      slotWmm: photoWidthMm,
      slotHmm: photoHeightMm,
      cols: Math.floor(paperWidthMm / photoWidthMm),
      rows: Math.floor(paperHeightMm / photoHeightMm),
    },
    {
      paperLabel: `${paperWidthMm}×${paperHeightMm} mm`,
      paperWIn: paperWidthMm / 25.4,
      paperHIn: paperHeightMm / 25.4,
      slotWmm: photoHeightMm,
      slotHmm: photoWidthMm,
      cols: Math.floor(paperWidthMm / photoHeightMm),
      rows: Math.floor(paperHeightMm / photoWidthMm),
    },
    {
      paperLabel: `${paperHeightMm}×${paperWidthMm} mm`,
      paperWIn: paperHeightMm / 25.4,
      paperHIn: paperWidthMm / 25.4,
      slotWmm: photoWidthMm,
      slotHmm: photoHeightMm,
      cols: Math.floor(paperHeightMm / photoWidthMm),
      rows: Math.floor(paperWidthMm / photoHeightMm),
    },
    {
      paperLabel: `${paperHeightMm}×${paperWidthMm} mm`,
      paperWIn: paperHeightMm / 25.4,
      paperHIn: paperWidthMm / 25.4,
      slotWmm: photoHeightMm,
      slotHmm: photoWidthMm,
      cols: Math.floor(paperHeightMm / photoHeightMm),
      rows: Math.floor(paperWidthMm / photoWidthMm),
    },
  ]

  let best = variants[0]
  let bestCount = best.cols * best.rows
  for (const v of variants) {
    const count = v.cols * v.rows
    if (count > bestCount) {
      best = v
      bestCount = count
    }
  }

  const sheetWmm = best.paperWIn * 25.4
  const sheetHmm = best.paperHIn * 25.4
  const gapCol = best.cols > 1 ? (sheetWmm - best.cols * best.slotWmm) / (best.cols - 1) : 0
  const gapRow = best.rows > 1 ? (sheetHmm - best.rows * best.slotHmm) / (best.rows - 1) : 0

  return {
    ...best,
    count: bestCount,
    sheetWmm,
    sheetHmm,
    gapCol,
    gapRow,
  }
}
