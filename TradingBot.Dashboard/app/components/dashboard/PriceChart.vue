<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">BTC Price History</h2>

        <!-- Timeframe selector -->
        <div class="flex gap-2">
          <UButton
            v-for="tf in timeframes"
            :key="tf"
            size="sm"
            :variant="selectedTimeframe === tf ? 'solid' : 'soft'"
            @click="selectedTimeframe = tf"
          >
            {{ tf }}
          </UButton>
        </div>
      </div>
    </template>

    <div v-if="pending" class="h-[400px]">
      <USkeleton class="h-full w-full" />
    </div>

    <div v-else-if="!chartData || chartData.prices.length === 0" class="h-[400px] flex items-center justify-center">
      <p class="text-gray-500 dark:text-gray-400">
        No price data available. Historical data may need to be ingested.
      </p>
    </div>

    <ClientOnly v-else>
      <div class="h-[400px]">
        <Line :data="chartDataset" :options="chartOptions" />
      </div>
    </ClientOnly>
  </UCard>
</template>

<script setup lang="ts">
import { Line } from 'vue-chartjs'
import type { ChartTimeframe, PriceChartResponse } from '~/types/dashboard'
import type { ChartData, ChartOptions } from 'chart.js'

const timeframes: ChartTimeframe[] = ['7D', '1M', '3M', '6M', '1Y', 'All']
const selectedTimeframe = ref<ChartTimeframe>('1M')

const { data: chartData, pending } = useFetch<PriceChartResponse>(
  '/api/dashboard/chart',
  {
    query: { timeframe: selectedTimeframe },
    server: false,
    lazy: true,
    watch: [selectedTimeframe]
  }
)

const chartDataset = computed<ChartData<'line'>>(() => {
  if (!chartData.value) {
    return { labels: [], datasets: [] }
  }

  return {
    labels: chartData.value.prices.map(p => p.date),
    datasets: [
      {
        label: 'BTC Price',
        data: chartData.value.prices.map(p => p.price),
        borderColor: 'rgb(59, 130, 246)',
        backgroundColor: 'rgb(59, 130, 246)',
        tension: 0.1,
        pointRadius: 0,
        borderWidth: 2
      }
    ]
  }
})

const chartOptions = computed<ChartOptions<'line'>>(() => {
  if (!chartData.value) {
    return {}
  }

  const annotations: any = {}

  // Only add average cost basis line when data is available (null when no purchases)
  if (chartData.value.averageCostBasis !== null) {
    annotations.avgLine = {
      type: 'line',
      yMin: chartData.value.averageCostBasis,
      yMax: chartData.value.averageCostBasis,
      borderColor: 'rgba(239, 68, 68, 0.6)',
      borderWidth: 2,
      borderDash: [6, 6],
      label: {
        display: true,
        content: 'Avg Cost Basis',
        position: 'end',
        backgroundColor: 'rgba(239, 68, 68, 0.8)',
        color: 'white',
        font: {
          size: 10
        }
      }
    }
  }

  // Add purchase markers
  chartData.value.purchases.forEach((purchase, index) => {
    annotations[`purchase${index}`] = {
      type: 'label',
      xValue: purchase.date,
      yValue: purchase.price,
      content: ['B'],
      backgroundColor: 'rgb(34, 197, 94)',
      color: 'white',
      font: {
        size: 10,
        weight: 'bold'
      },
      borderRadius: 4,
      padding: {
        top: 2,
        bottom: 2,
        left: 4,
        right: 4
      }
    }
  })

  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: false
      },
      tooltip: {
        callbacks: {
          label: (context) => {
            return `$${context.parsed.y.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
          }
        }
      },
      annotation: {
        annotations
      }
    },
    scales: {
      y: {
        beginAtZero: false,
        ticks: {
          callback: (value) => `$${Number(value).toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`
        }
      }
    }
  }
})
</script>
