import { defineVitestConfig } from '@nuxt/test-utils/config'

export default defineVitestConfig({
  test: {
    environment: 'happy-dom',
    globals: true,
    include: ['app/**/*.{test,spec}.{ts,tsx}', 'app/**/*.nuxt.spec.ts'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'lcov'],
      include: [
        'app/composables/useAuth.ts',
        'app/composables/useCorrelationId.ts',
        'app/stores/auth.ts',
        'app/stores/app.ts',
        'app/utils/constants.ts',
        'app/utils/formatters.ts'
      ],
      thresholds: {
        lines: 80,
        functions: 80,
        branches: 55,
        statements: 80
      }
    }
  }
})
