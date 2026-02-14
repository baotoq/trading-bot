import type { ConfigResponse, UpdateConfigRequest } from '~/types/config'

export function useConfig() {
  // Reactive state
  const config = ref<ConfigResponse | null>(null)
  const defaults = ref<ConfigResponse | null>(null)
  const isLoading = ref(false)
  const isSaving = ref(false)
  const error = ref<string | null>(null)

  // Load current config from server
  async function loadConfig() {
    isLoading.value = true
    error.value = null
    try {
      const data = await $fetch<ConfigResponse>('/api/config')
      config.value = data
      return data
    } catch (err: any) {
      error.value = err.message || 'Failed to load configuration'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  // Load default config from appsettings.json
  async function loadDefaults() {
    isLoading.value = true
    error.value = null
    try {
      const data = await $fetch<ConfigResponse>('/api/config/defaults')
      defaults.value = data
      return data
    } catch (err: any) {
      error.value = err.message || 'Failed to load default configuration'
      throw err
    } finally {
      isLoading.value = false
    }
  }

  // Save config to server
  async function saveConfig(request: UpdateConfigRequest): Promise<boolean> {
    isSaving.value = true
    error.value = null
    try {
      await $fetch('/api/config', {
        method: 'PUT',
        body: request
      })
      // Reload config after successful save
      await loadConfig()
      return true
    } catch (err: any) {
      // Parse validation errors from backend response
      if (err.data?.errors) {
        error.value = Array.isArray(err.data.errors)
          ? err.data.errors.join(', ')
          : err.data.errors
      } else {
        error.value = err.message || 'Failed to save configuration'
      }
      return false
    } finally {
      isSaving.value = false
    }
  }

  // Reset to defaults (returns defaults, does not save)
  async function resetToDefaults(): Promise<ConfigResponse | null> {
    const defaultConfig = await loadDefaults()
    return defaultConfig
  }

  return {
    // State
    config,
    defaults,
    isLoading,
    isSaving,
    error,
    // Methods
    loadConfig,
    loadDefaults,
    saveConfig,
    resetToDefaults
  }
}
