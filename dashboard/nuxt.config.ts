export default defineNuxtConfig({
  modules: ['@nuxt/ui'],

  css: ['~/assets/css/main.css'],

  runtimeConfig: {
    // Server-side only (never exposed to client)
    apiKey: process.env.NUXT_API_KEY || '',

    // Public variables (exposed to client via useRuntimeConfig)
    public: {
      apiEndpoint: process.env.NUXT_PUBLIC_API_ENDPOINT || 'http://localhost:5000'
    }
  },

  routeRules: {
    // Proxy /proxy/api/** to backend API to avoid CORS in development
    '/proxy/api/**': {
      proxy: `${process.env.NUXT_PUBLIC_API_ENDPOINT || 'http://localhost:5000'}/api/**`
    }
  },

  devtools: { enabled: true },

  typescript: {
    strict: true,
    typeCheck: false
  },

  compatibilityDate: '2024-11-01'
})
