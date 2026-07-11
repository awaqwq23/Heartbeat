import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import type { AppInfoResponse, KeyFrequencyItem } from '../api/index'
import { fetchPublicApps, fetchPublicKeyFrequency, getTimezoneLabel } from '../api/index'
import { useAsyncData } from './useAsyncData'
import { useDeviceSelection } from './useDeviceSelection'
import { useDeviceStatus } from './useDeviceStatus'
import { useReports } from './useReports'

export function formatDuration(sec: number): string {
  const h = Math.floor(sec / 3600)
  const m = Math.floor((sec % 3600) / 60)
  if (h > 0) return `${h}h ${m}m`
  if (m > 0) return `${m}m`
  return '< 1m'
}

/**
 * Dashboard 的瘦协调器：持有应用元数据，组合设备选择 / 在场 / 报表三个数据域，
 * 编排 30s 报表轮询与 device/date 变更时的统一刷新。
 */
export function useHeartbeat(username: string) {
  const selection = useDeviceSelection(username)
  const { selectedDevice, selectedDate, isToday } = selection

  const appsData = useAsyncData<AppInfoResponse[]>(() => fetchPublicApps(username), [])
  const apps = appsData.data
  const loading = ref(false)

  const appNameMap = computed(() => {
    const map = new Map<number, string>()
    for (const app of apps.value) map.set(app.id!, app.name!)
    return map
  })

  const status = useDeviceStatus(username, selectedDevice, isToday, appNameMap)
  const reports = useReports(username, selectedDevice, selectedDate)

  const kf = useAsyncData<KeyFrequencyItem[]>(() => {
    const dateObj = new Date(selectedDate.value + 'T00:00:00')
    return fetchPublicKeyFrequency(username, {
      deviceId: selectedDevice.value,
      start: dateObj.toISOString(),
      end: new Date(dateObj.getTime() + 86400000).toISOString(),
    })
  }, [])
  const keyFrequency = kf.data
  async function loadKeyFrequency() {
    if (!selectedDevice.value) return
    await kf.run()
  }

  // 任一数据域出错就点亮:UI 据此区分"出错"与"这天没数据"。
  const error = computed(() =>
    selection.error.value
    ?? appsData.error.value
    ?? status.error.value
    ?? reports.error.value
    ?? kf.error.value,
  )

  const timezoneLabel = getTimezoneLabel()

  async function refresh() {
    loading.value = true
    try {
      // 设备列表没拉起来(selectedDevice 恒为 0)时,先补拉一次,否则下面全早退。
      if (!selectedDevice.value) await selection.reload()
      await Promise.all([
        appsData.run(),
        reports.loadUsage(),
        status.load(),
        reports.loadDaily(),
        reports.loadWeekly(),
        loadKeyFrequency(),
      ])
    } finally {
      loading.value = false
    }
  }

  let usageTimer: ReturnType<typeof setInterval>

  onMounted(async () => {
    await appsData.run()

    usageTimer = setInterval(() => {
      if (isToday.value) {
        reports.loadUsage()
        reports.loadDaily()
        reports.loadWeekly()
        loadKeyFrequency()
      }
    }, 30_000)
  })

  onUnmounted(() => clearInterval(usageTimer))

  watch([selectedDevice, selectedDate], () => refresh())

  return {
    devices: selection.devices,
    error,
    refresh,
    selectedDevice,
    selectedDeviceName: selection.selectedDeviceName,
    selectedDate,
    usageData: reports.usageData,
    appNameMap,
    loading,
    isToday,
    isAlive: status.isAlive,
    currentApp: status.currentApp,
    currentAppId: status.currentAppId,
    lastSeenStr: status.lastSeenStr,
    appSummaries: reports.appSummaries,
    totalSeconds: reports.totalSeconds,
    usageSeconds: reports.usageSeconds,
    awaySeconds: reports.awaySeconds,
    maxSeconds: reports.maxSeconds,
    activeHours: reports.activeHours,
    weeklyAppSummaries: reports.weeklyAppSummaries,
    weeklyTotalSeconds: reports.weeklyTotalSeconds,
    weeklyAwaySeconds: reports.weeklyAwaySeconds,
    includeAway: reports.includeAway,
    keyFrequency,
    timezoneLabel,
  }
}
