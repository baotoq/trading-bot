import { useIntervalFn } from '@vueuse/core'
import type { PortfolioResponse, LiveStatusResponse } from '~/types/dashboard'

export function useDashboard() {
  // Fetch portfolio data
  const { data: portfolio, pending: portfolioPending, error: portfolioError, refresh: refreshPortfolio } = useFetch<PortfolioResponse>(
    '/api/dashboard/portfolio',
    { lazy: true, server: false }
  )

  // Fetch status data
  const { data: status, pending: statusPending, error: statusError, refresh: refreshStatus } = useFetch<LiveStatusResponse>(
    '/api/dashboard/status',
    { lazy: true, server: false }
  )

  // Refresh all data
  const refreshAll = async () => {
    await Promise.all([
      refreshPortfolio(),
      refreshStatus()
    ])
  }

  // Set up 10-second polling
  const { pause } = useIntervalFn(refreshAll, 10000, { immediate: true })

  // Clean up on unmount
  onUnmounted(() => {
    pause()
  })

  return {
    portfolio,
    status,
    portfolioPending,
    statusPending,
    portfolioError,
    statusError,
    refreshAll
  }
}
