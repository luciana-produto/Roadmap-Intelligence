<script setup lang="ts">
import type { DemandFormData, DemandStatus, RoadmapDemand, RoadmapItemType } from '~/types/roadmap'

useSeoMeta({ title: 'Hierarquia do Roadmap · ProductHub' })

const route = useRoute()
const toast = useToast()
const roadmapStore = useRoadmapStore()
const kpiStore = useKpiStore()

const { projects, demands, dependencyOptions, customerSuggestions, selectedProjectId, isLoading } = storeToRefs(roadmapStore)
const { kpis: availableKpis } = storeToRefs(kpiStore)

const modalOpen = ref(false)
const editingDemand = ref<RoadmapDemand | null>(null)
const createItemType = ref<RoadmapItemType | undefined>()
const deleteTarget = ref<RoadmapDemand | null>(null)
const confirmDeleteOpen = ref(false)

const roadmapItems = computed(() => demands.value.filter(item => item.itemType === 'Roadmap'))
const epicItems = computed(() => demands.value.filter(item => item.itemType === 'Epic'))
const roadmapGroups = computed(() =>
  roadmapItems.value.map(roadmap => ({
    roadmap,
    epics: epicItems.value.filter(epic => epic.parentDemandId === roadmap.id)
  }))
)
const orphanEpics = computed(() =>
  epicItems.value.filter(epic => !epic.parentDemandId || !roadmapItems.value.some(roadmap => roadmap.id === epic.parentDemandId))
)

const statusLabels: Record<DemandStatus, string> = {
  Backlog: 'Backlog',
  InProgress: 'Em andamento',
  Done: 'Concluído',
  Deprioritized: 'Despriorizado'
}

const statusTone: Record<DemandStatus, string> = {
  Backlog: 'border-default bg-elevated text-muted',
  InProgress: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-800 dark:bg-blue-900/20 dark:text-blue-300',
  Done: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-800 dark:bg-emerald-900/20 dark:text-emerald-300',
  Deprioritized: 'border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-800 dark:bg-rose-900/20 dark:text-rose-300'
}

async function loadPageData() {
  await Promise.all([
    roadmapStore.fetchDemands(),
    roadmapStore.fetchDependencyOptions(),
    roadmapStore.fetchCustomerSuggestions(),
    kpiStore.fetchKpis()
  ])
}

function openCreateModal(itemType?: RoadmapItemType) {
  createItemType.value = itemType
  editingDemand.value = null
  modalOpen.value = true
}

function openEditModal(item: RoadmapDemand) {
  editingDemand.value = item
  modalOpen.value = true
}

function promptDelete(item: RoadmapDemand) {
  deleteTarget.value = item
  confirmDeleteOpen.value = true
}

async function handleSubmit(data: DemandFormData) {
  try {
    if (editingDemand.value) {
      await roadmapStore.updateDemand(editingDemand.value.id, data)
      toast.add({ title: 'Item atualizado', color: 'success' })
    }
    else {
      await roadmapStore.createDemand(data)
      toast.add({ title: 'Item criado', color: 'success' })
    }

    modalOpen.value = false
    await roadmapStore.fetchDemands()
  }
  catch {
    // handled by useApi
  }
}

function openKpiWorkspace(epic: RoadmapDemand) {
  navigateTo({
    path: '/roadmap',
    query: {
      projectId: selectedProjectId.value ?? epic.projectId,
      kpiDemandId: epic.id
    }
  })
}

async function confirmDelete() {
  if (!deleteTarget.value)
    return

  try {
    await roadmapStore.deleteDemand(deleteTarget.value.id)
    toast.add({ title: 'Item removido', color: 'success' })
    confirmDeleteOpen.value = false
    deleteTarget.value = null
    await roadmapStore.fetchDemands()
  }
  catch {
    // handled by useApi
  }
}

await roadmapStore.fetchProjects()

const queryProjectId = typeof route.query.projectId === 'string'
  ? route.query.projectId
  : null

if (queryProjectId && projects.value.some(project => project.id === queryProjectId))
  selectedProjectId.value = queryProjectId

await loadPageData()

watch(selectedProjectId, async (projectId) => {
  if (!projectId)
    return

  await loadPageData()
})
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
      <div>
        <p class="text-sm font-semibold uppercase tracking-[0.12em] text-primary/70">Estrutura</p>
        <h1 class="mt-1 text-2xl font-semibold text-highlighted">Roadmaps e Épicos</h1>
        <p class="mt-2 max-w-3xl text-sm text-muted">
          Visualização exclusiva dos níveis estruturais do roadmap. Aqui você gerencia roadmaps e épicos sem misturar com a listagem operacional das demandas.
        </p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <UButton
          color="neutral"
          variant="ghost"
          icon="i-lucide-arrow-left"
          @click="navigateTo({ path: '/roadmap', query: selectedProjectId ? { projectId: selectedProjectId } : undefined })"
        >
          Voltar para Demandas
        </UButton>
        <UButton color="neutral" variant="ghost" icon="i-lucide-map" @click="openCreateModal('Roadmap')">
          Novo Roadmap
        </UButton>
        <UButton icon="i-lucide-layers-3" @click="openCreateModal('Epic')">
          Novo Épico
        </UButton>
      </div>
    </div>

    <UCard>
      <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <UFormField label="Projeto" class="w-full lg:max-w-sm">
          <USelect
            v-model="selectedProjectId"
            :items="projects.map(project => ({ value: project.id, label: project.name }))"
            placeholder="Selecione um projeto"
            class="w-full"
          />
        </UFormField>

        <div class="flex flex-wrap items-center gap-2 text-xs text-muted">
          <span class="rounded-full border border-default bg-elevated px-3 py-1">{{ roadmapItems.length }} roadmaps</span>
          <span class="rounded-full border border-default bg-elevated px-3 py-1">{{ epicItems.length }} épicos</span>
        </div>
      </div>
    </UCard>

    <div v-if="isLoading" class="flex items-center justify-center py-16">
      <UIcon name="i-lucide-loader-circle" class="h-6 w-6 animate-spin text-primary" />
    </div>

    <template v-else>
      <div v-if="!roadmapGroups.length && !orphanEpics.length" class="rounded-2xl border border-dashed border-default bg-elevated/30 px-5 py-12 text-center text-sm text-muted">
        Nenhum roadmap ou épico encontrado para o projeto selecionado.
      </div>

      <div v-else class="space-y-4">
        <section
          v-for="group in roadmapGroups"
          :key="group.roadmap.id"
          class="rounded-2xl border border-default bg-default shadow-sm"
        >
          <div class="flex flex-col gap-3 border-b border-default px-4 py-4 lg:flex-row lg:items-start lg:justify-between">
            <div class="min-w-0">
              <div class="flex flex-wrap items-center gap-2">
                <span class="inline-flex items-center rounded-full border border-default bg-elevated px-2 py-0.5 text-[11px] font-semibold uppercase tracking-[0.08em] text-muted">
                  Roadmap
                </span>
                <span class="inline-flex items-center rounded-full border px-2 py-0.5 text-[11px] font-medium" :class="statusTone[group.roadmap.status]">
                  {{ statusLabels[group.roadmap.status] }}
                </span>
                <span class="inline-flex items-center rounded-full border border-primary/20 bg-primary/10 px-2 py-0.5 text-[11px] font-semibold text-primary">
                  {{ group.epics.length }} épico<span v-if="group.epics.length !== 1">s</span>
                </span>
              </div>
              <h2 class="mt-2 text-lg font-semibold text-highlighted">{{ group.roadmap.title }}</h2>
              <p v-if="group.roadmap.description" class="mt-1 max-w-3xl text-sm text-muted">{{ group.roadmap.description }}</p>
            </div>

            <div class="flex items-center gap-2">
              <UButton size="sm" variant="ghost" color="neutral" icon="i-lucide-pencil" @click="openEditModal(group.roadmap)">
                Editar
              </UButton>
              <UButton size="sm" variant="ghost" color="error" icon="i-lucide-trash-2" @click="promptDelete(group.roadmap)">
                Remover
              </UButton>
              <UButton size="sm" variant="ghost" color="primary" icon="i-lucide-plus" @click="openCreateModal('Epic')">
                Novo Épico
              </UButton>
            </div>
          </div>

          <div class="space-y-3 px-4 py-4">
            <div v-if="!group.epics.length" class="rounded-xl border border-dashed border-default bg-elevated/20 px-4 py-6 text-sm text-muted">
              Nenhum épico vinculado a este roadmap ainda.
            </div>

            <article
              v-for="epic in group.epics"
              :key="epic.id"
              class="rounded-xl border border-default bg-elevated/20 px-4 py-3"
            >
              <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                <div class="min-w-0">
                  <div class="flex flex-wrap items-center gap-2">
                    <span class="inline-flex items-center rounded-full border border-default bg-default px-2 py-0.5 text-[11px] font-semibold uppercase tracking-[0.08em] text-muted">
                      Épico
                    </span>
                    <span class="inline-flex items-center rounded-full border px-2 py-0.5 text-[11px] font-medium" :class="statusTone[epic.status]">
                      {{ statusLabels[epic.status] }}
                    </span>
                  </div>
                  <p class="mt-2 text-sm font-semibold text-highlighted">{{ epic.title }}</p>
                  <p v-if="epic.description" class="mt-1 text-xs text-muted">{{ epic.description }}</p>
                </div>

                <div class="flex items-center gap-2">
                  <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-line-chart" @click="openKpiWorkspace(epic)" />
                  <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" @click="openEditModal(epic)">
                    Editar
                  </UButton>
                  <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" @click="promptDelete(epic)">
                    Remover
                  </UButton>
                </div>
              </div>
            </article>
          </div>
        </section>

        <section v-if="orphanEpics.length" class="rounded-2xl border border-default bg-default shadow-sm">
          <div class="border-b border-default px-4 py-4">
            <p class="text-sm font-semibold text-highlighted">Épicos sem roadmap visível</p>
            <p class="mt-1 text-xs text-muted">Itens que precisam ser revisados ou reatribuídos dentro da hierarquia.</p>
          </div>

          <div class="space-y-3 px-4 py-4">
            <article
              v-for="epic in orphanEpics"
              :key="epic.id"
              class="rounded-xl border border-default bg-elevated/20 px-4 py-3"
            >
              <div class="flex items-start justify-between gap-3">
                <div>
                  <div class="flex flex-wrap items-center gap-2">
                    <span class="inline-flex items-center rounded-full border px-2 py-0.5 text-[11px] font-medium" :class="statusTone[epic.status]">
                      {{ statusLabels[epic.status] }}
                    </span>
                  </div>
                  <p class="mt-2 text-sm font-semibold text-highlighted">{{ epic.title }}</p>
                  <p v-if="epic.description" class="mt-1 text-xs text-muted">{{ epic.description }}</p>
                </div>

                <div class="flex items-center gap-2">
                  <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-line-chart" @click="openKpiWorkspace(epic)" />
                  <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" @click="openEditModal(epic)">
                    Editar
                  </UButton>
                  <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" @click="promptDelete(epic)">
                    Remover
                  </UButton>
                </div>
              </div>
            </article>
          </div>
        </section>
      </div>
    </template>

    <RoadmapDemandFormModal
      v-model:open="modalOpen"
      :projects="projects"
      :dependency-options="dependencyOptions"
      :customer-suggestions="customerSuggestions"
      :demand="editingDemand"
      :default-item-type="createItemType"
      :roadmap-options="roadmapItems.map(item => ({ id: item.id, title: item.title }))"
      :epic-options="epicItems.map(item => ({ id: item.id, title: item.title, roadmapTitle: item.roadmapTitle }))"
      :default-project-id="selectedProjectId ?? undefined"
      :available-kpis="availableKpis"
      @submit="handleSubmit"
    />

    <UModal
      v-model:open="confirmDeleteOpen"
      :title="deleteTarget?.itemType === 'Roadmap' ? 'Remover Roadmap' : 'Remover Épico'"
      :description="deleteTarget ? `Tem certeza que deseja remover ${deleteTarget.itemType === 'Roadmap' ? 'este roadmap' : 'este épico'}? Esta ação não pode ser desfeita.` : ''"
    >
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            variant="outline"
            color="neutral"
            label="Cancelar"
            @click="confirmDeleteOpen = false"
          />
          <UButton
            color="error"
            icon="i-lucide-trash-2"
            label="Remover"
            @click="confirmDelete"
          />
        </div>
      </template>
    </UModal>
  </div>
</template>
