<template>
  <UApp>
    <div class="min-h-screen bg-gray-50 dark:bg-gray-900">
      <!-- Header -->
      <header class="border-b border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-950">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div class="flex items-center justify-between">
            <h1 class="text-2xl font-bold text-gray-900 dark:text-white">
              BTC Smart DCA Dashboard
            </h1>
            <div class="flex items-center gap-4">
              <!-- Backtest link -->
              <UButton
                icon="i-lucide-bar-chart-2"
                variant="soft"
                to="/backtest"
              >
                Backtest
              </UButton>
              <!-- Connection status dot -->
              <div class="flex items-center gap-2">
                <span class="text-sm text-gray-500 dark:text-gray-400">
                  {{ connectionLabel }}
                </span>
                <span
                  class="w-2.5 h-2.5 rounded-full"
                  :class="connectionDotClass"
                />
              </div>
            </div>
          </div>
        </div>
      </header>

      <!-- Main content -->
      <main class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Tabs -->
        <UTabs :items="tabs">
          <template #dashboard>
            <div class="space-y-8 pt-6">
              <!-- Section 1: Portfolio Stats -->
              <DashboardPortfolioStats
                :portfolio="portfolio"
                :pending="portfolioPending"
              />

              <!-- Section 2: Price Chart -->
              <DashboardPriceChart />

              <!-- Section 3: Live Status -->
              <DashboardLiveStatus
                :status="status"
                :pending="statusPending"
              />

              <!-- Section 4: Purchase History -->
              <DashboardPurchaseHistory />
            </div>
          </template>

          <template #config>
            <div class="pt-6">
              <ConfigPanel />
            </div>
          </template>
        </UTabs>
      </main>
    </div>
  </UApp>
</template>

<script setup lang="ts">
const { portfolio, status, portfolioPending, statusPending, portfolioError, statusError } = useDashboard()

// Tab items
const tabs = [
  {
    key: 'dashboard',
    label: 'Dashboard',
    slot: 'dashboard'
  },
  {
    key: 'config',
    label: 'Configuration',
    slot: 'config'
  }
]

// Connection status derived from data fetch state
const connectionState = computed(() => {
  if (portfolioError.value || statusError.value) return 'disconnected'
  if (portfolioPending.value && !portfolio.value) return 'connecting'
  return 'connected'
})

const connectionDotClass = computed(() => ({
  'bg-green-500': connectionState.value === 'connected',
  'bg-yellow-500 animate-pulse': connectionState.value === 'connecting',
  'bg-red-500': connectionState.value === 'disconnected'
}))

const connectionLabel = computed(() => ({
  connected: 'Connected',
  connecting: 'Connecting...',
  disconnected: 'Disconnected'
}[connectionState.value]))
</script>
