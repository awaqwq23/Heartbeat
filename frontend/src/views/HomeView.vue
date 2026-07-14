<script setup lang="ts">
import Dashboard from '../components/Dashboard.vue'
import { authStore } from '../stores/auth'
</script>

<template>
  <Dashboard :username="authStore.username.value" />
</template>
