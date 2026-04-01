<script setup lang="ts">
import type { GlobalThemeOverrides } from 'naive-ui'
import { NConfigProvider, NMessageProvider, darkTheme } from 'naive-ui'
import { RouterView } from 'vue-router'

/** 与页面底色统一，避免卡片/弹层像「浮在白底上」 */
const themeOverrides: GlobalThemeOverrides = {
  common: {
    bodyColor: '#14161a',
    cardColor: '#252930',
    modalColor: '#252930',
    popoverColor: '#252930',
    tableColor: '#252930',
  },
  Card: {
    color: '#252930',
    colorModal: '#252930',
    borderColor: 'rgba(255, 255, 255, 0.08)',
    titleTextColor: '#e8eaed',
  },
  Modal: {
    color: '#252930',
    textColor: '#e8eaed',
  },
}
</script>

<template>
  <n-config-provider :theme="darkTheme" :theme-overrides="themeOverrides">
    <n-message-provider>
      <RouterView />
    </n-message-provider>
  </n-config-provider>
</template>
