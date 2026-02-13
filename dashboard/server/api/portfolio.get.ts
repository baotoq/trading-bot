export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig(event)

  try {
    const data = await $fetch(`${config.public.apiEndpoint}/api/dashboard/portfolio`, {
      headers: {
        'x-api-key': config.apiKey
      }
    })

    return data
  } catch (error: any) {
    const statusCode = error?.response?.status || 502
    throw createError({
      status: statusCode,
      statusText: statusCode === 502 ? 'Bad Gateway' : error?.response?.statusText || 'Error',
      data: { reason: 'Failed to fetch portfolio from backend API' }
    })
  }
})
