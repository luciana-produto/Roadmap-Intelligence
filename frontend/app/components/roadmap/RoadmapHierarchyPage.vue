<script setup lang="ts">
import type { ApiResponse } from '~/types/api'
import type { BulkEditRoadmapItemsData, CustomerRename, DemandFormData, DemandStatus, DeprioritizationReason, RoadmapDemand, RoadmapItemType } from '~/types/roadmap'
import BulkEditRoadmapItemsModal from '~/components/roadmap/BulkEditRoadmapItemsModal.vue'
import { buildDemandDueSearchText, buildDueSortKey, hasPlannedQuarter } from '~/utils/roadmapDue'
import { getLatestPromisedDate } from '~/utils/roadmapPromisedDate'
import { BACKLOG_QUARTER } from '~/utils/roadmapQuarter'

type HierarchySortKey = 'item' | 'status' | 'products' | 'classification' | 'due'
type HierarchyColumnId = 'item' | 'status' | 'products' | 'hours' | 'classification' | 'customers' | 'due' | 'kpi' | 'actions'

type DisplayEpicGroup = {
  epic: RoadmapDemand
  demands: RoadmapDemand[]
}

type DisplayRoadmapGroup = {
  roadmap: RoadmapDemand
  epics: DisplayEpicGroup[]
}

useSeoMeta({ title: 'Roadmap · ProductHub' })

const props = defineProps<{
  projectIds?: string[]
}>()
const emit = defineEmits<{
  'update:projectIds': [value: string[]]
}>()

const CACHE_KEY_HIERARCHY_PROJECTS = 'roadmap:hierarchy:projectIds'

function readHierarchyCacheJson<T>(key: string): T | null {
  try { return JSON.parse(localStorage.getItem(key) ?? 'null') as T }
  catch { return null }
}

const route = useRoute()
const toast = useToast()
const api = useApi()
const roadmapStore = useRoadmapStore()
const kpiStore = useKpiStore()

const { projects, dependencyOptions, customerSuggestions } = storeToRefs(roadmapStore)
const { kpis: availableKpis } = storeToRefs(kpiStore)

const projectFilterOptions = computed(() =>
  [...projects.value]
    .sort((left, right) => right.name.localeCompare(left.name, 'pt-BR'))
    .map(project => ({ value: project.id, label: project.name }))
)

const modalOpen = ref(false)
const editingDemand = ref<RoadmapDemand | null>(null)
const createItemType = ref<RoadmapItemType | undefined>()
const defaultParentDemandId = ref<string | undefined>()
const defaultProjectId = ref<string | undefined>()
const defaultProjectIds = ref<string[]>([])
const defaultQuarterYear = ref<number | undefined>()
const defaultQuarterNumber = ref<number | undefined>()
const defaultType = ref<RoadmapDemand['type'] | undefined>()
const defaultHours = ref<number | undefined>()
const defaultProductIds = ref<string[]>([])
const forceSimpleEpic = ref(false)
const convertSourceEpic = ref<RoadmapDemand | null>(null)
const confirmConvertToCompositeOpen = ref(false)
const deleteTarget = ref<RoadmapDemand | null>(null)
const confirmDeleteOpen = ref(false)
const bulkEditModalOpen = ref(false)
const collapsedRoadmapIds = ref<string[]>([])
const collapsedEpicIds = ref<string[]>([])
const isSavingDemand = ref(false)
const isBulkEditing = ref(false)
const isHierarchyLoading = ref(false)
const isSavingAllHierarchyEdits = ref(false)
const hierarchyDemands = ref<RoadmapDemand[]>([])
const selectedHierarchyItemIds = ref<string[]>([])
type HierarchyInlineDraft = {
  title: string
  status: DemandStatus
  dueDate: string
  hoursInput: string
  hoursRed: boolean
  classification: RoadmapDemand['classification']
  productIds: string[]
  customers: string[]
  observation: string
  blockedReason: string
  deliveryDate: string
  deprioritizationReason?: DeprioritizationReason
  replacementDemandId?: string
}
type HierarchyEditableField = 'title' | 'status' | 'dueDate' | 'hours' | 'classification' | 'products' | 'customers'
type HierarchyActiveCell = {
  itemId: string
  field: HierarchyEditableField
}
const hierarchyInlineDrafts = ref<Record<string, HierarchyInlineDraft>>({})
const hierarchyInlineSavingIds = ref<string[]>([])
const activeHierarchyCell = ref<HierarchyActiveCell | null>(null)
const hierarchyCustomerInputs = ref<Record<string, string>>({})
const hierarchyStatusModalOpen = ref(false)
const hierarchyStatusModalItemId = ref<string | null>(null)
const hierarchyStatusModalSnapshot = ref<HierarchyInlineDraft | null>(null)
const selectedProjectIds = ref<string[]>([])
watch(selectedProjectIds, (val) => {
  localStorage.setItem(CACHE_KEY_HIERARCHY_PROJECTS, JSON.stringify(val))
})
const hierarchyItemFilter = ref('')
const hierarchyStatusFilter = ref<string[]>([])
const hierarchyClassificationFilter = ref<string[]>([])
const hierarchyProductsFilter = ref<string[]>([])
const hierarchyCustomersFilter = ref('')
const hierarchyDueFilter = ref('')
const hierarchyProblemFilter = ref<string[]>([])
const hierarchySort = ref<{ key: HierarchySortKey | null, direction: 'asc' | 'desc' }>({ key: null, direction: 'asc' })
const hierarchyTableContainerRef = ref<HTMLElement | null>(null)
const hierarchyContainerWidth = ref(0)
const hierarchyHeaderScrollLeft = ref(0)
let hierarchyWidthObserver: ResizeObserver | null = null

const HIERARCHY_COL_MIN = 30
const hierarchyColumnOrder: HierarchyColumnId[] = ['item', 'products', 'hours', 'classification', 'customers', 'status', 'due', 'kpi', 'actions']
const hierarchyColumnDefaults: Record<HierarchyColumnId, number> = {
  item: 360,
  status: 88,
  products: 128,
  hours: 56,
  classification: 104,
  customers: 128,
  due: 136,
  kpi: 64,
  actions: 28
}
const hierarchyColumnSizing = ref<Partial<Record<HierarchyColumnId, number>>>({})

// The "Item" column absorbs the free horizontal space so it starts expanded without creating a
// horizontal scrollbar (the other columns keep their widths and everything stays resizable).
const hierarchyItemExtraWidth = computed(() => {
  const total = hierarchyColumnOrder.reduce((sum, columnId) => sum + getHierarchyColSize(columnId), 0)
  return Math.max(0, hierarchyContainerWidth.value - total)
})

const deprioritizationReasonOptions = [
  { value: 'Strategic', label: 'Estratégico' },
  { value: 'MandatoryUrgent', label: 'Mandatório/Urgente' },
  { value: 'LowImpact', label: 'Baixo impacto' },
  { value: 'LackOfCapacity', label: 'Falta de capacidade' },
  { value: 'ContextChange', label: 'Mudança de contexto' },
  { value: 'Customizacao', label: 'Customização' }
] as const satisfies Array<{ value: DeprioritizationReason, label: string }>

const hierarchyProblemOptions = [
  { value: 'overdueOpen', label: 'Itens atrasados' },
  { value: 'deliveredLate', label: 'Itens entregues com atraso' },
  { value: 'noKpi', label: 'Itens sem KPIs' },
  { value: 'doneNoKpi', label: 'Concluídos sem KPI apurado' },
  { value: 'noJira', label: 'Itens sem issue Jira' },
  { value: 'noHours', label: 'Itens sem horas' }
] as const

function updateHierarchyContainerWidth() {
  hierarchyContainerWidth.value = hierarchyTableContainerRef.value?.clientWidth ?? 0
}

function syncHierarchyHeaderScroll() {
  hierarchyHeaderScrollLeft.value = hierarchyTableContainerRef.value?.scrollLeft ?? 0
}

function getHierarchyColSize(columnId: HierarchyColumnId) {
  return hierarchyColumnSizing.value[columnId] ?? hierarchyColumnDefaults[columnId]
}

function getHierarchyColWidth(columnId: HierarchyColumnId) {
  const base = getHierarchyColSize(columnId)
  return `${columnId === 'item' ? base + hierarchyItemExtraWidth.value : base}px`
}

function startHierarchyResize(columnId: HierarchyColumnId, event: MouseEvent) {
  const startX = event.clientX
  const startWidth = getHierarchyColSize(columnId)

  const onMove = (moveEvent: MouseEvent) => {
    const nextWidth = Math.max(HIERARCHY_COL_MIN, startWidth + (moveEvent.clientX - startX))
    hierarchyColumnSizing.value = {
      ...hierarchyColumnSizing.value,
      [columnId]: nextWidth
    }
  }

  const onUp = () => {
    window.removeEventListener('mousemove', onMove)
    window.removeEventListener('mouseup', onUp)
  }

  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onUp)
}

watch(hierarchyTableContainerRef, async (element) => {
  hierarchyWidthObserver?.disconnect()
  hierarchyWidthObserver = null

  if (!element || typeof ResizeObserver === 'undefined') {
    updateHierarchyContainerWidth()
    syncHierarchyHeaderScroll()
    return
  }

  updateHierarchyContainerWidth()
  syncHierarchyHeaderScroll()
  await nextTick()

  hierarchyWidthObserver = new ResizeObserver(() => {
    updateHierarchyContainerWidth()
  })
  hierarchyWidthObserver.observe(element)
}, { flush: 'post' })

onUnmounted(() => {
  hierarchyWidthObserver?.disconnect()
})

function handleHierarchyPopoverOpenChange(item: RoadmapDemand, field: Extract<HierarchyEditableField, 'products' | 'customers'>, open: boolean) {
  if (open) {
    activateHierarchyCell(item, field)
    return
  }

  deactivateHierarchyCell(item.id, field)
}

const allRoadmapItems = computed(() => hierarchyDemands.value.filter(item => item.itemType === 'Roadmap'))
const allEpicItems = computed(() => hierarchyDemands.value.filter(item => item.itemType === 'Epic'))
const allDemandItems = computed(() => hierarchyDemands.value.filter(item => item.itemType === 'Demand' && !item.successorDemandId))
const selectedProjectIdSet = computed(() => new Set(selectedProjectIds.value))
const currentPrimaryProjectId = computed(() => selectedProjectIds.value[0] ?? null)

const epicAncestorIdsFromMatchingDemands = computed(() =>
  new Set(
    allDemandItems.value
      .filter(demand => hasProjectIntersection(demand))
      .map(demand => demand.epicId)
      .filter((value): value is string => !!value)
  )
)

const roadmapAncestorIdsFromMatchingEpics = computed(() =>
  new Set(
    allEpicItems.value
      .filter(epic => hasProjectIntersection(epic) || epicAncestorIdsFromMatchingDemands.value.has(epic.id))
      .map(epic => epic.parentDemandId)
      .filter((value): value is string => !!value)
  )
)

const roadmapItems = computed(() => {
  if (!selectedProjectIdSet.value.size)
    return allRoadmapItems.value

  return allRoadmapItems.value.filter(item =>
    hasProjectIntersection(item) || roadmapAncestorIdsFromMatchingEpics.value.has(item.id)
  )
})

const visibleRoadmapIds = computed(() => new Set(roadmapItems.value.map(item => item.id)))

const epicItems = computed(() => allEpicItems.value.filter((epic) => {
  return hasProjectIntersection(epic)
    || epicAncestorIdsFromMatchingDemands.value.has(epic.id)
    || (!!epic.parentDemandId && visibleRoadmapIds.value.has(epic.parentDemandId))
}))

const visibleEpicIds = computed(() => new Set(epicItems.value.map(item => item.id)))

const demandItems = computed(() => allDemandItems.value.filter((demand) => {
  return hasProjectIntersection(demand)
    || (!!demand.epicId && visibleEpicIds.value.has(demand.epicId))
}))

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

const hasCollapsibleRoadmaps = computed(() => displayRoadmapGroups.value.some(group => group.epics.length > 0))
const hasCollapsibleEpics = computed(() => displayRoadmapGroups.value.some(group => group.epics.some(epic => epic.demands.length > 0)))
const areAllRoadmapsCollapsed = computed(() =>
  hasCollapsibleRoadmaps.value && displayRoadmapGroups.value.every(group => !group.epics.length || collapsedRoadmapIds.value.includes(group.roadmap.id))
)
const areAllEpicsCollapsed = computed(() => {
  const visibleEpics = displayRoadmapGroups.value.flatMap(group => group.epics)
  return hasCollapsibleEpics.value && visibleEpics.every(epic => !epic.demands.length || collapsedEpicIds.value.includes(epic.epic.id))
})

const projectNameById = computed(() =>
  new Map(projects.value.map(project => [project.id, project.name] as const))
)
const epicById = computed(() =>
  new Map(allEpicItems.value.map(item => [item.id, item] as const))
)

const projectFilterLabel = computed(() => {
  if (!selectedProjectIds.value.length)
    return 'Todos os times'

  if (selectedProjectIds.value.length === 1)
    return projectNameById.value.get(selectedProjectIds.value[0]!) ?? '1 time'

  return `${selectedProjectIds.value.length} times`
})

const classificationFilterOptions = computed(() =>
  Object.entries(classificationLabels).map(([value, label]) => ({ value, label }))
)

const statusFilterOptions = computed(() =>
  Object.entries(statusLabels).map(([value, label]) => ({ value, label }))
)

const statusSelectOptions = computed(() =>
  Object.entries(statusLabels).map(([value, label]) => ({ value, label }))
)

const classificationSelectOptions = computed(() =>
  Object.entries(classificationLabels).map(([value, label]) => ({ value, label }))
)

const hierarchyInlineDirtyIds = computed(() =>
  hierarchyDemands.value
    .filter(item => isHierarchyInlineDirty(item))
    .map(item => item.id)
)

const hierarchyPendingEditCount = computed(() => hierarchyInlineDirtyIds.value.length)
const hierarchyPendingEditLabel = computed(() => {
  const count = hierarchyPendingEditCount.value
  return `Salvar ${count.toLocaleString('pt-BR')} ${count === 1 ? 'edição' : 'edições'}`
})
const hierarchyStatusModalItem = computed(() =>
  hierarchyStatusModalItemId.value
    ? hierarchyDemands.value.find(item => item.id === hierarchyStatusModalItemId.value) ?? null
    : null
)
const hierarchyStatusModalDraft = computed(() =>
  hierarchyStatusModalItem.value ? getHierarchyInlineDraft(hierarchyStatusModalItem.value) : null
)
const hierarchyStatusModalRequiresDeliveryDate = computed(() => hierarchyStatusModalDraft.value?.status === 'Done')
const hierarchyStatusModalRequiresBlockedReason = computed(() => hierarchyStatusModalDraft.value?.status === 'Blocked')
const hierarchyStatusModalRequiresDeprioritization = computed(() => hierarchyStatusModalDraft.value?.status === 'Deprioritized')
const hierarchyStatusReplacementDemandOptions = computed(() => {
  const currentItemId = hierarchyStatusModalItem.value?.id

  return dependencyOptions.value
    .filter(option => option.demandId !== currentItemId)
    .map(option => ({
      value: option.demandId,
      label: `${option.projectName} · ${option.title}`
    }))
})

const productFilterOptions = computed(() => {
  const productsMap = new Map<string, string>()

  hierarchyDemands.value.forEach((item) => {
    getProductEntries(item).forEach((product) => {
      if (!productsMap.has(product.value))
        productsMap.set(product.value, product.label)
    })
  })

  return Array.from(productsMap.entries())
    .map(([value, label]) => ({ value, label }))
    .sort((left, right) => left.label.localeCompare(right.label, 'pt-BR'))
})

const hierarchyStatusFilterLabel = computed(() => {
  if (!hierarchyStatusFilter.value.length)
    return 'Todos'

  if (hierarchyStatusFilter.value.length === 1)
    return statusLabels[hierarchyStatusFilter.value[0] as DemandStatus] ?? '1 status'

  return `${hierarchyStatusFilter.value.length} status`
})

const hierarchyClassificationFilterLabel = computed(() => {
  if (!hierarchyClassificationFilter.value.length)
    return 'Todas'

  if (hierarchyClassificationFilter.value.length === 1)
    return classificationLabels[hierarchyClassificationFilter.value[0] as RoadmapDemand['classification']] ?? '1 classificação'

  return `${hierarchyClassificationFilter.value.length} classificações`
})

const hierarchyProductsFilterLabel = computed(() => {
  if (!hierarchyProductsFilter.value.length)
    return 'Todos'

  if (hierarchyProductsFilter.value.length === 1)
    return productFilterOptions.value.find(option => option.value === hierarchyProductsFilter.value[0])?.label ?? '1 produto'

  return `${hierarchyProductsFilter.value.length} produtos`
})

const hierarchyProblemFilterLabel = computed(() => {
  if (!hierarchyProblemFilter.value.length)
    return 'Problemas'

  if (hierarchyProblemFilter.value.includes('__all__'))
    return 'Todos os problemas'

  if (hierarchyProblemFilter.value.length === 1)
    return hierarchyProblemOptions.find(option => option.value === hierarchyProblemFilter.value[0])?.label ?? '1 problema'

  return `${hierarchyProblemFilter.value.length} problemas`
})

const statusLabels: Record<DemandStatus, string> = {
  Backlog: 'Backlog',
  InProgress: 'Doing',
  Done: 'Concluído',
  Deprioritized: 'Despriorizado',
  Blocked: 'Impedido'
}

const statusTone: Record<DemandStatus, string> = {
  Backlog: 'border-default bg-elevated text-muted',
  InProgress: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-800 dark:bg-blue-900/20 dark:text-blue-300',
  Done: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-800 dark:bg-emerald-900/20 dark:text-emerald-300',
  Deprioritized: 'border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-800 dark:bg-rose-900/20 dark:text-rose-300',
  Blocked: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-800 dark:bg-amber-900/20 dark:text-amber-300'
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

const classificationDisplayLabels: Record<RoadmapDemand['classification'], string> = {
  TechnicalDebtSecurity: 'Déb. Técnico',
  Strategic: 'Estratégico',
  Evolution: 'Evolução',
  ImprovementGap: 'Melhoria/Gap',
  Mandatory: 'Mandatório',
  Homologation: 'Homologação',
  Customizacao: 'Customização'
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

const hierarchyDisplayMenuItems = computed(() => [[
  {
    label: areAllRoadmapsCollapsed.value ? 'Expandir roadmaps' : 'Recolher roadmaps',
    icon: 'i-lucide-chevrons-up-down',
    disabled: !hasCollapsibleRoadmaps.value,
    onSelect: () => {
      if (areAllRoadmapsCollapsed.value)
        expandAllRoadmaps()
      else
        collapseAllRoadmaps()
    }
  },
  {
    label: areAllEpicsCollapsed.value ? 'Expandir épicos' : 'Recolher épicos',
    icon: 'i-lucide-chevrons-up-down',
    disabled: !hasCollapsibleEpics.value,
    onSelect: () => {
      if (areAllEpicsCollapsed.value)
        expandAllEpics()
      else
        collapseAllEpics()
    }
  },
  {
    label: 'Recolher tudo',
    icon: 'i-lucide-fold-vertical',
    disabled: !hasCollapsibleRoadmaps.value && !hasCollapsibleEpics.value,
    onSelect: () => {
      collapseAllRoadmaps()
      collapseAllEpics()
    }
  },
  {
    label: 'Expandir tudo',
    icon: 'i-lucide-unfold-vertical',
    disabled: !collapsedRoadmapIds.value.length && !collapsedEpicIds.value.length,
    onSelect: () => {
      expandAllRoadmaps()
      expandAllEpics()
    }
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

function formatShortDate(value?: string) {
  if (!value)
    return '—'

  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day)
    return value

  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit',
    month: '2-digit',
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

function isOutsideSelectedProject(item: Pick<RoadmapDemand, 'projectId' | 'projectIds'>) {
  if (!selectedProjectIdSet.value.size)
    return false

  const ownerProjectId = item.projectId ?? item.projectIds?.[0]

  return !!ownerProjectId && !selectedProjectIdSet.value.has(ownerProjectId)
}

function getCrossProjectWatermarkClass(item: Pick<RoadmapDemand, 'projectId' | 'projectIds'>) {
  return isOutsideSelectedProject(item)
    ? 'opacity-55 saturate-75'
    : ''
}

function getProjectNames(item: Pick<RoadmapDemand, 'projectId' | 'projectIds'>) {
  const ids = getItemProjectIds(item)

  return ids
    .map(id => projectNameById.value.get(id) ?? '')
    .filter(Boolean)
}

function getProductEntries(item: Pick<RoadmapDemand, 'products'>) {
  const productsMap = new Map<string, string>()

  for (const product of item.products ?? []) {
    if (!product.productId) continue

    let name = product.name
    if (!name) {
      name = projects.value.flatMap(p => p.products).find(p => p.id === product.productId)?.name ?? ''
    }
    if (!name) continue

    if (!productsMap.has(product.productId))
      productsMap.set(product.productId, name)
  }

  return Array.from(productsMap.entries()).map(([value, label]) => ({ value, label }))
}

function getProductNames(item: Pick<RoadmapDemand, 'products'>) {
  return getProductEntries(item).map(product => product.label)
}

function getEditableProductOptions(item: Pick<RoadmapDemand, 'projectId' | 'projectIds'>) {
  const optionsMap = new Map<string, string>()

  for (const projectId of getItemProjectIds(item)) {
    const project = projects.value.find(currentProject => currentProject.id === projectId)
    for (const product of project?.products ?? []) {
      if (!product.id || !product.name)
        continue

      if (!optionsMap.has(product.id))
        optionsMap.set(product.id, product.name)
    }
  }

  return Array.from(optionsMap.entries()).map(([value, label]) => ({ value, label }))
}

function normalizeCustomers(customers?: string[]) {
  return (customers ?? []).map(customer => customer.trim()).filter(Boolean)
}

function formatCustomersInput(customers?: string[]) {
  return normalizeCustomers(customers).join(', ')
}

function parseCustomersInput(value: string) {
  return Array.from(new Set(
    value
      .split(/[\n,;]+/)
      .map(customer => customer.trim())
      .filter(Boolean)
  ))
}

const INLINE_LIST_AVG_CHAR_PX = 6.4
const INLINE_LIST_SIDE_PADDING_PX = 22
const INLINE_LIST_MORE_BADGE_PX = 28

function getDisplayedCustomers(item: RoadmapDemand) {
  if (item.itemType === 'Epic')
    return normalizeCustomers(item.customers)

  if (item.itemType === 'Demand' && item.epicId)
    return normalizeCustomers(epicById.value.get(item.epicId)?.customers)

  return normalizeCustomers(item.customers)
}

function getDemandProblemKeys(item: RoadmapDemand) {
  // Epics (simple or composite) have their own set of problems.
  if (item.itemType === 'Epic') {
    const epicKeys: string[] = []

    if (item.hasNoKpi || item.kpiLinks.length === 0)
      epicKeys.push('noKpi')

    if (!getDisplayIssueLinks(item).length)
      epicKeys.push('noJira')

    if (item.isSimple) {
      if (item.hours == null)
        epicKeys.push('noHours')
      if (item.status !== 'Done' && item.isOverdue)
        epicKeys.push('overdueOpen')
      if (item.status === 'Done' && item.isDeliveredLate)
        epicKeys.push('deliveredLate')
    }

    if (item.status === 'Done' && !item.hasNoKpi && item.kpiLinks.length > 0
      && (item.kpiMeasurements?.length ?? 0) === 0)
      epicKeys.push('doneNoKpi')

    return epicKeys
  }

  if (item.itemType !== 'Demand')
    return []

  const keys: string[] = []
  const targetEpic = getKpiTargetEpic(item)

  if (item.status !== 'Done' && item.isOverdue)
    keys.push('overdueOpen')

  if (item.status === 'Done' && item.isDeliveredLate)
    keys.push('deliveredLate')

  if (!targetEpic || targetEpic.hasNoKpi || targetEpic.kpiLinks.length === 0)
    keys.push('noKpi')

  if (!getDisplayIssueLinks(item).length)
    keys.push('noJira')

  if (item.hours == null)
    keys.push('noHours')

  const targetEpicForDoneKpi = getKpiTargetEpic(item)
  if (item.status === 'Done' && targetEpicForDoneKpi && targetEpicForDoneKpi.status === 'Done' && !targetEpicForDoneKpi.hasNoKpi && targetEpicForDoneKpi.kpiLinks.length > 0) {
    const hasApuratedKpi = (targetEpicForDoneKpi.kpiMeasurements?.length ?? 0) > 0
    if (!hasApuratedKpi)
      keys.push('doneNoKpi')
  }

  return keys
}

const problemLabels: Record<string, string> = {
  overdueOpen: 'Item atrasado',
  deliveredLate: 'Entregue com atraso',
  noKpi: 'Sem KPIs associados',
  doneNoKpi: 'Concluído sem KPI apurado',
  noJira: 'Sem issue Jira associada',
  noHours: 'Sem horas estimadas',
}

function getDemandProblemTooltip(item: RoadmapDemand) {
  return getDemandProblemKeys(item).map(k => problemLabels[k] ?? k).join('\n')
}

function getCustomersLine(customers: string[]) {
  return customers.join(' · ')
}

function getAdaptiveInlineListDisplay(items: string[], columnWidth: number, separator = ' · ') {
  const normalizedItems = items.filter(Boolean)
  const fullLabel = normalizedItems.join(separator)

  if (!normalizedItems.length) {
    return {
      items: normalizedItems,
      visibleItems: [] as string[],
      hiddenCount: 0,
      previewLabel: '',
      fullLabel,
      allVisible: true
    }
  }

  const availableWidth = Math.max(columnWidth - INLINE_LIST_SIDE_PADDING_PX, 36)
  const visibleItems: string[] = []
  let usedWidth = 0

  for (let index = 0; index < normalizedItems.length; index++) {
    const item = normalizedItems[index]!
    const remainingAfterCurrent = normalizedItems.length - index - 1
    const reserveForMore = remainingAfterCurrent > 0
      ? INLINE_LIST_MORE_BADGE_PX + String(remainingAfterCurrent).length * INLINE_LIST_AVG_CHAR_PX
      : 0
    const chunk = `${visibleItems.length ? separator : ''}${item}`
    const nextWidth = usedWidth + chunk.length * INLINE_LIST_AVG_CHAR_PX

    if (visibleItems.length && nextWidth + reserveForMore > availableWidth)
      break

    visibleItems.push(item)
    usedWidth = nextWidth
  }

  if (!visibleItems.length)
    visibleItems.push(normalizedItems[0]!)

  const hiddenCount = Math.max(normalizedItems.length - visibleItems.length, 0)

  return {
    items: normalizedItems,
    visibleItems,
    hiddenCount,
    previewLabel: visibleItems.join(separator),
    fullLabel,
    allVisible: hiddenCount === 0
  }
}

function getCustomerCellDisplay(item: RoadmapDemand) {
  return getAdaptiveInlineListDisplay(getDisplayedCustomers(item), getHierarchyColSize('customers'))
}

function getProductCellDisplay(names: string[]) {
  return getAdaptiveInlineListDisplay(names, getHierarchyColSize('products'))
}

function getDueDateLabel(item: RoadmapDemand) {
  const date = getDisplayedConclusionDate(item)
  return date ? formatShortDate(date) : ''
}

function getDueQuarterLabel(item: RoadmapDemand) {
  return hasPlannedQuarter(item) && item.quarterLabel ? item.quarterLabel : ''
}

function getDueTooltip(item: RoadmapDemand) {
  const dateLabel = getDueDateLabel(item)
  const quarterLabel = getDueQuarterLabel(item)
  const parts = [
    dateLabel ? `Data: ${dateLabel}` : '',
    quarterLabel ? `Quarter: ${quarterLabel}` : '',
    isDelayed(item) ? 'Atrasado' : ''
  ].filter(Boolean)

  return parts.join(' · ') || 'Sem conclusão definida'
}

function getClassificationDisplayLabel(classification: RoadmapDemand['classification']) {
  return classificationDisplayLabels[classification] ?? classificationLabels[classification]
}

function getDueDateTone(item: RoadmapDemand) {
  return item.status === 'Done' && item.deliveryDate
    ? 'text-green-600 dark:text-green-400'
    : 'text-muted'
}

function getDueDateClass(item: RoadmapDemand) {
  if (isDelayed(item)) {
    return 'inline-flex shrink-0 items-center rounded-md border border-amber-300 bg-amber-50/80 px-1.5 py-0.5 text-[10px] font-medium text-amber-700 dark:border-amber-700 dark:bg-amber-900/20 dark:text-amber-300'
  }

  return `truncate ${getDueDateTone(item)}`
}

function getRoadmapGroupCustomerNames(epics: RoadmapDemand[]) {
  return Array.from(new Set(
    epics.flatMap(epic => [
      ...getDisplayedCustomers(epic),
      ...getDemandsForEpic(epic.id).flatMap(demand => getDisplayedCustomers(demand))
    ])
  ))
}

function getProductNamesLine(names: string[]) {
  return names.join(' · ')
}

function getRoadmapGroupProductNames(roadmap: RoadmapDemand, epics: RoadmapDemand[]) {
  return getRoadmapGroupProductEntries(roadmap, epics).map(product => product.label)
}

function getRoadmapGroupProductEntries(roadmap: RoadmapDemand, epics: RoadmapDemand[]) {
  const productsMap = new Map<string, string>()

  ;[
    ...getProductEntries(roadmap),
    ...epics.flatMap(epic => [
      ...getProductEntries(epic),
      ...getDemandsForEpic(epic.id).flatMap(demand => getProductEntries(demand))
    ])
  ].forEach((product) => {
    if (!productsMap.has(product.value))
      productsMap.set(product.value, product.label)
  })

  return Array.from(productsMap.entries()).map(([value, label]) => ({ value, label }))
}

function getEpicDisplayProductNames(epic: RoadmapDemand) {
  return getEpicDisplayProductEntries(epic).map(product => product.label)
}

function getEpicDisplayProductEntries(epic: RoadmapDemand) {
  const productsMap = new Map<string, string>()
  const epicProducts = getProductEntries(epic)
  const demandProducts = getDemandsForEpic(epic.id).flatMap(demand => getProductEntries(demand))

  ;[...epicProducts, ...demandProducts].forEach((product) => {
    if (!productsMap.has(product.value))
      productsMap.set(product.value, product.label)
  })

  return Array.from(productsMap.entries()).map(([value, label]) => ({ value, label }))
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
    label: '+KPI',
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

function getDisplayedClassification(item: RoadmapDemand) {
  if (item.itemType !== 'Demand')
    return item.classification

  if (!item.epicId)
    return item.classification

  return epicById.value.get(item.epicId)?.classification ?? item.classification
}

function getDemandsForEpic(epicId: string) {
  return demandItems.value
    .filter(demand => demand.epicId === epicId)
    .sort(comparePlanningPriority)
}

function comparePlanningPriority(left: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'sortOrder' | 'title'>, right: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'sortOrder' | 'title'>) {
  if (left.quarterYear !== right.quarterYear)
    return left.quarterYear - right.quarterYear

  if (left.quarterNumber !== right.quarterNumber)
    return left.quarterNumber - right.quarterNumber

  if (left.sortOrder !== right.sortOrder)
    return left.sortOrder - right.sortOrder

  return left.title.localeCompare(right.title, 'pt-BR')
}

function getItemPlanningAnchor(item: RoadmapDemand) {
  if (item.itemType === 'Demand')
    return item

  if (item.itemType === 'Epic')
    return getDemandsForEpic(item.id)[0] ?? item

  return demandItems.value
    .filter(demand => demand.roadmapId === item.id)
    .sort(comparePlanningPriority)[0] ?? item
}

function getDisplayedHours(item: RoadmapDemand) {
  const values = (item.itemType === 'Roadmap'
    ? demandItems.value.filter(demand => demand.roadmapId === item.id)
    : item.itemType === 'Epic'
      ? (item.isSimple ? [item] : getDemandsForEpic(item.id))
      : [item])
    .map(entry => entry.hours)
    .filter((value): value is number => typeof value === 'number')

  if (!values.length)
    return null

  return values.reduce((total, value) => total + value, 0)
}

function normalizeSearchText(value?: string | null) {
  return (value ?? '').trim().toLowerCase()
}

function getDemandDueSearchText(demand: RoadmapDemand) {
  return buildDemandDueSearchText(demand, formatDate(getDisplayedConclusionDate(demand)))
}

function getDisplayedConclusionDate(item: RoadmapDemand) {
  if (item.status === 'Done' && item.deliveryDate)
    return item.deliveryDate

  return getDisplayedPromisedDate(item)
}

function isDelayed(item: RoadmapDemand) {
  return item.isOverdue || item.isDeliveredLate
}

function matchesTextFilter(haystackParts: Array<string | undefined>, query: string) {
  if (!query)
    return true

  return haystackParts.some(part => normalizeSearchText(part).includes(query))
}

function matchesHierarchyFilters(
  item: RoadmapDemand,
  options?: { products?: string[], classification?: string, customerText?: string, dueText?: string }
) {
  const itemQuery = normalizeSearchText(hierarchyItemFilter.value)
  const customerQuery = normalizeSearchText(hierarchyCustomersFilter.value)
  const dueQuery = normalizeSearchText(hierarchyDueFilter.value)

  if (hierarchyStatusFilter.value.length && !hierarchyStatusFilter.value.includes(item.status))
    return false

  if (hierarchyClassificationFilter.value.length && item.itemType !== 'Roadmap') {
    const classification = item.itemType === 'Demand'
      ? getDisplayedClassification(item)
      : item.classification
    if (!hierarchyClassificationFilter.value.includes(classification))
      return false
  }

  if (hierarchyClassificationFilter.value.length && item.itemType === 'Roadmap')
    return false

  if (!matchesTextFilter([
    item.title,
    item.description,
    ...getProjectNames(item),
    ...getDisplayIssueLinks(item).map(issue => issue.key)
  ], itemQuery))
    return false

  if (hierarchyProductsFilter.value.length && !hierarchyProductsFilter.value.some(productId => options?.products?.includes(productId)))
    return false

  if (hierarchyProblemFilter.value.length) {
    // Problems apply to demands and epics (roadmaps have none).
    if (item.itemType === 'Roadmap')
      return false

    const problemKeys = getDemandProblemKeys(item)
    const isAllProblems = hierarchyProblemFilter.value.includes('__all__')
    if (isAllProblems) {
      if (problemKeys.length === 0)
        return false
    }
    else if (!hierarchyProblemFilter.value.some(problem => problemKeys.includes(problem))) {
      return false
    }
  }

  if (!matchesTextFilter([options?.customerText], customerQuery))
    return false

  if (!matchesTextFilter([options?.dueText], dueQuery))
    return false

  return true
}

function compareText(left: string, right: string) {
  return left.localeCompare(right, 'pt-BR')
}

function compareDates(left?: string, right?: string) {
  return (left ?? '').localeCompare(right ?? '')
}

function applySortDirection(result: number) {
  return hierarchySort.value.direction === 'asc' ? result : -result
}

function sortItems(items: RoadmapDemand[], level: 'roadmap' | 'epic' | 'demand') {
  if (!hierarchySort.value.key) {
    if (level === 'demand') {
      return [...items].sort(comparePlanningPriority)
    }

    return [...items].sort((left, right) => {
      const planningComparison = comparePlanningPriority(getItemPlanningAnchor(left), getItemPlanningAnchor(right))
      if (planningComparison !== 0)
        return planningComparison

      if (left.sortOrder !== right.sortOrder)
        return left.sortOrder - right.sortOrder

      return left.title.localeCompare(right.title, 'pt-BR')
    })
  }

  return [...items].sort((left, right) => {
    switch (hierarchySort.value.key) {
      case 'item':
        return applySortDirection(compareText(left.title, right.title))
      case 'status':
        return applySortDirection(compareText(statusLabels[left.status], statusLabels[right.status]))
      case 'products':
        return applySortDirection(compareText(
          getProductNamesLine(left.itemType === 'Roadmap'
            ? getRoadmapGroupProductNames(left, roadmapGroups.value.find(group => group.roadmap.id === left.id)?.epics ?? [])
            : left.itemType === 'Epic'
              ? getEpicDisplayProductNames(left)
              : getProductNames(left)),
          getProductNamesLine(right.itemType === 'Roadmap'
            ? getRoadmapGroupProductNames(right, roadmapGroups.value.find(group => group.roadmap.id === right.id)?.epics ?? [])
            : right.itemType === 'Epic'
              ? getEpicDisplayProductNames(right)
              : getProductNames(right))
        ))
      case 'classification': {
        const leftClassification = left.itemType === 'Demand' ? classificationLabels[getDisplayedClassification(left)] : classificationLabels[left.classification] ?? ''
        const rightClassification = right.itemType === 'Demand' ? classificationLabels[getDisplayedClassification(right)] : classificationLabels[right.classification] ?? ''
        return applySortDirection(compareText(leftClassification, rightClassification))
      }
      case 'due': {
        const leftDue = buildDueSortKey(getDisplayedConclusionDate(left), left)
        const rightDue = buildDueSortKey(getDisplayedConclusionDate(right), right)
        return applySortDirection(compareDates(leftDue, rightDue))
      }
      default:
        return 0
    }
  })
}

function toggleHierarchySort(key: HierarchySortKey) {
  if (hierarchySort.value.key !== key) {
    hierarchySort.value = { key, direction: 'asc' }
    return
  }

  if (hierarchySort.value.direction === 'asc') {
    hierarchySort.value = { key, direction: 'desc' }
    return
  }

  hierarchySort.value = { key: null, direction: 'asc' }
}

function getHierarchySortIcon(key: HierarchySortKey) {
  if (hierarchySort.value.key !== key)
    return 'i-lucide-arrow-up-down'

  return hierarchySort.value.direction === 'asc'
    ? 'i-lucide-arrow-up'
    : 'i-lucide-arrow-down'
}

const displayRoadmapGroups = computed<DisplayRoadmapGroup[]>(() => {
  return sortItems(roadmapItems.value, 'roadmap')
    .map((roadmap) => {
      const sourceGroup = roadmapGroups.value.find(group => group.roadmap.id === roadmap.id)
      const sourceEpics = sourceGroup?.epics ?? []
      const roadmapProjectMatch = hasProjectIntersection(roadmap)
      const hasActiveProductFilter = hierarchyProductsFilter.value.length > 0
      const hasActiveCustomerFilter = !!normalizeSearchText(hierarchyCustomersFilter.value)
      const hasActiveProblemFilter = hierarchyProblemFilter.value.length > 0
      const requiresChildMatch = hasActiveProductFilter || hasActiveCustomerFilter || hasActiveProblemFilter
      const roadmapMatches = matchesHierarchyFilters(roadmap, {
        products: getRoadmapGroupProductEntries(roadmap, sourceEpics).map(product => product.value),
        customerText: getCustomersLine(getRoadmapGroupCustomerNames(sourceEpics)),
        dueText: formatDate(getDisplayedConclusionDate(roadmap))
      })

      const epics = sortItems(sourceEpics, 'epic')
        .map((epic) => {
          const sourceDemands = getDemandsForEpic(epic.id)
          const epicProjectMatch = hasProjectIntersection(epic)
          const projectScopedDemands = roadmapProjectMatch || epicProjectMatch
            ? sourceDemands
            : sourceDemands.filter(demand => hasProjectIntersection(demand))

          if (!roadmapProjectMatch && !epicProjectMatch && projectScopedDemands.length === 0)
            return null

          const epicMatches = matchesHierarchyFilters(epic, {
            products: getEpicDisplayProductEntries(epic).map(product => product.value),
            classification: classificationLabels[epic.classification],
            customerText: getCustomersLine(getDisplayedCustomers(epic)),
            dueText: formatDate(getDisplayedConclusionDate(epic))
          })

          const matchingDemands = sortItems(projectScopedDemands, 'demand')
            .filter(demand => matchesHierarchyFilters(demand, {
              products: getProductEntries(demand).map(product => product.value),
              classification: classificationLabels[getDisplayedClassification(demand)],
              customerText: getCustomersLine(getDisplayedCustomers(demand)),
              dueText: getDemandDueSearchText(demand)
            }))

          if (requiresChildMatch && !epicMatches && matchingDemands.length === 0)
            return null

          if (!roadmapMatches && !epicMatches && matchingDemands.length === 0)
            return null

          return {
            epic,
            demands: requiresChildMatch
              ? matchingDemands
              : (roadmapMatches || epicMatches ? sortItems(projectScopedDemands, 'demand') : matchingDemands)
          }
        })
        .filter((entry): entry is DisplayEpicGroup => !!entry)

      if ((!roadmapMatches && epics.length === 0) || (requiresChildMatch && epics.length === 0))
        return null

      return { roadmap, epics }
    })
    .filter((group): group is DisplayRoadmapGroup => !!group)
})

const displayOrphanEpics = computed(() =>
  sortItems(orphanEpics.value, 'epic').filter(epic => matchesHierarchyFilters(epic, {
    products: getEpicDisplayProductEntries(epic).map(product => product.value),
    classification: classificationLabels[epic.classification],
    customerText: getCustomersLine(getDisplayedCustomers(epic)),
    dueText: formatDate(getDisplayedConclusionDate(epic))
  }))
)

const displayOrphanDemands = computed(() =>
  sortItems(orphanDemands.value, 'demand').filter(demand => matchesHierarchyFilters(demand, {
    products: getProductEntries(demand).map(product => product.value),
    classification: classificationLabels[getDisplayedClassification(demand)],
    customerText: getCustomersLine(getDisplayedCustomers(demand)),
    dueText: getDemandDueSearchText(demand)
  }))
)

const visibleHierarchySelectableItems = computed(() => {
  const items = new Map<string, RoadmapDemand>()

  displayRoadmapGroups.value.forEach((group) => {
    group.epics.forEach((entry) => {
      items.set(entry.epic.id, entry.epic)
      entry.demands.forEach(demand => items.set(demand.id, demand))
    })
  })

  displayOrphanEpics.value.forEach(epic => items.set(epic.id, epic))
  displayOrphanDemands.value.forEach(demand => items.set(demand.id, demand))

  return Array.from(items.values())
})

const selectedHierarchyItems = computed(() => {
  const selectedIds = new Set(selectedHierarchyItemIds.value)
  return visibleHierarchySelectableItems.value.filter(item => selectedIds.has(item.id))
})

const selectedHierarchyItemCount = computed(() => selectedHierarchyItems.value.length)

watch(visibleHierarchySelectableItems, (items) => {
  const validIds = new Set(items.map(item => item.id))
  selectedHierarchyItemIds.value = selectedHierarchyItemIds.value.filter(id => validIds.has(id))
}, { immediate: true })

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
  collapsedRoadmapIds.value = displayRoadmapGroups.value
    .filter(group => group.epics.length > 0)
    .map(group => group.roadmap.id)
}

function expandAllRoadmaps() {
  collapsedRoadmapIds.value = []
}

function collapseAllEpics() {
  collapsedEpicIds.value = displayRoadmapGroups.value
    .flatMap(group => group.epics)
    .filter(group => group.demands.length > 0)
    .map(group => group.epic.id)
}

function expandAllEpics() {
  collapsedEpicIds.value = []
}

watch(displayRoadmapGroups, (groups) => {
  const validIds = new Set(groups.map(group => group.roadmap.id))
  collapsedRoadmapIds.value = collapsedRoadmapIds.value.filter(id => validIds.has(id))
}, { immediate: true })

watch(displayRoadmapGroups, (groups) => {
  const validIds = new Set(groups.flatMap(group => group.epics.map(item => item.epic.id)))
  collapsedEpicIds.value = collapsedEpicIds.value.filter(id => validIds.has(id))
}, { immediate: true })

async function loadPageData() {
  isHierarchyLoading.value = true
  try {
    const response = await api.get<ApiResponse<RoadmapDemand[]>>('/api/roadmap/demands')
    hierarchyDemands.value = response.data ?? []

    await Promise.all([
      roadmapStore.fetchDependencyOptions(),
      roadmapStore.fetchCustomerSuggestions(),
      kpiStore.fetchKpis()
    ])
  }
  finally {
    isHierarchyLoading.value = false
  }
}

function getItemProjectIds(item?: Pick<RoadmapDemand, 'projectId' | 'projectIds'> | null) {
  if (!item)
    return []

  return [...new Set([
    ...(item.projectId ? [item.projectId] : []),
    ...(item.projectIds ?? [])
  ])]
}

function hasProjectIntersection(item?: Pick<RoadmapDemand, 'projectId' | 'projectIds'> | null) {
  if (!selectedProjectIdSet.value.size)
    return true

  return getItemProjectIds(item).some(projectId => selectedProjectIdSet.value.has(projectId))
}

function pickDefaultProjectId(projectIds: string[]) {
  if (currentPrimaryProjectId.value && projectIds.includes(currentPrimaryProjectId.value))
    return currentPrimaryProjectId.value

  return projectIds[0]
}

function toggleProjectFilter(projectId: string) {
  if (selectedProjectIds.value.includes(projectId)) {
    selectedProjectIds.value = selectedProjectIds.value.filter(id => id !== projectId)
  }
  else {
    selectedProjectIds.value = [...selectedProjectIds.value, projectId]
  }
  emit('update:projectIds', selectedProjectIds.value)
}

function clearProjectFilter() {
  selectedProjectIds.value = []
  emit('update:projectIds', selectedProjectIds.value)
}

function toggleHierarchyStatusFilter(status: string) {
  if (hierarchyStatusFilter.value.includes(status)) {
    hierarchyStatusFilter.value = hierarchyStatusFilter.value.filter(value => value !== status)
    return
  }

  hierarchyStatusFilter.value = [...hierarchyStatusFilter.value, status]
}

function clearHierarchyStatusFilter() {
  hierarchyStatusFilter.value = []
}

function toggleHierarchyProductsFilter(productId: string) {
  if (hierarchyProductsFilter.value.includes(productId)) {
    hierarchyProductsFilter.value = hierarchyProductsFilter.value.filter(value => value !== productId)
    return
  }

  hierarchyProductsFilter.value = [...hierarchyProductsFilter.value, productId]
}

function clearHierarchyProductsFilter() {
  hierarchyProductsFilter.value = []
}

function toggleHierarchyProblemFilter(problem: string) {
  if (hierarchyProblemFilter.value.includes(problem)) {
    hierarchyProblemFilter.value = hierarchyProblemFilter.value.filter(value => value !== problem)
    return
  }
  // Selecting __all__ clears specific selections and vice versa
  if (problem === '__all__') {
    hierarchyProblemFilter.value = ['__all__']
    return
  }
  hierarchyProblemFilter.value = [...hierarchyProblemFilter.value.filter(v => v !== '__all__'), problem]
}

function clearHierarchyProblemFilter() {
  hierarchyProblemFilter.value = []
}

function toggleHierarchyClassificationFilter(classification: string) {
  if (hierarchyClassificationFilter.value.includes(classification)) {
    hierarchyClassificationFilter.value = hierarchyClassificationFilter.value.filter(value => value !== classification)
    return
  }

  hierarchyClassificationFilter.value = [...hierarchyClassificationFilter.value, classification]
}

function clearHierarchyClassificationFilter() {
  hierarchyClassificationFilter.value = []
}

function isHierarchyItemSelected(itemId: string) {
  return selectedHierarchyItemIds.value.includes(itemId)
}

function toggleHierarchyItemSelection(itemId: string, selected: boolean) {
  if (selected) {
    if (!selectedHierarchyItemIds.value.includes(itemId))
      selectedHierarchyItemIds.value = [...selectedHierarchyItemIds.value, itemId]

    return
  }

  selectedHierarchyItemIds.value = selectedHierarchyItemIds.value.filter(id => id !== itemId)
}

function clearHierarchySelection() {
  selectedHierarchyItemIds.value = []
}

function cloneHierarchyInlineDraft(draft: HierarchyInlineDraft): HierarchyInlineDraft {
  return {
    ...draft,
    productIds: [...draft.productIds],
    customers: [...draft.customers]
  }
}

function requiresHierarchyStatusDetails(status: DemandStatus) {
  return status === 'Done' || status === 'Blocked' || status === 'Deprioritized'
}

function createHierarchyInlineDraft(item: RoadmapDemand): HierarchyInlineDraft {
  return {
    title: item.title,
    status: item.status,
    dueDate: item.status === 'Done' ? (item.deliveryDate ?? '') : (item.effectivePromisedDate ?? item.promisedDate ?? ''),
    hoursInput: (item.itemType === 'Demand' || item.isSimple) && item.hours != null ? String(item.hours) : '',
    hoursRed: (item.itemType === 'Demand' || item.isSimple) ? (item.hoursRed ?? false) : false,
    classification: item.classification,
    productIds: getProductEntries(item).map(product => product.value),
    customers: getDisplayedCustomers(item),
    observation: item.observation ?? '',
    blockedReason: item.blockedReason ?? '',
    deliveryDate: item.deliveryDate ?? '',
    deprioritizationReason: item.deprioritizationReason ?? undefined,
    replacementDemandId: item.replacementDemandId ?? undefined
  }
}

function getHierarchyInlineDraft(item: RoadmapDemand) {
  return hierarchyInlineDrafts.value[item.id] ?? createHierarchyInlineDraft(item)
}

function updateHierarchyInlineDraft(item: RoadmapDemand, patch: Partial<HierarchyInlineDraft>) {
  hierarchyInlineDrafts.value = {
    ...hierarchyInlineDrafts.value,
    [item.id]: {
      ...getHierarchyInlineDraft(item),
      ...patch
    }
  }
}

function clearHierarchyInlineDraft(itemId: string) {
  const { [itemId]: _removed, ...rest } = hierarchyInlineDrafts.value
  hierarchyInlineDrafts.value = rest
}

function clearAllHierarchyInlineDrafts() {
  hierarchyInlineDrafts.value = {}
}

function closeHierarchyStatusModal(options?: { restoreSnapshot?: boolean }) {
  const restoreSnapshot = options?.restoreSnapshot ?? false
  const item = hierarchyStatusModalItem.value
  const snapshot = hierarchyStatusModalSnapshot.value

  if (restoreSnapshot && item && snapshot) {
    hierarchyInlineDrafts.value = {
      ...hierarchyInlineDrafts.value,
      [item.id]: cloneHierarchyInlineDraft(snapshot)
    }
  }

  hierarchyStatusModalOpen.value = false
  hierarchyStatusModalItemId.value = null
  hierarchyStatusModalSnapshot.value = null
}

function openHierarchyStatusModal(item: RoadmapDemand, status: Extract<DemandStatus, 'Done' | 'Blocked' | 'Deprioritized'>) {
  const draft = getHierarchyInlineDraft(item)

  hierarchyStatusModalSnapshot.value = cloneHierarchyInlineDraft(draft)
  hierarchyStatusModalItemId.value = item.id
  hierarchyStatusModalOpen.value = true

  updateHierarchyInlineDraft(item, {
    status,
    dueDate: (draft.status === 'Done' && status !== 'Done') ? (item.promisedDate ?? '') : draft.dueDate,
    deliveryDate: status === 'Done' ? (draft.deliveryDate || item.deliveryDate || '') : draft.deliveryDate,
    blockedReason: status === 'Blocked' ? draft.blockedReason : '',
    deprioritizationReason: status === 'Deprioritized' ? draft.deprioritizationReason : undefined,
    replacementDemandId: status === 'Deprioritized' ? draft.replacementDemandId : undefined,
    observation: status === 'Deprioritized' ? draft.observation : draft.observation
  })
}

function handleHierarchyStatusChange(item: RoadmapDemand, nextStatus: DemandStatus) {
  const currentDraft = getHierarchyInlineDraft(item)

  if (nextStatus === currentDraft.status && !requiresHierarchyStatusDetails(nextStatus)) {
    deactivateHierarchyCell(item.id, 'status')
    return
  }

  if (requiresHierarchyStatusDetails(nextStatus)) {
    openHierarchyStatusModal(item, nextStatus)
    deactivateHierarchyCell(item.id, 'status')
    return
  }

  updateHierarchyInlineDraft(item, {
    status: nextStatus,
    dueDate: currentDraft.status === 'Done' ? (item.promisedDate ?? '') : currentDraft.dueDate,
    deliveryDate: '',
    blockedReason: '',
    deprioritizationReason: undefined,
    replacementDemandId: undefined
  })
  deactivateHierarchyCell(item.id, 'status')
}

function confirmHierarchyStatusModal() {
  const item = hierarchyStatusModalItem.value
  const draft = hierarchyStatusModalDraft.value

  if (!item || !draft)
    return

  if (draft.status === 'Done' && !draft.deliveryDate) {
    toast.add({ title: 'Informe a data de entrega', color: 'warning' })
    return
  }

  if (draft.status === 'Blocked' && !draft.blockedReason.trim()) {
    toast.add({ title: 'Informe o motivo do impedimento', color: 'warning' })
    return
  }

  if (draft.status === 'Deprioritized') {
    if (!draft.deprioritizationReason) {
      toast.add({ title: 'Selecione o motivo da despriorização', color: 'warning' })
      return
    }

    if (!draft.observation.trim()) {
      toast.add({ title: 'Informe a observação da despriorização', color: 'warning' })
      return
    }
  }

  if (draft.status === 'Done')
    updateHierarchyInlineDraft(item, { dueDate: draft.deliveryDate })

  closeHierarchyStatusModal()
}

function activateHierarchyCell(item: RoadmapDemand, field: HierarchyEditableField) {
  if (field === 'hours' && item.itemType !== 'Demand' && !item.isSimple)
    return

  if (field === 'dueDate' && item.itemType === 'Roadmap')
    return

  if (field === 'classification' && item.itemType !== 'Epic')
    return

  if (field === 'products' && item.itemType !== 'Demand' && !item.isSimple)
    return

  if (field === 'customers' && item.itemType !== 'Epic')
    return

  if (field === 'customers') {
    hierarchyCustomerInputs.value = {
      ...hierarchyCustomerInputs.value,
      [item.id]: ''
    }
  }

  activeHierarchyCell.value = { itemId: item.id, field }
}

function deactivateHierarchyCell(itemId?: string, field?: HierarchyEditableField) {
  if (!activeHierarchyCell.value)
    return

  if (itemId && activeHierarchyCell.value.itemId !== itemId)
    return

  if (field && activeHierarchyCell.value.field !== field)
    return

  if (activeHierarchyCell.value.field === 'customers') {
    const customerItemId = activeHierarchyCell.value.itemId
    const { [customerItemId]: _removed, ...rest } = hierarchyCustomerInputs.value
    hierarchyCustomerInputs.value = rest
  }

  activeHierarchyCell.value = null
}

function isHierarchyCellEditing(item: RoadmapDemand, field: HierarchyEditableField) {
  return activeHierarchyCell.value?.itemId === item.id && activeHierarchyCell.value?.field === field
}

function getHierarchyEditableCellButtonClass(item: RoadmapDemand) {
  return isHierarchyInlineDirty(item)
    ? 'border-primary/40 ring-1 ring-primary/10 hover:border-primary/60 hover:bg-primary/5'
    : 'border-transparent text-highlighted hover:border-primary/30 hover:bg-elevated'
}

function getHierarchyHoursButtonClass(item: RoadmapDemand) {
  const draft = getHierarchyInlineDraft(item)
  const textClass = draft.hoursRed ? 'text-red-500' : 'text-highlighted'
  return isHierarchyInlineDirty(item)
    ? `${textClass} rounded-sm ring-1 ring-primary/20 bg-primary/5 px-0.5`
    : `${textClass} hover:opacity-70`
}

function getHierarchyReadonlyCellClass(hasValue = true) {
  return hasValue
    ? 'text-muted opacity-60'
    : 'text-muted/70 opacity-55'
}

function getHierarchyReadonlyOverflowTriggerClass() {
  return 'inline-flex max-w-full items-center gap-1 truncate bg-transparent p-0 text-[11px] text-muted transition-colors hover:text-highlighted'
}

function getHierarchyDraftDisplayItem(item: RoadmapDemand): RoadmapDemand {
  const draft = hierarchyInlineDrafts.value[item.id]
  if (!draft)
    return item

  const isDoneStatus = draft.status === 'Done'

  return {
    ...item,
    title: draft.title,
    status: draft.status,
    classification: draft.classification,
    customers: draft.customers,
    effectivePromisedDate: isDoneStatus ? item.effectivePromisedDate : undefined,
    promisedDate: isDoneStatus ? item.promisedDate : draft.dueDate,
    deliveryDate: isDoneStatus ? draft.dueDate : ''
  }
}

function getHierarchyDraftProductEntries(item: RoadmapDemand) {
  const draft = getHierarchyInlineDraft(item)
  const allowedOptions = getEditableProductOptions(item)
  const allowedNameById = new Map(allowedOptions.map(option => [option.value, option.label] as const))
  const currentNameById = new Map(getProductEntries(item).map(product => [product.value, product.label] as const))

  return draft.productIds
    .map((productId) => {
      const label = allowedNameById.get(productId) ?? currentNameById.get(productId)
      return label ? { value: productId, label } : null
    })
    .filter((product): product is { value: string, label: string } => !!product)
}

function getHierarchyDraftCustomerDisplay(item: RoadmapDemand) {
  return getAdaptiveInlineListDisplay(getHierarchyInlineDraft(item).customers, getHierarchyColSize('customers'))
}

function toggleHierarchyDraftProduct(item: RoadmapDemand, productId: string, checked: boolean) {
  const allowedProductIds = new Set(getEditableProductOptions(item).map(product => product.value))
  const currentSelection = new Set(getHierarchyInlineDraft(item).productIds.filter(currentProductId => allowedProductIds.has(currentProductId)))

  if (checked)
    currentSelection.add(productId)
  else
    currentSelection.delete(productId)

  updateHierarchyInlineDraft(item, { productIds: Array.from(currentSelection) })
}

function addHierarchyCustomer(item: RoadmapDemand, customer: string) {
  const normalized = customer.trim()
  if (!normalized)
    return

  const nextCustomers = Array.from(new Set([...getHierarchyInlineDraft(item).customers, normalized]))
  updateHierarchyInlineDraft(item, { customers: nextCustomers })
  hierarchyCustomerInputs.value = {
    ...hierarchyCustomerInputs.value,
    [item.id]: ''
  }
}

function removeHierarchyCustomer(item: RoadmapDemand, customer: string) {
  updateHierarchyInlineDraft(item, {
    customers: getHierarchyInlineDraft(item).customers.filter(currentCustomer => currentCustomer !== customer)
  })
}

function getFilteredHierarchyCustomerSuggestions(item: RoadmapDemand) {
  const query = hierarchyCustomerInputs.value[item.id]?.trim().toLowerCase() ?? ''
  if (!query)
    return []

  const selected = new Set(getHierarchyInlineDraft(item).customers.map(customer => customer.toLowerCase()))

  return customerSuggestions.value
    .filter(customer => !selected.has(customer.toLowerCase()))
    .filter(customer => customer.toLowerCase().includes(query))
    .slice(0, 6)
}

function isHierarchyInlineSaving(itemId: string) {
  return hierarchyInlineSavingIds.value.includes(itemId)
}

function parseHierarchyInlineHours(item: RoadmapDemand) {
  if (item.itemType !== 'Demand' && !item.isSimple)
    return item.hours

  const normalized = getHierarchyInlineDraft(item).hoursInput.trim().replace(',', '.')
  if (!normalized)
    return undefined

  const parsed = Number(normalized)
  if (!Number.isFinite(parsed) || parsed < 0)
    return Number.NaN

  return Number(parsed.toFixed(2))
}

function isHierarchyInlineDirty(item: RoadmapDemand) {
  const draft = hierarchyInlineDrafts.value[item.id]
  if (!draft)
    return false

  const originalDueDate = item.status === 'Done' ? (item.deliveryDate ?? '') : (item.promisedDate ?? '')
  const hours = parseHierarchyInlineHours(item)

  if (Number.isNaN(hours))
    return true

  return draft.status !== item.status
    || draft.title.trim() !== item.title.trim()
    || draft.dueDate !== originalDueDate
    || (draft.status === 'Done' && draft.deliveryDate !== (item.deliveryDate ?? ''))
    || (draft.status === 'Blocked' && draft.blockedReason.trim() !== (item.blockedReason ?? '').trim())
    || (draft.status === 'Deprioritized' && draft.observation.trim() !== (item.observation ?? '').trim())
    || (draft.status === 'Deprioritized' && draft.deprioritizationReason !== item.deprioritizationReason)
    || (draft.status === 'Deprioritized' && draft.replacementDemandId !== item.replacementDemandId)
    || (item.itemType === 'Epic' && draft.classification !== item.classification)
    || ((item.itemType === 'Demand' || item.isSimple) && draft.productIds.slice().sort().join('|') !== getProductEntries(item).map(product => product.value).sort().join('|'))
    || draft.customers.slice().sort((left, right) => left.localeCompare(right, 'pt-BR')).join('|') !== getDisplayedCustomers(item).slice().sort((left, right) => left.localeCompare(right, 'pt-BR')).join('|')
    || ((item.itemType === 'Demand' || item.isSimple) && hours !== item.hours)
    || ((item.itemType === 'Demand' || item.isSimple) && draft.hoursRed !== (item.hoursRed ?? false))
}

function discardAllHierarchyInlineEdits() {
  if (isSavingAllHierarchyEdits.value || hierarchyInlineSavingIds.value.length)
    return

  const discardedChanges = hierarchyPendingEditCount.value
  deactivateHierarchyCell()
  clearAllHierarchyInlineDrafts()

  if (discardedChanges > 0) {
    toast.add({
      title: 'Edição cancelada',
      description: `${discardedChanges.toLocaleString('pt-BR')} alteração(ões) descartada(s).`,
      color: 'warning'
    })
  }
}

async function saveHierarchyInline(
  item: RoadmapDemand,
  options?: { reloadAfterSave?: boolean, showSuccessToast?: boolean }
) {
  if (!isHierarchyInlineDirty(item) || isHierarchyInlineSaving(item.id))
    return true

  const reloadAfterSave = options?.reloadAfterSave ?? true
  const showSuccessToast = options?.showSuccessToast ?? true

  const draft = getHierarchyInlineDraft(item)
  const hours = parseHierarchyInlineHours(item)

  if (Number.isNaN(hours)) {
    toast.add({
      title: 'Horas inválidas',
      description: 'Informe um valor numérico maior que zero.',
      color: 'warning'
    })
    return false
  }

  if ((item.itemType === 'Demand' || item.isSimple) && hours === 0) {
    toast.add({
      title: 'Horas inválidas',
      description: '0h não é aceito. Informe um valor válido ou deixe as horas em branco.',
      color: 'warning'
    })
    return false
  }

  if (draft.status === 'Done' && !draft.deliveryDate) {
    toast.add({
      title: 'Informe a data de entrega',
      color: 'warning'
    })
    return false
  }

  if (draft.status === 'Blocked' && !draft.blockedReason.trim()) {
    toast.add({
      title: 'Informe o motivo do impedimento',
      color: 'warning'
    })
    return false
  }

  if (draft.status === 'Deprioritized' && (!draft.deprioritizationReason || !draft.observation.trim())) {
    toast.add({
      title: 'Preencha os campos da despriorização',
      description: 'Informe o motivo e a observação para concluir a alteração.',
      color: 'warning'
    })
    return false
  }

  const isDoneStatus = draft.status === 'Done'

  try {
    hierarchyInlineSavingIds.value = [...hierarchyInlineSavingIds.value, item.id]

    const updated = await roadmapStore.updateDemand(item.id, buildDemandFormData(item, {
      title: draft.title.trim() || item.title,
      status: draft.status,
      classification: item.itemType === 'Epic' ? draft.classification : item.classification,
      productIds: (item.itemType === 'Demand' || item.isSimple) ? draft.productIds : item.products.map(product => product.productId),
      customers: item.itemType === 'Epic' ? draft.customers : (item.customers ?? []),
      observation: draft.observation,
      blockedReason: draft.status === 'Blocked' ? draft.blockedReason : '',
      deprioritizationReason: draft.status === 'Deprioritized' ? draft.deprioritizationReason : undefined,
      replacementDemandId: draft.status === 'Deprioritized' ? draft.replacementDemandId : undefined,
      hours: (item.itemType === 'Demand' || item.isSimple) ? hours : item.hours,
      hoursRed: (item.itemType === 'Demand' || item.isSimple) ? draft.hoursRed : false,
      promisedDate: isDoneStatus ? (item.promisedDate ?? '') : draft.dueDate,
      deliveryDate: isDoneStatus ? draft.dueDate : ''
    }))

    clearHierarchyInlineDraft(item.id)
    if (reloadAfterSave) {
      const idx = hierarchyDemands.value.findIndex(d => d.id === updated.id)
      if (idx !== -1)
        hierarchyDemands.value.splice(idx, 1, updated)
      else
        hierarchyDemands.value.push(updated)
    }

    if (showSuccessToast)
      toast.add({ title: 'Item atualizado', color: 'success' })

    return true
  }
  catch {
    // handled by useApi
    return false
  }
  finally {
    hierarchyInlineSavingIds.value = hierarchyInlineSavingIds.value.filter(currentId => currentId !== item.id)
  }
}

async function saveAllHierarchyInlineEdits() {
  if (!hierarchyPendingEditCount.value || isSavingAllHierarchyEdits.value)
    return

  const dirtyIds = new Set(hierarchyInlineDirtyIds.value)
  const itemsToSave = hierarchyDemands.value.filter(item => dirtyIds.has(item.id))

  try {
    isSavingAllHierarchyEdits.value = true

    for (const item of itemsToSave) {
      const saved = await saveHierarchyInline(item, { reloadAfterSave: false, showSuccessToast: false })
      if (!saved)
        return
    }

    // Sync all updated items from store — no API call, no loading indicator
    const storeMap = new Map(roadmapStore.demands.map(d => [d.id, d]))
    hierarchyDemands.value = hierarchyDemands.value.map(d => storeMap.get(d.id) ?? d)

    deactivateHierarchyCell()
    toast.add({
      title: 'Edições salvas',
      description: `${itemsToSave.length.toLocaleString('pt-BR')} item(ns) atualizado(s).`,
      color: 'success'
    })
  }
  finally {
    isSavingAllHierarchyEdits.value = false
  }
}

function resetCreateModalDefaults() {
  defaultQuarterYear.value = undefined
  defaultQuarterNumber.value = undefined
  defaultType.value = undefined
  defaultHours.value = undefined
  defaultProductIds.value = []
}

function openCreateModal(
  itemType?: RoadmapItemType,
  parentDemandId?: string,
  defaults?: {
    projectId?: string
    projectIds?: string[]
    quarterYear?: number
    quarterNumber?: number
    type?: RoadmapDemand['type']
    hours?: number
    productIds?: string[]
  }
) {
  createItemType.value = itemType
  defaultParentDemandId.value = parentDemandId
  defaultProjectId.value = defaults?.projectId
  defaultProjectIds.value = defaults?.projectIds ?? []
  defaultQuarterYear.value = defaults?.quarterYear
  defaultQuarterNumber.value = defaults?.quarterNumber
  defaultType.value = defaults?.type
  defaultHours.value = defaults?.hours
  defaultProductIds.value = defaults?.productIds ?? []
  forceSimpleEpic.value = false
  editingDemand.value = null
  modalOpen.value = true
}

const modalEditFocusField = ref<string | undefined>()

function openEditModal(item: RoadmapDemand, options?: { forceSimpleEpic?: boolean, focusField?: string }) {
  editingDemand.value = item
  defaultParentDemandId.value = undefined
  defaultProjectId.value = undefined
  defaultProjectIds.value = []
  resetCreateModalDefaults()
  forceSimpleEpic.value = options?.forceSimpleEpic ?? false
  modalEditFocusField.value = options?.focusField
  modalOpen.value = true
}

// Adding the first demand to a simple epic turns it into a composite epic: warn the user
// (its quarter/type/hours/product will be lost) before opening the prefilled demand form.
function promptCreateDemandInSimpleEpic(epic: RoadmapDemand) {
  convertSourceEpic.value = epic
  confirmConvertToCompositeOpen.value = true
}

function confirmCreateDemandInSimpleEpic() {
  const epic = convertSourceEpic.value
  if (!epic)
    return

  confirmConvertToCompositeOpen.value = false
  convertSourceEpic.value = null

  openCreateModal('Demand', epic.id, {
    projectId: pickDefaultProjectId(getItemProjectIds(epic)),
    quarterYear: epic.quarterYear,
    quarterNumber: epic.quarterNumber,
    type: epic.type,
    hours: epic.hours ?? undefined,
    productIds: epic.products.map(product => product.productId)
  })
}

// A composite epic can become simple again only when it has no demands linked.
function promptConvertEpicToSimple(epic: RoadmapDemand) {
  openEditModal(epic, { forceSimpleEpic: true })
}

function startCreateDemandForEpic(epic: RoadmapDemand) {
  if (epic.isSimple)
    promptCreateDemandInSimpleEpic(epic)
  else
    openCreateModal('Demand', epic.id, { projectId: pickDefaultProjectId(getItemProjectIds(epic)) })
}

function buildDemandFormData(item: RoadmapDemand, overrides?: Partial<DemandFormData>): DemandFormData {
  return {
    itemType: item.itemType,
    parentDemandId: item.parentDemandId,
    title: item.title,
    description: item.description ?? '',
    projectId: item.projectId,
    projectIds: item.projectIds ?? (item.projectId ? [item.projectId] : []),
    quarterYear: item.quarterYear,
    quarterNumber: item.quarterNumber,
    type: item.type,
    classification: item.classification,
    productIds: item.products.map(product => product.productId),
    status: item.status,
    observation: item.observation ?? '',
    deprioritizationReason: item.deprioritizationReason ?? undefined,
    replacementDemandId: item.replacementDemandId ?? undefined,
    jiraIssue: item.jiraIssue ?? '',
    issueLinks: item.issueLinks?.map(issue => ({ key: issue.key, url: issue.url ?? '' })) ?? [],
    hours: item.hours,
    hoursRed: item.hoursRed ?? false,
    isSimple: item.isSimple ?? false,
    promisedDate: item.promisedDate ?? '',
    customers: item.customers ?? [],
    dependencyDemandIds: item.dependsOn.map(dependency => dependency.demandId),
    blockedReason: item.blockedReason ?? '',
    deliveryDate: item.deliveryDate ?? '',
    problemClarity: item.problemClarity ?? undefined,
    hasNoKpi: item.hasNoKpi,
    noKpiClassification: item.noKpiClassification ?? undefined,
    ...overrides
  }
}

function normalizeCustomerName(value?: string) {
  return value?.trim().toLowerCase() ?? ''
}

function sanitizeCustomerRenames(renames?: CustomerRename[]) {
  const result: CustomerRename[] = []
  const seen = new Set<string>()

  for (const rename of renames ?? []) {
    const from = rename.from.trim()
    const to = rename.to.trim()
    const key = normalizeCustomerName(from)

    if (!from || !to || key === normalizeCustomerName(to) || seen.has(key))
      continue

    seen.add(key)
    result.push({ from, to })
  }

  return result
}

function applyCustomerRenames(customers: string[] | undefined, renames: CustomerRename[]) {
  const renameByCustomer = new Map(
    renames.map(rename => [normalizeCustomerName(rename.from), rename.to.trim()] as const)
  )
  const nextCustomers: string[] = []

  for (const customer of customers ?? []) {
    const renamed = renameByCustomer.get(normalizeCustomerName(customer)) ?? customer.trim()

    if (renamed && !nextCustomers.includes(renamed))
      nextCustomers.push(renamed)
  }

  return nextCustomers
}

async function propagateEpicCustomerRenames(sourceEpicId: string, renames: CustomerRename[]) {
  if (!renames.length)
    return

  const response = await api.get<ApiResponse<RoadmapDemand[]>>('/api/roadmap/demands')
  const impactedEpics = (response.data ?? []).filter((item) => {
    if (item.itemType !== 'Epic' || item.id === sourceEpicId)
      return false

    return (item.customers ?? []).some(customer =>
      renames.some(rename => normalizeCustomerName(customer) === normalizeCustomerName(rename.from))
    )
  })

  for (const epic of impactedEpics) {
    await roadmapStore.updateDemand(epic.id, buildDemandFormData(epic, {
      customers: applyCustomerRenames(epic.customers, renames),
      customerRenames: []
    }))
  }

  await roadmapStore.fetchCustomerSuggestions()
}

function buildBulkEditOverrides(item: RoadmapDemand, changes: BulkEditRoadmapItemsData): Partial<DemandFormData> {
  const overrides: Partial<DemandFormData> = {}

  if (changes.status) {
    overrides.status = changes.status

    if (changes.status === 'Done')
      overrides.deliveryDate = changes.deliveryDate ?? item.deliveryDate ?? ''

    if (changes.status === 'Blocked')
      overrides.blockedReason = changes.blockedReason ?? item.blockedReason ?? ''

    if (changes.status === 'Deprioritized') {
      overrides.observation = changes.observation ?? item.observation ?? ''
      overrides.deprioritizationReason = changes.deprioritizationReason ?? item.deprioritizationReason ?? undefined

      if (Object.prototype.hasOwnProperty.call(changes, 'replacementDemandId'))
        overrides.replacementDemandId = changes.replacementDemandId
    }
    else {
      overrides.deprioritizationReason = undefined
      overrides.replacementDemandId = undefined

      if (changes.status !== 'Blocked')
        overrides.blockedReason = ''
    }
  }

  if (Object.prototype.hasOwnProperty.call(changes, 'promisedDate'))
    overrides.promisedDate = changes.promisedDate

  if (item.itemType === 'Demand') {
    if (changes.type)
      overrides.type = changes.type

    if (changes.quarterYear != null && changes.quarterNumber != null) {
      overrides.quarterYear = changes.quarterYear
      overrides.quarterNumber = changes.quarterNumber
    }
  }

  return overrides
}

async function handleBulkEditSubmit(changes: BulkEditRoadmapItemsData) {
  if (!selectedHierarchyItems.value.length || isBulkEditing.value)
    return

  const updatedCount = selectedHierarchyItems.value.length
  isBulkEditing.value = true

  try {
    for (const item of selectedHierarchyItems.value) {
      await roadmapStore.updateDemand(item.id, buildDemandFormData(item, buildBulkEditOverrides(item, changes)))
    }

    await loadPageData()
    bulkEditModalOpen.value = false
    clearHierarchySelection()
    toast.add({
      title: 'Itens atualizados em lote',
      description: `${updatedCount.toLocaleString('pt-BR')} itens atualizados com sucesso.`,
      color: 'success'
    })
  }
  catch {
    // handled by useApi
  }
  finally {
    isBulkEditing.value = false
  }
}

function handleTradeOffDeleted(tradeOffId: string) {
  if (!editingDemand.value)
    return

  editingDemand.value = {
    ...editingDemand.value,
    tradeOffHistory: editingDemand.value.tradeOffHistory.filter(item => item.id !== tradeOffId)
  }
}

function promptDelete(item: RoadmapDemand) {
  if (item.itemType === 'Roadmap' && hierarchyDemands.value.some(demand => demand.parentDemandId === item.id)) {
    toast.add({
      title: 'Exclusão não permitida',
      description: 'Este roadmap possui épicos vinculados e não pode ser removido.',
      color: 'warning'
    })
    return
  }

  if (item.itemType === 'Epic' && hierarchyDemands.value.some(demand => demand.parentDemandId === item.id)) {
    toast.add({
      title: 'Exclusão não permitida',
      description: 'Este épico possui demandas vinculadas e não pode ser removido.',
      color: 'warning'
    })
    return
  }

  deleteTarget.value = item
  confirmDeleteOpen.value = true
}

async function handleSubmit(data: DemandFormData) {
  if (isSavingDemand.value)
    return

  const customerRenames = sanitizeCustomerRenames(data.customerRenames)

  try {
    isSavingDemand.value = true
    if (editingDemand.value) {
      const updated = await roadmapStore.updateDemand(editingDemand.value.id, data)

      if (editingDemand.value.itemType === 'Epic')
        await propagateEpicCustomerRenames(editingDemand.value.id, customerRenames)

      // Sync local list from store — covers the edited item and any customer renames
      // propagated to other epics, without triggering a full page reload
      const storeMap = new Map(roadmapStore.demands.map(d => [d.id, d]))
      hierarchyDemands.value = hierarchyDemands.value.map(d => storeMap.get(d.id) ?? d)
      if (!hierarchyDemands.value.some(d => d.id === updated.id))
        hierarchyDemands.value.push(updated)

      toast.add({ title: 'Item atualizado', color: 'success' })
    }
    else {
      const created = await roadmapStore.createDemand(data)
      hierarchyDemands.value.push(created)

      // The backend converts a simple epic to composite when its first demand is created.
      // Mirror that on every in-memory copy (local list AND the shared store, which the
      // planning view reads) so the epic stops rendering as a standalone simple epic.
      if (created.itemType === 'Demand' && created.parentDemandId) {
        const applyConversion = (epic?: RoadmapDemand | null) => {
          if (!epic?.isSimple)
            return
          epic.isSimple = false
          epic.hours = undefined
          epic.hoursRed = false
          epic.products = []
          epic.quarterYear = BACKLOG_QUARTER.year
          epic.quarterNumber = BACKLOG_QUARTER.number
        }

        applyConversion(hierarchyDemands.value.find(item => item.id === created.parentDemandId))
        applyConversion(roadmapStore.demands.find(item => item.id === created.parentDemandId))
      }

      toast.add({ title: 'Item criado', color: 'success' })
    }

    modalOpen.value = false
  }
  catch {
    // handled by useApi
  }
  finally {
    isSavingDemand.value = false
  }
}

function openKpiWorkspace(item: RoadmapDemand) {
  const targetEpic = getKpiTargetEpic(item)
  if (!targetEpic)
    return

  navigateTo({
    path: '/roadmap',
    query: {
      projectId: currentPrimaryProjectId.value ?? targetEpic.projectId,
      kpiDemandId: targetEpic.id,
      view: 'hierarchy'
    }
  })
}

async function confirmDelete() {
  if (!deleteTarget.value)
    return

  try {
    await roadmapStore.deleteDemand(deleteTarget.value.id)
    await loadPageData()
    toast.add({ title: 'Item removido', color: 'success' })
    confirmDeleteOpen.value = false
    deleteTarget.value = null
  }
  catch {
    // handled by useApi
  }
}

async function initializeHierarchyPage() {
  await roadmapStore.fetchProjects()

  // Priority: prop from planning view > URL query > localStorage cache.
  if (props.projectIds?.length) {
    selectedProjectIds.value = props.projectIds
      .filter(projectId => projects.value.some(project => project.id === projectId))
  }
  else {
    const fromUrl = [
      ...(typeof route.query.projectIds === 'string'
        ? route.query.projectIds.split(',')
        : []),
      ...(typeof route.query.projectId === 'string'
        ? [route.query.projectId]
        : [])
    ]
    if (fromUrl.length) {
      selectedProjectIds.value = [...new Set(fromUrl)]
        .filter(projectId => projects.value.some(project => project.id === projectId))
    }
    else {
      const cached = readHierarchyCacheJson<string[]>(CACHE_KEY_HIERARCHY_PROJECTS)
      selectedProjectIds.value = (cached ?? [])
        .filter(projectId => projects.value.some(project => project.id === projectId))
    }
  }

  await loadPageData()
}

void initializeHierarchyPage()

// ─── Soft auto-refresh (hierarchy) ──────────────────────────────────────────────
// Mirrors the planning view: refetch on returning to the tab and after 15 min of idle, but never
// while the user is busy (form open, inline edit, staged edits awaiting confirmation, or a save in
// progress) so unsaved work is preserved. Switching into the hierarchy already reloads on mount.
const HIERARCHY_SOFT_REFRESH_IDLE_MS = 15 * 60 * 1000
let lastHierarchyActivityAt = Date.now()
let lastHierarchySoftRefreshAt = Date.now()
let hierarchySoftRefreshIntervalId: ReturnType<typeof setInterval> | null = null

const isHierarchyBusy = computed(() =>
  modalOpen.value
  || activeHierarchyCell.value != null
  || hierarchyPendingEditCount.value > 0
  || hierarchyInlineSavingIds.value.length > 0
  || isSavingAllHierarchyEdits.value
  || isSavingDemand.value
  || isHierarchyLoading.value
)

watch(isHierarchyBusy, () => {
  lastHierarchyActivityAt = Date.now()
})

async function softRefreshHierarchyData() {
  if (isHierarchyBusy.value)
    return
  lastHierarchySoftRefreshAt = Date.now()
  try {
    await loadPageData()
  }
  catch {
    // handled by useApi
  }
}

function handleHierarchyVisibilityChange() {
  if (document.visibilityState === 'visible')
    void softRefreshHierarchyData()
}

onMounted(() => {
  hierarchySoftRefreshIntervalId = setInterval(() => {
    const idleSince = Math.max(lastHierarchyActivityAt, lastHierarchySoftRefreshAt)
    if (Date.now() - idleSince >= HIERARCHY_SOFT_REFRESH_IDLE_MS)
      void softRefreshHierarchyData()
  }, 60 * 1000)
  document.addEventListener('visibilitychange', handleHierarchyVisibilityChange)
})

onUnmounted(() => {
  if (hierarchySoftRefreshIntervalId != null)
    clearInterval(hierarchySoftRefreshIntervalId)
  document.removeEventListener('visibilitychange', handleHierarchyVisibilityChange)
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
            @click="navigateTo({ path: '/roadmap', query: currentPrimaryProjectId ? { projectId: currentPrimaryProjectId } : undefined })"
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
        <div class="flex w-full flex-col gap-2 lg:flex-1 lg:flex-row lg:items-end">
          <UFormField label="Time" class="w-full lg:max-w-sm">
            <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
              <UButton
                type="button"
                variant="outline"
                color="neutral"
                trailing-icon="i-lucide-chevron-down"
                class="w-full justify-between"
              >
                <span class="truncate">{{ projectFilterLabel }}</span>
              </UButton>

              <template #content>
                <div class="min-w-72 space-y-1 p-1">
                  <button
                    type="button"
                    class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                    :class="selectedProjectIds.length === 0 ? 'text-primary' : 'text-highlighted'"
                    @click="clearProjectFilter"
                  >
                    <UIcon v-if="selectedProjectIds.length === 0" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                    <span v-else class="inline-block h-4 w-4 shrink-0" />
                    Todos os times
                  </button>

                  <button
                    v-for="project in projectFilterOptions"
                    :key="project.value"
                    type="button"
                    class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                    :class="selectedProjectIds.includes(project.value) ? 'text-primary' : 'text-highlighted'"
                    @click="toggleProjectFilter(project.value)"
                  >
                    <UIcon v-if="selectedProjectIds.includes(project.value)" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                    <span v-else class="inline-block h-4 w-4 shrink-0" />
                    {{ project.label }}
                  </button>
                </div>
              </template>
            </UPopover>
          </UFormField>

          <div class="flex flex-col gap-2 xl:flex-row xl:items-center xl:justify-between xl:gap-3">
            <div class="flex flex-wrap items-center gap-1.5 lg:flex-nowrap lg:whitespace-nowrap lg:pb-0.5">
              <UDropdownMenu :items="hierarchyDisplayMenuItems">
                <UButton
                  size="xs"
                  color="neutral"
                  variant="outline"
                  icon="i-lucide-panel-top-open"
                >
                  Exibição
                </UButton>
              </UDropdownMenu>
              <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                <UButton
                  size="xs"
                  color="neutral"
                  variant="outline"
                  icon="i-lucide-triangle-alert"
                >
                  {{ hierarchyProblemFilterLabel }}
                </UButton>

                <template #content>
                  <div class="min-w-64 space-y-1 p-1">
                    <button type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="hierarchyProblemFilter.length === 0 ? 'text-primary' : 'text-highlighted'" @click="clearHierarchyProblemFilter">
                      <UIcon v-if="hierarchyProblemFilter.length === 0" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                      <span v-else class="inline-block h-4 w-4 shrink-0" />
                      Sem filtro
                    </button>
                    <button type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="hierarchyProblemFilter.includes('__all__') ? 'text-primary' : 'text-highlighted'" @click="toggleHierarchyProblemFilter('__all__')">
                      <UIcon v-if="hierarchyProblemFilter.includes('__all__')" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                      <span v-else class="inline-block h-4 w-4 shrink-0" />
                      Todos os problemas
                    </button>
                    <button v-for="problem in hierarchyProblemOptions" :key="problem.value" type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="hierarchyProblemFilter.includes(problem.value) ? 'text-primary' : 'text-highlighted'" @click="toggleHierarchyProblemFilter(problem.value)">
                      <UIcon v-if="hierarchyProblemFilter.includes(problem.value)" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                      <span v-else class="inline-block h-4 w-4 shrink-0" />
                      {{ problem.label }}
                    </button>
                  </div>
                </template>
              </UPopover>
              <UButton
                v-if="selectedHierarchyItemCount"
                size="xs"
                color="neutral"
                variant="soft"
                icon="i-lucide-pencil-ruler"
                :loading="isBulkEditing"
                @click="bulkEditModalOpen = true"
              >
                Editar {{ selectedHierarchyItemCount.toLocaleString('pt-BR') }} itens
              </UButton>
              <UButton
                v-if="selectedHierarchyItemCount"
                size="xs"
                color="neutral"
                variant="ghost"
                icon="i-lucide-square-minus"
                @click="clearHierarchySelection"
              >
                Desmarcar
              </UButton>
            </div>

            <div class="flex flex-wrap items-center gap-1.5 text-[11px] text-muted xl:justify-end">
              <span class="rounded-full border border-default bg-elevated px-2.5 py-0.5">{{ roadmapItems.length }} roadmaps</span>
              <span class="rounded-full border border-default bg-elevated px-2.5 py-0.5">{{ epicItems.length }} épicos</span>
              <span class="rounded-full border border-default bg-elevated px-2.5 py-0.5">{{ demandItems.length }} demandas</span>
            </div>
          </div>
        </div>
      </div>
    </UCard>

    <div v-if="isHierarchyLoading" class="flex items-center justify-center py-16">
      <UIcon name="i-lucide-loader-circle" class="h-6 w-6 animate-spin text-primary" />
    </div>

    <template v-else>
      <div v-if="!roadmapItems.length && !epicItems.length && !demandItems.length" class="rounded-2xl border border-dashed border-default bg-elevated/30 px-5 py-12 text-center text-sm text-muted">
        Nenhum item encontrado para o projeto selecionado.
      </div>

      <div v-else class="overflow-visible rounded-2xl border border-default bg-default shadow-sm">
        <div class="sticky top-14 z-20 overflow-hidden rounded-t-2xl border-b border-default bg-elevated/95 shadow-sm backdrop-blur supports-[backdrop-filter]:bg-elevated/85 md:top-0">
          <table class="w-full table-fixed border-separate border-spacing-0 text-[13px] will-change-transform" :style="{ transform: `translateX(-${hierarchyHeaderScrollLeft}px)` }">
            <colgroup>
              <col v-for="columnId in hierarchyColumnOrder" :key="columnId" :style="{ width: getHierarchyColWidth(columnId) }">
            </colgroup>
            <thead>
              <tr class="bg-elevated/80 text-left text-[11px] font-semibold uppercase tracking-[0.08em] text-muted">
                <th class="relative border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('item') }">
                  <button type="button" class="inline-flex items-center gap-1 transition-colors hover:text-highlighted" @click="toggleHierarchySort('item')">
                    <span>Item</span>
                    <UIcon :name="getHierarchySortIcon('item')" class="h-3.5 w-3.5" />
                  </button>
                  <span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('item', $event)" />
                </th>
                <th class="relative border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('products') }">
                  <button type="button" class="inline-flex items-center gap-1 transition-colors hover:text-highlighted" @click="toggleHierarchySort('products')">
                    <span>Produtos</span>
                    <UIcon :name="getHierarchySortIcon('products')" class="h-3.5 w-3.5" />
                  </button>
                  <span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('products', $event)" />
                </th>
                <th class="relative border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('hours') }">HR<span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('hours', $event)" /></th>
                <th class="relative border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('classification') }">
                  <button type="button" class="inline-flex items-center gap-1 transition-colors hover:text-highlighted" @click="toggleHierarchySort('classification')">
                    <span>Classificação</span>
                    <UIcon :name="getHierarchySortIcon('classification')" class="h-3.5 w-3.5" />
                  </button>
                  <span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('classification', $event)" />
                </th>
                <th class="relative border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('customers') }">Clientes<span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('customers', $event)" /></th>
                <th class="relative border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('status') }">
                  <button type="button" class="inline-flex items-center gap-1 transition-colors hover:text-highlighted" @click="toggleHierarchySort('status')">
                    <span>Status</span>
                    <UIcon :name="getHierarchySortIcon('status')" class="h-3.5 w-3.5" />
                  </button>
                  <span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('status', $event)" />
                </th>
                <th class="relative border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('due') }">
                  <button type="button" class="inline-flex items-center gap-1 transition-colors hover:text-highlighted" @click="toggleHierarchySort('due')">
                    <span>Conclusão</span>
                    <UIcon :name="getHierarchySortIcon('due')" class="h-3.5 w-3.5" />
                  </button>
                  <span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('due', $event)" />
                </th>
                <th class="relative border-b border-default bg-elevated/95 px-2 py-2" :style="{ width: getHierarchyColWidth('kpi') }">KPI<span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('kpi', $event)" /></th>
                <th class="relative border-b border-default bg-elevated/95 !px-0" :style="{ width: getHierarchyColWidth('actions') }"><span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('actions', $event)" /></th>
              </tr>
              <tr class="bg-elevated/60 text-left text-[11px] text-muted">
                <th class="border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('item') }">
                  <input v-model="hierarchyItemFilter" type="text" placeholder="Filtrar..." class="w-full rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted outline-none transition-colors placeholder:text-muted focus:border-primary/40" >
                </th>
                <th class="border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('products') }">
                  <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                    <button class="flex w-full items-center gap-1.5 rounded-md border border-default bg-default px-2 py-1 text-xs transition-colors hover:border-primary/40">
                      <span class="flex-1 truncate text-left text-highlighted">{{ hierarchyProductsFilterLabel }}</span>
                      <UIcon name="i-lucide-chevron-down" class="h-3.5 w-3.5 shrink-0 text-muted" />
                    </button>
                    <template #content>
                      <div class="min-w-52 space-y-1 p-1">
                        <button type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="hierarchyProductsFilter.length === 0 ? 'text-primary' : 'text-highlighted'" @click="clearHierarchyProductsFilter">
                          <UIcon v-if="hierarchyProductsFilter.length === 0" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          Todos
                        </button>
                        <button v-for="product in productFilterOptions" :key="product.value" type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="hierarchyProductsFilter.includes(product.value) ? 'text-primary' : 'text-highlighted'" @click="toggleHierarchyProductsFilter(product.value)">
                          <UIcon v-if="hierarchyProductsFilter.includes(product.value)" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          {{ product.label }}
                        </button>
                      </div>
                    </template>
                  </UPopover>
                </th>
                <th class="border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('hours') }" />
                <th class="border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('classification') }">
                  <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                    <button class="flex w-full items-center gap-1.5 rounded-md border border-default bg-default px-2 py-1 text-xs transition-colors hover:border-primary/40">
                      <span class="flex-1 truncate text-left text-highlighted">{{ hierarchyClassificationFilterLabel }}</span>
                      <UIcon name="i-lucide-chevron-down" class="h-3.5 w-3.5 shrink-0 text-muted" />
                    </button>
                    <template #content>
                      <div class="min-w-52 space-y-1 p-1">
                        <button type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="hierarchyClassificationFilter.length === 0 ? 'text-primary' : 'text-highlighted'" @click="clearHierarchyClassificationFilter">
                          <UIcon v-if="hierarchyClassificationFilter.length === 0" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          Todas
                        </button>
                        <button v-for="classification in classificationFilterOptions" :key="classification.value" type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="hierarchyClassificationFilter.includes(classification.value) ? 'text-primary' : 'text-highlighted'" @click="toggleHierarchyClassificationFilter(classification.value)">
                          <UIcon v-if="hierarchyClassificationFilter.includes(classification.value)" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          {{ classification.label }}
                        </button>
                      </div>
                    </template>
                  </UPopover>
                </th>
                <th class="border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('customers') }">
                  <input v-model="hierarchyCustomersFilter" type="text" placeholder="Clientes" class="w-full rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted outline-none transition-colors placeholder:text-muted focus:border-primary/40" >
                </th>
                <th class="border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('status') }">
                  <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                    <button class="flex w-full items-center gap-1.5 rounded-md border border-default bg-default px-2 py-1 text-xs transition-colors hover:border-primary/40">
                      <span class="flex-1 truncate text-left text-highlighted">{{ hierarchyStatusFilterLabel }}</span>
                      <UIcon name="i-lucide-chevron-down" class="h-3.5 w-3.5 shrink-0 text-muted" />
                    </button>
                    <template #content>
                      <div class="min-w-44 space-y-1 p-1">
                        <button type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="hierarchyStatusFilter.length === 0 ? 'text-primary' : 'text-highlighted'" @click="clearHierarchyStatusFilter">
                          <UIcon v-if="hierarchyStatusFilter.length === 0" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          Todos
                        </button>
                        <button v-for="status in statusFilterOptions" :key="status.value" type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="hierarchyStatusFilter.includes(status.value) ? 'text-primary' : 'text-highlighted'" @click="toggleHierarchyStatusFilter(status.value)">
                          <UIcon v-if="hierarchyStatusFilter.includes(status.value)" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          {{ status.label }}
                        </button>
                      </div>
                    </template>
                  </UPopover>
                </th>
                <th class="border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('due') }">
                  <input v-model="hierarchyDueFilter" type="text" placeholder="Quarter/Data" class="w-full rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted outline-none transition-colors placeholder:text-muted focus:border-primary/40" >
                </th>
                <th class="border-b border-default bg-elevated/95 px-2 py-2" :style="{ width: getHierarchyColWidth('kpi') }" />
                <th class="border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('actions') }" />
              </tr>
            </thead>

          </table>
        </div>

        <div ref="hierarchyTableContainerRef" class="overflow-x-auto overflow-y-visible" @scroll="syncHierarchyHeaderScroll">
          <table class="w-full table-fixed border-separate border-spacing-0 text-[13px]">
            <colgroup>
              <col v-for="columnId in hierarchyColumnOrder" :key="columnId" :style="{ width: getHierarchyColWidth(columnId) }">
            </colgroup>

            <tbody>
              <tr v-if="!displayRoadmapGroups.length && !displayOrphanEpics.length && !displayOrphanDemands.length">
                <td colspan="9" class="px-5 py-12 text-center text-sm text-muted">
                  Nenhum item encontrado para os filtros aplicados.
                </td>
              </tr>

              <template v-for="group in displayRoadmapGroups" :key="group.roadmap.id">
                <tr class="border-b border-default bg-default hover:bg-elevated/30 transition-colors">
                  <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('item') }">
                    <div class="flex items-start gap-1">
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

                      <div class="min-w-0 flex-1" :class="getCrossProjectWatermarkClass(group.roadmap)">
                        <div class="mt-0.5 flex items-start gap-1">
                          <UIcon name="i-lucide-map" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-primary" />
                          <div class="min-w-0 flex-1">
                            <UInput
                              v-if="isHierarchyCellEditing(group.roadmap, 'title')"
                              :model-value="getHierarchyInlineDraft(group.roadmap).title"
                              size="xs"
                              autofocus
                              class="min-w-0 w-full"
                              :disabled="isHierarchyInlineSaving(group.roadmap.id) || isSavingAllHierarchyEdits"
                              @blur="deactivateHierarchyCell(group.roadmap.id, 'title')"
                              @keydown.esc.prevent="deactivateHierarchyCell(group.roadmap.id, 'title')"
                              @keydown.enter.prevent="deactivateHierarchyCell(group.roadmap.id, 'title')"
                              @update:model-value="(value) => updateHierarchyInlineDraft(group.roadmap, { title: String(value ?? '') })"
                            />
                            <button v-else type="button" class="w-full truncate rounded-md border px-1 py-0.5 text-left text-[12px] font-semibold text-highlighted transition-colors" :class="getHierarchyEditableCellButtonClass(group.roadmap)" :title="group.roadmap.description || undefined" :disabled="isHierarchyInlineSaving(group.roadmap.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(group.roadmap, 'title')">{{ getHierarchyDraftDisplayItem(group.roadmap).title }}</button>
                          </div>
                        </div>
                      </div>
                    </div>
                  </td>

                  <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('products') }">
                    <span v-if="getProductCellDisplay(getRoadmapGroupProductNames(group.roadmap, group.epics.map(entry => entry.epic))).allVisible && getProductCellDisplay(getRoadmapGroupProductNames(group.roadmap, group.epics.map(entry => entry.epic))).items.length" class="block max-w-full truncate text-[9px] text-highlighted" :class="getHierarchyReadonlyCellClass()" :title="getProductCellDisplay(getRoadmapGroupProductNames(group.roadmap, group.epics.map(entry => entry.epic))).fullLabel">
                      {{ getProductCellDisplay(getRoadmapGroupProductNames(group.roadmap, group.epics.map(entry => entry.epic))).previewLabel }}
                    </span>
                    <UPopover v-else-if="getProductCellDisplay(getRoadmapGroupProductNames(group.roadmap, group.epics.map(entry => entry.epic))).items.length" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                      <button type="button" class="inline-flex max-w-full items-center gap-1 truncate bg-transparent p-0 text-[9px] text-muted transition-colors hover:text-highlighted" :class="getHierarchyReadonlyCellClass()" :title="getProductCellDisplay(getRoadmapGroupProductNames(group.roadmap, group.epics.map(entry => entry.epic))).fullLabel">
                        <span class="max-w-[140px] truncate">{{ getProductCellDisplay(getRoadmapGroupProductNames(group.roadmap, group.epics.map(entry => entry.epic))).previewLabel }}</span>
                        <span class="shrink-0 text-muted">+{{ getProductCellDisplay(getRoadmapGroupProductNames(group.roadmap, group.epics.map(entry => entry.epic))).hiddenCount }}</span>
                      </button>

                      <template #content>
                        <div class="flex max-w-xs flex-col gap-1 p-2">
                          <span v-for="product in getProductCellDisplay(getRoadmapGroupProductNames(group.roadmap, group.epics.map(entry => entry.epic))).items" :key="`${group.roadmap.id}-${product}`" class="text-xs text-highlighted">
                            {{ product }}
                          </span>
                        </div>
                      </template>
                    </UPopover>
                    <span v-else class="text-xs text-muted">—</span>
                  </td>

                  <td class="border-b border-default px-2.5 py-0.5 align-top text-right text-[11px]" :style="{ width: getHierarchyColWidth('hours') }">
                    <span v-if="getDisplayedHours(group.roadmap) !== null" :class="getHierarchyReadonlyCellClass()">{{ getDisplayedHours(group.roadmap) }}h</span>
                    <span v-else class="text-xs" :class="getHierarchyReadonlyCellClass(false)">—</span>
                  </td>

                  <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('classification') }">
                    <span class="text-xs" :class="getHierarchyReadonlyCellClass(false)">—</span>
                  </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('customers') }">
                    <span class="text-xs" :class="getHierarchyReadonlyCellClass(false)">—</span>
                  </td>

                  <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('status') }">
                    <USelect
                      v-if="isHierarchyCellEditing(group.roadmap, 'status')"
                      :model-value="getHierarchyInlineDraft(group.roadmap).status"
                      :items="statusSelectOptions"
                      value-key="value"
                      option-attribute="label"
                      size="xs"
                      class="w-full"
                      :disabled="isHierarchyInlineSaving(group.roadmap.id) || isSavingAllHierarchyEdits"
                      @blur="deactivateHierarchyCell(group.roadmap.id, 'status')"
                      @update:model-value="(value) => value && handleHierarchyStatusChange(group.roadmap, value as DemandStatus)"
                    />
                    <button v-else type="button" class="inline-flex items-center rounded-md border px-1 py-0 text-[9px] font-medium transition-colors" :class="[statusTone[getHierarchyInlineDraft(group.roadmap).status], getHierarchyEditableCellButtonClass(group.roadmap)]" :disabled="isHierarchyInlineSaving(group.roadmap.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(group.roadmap, 'status')">
                      {{ statusLabels[getHierarchyInlineDraft(group.roadmap).status] }}
                    </button>
                  </td>

                  <td class="border-b border-default px-2.5 py-0.5 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('due') }">
                    <div v-if="getDueDateLabel(group.roadmap) || getDueQuarterLabel(group.roadmap) || isDelayed(group.roadmap)" class="flex min-w-0 items-center gap-1" :class="getHierarchyReadonlyCellClass()" :title="getDueTooltip(group.roadmap)">
                      <span v-if="getDueDateLabel(group.roadmap)" :class="getDueDateClass(group.roadmap)">{{ getDueDateLabel(group.roadmap) }}</span>
                      <span v-if="getDueQuarterLabel(group.roadmap)" class="inline-flex shrink-0 items-center rounded-md border border-default bg-elevated px-1 py-0 text-[8px] font-medium text-muted">
                        {{ getDueQuarterLabel(group.roadmap) }}
                      </span>
                    </div>
                    <span v-else class="text-xs" :class="getHierarchyReadonlyCellClass(false)">—</span>
                  </td>

                  <td class="border-b border-default px-2 py-0.5 align-top" :style="{ width: getHierarchyColWidth('kpi') }">
                    <span class="text-xs" :class="getHierarchyReadonlyCellClass(false)">—</span>
                  </td>

                    <td class="border-b border-default relative overflow-visible !p-0" :style="{ width: getHierarchyColWidth('actions') }">
                      <div class="group absolute inset-0 flex items-center justify-center">
                        <span class="pointer-events-none select-none text-[10px] text-muted/40 transition-opacity group-hover:opacity-0">···</span>
                        <div class="pointer-events-none absolute inset-y-0 right-0 z-30 flex items-center gap-0.5 rounded-md border border-default/60 bg-default/95 px-1 opacity-0 shadow-md backdrop-blur-sm transition-opacity group-hover:pointer-events-auto group-hover:opacity-100">
                          <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-plus" class="h-6 w-6 p-0" title="Novo épico" @click.stop="openCreateModal('Epic', group.roadmap.id, { projectIds: getItemProjectIds(group.roadmap) })" />
                          <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" class="h-6 w-6 p-0" title="Editar roadmap" @click.stop="openEditModal(group.roadmap)" />
                          <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" class="h-6 w-6 p-0" title="Excluir roadmap" @click.stop="promptDelete(group.roadmap)" />
                        </div>
                      </div>
                    </td>
                </tr>

                <tr
                  v-if="!group.epics.length && !isRoadmapCollapsed(group.roadmap.id)"
                  class="bg-elevated/10"
                >
                  <td colspan="9" class="border-b border-default px-3 py-3 text-xs text-muted">
                    Nenhum épico vinculado a este roadmap ainda.
                  </td>
                </tr>

                <template v-for="epicEntry in group.epics" :key="epicEntry.epic.id">
                  <tr
                    v-show="!isRoadmapCollapsed(group.roadmap.id)"
                    class="bg-elevated/10 hover:bg-elevated/20 transition-colors"
                  >
                    <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('item') }">
                      <div class="flex items-start gap-1 pl-5">
                        <input
                          type="checkbox"
                          class="mt-1 h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary"
                          :checked="isHierarchyItemSelected(epicEntry.epic.id)"
                          @click.stop
                          @change="toggleHierarchyItemSelection(epicEntry.epic.id, ($event.target as HTMLInputElement).checked)"
                        >
                        <button
                          type="button"
                          class="mt-0.5 inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-md border border-default bg-default text-muted transition-colors hover:text-highlighted"
                          :class="getCrossProjectWatermarkClass(epicEntry.epic)"
                          :disabled="!epicEntry.demands.length"
                          @click="toggleEpicCollapse(epicEntry.epic.id)"
                        >
                          <UIcon
                            :name="epicEntry.demands.length ? (isEpicCollapsed(epicEntry.epic.id) ? 'i-lucide-chevron-right' : 'i-lucide-chevron-down') : 'i-lucide-minus'"
                            class="h-3.5 w-3.5"
                          />
                        </button>
                        <div class="min-w-0 flex-1" :class="getCrossProjectWatermarkClass(epicEntry.epic)">
                          <div class="flex flex-wrap items-center gap-1">
                            <span v-if="isOutsideSelectedProject(epicEntry.epic)" class="inline-flex items-center rounded-md border border-warning/40 bg-warning/10 px-1 py-0 text-[8px] font-semibold uppercase tracking-[0.06em] text-warning">
                              Outro time
                            </span>
                          </div>
                          <div class="mt-0.5 flex items-start gap-1">
                            <UIcon name="i-lucide-star" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-amber-500" :class="getCrossProjectWatermarkClass(epicEntry.epic)" />
                            <div class="flex min-w-0 flex-1 items-center gap-1">
                              <UInput
                                v-if="isHierarchyCellEditing(epicEntry.epic, 'title')"
                                :model-value="getHierarchyInlineDraft(epicEntry.epic).title"
                                size="xs"
                                autofocus
                                class="min-w-0 flex-1 w-full"
                                :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits"
                                @blur="deactivateHierarchyCell(epicEntry.epic.id, 'title')"
                                @keydown.esc.prevent="deactivateHierarchyCell(epicEntry.epic.id, 'title')"
                                @keydown.enter.prevent="deactivateHierarchyCell(epicEntry.epic.id, 'title')"
                                @update:model-value="(value) => updateHierarchyInlineDraft(epicEntry.epic, { title: String(value ?? '') })"
                              />
                              <button v-else type="button" class="min-w-0 flex-1 truncate rounded-md border px-1 py-0.5 text-left text-[12px] font-medium text-highlighted transition-colors" :class="getHierarchyEditableCellButtonClass(epicEntry.epic)" :title="epicEntry.epic.description || undefined" :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epicEntry.epic, 'title')">{{ getHierarchyDraftDisplayItem(epicEntry.epic).title }}</button>
                              <UIcon v-if="getDemandProblemKeys(epicEntry.epic).length" name="i-lucide-triangle-alert" class="h-3.5 w-3.5 shrink-0 text-warning" :title="getDemandProblemTooltip(epicEntry.epic)" />
                              <a
                                v-if="getDisplayIssueLinks(epicEntry.epic).length === 1 && getDisplayIssueLinks(epicEntry.epic)[0]?.url"
                                :href="getDisplayIssueLinks(epicEntry.epic)[0]?.url"
                                target="_blank"
                                rel="noreferrer"
                                class="inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-default bg-default px-1 text-[9px] font-medium text-primary transition-colors hover:border-primary/40"
                              >
                                <UIcon name="i-simple-icons-jira" class="h-3 w-3" />
                              </a>
                              <UPopover v-else-if="getDisplayIssueLinks(epicEntry.epic).length" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                                <button type="button" class="inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-default bg-default px-1 text-[9px] font-medium text-primary transition-colors hover:border-primary/40">
                                  <UIcon name="i-simple-icons-jira" class="h-3 w-3" />
                                </button>
                                <template #content>
                                  <div class="flex min-w-40 flex-col gap-1 p-1">
                                    <a
                                      v-for="issue in getDisplayIssueLinks(epicEntry.epic)"
                                      :key="`${epicEntry.epic.id}-${issue.key}`"
                                      :href="issue.url || undefined"
                                      :target="issue.url ? '_blank' : undefined"
                                      rel="noreferrer"
                                      class="inline-flex items-center rounded-md border border-default bg-default px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:border-primary/40"
                                    >
                                      {{ issue.key }}
                                    </a>
                                  </div>
                                </template>
                              </UPopover>
                              <button v-else type="button" class="inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-red-200 bg-default px-1 text-[9px] font-medium text-red-500 transition-colors hover:border-red-400 dark:border-red-800 dark:text-red-400" title="Sem issue Jira — clique para adicionar" @click="openEditModal(epicEntry.epic, { focusField: 'jiraIssue' })"><UIcon name="i-simple-icons-jira" class="h-3 w-3" /></button>
                            </div>
                          </div>
                        </div>
                      </div>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top text-left" :style="{ width: getHierarchyColWidth('products') }">
                      <template v-if="epicEntry.epic.isSimple">
                        <UPopover
                          :open="isHierarchyCellEditing(epicEntry.epic, 'products')"
                          :content="{ side: 'bottom', align: 'start', sideOffset: 8 }"
                          @update:open="(open) => handleHierarchyPopoverOpenChange(epicEntry.epic, 'products', open)"
                        >
                          <button v-if="getProductCellDisplay(getHierarchyDraftProductEntries(epicEntry.epic).map(p => p.label)).items.length" type="button" class="inline-flex max-w-full items-center gap-1 rounded-md border text-[9px] text-highlighted transition-colors" :class="getHierarchyEditableCellButtonClass(epicEntry.epic)" :title="getProductCellDisplay(getHierarchyDraftProductEntries(epicEntry.epic).map(p => p.label)).fullLabel" :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epicEntry.epic, 'products')">
                            <span class="max-w-[140px] truncate">{{ getProductCellDisplay(getHierarchyDraftProductEntries(epicEntry.epic).map(p => p.label)).previewLabel }}</span>
                            <span v-if="!getProductCellDisplay(getHierarchyDraftProductEntries(epicEntry.epic).map(p => p.label)).allVisible" class="shrink-0 text-muted">+{{ getProductCellDisplay(getHierarchyDraftProductEntries(epicEntry.epic).map(p => p.label)).hiddenCount }}</span>
                          </button>
                          <button v-else type="button" class="text-xs transition-colors" :class="getHierarchyEditableCellButtonClass(epicEntry.epic)" :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epicEntry.epic, 'products')">—</button>
                          <template #content>
                            <div class="w-[18rem] max-w-[min(18rem,calc(100vw-2rem))] space-y-2 p-3">
                              <label v-for="product in getEditableProductOptions(epicEntry.epic)" :key="product.value" class="flex items-center gap-2 text-[11px] text-highlighted">
                                <input autofocus type="checkbox" class="h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary" :checked="getHierarchyInlineDraft(epicEntry.epic).productIds.includes(product.value)" @change="toggleHierarchyDraftProduct(epicEntry.epic, product.value, ($event.target as HTMLInputElement).checked)">
                                <span class="truncate">{{ product.label }}</span>
                              </label>
                            </div>
                          </template>
                        </UPopover>
                      </template>
                      <template v-else>
                        <span v-if="getProductCellDisplay(getEpicDisplayProductNames(epicEntry.epic)).allVisible && getProductCellDisplay(getEpicDisplayProductNames(epicEntry.epic)).items.length" class="block max-w-full truncate text-[9px] text-highlighted" :class="getHierarchyReadonlyCellClass()" :title="getProductCellDisplay(getEpicDisplayProductNames(epicEntry.epic)).fullLabel">
                          {{ getProductCellDisplay(getEpicDisplayProductNames(epicEntry.epic)).previewLabel }}
                        </span>
                        <UPopover v-else-if="getProductCellDisplay(getEpicDisplayProductNames(epicEntry.epic)).items.length" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                          <button type="button" class="inline-flex max-w-full items-center gap-1 truncate bg-transparent p-0 text-[9px] text-muted transition-colors hover:text-highlighted" :class="getHierarchyReadonlyCellClass()" :title="getProductCellDisplay(getEpicDisplayProductNames(epicEntry.epic)).fullLabel">
                            <span class="max-w-[140px] truncate">{{ getProductCellDisplay(getEpicDisplayProductNames(epicEntry.epic)).previewLabel }}</span>
                            <span class="shrink-0 text-muted">+{{ getProductCellDisplay(getEpicDisplayProductNames(epicEntry.epic)).hiddenCount }}</span>
                          </button>
                          <template #content>
                            <div class="flex min-w-44 flex-col gap-1 p-1">
                              <span v-for="product in getProductCellDisplay(getEpicDisplayProductNames(epicEntry.epic)).items" :key="`${epicEntry.epic.id}-${product}`" class="text-xs text-highlighted">
                                {{ product }}
                              </span>
                            </div>
                          </template>
                        </UPopover>
                        <span v-else class="text-xs text-muted">—</span>
                      </template>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top text-right text-[11px]" :style="{ width: getHierarchyColWidth('hours') }">
                      <template v-if="epicEntry.epic.isSimple">
                        <div v-if="isHierarchyCellEditing(epicEntry.epic, 'hours')" class="flex items-center gap-1">
                          <UInput
                            :model-value="getHierarchyInlineDraft(epicEntry.epic).hoursInput"
                            type="text"
                            inputmode="decimal"
                            size="xs"
                            autofocus
                            class="w-full"
                            :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits"
                            @blur="deactivateHierarchyCell(epicEntry.epic.id, 'hours')"
                            @keydown.esc.prevent="deactivateHierarchyCell(epicEntry.epic.id, 'hours')"
                            @keydown.enter.prevent="deactivateHierarchyCell(epicEntry.epic.id, 'hours')"
                            @update:model-value="(value) => updateHierarchyInlineDraft(epicEntry.epic, { hoursInput: String(value ?? '') })"
                          />
                          <button
                            type="button"
                            class="h-3 w-3 shrink-0 rounded-full transition-colors"
                            :class="getHierarchyInlineDraft(epicEntry.epic).hoursRed ? 'bg-red-500 hover:bg-red-600' : 'bg-muted/30 hover:bg-red-300'"
                            title="Destacar horas em vermelho"
                            @mousedown.prevent="updateHierarchyInlineDraft(epicEntry.epic, { hoursRed: !getHierarchyInlineDraft(epicEntry.epic).hoursRed })"
                          />
                        </div>
                        <button v-else-if="getDisplayedHours(epicEntry.epic) !== null || getHierarchyInlineDraft(epicEntry.epic).hoursInput" type="button" class="text-right transition-colors" :class="getHierarchyHoursButtonClass(epicEntry.epic)" :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epicEntry.epic, 'hours')">
                          {{ getHierarchyInlineDraft(epicEntry.epic).hoursInput ? `${getHierarchyInlineDraft(epicEntry.epic).hoursInput}h` : '—' }}
                        </button>
                        <button v-else type="button" class="text-right text-xs transition-colors" :class="getHierarchyEditableCellButtonClass(epicEntry.epic)" :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epicEntry.epic, 'hours')">—</button>
                      </template>
                      <template v-else>
                        <span v-if="getDisplayedHours(epicEntry.epic) !== null" :class="getHierarchyReadonlyCellClass()">{{ getDisplayedHours(epicEntry.epic) }}h</span>
                        <span v-else class="text-xs" :class="getHierarchyReadonlyCellClass(false)">—</span>
                      </template>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('classification') }">
                      <USelect
                        v-if="isHierarchyCellEditing(epicEntry.epic, 'classification')"
                        :model-value="getHierarchyInlineDraft(epicEntry.epic).classification"
                        :items="classificationSelectOptions"
                        value-key="value"
                        option-attribute="label"
                        size="xs"
                        class="w-full"
                        :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits"
                        @blur="deactivateHierarchyCell(epicEntry.epic.id, 'classification')"
                        @update:model-value="(value) => value && updateHierarchyInlineDraft(epicEntry.epic, { classification: value as RoadmapDemand['classification'] })"
                      />
                      <button v-else type="button" class="inline-flex items-center rounded-md border px-1 py-0 text-[9px] font-medium transition-colors" :class="[classificationBadgeClass[getHierarchyDraftDisplayItem(epicEntry.epic).classification], getHierarchyEditableCellButtonClass(epicEntry.epic)]" :title="classificationLabels[getHierarchyDraftDisplayItem(epicEntry.epic).classification]" :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epicEntry.epic, 'classification')">
                        {{ getClassificationDisplayLabel(getHierarchyDraftDisplayItem(epicEntry.epic).classification) }}
                      </button>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top text-left" :style="{ width: getHierarchyColWidth('customers') }">
                      <UPopover
                        :open="isHierarchyCellEditing(epicEntry.epic, 'customers')"

                        :content="{ side: 'bottom', align: 'start', sideOffset: 8 }"
                        @update:open="(open) => handleHierarchyPopoverOpenChange(epicEntry.epic, 'customers', open)"
                      >
                        <button v-if="getHierarchyDraftCustomerDisplay(epicEntry.epic).items.length" type="button" class="inline-flex max-w-full items-center gap-1 rounded-md border text-[9px] text-highlighted transition-colors" :class="getHierarchyEditableCellButtonClass(epicEntry.epic)" :title="getHierarchyDraftCustomerDisplay(epicEntry.epic).fullLabel" :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epicEntry.epic, 'customers')">
                          <span class="max-w-[140px] truncate">{{ getHierarchyDraftCustomerDisplay(epicEntry.epic).previewLabel }}</span>
                          <span v-if="!getHierarchyDraftCustomerDisplay(epicEntry.epic).allVisible" class="shrink-0 text-muted">+{{ getHierarchyDraftCustomerDisplay(epicEntry.epic).hiddenCount }}</span>
                        </button>
                        <button v-else type="button" class="text-xs transition-colors" :class="getHierarchyEditableCellButtonClass(epicEntry.epic)" :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epicEntry.epic, 'customers')">—</button>

                        <template #content>
                          <div class="w-[22rem] max-w-[min(22rem,calc(100vw-2rem))] space-y-2 p-3">
                            <div class="flex max-h-24 flex-wrap gap-1 overflow-y-auto">
                              <span v-for="customer in getHierarchyInlineDraft(epicEntry.epic).customers" :key="`${epicEntry.epic.id}-${customer}`" class="inline-flex items-center gap-1 rounded-full border border-primary/20 bg-primary/10 px-2 py-0.5 text-[10px] text-primary">
                                {{ customer }}
                                <button type="button" class="inline-flex h-3.5 w-3.5 items-center justify-center rounded-full hover:bg-primary/15" @click="removeHierarchyCustomer(epicEntry.epic, customer)">
                                  <UIcon name="i-lucide-x" class="h-3 w-3" />
                                </button>
                              </span>
                            </div>
                            <div class="flex items-center gap-2">
                              <UInput
                                :model-value="hierarchyCustomerInputs[epicEntry.epic.id] ?? ''"
                                size="sm"
                                autofocus
                                class="flex-1"
                                placeholder="Digite um novo cliente"
                                @update:model-value="(value) => hierarchyCustomerInputs = { ...hierarchyCustomerInputs, [epicEntry.epic.id]: String(value ?? '') }"
                                @keydown.enter.prevent="addHierarchyCustomer(epicEntry.epic, hierarchyCustomerInputs[epicEntry.epic.id] ?? '')"
                                @keydown.esc.prevent="deactivateHierarchyCell(epicEntry.epic.id, 'customers')"
                              />
                              <UButton size="sm" color="primary" variant="soft" :disabled="!(hierarchyCustomerInputs[epicEntry.epic.id] ?? '').trim()" @click="addHierarchyCustomer(epicEntry.epic, hierarchyCustomerInputs[epicEntry.epic.id] ?? '')">
                                Adicionar
                              </UButton>
                            </div>
                            <div v-if="getFilteredHierarchyCustomerSuggestions(epicEntry.epic).length" class="max-h-32 overflow-y-auto rounded border border-default bg-elevated/40">
                              <button v-for="customer in getFilteredHierarchyCustomerSuggestions(epicEntry.epic)" :key="customer" type="button" class="flex w-full px-2 py-1.5 text-left text-[11px] text-highlighted hover:bg-elevated" @click="addHierarchyCustomer(epicEntry.epic, customer)">
                                {{ customer }}
                              </button>
                            </div>
                          </div>
                        </template>
                      </UPopover>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('status') }">
                      <USelect
                        v-if="isHierarchyCellEditing(epicEntry.epic, 'status')"
                        :model-value="getHierarchyInlineDraft(epicEntry.epic).status"
                        :items="statusSelectOptions"
                        value-key="value"
                        option-attribute="label"
                        size="xs"
                        class="w-full"
                        :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits"
                        @blur="deactivateHierarchyCell(epicEntry.epic.id, 'status')"
                        @update:model-value="(value) => value && handleHierarchyStatusChange(epicEntry.epic, value as DemandStatus)"
                      />
                      <button v-else type="button" class="inline-flex items-center rounded-md border px-1 py-0 text-[9px] font-medium transition-colors" :class="[statusTone[getHierarchyInlineDraft(epicEntry.epic).status], getHierarchyEditableCellButtonClass(epicEntry.epic)]" :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epicEntry.epic, 'status')">
                        {{ statusLabels[getHierarchyInlineDraft(epicEntry.epic).status] }}
                      </button>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('due') }">
                      <div v-if="isHierarchyCellEditing(epicEntry.epic, 'dueDate')">
                        <UInput
                          :model-value="getHierarchyInlineDraft(epicEntry.epic).dueDate"
                          type="date"
                          size="xs"
                          autofocus
                          class="w-full"
                          :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits"
                          @blur="deactivateHierarchyCell(epicEntry.epic.id, 'dueDate')"
                          @keydown.esc.prevent="deactivateHierarchyCell(epicEntry.epic.id, 'dueDate')"
                          @keydown.enter.prevent="deactivateHierarchyCell(epicEntry.epic.id, 'dueDate')"
                          @update:model-value="(value) => updateHierarchyInlineDraft(epicEntry.epic, { dueDate: String(value ?? '') })"
                        />
                      </div>
                      <button v-else-if="getDueDateLabel(getHierarchyDraftDisplayItem(epicEntry.epic)) || getDueQuarterLabel(getHierarchyDraftDisplayItem(epicEntry.epic)) || isDelayed(getHierarchyDraftDisplayItem(epicEntry.epic))" type="button" class="flex min-w-0 items-center gap-1 rounded-md border px-1 py-0.5 text-left transition-colors" :class="getHierarchyEditableCellButtonClass(epicEntry.epic)" :title="getDueTooltip(getHierarchyDraftDisplayItem(epicEntry.epic))" :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epicEntry.epic, 'dueDate')">
                        <span v-if="getDueDateLabel(getHierarchyDraftDisplayItem(epicEntry.epic))" :class="getDueDateClass(getHierarchyDraftDisplayItem(epicEntry.epic))">{{ getDueDateLabel(getHierarchyDraftDisplayItem(epicEntry.epic)) }}</span>
                        <span v-if="getDueQuarterLabel(getHierarchyDraftDisplayItem(epicEntry.epic))" class="inline-flex shrink-0 items-center rounded-md border border-default bg-elevated px-1 py-0 text-[8px] font-medium text-muted">
                          {{ getDueQuarterLabel(getHierarchyDraftDisplayItem(epicEntry.epic)) }}
                        </span>
                      </button>
                      <button v-else type="button" class="flex min-w-0 items-center gap-1 rounded-md border px-1 py-0.5 text-left transition-colors" :class="getHierarchyEditableCellButtonClass(epicEntry.epic)" :disabled="isHierarchyInlineSaving(epicEntry.epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epicEntry.epic, 'dueDate')">
                        <span class="text-xs text-muted">—</span>
                      </button>
                    </td>
                    <td class="border-b border-default px-2 py-0.5 align-top" :style="{ width: getHierarchyColWidth('kpi') }">
                      <div class="flex min-w-0 flex-col items-start gap-1" :class="getHierarchyReadonlyCellClass()">
                        <button type="button" class="inline-flex items-center rounded-md border px-1 py-0 text-[9px] font-medium transition-colors hover:opacity-80" :class="getKpiSummary(epicEntry.epic).tone" :title="getKpiSummary(epicEntry.epic).actionLabel" @click="openKpiWorkspace(epicEntry.epic)">
                          {{ getKpiSummary(epicEntry.epic).label }}
                        </button>
                        <span v-if="getKpiSecondaryLabel(epicEntry.epic)" class="text-[11px] text-muted">
                          {{ getKpiSecondaryLabel(epicEntry.epic) }}
                        </span>
                      </div>
                    </td>

                    <td class="border-b border-default relative overflow-visible !p-0" :style="{ width: getHierarchyColWidth('actions') }">
                      <div class="group absolute inset-0 flex items-center justify-center">
                        <span class="pointer-events-none select-none text-[10px] text-muted/40 transition-opacity group-hover:opacity-0">···</span>
                        <div class="pointer-events-none absolute inset-y-0 right-0 z-30 flex items-center gap-0.5 rounded-md border border-default/60 bg-default/95 px-1 opacity-0 shadow-md backdrop-blur-sm transition-opacity group-hover:pointer-events-auto group-hover:opacity-100">
                          <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-line-chart" class="h-6 w-6 p-0" title="Abrir KPIs do épico" @click.stop="openKpiWorkspace(epicEntry.epic)" />
                          <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-plus" class="h-6 w-6 p-0" title="Nova demanda" @click.stop="startCreateDemandForEpic(epicEntry.epic)" />
                          <UButton v-if="!epicEntry.epic.isSimple && !getDemandsForEpic(epicEntry.epic.id).length" size="xs" variant="ghost" color="primary" icon="i-lucide-minimize-2" class="h-6 w-6 p-0" title="Transformar em épico simples" @click.stop="promptConvertEpicToSimple(epicEntry.epic)" />
                          <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" class="h-6 w-6 p-0" title="Editar épico" @click.stop="openEditModal(epicEntry.epic)" />
                          <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" class="h-6 w-6 p-0" title="Excluir épico" @click.stop="promptDelete(epicEntry.epic)" />
                        </div>
                      </div>
                    </td>
                  </tr>

                  <tr
                    v-for="demand in epicEntry.demands"
                    v-show="!isRoadmapCollapsed(group.roadmap.id) && !isEpicCollapsed(epicEntry.epic.id)"
                    :key="demand.id"
                    class="bg-default hover:bg-elevated/10 transition-colors"
                  >
                    <td class="border-b border-default px-2.5 py-0.5 align-top">
                      <div class="flex items-start gap-1 pl-12">
                        <input
                          type="checkbox"
                          class="mt-1 h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary"
                          :checked="isHierarchyItemSelected(demand.id)"
                          @click.stop
                          @change="toggleHierarchyItemSelection(demand.id, ($event.target as HTMLInputElement).checked)"
                        >
                        <div class="min-w-0 flex-1" :class="getCrossProjectWatermarkClass(demand)">
                          <div class="flex flex-wrap items-center gap-1">
                            <span v-if="isOutsideSelectedProject(demand)" class="inline-flex items-center rounded-md border border-warning/40 bg-warning/10 px-1 py-0 text-[8px] font-semibold uppercase tracking-[0.06em] text-warning">
                              Outro time
                            </span>
                            <span v-if="demand.type === 'Spillover'" class="inline-flex items-center gap-0.5 rounded-md border border-amber-200 bg-amber-50 px-1 py-0 text-[8px] font-semibold text-amber-700 dark:border-amber-800 dark:bg-amber-900/20 dark:text-amber-300">
                              <UIcon name="i-lucide-forward" class="h-2.5 w-2.5" />
                              Transbordo
                            </span>
                          </div>
                          <div class="mt-0.5 flex items-start gap-1">
                            <UIcon name="i-lucide-list-todo" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-sky-600" :class="getCrossProjectWatermarkClass(demand)" />
                            <div class="flex min-w-0 flex-1 items-center gap-1">
                              <UInput
                                v-if="isHierarchyCellEditing(demand, 'title')"
                                :model-value="getHierarchyInlineDraft(demand).title"
                                size="xs"
                                autofocus
                                class="min-w-0 flex-1 w-full"
                                :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits"
                                @blur="deactivateHierarchyCell(demand.id, 'title')"
                                @keydown.esc.prevent="deactivateHierarchyCell(demand.id, 'title')"
                                @keydown.enter.prevent="deactivateHierarchyCell(demand.id, 'title')"
                                @update:model-value="(value) => updateHierarchyInlineDraft(demand, { title: String(value ?? '') })"
                              />
                              <button v-else type="button" class="min-w-0 flex-1 truncate rounded-md border px-1 py-0.5 text-left text-[12px] font-medium text-highlighted transition-colors" :class="getHierarchyEditableCellButtonClass(demand)" :title="demand.description || undefined" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'title')">{{ getHierarchyDraftDisplayItem(demand).title }}</button>
                              <UIcon v-if="getDemandProblemKeys(demand).length" name="i-lucide-triangle-alert" class="h-3.5 w-3.5 shrink-0 text-warning" :title="getDemandProblemTooltip(demand)" />
                              <a
                                v-if="getDisplayIssueLinks(demand).length === 1 && getDisplayIssueLinks(demand)[0]?.url"
                                :href="getDisplayIssueLinks(demand)[0]?.url"
                                target="_blank"
                                rel="noreferrer"
                                class="inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-default bg-default px-1 text-[9px] font-medium text-primary transition-colors hover:border-primary/40"
                              >
                                <UIcon name="i-simple-icons-jira" class="h-3 w-3" />
                              </a>
                              <UPopover v-else-if="getDisplayIssueLinks(demand).length" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                                <button type="button" class="inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-default bg-default px-1 text-[9px] font-medium text-primary transition-colors hover:border-primary/40">
                                  <UIcon name="i-simple-icons-jira" class="h-3 w-3" />
                                </button>
                                <template #content>
                                  <div class="flex min-w-40 flex-col gap-1 p-1">
                                    <a
                                      v-for="issue in getDisplayIssueLinks(demand)"
                                      :key="`${demand.id}-${issue.key}`"
                                      :href="issue.url || undefined"
                                      :target="issue.url ? '_blank' : undefined"
                                      rel="noreferrer"
                                      class="inline-flex items-center rounded-md border border-default bg-default px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:border-primary/40"
                                    >
                                      {{ issue.key }}
                                    </a>
                                  </div>
                                </template>
                              </UPopover>
                              <button v-else type="button" class="inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-red-200 bg-default px-1 text-[9px] font-medium text-red-500 transition-colors hover:border-red-400 dark:border-red-800 dark:text-red-400" title="Sem issue Jira — clique para adicionar" @click="openEditModal(demand, { focusField: 'jiraIssue' })"><UIcon name="i-simple-icons-jira" class="h-3 w-3" /></button>
                            </div>
                          </div>
                        </div>
                      </div>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top text-left" :style="{ width: getHierarchyColWidth('products') }">
                      <UPopover
                        :open="isHierarchyCellEditing(demand, 'products')"
                        :content="{ side: 'bottom', align: 'start', sideOffset: 8 }"
                        @update:open="(open) => handleHierarchyPopoverOpenChange(demand, 'products', open)"
                      >
                        <button v-if="getProductCellDisplay(getHierarchyDraftProductEntries(demand).map(product => product.label)).items.length" type="button" class="inline-flex max-w-full items-center gap-1 rounded-md border text-[9px] text-highlighted transition-colors" :class="getHierarchyEditableCellButtonClass(demand)" :title="getProductCellDisplay(getHierarchyDraftProductEntries(demand).map(product => product.label)).fullLabel" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'products')">
                          <span class="max-w-[140px] truncate">{{ getProductCellDisplay(getHierarchyDraftProductEntries(demand).map(product => product.label)).previewLabel }}</span>
                          <span v-if="!getProductCellDisplay(getHierarchyDraftProductEntries(demand).map(product => product.label)).allVisible" class="shrink-0 text-muted">+{{ getProductCellDisplay(getHierarchyDraftProductEntries(demand).map(product => product.label)).hiddenCount }}</span>
                        </button>
                        <button v-else type="button" class="text-xs transition-colors" :class="getHierarchyEditableCellButtonClass(demand)" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'products')">—</button>

                        <template #content>
                          <div class="w-[18rem] max-w-[min(18rem,calc(100vw-2rem))] space-y-2 p-3">
                            <label v-for="product in getEditableProductOptions(demand)" :key="product.value" class="flex items-center gap-2 text-[11px] text-highlighted">
                              <input autofocus type="checkbox" class="h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary" :checked="getHierarchyInlineDraft(demand).productIds.includes(product.value)" @change="toggleHierarchyDraftProduct(demand, product.value, ($event.target as HTMLInputElement).checked)">
                              <span class="truncate">{{ product.label }}</span>
                            </label>
                          </div>
                        </template>
                      </UPopover>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top text-right text-[11px]" :style="{ width: getHierarchyColWidth('hours') }">
                      <div v-if="isHierarchyCellEditing(demand, 'hours')" class="flex items-center gap-1">
                        <UInput
                          :model-value="getHierarchyInlineDraft(demand).hoursInput"
                          type="text"
                          inputmode="decimal"
                          size="xs"
                          autofocus
                          class="w-full"
                          :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits"
                          @blur="deactivateHierarchyCell(demand.id, 'hours')"
                          @keydown.esc.prevent="deactivateHierarchyCell(demand.id, 'hours')"
                          @keydown.enter.prevent="deactivateHierarchyCell(demand.id, 'hours')"
                          @update:model-value="(value) => updateHierarchyInlineDraft(demand, { hoursInput: String(value ?? '') })"
                        />
                        <button
                          type="button"
                          class="h-3 w-3 shrink-0 rounded-full transition-colors"
                          :class="getHierarchyInlineDraft(demand).hoursRed ? 'bg-red-500 hover:bg-red-600' : 'bg-muted/30 hover:bg-red-300'"
                          title="Destacar horas em vermelho"
                          @mousedown.prevent="updateHierarchyInlineDraft(demand, { hoursRed: !getHierarchyInlineDraft(demand).hoursRed })"
                        />
                      </div>
                      <button v-else type="button" class="text-right transition-colors" :class="getHierarchyHoursButtonClass(demand)" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'hours')">
                        {{ getHierarchyInlineDraft(demand).hoursInput ? `${getHierarchyInlineDraft(demand).hoursInput}h` : '—' }}
                      </button>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('classification') }">
                      <span class="inline-flex items-center rounded-md border px-1 py-0 text-[9px] font-medium" :class="[classificationBadgeClass[getDisplayedClassification(demand)], getHierarchyReadonlyCellClass()]" :title="classificationLabels[getDisplayedClassification(demand)]">
                        {{ getClassificationDisplayLabel(getDisplayedClassification(demand)) }}
                      </span>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top text-left" :style="{ width: getHierarchyColWidth('customers') }">
                      <span v-if="getCustomerCellDisplay(demand).allVisible && getCustomerCellDisplay(demand).items.length" class="block max-w-full truncate text-[9px] text-highlighted" :class="getHierarchyReadonlyCellClass()" :title="getCustomerCellDisplay(demand).fullLabel">
                        {{ getCustomerCellDisplay(demand).previewLabel }}
                      </span>
                      <UPopover v-else-if="getCustomerCellDisplay(demand).items.length" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                        <button type="button" class="inline-flex max-w-full items-center gap-1 truncate bg-transparent p-0 text-[9px] text-muted transition-colors hover:text-highlighted" :class="getHierarchyReadonlyCellClass()" :title="getCustomerCellDisplay(demand).fullLabel">
                          <span class="max-w-[140px] truncate">{{ getCustomerCellDisplay(demand).previewLabel }}</span>
                          <span class="shrink-0 text-muted">+{{ getCustomerCellDisplay(demand).hiddenCount }}</span>
                        </button>
                        <template #content>
                          <div class="flex max-w-xs flex-col gap-1 p-2">
                            <span v-for="customer in getCustomerCellDisplay(demand).items" :key="`${demand.id}-${customer}`" class="text-xs text-highlighted">
                              {{ customer }}
                            </span>
                          </div>
                        </template>
                      </UPopover>
                      <span v-else class="text-xs" :class="getHierarchyReadonlyCellClass(false)">—</span>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('status') }">
                      <USelect
                        v-if="isHierarchyCellEditing(demand, 'status')"
                        :model-value="getHierarchyInlineDraft(demand).status"
                        :items="statusSelectOptions"
                        value-key="value"
                        option-attribute="label"
                        size="xs"
                        class="w-full"
                        :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits"
                        @blur="deactivateHierarchyCell(demand.id, 'status')"
                        @update:model-value="(value) => value && handleHierarchyStatusChange(demand, value as DemandStatus)"
                      />
                      <button v-else type="button" class="inline-flex items-center rounded-md border px-1 py-0 text-[9px] font-medium transition-colors" :class="[statusTone[getHierarchyInlineDraft(demand).status], getHierarchyEditableCellButtonClass(demand)]" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'status')">
                        {{ statusLabels[getHierarchyInlineDraft(demand).status] }}
                      </button>
                    </td>

                    <td class="border-b border-default px-2.5 py-0.5 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('due') }">
                      <div v-if="isHierarchyCellEditing(demand, 'dueDate')">
                        <UInput
                          :model-value="getHierarchyInlineDraft(demand).dueDate"
                          type="date"
                          size="xs"
                          autofocus
                          class="w-full"
                          :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits"
                          @blur="deactivateHierarchyCell(demand.id, 'dueDate')"
                          @keydown.esc.prevent="deactivateHierarchyCell(demand.id, 'dueDate')"
                          @keydown.enter.prevent="deactivateHierarchyCell(demand.id, 'dueDate')"
                          @update:model-value="(value) => updateHierarchyInlineDraft(demand, { dueDate: String(value ?? '') })"
                        />
                      </div>
                      <button v-else-if="getDueDateLabel(getHierarchyDraftDisplayItem(demand)) || getDueQuarterLabel(getHierarchyDraftDisplayItem(demand)) || isDelayed(getHierarchyDraftDisplayItem(demand))" type="button" class="flex min-w-0 items-center gap-1 rounded-md border px-1 py-0.5 text-left transition-colors" :class="getHierarchyEditableCellButtonClass(demand)" :title="getDueTooltip(getHierarchyDraftDisplayItem(demand))" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'dueDate')">
                        <span v-if="getDueDateLabel(getHierarchyDraftDisplayItem(demand))" :class="getDueDateClass(getHierarchyDraftDisplayItem(demand))">{{ getDueDateLabel(getHierarchyDraftDisplayItem(demand)) }}</span>
                        <span v-if="getDueQuarterLabel(getHierarchyDraftDisplayItem(demand))" class="inline-flex shrink-0 items-center rounded-md border border-default bg-elevated px-1 py-0 text-[8px] font-medium text-muted">
                          {{ getDueQuarterLabel(getHierarchyDraftDisplayItem(demand)) }}
                        </span>
                      </button>
                      <button v-else type="button" class="flex min-w-0 items-center gap-1 rounded-md border px-1 py-0.5 text-left transition-colors" :class="getHierarchyEditableCellButtonClass(demand)" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'dueDate')">
                        <span class="text-xs text-muted">—</span>
                      </button>
                    </td>

                    <td class="border-b border-default px-2 py-0.5 align-top" :style="{ width: getHierarchyColWidth('kpi') }">
                      <span class="text-xs text-muted">—</span>
                    </td>

                    <td class="border-b border-default relative overflow-visible !p-0" :style="{ width: getHierarchyColWidth('actions') }">
                      <div class="group absolute inset-0 flex items-center justify-center">
                        <span class="pointer-events-none select-none text-[10px] text-muted/40 transition-opacity group-hover:opacity-0">···</span>
                        <div class="pointer-events-none absolute inset-y-0 right-0 z-30 flex items-center gap-0.5 rounded-md border border-default/60 bg-default/95 px-1 opacity-0 shadow-md backdrop-blur-sm transition-opacity group-hover:pointer-events-auto group-hover:opacity-100">
                          <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" class="h-6 w-6 p-0" title="Editar demanda" @click.stop="openEditModal(demand)" />
                          <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" class="h-6 w-6 p-0" title="Remover demanda" @click.stop="promptDelete(demand)" />
                        </div>
                      </div>
                    </td>
                  </tr>
                </template>
              </template>

              <tr v-if="displayOrphanEpics.length" class="bg-elevated/60">
                <td colspan="9" class="border-b border-default px-3 py-2 text-[11px] font-semibold uppercase tracking-[0.08em] text-muted">
                  Épicos sem roadmap visível
                </td>
              </tr>

              <tr
                v-for="epic in displayOrphanEpics"
                :key="`orphan-${epic.id}`"
                class="bg-rose-50/30 hover:bg-rose-50/50 dark:bg-rose-950/10 dark:hover:bg-rose-950/20 transition-colors"
              >
                <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('item') }">
                  <div class="flex items-start gap-1">
                    <input
                      type="checkbox"
                      class="mt-1 h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary"
                      :checked="isHierarchyItemSelected(epic.id)"
                      @click.stop
                      @change="toggleHierarchyItemSelection(epic.id, ($event.target as HTMLInputElement).checked)"
                    >
                    <UIcon name="i-lucide-triangle-alert" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-rose-500" />
                    <div class="min-w-0 flex-1">
                      <span class="inline-flex items-center rounded-md border border-rose-200 bg-rose-50 px-1 py-0 text-[8px] font-semibold uppercase tracking-[0.06em] text-rose-700 dark:border-rose-800 dark:bg-rose-900/20 dark:text-rose-300">
                        Épico órfão
                      </span>
                      <div class="mt-0.5 flex min-w-0 items-center gap-1">
                        <UInput
                          v-if="isHierarchyCellEditing(epic, 'title')"
                          :model-value="getHierarchyInlineDraft(epic).title"
                          size="xs"
                          autofocus
                          class="min-w-0 flex-1 w-full"
                          :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits"
                          @blur="deactivateHierarchyCell(epic.id, 'title')"
                          @keydown.esc.prevent="deactivateHierarchyCell(epic.id, 'title')"
                          @keydown.enter.prevent="deactivateHierarchyCell(epic.id, 'title')"
                          @update:model-value="(value) => updateHierarchyInlineDraft(epic, { title: String(value ?? '') })"
                        />
                        <button v-else type="button" class="min-w-0 flex-1 truncate rounded-md border px-1 py-0.5 text-left text-[12px] font-medium text-highlighted transition-colors" :class="getHierarchyEditableCellButtonClass(epic)" :title="epic.description || undefined" :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epic, 'title')">{{ getHierarchyDraftDisplayItem(epic).title }}</button>
                        <UIcon v-if="getDemandProblemKeys(epic).length" name="i-lucide-triangle-alert" class="h-3.5 w-3.5 shrink-0 text-warning" :title="getDemandProblemTooltip(epic)" />
                        <a
                          v-if="getDisplayIssueLinks(epic).length === 1 && getDisplayIssueLinks(epic)[0]?.url"
                          :href="getDisplayIssueLinks(epic)[0]?.url"
                          target="_blank"
                          rel="noreferrer"
                          class="inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-default bg-default px-1 text-[9px] font-medium text-primary transition-colors hover:border-primary/40"
                        >
                          <UIcon name="i-simple-icons-jira" class="h-3 w-3" />
                        </a>
                        <UPopover v-else-if="getDisplayIssueLinks(epic).length" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                          <button type="button" class="inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-default bg-default px-1 text-[9px] font-medium text-primary transition-colors hover:border-primary/40">
                            <UIcon name="i-simple-icons-jira" class="h-3 w-3" />
                          </button>
                          <template #content>
                            <div class="flex min-w-40 flex-col gap-1 p-1">
                              <a
                                v-for="issue in getDisplayIssueLinks(epic)"
                                :key="`orphan-${epic.id}-${issue.key}`"
                                :href="issue.url || undefined"
                                :target="issue.url ? '_blank' : undefined"
                                rel="noreferrer"
                                class="inline-flex items-center rounded-md border border-default bg-default px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:border-primary/40"
                              >
                                {{ issue.key }}
                              </a>
                            </div>
                          </template>
                        </UPopover>
                        <button v-else type="button" class="inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-red-200 bg-default px-1 text-[9px] font-medium text-red-500 transition-colors hover:border-red-400 dark:border-red-800 dark:text-red-400" title="Sem issue Jira — clique para adicionar" @click="openEditModal(epic, { focusField: 'jiraIssue' })"><UIcon name="i-simple-icons-jira" class="h-3 w-3" /></button>
                      </div>
                    </div>
                  </div>
                </td>
                    <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('status') }">
                      <USelect
                        v-if="isHierarchyCellEditing(epic, 'status')"
                        :model-value="getHierarchyInlineDraft(epic).status"
                        :items="statusSelectOptions"
                        value-key="value"
                        option-attribute="label"
                        size="xs"
                        class="w-full"
                        :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits"
                        @blur="deactivateHierarchyCell(epic.id, 'status')"
                        @update:model-value="(value) => value && handleHierarchyStatusChange(epic, value as DemandStatus)"
                      />
                      <button v-else type="button" class="inline-flex items-center rounded-md border px-1 py-0 text-[9px] font-medium transition-colors" :class="[statusTone[getHierarchyInlineDraft(epic).status], getHierarchyEditableCellButtonClass(epic)]" :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epic, 'status')">
                        {{ statusLabels[getHierarchyInlineDraft(epic).status] }}
                      </button>
                </td>
                <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('products') }">
                  <template v-if="epic.isSimple">
                    <UPopover
                      :open="isHierarchyCellEditing(epic, 'products')"
                      :content="{ side: 'bottom', align: 'start', sideOffset: 8 }"
                      @update:open="(open) => handleHierarchyPopoverOpenChange(epic, 'products', open)"
                    >
                      <button v-if="getProductCellDisplay(getHierarchyDraftProductEntries(epic).map(p => p.label)).items.length" type="button" class="inline-flex max-w-full items-center gap-1 rounded-md border text-[11px] text-highlighted transition-colors" :class="getHierarchyEditableCellButtonClass(epic)" :title="getProductCellDisplay(getHierarchyDraftProductEntries(epic).map(p => p.label)).fullLabel" :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epic, 'products')">
                        <span class="max-w-[140px] truncate">{{ getProductCellDisplay(getHierarchyDraftProductEntries(epic).map(p => p.label)).previewLabel }}</span>
                        <span v-if="!getProductCellDisplay(getHierarchyDraftProductEntries(epic).map(p => p.label)).allVisible" class="shrink-0 text-muted">+{{ getProductCellDisplay(getHierarchyDraftProductEntries(epic).map(p => p.label)).hiddenCount }}</span>
                      </button>
                      <button v-else type="button" class="text-xs transition-colors" :class="getHierarchyEditableCellButtonClass(epic)" :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epic, 'products')">—</button>
                      <template #content>
                        <div class="w-[18rem] max-w-[min(18rem,calc(100vw-2rem))] space-y-2 p-3">
                          <label v-for="product in getEditableProductOptions(epic)" :key="product.value" class="flex items-center gap-2 text-[11px] text-highlighted">
                            <input autofocus type="checkbox" class="h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary" :checked="getHierarchyInlineDraft(epic).productIds.includes(product.value)" @change="toggleHierarchyDraftProduct(epic, product.value, ($event.target as HTMLInputElement).checked)">
                            <span class="truncate">{{ product.label }}</span>
                          </label>
                        </div>
                      </template>
                    </UPopover>
                  </template>
                  <template v-else>
                    <span v-if="getProductCellDisplay(getEpicDisplayProductNames(epic)).allVisible && getProductCellDisplay(getEpicDisplayProductNames(epic)).items.length" class="block max-w-full truncate text-[11px] text-highlighted" :class="getHierarchyReadonlyCellClass()" :title="getProductCellDisplay(getEpicDisplayProductNames(epic)).fullLabel">
                      {{ getProductCellDisplay(getEpicDisplayProductNames(epic)).previewLabel }}
                    </span>
                    <UPopover v-else-if="getProductCellDisplay(getEpicDisplayProductNames(epic)).items.length" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                      <button type="button" :class="[getHierarchyReadonlyOverflowTriggerClass(), getHierarchyReadonlyCellClass()]" :title="getProductCellDisplay(getEpicDisplayProductNames(epic)).fullLabel">
                        <span class="max-w-[140px] truncate">{{ getProductCellDisplay(getEpicDisplayProductNames(epic)).previewLabel }}</span>
                        <span class="shrink-0 text-muted">+{{ getProductCellDisplay(getEpicDisplayProductNames(epic)).hiddenCount }}</span>
                      </button>
                      <template #content>
                        <div class="flex min-w-44 flex-col gap-1 p-1">
                          <span v-for="product in getProductCellDisplay(getEpicDisplayProductNames(epic)).items" :key="`${epic.id}-${product}`" class="text-xs text-highlighted">
                            {{ product }}
                          </span>
                        </div>
                      </template>
                    </UPopover>
                    <span v-else class="text-xs text-muted">—</span>
                  </template>
                </td>
                <td class="border-b border-default px-2.5 py-0.5 align-top text-[11px]" :style="{ width: getHierarchyColWidth('hours') }">
                  <template v-if="epic.isSimple">
                    <div v-if="isHierarchyCellEditing(epic, 'hours')" class="flex items-center gap-1">
                      <UInput
                        :model-value="getHierarchyInlineDraft(epic).hoursInput"
                        type="text"
                        inputmode="decimal"
                        size="xs"
                        autofocus
                        class="w-full"
                        :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits"
                        @blur="deactivateHierarchyCell(epic.id, 'hours')"
                        @keydown.esc.prevent="deactivateHierarchyCell(epic.id, 'hours')"
                        @keydown.enter.prevent="deactivateHierarchyCell(epic.id, 'hours')"
                        @update:model-value="(value) => updateHierarchyInlineDraft(epic, { hoursInput: String(value ?? '') })"
                      />
                      <button
                        type="button"
                        class="h-3 w-3 shrink-0 rounded-full transition-colors"
                        :class="getHierarchyInlineDraft(epic).hoursRed ? 'bg-red-500 hover:bg-red-600' : 'bg-muted/30 hover:bg-red-300'"
                        title="Destacar horas em vermelho"
                        @mousedown.prevent="updateHierarchyInlineDraft(epic, { hoursRed: !getHierarchyInlineDraft(epic).hoursRed })"
                      />
                    </div>
                    <button v-else-if="getDisplayedHours(epic) !== null || getHierarchyInlineDraft(epic).hoursInput" type="button" class="text-right transition-colors" :class="getHierarchyHoursButtonClass(epic)" :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epic, 'hours')">
                      {{ getHierarchyInlineDraft(epic).hoursInput ? `${getHierarchyInlineDraft(epic).hoursInput}h` : '—' }}
                    </button>
                    <button v-else type="button" class="text-right text-xs transition-colors" :class="getHierarchyEditableCellButtonClass(epic)" :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epic, 'hours')">—</button>
                  </template>
                  <template v-else>
                    <span v-if="getDisplayedHours(epic) !== null" :class="getHierarchyReadonlyCellClass()">{{ getDisplayedHours(epic) }}h</span>
                    <span v-else class="text-xs" :class="getHierarchyReadonlyCellClass(false)">—</span>
                  </template>
                </td>
                <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('classification') }">
                  <USelect
                    v-if="isHierarchyCellEditing(epic, 'classification')"
                    :model-value="getHierarchyInlineDraft(epic).classification"
                    :items="classificationSelectOptions"
                    value-key="value"
                    option-attribute="label"
                    size="xs"
                    class="w-full"
                    :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits"
                    @blur="deactivateHierarchyCell(epic.id, 'classification')"
                    @update:model-value="(value) => value && updateHierarchyInlineDraft(epic, { classification: value as RoadmapDemand['classification'] })"
                  />
                  <button v-else type="button" class="inline-flex items-center rounded-md border px-1 py-0 text-[9px] font-medium transition-colors" :class="[classificationBadgeClass[getHierarchyDraftDisplayItem(epic).classification], getHierarchyEditableCellButtonClass(epic)]" :title="classificationLabels[getHierarchyDraftDisplayItem(epic).classification]" :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epic, 'classification')">
                    {{ getClassificationDisplayLabel(getHierarchyDraftDisplayItem(epic).classification) }}
                  </button>
                </td>
                <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('customers') }">
                  <UPopover
                    :open="isHierarchyCellEditing(epic, 'customers')"
                    :content="{ side: 'bottom', align: 'start', sideOffset: 8 }"
                    @update:open="(open) => handleHierarchyPopoverOpenChange(epic, 'customers', open)"
                  >
                    <button v-if="getHierarchyDraftCustomerDisplay(epic).items.length" type="button" class="inline-flex max-w-full items-center gap-1 rounded-md border px-1 py-0 text-[9px] text-highlighted transition-colors" :class="getHierarchyEditableCellButtonClass(epic)" :title="getHierarchyDraftCustomerDisplay(epic).fullLabel" :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epic, 'customers')">
                      <span class="max-w-[140px] truncate">{{ getHierarchyDraftCustomerDisplay(epic).previewLabel }}</span>
                      <span v-if="!getHierarchyDraftCustomerDisplay(epic).allVisible" class="shrink-0 text-muted">+{{ getHierarchyDraftCustomerDisplay(epic).hiddenCount }}</span>
                    </button>
                    <button v-else type="button" class="text-xs transition-colors" :class="getHierarchyEditableCellButtonClass(epic)" :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epic, 'customers')">—</button>

                    <template #content>
                      <div class="w-[22rem] max-w-[min(22rem,calc(100vw-2rem))] space-y-2 p-3">
                        <div class="flex max-h-24 flex-wrap gap-1 overflow-y-auto">
                          <span v-for="customer in getHierarchyInlineDraft(epic).customers" :key="`${epic.id}-${customer}`" class="inline-flex items-center gap-1 rounded-full border border-primary/20 bg-primary/10 px-2 py-0.5 text-[10px] text-primary">
                            {{ customer }}
                            <button type="button" class="inline-flex h-3.5 w-3.5 items-center justify-center rounded-full hover:bg-primary/15" @click="removeHierarchyCustomer(epic, customer)">
                              <UIcon name="i-lucide-x" class="h-3 w-3" />
                            </button>
                          </span>
                        </div>
                        <div class="flex items-center gap-2">
                          <UInput
                            :model-value="hierarchyCustomerInputs[epic.id] ?? ''"
                            size="sm"
                            autofocus
                            class="flex-1"
                            placeholder="Digite um novo cliente"
                            @update:model-value="(value) => hierarchyCustomerInputs = { ...hierarchyCustomerInputs, [epic.id]: String(value ?? '') }"
                            @keydown.enter.prevent="addHierarchyCustomer(epic, hierarchyCustomerInputs[epic.id] ?? '')"
                            @keydown.esc.prevent="deactivateHierarchyCell(epic.id, 'customers')"
                          />
                          <UButton size="sm" color="primary" variant="soft" :disabled="!(hierarchyCustomerInputs[epic.id] ?? '').trim()" @click="addHierarchyCustomer(epic, hierarchyCustomerInputs[epic.id] ?? '')">
                            Adicionar
                          </UButton>
                        </div>
                        <div v-if="getFilteredHierarchyCustomerSuggestions(epic).length" class="max-h-32 overflow-y-auto rounded border border-default bg-elevated/40">
                          <button v-for="customer in getFilteredHierarchyCustomerSuggestions(epic)" :key="customer" type="button" class="flex w-full px-2 py-1.5 text-left text-[11px] text-highlighted hover:bg-elevated" @click="addHierarchyCustomer(epic, customer)">
                            {{ customer }}
                          </button>
                        </div>
                      </div>
                    </template>
                  </UPopover>
                </td>
                <td class="border-b border-default px-2.5 py-0.5 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('due') }">
                  <div v-if="isHierarchyCellEditing(epic, 'dueDate')">
                    <UInput
                      :model-value="getHierarchyInlineDraft(epic).dueDate"
                      type="date"
                      size="xs"
                      autofocus
                      class="w-full"
                      :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits"
                      @blur="deactivateHierarchyCell(epic.id, 'dueDate')"
                      @keydown.esc.prevent="deactivateHierarchyCell(epic.id, 'dueDate')"
                      @keydown.enter.prevent="deactivateHierarchyCell(epic.id, 'dueDate')"
                      @update:model-value="(value) => updateHierarchyInlineDraft(epic, { dueDate: String(value ?? '') })"
                    />
                  </div>
                  <button v-else-if="getDueDateLabel(getHierarchyDraftDisplayItem(epic)) || getDueQuarterLabel(getHierarchyDraftDisplayItem(epic)) || isDelayed(getHierarchyDraftDisplayItem(epic))" type="button" class="flex min-w-0 items-center gap-1 rounded-md border px-1 py-0.5 text-left transition-colors" :class="getHierarchyEditableCellButtonClass(epic)" :title="getDueTooltip(getHierarchyDraftDisplayItem(epic))" :disabled="isHierarchyInlineSaving(epic.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(epic, 'dueDate')">
                    <span v-if="getDueDateLabel(getHierarchyDraftDisplayItem(epic))" :class="getDueDateClass(getHierarchyDraftDisplayItem(epic))">{{ getDueDateLabel(getHierarchyDraftDisplayItem(epic)) }}</span>
                    <span v-if="getDueQuarterLabel(getHierarchyDraftDisplayItem(epic))" class="inline-flex shrink-0 items-center rounded-md border border-default bg-elevated px-1 py-0 text-[8px] font-medium text-muted">
                      {{ getDueQuarterLabel(getHierarchyDraftDisplayItem(epic)) }}
                    </span>
                  </button>
                  <span v-else class="text-xs text-muted">—</span>
                </td>
                <td class="border-b border-default px-2 py-0.5 align-top" :style="{ width: getHierarchyColWidth('kpi') }">
                  <div class="flex min-w-0 flex-col items-start gap-1" :class="getHierarchyReadonlyCellClass()">
                    <button type="button" class="inline-flex items-center rounded-md border px-1 py-0 text-[9px] font-medium transition-colors hover:opacity-80" :class="getKpiSummary(epic).tone" :title="getKpiSummary(epic).actionLabel" @click="openKpiWorkspace(epic)">
                      {{ getKpiSummary(epic).label }}
                    </button>
                    <span v-if="getKpiSecondaryLabel(epic)" class="text-[11px] text-muted">
                      {{ getKpiSecondaryLabel(epic) }}
                    </span>
                  </div>
                </td>
                <td class="border-b border-default relative overflow-visible !p-0" :style="{ width: getHierarchyColWidth('actions') }">
                  <div class="group absolute inset-0 flex items-center justify-center">
                    <span class="pointer-events-none select-none text-[10px] text-muted/40 transition-opacity group-hover:opacity-0">···</span>
                    <div class="pointer-events-none absolute inset-y-0 right-0 z-30 flex items-center gap-0.5 rounded-md border border-default/60 bg-default/95 px-1 opacity-0 shadow-md backdrop-blur-sm transition-opacity group-hover:pointer-events-auto group-hover:opacity-100">
                      <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-line-chart" class="h-6 w-6 p-0" title="Abrir KPIs do épico" @click.stop="openKpiWorkspace(epic)" />
                      <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-plus" class="h-6 w-6 p-0" title="Nova demanda" @click.stop="startCreateDemandForEpic(epic)" />
                      <UButton v-if="!epic.isSimple && !getDemandsForEpic(epic.id).length" size="xs" variant="ghost" color="primary" icon="i-lucide-minimize-2" class="h-6 w-6 p-0" title="Transformar em épico simples" @click.stop="promptConvertEpicToSimple(epic)" />
                      <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" class="h-6 w-6 p-0" title="Editar épico" @click.stop="openEditModal(epic)" />
                      <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" class="h-6 w-6 p-0" title="Excluir épico" @click.stop="promptDelete(epic)" />
                    </div>
                  </div>
                </td>
              </tr>

              <tr v-if="displayOrphanDemands.length" class="bg-elevated/60">
                <td colspan="9" class="border-b border-default px-3 py-2 text-[11px] font-semibold uppercase tracking-[0.08em] text-muted">
                  Demandas sem épico visível
                </td>
              </tr>

              <tr
                v-for="demand in displayOrphanDemands"
                :key="`orphan-demand-${demand.id}`"
                class="bg-sky-50/20 hover:bg-sky-50/40 dark:bg-sky-950/10 dark:hover:bg-sky-950/20 transition-colors"
              >
                <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('item') }">
                  <div class="flex items-start gap-1">
                    <input
                      type="checkbox"
                      class="mt-1 h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary"
                      :checked="isHierarchyItemSelected(demand.id)"
                      @click.stop
                      @change="toggleHierarchyItemSelection(demand.id, ($event.target as HTMLInputElement).checked)"
                    >
                    <UIcon name="i-lucide-link-2-off" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-sky-600" />
                    <div class="min-w-0 flex-1">
                      <div class="flex flex-wrap items-center gap-1">
                        <span class="inline-flex items-center rounded-md border border-sky-200 bg-sky-50 px-1 py-0 text-[8px] font-semibold uppercase tracking-[0.06em] text-sky-700 dark:border-sky-800 dark:bg-sky-900/20 dark:text-sky-300">
                          Demanda órfã
                        </span>
                        <span v-if="hasPlannedQuarter(demand) && demand.quarterLabel" class="inline-flex items-center rounded-md border border-default bg-elevated px-1 py-0 text-[8px] font-medium text-muted">
                          {{ demand.quarterLabel }}
                        </span>
                        <span v-else class="text-xs text-muted">—</span>
                      </div>
                      <div class="mt-0.5 flex min-w-0 items-center gap-1">
                          <UInput
                            v-if="isHierarchyCellEditing(demand, 'title')"
                            :model-value="getHierarchyInlineDraft(demand).title"
                            size="xs"
                            autofocus
                            class="min-w-0 flex-1 w-full"
                            :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits"
                            @blur="deactivateHierarchyCell(demand.id, 'title')"
                            @keydown.esc.prevent="deactivateHierarchyCell(demand.id, 'title')"
                            @keydown.enter.prevent="deactivateHierarchyCell(demand.id, 'title')"
                            @update:model-value="(value) => updateHierarchyInlineDraft(demand, { title: String(value ?? '') })"
                          />
                          <button v-else type="button" class="min-w-0 flex-1 truncate rounded-md border px-1 py-0.5 text-left text-[12px] font-medium text-highlighted transition-colors" :class="getHierarchyEditableCellButtonClass(demand)" :title="demand.description || undefined" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'title')">{{ getHierarchyDraftDisplayItem(demand).title }}</button>
                          <UIcon v-if="getDemandProblemKeys(demand).length" name="i-lucide-triangle-alert" class="h-3.5 w-3.5 shrink-0 text-warning" :title="getDemandProblemTooltip(demand)" />
                        <a
                          v-if="getDisplayIssueLinks(demand).length === 1 && getDisplayIssueLinks(demand)[0]?.url"
                          :href="getDisplayIssueLinks(demand)[0]?.url"
                          target="_blank"
                          rel="noreferrer"
                          class="inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-default bg-default px-1 text-[9px] font-medium text-primary transition-colors hover:border-primary/40"
                        >
                          <UIcon name="i-simple-icons-jira" class="h-3 w-3" />
                        </a>
                        <UPopover v-else-if="getDisplayIssueLinks(demand).length" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                          <button type="button" class="inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-default bg-default px-1 text-[9px] font-medium text-primary transition-colors hover:border-primary/40">
                            <UIcon name="i-simple-icons-jira" class="h-3 w-3" />
                          </button>
                          <template #content>
                            <div class="flex min-w-40 flex-col gap-1 p-1">
                              <a
                                v-for="issue in getDisplayIssueLinks(demand)"
                                :key="`orphan-demand-${demand.id}-${issue.key}`"
                                :href="issue.url || undefined"
                                :target="issue.url ? '_blank' : undefined"
                                rel="noreferrer"
                                class="inline-flex items-center rounded-md border border-default bg-default px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:border-primary/40"
                              >
                                {{ issue.key }}
                              </a>
                            </div>
                          </template>
                        </UPopover>
                      </div>
                    </div>
                  </div>
                </td>
                <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('status') }">
                  <USelect
                    v-if="isHierarchyCellEditing(demand, 'status')"
                    :model-value="getHierarchyInlineDraft(demand).status"
                    :items="statusSelectOptions"
                    value-key="value"
                    option-attribute="label"
                    size="xs"
                    class="w-full"
                    :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits"
                    @blur="deactivateHierarchyCell(demand.id, 'status')"
                    @update:model-value="(value) => value && handleHierarchyStatusChange(demand, value as DemandStatus)"
                  />
                  <button v-else type="button" class="inline-flex items-center rounded-md border px-1 py-0 text-[9px] font-medium transition-colors" :class="[statusTone[getHierarchyInlineDraft(demand).status], getHierarchyEditableCellButtonClass(demand)]" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'status')">
                    {{ statusLabels[getHierarchyInlineDraft(demand).status] }}
                  </button>
                </td>
                <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('products') }">
                  <UPopover
                    :open="isHierarchyCellEditing(demand, 'products')"
                    :content="{ side: 'bottom', align: 'start', sideOffset: 8 }"
                    @update:open="(open) => handleHierarchyPopoverOpenChange(demand, 'products', open)"
                  >
                    <button v-if="getProductCellDisplay(getHierarchyDraftProductEntries(demand).map(product => product.label)).items.length" type="button" class="inline-flex max-w-full items-center gap-1 rounded-md border px-1 py-0 text-[9px] text-highlighted transition-colors" :class="getHierarchyEditableCellButtonClass(demand)" :title="getProductCellDisplay(getHierarchyDraftProductEntries(demand).map(product => product.label)).fullLabel" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'products')">
                      <span class="max-w-[140px] truncate">{{ getProductCellDisplay(getHierarchyDraftProductEntries(demand).map(product => product.label)).previewLabel }}</span>
                      <span v-if="!getProductCellDisplay(getHierarchyDraftProductEntries(demand).map(product => product.label)).allVisible" class="shrink-0 text-muted">+{{ getProductCellDisplay(getHierarchyDraftProductEntries(demand).map(product => product.label)).hiddenCount }}</span>
                    </button>
                    <button v-else type="button" class="text-xs transition-colors" :class="getHierarchyEditableCellButtonClass(demand)" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'products')">—</button>

                    <template #content>
                      <div class="w-[18rem] max-w-[min(18rem,calc(100vw-2rem))] space-y-2 p-3">
                        <label v-for="product in getEditableProductOptions(demand)" :key="product.value" class="flex items-center gap-2 text-[11px] text-highlighted">
                          <input autofocus type="checkbox" class="h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary" :checked="getHierarchyInlineDraft(demand).productIds.includes(product.value)" @change="toggleHierarchyDraftProduct(demand, product.value, ($event.target as HTMLInputElement).checked)">
                          <span class="truncate">{{ product.label }}</span>
                        </label>
                      </div>
                    </template>
                  </UPopover>
                </td>
                <td class="border-b border-default px-2.5 py-0.5 align-top text-[11px]" :style="{ width: getHierarchyColWidth('hours') }">
                  <div v-if="isHierarchyCellEditing(demand, 'hours')" class="flex items-center gap-1">
                    <UInput
                      :model-value="getHierarchyInlineDraft(demand).hoursInput"
                      type="text"
                      inputmode="decimal"
                      size="xs"
                      autofocus
                      class="w-full"
                      :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits"
                      @blur="deactivateHierarchyCell(demand.id, 'hours')"
                      @keydown.esc.prevent="deactivateHierarchyCell(demand.id, 'hours')"
                      @keydown.enter.prevent="deactivateHierarchyCell(demand.id, 'hours')"
                      @update:model-value="(value) => updateHierarchyInlineDraft(demand, { hoursInput: String(value ?? '') })"
                    />
                    <button
                      type="button"
                      class="h-3 w-3 shrink-0 rounded-full transition-colors"
                      :class="getHierarchyInlineDraft(demand).hoursRed ? 'bg-red-500 hover:bg-red-600' : 'bg-muted/30 hover:bg-red-300'"
                      title="Destacar horas em vermelho"
                      @mousedown.prevent="updateHierarchyInlineDraft(demand, { hoursRed: !getHierarchyInlineDraft(demand).hoursRed })"
                    />
                  </div>
                  <button v-else type="button" class="text-right transition-colors" :class="getHierarchyHoursButtonClass(demand)" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'hours')">
                    {{ getHierarchyInlineDraft(demand).hoursInput ? `${getHierarchyInlineDraft(demand).hoursInput}h` : '—' }}
                  </button>
                </td>
                <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('classification') }">
                  <span class="inline-flex items-center rounded-md border px-1 py-0 text-[9px] font-medium" :class="[classificationBadgeClass[getDisplayedClassification(demand)], getHierarchyReadonlyCellClass()]" :title="classificationLabels[getDisplayedClassification(demand)]">
                    {{ getClassificationDisplayLabel(getDisplayedClassification(demand)) }}
                  </span>
                </td>
                <td class="border-b border-default px-2.5 py-0.5 align-top" :style="{ width: getHierarchyColWidth('customers') }">
                  <span v-if="getCustomerCellDisplay(demand).allVisible && getCustomerCellDisplay(demand).items.length" class="block max-w-full truncate text-[11px] text-highlighted" :class="getHierarchyReadonlyCellClass()" :title="getCustomerCellDisplay(demand).fullLabel">
                    {{ getCustomerCellDisplay(demand).previewLabel }}
                  </span>
                  <UPopover v-else-if="getCustomerCellDisplay(demand).items.length" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                    <button type="button" :class="[getHierarchyReadonlyOverflowTriggerClass(), getHierarchyReadonlyCellClass()]" :title="getCustomerCellDisplay(demand).fullLabel">
                      <span class="max-w-[140px] truncate">{{ getCustomerCellDisplay(demand).previewLabel }}</span>
                      <span class="shrink-0 text-muted">+{{ getCustomerCellDisplay(demand).hiddenCount }}</span>
                    </button>
                    <template #content>
                      <div class="flex max-w-xs flex-col gap-1 p-2">
                        <span v-for="customer in getCustomerCellDisplay(demand).items" :key="`${demand.id}-${customer}`" class="text-xs text-highlighted">
                          {{ customer }}
                        </span>
                      </div>
                    </template>
                  </UPopover>
                  <span v-else class="text-xs" :class="getHierarchyReadonlyCellClass(false)">—</span>
                </td>
                <td class="border-b border-default px-2.5 py-0.5 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('due') }">
                  <div v-if="isHierarchyCellEditing(demand, 'dueDate')">
                    <UInput
                      :model-value="getHierarchyInlineDraft(demand).dueDate"
                      type="date"
                      size="xs"
                      autofocus
                      class="w-full"
                      :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits"
                      @blur="deactivateHierarchyCell(demand.id, 'dueDate')"
                      @keydown.esc.prevent="deactivateHierarchyCell(demand.id, 'dueDate')"
                      @keydown.enter.prevent="deactivateHierarchyCell(demand.id, 'dueDate')"
                      @update:model-value="(value) => updateHierarchyInlineDraft(demand, { dueDate: String(value ?? '') })"
                    />
                  </div>
                  <button v-else-if="getDueDateLabel(getHierarchyDraftDisplayItem(demand)) || getDueQuarterLabel(getHierarchyDraftDisplayItem(demand)) || isDelayed(getHierarchyDraftDisplayItem(demand))" type="button" class="flex min-w-0 items-center gap-1 rounded-md border px-1 py-0.5 text-left transition-colors" :class="getHierarchyEditableCellButtonClass(demand)" :title="getDueTooltip(getHierarchyDraftDisplayItem(demand))" :disabled="isHierarchyInlineSaving(demand.id) || isSavingAllHierarchyEdits" @click="activateHierarchyCell(demand, 'dueDate')">
                    <span v-if="getDueDateLabel(getHierarchyDraftDisplayItem(demand))" :class="getDueDateClass(getHierarchyDraftDisplayItem(demand))">{{ getDueDateLabel(getHierarchyDraftDisplayItem(demand)) }}</span>
                    <span v-if="getDueQuarterLabel(getHierarchyDraftDisplayItem(demand))" class="inline-flex shrink-0 items-center rounded-md border border-default bg-elevated px-1 py-0 text-[8px] font-medium text-muted">
                      {{ getDueQuarterLabel(getHierarchyDraftDisplayItem(demand)) }}
                    </span>
                  </button>
                  <span v-else class="text-xs text-muted">—</span>
                </td>
                <td class="border-b border-default px-2 py-0.5 align-top" :style="{ width: getHierarchyColWidth('kpi') }">
                  <span class="text-xs text-muted">—</span>
                </td>
                <td class="border-b border-default relative overflow-visible !p-0" :style="{ width: getHierarchyColWidth('actions') }">
                  <div class="group absolute inset-0 flex items-center justify-center">
                    <span class="pointer-events-none select-none text-[10px] text-muted/40 transition-opacity group-hover:opacity-0">···</span>
                    <div class="pointer-events-none absolute inset-y-0 right-0 z-30 flex items-center gap-0.5 rounded-md border border-default/60 bg-default/95 px-1 opacity-0 shadow-md backdrop-blur-sm transition-opacity group-hover:pointer-events-auto group-hover:opacity-100">
                      <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" class="h-6 w-6 p-0" title="Editar demanda" @click.stop="openEditModal(demand)" />
                      <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" class="h-6 w-6 p-0" title="Remover demanda" @click.stop="promptDelete(demand)" />
                    </div>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </template>

    <div v-if="hierarchyPendingEditCount" class="pointer-events-none fixed inset-x-0 bottom-4 z-40 flex justify-center px-4">
      <div class="pointer-events-auto flex w-full max-w-fit items-center gap-3 rounded-2xl border border-primary/20 bg-default/95 px-4 py-3 shadow-2xl backdrop-blur supports-[backdrop-filter]:bg-default/85">
        <div class="min-w-0">
          <p class="text-sm font-semibold text-highlighted">Edições pendentes</p>
          <p class="text-xs text-muted">{{ hierarchyPendingEditCount.toLocaleString('pt-BR') }} alteração(ões) aguardando confirmação.</p>
        </div>
        <UButton
          size="sm"
          color="neutral"
          variant="outline"
          icon="i-lucide-rotate-ccw"
          :disabled="isSavingAllHierarchyEdits || !!hierarchyInlineSavingIds.length"
          @click="discardAllHierarchyInlineEdits"
        >
          Descartar
        </UButton>
        <UButton
          size="sm"
          color="primary"
          variant="solid"
          icon="i-lucide-check-check"
          class="font-semibold shadow-sm"
          :loading="isSavingAllHierarchyEdits"
          :disabled="isSavingAllHierarchyEdits"
          @click="saveAllHierarchyInlineEdits"
        >
          {{ hierarchyPendingEditLabel }}
        </UButton>
      </div>
    </div>

    <BulkEditRoadmapItemsModal
      v-model:open="bulkEditModalOpen"
      :selected-items="selectedHierarchyItems"
      :dependency-options="dependencyOptions"
      :is-saving="isBulkEditing"
      :hide-row-color="true"
      @submit="handleBulkEditSubmit"
    />

    <RoadmapDemandFormModal
      v-model:open="modalOpen"
      :projects="projects"
      :dependency-options="dependencyOptions"
      :customer-suggestions="customerSuggestions"
      :demand="editingDemand"
      :default-item-type="createItemType"
      :default-parent-demand-id="defaultParentDemandId"
      :default-project-id="defaultProjectId ?? currentPrimaryProjectId ?? undefined"
      :default-project-ids="defaultProjectIds.length ? defaultProjectIds : selectedProjectIds"
      :default-quarter-year="defaultQuarterYear"
      :default-quarter-number="defaultQuarterNumber"
      :default-type="defaultType"
      :default-hours="defaultHours"
      :default-product-ids="defaultProductIds"
      :force-simple-epic="forceSimpleEpic"
      :roadmap-options="allRoadmapItems.map(item => ({ id: item.id, title: item.title, projectId: item.projectId, projectIds: item.projectIds }))"
      :epic-options="allEpicItems.map(item => ({
        id: item.id,
        title: item.title,
        roadmapTitle: item.roadmapTitle,
        status: item.status,
        projectId: item.projectId,
        projectIds: item.projectIds
      }))"
      :available-kpis="availableKpis"
      :is-saving="isSavingDemand"
      :focus-field="modalEditFocusField"
      @trade-off-deleted="handleTradeOffDeleted"
      @submit="handleSubmit"
    />

    <UModal v-model:open="hierarchyStatusModalOpen" :title="hierarchyStatusModalDraft ? `Completar status: ${statusLabels[hierarchyStatusModalDraft.status]}` : 'Completar status'" :ui="{ content: 'sm:max-w-2xl' }" @update:open="(open) => { if (!open) closeHierarchyStatusModal({ restoreSnapshot: true }) }">
      <template #body>
        <div v-if="hierarchyStatusModalDraft" class="space-y-4">
          <p class="text-sm text-muted">Alguns status exigem dados adicionais antes de salvar a grade.</p>

          <UFormField v-if="hierarchyStatusModalRequiresDeliveryDate" label="Data de entrega" required>
            <UInput
              :model-value="hierarchyStatusModalDraft.deliveryDate"
              type="date"
              autofocus
              class="w-full"
              @update:model-value="(value) => hierarchyStatusModalItem && updateHierarchyInlineDraft(hierarchyStatusModalItem, { deliveryDate: String(value ?? '') })"
            />
          </UFormField>

          <UFormField v-if="hierarchyStatusModalRequiresBlockedReason" label="Motivo do impedimento" required>
            <UTextarea
              :model-value="hierarchyStatusModalDraft.blockedReason"
              :rows="5"
              autofocus
              class="w-full"
              @update:model-value="(value) => hierarchyStatusModalItem && updateHierarchyInlineDraft(hierarchyStatusModalItem, { blockedReason: String(value ?? '') })"
            />
          </UFormField>

          <template v-if="hierarchyStatusModalRequiresDeprioritization">
            <UFormField label="Motivo da despriorização" required>
              <USelect
                :model-value="hierarchyStatusModalDraft.deprioritizationReason"
                :items="deprioritizationReasonOptions"
                value-key="value"
                option-attribute="label"
                class="w-full"
                @update:model-value="(value) => hierarchyStatusModalItem && updateHierarchyInlineDraft(hierarchyStatusModalItem, { deprioritizationReason: value as DeprioritizationReason | undefined })"
              />
            </UFormField>

            <UFormField label="Demanda priorizada no lugar" hint="Opcional">
              <USelect
                :model-value="hierarchyStatusModalDraft.replacementDemandId"
                :items="hierarchyStatusReplacementDemandOptions"
                value-key="value"
                option-attribute="label"
                placeholder="Selecione uma demanda"
                class="w-full"
                @update:model-value="(value) => hierarchyStatusModalItem && updateHierarchyInlineDraft(hierarchyStatusModalItem, { replacementDemandId: value ? String(value) : undefined })"
              />
            </UFormField>

            <UFormField label="Observação" required>
              <UTextarea
                :model-value="hierarchyStatusModalDraft.observation"
                :rows="5"
                class="w-full"
                @update:model-value="(value) => hierarchyStatusModalItem && updateHierarchyInlineDraft(hierarchyStatusModalItem, { observation: String(value ?? '') })"
              />
            </UFormField>
          </template>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton color="neutral" variant="outline" @click="closeHierarchyStatusModal({ restoreSnapshot: true })">
            Cancelar
          </UButton>
          <UButton color="primary" @click="confirmHierarchyStatusModal">
            Confirmar
          </UButton>
        </div>
      </template>
    </UModal>

    <UModal
      v-model:open="confirmConvertToCompositeOpen"
      title="Transformar em épico com demandas"
      description="Ao criar uma demanda neste épico, ele passará a ser controlado por demandas. Os atributos do épico (produto, quarter, tipo e horas) serão removidos e deverão ser controlados em cada demanda. Esses valores serão migrados automaticamente para a primeira demanda."
    >
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            variant="outline"
            color="neutral"
            label="Cancelar"
            @click="confirmConvertToCompositeOpen = false; convertSourceEpic = null"
          />
          <UButton
            color="primary"
            icon="i-lucide-list-todo"
            label="Continuar e criar demanda"
            @click="confirmCreateDemandInSimpleEpic"
          />
        </div>
      </template>
    </UModal>

    <UModal
      v-model:open="confirmDeleteOpen"
      :title="deleteTarget?.itemType === 'Roadmap' ? 'Excluir Roadmap' : 'Excluir Épico'"
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
