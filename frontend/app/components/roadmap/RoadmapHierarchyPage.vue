<script setup lang="ts">
import type { DemandFormData, DemandStatus, RoadmapDemand, RoadmapItemType } from '~/types/roadmap'
import { getLatestPromisedDate } from '~/utils/roadmapPromisedDate'

useSeoMeta({ title: 'Roadmap · ProductHub' })

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
const collapsedRoadmapIds = ref<string[]>([])
const collapsedEpicIds = ref<string[]>([])

const roadmapItems = computed(() => demands.value.filter(item => item.itemType === 'Roadmap'))
const epicItems = computed(() => demands.value.filter(item => item.itemType === 'Epic'))
const demandItems = computed(() => demands.value.filter(item => item.itemType === 'Demand'))
const roadmapGroups = computed(() =>
  roadmapItems.value.map(roadmap => ({
    roadmap,
    epics: epicItems.value.filter(epic => epic.parentDemandId === roadmap.id)
  }))
)
const orphanEpics = computed(() =>
  epicItems.value.filter(epic => !epic.parentDemandId || !roadmapItems.value.some(roadmap => roadmap.id === epic.parentDemandId))
)
const orphanDemands = computed(() =>
  demandItems.value.filter((demand) => {
    if (!demand.epicId)
      return true

    return !epicItems.value.some(epic => epic.id === demand.epicId)
  })
)

const hasCollapsibleRoadmaps = computed(() => roadmapGroups.value.some(group => group.epics.length > 0))
const hasCollapsibleEpics = computed(() => epicItems.value.some(epic => getDemandsForEpic(epic.id).length > 0))
const areAllRoadmapsCollapsed = computed(() =>
  hasCollapsibleRoadmaps.value && roadmapGroups.value.every(group => !group.epics.length || collapsedRoadmapIds.value.includes(group.roadmap.id))
)
const areAllEpicsCollapsed = computed(() =>
  hasCollapsibleEpics.value && epicItems.value.every(epic => !getDemandsForEpic(epic.id).length || collapsedEpicIds.value.includes(epic.id))
)

const projectNameById = computed(() =>
  new Map(projects.value.map(project => [project.id, project.name] as const))
)
const epicById = computed(() =>
  new Map(epicItems.value.map(item => [item.id, item] as const))
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

const classificationLabels: Record<RoadmapDemand['classification'], string> = {
  TechnicalDebtSecurity: 'Débito Técnico',
  Strategic: 'Estratégico',
  Evolution: 'Evolução',
  ImprovementGap: 'Melhoria/Gap',
  Mandatory: 'Mandatório',
  Homologation: 'Homologação',
  Customizacao: 'Customização'
}

const classificationBadgeClass: Record<RoadmapDemand['classification'], string> = {
  TechnicalDebtSecurity: 'bg-slate-100 text-slate-700 border-slate-200 dark:bg-slate-800/60 dark:text-slate-300 dark:border-slate-700',
  Strategic: 'bg-indigo-100 text-indigo-700 border-indigo-200 dark:bg-indigo-900/30 dark:text-indigo-300 dark:border-indigo-800',
  Evolution: 'bg-sky-100 text-sky-700 border-sky-200 dark:bg-sky-900/30 dark:text-sky-300 dark:border-sky-800',
  ImprovementGap: 'bg-emerald-100 text-emerald-700 border-emerald-200 dark:bg-emerald-900/30 dark:text-emerald-300 dark:border-emerald-800',
  Mandatory: 'bg-red-100 text-red-700 border-red-200 dark:bg-red-900/30 dark:text-red-300 dark:border-red-800',
  Homologation: 'bg-violet-100 text-violet-700 border-violet-200 dark:bg-violet-900/30 dark:text-violet-300 dark:border-violet-800',
  Customizacao: 'bg-orange-100 text-orange-700 border-orange-200 dark:bg-orange-900/30 dark:text-orange-300 dark:border-orange-800'
}

const createMenuItems = computed(() => [[
  {
    label: 'Novo roadmap',
    icon: 'i-lucide-map',
    onSelect: () => openCreateModal('Roadmap')
  },
  {
    label: 'Novo épico',
    icon: 'i-lucide-layers-3',
    onSelect: () => openCreateModal('Epic')
  },
  {
    label: 'Nova demanda',
    icon: 'i-lucide-list-todo',
    onSelect: () => openCreateModal('Demand')
  }
]])

function formatDate(value?: string) {
  if (!value)
    return '—'

  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day)
    return value

  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit',
    month: 'short',
    year: '2-digit'
  }).format(new Date(year, month - 1, day))
}

function getNoKpiClassificationLabel(value: RoadmapDemand['noKpiClassification']) {
  switch (value) {
    case 'Relationship':
      return 'Relacionamento'
    case 'Mandatory':
      return 'Mandatório'
    case 'Technical':
      return 'Técnico'
    default:
      return ''
  }
}

function getDerivedPromisedDateFromDemands(items: RoadmapDemand[]) {
  return getLatestPromisedDate(items)
}

function getDisplayedPromisedDate(item: RoadmapDemand) {
  const directPromisedDate = item.effectivePromisedDate ?? item.promisedDate ?? ''
  if (directPromisedDate || item.itemType === 'Demand')
    return directPromisedDate

  if (item.itemType === 'Epic')
    return getDerivedPromisedDateFromDemands(getDemandsForEpic(item.id))

  const roadmapDemands = demandItems.value.filter(demand => demand.roadmapId === item.id)
  const derivedFromDemands = getDerivedPromisedDateFromDemands(roadmapDemands)
  if (derivedFromDemands)
    return derivedFromDemands

  const roadmapEpics = epicItems.value.filter(epic => epic.parentDemandId === item.id)
  const epicDates = roadmapEpics
    .map(epic => getDisplayedPromisedDate(epic))
    .filter((value): value is string => !!value)

  return epicDates.sort().at(-1) ?? ''
}

function getDisplayIssueLinks(item: Pick<RoadmapDemand, 'issueLinks' | 'jiraIssue'>) {
  if (item.issueLinks?.length)
    return item.issueLinks

  if (item.jiraIssue?.trim())
    return [{ key: item.jiraIssue.trim() }]

  return []
}

function getProjectNames(item: Pick<RoadmapDemand, 'projectId' | 'projectIds'>) {
  const ids = item.projectId
    ? [item.projectId]
    : (item.projectIds ?? [])

  return ids
    .map(id => projectNameById.value.get(id) ?? '')
    .filter(Boolean)
}

function getKpiTargetEpic(item: RoadmapDemand) {
  if (item.itemType === 'Epic')
    return item

  if (!item.epicId)
    return null

  return epicById.value.get(item.epicId) ?? null
}

function getKpiSummary(item: RoadmapDemand) {
  const targetEpic = getKpiTargetEpic(item)

  if (!targetEpic) {
    return {
      label: '—',
      tone: 'border-default bg-elevated text-muted',
      actionLabel: 'Sem épico vinculado',
      clickable: false
    }
  }

  if (targetEpic.hasNoKpi) {
    return {
      label: 'SEM KPI',
      tone: 'border-warning/40 bg-warning/10 text-warning',
      actionLabel: 'Editar registro de KPI do épico',
      clickable: true
    }
  }

  if (targetEpic.kpiLinks.length > 0) {
    return {
      label: `${targetEpic.kpiLinks.length} KPI${targetEpic.kpiLinks.length > 1 ? 's' : ''}`,
      tone: 'border-primary/20 bg-primary/10 text-primary',
      actionLabel: 'Abrir registro de KPI do épico',
      clickable: true
    }
  }

  return {
    label: 'Incluir KPI',
    tone: 'border-error/40 bg-error/10 text-error',
    actionLabel: 'Incluir KPI',
    clickable: true
  }
}

function getKpiSecondaryLabel(item: RoadmapDemand) {
  const targetEpic = getKpiTargetEpic(item)
  if (!targetEpic?.hasNoKpi)
    return ''

  return getNoKpiClassificationLabel(targetEpic.noKpiClassification)
}

function getDemandsForEpic(epicId: string) {
  return demandItems.value
    .filter(demand => demand.epicId === epicId)
    .sort((left, right) => {
      if (left.quarterYear !== right.quarterYear)
        return left.quarterYear - right.quarterYear

      if (left.quarterNumber !== right.quarterNumber)
        return left.quarterNumber - right.quarterNumber

      return left.sortOrder - right.sortOrder
    })
}

function isRoadmapCollapsed(roadmapId: string) {
  return collapsedRoadmapIds.value.includes(roadmapId)
}

function toggleRoadmapCollapse(roadmapId: string) {
  if (isRoadmapCollapsed(roadmapId)) {
    collapsedRoadmapIds.value = collapsedRoadmapIds.value.filter(id => id !== roadmapId)
    return
  }

  collapsedRoadmapIds.value = [...collapsedRoadmapIds.value, roadmapId]
}

function isEpicCollapsed(epicId: string) {
  return collapsedEpicIds.value.includes(epicId)
}

function toggleEpicCollapse(epicId: string) {
  if (isEpicCollapsed(epicId)) {
    collapsedEpicIds.value = collapsedEpicIds.value.filter(id => id !== epicId)
    return
  }

  collapsedEpicIds.value = [...collapsedEpicIds.value, epicId]
}

function collapseAllRoadmaps() {
  collapsedRoadmapIds.value = roadmapGroups.value
    .filter(group => group.epics.length > 0)
    .map(group => group.roadmap.id)
}

function expandAllRoadmaps() {
  collapsedRoadmapIds.value = []
}

function collapseAllEpics() {
  collapsedEpicIds.value = epicItems.value
    .filter(epic => getDemandsForEpic(epic.id).length > 0)
    .map(epic => epic.id)
}

function expandAllEpics() {
  collapsedEpicIds.value = []
}

watch(roadmapGroups, (groups) => {
  const validIds = new Set(groups.map(group => group.roadmap.id))
  collapsedRoadmapIds.value = collapsedRoadmapIds.value.filter(id => validIds.has(id))
}, { immediate: true })

watch(epicItems, (items) => {
  const validIds = new Set(items.map(item => item.id))
  collapsedEpicIds.value = collapsedEpicIds.value.filter(id => validIds.has(id))
}, { immediate: true })

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

function openKpiWorkspace(item: RoadmapDemand) {
  const targetEpic = getKpiTargetEpic(item)
  if (!targetEpic)
    return

  navigateTo({
    path: '/roadmap',
    query: {
      projectId: selectedProjectId.value ?? targetEpic.projectId,
      kpiDemandId: targetEpic.id
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
  <div class="space-y-4">
    <div class="rounded-[24px] bg-[linear-gradient(135deg,rgba(255,255,255,0.92),rgba(248,250,252,0.88))] px-4 py-4 shadow-sm dark:bg-[linear-gradient(135deg,rgba(23,23,23,0.94),rgba(31,41,55,0.78))]">
      <div class="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
      <div class="min-w-0">
        <h1 class="text-lg font-semibold tracking-tight text-highlighted">Roadmaps, Épicos e Demandas</h1>
        <p class="mt-1 truncate text-xs text-muted">
          Planejamento do roadmap em uma única visão.
        </p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <div class="inline-flex items-center rounded-xl border border-default bg-default/80 p-1 shadow-sm backdrop-blur">
          <UButton
            size="sm"
            color="neutral"
            variant="ghost"
            icon="i-lucide-layout-list"
            @click="navigateTo({ path: '/roadmap', query: selectedProjectId ? { projectId: selectedProjectId } : undefined })"
          >
            Planejamento
          </UButton>
          <UButton
            size="sm"
            color="neutral"
            variant="soft"
            icon="i-lucide-workflow"
          >
            Roadmap
          </UButton>
        </div>
        <UDropdownMenu :items="createMenuItems">
          <UButton icon="i-lucide-plus" label="Novo Item" />
        </UDropdownMenu>
      </div>
      </div>
    </div>

    <UCard :ui="{ body: 'p-3 sm:p-3' }">
      <div class="flex flex-col gap-2 lg:flex-row lg:items-end lg:justify-between">
        <UFormField label="Projeto" class="w-full lg:max-w-sm">
          <USelect
            v-model="selectedProjectId"
            :items="projects.map(project => ({ value: project.id, label: project.name }))"
            placeholder="Selecione um projeto"
            class="w-full"
          />
        </UFormField>

        <div class="flex flex-wrap items-center gap-1.5 text-[11px] text-muted">
          <span class="rounded-full border border-default bg-elevated px-2.5 py-0.5">{{ roadmapItems.length }} roadmaps</span>
          <span class="rounded-full border border-default bg-elevated px-2.5 py-0.5">{{ epicItems.length }} épicos</span>
          <span class="rounded-full border border-default bg-elevated px-2.5 py-0.5">{{ demandItems.length }} demandas</span>
        </div>
      </div>

      <div class="mt-2.5 flex flex-wrap items-center gap-1.5 border-t border-default pt-2.5">
        <span class="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted">Exibição</span>
        <UButton
          size="xs"
          color="neutral"
          variant="outline"
          icon="i-lucide-chevrons-up-down"
          :disabled="!hasCollapsibleRoadmaps"
          @click="areAllRoadmapsCollapsed ? expandAllRoadmaps() : collapseAllRoadmaps()"
        >
          {{ areAllRoadmapsCollapsed ? 'Expandir roadmaps' : 'Recolher roadmaps' }}
        </UButton>
        <UButton
          size="xs"
          color="neutral"
          variant="outline"
          icon="i-lucide-chevrons-up-down"
          :disabled="!hasCollapsibleEpics"
          @click="areAllEpicsCollapsed ? expandAllEpics() : collapseAllEpics()"
        >
          {{ areAllEpicsCollapsed ? 'Expandir épicos' : 'Recolher épicos' }}
        </UButton>
        <UButton
          size="xs"
          color="neutral"
          variant="ghost"
          icon="i-lucide-unfold-vertical"
          :disabled="(!collapsedRoadmapIds.length && !collapsedEpicIds.length)"
          @click="expandAllRoadmaps(); expandAllEpics()"
        >
          Expandir tudo
        </UButton>
      </div>
    </UCard>

    <div v-if="isLoading" class="flex items-center justify-center py-16">
      <UIcon name="i-lucide-loader-circle" class="h-6 w-6 animate-spin text-primary" />
    </div>

    <template v-else>
      <div v-if="!roadmapGroups.length && !orphanEpics.length && !orphanDemands.length" class="rounded-2xl border border-dashed border-default bg-elevated/30 px-5 py-12 text-center text-sm text-muted">
        Nenhum item encontrado para o projeto selecionado.
      </div>

      <div v-else class="overflow-hidden rounded-2xl border border-default bg-default shadow-sm">
        <div class="overflow-x-auto">
          <table class="min-w-[1120px] w-full border-separate border-spacing-0 text-[13px]">
            <thead>
              <tr class="bg-elevated/80 text-left text-[11px] font-semibold uppercase tracking-[0.08em] text-muted">
                <th class="sticky top-0 z-10 border-b border-default px-3 py-2 bg-elevated/95">Item</th>
                <th class="sticky top-0 z-10 border-b border-default px-3 py-2 bg-elevated/95">Status</th>
                <th class="sticky top-0 z-10 border-b border-default px-3 py-2 bg-elevated/95">Projetos</th>
                <th class="sticky top-0 z-10 border-b border-default px-3 py-2 bg-elevated/95">Classificação</th>
                <th class="sticky top-0 z-10 border-b border-default px-3 py-2 bg-elevated/95">Issue</th>
                <th class="sticky top-0 z-10 border-b border-default px-3 py-2 bg-elevated/95">Conclusão Prev.</th>
                <th class="sticky top-0 z-10 border-b border-default px-3 py-2 bg-elevated/95">KPI</th>
                <th class="sticky top-0 z-10 border-b border-default px-3 py-2 text-right bg-elevated/95">Ações</th>
              </tr>
            </thead>

            <tbody>
              <template v-for="group in roadmapGroups" :key="group.roadmap.id">
                <tr class="border-b border-default bg-default hover:bg-elevated/30 transition-colors">
                  <td class="border-b border-default px-3 py-2 align-top">
                    <div class="flex items-start gap-1.5">
                      <button
                        type="button"
                        class="mt-0.5 inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-md border border-default bg-default text-muted transition-colors hover:text-highlighted"
                        :disabled="!group.epics.length"
                        @click="toggleRoadmapCollapse(group.roadmap.id)"
                      >
                        <UIcon
                          :name="group.epics.length ? (isRoadmapCollapsed(group.roadmap.id) ? 'i-lucide-chevron-right' : 'i-lucide-chevron-down') : 'i-lucide-minus'"
                          class="h-3.5 w-3.5"
                        />
                      </button>

                      <div class="min-w-0 flex-1">
                        <div class="flex flex-wrap items-center gap-1.5">
                          <span class="inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-muted">
                            Roadmap
                          </span>
                          <span class="inline-flex items-center rounded-md border border-primary/20 bg-primary/10 px-1.5 py-0.5 text-[9px] font-semibold text-primary">
                            {{ group.epics.length }} épico<span v-if="group.epics.length !== 1">s</span>
                          </span>
                          <span class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[9px] font-medium text-muted">
                            {{ group.epics.reduce((total, epic) => total + getDemandsForEpic(epic.id).length, 0) }} demandas
                          </span>
                        </div>
                        <div class="mt-0.5 flex items-start gap-1.5">
                          <UIcon name="i-lucide-map" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-primary" />
                          <div class="min-w-0">
                            <p class="truncate text-[13px] font-semibold text-highlighted" :title="group.roadmap.description || undefined">{{ group.roadmap.title }}</p>
                          </div>
                        </div>
                      </div>
                    </div>
                  </td>

                  <td class="border-b border-default px-3 py-2 align-top">
                    <span class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="statusTone[group.roadmap.status]">
                      {{ statusLabels[group.roadmap.status] }}
                    </span>
                  </td>

                  <td class="border-b border-default px-3 py-2 align-top">
                    <div class="flex flex-wrap gap-1.5">
                      <span
                        v-for="projectName in getProjectNames(group.roadmap)"
                        :key="projectName"
                        class="inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[10px] text-highlighted"
                      >
                        {{ projectName }}
                      </span>
                      <span v-if="!getProjectNames(group.roadmap).length" class="text-xs text-muted">—</span>
                    </div>
                  </td>

                  <td class="border-b border-default px-3 py-2 align-top">
                    <span class="text-xs text-muted">—</span>
                  </td>

                  <td class="border-b border-default px-3 py-2 align-top">
                    <span class="text-xs text-muted">—</span>
                  </td>

                  <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted">
                    {{ formatDate(getDisplayedPromisedDate(group.roadmap)) }}
                  </td>

                  <td class="border-b border-default px-3 py-2 align-top">
                    <div class="flex min-w-0 flex-col items-start gap-1">
                      <span class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="getKpiSummary(group.roadmap).tone" :title="getKpiSummary(group.roadmap).actionLabel">
                        {{ getKpiSummary(group.roadmap).label }}
                      </span>
                      <span v-if="getKpiSecondaryLabel(group.roadmap)" class="text-[11px] text-muted">
                        {{ getKpiSecondaryLabel(group.roadmap) }}
                      </span>
                    </div>
                  </td>

                  <td class="border-b border-default px-3 py-2 align-top">
                    <div class="flex items-center justify-end gap-1">
                      <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" title="Editar roadmap" @click="openEditModal(group.roadmap)" />
                      <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" title="Remover roadmap" @click="promptDelete(group.roadmap)" />
                      <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-plus" title="Novo épico" @click="openCreateModal('Epic')" />
                    </div>
                  </td>
                </tr>

                <tr
                  v-if="!group.epics.length && !isRoadmapCollapsed(group.roadmap.id)"
                  class="bg-elevated/10"
                >
                  <td colspan="8" class="border-b border-default px-3 py-3 text-xs text-muted">
                    Nenhum épico vinculado a este roadmap ainda.
                  </td>
                </tr>

                <template v-for="epic in group.epics" :key="epic.id">
                  <tr
                    v-show="!isRoadmapCollapsed(group.roadmap.id)"
                    class="bg-elevated/10 hover:bg-elevated/20 transition-colors"
                  >
                    <td class="border-b border-default px-3 py-2 align-top">
                      <div class="flex items-start gap-1.5 pl-8">
                        <button
                          type="button"
                          class="mt-0.5 inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-md border border-default bg-default text-muted transition-colors hover:text-highlighted"
                          :disabled="!getDemandsForEpic(epic.id).length"
                          @click="toggleEpicCollapse(epic.id)"
                        >
                          <UIcon
                            :name="getDemandsForEpic(epic.id).length ? (isEpicCollapsed(epic.id) ? 'i-lucide-chevron-right' : 'i-lucide-chevron-down') : 'i-lucide-minus'"
                            class="h-3.5 w-3.5"
                          />
                        </button>
                        <UIcon name="i-lucide-star" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-amber-500" />
                        <div class="min-w-0 flex-1">
                          <div class="flex flex-wrap items-center gap-1.5">
                            <span class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-muted">
                              Épico
                            </span>
                            <span class="inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[9px] font-medium text-muted">
                              {{ getDemandsForEpic(epic.id).length }} demandas
                            </span>
                          </div>
                          <p class="mt-0.5 truncate text-[13px] font-medium text-highlighted" :title="epic.description || undefined">{{ epic.title }}</p>
                        </div>
                      </div>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <span class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="statusTone[epic.status]">
                        {{ statusLabels[epic.status] }}
                      </span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <div class="flex flex-wrap gap-1.5">
                        <span
                          v-for="projectName in getProjectNames(epic)"
                          :key="`${epic.id}-${projectName}`"
                          class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] text-highlighted"
                        >
                          {{ projectName }}
                        </span>
                        <span v-if="!getProjectNames(epic).length" class="text-xs text-muted">—</span>
                      </div>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <span class="inline-flex items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium" :class="classificationBadgeClass[epic.classification]">
                        {{ classificationLabels[epic.classification] }}
                      </span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <div class="flex flex-wrap gap-1.5">
                        <a
                          v-for="issue in getDisplayIssueLinks(epic)"
                          :key="`${epic.id}-${issue.key}`"
                          :href="issue.url || undefined"
                          :target="issue.url ? '_blank' : undefined"
                          rel="noreferrer"
                          class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] font-medium text-primary transition-colors hover:border-primary/40"
                        >
                          {{ issue.key }}
                        </a>
                        <span v-if="!getDisplayIssueLinks(epic).length" class="text-xs text-muted">—</span>
                      </div>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted">
                      {{ formatDate(getDisplayedPromisedDate(epic)) }}
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <div class="flex min-w-0 flex-col items-start gap-1">
                        <button type="button" class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium transition-colors hover:opacity-80" :class="getKpiSummary(epic).tone" :title="getKpiSummary(epic).actionLabel" @click="openKpiWorkspace(epic)">
                          {{ getKpiSummary(epic).label }}
                        </button>
                        <span v-if="getKpiSecondaryLabel(epic)" class="text-[11px] text-muted">
                          {{ getKpiSecondaryLabel(epic) }}
                        </span>
                      </div>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <div class="flex items-center justify-end gap-1">
                        <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-line-chart" title="Abrir KPIs do épico" @click="openKpiWorkspace(epic)" />
                        <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" title="Editar épico" @click="openEditModal(epic)" />
                        <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" title="Remover épico" @click="promptDelete(epic)" />
                      </div>
                    </td>
                  </tr>

                  <tr
                    v-if="!getDemandsForEpic(epic.id).length && !isRoadmapCollapsed(group.roadmap.id) && !isEpicCollapsed(epic.id)"
                    class="bg-elevated/5"
                  >
                    <td colspan="8" class="border-b border-default px-3 py-2.5 pl-20 text-[11px] text-muted">
                      Nenhuma demanda vinculada a este épico ainda.
                    </td>
                  </tr>

                  <tr
                    v-for="demand in getDemandsForEpic(epic.id)"
                    v-show="!isRoadmapCollapsed(group.roadmap.id) && !isEpicCollapsed(epic.id)"
                    :key="demand.id"
                    class="bg-default hover:bg-elevated/10 transition-colors"
                  >
                    <td class="border-b border-default px-3 py-2 align-top">
                      <div class="flex items-start gap-1.5 pl-20">
                        <UIcon name="i-lucide-list-todo" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-sky-600" />
                        <div class="min-w-0 flex-1">
                          <div class="flex flex-wrap items-center gap-1.5">
                            <span class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-muted">
                              Demanda
                            </span>
                            <span class="inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[9px] font-medium text-muted">
                              {{ demand.quarterLabel || 'Backlog' }}
                            </span>
                            <span v-if="typeof demand.hours === 'number'" class="inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[9px] font-medium text-muted">
                              {{ demand.hours }}h
                            </span>
                          </div>
                          <p class="mt-0.5 truncate text-[13px] font-medium text-highlighted" :title="demand.description || undefined">{{ demand.title }}</p>
                        </div>
                      </div>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <span class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="statusTone[demand.status]">
                        {{ statusLabels[demand.status] }}
                      </span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <div class="flex flex-wrap gap-1.5">
                        <span
                          v-for="projectName in getProjectNames(demand)"
                          :key="`${demand.id}-${projectName}`"
                          class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] text-highlighted"
                        >
                          {{ projectName }}
                        </span>
                        <span v-if="!getProjectNames(demand).length" class="text-xs text-muted">—</span>
                      </div>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <span class="inline-flex items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium" :class="classificationBadgeClass[demand.classification]">
                        {{ classificationLabels[demand.classification] }}
                      </span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <div class="flex flex-wrap gap-1.5">
                        <a
                          v-for="issue in getDisplayIssueLinks(demand)"
                          :key="`${demand.id}-${issue.key}`"
                          :href="issue.url || undefined"
                          :target="issue.url ? '_blank' : undefined"
                          rel="noreferrer"
                          class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] font-medium text-primary transition-colors hover:border-primary/40"
                        >
                          {{ issue.key }}
                        </a>
                        <span v-if="!getDisplayIssueLinks(demand).length" class="text-xs text-muted">—</span>
                      </div>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted">
                      {{ formatDate(getDisplayedPromisedDate(demand)) }}
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <div class="flex min-w-0 flex-col items-start gap-1">
                        <button v-if="getKpiSummary(demand).clickable" type="button" class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium transition-colors hover:opacity-80" :class="getKpiSummary(demand).tone" :title="getKpiSummary(demand).actionLabel" @click="openKpiWorkspace(demand)">
                          {{ getKpiSummary(demand).label }}
                        </button>
                        <span v-else class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="getKpiSummary(demand).tone" :title="getKpiSummary(demand).actionLabel">
                          {{ getKpiSummary(demand).label }}
                        </span>
                        <span v-if="getKpiSecondaryLabel(demand)" class="text-[11px] text-muted">
                          {{ getKpiSecondaryLabel(demand) }}
                        </span>
                      </div>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top">
                      <div class="flex items-center justify-end gap-1">
                        <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" title="Editar demanda" @click="openEditModal(demand)" />
                        <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" title="Remover demanda" @click="promptDelete(demand)" />
                      </div>
                    </td>
                  </tr>
                </template>
              </template>

              <tr v-if="orphanEpics.length" class="bg-elevated/60">
                <td colspan="8" class="border-b border-default px-3 py-2 text-[11px] font-semibold uppercase tracking-[0.08em] text-muted">
                  Épicos sem roadmap visível
                </td>
              </tr>

              <tr
                v-for="epic in orphanEpics"
                :key="`orphan-${epic.id}`"
                class="bg-rose-50/30 hover:bg-rose-50/50 dark:bg-rose-950/10 dark:hover:bg-rose-950/20 transition-colors"
              >
                <td class="border-b border-default px-3 py-2 align-top">
                  <div class="flex items-start gap-1.5">
                    <UIcon name="i-lucide-triangle-alert" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-rose-500" />
                    <div class="min-w-0 flex-1">
                      <span class="inline-flex items-center rounded-md border border-rose-200 bg-rose-50 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-rose-700 dark:border-rose-800 dark:bg-rose-900/20 dark:text-rose-300">
                        Épico órfão
                      </span>
                      <p class="mt-0.5 truncate text-[13px] font-medium text-highlighted" :title="epic.description || undefined">{{ epic.title }}</p>
                    </div>
                  </div>
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <span class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="statusTone[epic.status]">
                    {{ statusLabels[epic.status] }}
                  </span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <div class="flex flex-wrap gap-1.5">
                    <span
                      v-for="projectName in getProjectNames(epic)"
                      :key="`orphan-${epic.id}-${projectName}`"
                      class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] text-highlighted"
                    >
                      {{ projectName }}
                    </span>
                    <span v-if="!getProjectNames(epic).length" class="text-xs text-muted">—</span>
                  </div>
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <span class="inline-flex items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium" :class="classificationBadgeClass[epic.classification]">
                    {{ classificationLabels[epic.classification] }}
                  </span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <div class="flex flex-wrap gap-1.5">
                    <a
                      v-for="issue in getDisplayIssueLinks(epic)"
                      :key="`orphan-${epic.id}-${issue.key}`"
                      :href="issue.url || undefined"
                      :target="issue.url ? '_blank' : undefined"
                      rel="noreferrer"
                      class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] font-medium text-primary transition-colors hover:border-primary/40"
                    >
                      {{ issue.key }}
                    </a>
                    <span v-if="!getDisplayIssueLinks(epic).length" class="text-xs text-muted">—</span>
                  </div>
                </td>
                <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted">
                  {{ formatDate(getDisplayedPromisedDate(epic)) }}
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <div class="flex min-w-0 flex-col items-start gap-1">
                    <button type="button" class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium transition-colors hover:opacity-80" :class="getKpiSummary(epic).tone" :title="getKpiSummary(epic).actionLabel" @click="openKpiWorkspace(epic)">
                      {{ getKpiSummary(epic).label }}
                    </button>
                    <span v-if="getKpiSecondaryLabel(epic)" class="text-[11px] text-muted">
                      {{ getKpiSecondaryLabel(epic) }}
                    </span>
                  </div>
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <div class="flex items-center justify-end gap-1">
                    <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-line-chart" title="Abrir KPIs do épico" @click="openKpiWorkspace(epic)" />
                    <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" title="Editar épico" @click="openEditModal(epic)" />
                    <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" title="Remover épico" @click="promptDelete(epic)" />
                  </div>
                </td>
              </tr>

              <tr v-if="orphanDemands.length" class="bg-elevated/60">
                <td colspan="8" class="border-b border-default px-3 py-2 text-[11px] font-semibold uppercase tracking-[0.08em] text-muted">
                  Demandas sem épico visível
                </td>
              </tr>

              <tr
                v-for="demand in orphanDemands"
                :key="`orphan-demand-${demand.id}`"
                class="bg-sky-50/20 hover:bg-sky-50/40 dark:bg-sky-950/10 dark:hover:bg-sky-950/20 transition-colors"
              >
                <td class="border-b border-default px-3 py-2 align-top">
                  <div class="flex items-start gap-1.5">
                    <UIcon name="i-lucide-link-2-off" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-sky-600" />
                    <div class="min-w-0 flex-1">
                      <div class="flex flex-wrap items-center gap-1.5">
                        <span class="inline-flex items-center rounded-md border border-sky-200 bg-sky-50 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-sky-700 dark:border-sky-800 dark:bg-sky-900/20 dark:text-sky-300">
                          Demanda órfã
                        </span>
                        <span class="inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[9px] font-medium text-muted">
                          {{ demand.quarterLabel || 'Backlog' }}
                        </span>
                      </div>
                      <p class="mt-0.5 truncate text-[13px] font-medium text-highlighted" :title="demand.description || undefined">{{ demand.title }}</p>
                    </div>
                  </div>
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <span class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="statusTone[demand.status]">
                    {{ statusLabels[demand.status] }}
                  </span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <div class="flex flex-wrap gap-1.5">
                    <span
                      v-for="projectName in getProjectNames(demand)"
                      :key="`orphan-demand-${demand.id}-${projectName}`"
                      class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] text-highlighted"
                    >
                      {{ projectName }}
                    </span>
                    <span v-if="!getProjectNames(demand).length" class="text-xs text-muted">—</span>
                  </div>
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <span class="inline-flex items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium" :class="classificationBadgeClass[demand.classification]">
                    {{ classificationLabels[demand.classification] }}
                  </span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <div class="flex flex-wrap gap-1.5">
                    <a
                      v-for="issue in getDisplayIssueLinks(demand)"
                      :key="`orphan-demand-${demand.id}-${issue.key}`"
                      :href="issue.url || undefined"
                      :target="issue.url ? '_blank' : undefined"
                      rel="noreferrer"
                      class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] font-medium text-primary transition-colors hover:border-primary/40"
                    >
                      {{ issue.key }}
                    </a>
                    <span v-if="!getDisplayIssueLinks(demand).length" class="text-xs text-muted">—</span>
                  </div>
                </td>
                <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted">
                  {{ formatDate(getDisplayedPromisedDate(demand)) }}
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <div class="flex min-w-0 flex-col items-start gap-1">
                    <button v-if="getKpiSummary(demand).clickable" type="button" class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium transition-colors hover:opacity-80" :class="getKpiSummary(demand).tone" :title="getKpiSummary(demand).actionLabel" @click="openKpiWorkspace(demand)">
                      {{ getKpiSummary(demand).label }}
                    </button>
                    <span v-else class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="getKpiSummary(demand).tone" :title="getKpiSummary(demand).actionLabel">
                      {{ getKpiSummary(demand).label }}
                    </span>
                    <span v-if="getKpiSecondaryLabel(demand)" class="text-[11px] text-muted">
                      {{ getKpiSecondaryLabel(demand) }}
                    </span>
                  </div>
                </td>
                <td class="border-b border-default px-3 py-2 align-top">
                  <div class="flex items-center justify-end gap-1">
                    <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" title="Editar demanda" @click="openEditModal(demand)" />
                    <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" title="Remover demanda" @click="promptDelete(demand)" />
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
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
      :epic-options="epicItems.map(item => ({
        id: item.id,
        title: item.title,
        roadmapTitle: item.roadmapTitle,
        projectId: item.projectId,
        projectIds: item.projectIds
      }))"
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
