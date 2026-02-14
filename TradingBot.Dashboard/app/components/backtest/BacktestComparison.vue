<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
          Comparison ({{ comparedBacktests.length }}/3)
        </h2>
        <UButton
          v-if="comparedBacktests.length > 0"
          size="sm"
          variant="soft"
          color="red"
          icon="i-lucide-trash-2"
          @click="clearComparison"
        >
          Clear All
        </UButton>
      </div>
    </template>

    <!-- Empty state -->
    <div v-if="comparedBacktests.length === 0" class="text-center py-12">
      <p class="text-gray-500 dark:text-gray-400">
        Run backtests and add to comparison to see side-by-side results
      </p>
    </div>

    <!-- Comparison content -->
    <div v-else class="space-y-6">
      <!-- Overlaid Equity Curve Chart -->
      <div>
        <h3 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-4">
          Equity Curves
        </h3>

        <!-- Chart data not available message -->
        <div v-if="!hasAnyChartData" class="bg-gray-50 dark:bg-gray-800 rounded-lg p-8 text-center">
          <p class="text-gray-500 dark:text-gray-400">
            Re-run backtests to view comparison chart
          </p>
          <p class="text-sm text-gray-400 dark:text-gray-500 mt-2">
            Chart data is not persisted across page refresh
          </p>
        </div>

        <!-- Chart -->
        <ClientOnly v-else>
          <div class="h-[400px]">
            <Line :data="chartData" :options="chartOptions" />
          </div>
        </ClientOnly>
      </div>

      <!-- Metrics Comparison Table -->
      <div>
        <h3 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-4">
          Metrics Comparison
        </h3>

        <div class="overflow-x-auto">
          <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
            <thead>
              <tr class="bg-gray-50 dark:bg-gray-800">
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                  Metric
                </th>
                <th
                  v-for="entry in comparedBacktests"
                  :key="entry.id"
                  class="px-4 py-3 text-center text-xs font-medium uppercase tracking-wider"
                  :style="{ color: entry.color }"
                >
                  <div class="flex items-center justify-between gap-2">
                    <span class="flex-1">{{ entry.label }}</span>
                    <UButton
                      size="xs"
                      variant="ghost"
                      icon="i-lucide-x"
                      @click="removeFromComparison(entry.id)"
                      class="flex-shrink-0"
                    />
                  </div>
                </th>
              </tr>
            </thead>
            <tbody class="bg-white dark:bg-gray-900 divide-y divide-gray-200 dark:divide-gray-700">
              <!-- Total BTC -->
              <tr>
                <td class="px-4 py-3 text-sm font-medium text-gray-900 dark:text-white">
                  Total BTC
                </td>
                <td
                  v-for="entry in comparedBacktests"
                  :key="entry.id"
                  class="px-4 py-3 text-sm text-center"
                  :class="isBestValue('totalBtc', entry) ? 'font-bold text-green-600 dark:text-green-400' : 'text-gray-700 dark:text-gray-300'"
                >
                  {{ entry.metrics.totalBtc.toFixed(4) }}
                </td>
              </tr>

              <!-- Avg Cost Basis -->
              <tr>
                <td class="px-4 py-3 text-sm font-medium text-gray-900 dark:text-white">
                  Avg Cost Basis
                </td>
                <td
                  v-for="entry in comparedBacktests"
                  :key="entry.id"
                  class="px-4 py-3 text-sm text-center"
                  :class="isBestValue('avgCostBasis', entry) ? 'font-bold text-green-600 dark:text-green-400' : 'text-gray-700 dark:text-gray-300'"
                >
                  ${{ entry.metrics.avgCostBasis.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}
                </td>
              </tr>

              <!-- Return % -->
              <tr>
                <td class="px-4 py-3 text-sm font-medium text-gray-900 dark:text-white">
                  Return %
                </td>
                <td
                  v-for="entry in comparedBacktests"
                  :key="entry.id"
                  class="px-4 py-3 text-sm text-center"
                  :class="isBestValue('returnPercent', entry) ? 'font-bold text-green-600 dark:text-green-400' : 'text-gray-700 dark:text-gray-300'"
                >
                  {{ entry.metrics.returnPercent.toFixed(2) }}%
                </td>
              </tr>

              <!-- Efficiency Ratio -->
              <tr>
                <td class="px-4 py-3 text-sm font-medium text-gray-900 dark:text-white">
                  Efficiency Ratio
                </td>
                <td
                  v-for="entry in comparedBacktests"
                  :key="entry.id"
                  class="px-4 py-3 text-sm text-center"
                  :class="isBestValue('efficiencyRatio', entry) ? 'font-bold text-green-600 dark:text-green-400' : 'text-gray-700 dark:text-gray-300'"
                >
                  {{ entry.comparison.efficiencyRatio.toFixed(3) }}
                </td>
              </tr>

              <!-- Max Drawdown -->
              <tr>
                <td class="px-4 py-3 text-sm font-medium text-gray-900 dark:text-white">
                  Max Drawdown
                </td>
                <td
                  v-for="entry in comparedBacktests"
                  :key="entry.id"
                  class="px-4 py-3 text-sm text-center"
                  :class="isBestValue('maxDrawdown', entry) ? 'font-bold text-green-600 dark:text-green-400' : 'text-gray-700 dark:text-gray-300'"
                >
                  {{ entry.metrics.maxDrawdown.toFixed(2) }}%
                </td>
              </tr>

              <!-- Total Invested -->
              <tr>
                <td class="px-4 py-3 text-sm font-medium text-gray-900 dark:text-white">
                  Total Invested
                </td>
                <td
                  v-for="entry in comparedBacktests"
                  :key="entry.id"
                  class="px-4 py-3 text-sm text-center text-gray-700 dark:text-gray-300"
                >
                  ${{ entry.metrics.totalInvested.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Config Summary (Collapsible) -->
      <div>
        <h3 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-4">
          Configuration Details
        </h3>

        <div class="space-y-3">
          <UAccordion
            v-for="entry in comparedBacktests"
            :key="entry.id"
            :items="[{
              label: entry.label,
              slot: `config-${entry.id}`,
              defaultOpen: false
            }]"
          >
            <template #[`config-${entry.id}`]>
              <div class="grid grid-cols-2 gap-3 text-sm">
                <div>
                  <span class="text-gray-500 dark:text-gray-400">Base Amount:</span>
                  <span class="ml-2 text-gray-900 dark:text-white">${{ entry.config.baseDailyAmount }}</span>
                </div>
                <div>
                  <span class="text-gray-500 dark:text-gray-400">Lookback Days:</span>
                  <span class="ml-2 text-gray-900 dark:text-white">{{ entry.config.highLookbackDays }}</span>
                </div>
                <div>
                  <span class="text-gray-500 dark:text-gray-400">Bear MA Period:</span>
                  <span class="ml-2 text-gray-900 dark:text-white">{{ entry.config.bearMarketMaPeriod }}</span>
                </div>
                <div>
                  <span class="text-gray-500 dark:text-gray-400">Bear Boost:</span>
                  <span class="ml-2 text-gray-900 dark:text-white">{{ entry.config.bearBoostFactor }}x</span>
                </div>
                <div>
                  <span class="text-gray-500 dark:text-gray-400">Max Multiplier Cap:</span>
                  <span class="ml-2 text-gray-900 dark:text-white">{{ entry.config.maxMultiplierCap }}x</span>
                </div>
                <div class="col-span-2">
                  <span class="text-gray-500 dark:text-gray-400">Tiers:</span>
                  <span class="ml-2 text-gray-900 dark:text-white">
                    {{ entry.config.tiers.map(t => `${t.dropPercentage}%: ${t.multiplier}x`).join(', ') }}
                  </span>
                </div>
              </div>
            </template>
          </UAccordion>
        </div>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { Line } from 'vue-chartjs'
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend
} from 'chart.js'
import type { ChartData, ChartOptions } from 'chart.js'
import type { ComparisonEntrySummary } from '~/composables/useBacktestComparison'

ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend
)

const { comparedBacktests, removeFromComparison, clearComparison, fullEntries } = useBacktestComparison()

// Check if any entries have chart data
const hasAnyChartData = computed(() => {
  return fullEntries.value.some(entry => entry.purchaseLog && entry.purchaseLog.length > 0)
})

// Build chart data from entries with purchaseLog
const chartData = computed<ChartData<'line'>>(() => {
  const entriesWithData = fullEntries.value.filter(entry => entry.purchaseLog && entry.purchaseLog.length > 0)

  if (entriesWithData.length === 0) {
    return { labels: [], datasets: [] }
  }

  // Use first entry's dates as labels
  const labels = entriesWithData[0].purchaseLog!.map(p => p.date)

  // Create datasets for each entry
  const datasets = entriesWithData.map(entry => ({
    label: entry.label,
    data: entry.purchaseLog!.map(p => p.smartCumulativeUsd),
    borderColor: entry.color,
    backgroundColor: entry.color,
    yAxisID: 'y',
    tension: 0.1,
    pointRadius: 0,
    borderWidth: 2
  }))

  // Add BTC price as reference (from first entry)
  const priceData = entriesWithData[0].purchaseLog!.map(p => p.price)
  datasets.push({
    label: 'BTC Price',
    data: priceData,
    borderColor: 'rgba(251, 191, 36, 0.3)',
    backgroundColor: 'rgba(251, 191, 36, 0.3)',
    yAxisID: 'y1',
    tension: 0.1,
    pointRadius: 0,
    borderWidth: 2,
    borderDash: [5, 5]
  } as any)

  return {
    labels,
    datasets
  }
})

const chartOptions = computed<ChartOptions<'line'>>(() => {
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: {
      mode: 'index',
      intersect: false
    },
    plugins: {
      legend: {
        display: true,
        position: 'top'
      },
      tooltip: {
        callbacks: {
          label: (context) => {
            const label = context.dataset.label || ''
            const value = context.parsed.y

            if (label === 'BTC Price') {
              return `${label}: $${value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
            }

            return `${label}: $${value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
          }
        }
      }
    },
    scales: {
      y: {
        type: 'linear',
        display: true,
        position: 'left',
        title: {
          display: true,
          text: 'Portfolio Value (USD)'
        },
        ticks: {
          callback: (value) => {
            return `$${Number(value).toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`
          }
        }
      },
      y1: {
        type: 'linear',
        display: true,
        position: 'right',
        title: {
          display: true,
          text: 'BTC Price'
        },
        grid: {
          drawOnChartArea: false
        },
        ticks: {
          callback: (value) => `$${Number(value).toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`
        }
      }
    }
  }
})

/**
 * Determine if a value is the best in its category
 */
function isBestValue(metric: string, entry: ComparisonEntrySummary): boolean {
  const values = comparedBacktests.value.map(e => {
    switch (metric) {
      case 'totalBtc':
        return e.metrics.totalBtc
      case 'avgCostBasis':
        return e.metrics.avgCostBasis
      case 'returnPercent':
        return e.metrics.returnPercent
      case 'efficiencyRatio':
        return e.comparison.efficiencyRatio
      case 'maxDrawdown':
        return Math.abs(e.metrics.maxDrawdown) // Lower is better
      default:
        return 0
    }
  })

  const entryValue = metric === 'totalBtc' ? entry.metrics.totalBtc
    : metric === 'avgCostBasis' ? entry.metrics.avgCostBasis
    : metric === 'returnPercent' ? entry.metrics.returnPercent
    : metric === 'efficiencyRatio' ? entry.comparison.efficiencyRatio
    : metric === 'maxDrawdown' ? Math.abs(entry.metrics.maxDrawdown)
    : 0

  // For avgCostBasis and maxDrawdown, lower is better
  if (metric === 'avgCostBasis' || metric === 'maxDrawdown') {
    return entryValue === Math.min(...values)
  }

  // For others, higher is better
  return entryValue === Math.max(...values)
}
</script>
