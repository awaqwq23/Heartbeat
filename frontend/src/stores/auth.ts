import { ref, readonly } from 'vue'

const AUTH_SERVICE_URL = 'https://auth.shenxianovo.com'
const TOKEN_KEY = 'access_token'
const REFRESH_TOKEN_KEY = 'refresh_token'
const USER_ID_KEY = 'user_id'

const token = ref<string | null>(localStorage.getItem(TOKEN_KEY))
const refreshToken = ref<string | null>(localStorage.getItem(REFRESH_TOKEN_KEY))
const userId = ref<string | null>(localStorage.getItem(USER_ID_KEY))

let refreshPromise: Promise<boolean> | null = null

function setAuth(accessToken: string, uid: string, refresh?: string) {
  token.value = accessToken
  userId.value = uid
  localStorage.setItem(TOKEN_KEY, accessToken)
  localStorage.setItem(USER_ID_KEY, uid)
  if (refresh) {
    refreshToken.value = refresh
    localStorage.setItem(REFRESH_TOKEN_KEY, refresh)
  }
}

function clearAuth() {
  token.value = null
  refreshToken.value = null
  userId.value = null
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(REFRESH_TOKEN_KEY)
  localStorage.removeItem(USER_ID_KEY)
}

function redirectToLogin() {
  const redirectUrl = window.location.origin + window.location.pathname
  window.location.href = `${AUTH_SERVICE_URL}?redirect=${encodeURIComponent(redirectUrl)}`
}

function handleCallback(): boolean {
  const params = new URLSearchParams(window.location.search)
  const callbackToken = params.get('token')
  const callbackUserId = params.get('userId')
  const callbackRefresh = params.get('refreshToken')

  if (callbackToken && callbackUserId) {
    setAuth(callbackToken, callbackUserId, callbackRefresh ?? undefined)
    window.history.replaceState({}, document.title, window.location.pathname)
    return true
  }
  return false
}

async function tryRefresh(): Promise<boolean> {
  if (!refreshToken.value) return false

  if (refreshPromise) return refreshPromise

  refreshPromise = (async () => {
    try {
      const res = await fetch(`${AUTH_SERVICE_URL}/api/v1/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: refreshToken.value }),
      })
      if (!res.ok) return false
      const data = await res.json()
      setAuth(data.accessToken, data.userId ?? userId.value!, data.refreshToken)
      return true
    } catch {
      return false
    } finally {
      refreshPromise = null
    }
  })()

  return refreshPromise
}

export const authStore = {
  token: readonly(token),
  userId: readonly(userId),
  get isAuthenticated() { return token.value !== null },
  setAuth,
  clearAuth,
  redirectToLogin,
  handleCallback,
  tryRefresh,
}
