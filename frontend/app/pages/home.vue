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
        :key="card.to"
        :ui="{ body: 'p-5' }"
        class="hover:ring-1 hover:ring-primary/40 transition-all cursor-pointer"
        @click="navigateTo(card.to)"
      >
        <div class="flex items-start gap-3">
          <div class="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center shrink-0">
            <UIcon
              :name="card.icon"
              class="w-5 h-5 text-primary"
            />
          </div>
          <div>
            <p class="font-semibold text-sm text-highlighted">
              {{ card.label }}
            </p>
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
  { label: 'Produtos', icon: 'i-lucide-package', to: '/products', description: 'Catálogo de produtos' },
  { label: 'KPIs', icon: 'i-lucide-bar-chart-2', to: '/kpis', description: 'Indicadores chave' },
  { label: 'Indicadores', icon: 'i-lucide-trending-up', to: '/indicators', description: 'Métricas e análises' }
]
</script>
