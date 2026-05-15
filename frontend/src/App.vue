<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { authStore } from './stores/auth'
import Dashboard from './components/Dashboard.vue'

const ready = ref(false)

onMounted(() => {
  authStore.handleCallback()

  if (!authStore.isAuthenticated) {
    authStore.redirectToLogin()
    return
  }

  ready.value = true
})
</script>

<template>
  <Dashboard v-if="ready" />
</template>
