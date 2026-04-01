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
  // 文本字段放在 file 之前，避免个别环境下 multipart 解析异常
  form.append('bgColor', bgColor)
  if (options != null) {
    if (options.foregroundThreshold != null) {
      const n = Number(options.foregroundThreshold)
      if (Number.isFinite(n)) form.append('foregroundThreshold', n.toString())
    }
    if (options.edgeSoftness != null) {
      const n = Number(options.edgeSoftness)
      if (Number.isFinite(n)) form.append('edgeSoftness', n.toString())
    }
  }
  form.append('file', file)
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
