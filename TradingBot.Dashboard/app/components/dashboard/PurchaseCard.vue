<template>
  <UCard>
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <!-- Left side: Date and Tier -->
      <div class="flex flex-col space-y-2">
        <p class="text-sm font-medium text-gray-900 dark:text-white">
          {{ formattedDate }}
        </p>
        <UBadge :color="tierBadgeColor" size="sm">
          {{ purchase.multiplierTier }}
        </UBadge>
      </div>

      <!-- Right side: Purchase details -->
      <div class="grid grid-cols-3 gap-4 text-sm">
        <div class="text-right">
          <p class="text-gray-500 dark:text-gray-400">Price</p>
          <p class="font-semibold text-gray-900 dark:text-white">{{ formattedPrice }}</p>
        </div>
        <div class="text-right">
          <p class="text-gray-500 dark:text-gray-400">Cost</p>
          <p class="font-semibold text-gray-900 dark:text-white">{{ formattedCost }}</p>
        </div>
        <div class="text-right">
          <p class="text-gray-500 dark:text-gray-400">BTC</p>
          <p class="font-semibold text-gray-900 dark:text-white">{{ formattedQuantity }}</p>
        </div>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { PurchaseDto } from '~/types/dashboard'
import { format } from 'date-fns'

interface Props {
  purchase: PurchaseDto
}

const props = defineProps<Props>()

const formattedDate = computed(() => {
  return format(new Date(props.purchase.executedAt), 'MMM dd, yyyy HH:mm') + ' UTC'
})

const formattedPrice = computed(() => {
  return `$${props.purchase.price.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
})

const formattedCost = computed(() => {
  return `$${props.purchase.cost.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
})

const formattedQuantity = computed(() => {
  return props.purchase.quantity.toFixed(8)
})

const tierBadgeColor = computed(() => {
  const tier = props.purchase.multiplierTier.toLowerCase()
  if (tier === 'base') return 'gray'
  if (tier.includes('1') || tier === 'tier1') return 'blue'
  if (tier.includes('2') || tier === 'tier2') return 'purple'
  if (tier.includes('3') || tier === 'tier3') return 'orange'
  return 'green'
})
</script>
