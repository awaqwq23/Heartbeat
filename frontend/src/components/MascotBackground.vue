<script setup lang="ts">
import { useTheme } from '@/composables/useTheme'

const { isDark } = useTheme()
</script>

<template>
  <img
    src="/mascot.png"
    alt=""
    aria-hidden="true"
    class="mascot"
    :class="{ 'mascot--dark': isDark }"
  />
</template>

<style scoped>
.mascot {
  position: fixed;
  bottom: 0;
  left: 0;
  z-index: 0;
  width: clamp(220px, 33vw, 520px);
  height: auto;
  pointer-events: none;
  user-select: none;
  opacity: 0.55;
  /* 水平镜像，放左下角朝向内容区 */
  transform: scaleX(-1);
  transition: opacity 0.3s;
}

.mascot--dark {
  opacity: 0.32;
}

/* 窄屏内容优先，进一步降低存在感 */
@media (max-width: 768px) {
  .mascot {
    width: 140px;
    opacity: 0.3;
  }
}
</style>
