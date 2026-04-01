export interface PaperPreset {
  label: string
  widthMm: number
  heightMm: number
}

export interface PhotoSizePreset {
  label: string
  widthMm: number
  heightMm: number
}

export interface SheetLayout {
  paperLabel: string
  paperWIn: number
  paperHIn: number
  slotWmm: number
  slotHmm: number
  cols: number
  rows: number
  count: number
  sheetWmm: number
  sheetHmm: number
  gapCol: number
  gapRow: number
}
