<template>
  <UCard>
    <template #header>
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Backtest Configuration</h2>
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

      <!-- Parameter Inputs Grid -->
      <div class="grid grid-cols-2 gap-4">
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Base Daily Amount ($)
          </label>
          <UInput
            v-model.number="formData.baseDailyAmount"
            type="number"
            :disabled="isRunning"
            min="0"
            step="1"
          />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            High Lookback Days
          </label>
          <UInput
            v-model.number="formData.highLookbackDays"
            type="number"
            :disabled="isRunning"
            min="1"
            step="1"
          />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Bear Market MA Period
          </label>
          <UInput
            v-model.number="formData.bearMarketMaPeriod"
            type="number"
            :disabled="isRunning"
            min="1"
            step="1"
          />
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Bear Boost Factor
          </label>
          <UInput
            v-model.number="formData.bearBoostFactor"
            type="number"
            :disabled="isRunning"
            min="1"
            step="0.1"
          />
        </div>

        <div class="col-span-2">
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Max Multiplier Cap
          </label>
          <UInput
            v-model.number="formData.maxMultiplierCap"
            type="number"
            :disabled="isRunning"
            min="1"
            step="0.1"
          />
        </div>
      </div>

      <!-- Multiplier Tiers Section -->
      <div>
        <div class="flex items-center justify-between mb-2">
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Multiplier Tiers
          </label>
          <UButton
            size="xs"
            variant="soft"
            icon="i-lucide-plus"
            @click="addTier"
            :disabled="isRunning"
          >
            Add Tier
          </UButton>
        </div>

        <div class="space-y-2">
          <div
            v-for="(tier, index) in formData.tiers"
            :key="index"
            class="flex gap-2 items-center"
          >
            <div class="flex-1">
              <UInput
                v-model.number="tier.dropPercentage"
                type="number"
                placeholder="Drop %"
                :disabled="isRunning"
                min="0"
                max="100"
                step="1"
              />
            </div>
            <div class="flex-1">
              <UInput
                v-model.number="tier.multiplier"
                type="number"
                placeholder="Multiplier"
                :disabled="isRunning"
                min="1"
                step="0.1"
              />
            </div>
            <UButton
              size="sm"
              variant="ghost"
              icon="i-lucide-x"
              color="red"
              @click="removeTier(index)"
              :disabled="isRunning || formData.tiers.length === 1"
            />
          </div>
        </div>
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
        @click="runBacktest"
      >
        Run Backtest
      </UButton>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { format, subYears } from 'date-fns'
import { CalendarDate } from '@internationalized/date'
import type { DcaConfigResponse, BacktestRequest, BacktestResponse } from '~/types/backtest'

interface Props {
  config?: DcaConfigResponse | null
}

interface Emits {
  (e: 'backtestComplete', result: BacktestResponse): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// Form state
const formData = ref({
  baseDailyAmount: 10,
  highLookbackDays: 30,
  bearMarketMaPeriod: 200,
  bearBoostFactor: 1.5,
  maxMultiplierCap: 5,
  tiers: [
    { dropPercentage: 5, multiplier: 1.5 },
    { dropPercentage: 10, multiplier: 2.0 },
    { dropPercentage: 20, multiplier: 3.0 }
  ]
})

// Date range state
const selectedPreset = ref<string>('1Y')
const startDate = ref<CalendarDate | null>(null)
const endDate = ref<CalendarDate | null>(null)
const customDateRange = ref<{ start: CalendarDate; end: CalendarDate } | null>(null)

// Backtest execution state
const isRunning = ref(false)
const progress = ref(0)
const error = ref<string | null>(null)

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

// Watch config prop changes
watch(() => props.config, (newConfig) => {
  if (newConfig) {
    prefillFromConfig(newConfig)
  }
}, { immediate: true })

function prefillFromConfig(config: DcaConfigResponse) {
  formData.value.baseDailyAmount = config.baseDailyAmount
  formData.value.highLookbackDays = config.highLookbackDays
  formData.value.bearMarketMaPeriod = config.bearMarketMaPeriod
  formData.value.bearBoostFactor = config.bearBoostFactor
  formData.value.maxMultiplierCap = config.maxMultiplierCap

  if (config.tiers && config.tiers.length > 0) {
    formData.value.tiers = config.tiers.map(t => ({
      dropPercentage: t.dropPercentage,
      multiplier: t.multiplier
    }))
  }
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

function addTier() {
  formData.value.tiers.push({ dropPercentage: 0, multiplier: 1.0 })
}

function removeTier(index: number) {
  if (formData.value.tiers.length > 1) {
    formData.value.tiers.splice(index, 1)
  }
}

async function runBacktest() {
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
    // Build request with CalendarDate converted to string
    const request: BacktestRequest = {
      startDate: `${startDate.value!.year}-${String(startDate.value!.month).padStart(2, '0')}-${String(startDate.value!.day).padStart(2, '0')}`,
      endDate: `${endDate.value!.year}-${String(endDate.value!.month).padStart(2, '0')}-${String(endDate.value!.day).padStart(2, '0')}`,
      baseDailyAmount: formData.value.baseDailyAmount,
      highLookbackDays: formData.value.highLookbackDays,
      bearMarketMaPeriod: formData.value.bearMarketMaPeriod,
      bearBoostFactor: formData.value.bearBoostFactor,
      maxMultiplierCap: formData.value.maxMultiplierCap,
      tiers: formData.value.tiers.map(t => ({
        dropPercentage: t.dropPercentage,
        multiplier: t.multiplier
      }))
    }

    const response = await $fetch<BacktestResponse>('/api/backtest/run', {
      method: 'POST',
      body: request
    })

    clearInterval(progressInterval)
    progress.value = 100

    emit('backtestComplete', response)
  } catch (err: any) {
    clearInterval(progressInterval)
    error.value = err.message || 'Failed to run backtest'
  } finally {
    isRunning.value = false
  }
}
</script>
