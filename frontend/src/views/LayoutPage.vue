<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import Cropper from 'cropperjs'
import 'cropperjs/dist/cropper.css'
import { NButton, NCard, NInputNumber, NModal, NSelect, NSlider, NSpace, NTag, useMessage } from 'naive-ui'
import { matteImage } from '../api/images'
import { computeBestLayout } from '../composables/useSheetLayout'
import type { PaperPreset, PhotoSizePreset } from '../types/layout'

type SourceImage = {
  name: string
  src: string
  croppedSrc?: string
  processedSrc?: string
}

const message = useMessage()

const paperPresets: PaperPreset[] = [
  { label: '4×6 寸（102×152mm）', widthMm: 102, heightMm: 152 },
  { label: '5×7 寸（127×178mm）', widthMm: 127, heightMm: 178 },
  { label: '6×8 寸（152×203mm）', widthMm: 152, heightMm: 203 },
  { label: 'A4（210×297mm）', widthMm: 210, heightMm: 297 },
]

const photoPresets: PhotoSizePreset[] = [
  { label: '一寸（25×35mm）', widthMm: 25, heightMm: 35 },
  { label: '小二寸（35×45mm）', widthMm: 35, heightMm: 45 },
  { label: '二寸（35×49mm）', widthMm: 35, heightMm: 49 },
  { label: '护照（33×48mm）', widthMm: 33, heightMm: 48 },
]

const bgColorOptions = [
  { label: '白底', value: '#ffffff' },
  { label: '浅灰底', value: '#f2f2f2' },
  { label: '蓝底', value: '#2b7bde' },
  { label: '红底', value: '#d73a30' },
]

const fileInputRef = ref<HTMLInputElement | null>(null)
const cropImageRef = ref<HTMLImageElement | null>(null)

const selectedPaper = ref(paperPresets[0])
const selectedPhoto = ref(photoPresets[0])
const customPaperWmm = ref(selectedPaper.value.widthMm)
const customPaperHmm = ref(selectedPaper.value.heightMm)
const customPhotoWmm = ref(selectedPhoto.value.widthMm)
const customPhotoHmm = ref(selectedPhoto.value.heightMm)
const backgroundColor = ref('#ffffff')
/** 与 appsettings Matting 默认一致；调高可减少背景残留 */
const mattingForegroundThreshold = ref(0.58)
const mattingEdgeSoftness = ref(0.12)
/** 减轻头发里原背景色边；与「抠图阈值」不同，专治色边 */
const mattingEdgeColorPull = ref(0.42)

const source = ref<SourceImage | null>(null)
const cropModalVisible = ref(false)
const cropper = ref<Cropper | null>(null)
const processingColor = ref(false)

const PREVIEW_SCALE = 0.62

const paperOptions = paperPresets.map((x) => ({ label: x.label, value: x.label }))
const photoOptions = photoPresets.map((x) => ({ label: x.label, value: x.label }))
const bgOptions = bgColorOptions.map((x) => ({ label: x.label, value: x.value }))

const layout = computed(() =>
  computeBestLayout(
    customPaperWmm.value,
    customPaperHmm.value,
    customPhotoWmm.value,
    customPhotoHmm.value,
  ),
)

const effectivePhotoRatio = computed(() => layout.value.slotWmm / layout.value.slotHmm)
const cropPreviewStyle = computed(() => ({ aspectRatio: `${layout.value.slotWmm} / ${layout.value.slotHmm}` }))

const cropSourcePreview = computed(() => source.value?.croppedSrc ?? source.value?.src ?? '')
const finalSourcePreview = computed(() => source.value?.processedSrc ?? cropSourcePreview.value)

const cells = computed(() =>
  Array.from({ length: layout.value.count }, () => (source.value ? finalSourcePreview.value : null)),
)

const statsText = computed(() => {
  const L = layout.value
  return `排版 ${L.cols}×${L.rows} = ${L.count} 张；缝宽 横向 ${L.gapCol.toFixed(2)}mm / 纵向 ${L.gapRow.toFixed(2)}mm`
})

function onPaperPresetChange(label: string) {
  const picked = paperPresets.find((x) => x.label === label)
  if (!picked) return
  selectedPaper.value = picked
  customPaperWmm.value = picked.widthMm
  customPaperHmm.value = picked.heightMm
}

function onPhotoPresetChange(label: string) {
  const picked = photoPresets.find((x) => x.label === label)
  if (!picked) return
  selectedPhoto.value = picked
  customPhotoWmm.value = picked.widthMm
  customPhotoHmm.value = picked.heightMm
  if (cropper.value) cropper.value.setAspectRatio(effectivePhotoRatio.value)
}

function pickOriginal() {
  fileInputRef.value?.click()
}

function loadSingle(files: FileList | null) {
  if (!files?.length) return
  const file = [...files].find((f) => f.type.startsWith('image/'))
  if (!file) return
  const reader = new FileReader()
  reader.onload = () => {
    source.value = { name: file.name, src: reader.result as string }
  }
  reader.onerror = () => message.error('原图读取失败')
  reader.readAsDataURL(file)
}

function onSelectOriginal(e: Event) {
  const input = e.target as HTMLInputElement
  loadSingle(input.files)
  input.value = ''
}

function openCropModal() {
  if (!source.value) {
    message.warning('请先选择原图')
    return
  }
  cropModalVisible.value = true
}

function closeCropModal() {
  cropModalVisible.value = false
}

async function initCropper() {
  await nextTick()
  const img = cropImageRef.value
  if (!img) return

  cropper.value?.destroy()
  cropper.value = new Cropper(img, {
    viewMode: 1,
    dragMode: 'move',
    autoCropArea: 0.9,
    responsive: true,
    background: false,
    aspectRatio: effectivePhotoRatio.value,
  })
}

function applyCrop() {
  if (!cropper.value || !source.value) return

  const cropWidth = 960
  const cropHeight = Math.round(cropWidth / effectivePhotoRatio.value)
  const canvas = cropper.value.getCroppedCanvas({
    width: cropWidth,
    height: cropHeight,
    imageSmoothingEnabled: true,
    imageSmoothingQuality: 'high',
  })

  source.value = {
    ...source.value,
    croppedSrc: canvas.toDataURL('image/jpeg', 0.95),
    processedSrc: undefined,
  }

  cropModalVisible.value = false
  message.success('裁剪已应用')
}

function dataUrlToFile(dataUrl: string, filename: string) {
  const [meta, payload] = dataUrl.split(',')
  const mime = /data:(.*?);base64/.exec(meta)?.[1] || 'image/jpeg'
  const bin = atob(payload)
  const len = bin.length
  const bytes = new Uint8Array(len)
  for (let i = 0; i < len; i++) bytes[i] = bin.charCodeAt(i)
  return new File([bytes], filename, { type: mime })
}

async function applyBackgroundColor() {
  if (!source.value) {
    message.warning('请先选择原图')
    return
  }
  processingColor.value = true
  try {
    const file = dataUrlToFile(cropSourcePreview.value, `matte-${Date.now()}.jpg`)
    const res = await matteImage(file, backgroundColor.value, {
      foregroundThreshold: mattingForegroundThreshold.value,
      edgeSoftness: mattingEdgeSoftness.value,
      edgeColorPull: mattingEdgeColorPull.value,
    })
    source.value = {
      ...source.value,
      processedSrc: `${res.url}?t=${Date.now()}`,
    }
    message.success('换色完成，右侧和整版已更新')
  } catch (e) {
    message.error(e instanceof Error ? e.message : '换色失败')
  } finally {
    processingColor.value = false
  }
}

function printSheet() {
  window.print()
}

function resetAll() {
  source.value = null
  selectedPaper.value = paperPresets[0]
  selectedPhoto.value = photoPresets[0]
  customPaperWmm.value = selectedPaper.value.widthMm
  customPaperHmm.value = selectedPaper.value.heightMm
  customPhotoWmm.value = selectedPhoto.value.widthMm
  customPhotoHmm.value = selectedPhoto.value.heightMm
  backgroundColor.value = '#ffffff'
  mattingForegroundThreshold.value = 0.58
  mattingEdgeSoftness.value = 0.12
  mattingEdgeColorPull.value = 0.42
}

watch(cropModalVisible, (open) => {
  if (open) {
    initCropper()
    return
  }
  cropper.value?.destroy()
  cropper.value = null
})

watch(
  [() => layout.value.paperWIn, () => layout.value.paperHIn],
  ([w, h]) => {
    const el = document.getElementById('print-page-rule')
    if (el) el.textContent = `@page { size: ${w}in ${h}in; margin: 0; }`
  },
  { immediate: true },
)

onBeforeUnmount(() => {
  cropper.value?.destroy()
})
</script>

<template>
  <div class="layout-app">
    <header class="no-print layout-app__header">
      <h1 class="layout-app__title">照片拼版工具</h1>
      <p class="layout-app__sub">单原图：左完整原图 / 中裁剪预览 / 右换色结果</p>
    </header>

    <n-card class="no-print panel">
      <n-space vertical size="medium">
        <n-space align="center" wrap>
          <n-select
            style="width: 220px"
            :options="paperOptions"
            :value="selectedPaper.label"
            @update:value="onPaperPresetChange"
          />
          <n-input-number v-model:value="customPaperWmm" :min="30" :max="500" placeholder="相纸宽 mm" />
          <n-input-number v-model:value="customPaperHmm" :min="30" :max="500" placeholder="相纸高 mm" />
        </n-space>

        <n-space align="center" wrap>
          <n-select
            style="width: 220px"
            :options="photoOptions"
            :value="selectedPhoto.label"
            @update:value="onPhotoPresetChange"
          />
          <n-input-number v-model:value="customPhotoWmm" :min="10" :max="120" placeholder="单张宽 mm" />
          <n-input-number v-model:value="customPhotoHmm" :min="10" :max="120" placeholder="单张高 mm" />
        </n-space>

        <n-space align="center" wrap>
          <n-tag type="warning">底色（人像周围）</n-tag>
          <n-select
            style="width: 150px"
            :options="bgOptions"
            :value="backgroundColor"
            @update:value="(v) => (backgroundColor = v)"
          />
          <n-button :loading="processingColor" :disabled="!source || processingColor" type="primary" @click="applyBackgroundColor">
            换色
          </n-button>
        </n-space>

        <n-space vertical size="small" class="matting-sliders">
          <div class="matting-sliders__row">
            <span class="matting-sliders__label">抠图阈值 {{ mattingForegroundThreshold.toFixed(2) }}</span>
            <n-slider
              v-model:value="mattingForegroundThreshold"
              :min="0.2"
              :max="0.9"
              :step="0.01"
              :disabled="!source"
              style="max-width: 320px"
            />
            <span class="matting-sliders__hint">调高更吃背景；与「边缘过渡」配合看发际线/衣领变化最明显</span>
          </div>
          <div class="matting-sliders__row">
            <span class="matting-sliders__label">边缘过渡 {{ mattingEdgeSoftness.toFixed(2) }}</span>
            <n-slider
              v-model:value="mattingEdgeSoftness"
              :min="0.05"
              :max="0.55"
              :step="0.01"
              :disabled="!source"
              style="max-width: 320px"
            />
            <span class="matting-sliders__hint">略大更柔和，过小易出现硬边</span>
          </div>
          <div class="matting-sliders__row">
            <span class="matting-sliders__label">边缘去色 {{ mattingEdgeColorPull.toFixed(2) }}</span>
            <n-slider
              v-model:value="mattingEdgeColorPull"
              :min="0"
              :max="1"
              :step="0.02"
              :disabled="!source"
              style="max-width: 320px"
            />
            <span class="matting-sliders__hint">专治头发里夹带原背景色；阈值主要改轮廓透明，对这种「色边」请调这项</span>
          </div>
        </n-space>

        <p class="stats">{{ statsText }}</p>
      </n-space>
    </n-card>

    <div class="no-print source-row">
      <n-card class="source-box" size="small" title="原图展示（完整，不裁剪显示）">
        <n-space size="small" style="margin-bottom: 8px">
          <n-button size="small" type="primary" @click="pickOriginal">选择或更换原图</n-button>
          <n-button size="small" :disabled="!source" @click="openCropModal">裁剪</n-button>
        </n-space>
        <input ref="fileInputRef" class="sr-only" type="file" accept="image/*" @change="onSelectOriginal" />
        <div class="box-fixed square">
          <img v-if="source" :src="source.src" :alt="source.name" />
          <span v-else class="placeholder">未选择原图</span>
        </div>
      </n-card>

      <n-card class="source-box" size="small" :title="`实际裁剪尺寸（单张）${layout.slotWmm.toFixed(0)}×${layout.slotHmm.toFixed(0)}mm`">
        <div class="box-fixed ratio" :style="cropPreviewStyle">
          <img v-if="source" :src="cropSourcePreview" :alt="source.name" />
          <span v-else class="placeholder">裁剪后会显示在这里</span>
        </div>
      </n-card>

      <n-card class="source-box" size="small" title="换色后预览（最终效果）">
        <div class="box-fixed ratio" :style="cropPreviewStyle">
          <img v-if="source" :src="finalSourcePreview" :alt="source.name" />
          <span v-else class="placeholder">点击“换色”后显示最终效果</span>
        </div>
      </n-card>
    </div>

    <div class="preview-wrap">
      <div
        class="sheet-preview-outer"
        :style="{
          '--paper-w': layout.sheetWmm + 'mm',
          '--paper-h': layout.sheetHmm + 'mm',
          '--preview-scale': PREVIEW_SCALE,
        }"
      >
        <div class="sheet-preview-inner">
          <div
            class="sheet"
            :style="{
              width: layout.sheetWmm + 'mm',
              height: layout.sheetHmm + 'mm',
              gridTemplateColumns: `repeat(${layout.cols}, ${layout.slotWmm}mm)`,
              gridTemplateRows: `repeat(${layout.rows}, ${layout.slotHmm}mm)`,
              columnGap: layout.gapCol > 0 ? layout.gapCol + 'mm' : '0',
              rowGap: layout.gapRow > 0 ? layout.gapRow + 'mm' : '0',
              background: '#ffffff',
            }"
          >
            <div
              v-for="(src, i) in cells"
              :key="i"
              class="cell"
              :class="{ empty: !src }"
              :style="{ background: backgroundColor }"
            >
              <img v-if="src" :src="src" alt="" />
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="no-print bottom-actions">
      <n-space justify="end">
        <n-button :disabled="!source" @click="printSheet">打印</n-button>
        <n-button tertiary @click="resetAll">重置</n-button>
      </n-space>
    </div>

    <n-modal v-model:show="cropModalVisible" preset="card" class="crop-modal" title="拖拽裁剪区域">
      <p class="hint">
        裁剪框比例跟随当前排版方向（{{ layout.slotWmm.toFixed(0) }}×{{ layout.slotHmm.toFixed(0) }}）。
      </p>
      <div class="crop-wrap">
        <img v-if="source" ref="cropImageRef" class="crop-image" :src="source.src" :alt="source.name" />
      </div>
      <template #footer>
        <n-space justify="end">
          <n-button @click="closeCropModal">取消</n-button>
          <n-button type="primary" @click="applyCrop">应用裁剪</n-button>
        </n-space>
      </template>
    </n-modal>
  </div>
</template>

<style lang="scss">
.layout-app {
  max-width: 1220px;
  margin: 0 auto;
  padding: 1.1rem;
  background: #1a1d23;
  min-height: 100vh;
  color: #e8eaed;

  &__title {
    margin: 0 0 0.2rem;
  }

  &__sub {
    color: #9aa0a6;
    margin: 0 0 0.7rem;
  }
}

.panel {
  background: #252930 !important;
  margin-bottom: 0.8rem;
}

.stats {
  margin: 0;
  color: #9aa0a6;
}

.matting-sliders {
  &__row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.5rem 1rem;
  }

  &__label {
    min-width: 10rem;
    color: #c4c7cc;
    font-size: 0.875rem;
  }

  &__hint {
    flex: 1 1 100%;
    margin: 0;
    font-size: 0.78rem;
    color: #7d8288;
  }
}

.source-row {
  display: flex;
  gap: 0.65rem;
  overflow-x: auto;
  margin-bottom: 0.8rem;
}

.source-box {
  background: #252930 !important;
  min-width: 320px;
  flex: 1 1 0;
}

.box-fixed {
  width: 100%;
  border-radius: 8px;
  overflow: hidden;
  background: #111319;
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 180px;
  max-height: 210px;

  &.square {
    aspect-ratio: 1 / 1;
  }

  &.ratio {
    min-height: 180px;
  }

  img {
    width: 100%;
    height: 100%;
    object-fit: contain;
  }
}

.placeholder {
  color: #8891a3;
  font-size: 13px;
}

.hint {
  margin: 0 0 0.5rem;
  font-size: 12px;
  color: #9aa0a6;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  border: 0;
}

.preview-wrap {
  background: #0d0f12;
  border-radius: 10px;
  padding: 0.8rem;
  overflow: auto;
  display: flex;
  justify-content: center;
}

.bottom-actions {
  margin-top: 0.8rem;
}

.sheet-preview-outer {
  width: calc(var(--paper-w) * var(--preview-scale));
  height: calc(var(--paper-h) * var(--preview-scale));
  flex-shrink: 0;
}

.sheet-preview-inner {
  width: var(--paper-w);
  height: var(--paper-h);
  transform: scale(var(--preview-scale));
  transform-origin: top left;
}

.sheet {
  display: grid;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.4);
}

.cell {
  overflow: hidden;
  outline: 1px solid rgba(0, 0, 0, 0.08);

  img {
    width: 100%;
    height: 100%;
    object-fit: cover;
    object-position: center top;
  }

  &.empty {
    background-image: repeating-linear-gradient(
      -45deg,
      rgba(0, 0, 0, 0.03),
      rgba(0, 0, 0, 0.03) 6px,
      rgba(0, 0, 0, 0.06) 6px,
      rgba(0, 0, 0, 0.06) 12px
    );
  }
}

.crop-modal {
  width: min(92vw, 920px);
}

.crop-wrap {
  max-height: 65vh;
  overflow: hidden;
  background: #111319;
}

.crop-image {
  display: block;
  max-width: 100%;
}

@media print {
  body {
    background: #fff;
    margin: 0;
  }

  .layout-app {
    max-width: none;
    padding: 0;
    margin: 0;
    background: #fff;
    color: #000;
  }

  .no-print {
    display: none !important;
  }

  .preview-wrap {
    background: #fff;
    padding: 0;
    margin: 0;
    overflow: visible;
    display: block;
    min-height: 0;
  }

  .sheet-preview-outer,
  .sheet-preview-inner {
    width: auto !important;
    height: auto !important;
    transform: none !important;
    margin: 0;
  }

  .sheet {
    box-shadow: none;
    page-break-after: avoid;
    page-break-inside: avoid;
    break-inside: avoid;
  }

  .cell {
    outline: 1px solid #bbb;
  }
}
</style>
