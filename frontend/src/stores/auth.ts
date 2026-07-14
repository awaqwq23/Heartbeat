import { readonly, ref } from 'vue'

// 登录功能暂时禁用，默认使用 awaqwq233。
// 恢复登录后请重建完整的 auth store（含 token/refresh 管理、redirectToLogin、handleCallback 等）。

const DEFAULT_USERNAME = 'awaqwq233'

const username = ref<string>(DEFAULT_USERNAME)

export const authStore = {
  username: readonly(username),
  /** 暂时总是返回 true（登录已禁用） */
  get isAuthenticated() { return true },
  /** 忽略（保留为空函数以兼容现有调用） */
  redirectToLogin() {},
  /** 忽略（保留为空函数以兼容现有调用） */
  logout() {},
  /** 忽略（保留为空函数以兼容现有调用） */
  clearAuth() {},
}
