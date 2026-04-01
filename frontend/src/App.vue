<script setup lang="ts">
import { NConfigProvider, NMessageProvider, darkTheme } from 'naive-ui'
import { RouterView } from 'vue-router'
</script>

<template>
  <n-config-provider :theme="darkTheme">
    <n-message-provider>
      <RouterView />
    </n-message-provider>
  </n-config-provider>
</template>
