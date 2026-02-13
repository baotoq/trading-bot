<template>
  <UCard>
    <template #header>
      <div class="space-y-4">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Purchase History</h2>

        <!-- Date range filter -->
        <div class="flex flex-col sm:flex-row gap-4">
          <div class="flex-1">
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Start Date
            </label>
            <input
              v-model="startDate"
              type="date"
              class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-800 dark:text-white"
              @change="handleFilterChange"
            />
          </div>
          <div class="flex-1">
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              End Date
            </label>
            <input
              v-model="endDate"
              type="date"
              class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md shadow-sm focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-800 dark:text-white"
              @change="handleFilterChange"
            />
          </div>
        </div>
      </div>
    </template>

    <!-- Purchase cards list -->
    <div v-if="purchases.length > 0" class="space-y-4">
      <DashboardPurchaseCard
        v-for="purchase in purchases"
        :key="purchase.id"
        :purchase="purchase"
      />

      <!-- Loading indicator -->
      <div v-if="loading" class="flex items-center justify-center py-4">
        <div class="w-6 h-6 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
      </div>

      <!-- All loaded message -->
      <div v-else-if="!hasMore" class="text-center py-4 text-sm text-gray-500 dark:text-gray-400">
        All purchases loaded
      </div>

      <!-- Infinite scroll sentinel -->
      <div ref="sentinel" class="h-4" />
    </div>

    <!-- Empty state -->
    <div v-else-if="!loading" class="text-center py-12 text-gray-500 dark:text-gray-400">
      <p>No purchases found</p>
    </div>

    <!-- Initial loading -->
    <div v-else class="space-y-4">
      <USkeleton v-for="i in 3" :key="i" class="h-24 w-full" />
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { useInfiniteScroll } from '@vueuse/core'

const { purchases, loading, hasMore, loadMore, startDate, endDate, resetAndLoad } = usePurchaseHistory()

const sentinel = ref<HTMLElement | null>(null)

// Infinite scroll setup
useInfiniteScroll(
  sentinel,
  () => {
    if (!loading.value && hasMore.value) {
      loadMore()
    }
  },
  { distance: 200 }
)

const handleFilterChange = () => {
  resetAndLoad()
}

// Load initial data
onMounted(() => {
  loadMore()
})
</script>
