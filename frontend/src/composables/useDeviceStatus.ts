import { computed, onMounted, onUnmounted, type Ref, type ComputedRef } from 'vue'
import type { DeviceStatusResponse } from '../api/index'
import { fetchPublicDeviceStatus } from '../api/index'
import { useAsyncData } from './useAsyncData'

/**
 * 在场域：设备实时状态（是否在线、当前应用、最后活跃时间）。
 * 自带 5s 轮询（仅在查看今天时刷新）。与报表的 30s 轮询生命周期独立。
 */
export function useDeviceStatus(
  username: string,
  selectedDevice: Ref<number>,
  isToday: Ref<boolean>,
  appNameMap: ComputedRef<Map<number, string>>,
) {
  const status = useAsyncData<DeviceStatusResponse | null>(
    () => fetchPublicDeviceStatus(username, selectedDevice.value),
    null,
  )
  const deviceStatus = status.data

  const isAlive = computed(() => isToday.value && (deviceStatus.value?.isOnline ?? false))
  const currentApp = computed(() => deviceStatus.value?.currentApp ?? null)

  const currentAppId = computed(() => {
    const name = currentApp.value
    if (!name) return null
    for (const [id, n] of appNameMap.value) {
      if (n === name) return id
    }
    return null
  })

  const lastSeenStr = computed(() => {
    const raw = deviceStatus.value?.lastSeen
    if (!raw) return ''
    return raw.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
  })

  async function load() {
    if (!selectedDevice.value) return
    await status.run()
  }

  let timer: ReturnType<typeof setInterval>
  onMounted(() => {
    timer = setInterval(() => {
      if (isToday.value) load()
    }, 5_000)
  })
  onUnmounted(() => clearInterval(timer))

  return {
    deviceStatus,
    error: status.error,
    isAlive,
    currentApp,
    currentAppId,
    lastSeenStr,
    load,
  }
}
