import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useImageLayoutStore = defineStore('imageLayout', () => {
  const imageUrls = ref<string[]>([])

  function addUrls(urls: string[]) {
    imageUrls.value = imageUrls.value.concat(urls)
  }

  function addUrl(url: string) {
    imageUrls.value = [...imageUrls.value, url]
  }

  function clear() {
    imageUrls.value = []
  }

  return { imageUrls, addUrls, addUrl, clear }
})
