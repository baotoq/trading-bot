export default defineNuxtConfig({
  modules: ['@nuxt/ui'],

  css: ['~/assets/css/main.css'],

  devtools: { enabled: true },

  typescript: {
    strict: true,
    typeCheck: false
  },

  compatibilityDate: '2024-11-01'
})
