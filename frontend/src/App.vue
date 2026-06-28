<script setup lang="ts">
import { ConfigProvider } from 'reka-ui'
import ThemeToggle from '@/components/ThemeToggle.vue'
import MascotBackground from '@/components/MascotBackground.vue'
</script>

<template>
  <!-- scrollBody=false：禁用 Reka 弹层的 body padding/margin 补偿，
       改由全局 scrollbar-gutter: stable 处理滚动条空间，避免横向跳动 -->
  <ConfigProvider :scroll-body="false">
    <MascotBackground />
    <ThemeToggle />
    <router-view />
  </ConfigProvider>
</template>
