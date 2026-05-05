<script setup lang="ts">
useSeoMeta({ title: 'Home · ProductHub' })

const authStore = useAuthStore()
</script>

<template>
  <div class="space-y-6">
    <!-- Boas vindas -->
    <div>
      <h1 class="text-2xl font-bold text-highlighted">
        Olá, {{ authStore.user?.firstName ?? 'usuário' }} 👋
      </h1>
      <p class="text-sm text-muted mt-1">
        Bem-vindo ao ProductHub — gestão de roadmap e KPIs.
      </p>
    </div>

    <!-- Cards de acesso rápido -->
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <UCard
        v-for="card in quickAccess"
        :key="card.label"
        :ui="{ body: 'p-5' }"
        class="transition-all"
        :class="card.disabled
          ? 'opacity-70 cursor-not-allowed'
          : 'cursor-pointer hover:ring-1 hover:ring-primary/40'"
        @click="!card.disabled && navigateTo(card.to)"
      >
        <div class="flex items-start gap-3">
          <div class="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center shrink-0">
            <UIcon
              :name="card.icon"
              class="w-5 h-5 text-primary"
            />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <p class="font-semibold text-sm text-highlighted">
                {{ card.label }}
              </p>
              <UBadge v-if="card.badge" size="xs" color="neutral" variant="soft">{{ card.badge }}</UBadge>
            </div>
            <p class="text-xs text-muted mt-0.5">
              {{ card.description }}
            </p>
          </div>
        </div>
      </UCard>
    </div>
  </div>
</template>

<script lang="ts">
const quickAccess = [
  { label: 'Roadmap', icon: 'i-lucide-map', to: '/roadmap', description: 'Planejamento de produtos' },
  { label: 'Indicadores de KPIs', icon: 'i-lucide-trending-up', to: '/indicators', description: 'Métricas e análises', disabled: true, badge: 'Em breve' },
  { label: 'Cadastro de Projetos', icon: 'i-lucide-package', to: '/products', description: 'Projetos e produtos base' },
  { label: 'Cadastro de KPIs', icon: 'i-lucide-bar-chart-2', to: '/kpis', description: 'Indicadores chave' }
]
</script>
