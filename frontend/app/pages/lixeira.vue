<script setup lang="ts">
import type { ApiResponse } from '~/types/api'
import { formatQuarterLabel } from '~/utils/roadmapQuarter'

useSeoMeta({ title: 'Lixeira Roadmap · ProductHub' })

interface DeletedItem {
  id: string
  itemType: 'Roadmap' | 'Epic' | 'Demand'
  title: string
  quarterYear: number
  quarterNumber: number
  projectId: string | null
  projectName: string | null
  parentDemandId: string | null
  deletedAt: string | null
  deletedByEmail: string | null
}

const api = useApi()
const toast = useToast()
const access = useAccessStore()

const items = ref<DeletedItem[]>([])
const isLoading = ref(true)
const isBusy = ref(false)

const purgeTarget = ref<DeletedItem | null>(null)
const purgeModalOpen = ref(false)

const typeLabels: Record<DeletedItem['itemType'], string> = {
  Roadmap: 'Roadmap', Epic: 'Épico', Demand: 'Demanda'
}

async function load() {
  isLoading.value = true
  try {
    const res = await api.get<ApiResponse<DeletedItem[]>>('/api/roadmap/demands/deleted')
    items.value = res.data ?? []
  }
  finally {
    isLoading.value = false
  }
}

onMounted(load)

function quarterLabel(item: DeletedItem) {
  return formatQuarterLabel(item.quarterYear, item.quarterNumber)
}

function formatDateTime(value: string | null) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('pt-BR')
}

async function restore(item: DeletedItem) {
  isBusy.value = true
  try {
    await api.post(`/api/roadmap/demands/${item.id}/restore`, {})
    toast.add({ title: 'Item restaurado', color: 'success' })
    await load()
  }
  finally {
    isBusy.value = false
  }
}

function askPurge(item: DeletedItem) {
  purgeTarget.value = item
  purgeModalOpen.value = true
}

async function confirmPurge() {
  const item = purgeTarget.value
  if (!item) return
  isBusy.value = true
  try {
    await api.del(`/api/roadmap/demands/${item.id}/purge`)
    toast.add({ title: 'Item excluído definitivamente', color: 'success' })
    purgeModalOpen.value = false
    purgeTarget.value = null
    await load()
  }
  finally {
    isBusy.value = false
  }
}
</script>

<template>
  <div class="space-y-5">
    <div>
      <h1 class="text-2xl font-bold text-highlighted">Lixeira Roadmap</h1>
      <p class="text-sm text-muted mt-1">
        Roadmaps, épicos e demandas excluídos. Você pode restaurá-los a qualquer momento.
      </p>
    </div>

    <UCard :ui="{ body: 'p-0' }">
      <div v-if="isLoading" class="flex items-center justify-center py-12">
        <UIcon name="i-lucide-loader-circle" class="h-6 w-6 animate-spin text-muted" />
      </div>

      <div v-else-if="!items.length" class="flex flex-col items-center gap-2 px-4 py-14 text-center">
        <UIcon name="i-lucide-trash-2" class="h-8 w-8 text-muted/50" />
        <p class="text-sm text-muted">A lixeira está vazia.</p>
      </div>

      <div v-else class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-default text-left text-xs font-semibold text-muted">
              <th class="px-4 py-2.5">Tipo</th>
              <th class="px-4 py-2.5">Título</th>
              <th class="px-4 py-2.5">Quarter</th>
              <th class="px-4 py-2.5">Time</th>
              <th class="px-4 py-2.5">Excluído por</th>
              <th class="px-4 py-2.5">Data/hora</th>
              <th class="px-4 py-2.5 text-right">Ações</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in items" :key="item.id" class="border-b border-default/60 last:border-0">
              <td class="px-4 py-2.5">
                <span class="inline-flex items-center rounded-full border border-default bg-elevated px-2 py-0.5 text-[11px] font-medium text-highlighted">
                  {{ typeLabels[item.itemType] }}
                </span>
              </td>
              <td class="px-4 py-2.5 text-highlighted">{{ item.title }}</td>
              <td class="px-4 py-2.5 text-muted">{{ quarterLabel(item) }}</td>
              <td class="px-4 py-2.5 text-muted">{{ item.projectName ?? '—' }}</td>
              <td class="px-4 py-2.5 text-muted">{{ item.deletedByEmail ?? '—' }}</td>
              <td class="px-4 py-2.5 text-muted">{{ formatDateTime(item.deletedAt) }}</td>
              <td class="px-4 py-2.5">
                <div class="flex items-center justify-end gap-1.5">
                  <UButton
                    icon="i-lucide-rotate-ccw"
                    size="xs"
                    color="primary"
                    variant="soft"
                    label="Restaurar"
                    :disabled="isBusy"
                    @click="restore(item)"
                  />
                  <UButton
                    v-if="access.canManageAccess"
                    icon="i-lucide-trash-2"
                    size="xs"
                    color="error"
                    variant="ghost"
                    :disabled="isBusy"
                    title="Excluir definitivamente"
                    @click="askPurge(item)"
                  />
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <!-- Confirmação de exclusão definitiva -->
    <UModal
      v-model:open="purgeModalOpen"
      title="Excluir definitivamente"
      :description="purgeTarget ? `Esta ação remove '${purgeTarget.title}' do banco de dados de forma permanente e não pode ser desfeita.` : ''"
    >
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton variant="outline" color="neutral" label="Cancelar" :disabled="isBusy" @click="purgeModalOpen = false" />
          <UButton color="error" icon="i-lucide-trash-2" label="Excluir definitivamente" :loading="isBusy" @click="confirmPurge" />
        </div>
      </template>
    </UModal>
  </div>
</template>
