<template>
  <UApp>
    <div class="min-h-screen bg-gray-50 dark:bg-gray-900">
      <!-- Header -->
      <header class="border-b border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-950">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div class="flex items-center justify-between">
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

            <!-- Y-axis toggle (shared across tabs) -->
            <div v-if="(backtestResult || selectedSweepDetail)" class="flex gap-2">
              <UButton
                size="sm"
                :variant="yAxisMode === 'usd' ? 'solid' : 'soft'"
                @click="yAxisMode = 'usd'"
              >
                USD
              </UButton>
              <UButton
                size="sm"
                :variant="yAxisMode === 'btc' ? 'solid' : 'soft'"
                @click="yAxisMode = 'btc'"
              >
                BTC
              </UButton>
            </div>
          </div>
        </div>
      </header>

      <!-- Main content -->
      <main class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <UTabs
          v-model="activeTab"
          :items="tabs"
        >
          <!-- Single Backtest Tab -->
          <template #single>
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-8 mt-6">
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
                  <!-- Add to Compare Button -->
                  <div class="flex justify-end">
                    <UButton
                      variant="soft"
                      icon="i-lucide-plus"
                      :disabled="!canAdd"
                      @click="onAddToComparison(backtestResult)"
                    >
                      {{ addButtonText }}
                    </UButton>
                  </div>

                  <!-- Metrics Cards -->
                  <BacktestMetrics :result="backtestResult.result" />

                  <!-- Equity Curve Chart -->
                  <BacktestChart :result="backtestResult.result" :y-axis-mode="yAxisMode" />
                </template>
              </div>
            </div>
          </template>

          <!-- Parameter Sweep Tab -->
          <template #sweep>
            <div class="space-y-6 mt-6">
              <!-- Sweep Form -->
              <SweepForm
                :config="config"
                @sweep-complete="onSweepComplete"
              />

              <!-- Sweep Results Table -->
              <div v-if="sweepResults">
                <SweepResultsTable
                  :results="sweepResults.results"
                  @select-config="onSelectConfig"
                />
              </div>

              <!-- Selected Configuration Detail -->
              <div v-if="selectedSweepDetail" class="space-y-6">
                <div class="border-t border-gray-200 dark:border-gray-800 pt-6">
                  <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                    Configuration Detail (Rank #{{ selectedSweepEntry?.rank }})
                  </h3>

                  <!-- Loading state -->
                  <div v-if="loadingSweepDetail" class="flex items-center justify-center h-32">
                    <div class="text-center">
                      <div class="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
                      <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">Loading detail...</p>
                    </div>
                  </div>

                  <!-- Detail content -->
                  <template v-else>
                    <!-- Add to Compare Button -->
                    <div class="flex justify-end mb-4">
                      <UButton
                        variant="soft"
                        icon="i-lucide-plus"
                        :disabled="!canAdd"
                        @click="onAddToComparisonFromSweep()"
                      >
                        {{ addButtonText }}
                      </UButton>
                    </div>

                    <BacktestMetrics :result="selectedSweepDetail" />
                    <BacktestChart :result="selectedSweepDetail" :y-axis-mode="yAxisMode" class="mt-6" />
                  </template>
                </div>
              </div>
            </div>
          </template>
        </UTabs>

        <!-- Comparison Panel (Below tabs) -->
        <div v-if="comparedBacktests.length > 0" class="mt-8 pt-8 border-t border-gray-200 dark:border-gray-800">
          <BacktestComparison />
        </div>
      </main>
    </div>
  </UApp>
</template>

<script setup lang="ts">
import type {
  BacktestResponse,
  BacktestResult,
  BacktestRequest,
  SweepResponse,
  SweepResultEntry,
  SweepResultDetailEntry
} from '~/types/backtest'

const { config, loadConfig } = useBacktest()
const { addToComparison, comparedBacktests, canAdd } = useBacktestComparison()
const toast = useToast()

// Tab state
const activeTab = ref(0)
const tabs = [
  {
    slot: 'single',
    label: 'Single Backtest',
    icon: 'i-lucide-play'
  },
  {
    slot: 'sweep',
    label: 'Parameter Sweep',
    icon: 'i-lucide-layers'
  }
]

// Single backtest state
const backtestResult = ref<BacktestResponse | null>(null)

// Sweep state
const sweepResults = ref<SweepResponse | null>(null)
const selectedSweepEntry = ref<SweepResultEntry | null>(null)
const selectedSweepDetail = ref<BacktestResult | null>(null)
const loadingSweepDetail = ref(false)

// Shared chart state
const yAxisMode = ref<'usd' | 'btc'>('usd')

// Comparison button state
const addButtonText = computed(() => {
  if (!canAdd.value) {
    return 'Max 3 Comparisons'
  }
  return 'Add to Compare'
})

// Load config on mount
onMounted(() => {
  loadConfig()
})

function onBacktestComplete(result: BacktestResponse) {
  backtestResult.value = result
}

function onSweepComplete(result: SweepResponse) {
  sweepResults.value = result
  selectedSweepEntry.value = null
  selectedSweepDetail.value = null
}

function onAddToComparison(result: BacktestResponse) {
  const label = `Base $${result.config.baseDailyAmount}, ${result.config.highLookbackDays}d lookback`
  const success = addToComparison(result, label)

  if (success) {
    toast.add({
      title: 'Added to comparison',
      description: label,
      color: 'green',
      timeout: 2000
    })
  } else {
    if (!canAdd.value) {
      toast.add({
        title: 'Maximum 3 comparisons',
        description: 'Remove an entry to add a new one',
        color: 'orange',
        timeout: 3000
      })
    } else {
      toast.add({
        title: 'Already in comparison',
        description: 'This configuration is already added',
        color: 'orange',
        timeout: 3000
      })
    }
  }
}

function onAddToComparisonFromSweep() {
  if (!selectedSweepEntry.value || !selectedSweepDetail.value || !sweepResults.value) {
    return
  }

  // Build a BacktestResponse-like object for sweep detail
  const sweepAsBacktestResponse: BacktestResponse = {
    config: selectedSweepEntry.value.config,
    startDate: sweepResults.value.startDate,
    endDate: sweepResults.value.endDate,
    totalDays: sweepResults.value.totalDays,
    result: selectedSweepDetail.value
  }

  const label = `Sweep #${selectedSweepEntry.value.rank}: Base $${selectedSweepEntry.value.config.baseDailyAmount}`
  const success = addToComparison(sweepAsBacktestResponse, label)

  if (success) {
    toast.add({
      title: 'Added to comparison',
      description: label,
      color: 'green',
      timeout: 2000
    })
  } else {
    if (!canAdd.value) {
      toast.add({
        title: 'Maximum 3 comparisons',
        description: 'Remove an entry to add a new one',
        color: 'orange',
        timeout: 3000
      })
    } else {
      toast.add({
        title: 'Already in comparison',
        description: 'This configuration is already added',
        color: 'orange',
        timeout: 3000
      })
    }
  }
}

async function onSelectConfig(entry: SweepResultEntry) {
  selectedSweepEntry.value = entry
  selectedSweepDetail.value = null

  // Check if this is a top result (has full purchaseLog)
  const topResult = sweepResults.value?.topResults.find(r => r.rank === entry.rank) as SweepResultDetailEntry | undefined

  if (topResult && topResult.purchaseLog) {
    // Top result — load instantly from topResults
    selectedSweepDetail.value = {
      smartDca: topResult.smartDca,
      fixedDcaSameBase: topResult.fixedDcaSameBase,
      fixedDcaMatchTotal: entry.fixedDcaSameBase, // Use from entry (same structure)
      comparison: topResult.comparison,
      tierBreakdown: topResult.tierBreakdown,
      purchaseLog: topResult.purchaseLog
    }
  } else {
    // Not in top results — fetch full backtest
    loadingSweepDetail.value = true

    try {
      // Build backtest request from selected config
      const request: BacktestRequest = {
        startDate: sweepResults.value!.startDate,
        endDate: sweepResults.value!.endDate,
        baseDailyAmount: entry.config.baseDailyAmount,
        highLookbackDays: entry.config.highLookbackDays,
        bearMarketMaPeriod: entry.config.bearMarketMaPeriod,
        bearBoostFactor: entry.config.bearBoostFactor,
        maxMultiplierCap: entry.config.maxMultiplierCap,
        tiers: entry.config.tiers
      }

      const response = await $fetch<BacktestResponse>('/api/backtest/run', {
        method: 'POST',
        body: request
      })

      selectedSweepDetail.value = response.result
    } catch (err: any) {
      console.error('Failed to fetch sweep detail:', err)
    } finally {
      loadingSweepDetail.value = false
    }
  }
}
</script>
