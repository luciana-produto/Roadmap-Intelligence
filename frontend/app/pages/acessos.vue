<script setup lang="ts">
import type { ApiResponse } from '~/types/api'

useSeoMeta({ title: 'Acessos · ProductHub' })

interface UserAccessEntry {
  email: string
  canEditRoadmap: boolean
  canManageRegistrations: boolean
  createdAt: string
  updatedAt: string | null
}
interface UserAccessList {
  superAdminEmails: string[]
  users: UserAccessEntry[]
}

const api = useApi()
const toast = useToast()

const superAdmins = ref<string[]>([])
const users = ref<UserAccessEntry[]>([])
const isLoading = ref(true)
const isSaving = ref(false)

// Formulário de novo acesso
const newEmail = ref('')
const newCanEditRoadmap = ref(false)
const newCanManageRegistrations = ref(false)

async function load() {
  isLoading.value = true
  try {
    const res = await api.get<ApiResponse<UserAccessList>>('/api/access')
    superAdmins.value = res.data?.superAdminEmails ?? []
    users.value = res.data?.users ?? []
  }
  finally {
    isLoading.value = false
  }
}

onMounted(load)

function isValidEmail(email: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())
}

async function upsert(email: string, canEditRoadmap: boolean, canManageRegistrations: boolean) {
  isSaving.value = true
  try {
    await api.put('/api/access', { email: email.trim().toLowerCase(), canEditRoadmap, canManageRegistrations })
    await load()
  }
  finally {
    isSaving.value = false
  }
}

async function addUser() {
  const email = newEmail.value.trim().toLowerCase()
  if (!isValidEmail(email)) {
    toast.add({ title: 'E-mail inválido', color: 'warning' })
    return
  }
  if (!newCanEditRoadmap.value && !newCanManageRegistrations.value) {
    toast.add({ title: 'Selecione ao menos uma permissão', color: 'warning' })
    return
  }
  if (superAdmins.value.includes(email)) {
    toast.add({ title: 'Este e-mail já é super-admin (acesso total via configuração).', color: 'warning' })
    return
  }
  await upsert(email, newCanEditRoadmap.value, newCanManageRegistrations.value)
  newEmail.value = ''
  newCanEditRoadmap.value = false
  newCanManageRegistrations.value = false
  toast.add({ title: 'Acesso concedido', color: 'success' })
}

// Alterna uma permissão de um usuário já cadastrado (salva na hora).
async function toggle(user: UserAccessEntry, field: 'canEditRoadmap' | 'canManageRegistrations', value: boolean) {
  const next = { canEditRoadmap: user.canEditRoadmap, canManageRegistrations: user.canManageRegistrations, [field]: value }
  await upsert(user.email, next.canEditRoadmap, next.canManageRegistrations)
}

async function remove(email: string) {
  isSaving.value = true
  try {
    await api.del(`/api/access?email=${encodeURIComponent(email)}`)
    await load()
    toast.add({ title: 'Acesso removido', color: 'success' })
  }
  finally {
    isSaving.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold text-highlighted">Acessos</h1>
      <p class="text-sm text-muted mt-1">
        Defina o que cada pessoa pode fazer, pelo e-mail corporativo (o mesmo do login).
        Quem não estiver aqui consegue apenas <strong>visualizar</strong> dashboards e roadmap.
      </p>
    </div>

    <!-- Novo acesso -->
    <UCard :ui="{ body: 'p-4 sm:p-5' }">
      <p class="text-sm font-semibold text-highlighted mb-3">Conceder acesso</p>
      <div class="flex flex-col gap-3 lg:flex-row lg:items-end">
        <UFormField label="E-mail" class="flex-1">
          <UInput v-model="newEmail" type="email" placeholder="pessoa@linx.com.br" class="w-full" @keydown.enter="addUser" />
        </UFormField>
        <div class="flex items-center gap-4 pb-1">
          <UCheckbox v-model="newCanEditRoadmap" label="Roadmap edição" />
          <UCheckbox v-model="newCanManageRegistrations" label="Cadastros" />
        </div>
        <UButton icon="i-lucide-user-plus" label="Adicionar" :loading="isSaving" @click="addUser" />
      </div>
      <p class="mt-2 text-xs text-muted">
        <strong>Roadmap edição</strong>: criar/editar/repriorizar itens e KPIs nas demandas.
        <strong>Cadastros</strong>: acessar o menu Cadastros (times, produtos e definições de KPI).
      </p>
    </UCard>

    <!-- Super-admins (config) -->
    <UCard v-if="superAdmins.length" :ui="{ body: 'p-4 sm:p-5' }">
      <div class="flex items-center gap-2 mb-2">
        <UIcon name="i-lucide-shield-check" class="h-4 w-4 text-primary" />
        <p class="text-sm font-semibold text-highlighted">Super-admins</p>
      </div>
      <p class="text-xs text-muted mb-3">Acesso total, definido na configuração do sistema. Não editável por aqui.</p>
      <div class="flex flex-wrap gap-2">
        <span v-for="email in superAdmins" :key="email" class="inline-flex items-center gap-1.5 rounded-full border border-default bg-elevated px-2.5 py-1 text-xs text-highlighted">
          <UIcon name="i-lucide-crown" class="h-3.5 w-3.5 text-amber-500" />
          {{ email }}
        </span>
      </div>
    </UCard>

    <!-- Usuários com permissões -->
    <UCard :ui="{ body: 'p-0' }">
      <div class="border-b border-default px-4 py-3">
        <p class="text-sm font-semibold text-highlighted">Usuários com permissões</p>
      </div>

      <div v-if="isLoading" class="flex items-center justify-center py-12">
        <UIcon name="i-lucide-loader-circle" class="h-6 w-6 animate-spin text-muted" />
      </div>

      <div v-else-if="!users.length" class="px-4 py-10 text-center text-sm text-muted">
        Nenhum usuário com permissões específicas ainda.
      </div>

      <table v-else class="w-full text-sm">
        <thead>
          <tr class="border-b border-default text-left text-xs font-semibold text-muted">
            <th class="px-4 py-2.5">E-mail</th>
            <th class="px-4 py-2.5 text-left">Roadmap edição</th>
            <th class="px-4 py-2.5 text-left">Cadastros</th>
            <th class="px-4 py-2.5 text-right">Ações</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="user in users" :key="user.email" class="border-b border-default/60 last:border-0">
            <td class="px-4 py-2.5 text-highlighted">{{ user.email }}</td>
            <td class="px-4 py-2.5 text-left">
              <UCheckbox
                :model-value="user.canEditRoadmap"
                :disabled="isSaving"
                @update:model-value="(v) => toggle(user, 'canEditRoadmap', v === true)"
              />
            </td>
            <td class="px-4 py-2.5 text-left">
              <UCheckbox
                :model-value="user.canManageRegistrations"
                :disabled="isSaving"
                @update:model-value="(v) => toggle(user, 'canManageRegistrations', v === true)"
              />
            </td>
            <td class="px-4 py-2.5 text-right">
              <UButton
                icon="i-lucide-trash-2"
                size="xs"
                color="error"
                variant="ghost"
                :disabled="isSaving"
                title="Remover acesso"
                @click="remove(user.email)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </UCard>
  </div>
</template>
