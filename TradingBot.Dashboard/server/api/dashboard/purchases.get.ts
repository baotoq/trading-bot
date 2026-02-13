export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig(event)
  const query = getQuery(event)
  try {
    return await $fetch(`${config.public.apiEndpoint}/api/dashboard/purchases`, {
      headers: { 'x-api-key': config.apiKey },
      query
    })
  } catch (error: any) {
    throw createError({
      status: error?.response?.status || 502,
      statusText: error?.response?.statusText || 'Bad Gateway',
      data: { reason: 'Failed to fetch purchases from backend API' }
    })
  }
})
