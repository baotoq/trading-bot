export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig(event)
  try {
    return await $fetch(`${config.public.apiEndpoint}/api/dashboard/status`, {
      headers: { 'x-api-key': config.apiKey }
    })
  } catch (error: any) {
    throw createError({
      status: error?.response?.status || 502,
      statusText: error?.response?.statusText || 'Bad Gateway',
      data: { reason: 'Failed to fetch status from backend API' }
    })
  }
})
