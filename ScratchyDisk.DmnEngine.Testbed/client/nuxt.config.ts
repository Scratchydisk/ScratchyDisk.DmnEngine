export default defineNuxtConfig({
  ssr: false,

  modules: ['@nuxtjs/tailwindcss'],

  devtools: { enabled: false },

  // Proxy API requests to the .NET backend during development
  nitro: {
    devProxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  },

  app: {
    head: {
      title: 'DMN Testbed',
      meta: [
        { name: 'viewport', content: 'width=device-width, initial-scale=1' }
      ],
      link: [
        { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' }
      ]
    }
  },

  compatibilityDate: '2025-04-01'
})
