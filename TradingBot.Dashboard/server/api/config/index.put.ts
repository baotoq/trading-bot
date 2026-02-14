export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig(event)
  const body = await readBody(event)

  try {
    return await $fetch(`${config.public.apiEndpoint}/api/config`, {
      method: 'PUT',
      headers: { 'x-api-key': config.apiKey },
      body
    })
  } catch (error: any) {
    // Pass through backend validation errors (400) with structured error messages
    const status = error?.response?.status || 502
    const data = error?.data || { reason: 'Failed to update config in backend API' }

    throw createError({
      status,
      statusText: error?.response?.statusText || (status === 400 ? 'Bad Request' : 'Bad Gateway'),
      data
    })
  }
})
