import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import type { AppInfoResponse, AppUsageResponse, AppSummary, DeviceInfoResponse, DeviceStatusResponse, DailyReportResponse, WeeklyReportResponse } from '../api/index'
import { fetchPublicDevices, fetchPublicApps, fetchPublicDeviceStatus, fetchPublicUsage, fetchPublicDailyReport, fetchPublicWeeklyReport, fetchPublicKeyFrequency, getTimezoneLabel } from '../api/index'
import { AWAY_APP } from '../appLabels'

function todayStr(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export function formatDuration(sec: number): string {
  const h = Math.floor(sec / 3600)
  const m = Math.floor((sec % 3600) / 60)
  if (h > 0) return `${h}h ${m}m`
  if (m > 0) return `${m}m`
  return '< 1m'
}

export function useHeartbeat(username: string) {
  const devices = ref<DeviceInfoResponse[]>([])
  const apps = ref<AppInfoResponse[]>([])
  const selectedDevice = ref(0)
  const selectedDate = ref(todayStr())
  const usageData = ref<AppUsageResponse[]>([])
  const deviceStatus = ref<DeviceStatusResponse | null>(null)
  const dailyReport = ref<DailyReportResponse | null>(null)
  const weeklyReport = ref<WeeklyReportResponse | null>(null)
  const keyFrequency = ref<{ code: number; count: number }[]>([])
  const loading = ref(false)

  // 是否把"离开"时间（息屏/睡眠/锁屏）计入统计。默认不计入。详见 ADR-014。
  const includeAway = ref(false)

  const appNameMap = computed(() => {
    const map = new Map<number, string>()
    for (const app of apps.value) map.set(app.id!, app.name!)
    return map
  })

  const selectedDeviceName = computed(() => {
    const d = devices.value.find(d => d.id === selectedDevice.value)
    return d?.name ?? ''
  })

  const isToday = computed(() => selectedDate.value === todayStr())
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

  // 应用排行：始终排除 away（排行只展示真实应用）
  const appSummaries = computed<AppSummary[]>(() => {
    if (!dailyReport.value?.apps) return []
    return dailyReport.value.apps
      .filter(a => a.appName !== AWAY_APP)
      .map(a => ({
        appId: a.appId!,
        appName: a.appName ?? `App ${a.appId}`,
        totalSeconds: a.durationSeconds!,
      }))
      .sort((a, b) => b.totalSeconds - a.totalSeconds)
  })

  // 当日 away 总秒数（用于开关打开时附加显示）
  const awaySeconds = computed(() =>
    dailyReport.value?.apps?.find(a => a.appName === AWAY_APP)?.durationSeconds ?? 0
  )

  // 主时长 = 非 away 之和；开关打开时再加上 away。前端求和，服务端不再下发 total。
  const usageSeconds = computed(() => appSummaries.value.reduce((s, a) => s + a.totalSeconds, 0))
  const totalSeconds = computed(() =>
    usageSeconds.value + (includeAway.value ? awaySeconds.value : 0)
  )
  const maxSeconds = computed(() => appSummaries.value[0]?.totalSeconds ?? 1)

  const activeHours = computed(() => {
    const hours = new Set<number>()
    for (const u of usageData.value) {
      if (u.appName === AWAY_APP) continue // away 不算活跃
      const s = u.startTime!.getHours()
      const e = u.endTime!.getHours()
      if (e >= s) {
        for (let h = s; h <= e; h++) hours.add(h)
      } else {
        for (let h = s; h < 24; h++) hours.add(h)
      }
    }
    return hours
  })

  // 周报：同样排除 away，前端求和
  const weeklyAppSummaries = computed<AppSummary[]>(() => {
    if (!weeklyReport.value?.apps) return []
    return weeklyReport.value.apps
      .filter(a => a.appName !== AWAY_APP)
      .map(a => ({
        appId: a.appId!,
        appName: a.appName ?? `App ${a.appId}`,
        totalSeconds: a.durationSeconds!,
      }))
      .sort((a, b) => b.totalSeconds - a.totalSeconds)
  })

  const weeklyAwaySeconds = computed(() =>
    weeklyReport.value?.apps?.find(a => a.appName === AWAY_APP)?.durationSeconds ?? 0
  )
  const weeklyUsageSeconds = computed(() => weeklyAppSummaries.value.reduce((s, a) => s + a.totalSeconds, 0))
  const weeklyTotalSeconds = computed(() =>
    weeklyUsageSeconds.value + (includeAway.value ? weeklyAwaySeconds.value : 0)
  )

  const timezoneLabel = getTimezoneLabel()

  async function loadUsage() {
    if (!selectedDevice.value) return
    const dateObj = new Date(selectedDate.value + 'T00:00:00')
    const start = dateObj.toISOString()
    const end = new Date(dateObj.getTime() + 86400000).toISOString()
    usageData.value = await fetchPublicUsage(username, { deviceId: selectedDevice.value, start, end })
  }

  async function loadStatus() {
    if (!selectedDevice.value) return
    deviceStatus.value = await fetchPublicDeviceStatus(username, selectedDevice.value)
  }

  async function loadDailyReport() {
    if (!selectedDevice.value) return
    dailyReport.value = await fetchPublicDailyReport(username, { deviceId: selectedDevice.value, date: selectedDate.value })
  }

  async function loadWeeklyReport() {
    if (!selectedDevice.value) return
    weeklyReport.value = await fetchPublicWeeklyReport(username, { deviceId: selectedDevice.value, date: selectedDate.value })
  }

  async function loadKeyFrequency() {
    if (!selectedDevice.value) return
    const dateObj = new Date(selectedDate.value + 'T00:00:00')
    const start = dateObj.toISOString()
    const end = new Date(dateObj.getTime() + 86400000).toISOString()
    const res = await fetchPublicKeyFrequency(username, { deviceId: selectedDevice.value, start, end })
    keyFrequency.value = res.keys
  }

  async function refresh() {
    loading.value = true
    try {
      await Promise.all([loadUsage(), loadStatus(), loadDailyReport(), loadWeeklyReport(), loadKeyFrequency()])
    } finally {
      loading.value = false
    }
  }

  let statusTimer: ReturnType<typeof setInterval>
  let usageTimer: ReturnType<typeof setInterval>

  onMounted(async () => {
    const [deviceList, appList] = await Promise.all([fetchPublicDevices(username), fetchPublicApps(username)])
    devices.value = deviceList
    apps.value = appList

    if (devices.value.length > 0) {
      let picked = devices.value[0].id!
      for (const d of devices.value) {
        const s = await fetchPublicDeviceStatus(username, d.id!)
        if (s?.isOnline) { picked = d.id!; break }
      }
      selectedDevice.value = picked
    }

    statusTimer = setInterval(() => {
      if (isToday.value) loadStatus()
    }, 5_000)

    usageTimer = setInterval(() => {
      if (isToday.value) {
        // loadUsage()
        loadDailyReport()
        loadWeeklyReport()
        loadKeyFrequency()
      }
    }, 30_000)
  })

  onUnmounted(() => {
    clearInterval(statusTimer)
    clearInterval(usageTimer)
  })

  watch([selectedDevice, selectedDate], () => refresh())

  return {
    devices,
    selectedDevice,
    selectedDeviceName,
    selectedDate,
    usageData,
    appNameMap,
    loading,
    isToday,
    isAlive,
    currentApp,
    currentAppId,
    lastSeenStr,
    appSummaries,
    totalSeconds,
    usageSeconds,
    awaySeconds,
    maxSeconds,
    activeHours,
    weeklyAppSummaries,
    weeklyTotalSeconds,
    weeklyAwaySeconds,
    includeAway,
    keyFrequency,
    timezoneLabel,
  }
}
