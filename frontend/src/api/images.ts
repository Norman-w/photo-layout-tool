export interface UploadResponse {
  id: string
  originalUrl: string
  whiteBackgroundUrl: string
}

export async function uploadImage(file: File): Promise<UploadResponse> {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch('/api/images/upload', {
    method: 'POST',
    body: form,
  })
  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `上传失败 ${res.status}`)
  }
  return res.json() as Promise<UploadResponse>
}

export interface MatteResponse {
  url: string
}

export interface MatteOptions {
  /** 前景阈值 0~0.95，越高越去背景（易伤发丝） */
  foregroundThreshold?: number
  /** 软边 0.01~1 */
  edgeSoftness?: number
}

export async function matteImage(
  file: File,
  bgColor: string,
  options?: MatteOptions,
): Promise<MatteResponse> {
  const form = new FormData()
  form.append('file', file)
  form.append('bgColor', bgColor)
  if (options?.foregroundThreshold != null && Number.isFinite(options.foregroundThreshold)) {
    form.append('foregroundThreshold', String(options.foregroundThreshold))
  }
  if (options?.edgeSoftness != null && Number.isFinite(options.edgeSoftness)) {
    form.append('edgeSoftness', String(options.edgeSoftness))
  }
  const res = await fetch('/api/images/matte', {
    method: 'POST',
    body: form,
  })
  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `换色失败 ${res.status}`)
  }
  return res.json() as Promise<MatteResponse>
}
