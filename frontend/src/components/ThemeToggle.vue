<script setup lang="ts">
import { Moon, Sun } from 'lucide-vue-next'
import { useTheme } from '@/composables/useTheme'

const { isDark, toggle } = useTheme()
</script>

<template>
  <button
    class="theme-toggle"
    :title="isDark ? '切换到浅色' : '切换到深色'"
    @click="toggle"
  >
    <component :is="isDark ? Sun : Moon" :size="18" />
  </button>
</template>

<style scoped>
.theme-toggle {
  position: fixed;
  top: 1rem;
  right: 1rem;
  z-index: 50;
  display: grid;
  place-items: center;
  width: 2.5rem;
  height: 2.5rem;
  border-radius: 9999px;
  border: 1px solid var(--glass-border);
  background: var(--glass-bg);
  backdrop-filter: blur(var(--glass-blur));
  color: var(--foreground);
  box-shadow: var(--shadow-sm);
  cursor: pointer;
  transition: box-shadow 0.2s, transform 0.2s;
}
.theme-toggle:hover {
  box-shadow: var(--shadow-md);
  transform: translateY(-1px);
}
</style>
