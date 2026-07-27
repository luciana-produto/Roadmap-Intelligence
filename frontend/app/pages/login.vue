<script setup lang="ts">
import type { SsoUser } from '~/types/auth'

definePageMeta({ layout: 'auth' })

useSeoMeta({
  title: 'Entrar · ProductHub',
  description: 'Acesse o ProductHub com sua conta corporativa.'
})

const { redirectToSso, persistSession } = useAuth()
const authStore = useAuthStore()
const router = useRouter()

// Dev Logins (local only): cada variante usa um token distinto, que o backend mapeia para
// um e-mail com permissões vindas da configuração (Authorization:*Emails).
const devLogins = [
  { token: 'dev-session',           email: 'dev@producthub.local',           first: 'Dev', last: 'View',      label: 'Dev Login (local only)' },
  { token: 'dev-session-roadmap',   email: 'dev-roadmap@producthub.local',   first: 'Dev', last: 'Roadmap',   label: 'Dev Login · Roadmap edição (local only)' },
  { token: 'dev-session-cadastros', email: 'dev-cadastros@producthub.local', first: 'Dev', last: 'Cadastros', label: 'Dev Login · Cadastros (local only)' },
  { token: 'dev-session-full',      email: 'dev-full@producthub.local',      first: 'Dev', last: 'Full',      label: 'Dev Login · Full (local only)' }
]

function devLogin(entry: typeof devLogins[number]) {
  const devUser: SsoUser = {
    id: entry.email,
    name: `${entry.first} ${entry.last}`,
    email: entry.email,
    firstName: entry.first,
    lastName: entry.last,
    groups: [],
    loginTime: new Date().toISOString(),
    tenantKey: 'dev',
    tenantDisplayName: 'Ambiente Dev'
  }
  authStore.setSession(entry.token, devUser)
  persistSession(entry.token, devUser)
  // Zera permissões da sessão anterior para o middleware buscar as da nova sessão.
  useAccessStore().reset()
  router.push('/home')
}

const isLoading = ref(false)

function handleLogin() {
  if (isLoading.value) return
  isLoading.value = true
  setTimeout(() => redirectToSso(), 300)
}
</script>

<template>
  <div
    class="min-h-screen w-full flex items-center justify-center relative overflow-hidden select-none"
    style="background: #04080F;"
  >
    <!-- Orbs decorativos -->
    <div
      class="pointer-events-none absolute rounded-full blur-[120px] opacity-20 animate-pulse"
      style="width: 500px; height: 500px; background: radial-gradient(circle, #3B82F6, transparent); top: -100px; left: -150px;"
    />
    <div
      class="pointer-events-none absolute rounded-full blur-[100px] opacity-15 animate-pulse"
      style="width: 400px; height: 400px; background: radial-gradient(circle, #6366F1, transparent); bottom: -80px; right: -100px; animation-delay: 1.5s;"
    />

    <!-- Grade de pontos -->
    <div
      class="pointer-events-none absolute inset-0"
      style="background-image: radial-gradient(circle, rgba(99,102,241,0.06) 1px, transparent 1px); background-size: 36px 36px;"
    />
    <!-- Vignette -->
    <div
      class="pointer-events-none absolute inset-0"
      style="background: radial-gradient(ellipse at center, transparent 40%, #04080F 100%)"
    />

    <!-- Card principal -->
    <div
      class="relative z-10 w-full max-w-sm mx-6 p-8 rounded-2xl"
      style="background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.08); backdrop-filter: blur(20px);"
    >
      <!-- Logo + título -->
      <div class="flex flex-col items-center gap-4 mb-8">
        <div
          class="w-14 h-14 rounded-2xl flex items-center justify-center"
          style="background: linear-gradient(135deg, #3B82F6, #6366F1); box-shadow: 0 0 32px rgba(99,102,241,0.4);"
        >
          <UIcon
            name="i-lucide-package"
            class="w-7 h-7 text-white"
          />
        </div>
        <div class="text-center space-y-1">
          <h1
            class="text-base font-bold tracking-[0.18em] uppercase"
            style="color: rgba(99,102,241,0.92); text-shadow: 0 0 24px rgba(99,102,241,0.35);"
          >
            ProductHub
          </h1>
          <p
            class="text-xs tracking-widest"
            style="color: rgba(255,255,255,0.28);"
          >
            Gestão de Roadmap e KPIs
          </p>
        </div>
      </div>

      <!-- Divisor -->
      <div
        class="mb-8"
        style="height: 1px; background: linear-gradient(to right, transparent, rgba(99,102,241,0.3), transparent);"
      />

      <!-- Descrição -->
      <div class="text-center space-y-1.5 mb-7">
        <p
          class="text-sm font-semibold"
          style="color: rgba(255,255,255,0.82);"
        >
          Autenticação corporativa
        </p>
        <p
          class="text-xs"
          style="color: rgba(255,255,255,0.32);"
        >
          Você será redirecionado para o Microsoft Azure AD
        </p>
      </div>

      <!-- Botão SSO -->
      <button
        :disabled="isLoading"
        class="w-full h-12 rounded-xl font-semibold text-sm tracking-wide text-white flex items-center justify-center gap-2.5 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed transition-all"
        style="background: linear-gradient(135deg, #3B82F6, #6366F1); box-shadow: 0 0 20px rgba(99,102,241,0.3);"
        @click="handleLogin"
      >
        <UIcon
          :name="isLoading ? 'i-lucide-loader-circle' : 'i-lucide-building-2'"
          class="w-4 h-4"
          :class="{ 'animate-spin': isLoading }"
        />
        {{ isLoading ? 'Autenticando...' : 'Entrar com conta corporativa' }}
      </button>

      <!-- Badge de segurança -->
      <div class="flex items-center justify-center gap-1.5 mt-4">
        <UIcon
          name="i-lucide-shield-check"
          class="w-3.5 h-3.5"
          style="color: rgba(255,255,255,0.2);"
        />
        <span
          class="text-xs"
          style="color: rgba(255,255,255,0.2);"
        >
          Conexão segura · TLS/HTTPS
        </span>
      </div>

      <!-- Dev logins -->
      <div
        v-if="$config.public.apiBase?.includes('localhost')"
        class="mt-6 pt-5 space-y-2"
        style="border-top: 1px solid rgba(255,255,255,0.06);"
      >
        <button
          v-for="entry in devLogins"
          :key="entry.token"
          class="w-full h-9 rounded-lg text-xs font-medium cursor-pointer transition-all"
          style="color: rgba(255,255,255,0.25); border: 1px dashed rgba(255,255,255,0.1);"
          @click="devLogin(entry)"
        >
          {{ entry.label }}
        </button>
      </div>
    </div>
  </div>
</template>
