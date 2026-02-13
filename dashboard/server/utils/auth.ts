import type { H3Event } from 'h3'

export function requireApiKey(event: H3Event): void {
  const config = useRuntimeConfig(event)
  const authHeader = getHeader(event, 'x-api-key')

  if (!authHeader) {
    throw createError({
      status: 401,
      statusText: 'Unauthorized',
      data: { reason: 'API key required' }
    })
  }

  if (authHeader !== config.apiKey) {
    throw createError({
      status: 403,
      statusText: 'Forbidden',
      data: { reason: 'Invalid API key' }
    })
  }
}
