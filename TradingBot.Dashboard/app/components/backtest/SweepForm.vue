<template>
  <UCard>
    <template #header>
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Parameter Sweep Configuration</h2>
    </template>

    <!-- Date Range Section -->
    <div class="space-y-4">
      <div>
        <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
          Date Range
        </label>

        <!-- Quick presets -->
        <div class="flex gap-2 mb-3">
          <UButton
            v-for="preset in datePresets"
            :key="preset.label"
            size="sm"
            :variant="selectedPreset === preset.label ? 'solid' : 'soft'"
            @click="selectPreset(preset)"
          >
            {{ preset.label }}
          </UButton>
          <UPopover :popper="{ placement: 'bottom-start' }">
            <UButton
              size="sm"
              :variant="selectedPreset === 'Custom' ? 'solid' : 'soft'"
              icon="i-lucide-calendar"
            >
              Custom
            </UButton>
            <template #panel>
              <div class="p-4">
                <UCalendar
                  v-model="customDateRange"
                  mode="range"
                  :columns="2"
                  @update:model-value="onCustomDateChange"
                />
              </div>
            </template>
          </UPopover>
        </div>

        <!-- Selected range display -->
        <p class="text-sm text-gray-600 dark:text-gray-400">
          {{ dateRangeText }}
        </p>
      </div>

      <!-- Parameter Ranges -->
      <div class="space-y-3">
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Base Amounts ($) <span class="text-xs text-gray-500">comma-separated</span>
          </label>
          <UInput
            v-model="formData.baseAmounts"
            type="text"
            :disabled="isRunning"
            placeholder="e.g., 5, 10, 15, 20"
          />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            High Lookback Days <span class="text-xs text-gray-500">comma-separated</span>
          </label>
          <UInput
            v-model="formData.highLookbackDays"
            type="text"
            :disabled="isRunning"
            placeholder="e.g., 14, 30, 60"
          />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Bear Market MA Periods <span class="text-xs text-gray-500">comma-separated</span>
          </label>
          <UInput
            v-model="formData.bearMarketMaPeriods"
            type="text"
            :disabled="isRunning"
            placeholder="e.g., 100, 200"
          />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Bear Boost Factors <span class="text-xs text-gray-500">comma-separated</span>
          </label>
          <UInput
            v-model="formData.bearBoosts"
            type="text"
            :disabled="isRunning"
            placeholder="e.g., 1.0, 1.25, 1.5"
          />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Max Multiplier Caps <span class="text-xs text-gray-500">comma-separated</span>
          </label>
          <UInput
            v-model="formData.maxMultiplierCaps"
            type="text"
            :disabled="isRunning"
            placeholder="e.g., 3.0, 4.5, 6.0"
          />
        </div>
      </div>

      <!-- Sweep Options -->
      <div class="grid grid-cols-2 gap-4">
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Rank By
          </label>
          <USelect
            v-model="formData.rankBy"
            :options="rankOptions"
            :disabled="isRunning"
          />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Max Combinations
          </label>
          <UInput
            v-model.number="formData.maxCombinations"
            type="number"
            :disabled="isRunning"
            min="1"
            step="1"
          />
        </div>
      </div>

      <!-- Walk-Forward Validation Toggle -->
      <div class="flex items-center gap-2">
        <UCheckbox
          v-model="formData.validate"
          :disabled="isRunning"
        />
        <label class="text-sm font-medium text-gray-700 dark:text-gray-300">
          Enable Walk-Forward Validation
        </label>
      </div>

      <!-- Progress bar -->
      <UProgress
        v-if="isRunning"
        :value="progress"
        :max="100"
        color="primary"
      />

      <!-- Error message -->
      <div v-if="error" class="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-md">
        <p class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>
      </div>

      <!-- Submit button -->
      <UButton
        block
        size="lg"
        :loading="isRunning"
        :disabled="!hasValidDateRange"
        @click="runSweep"
      >
        Run Sweep
      </UButton>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { format, subYears } from 'date-fns'
import { CalendarDate } from '@internationalized/date'
import type { DcaConfigResponse, SweepRequest, SweepResponse } from '~/types/backtest'

interface Props {
  config?: DcaConfigResponse | null
}

interface Emits {
  (e: 'sweepComplete', result: SweepResponse): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Form state - string inputs for comma-separated values
const formData = ref({
  baseAmounts: '',
  highLookbackDays: '',
  bearMarketMaPeriods: '',
  bearBoosts: '',
  maxMultiplierCaps: '',
  rankBy: 'efficiency',
  maxCombinations: 1000,
  validate: false
})

// Date range state
const selectedPreset = ref<string>('1Y')
const startDate = ref<CalendarDate | null>(null)
const endDate = ref<CalendarDate | null>(null)
const customDateRange = ref<{ start: CalendarDate; end: CalendarDate } | null>(null)

// Sweep execution state
const isRunning = ref(false)
const progress = ref(0)
const error = ref<string | null>(null)

// Rank options
const rankOptions = [
  { label: 'Efficiency Ratio', value: 'efficiency' },
  { label: 'Return %', value: 'return' },
  { label: 'Total BTC', value: 'btc' },
  { label: 'Max Drawdown', value: 'drawdown' }
]

// Date presets
const datePresets = [
  { label: '1Y', years: 1 },
  { label: '2Y', years: 2 },
  { label: '3Y', years: 3 },
  { label: 'Max', years: null } // null = 2013-01-01
]

// Initialize with 1Y preset
onMounted(() => {
  selectPreset(datePresets[0])
})

// Watch config prop to pre-fill with live config defaults
watch(() => props.config, (newConfig) => {
  if (newConfig) {
    prefillFromConfig(newConfig)
  }
}, { immediate: true })

function prefillFromConfig(config: DcaConfigResponse) {
  // Pre-fill each field with just the live config value
  formData.value.baseAmounts = String(config.baseDailyAmount)
  formData.value.highLookbackDays = String(config.highLookbackDays)
  formData.value.bearMarketMaPeriods = String(config.bearMarketMaPeriod)
  formData.value.bearBoosts = String(config.bearBoostFactor)
  formData.value.maxMultiplierCaps = String(config.maxMultiplierCap)
}

function selectPreset(preset: { label: string; years: number | null }) {
  selectedPreset.value = preset.label

  const today = new Date()
  endDate.value = new CalendarDate(today.getFullYear(), today.getMonth() + 1, today.getDate())

  if (preset.years === null) {
    // Max = 2013-01-01
    startDate.value = new CalendarDate(2013, 1, 1)
  } else {
    const start = subYears(today, preset.years)
    startDate.value = new CalendarDate(start.getFullYear(), start.getMonth() + 1, start.getDate())
  }
}

function onCustomDateChange(range: { start: CalendarDate; end: CalendarDate } | null) {
  if (range && range.start && range.end) {
    selectedPreset.value = 'Custom'
    startDate.value = range.start
    endDate.value = range.end
  }
}

const dateRangeText = computed(() => {
  if (!startDate.value || !endDate.value) {
    return 'No date range selected'
  }

  const formatDate = (d: CalendarDate) => {
    return format(new Date(d.year, d.month - 1, d.day), 'MMM dd, yyyy')
  }

  return `${formatDate(startDate.value)} - ${formatDate(endDate.value)}`
})

const hasValidDateRange = computed(() => {
  return startDate.value !== null && endDate.value !== null
})

// Parse comma-separated string to number array
function parseCommaSeparated(value: string): number[] {
  if (!value || value.trim() === '') return []
  return value
    .split(',')
    .map(s => parseFloat(s.trim()))
    .filter(n => !isNaN(n))
}

async function runSweep() {
  if (!hasValidDateRange.value) return

  isRunning.value = true
  error.value = null
  progress.value = 0

  // Simulated progress: 0-90% in ~2 seconds
  const progressInterval = setInterval(() => {
    if (progress.value < 90) {
      progress.value += 5
    }
  }, 100)

  try {
    // Build request with parsed arrays
    const request: SweepRequest = {
      startDate: `${startDate.value!.year}-${String(startDate.value!.month).padStart(2, '0')}-${String(startDate.value!.day).padStart(2, '0')}`,
      endDate: `${endDate.value!.year}-${String(endDate.value!.month).padStart(2, '0')}-${String(endDate.value!.day).padStart(2, '0')}`,
      baseAmounts: parseCommaSeparated(formData.value.baseAmounts),
      highLookbackDays: parseCommaSeparated(formData.value.highLookbackDays),
      bearMarketMaPeriods: parseCommaSeparated(formData.value.bearMarketMaPeriods),
      bearBoosts: parseCommaSeparated(formData.value.bearBoosts),
      maxMultiplierCaps: parseCommaSeparated(formData.value.maxMultiplierCaps),
      rankBy: formData.value.rankBy,
      maxCombinations: formData.value.maxCombinations,
      validate: formData.value.validate
    }

    const response = await $fetch<SweepResponse>('/api/backtest/sweep', {
      method: 'POST',
      body: request
    })

    clearInterval(progressInterval)
    progress.value = 100

    emit('sweepComplete', response)
  } catch (err: any) {
    clearInterval(progressInterval)
    error.value = err.message || 'Failed to run parameter sweep'
  } finally {
    isRunning.value = false
  }
}
</script>
