<script setup lang="ts">
import { useTheme } from '@/composables/useTheme'

const { isDark } = useTheme()
</script>

<template>
  <img
    src="/boqi.png"
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
  /* 单一连续 clamp：窄屏取下限（占比大），宽屏随 vw 增长并封顶，
     全程连续，避免断点硬切造成的跳变 */
  width: clamp(220px, 40vw, 460px);
  height: auto;
  pointer-events: none;
  user-select: none;
  opacity: 0.5;
  /* 水平镜像，放左下角朝向内容区 */
  transform: scaleX(-1);
  transition: opacity 0.3s;
}

.mascot--dark {
  opacity: 0.3;
}
</style>
