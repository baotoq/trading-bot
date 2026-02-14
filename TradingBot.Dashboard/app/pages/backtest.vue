<template>
  <UApp>
    <div class="min-h-screen bg-gray-50 dark:bg-gray-900">
      <!-- Header -->
      <header class="border-b border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-950">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div class="flex items-center gap-4">
            <UButton
              icon="i-lucide-arrow-left"
              variant="soft"
              to="/"
            />
            <h1 class="text-2xl font-bold text-gray-900 dark:text-white">
              Backtest
            </h1>
          </div>
        </div>
      </header>

      <!-- Main content -->
      <main class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
          <!-- Form Section (Left - 1/3) -->
          <div class="lg:col-span-1">
            <BacktestForm
              :config="config"
              @backtest-complete="onBacktestComplete"
            />
          </div>

          <!-- Results Section (Right - 2/3) -->
          <div class="lg:col-span-2 space-y-6">
            <div v-if="!backtestResult" class="flex items-center justify-center h-64">
              <p class="text-gray-500 dark:text-gray-400 text-center">
                Configure parameters and run a backtest to see results
              </p>
            </div>

            <template v-else>
              <!-- Metrics Cards -->
              <BacktestMetrics :result="backtestResult.result" />

              <!-- Equity Curve Chart -->
              <BacktestChart :result="backtestResult.result" />
            </template>
          </div>
        </div>
      </main>
    </div>
  </UApp>
</template>

<script setup lang="ts">
import type { BacktestResponse } from '~/types/backtest'

const { config, loadConfig } = useBacktest()
const backtestResult = ref<BacktestResponse | null>(null)

// Load config on mount
onMounted(() => {
  loadConfig()
})

function onBacktestComplete(result: BacktestResponse) {
  backtestResult.value = result
}
</script>
