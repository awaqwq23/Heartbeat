<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { authStore } from '../stores/auth'

const router = useRouter()

onMounted(() => {
  authStore.handleCallback()

  if (authStore.username.value) {
    router.replace(`/${authStore.username.value}`)
  } else {
    router.replace('/')
  }
})
</script>

<template>
  <div></div>
</template>
