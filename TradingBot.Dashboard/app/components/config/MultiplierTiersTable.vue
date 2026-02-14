<template>
  <div class="space-y-4">
    <!-- Table -->
    <div class="overflow-x-auto">
      <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-800">
        <thead class="bg-gray-50 dark:bg-gray-800">
          <tr>
            <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Drop %
            </th>
            <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Multiplier
            </th>
            <th scope="col" class="px-4 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Action
            </th>
          </tr>
        </thead>
        <tbody class="bg-white dark:bg-gray-900 divide-y divide-gray-200 dark:divide-gray-800">
          <tr v-for="(tier, index) in sortedTiers" :key="index">
            <td class="px-4 py-3 whitespace-nowrap">
              <UInput
                v-model.number="tier.dropPercentage"
                type="number"
                step="0.1"
                :disabled="disabled"
                :class="getValidationClass(tier, 'dropPercentage')"
                @input="handleTierChange"
              />
            </td>
            <td class="px-4 py-3 whitespace-nowrap">
              <UInput
                v-model.number="tier.multiplier"
                type="number"
                step="0.1"
                :disabled="disabled"
                :class="getValidationClass(tier, 'multiplier')"
                @input="handleTierChange"
              />
            </td>
            <td class="px-4 py-3 whitespace-nowrap text-right">
              <UButton
                icon="i-lucide-trash-2"
                variant="ghost"
                color="error"
                size="sm"
                :disabled="disabled"
                @click="removeTier(index)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Add Tier Button -->
    <div class="flex items-center justify-between">
      <UButton
        icon="i-lucide-plus"
        variant="soft"
        :disabled="disabled || sortedTiers.length >= 5"
        @click="addTier"
      >
        Add Tier
      </UButton>
      <p class="text-sm text-gray-500 dark:text-gray-400">
        Maximum 5 tiers. Tiers are auto-sorted by drop percentage.
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { MultiplierTierDto } from '~/types/config'

interface Props {
  modelValue: MultiplierTierDto[]
  disabled?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  disabled: false
})

const emit = defineEmits<{
  'update:modelValue': [value: MultiplierTierDto[]]
}>()

// Local copy for editing
const sortedTiers = ref<MultiplierTierDto[]>([...props.modelValue])

// Watch for external changes
watch(() => props.modelValue, (newValue) => {
  sortedTiers.value = [...newValue]
}, { deep: true })

// Auto-sort and emit update
function handleTierChange() {
  const sorted = [...sortedTiers.value].sort((a, b) => a.dropPercentage - b.dropPercentage)
  sortedTiers.value = sorted
  emit('update:modelValue', sorted)
}

// Add new tier
function addTier() {
  if (sortedTiers.value.length >= 5) return
  sortedTiers.value.push({ dropPercentage: 0, multiplier: 1 })
  handleTierChange()
}

// Remove tier
function removeTier(index: number) {
  sortedTiers.value.splice(index, 1)
  handleTierChange()
}

// Validation styling
function getValidationClass(tier: MultiplierTierDto, field: 'dropPercentage' | 'multiplier') {
  const value = tier[field]

  // Check for negative values
  if (value < 0) {
    return 'border-red-500 dark:border-red-500'
  }

  // Check for duplicate dropPercentage
  if (field === 'dropPercentage') {
    const duplicateCount = sortedTiers.value.filter(t => t.dropPercentage === value).length
    if (duplicateCount > 1) {
      return 'border-red-500 dark:border-red-500'
    }
  }

  return ''
}
</script>
