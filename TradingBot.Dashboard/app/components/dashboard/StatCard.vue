<template>
  <UCard>
    <template #header>
      <p class="text-sm text-gray-500 dark:text-gray-400">{{ title }}</p>
    </template>

    <div v-if="value !== undefined && value !== null && value !== ''" class="space-y-1">
      <p class="text-2xl font-bold" :class="valueClass">
        {{ value }}
      </p>
      <p v-if="subtitle" class="text-sm text-gray-500 dark:text-gray-400">
        {{ subtitle }}
      </p>
    </div>

    <div v-else>
      <USkeleton class="h-8 w-full" />
    </div>
  </UCard>
</template>

<script setup lang="ts">
interface Props {
  title: string
  value?: string
  subtitle?: string
  valueClass?: string
}

defineProps<Props>()
</script>
