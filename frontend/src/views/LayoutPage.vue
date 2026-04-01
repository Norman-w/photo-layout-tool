<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import Cropper from 'cropperjs'
import 'cropperjs/dist/cropper.css'
import { NButton, NCard, NSelect, NSlider, NSpace, NTag, useMessage } from 'naive-ui'
import { matteImage } from '../api/images'
import { computeBestLayout } from '../composables/useSheetLayout'

type PaperItem = {
  id: string
  label: string
  sub: string
  widthMm: number
  heightMm: number
}

type PhotoItem = {
  id: string
  label: string
  sub: string
  widthMm: number
  heightMm: number
}

type SourceImage = {
  name: string
  src: string
  croppedSrc?: string
  processedSrc?: string
}

const message = useMessage()

const paperItems: PaperItem[] = [
  { id: 'p46', label: '4×6 寸', sub: '102×152 mm', widthMm: 102, heightMm: 152 },
  { id: 'p57', label: '5×7 寸', sub: '127×178 mm', widthMm: 127, heightMm: 178 },
  { id: 'p68', label: '6×8 寸', sub: '152×203 mm', widthMm: 152, heightMm: 203 },
  { id: 'a4', label: 'A4', sub: '210×297 mm', widthMm: 210, heightMm: 297 },
]

const photoItems: PhotoItem[] = [
  { id: 'id1', label: '一寸', sub: '25×35 mm', widthMm: 25, heightMm: 35 },
  { id: 'id2s', label: '小二寸', sub: '35×45 mm', widthMm: 35, heightMm: 45 },
  { id: 'id2', label: '二寸', sub: '35×49 mm', widthMm: 35, heightMm: 49 },
  { id: 'pass', label: '护照', sub: '33×48 mm', widthMm: 33, heightMm: 48 },
]

const bgColorOptions = [
  { label: '白底', value: '#ffffff' },
  { label: '浅灰底', value: '#f2f2f2' },
  { label: '蓝底', value: '#2b7bde' },
  { label: '红底', value: '#d73a30' },
]

const fileInputRef = ref<HTMLInputElement | null>(null)
const cropImageRef = ref<HTMLImageElement | null>(null)
const cropBackdropRef = ref<HTMLElement | null>(null)

const selectedPaperId = ref(paperItems[0].id)
const selectedPhotoId = ref(photoItems[0].id)
/** false=竖向相纸（默认长边朝上打印）；true=交换宽高，适合打印机进纸方向 */
const paperLandscape = ref(false)

const backgroundColor = ref('#ffffff')
const mattingForegroundThreshold = ref(0.58)
const mattingEdgeSoftness = ref(0.12)
const mattingEdgeColorPull = ref(0.42)

const source = ref<SourceImage | null>(null)
const cropModalVisible = ref(false)
const cropper = ref<Cropper | null>(null)
const processingColor = ref(false)

const bgOptions = bgColorOptions.map((x) => ({ label: x.label, value: x.value }))

const selectedPaper = computed(() => paperItems.find((p) => p.id === selectedPaperId.value) ?? paperItems[0])
const selectedPhoto = computed(() => photoItems.find((p) => p.id === selectedPhotoId.value) ?? photoItems[0])

const paperWmm = computed(() =>
  paperLandscape.value ? selectedPaper.value.heightMm : selectedPaper.value.widthMm,
)
const paperHmm = computed(() =>
  paperLandscape.value ? selectedPaper.value.widthMm : selectedPaper.value.heightMm,
)

const layout = computed(() =>
  computeBestLayout(
    paperWmm.value,
    paperHmm.value,
    selectedPhoto.value.widthMm,
    selectedPhoto.value.heightMm,
  ),
)

const effectivePhotoRatio = computed(() => layout.value.slotWmm / layout.value.slotHmm)
const cropPreviewStyle = computed(() => ({ aspectRatio: `${layout.value.slotWmm} / ${layout.value.slotHmm}` }))

const cropSourcePreview = computed(() => source.value?.croppedSrc ?? source.value?.src ?? '')
const finalSourcePreview = computed(() => source.value?.processedSrc ?? cropSourcePreview.value)

const cells = computed(() =>
  Array.from({ length: layout.value.count }, () => (source.value ? finalSourcePreview.value : null)),
)

/** 96 CSS px ≈ 1in，用于把 mm 换算成屏幕像素再算缩放，使预览在常见屏宽内完整可见 */
const previewScale = computed(() => {
  const mmToCssPx = 96 / 25.4
  const wPx = layout.value.sheetWmm * mmToCssPx
  const hPx = layout.value.sheetHmm * mmToCssPx
  const maxW = 540
  const maxH = 520
  return Math.min(1, maxW / Math.max(wPx, 1), maxH / Math.max(hPx, 1))
})

const statsText = computed(() => {
  const L = layout.value
  if (L.count < 1) {
    return '当前相纸放不下所选单张尺寸，请换更大相纸、或点击「横置相纸」调整进纸方向。'
  }
  const orient = paperLandscape.value ? '横置相纸' : '竖置相纸'
  return `${orient} · 排版 ${L.cols}×${L.rows} = ${L.count} 张 · 均匀缝 ${L.gapMm.toFixed(2)} mm · 边距 左右 ${L.padXMm.toFixed(1)} / 上下 ${L.padYMm.toFixed(1)} mm`
})

function pickPaper(id: string) {
  selectedPaperId.value = id
}

function pickPhoto(id: string) {
  selectedPhotoId.value = id
  if (cropper.value) cropper.value.setAspectRatio(effectivePhotoRatio.value)
}

function togglePaperLandscape() {
  paperLandscape.value = !paperLandscape.value
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

function cropperRotate(deg: number) {
  cropper.value?.rotate(deg)
}

function onCropKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') closeCropModal()
}

async function initCropper() {
  await nextTick()
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
    rotatable: true,
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
    message.success('换色完成，下方整版已更新')
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
  selectedPaperId.value = paperItems[0].id
  selectedPhotoId.value = photoItems[0].id
  paperLandscape.value = false
  backgroundColor.value = '#ffffff'
  mattingForegroundThreshold.value = 0.58
  mattingEdgeSoftness.value = 0.12
  mattingEdgeColorPull.value = 0.42
}

watch(cropModalVisible, (open) => {
  if (open) {
    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', onCropKeydown)
    void initCropper()
    void nextTick(() => cropBackdropRef.value?.focus())
    return
  }
  document.body.style.overflow = ''
  window.removeEventListener('keydown', onCropKeydown)
  cropper.value?.destroy()
  cropper.value = null
})

watch([effectivePhotoRatio, cropModalVisible], () => {
  if (cropModalVisible.value && cropper.value) {
    cropper.value.setAspectRatio(effectivePhotoRatio.value)
  }
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
  document.body.style.overflow = ''
  window.removeEventListener('keydown', onCropKeydown)
  cropper.value?.destroy()
})
</script>

<template>
  <div class="layout-app">
    <header class="no-print layout-app__header">
      <h1 class="layout-app__title">照片拼版工具</h1>
      <p class="layout-app__sub">
        ① 选相纸与方向 → ② 选证件尺寸 → ③ 上传 → ④ 裁剪（可旋转）→ ⑤ 换色 → ⑥ 看整版预览后打印
      </p>
    </header>

    <n-card class="no-print panel workflow-card" title="排版设置">
      <div class="workflow">
        <section class="step">
          <h3 class="step__title">1. 相纸</h3>
          <p class="step__hint">点选比例卡片；若打印出来脑袋方向不对，用「横置相纸」交换长宽。</p>
          <div class="tile-row">
            <button
              v-for="p in paperItems"
              :key="p.id"
              type="button"
              class="ratio-tile"
              :class="{ 'ratio-tile--active': selectedPaperId === p.id }"
              @click="pickPaper(p.id)"
            >
              <div
                class="ratio-tile__shape ratio-tile__shape--paper"
                :style="{
                  aspectRatio: paperLandscape
                    ? `${p.heightMm} / ${p.widthMm}`
                    : `${p.widthMm} / ${p.heightMm}`,
                }"
              />
              <span class="ratio-tile__label">{{ p.label }}</span>
              <span class="ratio-tile__sub">{{ p.sub }}</span>
            </button>
          </div>
          <n-button size="small" secondary class="step__btn" @click="togglePaperLandscape">
            {{ paperLandscape ? '当前：横置相纸（已交换宽高）' : '当前：竖置相纸' }} — 点击切换
          </n-button>
        </section>

        <section class="step">
          <h3 class="step__title">2. 单张尺寸（竖向证件比例）</h3>
          <p class="step__hint">排版始终按此竖向比例铺排，不会自动把人头横过来换张数。</p>
          <div class="tile-row">
            <button
              v-for="ph in photoItems"
              :key="ph.id"
              type="button"
              class="ratio-tile"
              :class="{ 'ratio-tile--active': selectedPhotoId === ph.id }"
              @click="pickPhoto(ph.id)"
            >
              <div
                class="ratio-tile__shape ratio-tile__shape--photo"
                :style="{ aspectRatio: `${ph.widthMm} / ${ph.heightMm}` }"
              />
              <span class="ratio-tile__label">{{ ph.label }}</span>
              <span class="ratio-tile__sub">{{ ph.sub }}</span>
            </button>
          </div>
        </section>

        <section class="step">
          <h3 class="step__title">3. 原图与换色</h3>
          <n-space align="center" wrap>
            <n-button type="primary" @click="pickOriginal">选择或更换原图</n-button>
            <n-button :disabled="!source" @click="openCropModal">打开裁剪</n-button>
            <n-tag type="warning">底色</n-tag>
            <n-select
              style="width: 140px"
              :options="bgOptions"
              :value="backgroundColor"
              @update:value="(v) => (backgroundColor = v)"
            />
            <n-button :loading="processingColor" :disabled="!source || processingColor" type="primary" @click="applyBackgroundColor">
              换色
            </n-button>
          </n-space>
          <input ref="fileInputRef" class="sr-only" type="file" accept="image/*" @change="onSelectOriginal" />

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
            </div>
          </n-space>
        </section>

        <p class="stats">{{ statsText }}</p>
      </div>
    </n-card>

    <div class="no-print source-row">
      <n-card class="source-box" size="small" title="④ 原图（完整）">
        <div class="box-fixed square">
          <img v-if="source" :src="source.src" :alt="source.name" />
          <span v-else class="placeholder">未选择</span>
        </div>
      </n-card>

      <n-card class="source-box" size="small" :title="`⑤ 裁剪后（${layout.slotWmm.toFixed(0)}×${layout.slotHmm.toFixed(0)} mm）`">
        <div class="box-fixed ratio" :style="cropPreviewStyle">
          <img v-if="source" :src="cropSourcePreview" :alt="source.name" />
          <span v-else class="placeholder">裁剪后</span>
        </div>
      </n-card>

      <n-card class="source-box" size="small" title="⑥ 换色后（单张效果）">
        <div class="box-fixed ratio" :style="cropPreviewStyle">
          <img v-if="source" :src="finalSourcePreview" :alt="source.name" />
          <span v-else class="placeholder">换色后</span>
        </div>
      </n-card>
    </div>

    <h3 class="no-print preview-heading">整版出片预览（比例与打印一致，已按屏幕自适应缩放）</h3>
    <div class="preview-wrap">
      <div v-if="layout.count < 1" class="preview-empty no-print">
        无法排版：请调整相纸或单张规格。
      </div>
      <div
        v-else
        class="sheet-preview-outer"
        :style="{
          '--paper-w': layout.sheetWmm + 'mm',
          '--paper-h': layout.sheetHmm + 'mm',
          '--preview-scale': previewScale,
        }"
      >
        <div class="sheet-preview-inner">
          <div
            class="sheet"
            :style="{
              width: layout.sheetWmm + 'mm',
              height: layout.sheetHmm + 'mm',
              background: '#ffffff',
            }"
          >
            <div
              class="sheet-grid"
              :style="{
                gridTemplateColumns: `repeat(${layout.cols}, ${layout.slotWmm}mm)`,
                gridTemplateRows: `repeat(${layout.rows}, ${layout.slotHmm}mm)`,
                gap: layout.gapMm > 0 ? layout.gapMm + 'mm' : '0',
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
    </div>

    <div class="no-print bottom-actions">
      <div class="bottom-actions__inner">
        <button
          type="button"
          class="cyber-action cyber-action--print"
          :disabled="!source || layout.count < 1"
          @click="printSheet"
        >
          <span class="cyber-action__glow" aria-hidden="true" />
          <span class="cyber-action__shine" aria-hidden="true" />
          <span class="cyber-action__text">打印出片</span>
        </button>
        <button type="button" class="cyber-action cyber-action--reset" @click="resetAll">
          <span class="cyber-action__glow cyber-action__glow--muted" aria-hidden="true" />
          <span class="cyber-action__shine cyber-action__shine--muted" aria-hidden="true" />
          <span class="cyber-action__text">重置全部</span>
        </button>
      </div>
    </div>

    <!-- 使用 Teleport 自建弹层：避免 Naive Modal 在部分环境下未挂到 body 导致内容落在页面底部 -->
    <Teleport to="body">
      <div
        v-if="cropModalVisible"
        ref="cropBackdropRef"
        class="crop-dialog-backdrop no-print"
        role="dialog"
        aria-modal="true"
        aria-labelledby="crop-dialog-title"
        tabindex="-1"
        @click.self="closeCropModal"
      >
        <div class="crop-dialog-panel" @click.stop>
          <div class="crop-dialog-header">
            <h2 id="crop-dialog-title" class="crop-dialog-title">裁剪（可旋转原图）</h2>
            <button type="button" class="crop-dialog-close" aria-label="关闭" @click="closeCropModal">
              ×
            </button>
          </div>
          <div class="crop-dialog-body">
            <p class="hint">
              裁剪框比例 {{ layout.slotWmm.toFixed(0) }}×{{ layout.slotHmm.toFixed(0) }} mm（竖向）。方向不对请用旋转。
            </p>
            <n-space style="margin-bottom: 8px">
              <n-button size="small" @click="cropperRotate(-90)">逆时针 90°</n-button>
              <n-button size="small" @click="cropperRotate(90)">顺时针 90°</n-button>
            </n-space>
            <div class="crop-wrap">
              <img v-if="source" ref="cropImageRef" class="crop-image" :src="source.src" :alt="source.name" />
            </div>
          </div>
          <div class="crop-dialog-footer">
            <n-space justify="end">
              <n-button @click="closeCropModal">取消</n-button>
              <n-button type="primary" @click="applyCrop">应用裁剪</n-button>
            </n-space>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style lang="scss">
.layout-app {
  max-width: 1180px;
  margin: 0 auto;
  padding: 1.1rem;
  background: #14161a;
  min-height: 100vh;
  color: #e8eaed;

  &__title {
    margin: 0 0 0.2rem;
  }

  &__sub {
    color: #9aa0a6;
    margin: 0 0 0.7rem;
    font-size: 0.9rem;
    line-height: 1.45;
  }
}

.workflow-card {
  background: #252930 !important;
  margin-bottom: 0.75rem;
}

.workflow {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.step {
  &__title {
    margin: 0 0 0.35rem;
    font-size: 1rem;
    font-weight: 600;
    color: #e8eaed;
  }

  &__hint {
    margin: 0 0 0.6rem;
    font-size: 0.8rem;
    color: #8b9199;
    line-height: 1.4;
  }

  &__btn {
    margin-top: 0.5rem;
  }
}

.tile-row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
}

.ratio-tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.35rem;
  padding: 0.55rem 0.65rem 0.5rem;
  min-width: 5.5rem;
  border-radius: 10px;
  border: 2px solid #3d4450;
  background: #1e2229;
  color: #c4c7cc;
  cursor: pointer;
  transition: border-color 0.15s, background 0.15s;

  &:hover {
    border-color: #5c6575;
    background: #252a33;
  }

  &--active {
    border-color: #63a4ff;
    background: #252d3d;
    color: #e8eaed;
  }

  &__shape {
    width: 44px;
    max-height: 56px;
    border-radius: 3px;
    background: linear-gradient(145deg, #4a5568, #2d3340);
    border: 1px solid #5a6478;
    box-sizing: border-box;

    &--paper {
      width: 40px;
    }

    &--photo {
      width: 34px;
    }
  }

  &__label {
    font-size: 0.8rem;
    font-weight: 600;
  }

  &__sub {
    font-size: 0.68rem;
    color: #8b9199;
  }
}

.panel {
  background: #252930 !important;
  margin-bottom: 0.8rem;
}

.stats {
  margin: 0;
  color: #9aa0a6;
  font-size: 0.85rem;
  line-height: 1.45;
}

.matting-sliders {
  margin-top: 0.65rem;

  &__row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.5rem 1rem;
  }

  &__label {
    min-width: 9.5rem;
    color: #c4c7cc;
    font-size: 0.875rem;
  }
}

.source-row {
  display: flex;
  gap: 0.65rem;
  overflow-x: auto;
  margin-bottom: 0.65rem;
}

.preview-heading {
  margin: 0 0 0.5rem;
  font-size: 0.95rem;
  font-weight: 600;
  color: #c4c7cc;
}

.source-box {
  background: #252930 !important;
  min-width: 280px;
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
  min-height: 160px;
  max-height: 200px;

  &.square {
    aspect-ratio: 1 / 1;
  }

  &.ratio {
    min-height: 160px;
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
  padding: 0.85rem;
  overflow: auto;
  display: flex;
  justify-content: center;
  align-items: flex-start;
  min-height: 120px;
}

.preview-empty {
  color: #c98;
  padding: 1rem;
  text-align: center;
}

.bottom-actions {
  margin-top: 1.1rem;
  padding-top: 1.15rem;
  border-top: 1px solid rgba(0, 220, 255, 0.08);
}

.bottom-actions__inner {
  display: flex;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 1rem 1.25rem;
}

.cyber-action {
  position: relative;
  overflow: hidden;
  padding: 1rem 2.6rem;
  min-width: 9.5rem;
  font-size: 1.1rem;
  font-weight: 600;
  letter-spacing: 0.12em;
  font-family: inherit;
  border-radius: 14px;
  cursor: pointer;
  border: 1px solid rgba(255, 255, 255, 0.14);
  transition:
    transform 0.22s cubic-bezier(0.34, 1.25, 0.64, 1),
    box-shadow 0.32s ease,
    border-color 0.25s ease,
    background 0.25s ease,
    color 0.22s ease,
    filter 0.22s ease;

  &:focus-visible {
    outline: 2px solid rgba(0, 230, 255, 0.85);
    outline-offset: 4px;
  }

  &__text {
    position: relative;
    z-index: 2;
  }

  &__glow {
    pointer-events: none;
    position: absolute;
    inset: -40%;
    z-index: 0;
    opacity: 0;
    background: radial-gradient(circle at 50% 50%, rgba(0, 240, 255, 0.35) 0%, transparent 55%);
    transition: opacity 0.35s ease;

    &--muted {
      background: radial-gradient(circle at 50% 50%, rgba(140, 170, 255, 0.28) 0%, transparent 55%);
    }
  }

  &__shine {
    pointer-events: none;
    position: absolute;
    inset: 0;
    z-index: 1;
    opacity: 0;
    background: linear-gradient(
      105deg,
      transparent 38%,
      rgba(255, 255, 255, 0.22) 50%,
      transparent 62%
    );
    transform: translateX(-120%);

    &--muted {
      background: linear-gradient(
        105deg,
        transparent 38%,
        rgba(180, 200, 255, 0.2) 50%,
        transparent 62%
      );
    }
  }

  &:hover:not(:disabled) &__glow,
  &:hover &__glow--muted {
    opacity: 1;
  }

  &:hover:not(:disabled) &__shine,
  &:hover &__shine--muted {
    opacity: 1;
    animation: cyber-sweep 0.7s ease forwards;
  }
}

@keyframes cyber-sweep {
  to {
    transform: translateX(120%);
  }
}

.cyber-action--print {
  color: #e8fdff;
  text-shadow: 0 0 20px rgba(0, 220, 255, 0.35);
  background: linear-gradient(168deg, #1a4a5c 0%, #0c2433 42%, #0e3044 100%);
  border-color: rgba(0, 210, 255, 0.55);
  box-shadow:
    0 0 0 1px rgba(0, 200, 255, 0.1),
    0 6px 24px rgba(0, 0, 0, 0.4),
    inset 0 1px 0 rgba(255, 255, 255, 0.14);

  &:hover:not(:disabled) {
    transform: translateY(-4px) scale(1.03);
    border-color: rgba(0, 255, 255, 0.95);
    box-shadow:
      0 0 32px rgba(0, 230, 255, 0.55),
      0 0 80px rgba(0, 180, 255, 0.2),
      0 14px 36px rgba(0, 0, 0, 0.45),
      inset 0 1px 0 rgba(255, 255, 255, 0.22);
    filter: brightness(1.08);
  }

  &:active:not(:disabled) {
    transform: translateY(-2px) scale(1.015);
  }

  &:disabled {
    opacity: 0.38;
    cursor: not-allowed;
    text-shadow: none;
    filter: grayscale(0.35);
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
  }
}

.cyber-action--reset {
  color: #c5d4e8;
  background: linear-gradient(168deg, #323846 0%, #1e232e 45%, #262c3a 100%);
  border-color: rgba(130, 155, 200, 0.4);
  box-shadow:
    0 6px 20px rgba(0, 0, 0, 0.32),
    inset 0 1px 0 rgba(255, 255, 255, 0.07);

  &:hover {
    color: #fff;
    transform: translateY(-4px) scale(1.03);
    border-color: rgba(170, 195, 255, 0.75);
    box-shadow:
      0 0 28px rgba(130, 160, 255, 0.35),
      0 10px 32px rgba(0, 0, 0, 0.38),
      inset 0 1px 0 rgba(255, 255, 255, 0.12);
    filter: brightness(1.06);
  }

  &:active {
    transform: translateY(-2px) scale(1.015);
  }
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
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.4);
}

.sheet-grid {
  display: grid;
  flex-shrink: 0;
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

.crop-dialog-backdrop {
  position: fixed;
  inset: 0;
  z-index: 4000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
  background: rgba(0, 0, 0, 0.58);
  outline: none;
}

.crop-dialog-panel {
  width: min(92vw, 920px);
  max-height: min(90vh, 880px);
  display: flex;
  flex-direction: column;
  background: #252930;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  box-shadow: 0 16px 48px rgba(0, 0, 0, 0.45);
}

.crop-dialog-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.85rem 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.crop-dialog-title {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
  color: #e8eaed;
}

.crop-dialog-close {
  flex-shrink: 0;
  width: 2rem;
  height: 2rem;
  border: none;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.08);
  color: #c4c7cc;
  font-size: 1.35rem;
  line-height: 1;
  cursor: pointer;

  &:hover {
    background: rgba(255, 255, 255, 0.14);
    color: #fff;
  }
}

.crop-dialog-body {
  padding: 0.75rem 1rem;
  overflow: hidden;
  min-height: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
}

.crop-dialog-footer {
  padding: 0.75rem 1rem;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}

.crop-wrap {
  flex: 1;
  min-height: 200px;
  max-height: min(58vh, 640px);
  overflow: auto;
  background: #111319;
  border-radius: 8px;
}

.crop-image {
  display: block;
  max-width: 100%;
}

@media print {
  html {
    color-scheme: light;
    background: #fff;
  }

  body {
    background: #fff !important;
    color: #000;
    margin: 0;
  }

  #app {
    background: #fff !important;
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
