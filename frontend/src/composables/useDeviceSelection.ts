import { ref, computed, onMounted } from 'vue'
import type { DeviceInfoResponse } from '../api/index'
import { fetchPublicDevices, fetchPublicDeviceStatus } from '../api/index'
import { useAsyncData } from './useAsyncData'

function todayStr(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/**
 * 设备选择枢纽：当前看哪台设备、哪一天。其余数据域都以此为输入。
 * onMounted 时拉取设备列表并自动选中第一台在线设备。
 */
export function useDeviceSelection(username: string) {
  const devicesData = useAsyncData<DeviceInfoResponse[]>(() => fetchPublicDevices(username), [])
  const devices = devicesData.data
  const selectedDevice = ref(0)
  const selectedDate = ref(todayStr())

  const selectedDeviceName = computed(() => {
    const d = devices.value.find(d => d.id === selectedDevice.value)
    return d?.name ?? ''
  })

  const isToday = computed(() => selectedDate.value === todayStr())

  /** 拉设备列表并自动选中第一台在线设备。挂载时跑一次,错误重试时可重跑。 */
  async function reload() {
    await devicesData.run()

    if (devices.value.length > 0) {
      let picked = devices.value[0].id!
      for (const d of devices.value) {
        // 单台探测失败不该中断整轮在线设备选择；失败当作"不在线"跳过。
        try {
          const s = await fetchPublicDeviceStatus(username, d.id!)
          if (s?.isOnline) { picked = d.id!; break }
        } catch { /* 跳过这台 */ }
      }
      selectedDevice.value = picked
    }
  }

  onMounted(reload)

  return {
    devices,
    error: devicesData.error,
    reload,
    selectedDevice,
    selectedDate,
    selectedDeviceName,
    isToday,
  }
}
