<script setup lang="ts">
import type { ApiResponse } from '~/types/api'
import type { DemandFormData, DemandStatus, RoadmapDemand, RoadmapItemType } from '~/types/roadmap'
import { buildDemandDueSearchText, buildDueSortKey, hasPlannedQuarter } from '~/utils/roadmapDue'
import { getLatestPromisedDate } from '~/utils/roadmapPromisedDate'

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
const deleteTarget = ref<RoadmapDemand | null>(null)
const confirmDeleteOpen = ref(false)
const collapsedRoadmapIds = ref<string[]>([])
const collapsedEpicIds = ref<string[]>([])
const isSavingDemand = ref(false)
const isHierarchyLoading = ref(false)
const hierarchyDemands = ref<RoadmapDemand[]>([])
const selectedProjectIds = ref<string[]>([])
const hierarchyItemFilter = ref('')
const hierarchyStatusFilter = ref<string[]>([])
const hierarchyClassificationFilter = ref<string[]>([])
const hierarchyProductsFilter = ref<string[]>([])
const hierarchyCustomersFilter = ref('')
const hierarchyDueFilter = ref('')
const hierarchySort = ref<{ key: HierarchySortKey | null, direction: 'asc' | 'desc' }>({ key: null, direction: 'asc' })
const hierarchyTableContainerRef = ref<HTMLElement | null>(null)
const hierarchyContainerWidth = ref(0)
const hierarchyHeaderScrollLeft = ref(0)
let hierarchyWidthObserver: ResizeObserver | null = null

const HIERARCHY_COL_MIN = 60
const hierarchyColumnOrder: HierarchyColumnId[] = ['item', 'status', 'products', 'hours', 'classification', 'customers', 'due', 'kpi', 'actions']
const hierarchyColumnDefaults: Record<HierarchyColumnId, number> = {
  item: 360,
  status: 88,
  products: 128,
  hours: 56,
  classification: 104,
  customers: 128,
  due: 112,
  kpi: 64,
  actions: 116
}
const hierarchyColumnSizing = ref<Partial<Record<HierarchyColumnId, number>>>({})

function updateHierarchyContainerWidth() {
  hierarchyContainerWidth.value = hierarchyTableContainerRef.value?.clientWidth ?? 0
}

function syncHierarchyHeaderScroll() {
  hierarchyHeaderScrollLeft.value = hierarchyTableContainerRef.value?.scrollLeft ?? 0
}

function getHierarchyColSize(columnId: HierarchyColumnId) {
  if (columnId === 'item' && hierarchyColumnSizing.value.item == null) {
    const otherColumnsTotal = hierarchyColumnOrder
      .filter(currentColumnId => currentColumnId !== 'item')
      .reduce((total, currentColumnId) => total + (hierarchyColumnSizing.value[currentColumnId] ?? hierarchyColumnDefaults[currentColumnId]), 0)

    return Math.max(HIERARCHY_COL_MIN, hierarchyContainerWidth.value - otherColumnsTotal)
  }

  return hierarchyColumnSizing.value[columnId] ?? hierarchyColumnDefaults[columnId]
}

function getHierarchyColWidth(columnId: HierarchyColumnId) {
  return `${getHierarchyColSize(columnId)}px`
}

function startHierarchyResize(columnId: HierarchyColumnId, event: MouseEvent) {
  event.preventDefault()
  event.stopPropagation()

  const startX = event.clientX
  const startWidth = getHierarchyColSize(columnId)

  const onMove = (moveEvent: MouseEvent) => {
    hierarchyColumnSizing.value = {
      ...hierarchyColumnSizing.value,
      [columnId]: Math.max(HIERARCHY_COL_MIN, startWidth + (moveEvent.clientX - startX))
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

const allRoadmapItems = computed(() => hierarchyDemands.value.filter(item => item.itemType === 'Roadmap'))
const allEpicItems = computed(() => hierarchyDemands.value.filter(item => item.itemType === 'Epic'))
const allDemandItems = computed(() => hierarchyDemands.value.filter(item => item.itemType === 'Demand'))
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
    return 'Todos os projetos'

  if (selectedProjectIds.value.length === 1)
    return projectNameById.value.get(selectedProjectIds.value[0]!) ?? '1 projeto'

  return `${selectedProjectIds.value.length} projetos`
})

const classificationFilterOptions = computed(() =>
  Object.entries(classificationLabels).map(([value, label]) => ({ value, label }))
)

const statusFilterOptions = computed(() =>
  Object.entries(statusLabels).map(([value, label]) => ({ value, label }))
)

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

const statusLabels: Record<DemandStatus, string> = {
  Backlog: 'Backlog',
  InProgress: 'Doing',
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
    if (!product.productId || !product.name)
      continue

    if (!productsMap.has(product.productId))
      productsMap.set(product.productId, product.name)
  }

  return Array.from(productsMap.entries()).map(([value, label]) => ({ value, label }))
}

function getProductNames(item: Pick<RoadmapDemand, 'products'>) {
  return getProductEntries(item).map(product => product.label)
}

function normalizeCustomers(customers?: string[]) {
  return (customers ?? []).map(customer => customer.trim()).filter(Boolean)
}

function getDisplayedCustomers(item: RoadmapDemand) {
  if (item.itemType === 'Epic')
    return normalizeCustomers(item.customers)

  if (item.itemType === 'Demand' && item.epicId)
    return normalizeCustomers(epicById.value.get(item.epicId)?.customers)

  return normalizeCustomers(item.customers)
}

function getCustomersLine(customers: string[]) {
  return customers.join(' · ')
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

function getRoadmapGroupProductNames(epics: RoadmapDemand[]) {
  return getRoadmapGroupProductEntries(epics).map(product => product.label)
}

function getRoadmapGroupProductEntries(epics: RoadmapDemand[]) {
  const productsMap = new Map<string, string>()

  epics.flatMap(epic => [
    ...getProductEntries(epic),
    ...getDemandsForEpic(epic.id).flatMap(demand => getProductEntries(demand))
  ]).forEach((product) => {
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
    .sort((left, right) => {
      if (left.quarterYear !== right.quarterYear)
        return left.quarterYear - right.quarterYear

      if (left.quarterNumber !== right.quarterNumber)
        return left.quarterNumber - right.quarterNumber

      return left.sortOrder - right.sortOrder
    })
}

function getDisplayedHours(item: RoadmapDemand) {
  const values = (item.itemType === 'Roadmap'
    ? demandItems.value.filter(demand => demand.roadmapId === item.id)
    : item.itemType === 'Epic'
      ? getDemandsForEpic(item.id)
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
      return [...items].sort((left, right) => {
        if (left.quarterYear !== right.quarterYear)
          return left.quarterYear - right.quarterYear

        if (left.quarterNumber !== right.quarterNumber)
          return left.quarterNumber - right.quarterNumber

        return left.sortOrder - right.sortOrder
      })
    }

    return [...items].sort((left, right) => left.sortOrder - right.sortOrder)
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
            ? getRoadmapGroupProductNames(roadmapGroups.value.find(group => group.roadmap.id === left.id)?.epics ?? [])
            : left.itemType === 'Epic'
              ? getEpicDisplayProductNames(left)
              : getProductNames(left)),
          getProductNamesLine(right.itemType === 'Roadmap'
            ? getRoadmapGroupProductNames(roadmapGroups.value.find(group => group.roadmap.id === right.id)?.epics ?? [])
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
      const roadmapMatches = matchesHierarchyFilters(roadmap, {
        products: getRoadmapGroupProductEntries(sourceEpics).map(product => product.value),
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

          if (hasActiveProductFilter && !epicMatches && matchingDemands.length === 0)
            return null

          if (!roadmapMatches && !epicMatches && matchingDemands.length === 0)
            return null

          return {
            epic,
            demands: hasActiveProductFilter
              ? matchingDemands
              : (roadmapMatches || epicMatches ? sortItems(projectScopedDemands, 'demand') : matchingDemands)
          }
        })
        .filter((entry): entry is DisplayEpicGroup => !!entry)

      if ((!roadmapMatches && epics.length === 0) || (hasActiveProductFilter && epics.length === 0))
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
    return
  }

  selectedProjectIds.value = [...selectedProjectIds.value, projectId]
}

function clearProjectFilter() {
  selectedProjectIds.value = []
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

function openCreateModal(
  itemType?: RoadmapItemType,
  parentDemandId?: string,
  defaults?: { projectId?: string, projectIds?: string[] }
) {
  createItemType.value = itemType
  defaultParentDemandId.value = parentDemandId
  defaultProjectId.value = defaults?.projectId
  defaultProjectIds.value = defaults?.projectIds ?? []
  editingDemand.value = null
  modalOpen.value = true
}

function openEditModal(item: RoadmapDemand) {
  editingDemand.value = item
  defaultParentDemandId.value = undefined
  defaultProjectId.value = undefined
  defaultProjectIds.value = []
  modalOpen.value = true
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

  try {
    isSavingDemand.value = true
    if (editingDemand.value) {
      await roadmapStore.updateDemand(editingDemand.value.id, data)
      await loadPageData()
      toast.add({ title: 'Item atualizado', color: 'success' })
    }
    else {
      await roadmapStore.createDemand(data)
      await loadPageData()
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

await roadmapStore.fetchProjects()

const initialProjectIds = [
  ...(typeof route.query.projectIds === 'string'
    ? route.query.projectIds.split(',')
    : []),
  ...(typeof route.query.projectId === 'string'
    ? [route.query.projectId]
    : [])
]

selectedProjectIds.value = [...new Set(initialProjectIds)]
  .filter(projectId => projects.value.some(project => project.id === projectId))

await loadPageData()
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
          <UFormField label="Projeto" class="w-full lg:max-w-sm">
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
                    Todos os projetos
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

          <div class="flex flex-wrap items-center gap-1.5 lg:flex-nowrap lg:whitespace-nowrap lg:pb-0.5">
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
              variant="outline"
              icon="i-lucide-fold-vertical"
              :disabled="!hasCollapsibleRoadmaps && !hasCollapsibleEpics"
              @click="collapseAllRoadmaps(); collapseAllEpics()"
            >
              Recolher tudo
            </UButton>
            <UButton
              size="xs"
              color="neutral"
              variant="outline"
              icon="i-lucide-unfold-vertical"
              :disabled="(!collapsedRoadmapIds.length && !collapsedEpicIds.length)"
              @click="expandAllRoadmaps(); expandAllEpics()"
            >
              Expandir tudo
            </UButton>
          </div>
        </div>

        <div class="flex flex-wrap items-center gap-1.5 text-[11px] text-muted">
          <span class="rounded-full border border-default bg-elevated px-2.5 py-0.5">{{ roadmapItems.length }} roadmaps</span>
          <span class="rounded-full border border-default bg-elevated px-2.5 py-0.5">{{ epicItems.length }} épicos</span>
          <span class="rounded-full border border-default bg-elevated px-2.5 py-0.5">{{ demandItems.length }} demandas</span>
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
                <th class="relative border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('status') }">
                  <button type="button" class="inline-flex items-center gap-1 transition-colors hover:text-highlighted" @click="toggleHierarchySort('status')">
                    <span>Status</span>
                    <UIcon :name="getHierarchySortIcon('status')" class="h-3.5 w-3.5" />
                  </button>
                  <span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('status', $event)" />
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
                <th class="relative border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('due') }">
                  <button type="button" class="inline-flex items-center gap-1 transition-colors hover:text-highlighted" @click="toggleHierarchySort('due')">
                    <span>Conclusão</span>
                    <UIcon :name="getHierarchySortIcon('due')" class="h-3.5 w-3.5" />
                  </button>
                  <span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('due', $event)" />
                </th>
                <th class="relative border-b border-default bg-elevated/95 px-2 py-2" :style="{ width: getHierarchyColWidth('kpi') }">KPI<span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('kpi', $event)" /></th>
                <th class="relative border-b border-default bg-elevated/95 px-3 py-2 text-right" :style="{ width: getHierarchyColWidth('actions') }">Ações<span class="absolute inset-y-0 right-0 w-2 cursor-col-resize" @mousedown.prevent.stop="startHierarchyResize('actions', $event)" /></th>
              </tr>
              <tr class="bg-elevated/60 text-left text-[11px] text-muted">
                <th class="border-b border-default bg-elevated/95 px-3 py-2" :style="{ width: getHierarchyColWidth('item') }">
                  <input v-model="hierarchyItemFilter" type="text" placeholder="Filtrar..." class="w-full rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted outline-none transition-colors placeholder:text-muted focus:border-primary/40" >
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
                  <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('item') }">
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

                      <div class="min-w-0 flex-1" :class="getCrossProjectWatermarkClass(group.roadmap)">
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

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('products') }">
                      <p v-if="getRoadmapGroupProductNames(group.epics.map(entry => entry.epic)).length" class="max-w-[160px] overflow-hidden text-[11px] leading-4 text-highlighted [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:2]" :title="getProductNamesLine(getRoadmapGroupProductNames(group.epics.map(entry => entry.epic)))">
                      {{ getProductNamesLine(getRoadmapGroupProductNames(group.epics.map(entry => entry.epic))) }}
                    </p>
                    <span v-else class="text-xs text-muted">—</span>
                  </td>

                  <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('hours') }">
                    <span v-if="getDisplayedHours(group.roadmap) !== null">{{ getDisplayedHours(group.roadmap) }}h</span>
                    <span v-else class="text-xs text-muted">—</span>
                  </td>

                  <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('classification') }">
                    <span class="text-xs text-muted">—</span>
                  </td>

                  <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('customers') }">
                    <span class="text-xs text-muted">—</span>
                  </td>

                  <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('due') }">
                    {{ formatDate(getDisplayedConclusionDate(group.roadmap)) }}
                  </td>

                  <td class="border-b border-default px-2 py-2 align-top" :style="{ width: getHierarchyColWidth('kpi') }">
                    <span class="text-xs text-muted">—</span>
                  </td>

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('actions') }">
                      <div class="ml-auto grid w-full max-w-full grid-cols-4 justify-items-end gap-1">
                        <span class="h-6 w-6" />
                      <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-plus" title="Novo épico" @click="openCreateModal('Epic', group.roadmap.id, { projectIds: getItemProjectIds(group.roadmap) })" />
                      <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" title="Editar roadmap" @click="openEditModal(group.roadmap)" />
                      <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" title="Excluir roadmap" @click="promptDelete(group.roadmap)" />
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
                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('item') }">
                      <div class="flex items-start gap-1.5 pl-8">
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
                        <UIcon name="i-lucide-star" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-amber-500" :class="getCrossProjectWatermarkClass(epicEntry.epic)" />
                        <div class="min-w-0 flex-1" :class="getCrossProjectWatermarkClass(epicEntry.epic)">
                          <div class="flex flex-wrap items-center gap-1.5">
                            <span class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-muted">
                              Épico
                            </span>
                            <span v-if="isOutsideSelectedProject(epicEntry.epic)" class="inline-flex items-center rounded-md border border-warning/40 bg-warning/10 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-warning">
                              Outro projeto
                            </span>
                            <span class="inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[9px] font-medium text-muted">
                              {{ epicEntry.demands.length }} demandas
                            </span>
                            <a
                              v-for="issue in getDisplayIssueLinks(epicEntry.epic)"
                              :key="`${epicEntry.epic.id}-${issue.key}`"
                              :href="issue.url || undefined"
                              :target="issue.url ? '_blank' : undefined"
                              rel="noreferrer"
                              class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] font-medium text-primary transition-colors hover:border-primary/40"
                            >
                              {{ issue.key }}
                            </a>
                          </div>
                          <p class="mt-0.5 truncate text-[13px] font-medium text-highlighted" :title="epicEntry.epic.description || undefined">{{ epicEntry.epic.title }}</p>
                        </div>
                      </div>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('status') }">
                      <span class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="statusTone[epicEntry.epic.status]">
                        {{ statusLabels[epicEntry.epic.status] }}
                      </span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('products') }">
                      <p v-if="getEpicDisplayProductNames(epicEntry.epic).length" class="max-w-[160px] overflow-hidden text-[11px] leading-4 text-highlighted [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:2]" :title="getProductNamesLine(getEpicDisplayProductNames(epicEntry.epic))">
                        {{ getProductNamesLine(getEpicDisplayProductNames(epicEntry.epic)) }}
                      </p>
                      <span v-else class="text-xs text-muted">—</span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('hours') }">
                      <span v-if="getDisplayedHours(epicEntry.epic) !== null">{{ getDisplayedHours(epicEntry.epic) }}h</span>
                      <span v-else class="text-xs text-muted">—</span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('classification') }">
                      <span class="inline-flex items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium" :class="classificationBadgeClass[epicEntry.epic.classification]">
                        {{ classificationLabels[epicEntry.epic.classification] }}
                      </span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('customers') }">
                      <p v-if="getDisplayedCustomers(epicEntry.epic).length" class="max-w-[130px] overflow-hidden text-[11px] leading-4 text-highlighted [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:2]" :title="getCustomersLine(getDisplayedCustomers(epicEntry.epic))">
                        {{ getCustomersLine(getDisplayedCustomers(epicEntry.epic)) }}
                      </p>
                      <span v-else class="text-xs text-muted">—</span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('due') }">
                      {{ formatDate(getDisplayedConclusionDate(epicEntry.epic)) }}
                    </td>

                    <td class="border-b border-default px-2 py-2 align-top" :style="{ width: getHierarchyColWidth('kpi') }">
                      <div class="flex min-w-0 flex-col items-start gap-1">
                        <button type="button" class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium transition-colors hover:opacity-80" :class="getKpiSummary(epicEntry.epic).tone" :title="getKpiSummary(epicEntry.epic).actionLabel" @click="openKpiWorkspace(epicEntry.epic)">
                          {{ getKpiSummary(epicEntry.epic).label }}
                        </button>
                        <span v-if="getKpiSecondaryLabel(epicEntry.epic)" class="text-[11px] text-muted">
                          {{ getKpiSecondaryLabel(epicEntry.epic) }}
                        </span>
                      </div>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('actions') }">
                      <div class="ml-auto grid w-full max-w-full grid-cols-4 justify-items-end gap-1">
                        <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-line-chart" title="Abrir KPIs do épico" @click="openKpiWorkspace(epicEntry.epic)" />
                        <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-plus" title="Nova demanda" @click="openCreateModal('Demand', epicEntry.epic.id, { projectId: pickDefaultProjectId(getItemProjectIds(epicEntry.epic)) })" />
                        <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" title="Editar épico" @click="openEditModal(epicEntry.epic)" />
                        <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" title="Excluir épico" @click="promptDelete(epicEntry.epic)" />
                      </div>
                    </td>
                  </tr>

                  <tr
                    v-for="demand in epicEntry.demands"
                    v-show="!isRoadmapCollapsed(group.roadmap.id) && !isEpicCollapsed(epicEntry.epic.id)"
                    :key="demand.id"
                    class="bg-default hover:bg-elevated/10 transition-colors"
                  >
                    <td class="border-b border-default px-3 py-2 align-top">
                      <div class="flex items-start gap-1.5 pl-20">
                        <UIcon name="i-lucide-list-todo" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-sky-600" :class="getCrossProjectWatermarkClass(demand)" />
                        <div class="min-w-0 flex-1" :class="getCrossProjectWatermarkClass(demand)">
                          <div class="flex flex-wrap items-center gap-1.5">
                            <span class="inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-muted">
                              Demanda
                            </span>
                            <span v-if="isOutsideSelectedProject(demand)" class="inline-flex items-center rounded-md border border-warning/40 bg-warning/10 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-warning">
                              Outro projeto
                            </span>
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
                          </div>
                          <p class="mt-0.5 truncate text-[13px] font-medium text-highlighted" :title="demand.description || undefined">{{ demand.title }}</p>
                        </div>
                      </div>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('status') }">
                      <span class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="statusTone[demand.status]">
                        {{ statusLabels[demand.status] }}
                      </span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('products') }">
                      <p v-if="getProductNames(demand).length" class="max-w-[160px] overflow-hidden text-[11px] leading-4 text-highlighted [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:2]" :title="getProductNamesLine(getProductNames(demand))">
                        {{ getProductNamesLine(getProductNames(demand)) }}
                      </p>
                      <span v-else class="text-xs text-muted">—</span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('hours') }">
                      <span v-if="getDisplayedHours(demand) !== null">{{ getDisplayedHours(demand) }}h</span>
                      <span v-else class="text-xs text-muted">—</span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('classification') }">
                      <span class="inline-flex items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium" :class="classificationBadgeClass[getDisplayedClassification(demand)]">
                        {{ classificationLabels[getDisplayedClassification(demand)] }}
                      </span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('customers') }">
                      <p v-if="getDisplayedCustomers(demand).length" class="max-w-[130px] overflow-hidden text-[11px] leading-4 text-highlighted [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:2]" :title="getCustomersLine(getDisplayedCustomers(demand))">
                        {{ getCustomersLine(getDisplayedCustomers(demand)) }}
                      </p>
                      <span v-else class="text-xs text-muted">—</span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('due') }">
                      <div class="flex flex-col gap-0.5">
                        <span v-if="hasPlannedQuarter(demand) && demand.quarterLabel" class="inline-flex max-w-fit items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[10px] font-medium text-highlighted">
                          {{ demand.quarterLabel }}
                        </span>
                        <span v-if="getDisplayedConclusionDate(demand)" class="text-[11px] text-muted">
                          {{ formatDate(getDisplayedConclusionDate(demand)) }}
                        </span>
                        <span v-else-if="demand.status === 'Backlog'" class="text-xs text-muted">
                          —
                        </span>
                      </div>
                    </td>

                    <td class="border-b border-default px-2 py-2 align-top" :style="{ width: getHierarchyColWidth('kpi') }">
                      <span class="text-xs text-muted">—</span>
                    </td>

                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('actions') }">
                      <div class="ml-auto grid w-full max-w-full grid-cols-4 justify-items-end gap-1">
                        <span class="h-6 w-6" />
                        <span class="h-6 w-6" />
                        <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" title="Editar demanda" @click="openEditModal(demand)" />
                        <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" title="Remover demanda" @click="promptDelete(demand)" />
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
                <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('item') }">
                  <div class="flex items-start gap-1.5">
                    <UIcon name="i-lucide-triangle-alert" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-rose-500" />
                    <div class="min-w-0 flex-1">
                      <span class="inline-flex items-center rounded-md border border-rose-200 bg-rose-50 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-rose-700 dark:border-rose-800 dark:bg-rose-900/20 dark:text-rose-300">
                        Épico órfão
                      </span>
                      <p class="mt-0.5 truncate text-[13px] font-medium text-highlighted" :title="epic.description || undefined">{{ epic.title }}</p>
                      <div class="mt-1 flex flex-wrap gap-1.5">
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
                    </div>
                  </div>
                </td>
                    <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('status') }">
                  <span class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="statusTone[epic.status]">
                    {{ statusLabels[epic.status] }}
                  </span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('products') }">
                  <p v-if="getEpicDisplayProductNames(epic).length" class="max-w-[160px] overflow-hidden text-[11px] leading-4 text-highlighted [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:2]" :title="getProductNamesLine(getEpicDisplayProductNames(epic))">
                    {{ getProductNamesLine(getEpicDisplayProductNames(epic)) }}
                  </p>
                  <span v-else class="text-xs text-muted">—</span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('hours') }">
                  <span v-if="getDisplayedHours(epic) !== null">{{ getDisplayedHours(epic) }}h</span>
                  <span v-else class="text-xs text-muted">—</span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('classification') }">
                  <span class="inline-flex items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium" :class="classificationBadgeClass[epic.classification]">
                    {{ classificationLabels[epic.classification] }}
                  </span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('customers') }">
                  <p v-if="getDisplayedCustomers(epic).length" class="max-w-[130px] overflow-hidden text-[11px] leading-4 text-highlighted [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:2]" :title="getCustomersLine(getDisplayedCustomers(epic))">
                    {{ getCustomersLine(getDisplayedCustomers(epic)) }}
                  </p>
                  <span v-else class="text-xs text-muted">—</span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('due') }">
                  {{ formatDate(getDisplayedConclusionDate(epic)) }}
                </td>
                <td class="border-b border-default px-2 py-2 align-top" :style="{ width: getHierarchyColWidth('kpi') }">
                  <div class="flex min-w-0 flex-col items-start gap-1">
                    <button type="button" class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium transition-colors hover:opacity-80" :class="getKpiSummary(epic).tone" :title="getKpiSummary(epic).actionLabel" @click="openKpiWorkspace(epic)">
                      {{ getKpiSummary(epic).label }}
                    </button>
                    <span v-if="getKpiSecondaryLabel(epic)" class="text-[11px] text-muted">
                      {{ getKpiSecondaryLabel(epic) }}
                    </span>
                  </div>
                </td>
                <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('actions') }">
                  <div class="ml-auto grid w-full max-w-full grid-cols-4 justify-items-end gap-1">
                    <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-line-chart" title="Abrir KPIs do épico" @click="openKpiWorkspace(epic)" />
                    <UButton size="xs" variant="ghost" color="primary" icon="i-lucide-plus" title="Nova demanda" @click="openCreateModal('Demand', epic.id, { projectId: pickDefaultProjectId(getItemProjectIds(epic)) })" />
                    <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" title="Editar épico" @click="openEditModal(epic)" />
                    <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" title="Excluir épico" @click="promptDelete(epic)" />
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
                <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('item') }">
                  <div class="flex items-start gap-1.5">
                    <UIcon name="i-lucide-link-2-off" class="mt-0.5 h-3.5 w-3.5 shrink-0 text-sky-600" />
                    <div class="min-w-0 flex-1">
                      <div class="flex flex-wrap items-center gap-1.5">
                        <span class="inline-flex items-center rounded-md border border-sky-200 bg-sky-50 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-sky-700 dark:border-sky-800 dark:bg-sky-900/20 dark:text-sky-300">
                          Demanda órfã
                        </span>
                        <span v-if="hasPlannedQuarter(demand) && demand.quarterLabel" class="inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[9px] font-medium text-muted">
                          {{ demand.quarterLabel }}
                        </span>
                        <span v-else class="text-xs text-muted">—</span>
                      </div>
                      <p class="mt-0.5 truncate text-[13px] font-medium text-highlighted" :title="demand.description || undefined">{{ demand.title }}</p>
                      <div class="mt-1 flex flex-wrap gap-1.5">
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
                    </div>
                  </div>
                </td>
                <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('status') }">
                  <span class="inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium" :class="statusTone[demand.status]">
                    {{ statusLabels[demand.status] }}
                  </span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('products') }">
                  <p v-if="getProductNames(demand).length" class="max-w-[160px] overflow-hidden text-[11px] leading-4 text-highlighted [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:2]" :title="getProductNamesLine(getProductNames(demand))">
                    {{ getProductNamesLine(getProductNames(demand)) }}
                  </p>
                  <span v-else class="text-xs text-muted">—</span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('hours') }">
                  <span v-if="getDisplayedHours(demand) !== null">{{ getDisplayedHours(demand) }}h</span>
                  <span v-else class="text-xs text-muted">—</span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('classification') }">
                  <span class="inline-flex items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium" :class="classificationBadgeClass[getDisplayedClassification(demand)]">
                    {{ classificationLabels[getDisplayedClassification(demand)] }}
                  </span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('customers') }">
                  <p v-if="getDisplayedCustomers(demand).length" class="max-w-[130px] overflow-hidden text-[11px] leading-4 text-highlighted [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:2]" :title="getCustomersLine(getDisplayedCustomers(demand))">
                    {{ getCustomersLine(getDisplayedCustomers(demand)) }}
                  </p>
                  <span v-else class="text-xs text-muted">—</span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top text-[11px] text-highlighted" :style="{ width: getHierarchyColWidth('due') }">
                  <div class="flex flex-col gap-0.5">
                    <span v-if="hasPlannedQuarter(demand) && demand.quarterLabel" class="inline-flex max-w-fit items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[10px] font-medium text-highlighted">
                      {{ demand.quarterLabel }}
                    </span>
                    <span v-if="getDisplayedConclusionDate(demand)" class="text-[11px] text-muted">
                      {{ formatDate(getDisplayedConclusionDate(demand)) }}
                    </span>
                    <span v-else-if="demand.status === 'Backlog'" class="text-xs text-muted">
                      —
                    </span>
                  </div>
                </td>
                <td class="border-b border-default px-2 py-2 align-top" :style="{ width: getHierarchyColWidth('kpi') }">
                  <span class="text-xs text-muted">—</span>
                </td>
                <td class="border-b border-default px-3 py-2 align-top" :style="{ width: getHierarchyColWidth('actions') }">
                  <div class="ml-auto grid w-full max-w-full grid-cols-4 justify-items-end gap-1">
                    <span class="h-6 w-6" />
                    <span class="h-6 w-6" />
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
      :default-parent-demand-id="defaultParentDemandId"
      :default-project-id="defaultProjectId ?? currentPrimaryProjectId ?? undefined"
      :default-project-ids="defaultProjectIds.length ? defaultProjectIds : selectedProjectIds"
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
      @submit="handleSubmit"
    />

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
