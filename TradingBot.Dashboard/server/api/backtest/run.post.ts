export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig(event)
  try {
    const body = await readBody(event)
    return await $fetch(`${config.public.apiEndpoint}/api/backtest`, {
      method: 'POST',
      headers: { 'x-api-key': config.apiKey },
      body
    })
  } catch (error: any) {
    throw createError({
      status: error?.response?.status || 502,
      statusText: error?.response?.statusText || 'Bad Gateway',
      data: { reason: 'Failed to run backtest via backend API' }
    })
  }
})
