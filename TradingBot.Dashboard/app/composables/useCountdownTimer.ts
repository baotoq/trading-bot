import { useIntervalFn } from '@vueuse/core'

export function useCountdownTimer(targetTime: Ref<string | null>) {
  const remaining = ref('N/A')

  const updateCountdown = () => {
    if (!targetTime.value) {
      remaining.value = 'N/A'
      return
    }

    const target = new Date(targetTime.value).getTime()
    const now = Date.now()
    const diffSeconds = Math.floor((target - now) / 1000)

    if (diffSeconds <= 0) {
      remaining.value = 'Now'
      return
    }

    const hours = Math.floor(diffSeconds / 3600)
    const minutes = Math.floor((diffSeconds % 3600) / 60)
    const seconds = diffSeconds % 60

    remaining.value = `${hours}h ${minutes}m ${seconds}s`
  }

  // Update every second
  const { pause } = useIntervalFn(updateCountdown, 1000, { immediate: true })

  // Clean up on unmount
  onUnmounted(() => {
    pause()
  })

  return {
    remaining
  }
}
