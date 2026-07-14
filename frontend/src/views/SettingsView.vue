<script setup lang="ts">
</script>

<template>
  <div class="settings">
    <header class="settings-header">
      <h1>设置</h1>
    </header>
    <p class="placeholder">设置页面（Phase 2 实现）</p>
  </div>
</template>

<style scoped>
.settings {
  width: min(100%, 800px);
  margin: 0 auto;
  padding: 2rem;
}
.settings-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}
h1 { font-size: 1.5rem; font-weight: 700; }
.placeholder { color: var(--muted-foreground); }
</style>
