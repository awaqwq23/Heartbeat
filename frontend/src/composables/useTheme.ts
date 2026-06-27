import { ref, watch } from 'vue'

export type ThemeMode = 'light' | 'dark' | 'system'

const STORAGE_KEY = 'heartbeat-theme'

function getStored(): ThemeMode {
  const v = localStorage.getItem(STORAGE_KEY)
  return v === 'light' || v === 'dark' || v === 'system' ? v : 'system'
}

function systemPrefersDark(): boolean {
  return window.matchMedia('(prefers-color-scheme: dark)').matches
}

/** 当前生效的明暗（解析 system 后的结果） */
const isDark = ref(false)
/** 用户选择的模式（light / dark / system） */
const mode = ref<ThemeMode>(getStored())

function apply() {
  const dark = mode.value === 'system' ? systemPrefersDark() : mode.value === 'dark'
  isDark.value = dark
  document.documentElement.classList.toggle('dark', dark)
}

// 跟随系统变化（仅当 mode === 'system' 时生效）
const mql = window.matchMedia('(prefers-color-scheme: dark)')
mql.addEventListener('change', () => {
  if (mode.value === 'system') apply()
})

watch(mode, (m) => {
  localStorage.setItem(STORAGE_KEY, m)
  apply()
})

// 首次解析
apply()

export function useTheme() {
  /** 在 light / dark 间切换（system 模式下先解析当前明暗再反转，转为显式选择） */
  function toggle() {
    const currentlyDark = mode.value === 'system' ? systemPrefersDark() : mode.value === 'dark'
    mode.value = currentlyDark ? 'light' : 'dark'
  }

  function setMode(m: ThemeMode) {
    mode.value = m
  }

  return { isDark, mode, toggle, setMode }
}
