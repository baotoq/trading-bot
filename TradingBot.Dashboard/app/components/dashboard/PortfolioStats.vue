<template>
  <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-4">
    <!-- Total BTC -->
    <DashboardStatCard
      title="Total BTC"
      :value="pending ? undefined : totalBtc"
    />

    <!-- Total Cost -->
    <DashboardStatCard
      title="Total Cost"
      :value="pending ? undefined : totalCost"
    />

    <!-- Average Cost Basis -->
    <DashboardStatCard
      title="Avg Cost Basis"
      :value="pending ? undefined : averageCostBasis"
    />

    <!-- Current Price -->
    <DashboardStatCard
      title="Current Price"
      :value="pending ? undefined : currentPrice"
    />

    <!-- Unrealized P&L -->
    <DashboardStatCard
      title="Unrealized P&L"
      :value="pending ? undefined : unrealizedPnl"
      :value-class="pnlColorClass"
    />
  </div>
</template>

<script setup lang="ts">
import type { PortfolioResponse } from '~/types/dashboard'

interface Props {
  portfolio: PortfolioResponse | null
  pending: boolean
}

const props = defineProps<Props>()

const totalBtc = computed(() => {
  if (!props.portfolio) return undefined
  return props.portfolio.totalBtc.toFixed(8)
})

const totalCost = computed(() => {
  if (!props.portfolio) return undefined
  return `$${props.portfolio.totalCost.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
})

const averageCostBasis = computed(() => {
  if (!props.portfolio) return undefined
  if (props.portfolio.averageCostBasis === null) return '--'
  return `$${props.portfolio.averageCostBasis.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
})

const currentPrice = computed(() => {
  if (!props.portfolio) return undefined
  if (props.portfolio.currentPrice === null) return '--'
  return `$${props.portfolio.currentPrice.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
})

const unrealizedPnl = computed(() => {
  if (!props.portfolio) return undefined
  if (props.portfolio.unrealizedPnl === null || props.portfolio.unrealizedPnlPercent === null) return '--'
  const pnlPercent = props.portfolio.unrealizedPnlPercent >= 0
    ? `+${props.portfolio.unrealizedPnlPercent.toFixed(2)}%`
    : `${props.portfolio.unrealizedPnlPercent.toFixed(2)}%`
  const pnlUsd = props.portfolio.unrealizedPnl >= 0
    ? `+$${props.portfolio.unrealizedPnl.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
    : `-$${Math.abs(props.portfolio.unrealizedPnl).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
  return `${pnlPercent} / ${pnlUsd}`
})

const pnlColorClass = computed(() => {
  if (!props.portfolio || props.portfolio.unrealizedPnl === null) return ''
  return props.portfolio.unrealizedPnl >= 0 ? 'text-green-500' : 'text-red-500'
})
</script>
