import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '.', 'VITE_')
  return {
    base: env.VITE_BASE_PATH || '/',
    plugins: [vue()],
    server: {
      port: 3000,
      proxy: {
        '/api': 'https://shenxianovo.com/heartbeat'
      }
    }
  }
})
