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
  /** 若列/行缝分别均分余量时的理论值（仅作参考） */
  gapCol: number
  gapRow: number
  /** 实际渲染：行/列共用同一缝宽，避免「横缝很宽、竖缝很细」 */
  gapMm: number
  /** 相纸四边留白（缝变均匀后多余空间均分） */
  padXMm: number
  padYMm: number
}
