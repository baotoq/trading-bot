import type { PurchaseHistoryResponse, PurchaseDto } from '~/types/dashboard'

export function usePurchaseHistory() {
  const purchases = ref<PurchaseDto[]>([])
  const cursor = ref<string | null>(null)
  const hasMore = ref(true)
  const loading = ref(false)
  const startDate = ref<string | null>(null)
  const endDate = ref<string | null>(null)
  const tier = ref<string | null>(null)

  const loadMore = async () => {
    // Guard: prevent duplicate loads
    if (loading.value || !hasMore.value) return

    loading.value = true

    try {
      const response = await $fetch<PurchaseHistoryResponse>('/api/dashboard/purchases', {
        query: {
          cursor: cursor.value,
          pageSize: 20,
          startDate: startDate.value,
          endDate: endDate.value,
          tier: tier.value
        }
      })

      // Append items to purchases array
      purchases.value.push(...response.items)

      // Update pagination state
      cursor.value = response.nextCursor
      hasMore.value = response.hasMore
    } finally {
      loading.value = false
    }
  }

  const resetAndLoad = async () => {
    // Clear existing data
    purchases.value = []
    cursor.value = null
    hasMore.value = true

    // Load first page
    await loadMore()
  }

  return {
    purchases,
    loading,
    hasMore,
    loadMore,
    startDate,
    endDate,
    tier,
    resetAndLoad
  }
}
