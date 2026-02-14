import { useSessionStorage } from '@vueuse/core'
import type { BacktestResponse, BacktestConfig, DcaMetrics, ComparisonMetrics, PurchaseLogEntry } from '~/types/backtest'

/**
 * Summary entry stored in session storage (no purchaseLog to avoid quota issues)
 */
export interface ComparisonEntrySummary {
  id: string
  label: string
  config: BacktestConfig
  metrics: DcaMetrics
  comparison: ComparisonMetrics
  color: string
}

/**
 * Full entry with in-memory purchaseLog
 */
export interface ComparisonEntry extends ComparisonEntrySummary {
  purchaseLog?: PurchaseLogEntry[]
}

/**
 * Color palette for comparison entries
 */
const COMPARISON_COLORS = [
  'rgb(59, 130, 246)',   // blue
  'rgb(34, 197, 94)',    // green
  'rgb(168, 85, 247)'    // purple
]

export function useBacktestComparison() {
  // Session storage for summary data only (survives refresh)
  const comparedBacktests = useSessionStorage<ComparisonEntrySummary[]>('backtest-comparison', [])

  // In-memory cache for purchaseLog (not persisted)
  const purchaseLogCache = ref<Map<string, PurchaseLogEntry[]>>(new Map())

  /**
   * Add a backtest result to comparison
   */
  function addToComparison(result: BacktestResponse, label?: string) {
    if (comparedBacktests.value.length >= 3) {
      console.warn('Maximum 3 comparisons reached')
      return false
    }

    // Check for duplicate config (compare by values)
    const isDuplicate = comparedBacktests.value.some(entry =>
      configsMatch(entry.config, result.config)
    )

    if (isDuplicate) {
      console.warn('This configuration is already in comparison')
      return false
    }

    const id = crypto.randomUUID()
    const color = COMPARISON_COLORS[comparedBacktests.value.length]

    // Auto-generate label if not provided
    const generatedLabel = label || generateLabel(result.config, comparedBacktests.value.length + 1)

    const summary: ComparisonEntrySummary = {
      id,
      label: generatedLabel,
      config: result.config,
      metrics: result.result.smartDca,
      comparison: result.result.comparison,
      color
    }

    // Add summary to session storage
    comparedBacktests.value.push(summary)

    // Store purchaseLog in memory cache
    purchaseLogCache.value.set(id, result.result.purchaseLog)

    return true
  }

  /**
   * Remove a comparison entry by ID
   */
  function removeFromComparison(id: string) {
    const index = comparedBacktests.value.findIndex(entry => entry.id === id)
    if (index !== -1) {
      comparedBacktests.value.splice(index, 1)
      purchaseLogCache.value.delete(id)
    }
  }

  /**
   * Clear all comparison entries
   */
  function clearComparison() {
    comparedBacktests.value = []
    purchaseLogCache.value.clear()
  }

  /**
   * Check if can add more entries
   */
  const canAdd = computed(() => comparedBacktests.value.length < 3)

  /**
   * Check if chart data is available for an entry
   */
  function hasChartData(id: string): boolean {
    return purchaseLogCache.value.has(id)
  }

  /**
   * Get full entry with purchaseLog from cache
   */
  function getFullEntry(summary: ComparisonEntrySummary): ComparisonEntry {
    return {
      ...summary,
      purchaseLog: purchaseLogCache.value.get(summary.id)
    }
  }

  /**
   * Get all full entries with purchaseLog
   */
  const fullEntries = computed<ComparisonEntry[]>(() => {
    return comparedBacktests.value.map(summary => getFullEntry(summary))
  })

  return {
    comparedBacktests,
    purchaseLogCache,
    addToComparison,
    removeFromComparison,
    clearComparison,
    canAdd,
    hasChartData,
    getFullEntry,
    fullEntries
  }
}

/**
 * Check if two configs match (for duplicate detection)
 */
function configsMatch(a: BacktestConfig, b: BacktestConfig): boolean {
  return (
    a.baseDailyAmount === b.baseDailyAmount &&
    a.highLookbackDays === b.highLookbackDays &&
    a.bearMarketMaPeriod === b.bearMarketMaPeriod &&
    a.bearBoostFactor === b.bearBoostFactor &&
    a.maxMultiplierCap === b.maxMultiplierCap &&
    JSON.stringify(a.tiers) === JSON.stringify(b.tiers)
  )
}

/**
 * Generate a label for a comparison entry
 */
function generateLabel(config: BacktestConfig, index: number): string {
  return `Base $${config.baseDailyAmount}, ${config.highLookbackDays}d lookback`
}
