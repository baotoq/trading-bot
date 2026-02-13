<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Live Bot Status</h2>

        <!-- Connection status dot -->
        <span
          class="w-2 h-2 rounded-full"
          :class="connectionDotClass"
        />
      </div>
    </template>

    <div v-if="pending" class="space-y-4">
      <USkeleton class="h-16 w-full" />
    </div>

    <div v-else-if="status" class="grid grid-cols-1 md:grid-cols-3 gap-6 divide-y md:divide-y-0 md:divide-x divide-gray-200 dark:divide-gray-700">
      <!-- Health Status -->
      <div class="flex flex-col items-center justify-center space-y-2 py-4 md:py-0">
        <p class="text-sm text-gray-500 dark:text-gray-400">Health Status</p>
        <UBadge :color="healthBadgeColor" size="lg">
          {{ status.healthStatus }}
        </UBadge>
        <p v-if="status.healthMessage" class="text-xs text-gray-500 dark:text-gray-400 text-center">
          {{ status.healthMessage }}
        </p>
      </div>

      <!-- Next Buy Countdown -->
      <div class="flex flex-col items-center justify-center space-y-2 py-4 md:py-0">
        <p class="text-sm text-gray-500 dark:text-gray-400">Next buy in:</p>
        <p class="font-mono text-2xl font-bold text-gray-900 dark:text-white">
          {{ countdown }}
        </p>
      </div>

      <!-- Last Action -->
      <div class="flex flex-col items-center justify-center space-y-2 py-4 md:py-0">
        <p class="text-sm text-gray-500 dark:text-gray-400">Last Action</p>
        <p class="text-sm text-gray-700 dark:text-gray-300 text-center">
          {{ lastActionText }}
        </p>
      </div>
    </div>

    <div v-else class="text-center py-8 text-gray-500 dark:text-gray-400">
      Unable to load status
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { LiveStatusResponse } from '~/types/dashboard'
import { formatDistanceToNow } from 'date-fns'

interface Props {
  status: LiveStatusResponse | null
  pending: boolean
}

const props = defineProps<Props>()

const nextBuyTime = computed(() => props.status?.nextBuyTime ?? null)
const { remaining: countdown } = useCountdownTimer(nextBuyTime)

const healthBadgeColor = computed(() => {
  if (!props.status) return 'gray'
  switch (props.status.healthStatus) {
    case 'Healthy':
      return 'green'
    case 'Warning':
      return 'amber'
    case 'Error':
      return 'red'
    default:
      return 'gray'
  }
})

const connectionDotClass = computed(() => {
  if (props.pending) return 'bg-yellow-500 animate-pulse'
  if (!props.status) return 'bg-red-500'
  return 'bg-green-500'
})

const lastActionText = computed(() => {
  if (!props.status || !props.status.lastPurchaseTime) {
    return 'No purchases yet'
  }

  const timeAgo = formatDistanceToNow(new Date(props.status.lastPurchaseTime), { addSuffix: true })
  const price = props.status.lastPurchasePrice
    ? `$${props.status.lastPurchasePrice.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`
    : 'N/A'

  return `Last buy: ${timeAgo} at ${price}`
})
</script>
