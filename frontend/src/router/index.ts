import { createRouter, createWebHistory } from 'vue-router'

const RESERVED_ROUTES = ['settings', 'callback']

const router = createRouter({
  history: createWebHistory('/'),
  routes: [
    {
      path: '/',
      redirect: '/awaqwq233',
    },
    {
      path: '/settings',
      component: () => import('../views/SettingsView.vue'),
    },
    {
      path: '/:username',
      component: () => import('../views/ProfileView.vue'),
    },
  ],
})

export { RESERVED_ROUTES }
export default router
