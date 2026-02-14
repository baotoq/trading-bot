<template>
  <UCard>
    <template #header>
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Equity Curve</h2>
    </template>

    <ClientOnly>
      <div class="h-[500px]">
        <Line :data="chartData" :options="chartOptions" />
      </div>
    </ClientOnly>
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
import annotationPlugin from 'chartjs-plugin-annotation'
import type { ChartData, ChartOptions } from 'chart.js'
import type { BacktestResult } from '~/types/backtest'

ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  annotationPlugin
)

interface Props {
  result: BacktestResult | null
  yAxisMode: 'usd' | 'btc'
}

const props = defineProps<Props>()

// Tier color mapping
const tierColors = {
  'Base': 'rgb(156, 163, 175)',
  'Tier 1': 'rgb(34, 197, 94)',
  'Tier 2': 'rgb(59, 130, 246)',
  'Tier 3': 'rgb(168, 85, 247)',
  'Bear Boost': 'rgb(239, 68, 68)'
}

function getTierColor(tierName: string): string {
  return tierColors[tierName as keyof typeof tierColors] || tierColors['Base']
}

const chartData = computed<ChartData<'line'>>(() => {
  if (!props.result) {
    return { labels: [], datasets: [] }
  }

  const labels = props.result.purchaseLog.map(p => p.date)

  const smartData = props.result.purchaseLog.map(p =>
    props.yAxisMode === 'usd' ? p.smartCumulativeUsd : p.smartCumulativeBtc
  )

  const fixedData = props.result.purchaseLog.map(p =>
    props.yAxisMode === 'usd' ? p.fixedSameBaseCumulativeUsd : p.fixedSameBaseCumulativeBtc
  )

  const priceData = props.result.purchaseLog.map(p => p.price)

  return {
    labels,
    datasets: [
      {
        label: 'Smart DCA',
        data: smartData,
        borderColor: 'rgb(59, 130, 246)',
        backgroundColor: 'rgb(59, 130, 246)',
        yAxisID: 'y',
        tension: 0.1,
        pointRadius: 0,
        borderWidth: 2
      },
      {
        label: 'Fixed DCA',
        data: fixedData,
        borderColor: 'rgb(156, 163, 175)',
        backgroundColor: 'rgb(156, 163, 175)',
        yAxisID: 'y',
        tension: 0.1,
        pointRadius: 0,
        borderWidth: 2
      },
      {
        label: 'BTC Price',
        data: priceData,
        borderColor: 'rgba(251, 191, 36, 0.3)',
        backgroundColor: 'rgba(251, 191, 36, 0.3)',
        yAxisID: 'y1',
        tension: 0.1,
        pointRadius: 0,
        borderWidth: 2,
        borderDash: [5, 5]
      }
    ]
  }
})

const chartOptions = computed<ChartOptions<'line'>>(() => {
  if (!props.result) {
    return {}
  }

  // Build purchase markers
  const annotations: any = {}

  // Filter to only show multiplied purchases (smartMultiplier > 1)
  const multipliedPurchases = props.result.purchaseLog.filter(p => p.smartMultiplier > 1)

  multipliedPurchases.forEach((purchase, index) => {
    const yValue = props.yAxisMode === 'usd'
      ? purchase.smartCumulativeUsd
      : purchase.smartCumulativeBtc

    const fontSize = Math.min(8 + purchase.smartMultiplier * 2, 16)

    annotations[`purchase${index}`] = {
      type: 'label',
      xValue: purchase.date,
      yValue: yValue,
      yAxisID: 'y',
      content: ['●'],
      backgroundColor: getTierColor(purchase.smartTier),
      borderRadius: 50,
      font: {
        size: fontSize
      },
      padding: 0
    }
  })

  const yAxisLabel = props.yAxisMode === 'usd' ? 'Portfolio Value' : 'BTC Accumulated'

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

            if (context.datasetIndex === 2) {
              // BTC Price
              return `${label}: $${value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
            }

            if (props.yAxisMode === 'usd') {
              return `${label}: $${value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
            } else {
              return `${label}: ${value.toFixed(4)} BTC`
            }
          }
        }
      },
      annotation: {
        annotations
      }
    },
    scales: {
      y: {
        type: 'linear',
        display: true,
        position: 'left',
        title: {
          display: true,
          text: yAxisLabel
        },
        ticks: {
          callback: (value) => {
            if (props.yAxisMode === 'usd') {
              return `$${Number(value).toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`
            } else {
              return Number(value).toFixed(4)
            }
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
</script>
