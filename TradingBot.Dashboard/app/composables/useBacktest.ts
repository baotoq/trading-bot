import type {
  DcaConfigResponse,
  BacktestRequest,
  BacktestResponse,
  SweepRequest,
  SweepResponse
} from '~/types/backtest'

export function useBacktest() {
  // Reactive state
  const config = ref<DcaConfigResponse | null>(null)
  const backtestResult = ref<BacktestResponse | null>(null)
  const sweepResult = ref<SweepResponse | null>(null)
  const isRunning = ref(false)
  const progress = ref(0)
  const error = ref<string | null>(null)

  // Fetch DCA config from backend
  async function fetchConfig() {
    try {
      const data = await $fetch<DcaConfigResponse>('/api/dashboard/config')
      config.value = data
      return data
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch config'
      throw err
    }
  }

  // Load config on composable init
  async function loadConfig() {
    await fetchConfig()
  }

  // Simulated progress bar helper
  function simulateProgress(targetPercent: number, intervalMs: number = 200) {
    const step = targetPercent / (2000 / intervalMs) // Reach target in ~2 seconds
    const interval = setInterval(() => {
      if (progress.value < targetPercent) {
        progress.value = Math.min(progress.value + step, targetPercent)
      } else {
        clearInterval(interval)
      }
    }, intervalMs)
    return interval
  }

  // Run single backtest
  async function runBacktest(request: BacktestRequest) {
    isRunning.value = true
    error.value = null
    progress.value = 0
    backtestResult.value = null

    const progressInterval = simulateProgress(90)

    try {
      const data = await $fetch<BacktestResponse>('/api/backtest/run', {
        method: 'POST',
        body: request
      })

      clearInterval(progressInterval)
      progress.value = 100
      backtestResult.value = data
      return data
    } catch (err: any) {
      clearInterval(progressInterval)
      error.value = err.message || 'Failed to run backtest'
      throw err
    } finally {
      isRunning.value = false
    }
  }

  // Run parameter sweep
  async function runSweep(request: SweepRequest) {
    isRunning.value = true
    error.value = null
    progress.value = 0
    sweepResult.value = null

    const progressInterval = simulateProgress(90)

    try {
      const data = await $fetch<SweepResponse>('/api/backtest/sweep', {
        method: 'POST',
        body: request
      })

      clearInterval(progressInterval)
      progress.value = 100
      sweepResult.value = data
      return data
    } catch (err: any) {
      clearInterval(progressInterval)
      error.value = err.message || 'Failed to run parameter sweep'
      throw err
    } finally {
      isRunning.value = false
    }
  }

  return {
    // State
    config,
    backtestResult,
    sweepResult,
    isRunning,
    progress,
    error,
    // Methods
    fetchConfig,
    loadConfig,
    runBacktest,
    runSweep
  }
}
