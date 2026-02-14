<template>
  <div class="space-y-6">
    <!-- Loading state -->
    <div v-if="isLoading && !config" class="space-y-6">
      <UCard>
        <USkeleton class="h-64 w-full" />
      </UCard>
    </div>

    <!-- Error state -->
    <UAlert
      v-else-if="error && !config"
      color="error"
      icon="i-lucide-alert-circle"
      title="Failed to load configuration"
      :description="error"
    />

    <!-- Config loaded -->
    <div v-else-if="config">
      <!-- Top action bar -->
      <div class="flex justify-end mb-4">
        <UButton
          v-if="!isEditing"
          icon="i-lucide-edit"
          variant="soft"
          @click="enterEditMode"
        >
          Edit
        </UButton>
      </div>

      <!-- Form (wrapped with UForm for validation) -->
      <UForm :schema="configSchema" :state="formState" class="space-y-6">
        <!-- Section 1: Core DCA Settings -->
        <UCard>
          <template #header>
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
              Core DCA Settings
            </h3>
          </template>

          <div class="space-y-4">
            <!-- Base Daily Amount -->
            <UFormField name="baseDailyAmount" label="Base Daily Amount ($)" help="Amount in USD to invest daily">
              <UInput
                v-model.number="formState.baseDailyAmount"
                type="number"
                step="0.01"
                :disabled="!isEditing"
              />
            </UFormField>

            <!-- Daily Buy Time -->
            <div class="grid grid-cols-2 gap-4">
              <UFormField name="dailyBuyHour" label="Daily Buy Hour (UTC)" help="Hour (0-23) in UTC for daily purchase">
                <UInput
                  v-model.number="formState.dailyBuyHour"
                  type="number"
                  :disabled="!isEditing"
                />
              </UFormField>

              <UFormField name="dailyBuyMinute" label="Daily Buy Minute" help="Minute (0-59) in UTC for daily purchase">
                <UInput
                  v-model.number="formState.dailyBuyMinute"
                  type="number"
                  :disabled="!isEditing"
                />
              </UFormField>
            </div>

            <!-- Lookback Days -->
            <UFormField name="highLookbackDays" label="High Lookback Days" help="Number of days to look back for all-time high calculation">
              <UInput
                v-model.number="formState.highLookbackDays"
                type="number"
                :disabled="!isEditing"
              />
            </UFormField>

            <!-- Dry Run -->
            <UFormField name="dryRun" label="Dry Run Mode" help="When enabled, simulates purchases without placing real orders">
              <USwitch
                v-model="formState.dryRun"
                :disabled="!isEditing"
              />
            </UFormField>
          </div>
        </UCard>

        <!-- Section 2: Multiplier Tiers -->
        <UCard>
          <template #header>
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
              Multiplier Tiers
            </h3>
          </template>

          <UFormField name="tiers">
            <ConfigMultiplierTiersTable
              v-model="formState.tiers"
              :disabled="!isEditing"
            />
          </UFormField>
        </UCard>

        <!-- Section 3: Bear Market Settings -->
        <UCard>
          <template #header>
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
              Bear Market Settings
            </h3>
          </template>

          <div class="space-y-4">
            <!-- Bear Market MA Period -->
            <UFormField name="bearMarketMaPeriod" label="Bear Market MA Period (days)" help="Number of days for the moving average used to detect bear markets">
              <UInput
                v-model.number="formState.bearMarketMaPeriod"
                type="number"
                :disabled="!isEditing"
              />
            </UFormField>

            <!-- Bear Boost Factor -->
            <UFormField name="bearBoostFactor" label="Bear Boost Factor" help="Multiplier boost applied during bear market conditions">
              <UInput
                v-model.number="formState.bearBoostFactor"
                type="number"
                step="0.01"
                :disabled="!isEditing"
              />
            </UFormField>

            <!-- Max Multiplier Cap -->
            <UFormField name="maxMultiplierCap" label="Maximum Multiplier Cap" help="Maximum allowed multiplier (caps tier * bear boost product)">
              <UInput
                v-model.number="formState.maxMultiplierCap"
                type="number"
                step="0.1"
                :disabled="!isEditing"
              />
            </UFormField>
          </div>
        </UCard>

        <!-- Action buttons (only shown when editing) -->
        <div v-if="isEditing" class="flex items-center gap-4">
          <UButton
            type="submit"
            icon="i-lucide-save"
            :loading="isSaving"
            @click.prevent="handleSave"
          >
            Save
          </UButton>

          <UButton
            variant="soft"
            icon="i-lucide-x"
            :disabled="isSaving"
            @click="handleCancel"
          >
            Cancel
          </UButton>

          <UButton
            variant="outline"
            icon="i-lucide-refresh-cw"
            :disabled="isSaving"
            @click="handleResetToDefaults"
          >
            Reset to Defaults
          </UButton>
        </div>
      </UForm>
    </div>
  </div>
</template>

<script setup lang="ts">
import { z } from 'zod'
import type { ConfigResponse, UpdateConfigRequest } from '~/types/config'

const { config, isLoading, isSaving, error, loadConfig, saveConfig, resetToDefaults } = useConfig()
const toast = useToast()

// Edit mode state
const isEditing = ref(false)

// Form state (clone of config for editing)
const formState = ref<UpdateConfigRequest>({
  baseDailyAmount: 0,
  dailyBuyHour: 0,
  dailyBuyMinute: 0,
  highLookbackDays: 0,
  dryRun: false,
  bearMarketMaPeriod: 0,
  bearBoostFactor: 0,
  maxMultiplierCap: 0,
  tiers: []
})

// Zod validation schema matching DcaOptionsValidator
const configSchema = z.object({
  baseDailyAmount: z.number().positive('Base daily amount must be positive'),
  dailyBuyHour: z.number().int().min(0).max(23, 'Hour must be between 0 and 23'),
  dailyBuyMinute: z.number().int().min(0).max(59, 'Minute must be between 0 and 59'),
  highLookbackDays: z.number().int().positive('Lookback days must be positive'),
  dryRun: z.boolean(),
  bearMarketMaPeriod: z.number().int().positive('Bear market MA period must be positive'),
  bearBoostFactor: z.number().positive('Bear boost factor must be positive'),
  maxMultiplierCap: z.number().min(1.0, 'Maximum multiplier cap must be at least 1.0'),
  tiers: z.array(z.object({
    dropPercentage: z.number().nonnegative('Drop percentage cannot be negative'),
    multiplier: z.number().positive('Multiplier must be positive')
  })).max(5, 'Maximum 5 tiers allowed').refine(
    (tiers) => {
      // Check for duplicate dropPercentage
      const drops = tiers.map(t => t.dropPercentage)
      return drops.length === new Set(drops).size
    },
    { message: 'Duplicate drop percentages not allowed' }
  )
})

// Load config on mount
onMounted(async () => {
  await loadConfig()
})

// Watch config changes and update formState when not editing
watch(config, (newConfig) => {
  if (newConfig && !isEditing.value) {
    formState.value = { ...newConfig }
  }
})

// Enter edit mode
function enterEditMode() {
  if (!config.value) return
  formState.value = JSON.parse(JSON.stringify(config.value)) // Deep clone
  isEditing.value = true
}

// Handle cancel
function handleCancel() {
  if (!config.value) return
  formState.value = JSON.parse(JSON.stringify(config.value)) // Reset to original
  isEditing.value = false
  error.value = null
}

// Handle reset to defaults
async function handleResetToDefaults() {
  try {
    const defaults = await resetToDefaults()
    if (defaults) {
      formState.value = JSON.parse(JSON.stringify(defaults))
      toast.add({
        title: 'Defaults loaded',
        description: 'Configuration reset to appsettings.json defaults. Click Save to apply.',
        color: 'info'
      })
    }
  } catch (err: any) {
    toast.add({
      title: 'Failed to load defaults',
      description: err.message || 'An error occurred',
      color: 'error'
    })
  }
}

// Check if critical fields changed
function hasCriticalChanges(original: ConfigResponse, updated: UpdateConfigRequest): boolean {
  return (
    original.baseDailyAmount !== updated.baseDailyAmount ||
    original.dailyBuyHour !== updated.dailyBuyHour ||
    original.dailyBuyMinute !== updated.dailyBuyMinute ||
    original.dryRun !== updated.dryRun
  )
}

// Handle save
async function handleSave() {
  if (!config.value) return

  // Sort tiers before saving
  const requestToSave: UpdateConfigRequest = {
    ...formState.value,
    tiers: [...formState.value.tiers].sort((a, b) => a.dropPercentage - b.dropPercentage)
  }

  // Check for critical changes
  if (hasCriticalChanges(config.value, requestToSave)) {
    const confirmed = confirm(
      'You are changing settings that affect live trading (daily amount, schedule, or dry run mode). Apply changes?'
    )
    if (!confirmed) return
  }

  // Save
  const success = await saveConfig(requestToSave)

  if (success) {
    toast.add({
      title: 'Configuration saved successfully',
      description: 'Changes applied immediately',
      color: 'success'
    })
    isEditing.value = false
  } else {
    toast.add({
      title: 'Failed to save configuration',
      description: error.value || 'An error occurred',
      color: 'error'
    })
  }
}
</script>
