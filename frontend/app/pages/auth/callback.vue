<script setup lang="ts">
definePageMeta({
  layout: 'auth'
})

useSeoMeta({ title: 'Autenticando · ProductHub' })

const route = useRoute()
const router = useRouter()
const { handleCallback } = useAuth()
const logger = useLogger('auth/callback')

const status = ref<'loading' | 'error'>('loading')
const errorMessage = ref('')

onMounted(async () => {
  const sessionId = route.query.sessionId as string | undefined

  if (!sessionId) {
    logger.warn('Callback sem sessionId', { query: route.query })
    errorMessage.value = 'Parâmetro de sessão ausente. Tente fazer login novamente.'
    status.value = 'error'
    return
  }

  const success = await handleCallback(sessionId)

  if (success) {
    await router.replace('/home')
  }
  else {
    status.value = 'error'
    errorMessage.value = 'Não foi possível completar a autenticação. Tente novamente.'
  }
})
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-default">
    <div class="text-center space-y-4 max-w-sm px-6">
      <template v-if="status === 'loading'">
        <div class="w-16 h-16 rounded-2xl bg-primary/10 flex items-center justify-center mx-auto mb-2">
          <UIcon
            name="i-lucide-loader-circle"
            class="w-8 h-8 text-primary animate-spin"
          />
        </div>
        <h2 class="text-xl font-bold text-highlighted">
          Autenticando...
        </h2>
        <p class="text-sm text-muted">
          Validando sua sessão corporativa, aguarde um instante.
        </p>
      </template>

      <template v-else>
        <div class="w-16 h-16 rounded-2xl bg-error/10 flex items-center justify-center mx-auto mb-2">
          <UIcon
            name="i-lucide-x-circle"
            class="w-8 h-8 text-error"
          />
        </div>
        <h2 class="text-xl font-bold text-highlighted">
          Falha na autenticação
        </h2>
        <p class="text-sm text-muted">
          {{ errorMessage }}
        </p>
        <UButton
          to="/login"
          variant="outline"
        >
          Voltar ao login
        </UButton>
      </template>
    </div>
  </div>
</template>
