<script setup lang="ts">
import { useRoute } from 'vue-router'
import Dashboard from '../components/Dashboard.vue'

const route = useRoute()
const username = route.params.username as string
</script>

<template>
  <Dashboard :username="username" />
</template>
