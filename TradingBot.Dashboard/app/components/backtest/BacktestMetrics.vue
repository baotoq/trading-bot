<template>
  <div v-if="result" class="space-y-4">
    <!-- Hero Metric: Efficiency Ratio -->
    <UCard>
      <div class="text-center py-4">
        <p class="text-sm font-medium text-gray-600 dark:text-gray-400 mb-2">
          Efficiency Ratio
        </p>
        <p class="text-5xl font-bold text-primary mb-2">
          {{ result.comparison.efficiencyRatio.toFixed(2) }}x
        </p>
        <p class="text-sm text-gray-500 dark:text-gray-400">
          Smart DCA vs Fixed DCA efficiency
        </p>
      </div>
    </UCard>

    <!-- Main KPI Cards -->
    <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
      <!-- Total BTC -->
      <UCard>
        <div class="space-y-2">
          <p class="text-sm font-medium text-gray-600 dark:text-gray-400">
            Total BTC
          </p>
          <p class="text-2xl font-bold text-gray-900 dark:text-white">
            {{ result.smartDca.totalBtc.toFixed(6) }}
          </p>
          <p class="text-xs text-gray-500 dark:text-gray-400">
            vs Fixed: {{ result.fixedDcaSameBase.totalBtc.toFixed(6) }} BTC
          </p>
        </div>
      </UCard>

      <!-- Avg Cost Basis -->
      <UCard>
        <div class="space-y-2">
          <p class="text-sm font-medium text-gray-600 dark:text-gray-400">
            Avg Cost Basis
          </p>
          <p class="text-2xl font-bold text-gray-900 dark:text-white">
            ${{ result.smartDca.avgCostBasis.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}
          </p>
          <p class="text-xs" :class="costBasisDeltaClass">
            {{ costBasisDeltaText }}
          </p>
        </div>
      </UCard>

      <!-- Max Drawdown -->
      <UCard>
        <div class="space-y-2">
          <p class="text-sm font-medium text-gray-600 dark:text-gray-400">
            Max Drawdown
          </p>
          <p class="text-2xl font-bold" :class="drawdownClass">
            {{ (result.smartDca.maxDrawdown * 100).toFixed(2) }}%
          </p>
          <p class="text-xs text-gray-500 dark:text-gray-400">
            Peak to trough decline
          </p>
        </div>
      </UCard>
    </div>

    <!-- Additional Metrics -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
      <!-- Return % -->
      <UCard>
        <div class="space-y-1">
          <p class="text-xs font-medium text-gray-600 dark:text-gray-400">
            Return %
          </p>
          <p class="text-lg font-bold" :class="returnClass">
            {{ (result.smartDca.returnPercent * 100).toFixed(2) }}%
          </p>
        </div>
      </UCard>

      <!-- Total Invested -->
      <UCard>
        <div class="space-y-1">
          <p class="text-xs font-medium text-gray-600 dark:text-gray-400">
            Total Invested
          </p>
          <p class="text-lg font-bold text-gray-900 dark:text-white">
            ${{ result.smartDca.totalInvested.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}
          </p>
        </div>
      </UCard>

      <!-- Portfolio Value -->
      <UCard>
        <div class="space-y-1">
          <p class="text-xs font-medium text-gray-600 dark:text-gray-400">
            Portfolio Value
          </p>
          <p class="text-lg font-bold text-gray-900 dark:text-white">
            ${{ result.smartDca.portfolioValue.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}
          </p>
        </div>
      </UCard>

      <!-- Extra BTC % -->
      <UCard>
        <div class="space-y-1">
          <p class="text-xs font-medium text-gray-600 dark:text-gray-400">
            Extra BTC %
          </p>
          <p class="text-lg font-bold text-green-600 dark:text-green-400">
            +{{ (result.comparison.extraBtcPercentSameBase * 100).toFixed(2) }}%
          </p>
        </div>
      </UCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { BacktestResult } from '~/types/backtest'

interface Props {
  result: BacktestResult | null
}

const props = defineProps<Props>()

const costBasisDeltaText = computed(() => {
  if (!props.result) return ''

  const delta = props.result.comparison.costBasisDeltaSameBase
  const sign = delta > 0 ? '+' : ''
  return `${sign}$${delta.toFixed(2)} vs Fixed`
})

const costBasisDeltaClass = computed(() => {
  if (!props.result) return ''

  const delta = props.result.comparison.costBasisDeltaSameBase
  if (delta < 0) return 'text-green-600 dark:text-green-400'
  if (delta > 0) return 'text-red-600 dark:text-red-400'
  return 'text-gray-500 dark:text-gray-400'
})

const drawdownClass = computed(() => {
  if (!props.result) return 'text-gray-900 dark:text-white'

  const drawdown = Math.abs(props.result.smartDca.maxDrawdown)
  if (drawdown > 0.2) return 'text-red-600 dark:text-red-400'
  if (drawdown > 0.1) return 'text-amber-600 dark:text-amber-400'
  return 'text-green-600 dark:text-green-400'
})

const returnClass = computed(() => {
  if (!props.result) return 'text-gray-900 dark:text-white'

  const returnPercent = props.result.smartDca.returnPercent
  if (returnPercent > 0) return 'text-green-600 dark:text-green-400'
  if (returnPercent < 0) return 'text-red-600 dark:text-red-400'
  return 'text-gray-900 dark:text-white'
})
</script>
