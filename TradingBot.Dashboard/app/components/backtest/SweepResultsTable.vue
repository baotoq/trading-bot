<template>
  <UCard>
    <template #header>
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
        Sweep Results ({{ results.length }} configurations)
      </h2>
    </template>

    <UTable
      :rows="results"
      :columns="columns"
      :virtualize="results.length > 100"
      :empty-state="{
        icon: 'i-lucide-table',
        label: 'No sweep results',
        description: 'Run a parameter sweep to see ranked configurations.'
      }"
      class="cursor-pointer"
      @select="onRowClick"
    >
      <template #rank-data="{ row }">
        <div class="text-center font-medium">
          {{ row.rank }}
        </div>
      </template>

      <template #efficiency-data="{ row }">
        <div class="font-bold text-primary">
          {{ formatDecimal(row.comparison.efficiencyRatio, 2) }}
        </div>
      </template>

      <template #return-data="{ row }">
        <div :class="row.smartDca.returnPercent >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'">
          {{ row.smartDca.returnPercent >= 0 ? '+' : '' }}{{ formatDecimal(row.smartDca.returnPercent, 2) }}%
        </div>
      </template>

      <template #btc-data="{ row }">
        <div>
          {{ formatDecimal(row.smartDca.totalBtc, 4) }}
        </div>
      </template>

      <template #costBasis-data="{ row }">
        <div>
          {{ formatUsd(row.smartDca.avgCostBasis) }}
        </div>
      </template>

      <template #drawdown-data="{ row }">
        <div>
          {{ formatDecimal(row.smartDca.maxDrawdown, 2) }}%
        </div>
      </template>

      <template #baseAmount-data="{ row }">
        <div>
          {{ formatUsd(row.config.baseDailyAmount) }}
        </div>
      </template>

      <template #overfit-data="{ row }">
        <div v-if="row.walkForward">
          <span
            v-if="row.walkForward.overfitWarning"
            class="text-xs px-2 py-1 rounded bg-amber-100 dark:bg-amber-900/20 text-amber-700 dark:text-amber-400"
          >
            Warning
          </span>
          <span
            v-else
            class="text-xs px-2 py-1 rounded bg-gray-100 dark:bg-gray-800 text-gray-500 dark:text-gray-400"
          >
            OK
          </span>
        </div>
        <div v-else class="text-gray-400">
          —
        </div>
      </template>
    </UTable>
  </UCard>
</template>

<script setup lang="ts">
import type { SweepResultEntry } from '~/types/backtest'

interface Props {
  results: SweepResultEntry[]
}

interface Emits {
  (e: 'selectConfig', row: SweepResultEntry): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Table columns with sorting
const columns = [
  {
    key: 'rank',
    label: 'Rank',
    sortable: true,
    class: 'w-16 text-center'
  },
  {
    key: 'efficiency',
    label: 'Efficiency Ratio',
    sortable: true,
    sort: (a: SweepResultEntry, b: SweepResultEntry) => b.comparison.efficiencyRatio - a.comparison.efficiencyRatio
  },
  {
    key: 'return',
    label: 'Return %',
    sortable: true,
    sort: (a: SweepResultEntry, b: SweepResultEntry) => b.smartDca.returnPercent - a.smartDca.returnPercent
  },
  {
    key: 'btc',
    label: 'Total BTC',
    sortable: true,
    sort: (a: SweepResultEntry, b: SweepResultEntry) => b.smartDca.totalBtc - a.smartDca.totalBtc
  },
  {
    key: 'costBasis',
    label: 'Avg Cost Basis',
    sortable: true,
    sort: (a: SweepResultEntry, b: SweepResultEntry) => a.smartDca.avgCostBasis - b.smartDca.avgCostBasis
  },
  {
    key: 'drawdown',
    label: 'Max Drawdown',
    sortable: true,
    sort: (a: SweepResultEntry, b: SweepResultEntry) => a.smartDca.maxDrawdown - b.smartDca.maxDrawdown
  },
  {
    key: 'baseAmount',
    label: 'Base Amount',
    sortable: true,
    sort: (a: SweepResultEntry, b: SweepResultEntry) => b.config.baseDailyAmount - a.config.baseDailyAmount
  },
  {
    key: 'overfit',
    label: 'Overfit',
    sortable: true,
    sort: (a: SweepResultEntry, b: SweepResultEntry) => {
      const aWarning = a.walkForward?.overfitWarning ? 1 : 0
      const bWarning = b.walkForward?.overfitWarning ? 1 : 0
      return aWarning - bWarning
    }
  }
]

// Format helpers
function formatDecimal(value: number, decimals: number): string {
  return value.toFixed(decimals)
}

function formatUsd(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0
  }).format(value)
}

function onRowClick(row: SweepResultEntry) {
  emit('selectConfig', row)
}
</script>

<style scoped>
:deep(tbody tr) {
  cursor: pointer;
}

:deep(tbody tr:hover) {
  @apply bg-gray-50 dark:bg-gray-800/50;
}
</style>
