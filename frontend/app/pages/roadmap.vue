<script setup lang="ts">
import { h, nextTick, resolveComponent, useTemplateRef, onMounted, onUnmounted } from 'vue'
import Sortable from 'sortablejs'
import type { TableColumn } from '@nuxt/ui'
import type { SortingState, ColumnFiltersState, ColumnSizingState } from '@tanstack/vue-table'
import type * as XLSXType from 'xlsx'
import type { RoadmapDemand, DemandDependency, RoadmapCapacitySummary, DemandFormData, CapacityFormData, DemandStatus, DemandType, DemandClassification } from '~/types/roadmap'

useSeoMeta({ title: 'Roadmap · ProductHub' })

const roadmapStore = useRoadmapStore()
const toast = useToast()

const { projects, demands, dependencyOptions, capacitySummary, selectedProject, selectedProjectId, selectedQuarterYear, selectedQuarterNumber, isLoading, isCapacityLoading } = storeToRefs(roadmapStore)

// ─── View mode ───────────────────────────────────────────────────────────────
const viewMode = ref<'kanban' | 'list'>('list')

// ─── Status columns ───────────────────────────────────────────────────────────
const statusColumns = [
  { key: 'Backlog' as DemandStatus,       label: 'Backlog',       icon: 'i-lucide-circle-dashed' },
  { key: 'InProgress' as DemandStatus,    label: 'Em andamento',  icon: 'i-lucide-loader-circle' },
  { key: 'Done' as DemandStatus,          label: 'Concluído',     icon: 'i-lucide-circle-check-big' },
  { key: 'Deprioritized' as DemandStatus, label: 'Despriorizado', icon: 'i-lucide-circle-arrow-down' }
]

// ─── Quarter phase ────────────────────────────────────────────────────────────
const now = new Date()
const currentYear = now.getFullYear()
const currentQuarterNumber = Math.ceil((now.getMonth() + 1) / 3)
const backlogQuarterValue = '0-0'

type QuarterPhase = 'past' | 'current' | 'future'

function getQuarterPhase(year: number, quarter: number): QuarterPhase {
  if (year < currentYear || (year === currentYear && quarter < currentQuarterNumber)) return 'past'
  if (year === currentYear && quarter === currentQuarterNumber) return 'current'
  return 'future'
}

const quarterPhaseConfig: Record<QuarterPhase, { label: string, class: string }> = {
  past:    { label: 'Encerrado',    class: 'bg-elevated text-muted border-default' },
  current: { label: 'Em andamento', class: 'bg-blue-50 text-blue-600 border-blue-200 dark:bg-blue-900/20 dark:text-blue-400 dark:border-blue-800' },
  future:  { label: 'Futuro',       class: 'bg-green-50 text-green-600 border-green-200 dark:bg-green-900/20 dark:text-green-400 dark:border-green-800' }
}

const quarterOptions = computed(() =>
  [
    { value: backlogQuarterValue, label: 'Backlog — não priorizado' },
    ...[currentYear, currentYear + 1].flatMap(y =>
      [1, 2, 3, 4].map(q => ({
        value: `${q}-${y}`,
        label: `Q${q}/${String(y).slice(2)} — ${quarterPhaseConfig[getQuarterPhase(y, q)].label}`
      }))
    )
  ]
)

const planningQuarterOptions = computed(() =>
  quarterOptions.value.filter(option => option.value !== backlogQuarterValue)
)
const bulkMoveQuarterOptions = computed(() => quarterOptions.value)

const filterQuarters = ref<string[]>([])

function quarterShortLabel(val: string): string {
  const [q, y] = val.split('-').map(Number)
  if (q === 0 && y === 0) return 'Backlog'
  return `Q${q}/${String(y).slice(2)}`
}

const quarterFilterLabel = computed(() => {
  if (!filterQuarters.value.length) return 'Todos os quarters'
  if (filterQuarters.value.length === 1) return quarterShortLabel(filterQuarters.value[0]!)
  if (filterQuarters.value.length === 2) return filterQuarters.value.map(quarterShortLabel).join(', ')
  return `${filterQuarters.value.length} quarters`
})

function formatDemandCustomers(customers?: string[]): string {
  return customers?.join(', ') ?? ''
}

const customerSuggestions = computed(() => {
  const values = new Set<string>()

  for (const demand of demands.value) {
    for (const customer of demand.customers ?? []) {
      const normalized = customer.trim()
      if (normalized)
        values.add(normalized)
    }
  }

  return [...values].sort((left, right) => left.localeCompare(right, 'pt-BR'))
})

function getDemandNotesTooltip(demand: RoadmapDemand): string {
  const notes = []
  if (demand.isBlocked && demand.blockedReason)
    notes.push(`Impedimento\n${demand.blockedReason}`)
  if (demand.status === 'Deprioritized' && demand.observation)
    notes.push(`Despriorização\n${demand.observation}`)
  return notes.join('\n\n')
}

function formatDependencySummaryLine(dependency: DemandDependency) {
  return `${dependency.projectName} · ${dependency.title}`
}

function formatDependencyProjectLabel(dependency: DemandDependency) {
  return dependency.projectName.toUpperCase()
}

function compareQuarterPosition(left: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber'>, right: Pick<DemandDependency, 'quarterYear' | 'quarterNumber'>) {
  if (left.quarterYear !== right.quarterYear)
    return left.quarterYear - right.quarterYear

  return left.quarterNumber - right.quarterNumber
}

function isDependencyInconsistent(demand: RoadmapDemand, dependency: DemandDependency) {
  const isDependentDemandBacklog = demand.quarterYear === 0 && demand.quarterNumber === 0
  if (isDependentDemandBacklog)
    return false

  const isDependencyBacklog = dependency.quarterYear === 0 && dependency.quarterNumber === 0
  if (isDependencyBacklog)
    return true

  return compareQuarterPosition(demand, dependency) < 0
}

function getDependencyTooltip(prefix: 'É bloqueado por' | 'Bloqueia', dependency: DemandDependency) {
  return `${prefix} ${dependency.projectName}: ${dependency.title}`
}

function toggleQuarterFilter(val: string) {
  const idx = filterQuarters.value.indexOf(val)
  if (idx >= 0) filterQuarters.value.splice(idx, 1)
  else filterQuarters.value.push(val)
}

function isBacklogDemand(demand: RoadmapDemand): boolean {
  return demand.quarterYear === 0 && demand.quarterNumber === 0
}

function isAdditionalDemand(demand: Pick<RoadmapDemand, 'type'>): boolean {
  return demand.type === 'Additional'
}

function getDemandGroupOrder(demand: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'type'>): number {
  if (isBacklogDemand(demand as RoadmapDemand)) return 2
  if (isAdditionalDemand(demand)) return 1
  return 0
}

function getDemandGroupKey(demand: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'type'>): 'regular' | 'additional' | 'backlog' {
  if (isBacklogDemand(demand as RoadmapDemand)) return 'backlog'
  if (isAdditionalDemand(demand)) return 'additional'
  return 'regular'
}

function compareListDemandGroups(left: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'type'>, right: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'type'>) {
  return getDemandGroupOrder(left) - getDemandGroupOrder(right)
}

function withListGroupSorting(compareWithinGroup: (left: RoadmapDemand, right: RoadmapDemand) => number) {
  return (rowA: { original: RoadmapDemand }, rowB: { original: RoadmapDemand }) => {
    const groupComparison = compareListDemandGroups(rowA.original, rowB.original)
    if (groupComparison !== 0)
      return groupComparison

    return compareWithinGroup(rowA.original, rowB.original)
  }
}

// ─── Filters ──────────────────────────────────────────────────────────────────
const filterText = ref('')
const filterProducts = ref<string[]>([])
const filterCustomer = ref('')
const filterTypes = ref<DemandType[]>([])
const filterClassifications = ref<DemandClassification[]>([])
const showKanbanFilters = ref(false)

const demandTypes: DemandType[] = ['Planned', 'Spillover', 'Unplanned', 'Additional']
const demandClassifications: DemandClassification[] = [
  'TechnicalDebtSecurity', 'Strategic', 'Evolution', 'ImprovementGap', 'Mandatory', 'Homologation'
]

const selectedProjectProducts = computed(() =>
  projects.value.find(p => p.id === selectedProjectId.value)?.products ?? []
)

const hasActiveFilters = computed(() =>
  !!(filterText.value || filterProducts.value.length || filterCustomer.value || filterTypes.value.length || filterClassifications.value.length)
)
const activeFilterCount = computed(() =>
  [filterText.value, filterCustomer.value].filter(Boolean).length
  + filterProducts.value.length + filterTypes.value.length + filterClassifications.value.length
)

const activeCapacityScope = computed(() => {
  if (!selectedProjectId.value || filterQuarters.value.length !== 1) return null

  const [quarterNumber, quarterYear] = filterQuarters.value[0]!.split('-').map(Number)
  if (quarterNumber === 0 && quarterYear === 0) return null

  return {
    projectId: selectedProjectId.value,
    quarterYear,
    quarterNumber,
    quarterLabel: quarterShortLabel(filterQuarters.value[0]!)
  }
})

const selectedDemandScope = computed(() => {
  if (filterQuarters.value.length !== 1) return null

  const [quarterNumber, quarterYear] = filterQuarters.value[0]!.split('-').map(Number)

  return {
    quarterYear,
    quarterNumber
  }
})

const quarterScopedDemands = computed(() => {
  if (!activeCapacityScope.value) return []

  return demands.value.filter(demand =>
    demand.projectId === activeCapacityScope.value!.projectId
    && demand.quarterYear === activeCapacityScope.value!.quarterYear
    && demand.quarterNumber === activeCapacityScope.value!.quarterNumber
  )
})

const displayCapacitySummary = computed<RoadmapCapacitySummary | null>(() => {
  if (!activeCapacityScope.value) return null

  const committedHours = quarterScopedDemands.value
    .filter(demand => demand.type !== 'Additional')
    .reduce((total, demand) => total + (demand.hours ?? 0), 0)

  const additionalHours = quarterScopedDemands.value
    .filter(demand => demand.type === 'Additional')
    .reduce((total, demand) => total + (demand.hours ?? 0), 0)

  const configuredCapacity = capacitySummary.value?.capacityHours
  const remainingHours = typeof configuredCapacity === 'number'
    ? Math.max(configuredCapacity - committedHours, 0)
    : undefined
  const overCapacityHours = typeof configuredCapacity === 'number'
    ? Math.max(committedHours - configuredCapacity, 0)
    : undefined

  return {
    id: capacitySummary.value?.id,
    projectId: activeCapacityScope.value.projectId,
    quarterLabel: activeCapacityScope.value.quarterLabel,
    quarterYear: activeCapacityScope.value.quarterYear,
    quarterNumber: activeCapacityScope.value.quarterNumber,
    capacityHours: configuredCapacity,
    observation: capacitySummary.value?.observation,
    committedHours,
    additionalHours,
    totalDemandHours: committedHours + additionalHours,
    remainingHours,
    overCapacityHours
  }
})

const capacityConfigured = computed(() => typeof displayCapacitySummary.value?.capacityHours === 'number')

const capacityProgressPercent = computed(() => {
  if (!displayCapacitySummary.value?.capacityHours) return 0
  return (displayCapacitySummary.value.committedHours / displayCapacitySummary.value.capacityHours) * 100
})

const capacityProgressBarPercent = computed(() => {
  if (!capacityConfigured.value) return 0
  return Math.min(capacityProgressPercent.value, 100)
})

const capacityIsOver = computed(() => (displayCapacitySummary.value?.overCapacityHours ?? 0) > 0)

const capacityProgressTone = computed(() => {
  if (capacityIsOver.value) return 'bg-red-500'
  return 'bg-indigo-500'
})

const capacityDeltaLabel = computed(() => {
  if (!displayCapacitySummary.value?.capacityHours) return 'Capacity não configurado'
  if (capacityIsOver.value) return 'Excedente'
  return 'Disponível'
})

const capacityDeltaValue = computed(() => {
  if (!displayCapacitySummary.value?.capacityHours) return null
  return capacityIsOver.value
    ? displayCapacitySummary.value.overCapacityHours ?? 0
    : displayCapacitySummary.value.remainingHours ?? 0
})

const capacityDeltaTone = computed(() => {
  if (!capacityConfigured.value) {
    return 'border-default bg-default text-muted'
  }

  return capacityIsOver.value
    ? 'border-red-200 bg-red-50 text-red-600 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300'
    : 'border-emerald-200 bg-emerald-50 text-emerald-600 dark:border-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-300'
})

const capacityPercentTone = computed(() => {
  if (!capacityConfigured.value) return 'text-muted'
  return capacityIsOver.value ? 'text-red-600 dark:text-red-300' : 'text-indigo-600 dark:text-indigo-300'
})

const capacityCommittedTone = computed(() => {
  if (!capacityConfigured.value) return 'text-highlighted'
  return capacityIsOver.value ? 'text-red-600 dark:text-red-300' : 'text-highlighted'
})

const scopedDemandTotalHours = computed(() =>
  quarterFilteredDemands.value.reduce((total, demand) => total + (demand.hours ?? 0), 0)
)

const classificationTotals = computed(() => {
  const totalHours = scopedDemandTotalHours.value

  const totals = new Map<DemandClassification, { classification: DemandClassification, label: string, hours: number, count: number }>()

  for (const demand of quarterFilteredDemands.value) {
    const classification = String(demand.classification ?? '').trim() as DemandClassification
    if (!classification || !(classification in classificationLabels)) continue

    const current = totals.get(classification) ?? {
      classification,
      label: classificationLabels[classification],
      hours: 0,
      count: 0
    }

    current.hours += demand.hours ?? 0
    current.count += 1
    totals.set(classification, current)
  }

  return [...totals.values()]
    .map(item => ({
      ...item,
      percentage: totalHours > 0 ? (item.hours / totalHours) * 100 : 0
    }))
    .sort((left, right) => right.hours - left.hours)
})

const customerTotals = computed(() => {
  const totalHours = scopedDemandTotalHours.value
  const totals = new Map<string, { name: string, hours: number, count: number }>()

  for (const demand of quarterFilteredDemands.value) {
    const customers = demand.customers?.filter(Boolean) ?? []
    for (const customer of customers) {
      const current = totals.get(customer) ?? { name: customer, hours: 0, count: 0 }
      current.hours += demand.hours ?? 0
      current.count += 1
      totals.set(customer, current)
    }
  }

  return [...totals.values()]
    .map(item => ({
      ...item,
      percentage: totalHours > 0 ? (item.hours / totalHours) * 100 : 0
    }))
    .sort((left, right) => right.hours - left.hours)
})

const typeTotals = computed(() => {
  const totalHours = scopedDemandTotalHours.value

  return demandTypes.map(type => {
    const scopedDemands = quarterFilteredDemands.value.filter(demand => demand.type === type)
    const hours = scopedDemands.reduce((total, demand) => total + (demand.hours ?? 0), 0)

    return {
      type,
      label: typeLabels[type],
      hours,
      count: scopedDemands.length,
      percentage: totalHours > 0 ? (hours / totalHours) * 100 : 0
    }
  })
    .filter(item => item.count > 0)
    .sort((left, right) => right.hours - left.hours)
})

const typeSummaryTone: Record<DemandType, string> = {
  Planned: 'bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-900/20 dark:text-emerald-300 dark:border-emerald-800',
  Spillover: 'bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-900/20 dark:text-amber-300 dark:border-amber-800',
  Unplanned: 'bg-rose-50 text-rose-700 border-rose-200 dark:bg-rose-900/20 dark:text-rose-300 dark:border-rose-800',
  Additional: 'bg-blue-50 text-blue-700 border-blue-200 dark:bg-blue-900/20 dark:text-blue-300 dark:border-blue-800'
}

const typeSummaryDot: Record<DemandType, string> = {
  Planned: 'bg-emerald-500',
  Spillover: 'bg-amber-500',
  Unplanned: 'bg-rose-500',
  Additional: 'bg-blue-500'
}

function toggleProductFilter(id: string) {
  const idx = filterProducts.value.indexOf(id)
  if (idx >= 0) filterProducts.value.splice(idx, 1)
  else filterProducts.value.push(id)
}

function toggleTypeFilter(type: DemandType) {
  const idx = filterTypes.value.indexOf(type)
  if (idx >= 0) filterTypes.value.splice(idx, 1)
  else filterTypes.value.push(type)
}

function toggleClassificationFilter(cls: DemandClassification) {
  const idx = filterClassifications.value.indexOf(cls)
  if (idx >= 0) filterClassifications.value.splice(idx, 1)
  else filterClassifications.value.push(cls)
}

function clearFilters() {
  filterText.value = ''
  filterProducts.value = []
  filterCustomer.value = ''
  filterTypes.value = []
  filterClassifications.value = []
}

watch(activeCapacityScope, async (scope) => {
  if (!scope) {
    roadmapStore.clearCapacity()
    return
  }

  roadmapStore.clearCapacity()
  await roadmapStore.fetchCapacity(scope.projectId, scope.quarterYear, scope.quarterNumber)
}, { immediate: true })

watch(selectedProjectId, () => {
  filterProducts.value = []
  setListMultiFilter('status', [])
  setListMultiFilter('type', [])
  setListMultiFilter('classification', [])
  setListMultiFilter('products', [])
})

const quarterFilteredDemands = computed(() => {
  const orderedDemands = [...demands.value].sort((left, right) => {
    const groupComparison = compareListDemandGroups(left, right)
    if (groupComparison !== 0)
      return groupComparison

    if (left.quarterYear !== right.quarterYear)
      return left.quarterYear - right.quarterYear

    if (left.quarterNumber !== right.quarterNumber)
      return left.quarterNumber - right.quarterNumber

    return left.sortOrder - right.sortOrder
  })
  if (!filterQuarters.value.length) return orderedDemands
  return orderedDemands.filter(d =>
    filterQuarters.value.includes(`${d.quarterNumber}-${d.quarterYear}`)
  )
})

const filteredDemands = computed(() => {
  let list = quarterFilteredDemands.value
  if (filterText.value) {
    const q = filterText.value.toLowerCase()
    list = list.filter(d =>
      d.title.toLowerCase().includes(q) || (d.description?.toLowerCase().includes(q) ?? false)
    )
  }
  if (filterProducts.value.length)
    list = list.filter(d => filterProducts.value.some(pid => d.products.some(p => p.productId === pid)))
  if (filterCustomer.value) {
    const q = filterCustomer.value.toLowerCase()
    list = list.filter(d => d.customers?.some(customer => customer.toLowerCase().includes(q)))
  }
  if (filterTypes.value.length)
    list = list.filter(d => filterTypes.value.includes(d.type))
  if (filterClassifications.value.length)
    list = list.filter(d => filterClassifications.value.includes(d.classification))
  return list
})

function getDemandsForStatus(status: DemandStatus) {
  return filteredDemands.value.filter(d => d.status === status)
}

// ─── Drag and Drop ────────────────────────────────────────────────────────────
const draggingId = ref<string | null>(null)
const kanbanListRefs = reactive<Record<DemandStatus, HTMLElement | null>>({
  Backlog: null,
  InProgress: null,
  Done: null,
  Deprioritized: null
})
const kanbanSortables: Partial<Record<DemandStatus, Sortable>> = {}
const listScrollContainerRef = ref<HTMLElement | null>(null)
const listTableRootRef = ref<HTMLElement | null>(null)
let listBodySortable: Sortable | null = null

function setKanbanListRef(status: DemandStatus, element: Element | null) {
  kanbanListRefs[status] = element as HTMLElement | null
}

function moveDemandId(
  ids: string[],
  movedId: string,
  beforeId: string | null,
  afterId: string | null
) {
  const nextIds = ids.filter(id => id !== movedId)

  if (beforeId) {
    const targetIndex = nextIds.indexOf(beforeId)
    if (targetIndex >= 0) {
      nextIds.splice(targetIndex, 0, movedId)
      return nextIds
    }
  }

  if (afterId) {
    const targetIndex = nextIds.indexOf(afterId)
    if (targetIndex >= 0) {
      nextIds.splice(targetIndex + 1, 0, movedId)
      return nextIds
    }
  }

  nextIds.push(movedId)
  return nextIds
}

function isSameDemandScope(left: Pick<RoadmapDemand, 'projectId' | 'quarterYear' | 'quarterNumber'>, right: Pick<RoadmapDemand, 'projectId' | 'quarterYear' | 'quarterNumber'>) {
  return left.projectId === right.projectId
    && left.quarterYear === right.quarterYear
    && left.quarterNumber === right.quarterNumber
}

function isSameDemandGroup(
  left: Pick<RoadmapDemand, 'projectId' | 'quarterYear' | 'quarterNumber' | 'type'>,
  right: Pick<RoadmapDemand, 'projectId' | 'quarterYear' | 'quarterNumber' | 'type'>
) {
  return isSameDemandScope(left, right)
    && getDemandGroupKey(left) === getDemandGroupKey(right)
}

function getScopedDemandIds(demand: RoadmapDemand) {
  return demands.value
    .filter(item => isSameDemandGroup(item, demand))
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map(item => item.id)
}

function getDemandScopeKey(demand: Pick<RoadmapDemand, 'projectId' | 'quarterYear' | 'quarterNumber' | 'type'>) {
  return `${demand.projectId}:${demand.quarterYear}:${demand.quarterNumber}:${getDemandGroupKey(demand)}`
}

function ensureDemandCanMoveToStatus(demand: RoadmapDemand, status: DemandStatus) {
  if (status === 'Done' && !demand.deliveryDate) {
    toast.add({ title: 'Informe a data de entrega antes de concluir', color: 'warning' })
    openEditModal(demand, status)
    return false
  }

  if (status === 'Deprioritized' && !demand.observation) {
    toast.add({ title: 'Informe o motivo da despriorização', color: 'warning' })
    openEditModal(demand, status)
    return false
  }

  return true
}

async function persistDemandPriority(
  demand: RoadmapDemand,
  status: DemandStatus,
  beforeId: string | null,
  afterId: string | null
) {
  if (!ensureDemandCanMoveToStatus(demand, status)) {
    await roadmapStore.fetchDemands()
    return
  }

  const scopedDemandIds = getScopedDemandIds(demand)
  const scopedDemandIdSet = new Set(scopedDemandIds)
  const orderedDemandIds = moveDemandId(
    scopedDemandIds,
    demand.id,
    beforeId && scopedDemandIdSet.has(beforeId) ? beforeId : null,
    afterId && scopedDemandIdSet.has(afterId) ? afterId : null
  )

  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null

  try {
    await roadmapStore.reorderDemand(demand.id, status, orderedDemandIds)
    await nextTick()

    if (listScrollContainerRef.value && listScrollTop != null) {
      listScrollContainerRef.value.scrollTop = listScrollTop
      listScrollContainerRef.value.scrollLeft = listScrollLeft ?? 0
    }
  }
  catch {
    // handled by useApi
  }
}

function getKanbanFallbackAnchors(
  status: DemandStatus,
  idsByStatus: Record<DemandStatus, string[]>
) {
  const statusIndex = statusColumns.findIndex(column => column.key === status)

  for (let index = statusIndex - 1; index >= 0; index--) {
    const previousIds = idsByStatus[statusColumns[index]!.key]
    if (previousIds.length)
      return { beforeId: null, afterId: previousIds[previousIds.length - 1] ?? null }
  }

  for (let index = statusIndex + 1; index < statusColumns.length; index++) {
    const nextIds = idsByStatus[statusColumns[index]!.key]
    if (nextIds.length)
      return { beforeId: nextIds[0] ?? null, afterId: null }
  }

  return { beforeId: null, afterId: null }
}

async function handleKanbanSortEnd(event: Sortable.SortableEvent, targetStatus: DemandStatus) {
  const movedId = (event.item as HTMLElement | null)?.dataset.demandId
  draggingId.value = null

  if (!movedId || event.newIndex == null) {
    await roadmapStore.fetchDemands()
    return
  }

  const demand = quarterFilteredDemands.value.find(item => item.id === movedId)
  if (!demand) {
    await roadmapStore.fetchDemands()
    return
  }

  const idsByStatus = Object.fromEntries(
    statusColumns.map(column => [column.key, getDemandsForStatus(column.key).map(item => item.id)])
  ) as Record<DemandStatus, string[]>

  for (const status of Object.keys(idsByStatus) as DemandStatus[])
    idsByStatus[status] = idsByStatus[status].filter(id => id !== movedId)

  idsByStatus[targetStatus].splice(event.newIndex, 0, movedId)

  const beforeId = idsByStatus[targetStatus][event.newIndex + 1] ?? null
  const afterId = idsByStatus[targetStatus][event.newIndex - 1] ?? null
  const fallbackAnchors = !beforeId && !afterId
    ? getKanbanFallbackAnchors(targetStatus, idsByStatus)
    : { beforeId: null, afterId: null }

  await persistDemandPriority(
    demand,
    targetStatus,
    beforeId ?? fallbackAnchors.beforeId,
    afterId ?? fallbackAnchors.afterId
  )
}

async function handleListSortEnd(oldIndex: number, newIndex: number) {
  const visibleRows = listTableRef.value?.tableApi?.getSortedRowModel().rows.map(row => row.original) ?? quarterFilteredDemands.value
  const demand = visibleRows[oldIndex]
  if (!demand) {
    await roadmapStore.fetchDemands()
    return
  }

  const remainingRows = visibleRows.filter((_, index) => index !== oldIndex)
  const nextScopedRow = remainingRows.slice(newIndex).find(item => isSameDemandGroup(item, demand))
  const previousScopedRow = [...remainingRows.slice(0, newIndex)].reverse().find(item => isSameDemandGroup(item, demand))
  const beforeId = nextScopedRow?.id ?? null
  const afterId = beforeId ? null : previousScopedRow?.id ?? null

  listSorting.value = []
  await persistDemandPriority(demand, demand.status, beforeId, afterId)
}

function destroyKanbanSortables() {
  for (const status of Object.keys(kanbanSortables) as DemandStatus[]) {
    kanbanSortables[status]?.destroy()
    delete kanbanSortables[status]
  }
}

function destroyListSortable() {
  listBodySortable?.destroy()
  listBodySortable = null
}

function initKanbanSortables() {
  destroyKanbanSortables()
  if (viewMode.value !== 'kanban') return

  for (const column of statusColumns) {
    const element = kanbanListRefs[column.key]
    if (!element) continue

    kanbanSortables[column.key] = Sortable.create(element, {
      group: 'roadmap-kanban-priority',
      animation: 150,
      draggable: '.kanban-demand-item',
      dataIdAttr: 'data-demand-id',
      ghostClass: 'opacity-40',
      onStart: (event) => {
        draggingId.value = (event.item as HTMLElement | null)?.dataset.demandId ?? null
      },
      onEnd: (event) => handleKanbanSortEnd(event, column.key)
    })
  }
}

function initListSortable() {
  destroyListSortable()
  if (viewMode.value !== 'list') return

  const tbody = listTableRootRef.value?.querySelector('tbody')
  if (!tbody) return

  syncListSectionDividers()

  listBodySortable = Sortable.create(tbody, {
    animation: 150,
    draggable: '.list-demand-row',
    handle: '.list-priority-handle',
    ghostClass: 'opacity-40',
    forceFallback: true,
    fallbackOnBody: true,
    fallbackTolerance: 4,
    filter: 'a,input,textarea,[role="button"]',
    preventOnFilter: false,
    onMove: (event) => {
      const draggedScope = (event.dragged as HTMLElement | null)?.dataset.scopeKey
      const related = event.related as HTMLElement | null
      const relatedScope = related?.dataset.scopeKey

      if (!draggedScope)
        return false

      if (!related)
        return true

      if (!relatedScope)
        return false

      return draggedScope === relatedScope
    },
    onEnd: (event) => {
      const oldIndex = event.oldDraggableIndex ?? event.oldIndex
      const newIndex = event.newDraggableIndex ?? event.newIndex

      if (oldIndex == null || newIndex == null || oldIndex === newIndex)
        return

      handleListSortEnd(oldIndex, newIndex)
    }
  })
}

function syncListSectionDividers() {
  const tbody = listTableRootRef.value?.querySelector('tbody')
  if (!tbody) return

  tbody.querySelectorAll('.list-section-divider').forEach(node => node.remove())

  const rows = Array.from(tbody.querySelectorAll('tr')) as HTMLTableRowElement[]
  const visibleRows = listTableRef.value?.tableApi?.getSortedRowModel().rows.map(row => row.original) ?? quarterFilteredDemands.value

  rows.forEach((row, index) => {
    const demand = visibleRows[index]
    if (!demand) {
      row.classList.remove('list-demand-row')
      delete row.dataset.demandId
      delete row.dataset.scopeKey
      return
    }

    row.classList.add('list-demand-row')
    row.dataset.demandId = demand.id
    row.dataset.scopeKey = getDemandScopeKey(demand)
  })

  const dividerConfigs = [
    {
      demand: visibleRows.find(demand => isAdditionalDemand(demand)),
      shouldRender: visibleRows.some(demand => !isAdditionalDemand(demand) && !isBacklogDemand(demand)),
      label: 'Adicionais - Não comprometidas'
    },
    {
      demand: visibleRows.find(demand => isBacklogDemand(demand)),
      shouldRender: visibleRows.some(demand => !isBacklogDemand(demand)),
      label: 'Backlog'
    }
  ]

  dividerConfigs.forEach(({ demand, shouldRender, label }) => {
    if (!demand || !shouldRender) return

    const targetRow = rows.find(row => row.dataset.demandId === demand.id)
    if (!targetRow) return

    const dividerRow = document.createElement('tr')
    dividerRow.className = 'list-section-divider'

    const dividerCell = document.createElement('td')
    dividerCell.colSpan = listOrderedCols.value.length
    dividerCell.className = 'border-y-2 border-default bg-gray-200 px-3 py-2 text-center text-xs font-extrabold uppercase tracking-[0.2em] text-gray-900 dark:bg-gray-700/80 dark:text-gray-50'
    dividerCell.textContent = label

    dividerRow.appendChild(dividerCell)
    tbody.insertBefore(dividerRow, targetRow)
  })
}

watch(
  () => `${viewMode.value}|${quarterFilteredDemands.value.map(demand => `${demand.id}:${demand.quarterYear}:${demand.quarterNumber}:${demand.status}:${demand.sortOrder}`).join('|')}|${JSON.stringify(listSorting.value)}|${JSON.stringify(listColumnFilters.value)}`,
  async () => {
    await nextTick()
    initKanbanSortables()
    syncListSectionDividers()
    initListSortable()
  },
  { flush: 'post' }
)

// ─── Modal ────────────────────────────────────────────────────────────────────
const modalOpen = ref(false)
const capacityModalOpen = ref(false)
const isSavingCapacity = ref(false)
const editingDemand = ref<RoadmapDemand | null>(null)
const deleteId = ref<string | null>(null)
const confirmDeleteOpen = ref(false)

function openCreateModal() {
  editingDemand.value = null
  modalOpen.value = true
}

function openCapacityModal() {
  if (!activeCapacityScope.value) return
  capacityModalOpen.value = true
}

function openEditModal(demand: RoadmapDemand, nextStatus?: DemandStatus) {
  editingDemand.value = nextStatus ? { ...demand, status: nextStatus } : demand
  modalOpen.value = true
}

function promptDelete(id: string) {
  deleteId.value = id
  confirmDeleteOpen.value = true
}

function buildDemandFormData(demand: RoadmapDemand, overrides?: Partial<DemandFormData>): DemandFormData {
  return {
    title: demand.title,
    description: demand.description ?? '',
    projectId: demand.projectId,
    quarterYear: demand.quarterYear,
    quarterNumber: demand.quarterNumber,
    type: demand.type,
    classification: demand.classification,
    productIds: demand.products.map(product => product.productId),
    status: demand.status,
    observation: demand.observation ?? '',
    jiraIssue: demand.jiraIssue ?? '',
    hours: demand.hours,
    customers: demand.customers ?? [],
    dependencyDemandIds: demand.dependsOn.map(item => item.demandId),
    isBlocked: demand.isBlocked,
    blockedReason: demand.blockedReason ?? '',
    deliveryDate: demand.deliveryDate ?? '',
    ...overrides
  }
}

async function planDemandToQuarter(demand: RoadmapDemand, quarterValue: string) {
  const [quarterNumber, quarterYear] = quarterValue.split('-').map(Number)
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null

  try {
    await roadmapStore.updateDemand(
      demand.id,
      buildDemandFormData(demand, {
        quarterYear,
        quarterNumber,
        status: 'Backlog'
      })
    )

    await refreshListPresentation(listScrollTop, listScrollLeft)

    toast.add({
      title: 'Demanda planejada no quarter',
      description: `${demand.title} movida para ${quarterShortLabel(quarterValue)}`,
      color: 'success'
    })
  }
  catch {
    // error handled by useApi
  }
}

async function handleSubmit(data: DemandFormData) {
  try {
    if (editingDemand.value) {
      await roadmapStore.updateDemand(editingDemand.value.id, data)
      toast.add({ title: 'Demanda atualizada', color: 'success' })
    }
    else {
      await roadmapStore.createDemand(data)
      toast.add({ title: 'Demanda criada', color: 'success' })
    }
    modalOpen.value = false
  }
  catch {
    // error handled by useApi
  }
}

async function handleCapacitySubmit(data: CapacityFormData) {
  try {
    isSavingCapacity.value = true
    await roadmapStore.upsertCapacity(data)
    toast.add({ title: 'Capacity atualizada', color: 'success' })
    capacityModalOpen.value = false
  }
  catch {
    // error handled by useApi
  }
  finally {
    isSavingCapacity.value = false
  }
}

const capacityModalInitialValue = computed<CapacityFormData | null>(() => {
  if (!activeCapacityScope.value) return null

  return {
    projectId: activeCapacityScope.value.projectId,
    quarterYear: activeCapacityScope.value.quarterYear,
    quarterNumber: activeCapacityScope.value.quarterNumber,
    capacityHours: capacitySummary.value?.capacityHours ?? 0,
    observation: capacitySummary.value?.observation ?? ''
  }
})

async function confirmDelete() {
  if (!deleteId.value) return
  try {
    await roadmapStore.deleteDemand(deleteId.value)
    toast.add({ title: 'Demanda removida', color: 'success' })
  }
  catch {
    // error handled by useApi
  }
  finally {
    confirmDeleteOpen.value = false
    deleteId.value = null
  }
}

// ─── List view labels ──────────────────────────────────────────────────────────
const statusLabels: Record<DemandStatus, string> = {
  Backlog: 'Backlog', InProgress: 'Em andamento', Done: 'Concluído', Deprioritized: 'Despriorizado'
}
const statusTextClass: Record<DemandStatus, string> = {
  Backlog: 'text-muted',
  InProgress: 'text-blue-600 dark:text-blue-400',
  Done: 'text-green-600 dark:text-green-400',
  Deprioritized: 'text-amber-600 dark:text-amber-400'
}
const statusDotClass: Record<DemandStatus, string> = {
  Backlog: 'bg-neutral-400 dark:bg-neutral-500',
  InProgress: 'bg-blue-500 dark:bg-blue-400',
  Done: 'bg-green-500 dark:bg-green-400',
  Deprioritized: 'bg-amber-500 dark:bg-amber-400'
}
const typeLabels: Record<DemandType, string> = {
  Planned: 'Planejado', Spillover: 'Transbordo', Unplanned: 'Não Planejado', Additional: 'Adicional'
}
const classificationLabels: Record<DemandClassification, string> = {
  TechnicalDebtSecurity: 'Débito Técnico', Strategic: 'Estratégico', Evolution: 'Evolução',
  ImprovementGap: 'Melhoria/Gap', Mandatory: 'Mandatório', Homologation: 'Homologação'
}

// ─── List view — TanStack table ──────────────────────────────────────────────────────────────────
const listSorting = ref<SortingState>([])
const listColumnFilters = ref<ColumnFiltersState>([])
const listColumnSizing = ref<ColumnSizingState>({})
const listColumnOrder = ref<string[]>([])

const listTableRef = useTemplateRef<{
  tableApi: {
    getFilteredRowModel: () => { rows: { original: RoadmapDemand }[] }
    getSortedRowModel:   () => { rows: { original: RoadmapDemand }[] }
    setColumnOrder:      (updater: string[] | ((old: string[]) => string[])) => void
    getAllLeafColumns:   () => { id: string }[]
  }
}>('listTable')

const listFilteredCount = computed(() => {
  void listColumnFilters.value
  return listTableRef.value?.tableApi?.getFilteredRowModel().rows.length ?? quarterFilteredDemands.value.length
})
const visibleListRows = computed(() => {
  void listSorting.value
  void listColumnFilters.value
  return listTableRef.value?.tableApi?.getSortedRowModel().rows.map(row => row.original) ?? quarterFilteredDemands.value
})
const visibleListDemandCount = computed(() => {
  return visibleListRows.value.length
})
const listHasActiveFilters = computed(() => listColumnFilters.value.length > 0)
const shouldConstrainListHeight = computed(() => visibleListDemandCount.value > 20)
const listTableKey = computed(() =>
  quarterFilteredDemands.value
    .map(demand => `${demand.id}:${demand.quarterYear}:${demand.quarterNumber}:${demand.status}:${demand.sortOrder}`)
    .join('|')
)
const priorityRankByDemandId = computed(() =>
  Object.fromEntries(
    quarterFilteredDemands.value.map((demand, index) => [demand.id, index + 1])
  ) as Record<string, number>
)
const selectedDemandIds = ref<string[]>([])
const isBulkPlanning = ref(false)
const visibleListDemandIds = computed(() => visibleListRows.value.map(demand => demand.id))
const selectedDemands = computed(() => {
  const selectedIds = new Set(selectedDemandIds.value)
  return demands.value.filter(demand => selectedIds.has(demand.id))
})
const selectedDemandCount = computed(() => selectedDemands.value.length)

const LIST_COL_MIN = 60

interface ListColMeta {
  id: string
  label: string
  defaultWidth: number
  filterType?: 'text' | 'select' | 'multi-select'
  selectOptions?: { label: string; value: string }[]
  allLabel?: string
  itemLabelPlural?: string
  alignRight?: boolean
  disableFilter?: boolean
  disableSorting?: boolean
}

const STATUS_SELECT_OPTIONS = [
  { label: 'Backlog',       value: 'Backlog' },
  { label: 'Em andamento',  value: 'InProgress' },
  { label: 'Concluído',     value: 'Done' },
  { label: 'Despriorizado', value: 'Deprioritized' },
]
const TYPE_SELECT_OPTIONS = [
  { label: 'Planejado',     value: 'Planned' },
  { label: 'Transbordo',    value: 'Spillover' },
  { label: 'Não Planejado', value: 'Unplanned' },
  { label: 'Adicional',     value: 'Additional' },
]
const CLASSIFICATION_SELECT_OPTIONS = [
  { label: 'Débito Técnico', value: 'TechnicalDebtSecurity' },
  { label: 'Estratégico',    value: 'Strategic' },
  { label: 'Evolução',       value: 'Evolution' },
  { label: 'Melhoria/Gap',   value: 'ImprovementGap' },
  { label: 'Mandatório',     value: 'Mandatory' },
  { label: 'Homologação',    value: 'Homologation' },
]

const classificationBadgeClass: Record<DemandClassification, string> = {
  TechnicalDebtSecurity: 'bg-slate-100 text-slate-700 border-slate-200 dark:bg-slate-800/60 dark:text-slate-300 dark:border-slate-700',
  Strategic: 'bg-indigo-100 text-indigo-700 border-indigo-200 dark:bg-indigo-900/30 dark:text-indigo-300 dark:border-indigo-800',
  Evolution: 'bg-sky-100 text-sky-700 border-sky-200 dark:bg-sky-900/30 dark:text-sky-300 dark:border-sky-800',
  ImprovementGap: 'bg-emerald-100 text-emerald-700 border-emerald-200 dark:bg-emerald-900/30 dark:text-emerald-300 dark:border-emerald-800',
  Mandatory: 'bg-red-100 text-red-700 border-red-200 dark:bg-red-900/30 dark:text-red-300 dark:border-red-800',
  Homologation: 'bg-violet-100 text-violet-700 border-violet-200 dark:bg-violet-900/30 dark:text-violet-300 dark:border-violet-800',
}

const LIST_COL_DEFS: ListColMeta[] = [
  { id: 'select',         label: '',             defaultWidth: 42, disableFilter: true, disableSorting: true },
  { id: 'priority',       label: 'Prioridade',   defaultWidth: 92, disableFilter: true },
  { id: 'quarterLabel',   label: 'Quarter / Tipo', defaultWidth: 126, filterType: 'multi-select', allLabel: 'Todos os quarters', itemLabelPlural: 'quarters' },
  { id: 'classification', label: 'Classificação', defaultWidth: 148, filterType: 'multi-select', selectOptions: CLASSIFICATION_SELECT_OPTIONS, allLabel: 'Todas as classificações', itemLabelPlural: 'classificações' },
  { id: 'title',          label: 'Demanda',       defaultWidth: 360, filterType: 'text' },
  { id: 'products',       label: 'Produtos',      defaultWidth: 136, filterType: 'multi-select', allLabel: 'Todos os produtos', itemLabelPlural: 'produtos', disableSorting: true },
  { id: 'hours',          label: 'Horas',         defaultWidth: 68,  disableFilter: true, alignRight: true },
  { id: 'status',         label: 'Status',        defaultWidth: 140, filterType: 'multi-select', selectOptions: STATUS_SELECT_OPTIONS, allLabel: 'Todos os status', itemLabelPlural: 'status' },
  { id: 'customers',      label: 'Clientes',      defaultWidth: 150, filterType: 'text' },
  { id: '_actions',       label: '',              defaultWidth: 80,  disableFilter: true, disableSorting: true, alignRight: true },
]

listColumnOrder.value = LIST_COL_DEFS.map(column => column.id)

const listOrderedCols = ref<ListColMeta[]>([...LIST_COL_DEFS])
const listHeaderRowRef = ref<HTMLElement | null>(null)

onMounted(() => {
  if (listHeaderRowRef.value) {
    Sortable.create(listHeaderRowRef.value, {
      animation: 150,
      handle: '.list-col-drag',
      onEnd(evt) {
        const api = listTableRef.value?.tableApi
        if (!api) return
        const newCols = [...listOrderedCols.value]
        const [moved] = newCols.splice(evt.oldIndex!, 1)
        newCols.splice(evt.newIndex!, 0, moved!)
        listOrderedCols.value = newCols
        const newOrder = newCols.map(c => c.id)
        listColumnOrder.value = newOrder
        api.setColumnOrder(newOrder)
      },
    })
  }
})

function getListColFilter(colId: string): string {
  return (listColumnFilters.value.find(f => f.id === colId)?.value as string) ?? ''
}
function setListColFilter(colId: string, value: string) {
  const others = listColumnFilters.value.filter(f => f.id !== colId)
  listColumnFilters.value = value ? [...others, { id: colId, value }] : others
}
function getListMultiFilter(colId: string): string[] {
  return (listColumnFilters.value.find(f => f.id === colId)?.value as string[]) ?? []
}
function setListMultiFilter(colId: string, values: string[]) {
  const others = listColumnFilters.value.filter(f => f.id !== colId)
  listColumnFilters.value = values.length ? [...others, { id: colId, value: values }] : others
}
function toggleListMultiFilterValue(colId: string, value: string) {
  const current = getListMultiFilter(colId)
  const next = current.includes(value)
    ? current.filter(item => item !== value)
    : [...current, value]
  setListMultiFilter(colId, next)
}
function getListProductsFilterLabel(): string {
  const selected = getListMultiFilter('products')
  if (!selected.length) return 'Todos'
  if (selected.length === 1) {
    return selectedProjectProducts.value.find(product => product.id === selected[0])?.name ?? '1 produto'
  }
  return `${selected.length} produtos`
}
function getListMultiFilterLabel(col: ListColMeta): string {
  const selected = getListMultiFilter(col.id)
  if (!selected.length) return col.allLabel ?? 'Todos'
  if (col.id === 'products') return getListProductsFilterLabel()
  if (col.id === 'quarterLabel') {
    if (selected.length === 1) return quarterShortLabel(selected[0]!)
    if (selected.length === 2) return selected.map(quarterShortLabel).join(', ')
    return `${selected.length} quarters`
  }
  if (selected.length === 1) {
    return col.selectOptions?.find(option => option.value === selected[0])?.label ?? '1 selecionado'
  }
  return `${selected.length} ${col.itemLabelPlural ?? 'selecionados'}`
}
function clearListFilters() {
  listColumnFilters.value = []
}
function sanitizeSelectedDemands() {
  const availableIds = new Set(demands.value.map(demand => demand.id))
  selectedDemandIds.value = selectedDemandIds.value.filter(id => availableIds.has(id))
}
function isDemandSelected(demandId: string) {
  return selectedDemandIds.value.includes(demandId)
}
function toggleDemandSelection(demandId: string, selected: boolean) {
  if (selected) {
    if (!selectedDemandIds.value.includes(demandId))
      selectedDemandIds.value = [...selectedDemandIds.value, demandId]

    return
  }

  selectedDemandIds.value = selectedDemandIds.value.filter(id => id !== demandId)
}
async function refreshListPresentation(scrollTop?: number | null, scrollLeft?: number | null) {
  await nextTick()
  syncListSectionDividers()
  initListSortable()
  await nextTick()
  syncListSectionDividers()

  if (listScrollContainerRef.value && scrollTop != null) {
    listScrollContainerRef.value.scrollTop = scrollTop
    listScrollContainerRef.value.scrollLeft = scrollLeft ?? 0
  }
}

async function planSelectedDemandsToQuarter(quarterValue: string) {
  if (!selectedDemands.value.length || isBulkPlanning.value)
    return

  const [quarterNumber, quarterYear] = quarterValue.split('-').map(Number)
  const movedCount = selectedDemands.value.length
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
  isBulkPlanning.value = true

  try {
    for (const demand of selectedDemands.value) {
      await roadmapStore.updateDemand(
        demand.id,
        buildDemandFormData(demand, {
          quarterYear,
          quarterNumber,
          status: demand.status
        })
      )
    }

    await roadmapStore.fetchDemands()
    await refreshListPresentation(listScrollTop, listScrollLeft)

    selectedDemandIds.value = []
    toast.add({
      title: 'Demandas planejadas no quarter',
      description: `${movedCount.toLocaleString('pt-BR')} demandas movidas para ${quarterShortLabel(quarterValue)}`,
      color: 'success'
    })
  }
  catch {
    // error handled by useApi
  }
  finally {
    isBulkPlanning.value = false
    sanitizeSelectedDemands()
  }
}

watch(
  () => demands.value.map(demand => `${demand.id}:${demand.quarterYear}:${demand.quarterNumber}`).join('|'),
  sanitizeSelectedDemands
)

function toggleListSort(colId: string) {
  const active = listSorting.value.find(s => s.id === colId)
  if (!active) listSorting.value = [{ id: colId, desc: false }]
  else if (!active.desc) listSorting.value = [{ id: colId, desc: true }]
  else listSorting.value = []
}
function startListResize(colId: string, e: MouseEvent) {
  e.preventDefault()
  e.stopPropagation()
  const col = LIST_COL_DEFS.find(c => c.id === colId)
  const startX = e.clientX
  const startWidth = listColumnSizing.value[colId] ?? col?.defaultWidth ?? 100
  const onMove = (ev: MouseEvent) => {
    listColumnSizing.value = { ...listColumnSizing.value, [colId]: Math.max(LIST_COL_MIN, startWidth + (ev.clientX - startX)) }
  }
  const onUp = () => { document.removeEventListener('mousemove', onMove); document.removeEventListener('mouseup', onUp) }
  document.addEventListener('mousemove', onMove)
  document.addEventListener('mouseup', onUp)
}
function listColWidth(colId: string, fallback: number): string {
  return `${listColumnSizing.value[colId] ?? fallback}px`
}

// Pre-resolve components for use inside cell h() renderers
const UButtonComp = resolveComponent('UButton')
const UIconComp   = resolveComponent('UIcon')
const UPopoverComp = resolveComponent('UPopover')

function renderDependencyBadge(dependency: DemandDependency) {
  return h('span', {
    class: 'inline-flex max-w-full items-center gap-1 rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-[11px] font-medium text-amber-700 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300',
    title: formatDependencySummaryLine(dependency)
  }, [
    h(UIconComp, { name: 'i-lucide-link', class: 'h-3 w-3 shrink-0' }),
    h('span', { class: 'min-w-0 max-w-[9rem] truncate uppercase tracking-[0.04em]' }, formatDependencyProjectLabel(dependency))
  ])
}

function renderDependsOnBadge(demand: RoadmapDemand, dependency: DemandDependency) {
  const inconsistent = isDependencyInconsistent(demand, dependency)

  return h('span', {
    class: inconsistent
      ? 'inline-flex max-w-full items-center gap-1 rounded-full border border-red-200 bg-red-50 px-2 py-0.5 text-[11px] font-medium text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300'
      : 'inline-flex max-w-full items-center gap-1 rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-[11px] font-medium text-amber-700 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300',
    title: `${getDependencyTooltip('É bloqueado por', dependency)}${inconsistent ? `\n\nInconsistência: a demanda vinculada está em ${dependency.quarterLabel}, depois de ${demand.quarterLabel}, ou sem priorização.` : ''}`
  }, [
    h(UIconComp, { name: 'i-lucide-link', class: 'h-3 w-3 shrink-0' }),
    h('span', { class: 'min-w-0 max-w-[9rem] truncate uppercase tracking-[0.04em]' }, formatDependencyProjectLabel(dependency)),
    ...(inconsistent ? [h(UIconComp, { name: 'i-lucide-triangle-alert', class: 'h-3 w-3 shrink-0' })] : [])
  ])
}

const listTanstackColumns: TableColumn<RoadmapDemand>[] = [
  {
    id: 'select',
    header: '',
    enableSorting: false,
    enableColumnFilter: false,
    size: 42,
    meta: { style: { td: () => ({ width: listColWidth('select', 42) }), th: () => ({ width: listColWidth('select', 42) }) } },
    cell: ({ row }) => {
      const demand = row.original
      return h('label', { class: 'flex items-center justify-center' }, [
        h('input', {
          type: 'checkbox',
          class: 'h-4 w-4 rounded border-default text-primary focus:ring-primary',
          checked: isDemandSelected(demand.id),
          onClick: (event: Event) => event.stopPropagation(),
          onChange: (event: Event) => toggleDemandSelection(demand.id, (event.target as HTMLInputElement).checked)
        })
      ])
    }
  },
  {
    id: 'priority',
    header: 'Prioridade',
    accessorFn: row => row.sortOrder,
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => left.sortOrder - right.sortOrder),
    enableColumnFilter: false,
    size: 92,
    meta: { style: { td: () => ({ width: listColWidth('priority', 92) }), th: () => ({ width: listColWidth('priority', 92) }) } },
    cell: ({ row }) => {
      const priority = priorityRankByDemandId.value[row.original.id] ?? 0
      return h('div', { class: 'flex items-center gap-2' }, [
        h('span', {
          class: 'list-priority-handle inline-flex h-7 w-7 items-center justify-center rounded-md border border-default bg-elevated text-muted transition-colors hover:border-primary/40 hover:text-highlighted cursor-grab active:cursor-grabbing',
          title: 'Arrastar para repriorizar'
        }, [h(UIconComp, { name: 'i-lucide-grip-vertical', class: 'h-4 w-4' })]),
        h('div', { class: 'flex flex-col min-w-0' }, [
          h('span', { class: 'text-xs font-semibold text-highlighted' }, `#${priority}`),
          h('span', { class: 'text-[11px] text-muted' }, 'Arraste')
        ])
      ])
    }
  },
  {
    accessorKey: 'title',
    header: 'Demanda',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => left.title.localeCompare(right.title, 'pt-BR')),
    enableColumnFilter: true,
    filterFn: (row, _colId, filterValue: string) => {
      const d = row.original
      const q = filterValue.toLowerCase()
      return d.title.toLowerCase().includes(q)
        || (d.description?.toLowerCase().includes(q) ?? false)
        || (d.jiraIssue?.toLowerCase().includes(q) ?? false)
    },
    size: 360,
    meta: { style: { td: () => ({ width: listColWidth('title', 360) }), th: () => ({ width: listColWidth('title', 360) }) } },
    cell: ({ row }) => {
      const d = row.original
      const textNodes = [h('p', { class: 'font-medium text-highlighted truncate', title: d.description || undefined }, d.title)]
      if (d.jiraIssue) textNodes.push(h('p', { class: 'text-xs text-blue-500 font-mono' }, d.jiraIssue))
      if (d.dependsOn.length) {
        textNodes.push(
          h('div', { class: 'mt-1 flex flex-wrap gap-1' }, [
              ...d.dependsOn.slice(0, 2).map(dependency => renderDependsOnBadge(d, dependency)),
              ...(d.dependsOn.length > 2
                ? [h('span', { class: 'text-[11px] text-muted' }, `+${d.dependsOn.length - 2}`)]
                : [])
          ])
        )
      }
      if (d.dependedOnBy.length) {
        textNodes.push(
          h('div', { class: 'mt-1 flex flex-wrap gap-1' }, [
              ...d.dependedOnBy.slice(0, 2).map(dependency => h('span', {
                ...renderDependencyBadge(dependency).props,
                title: getDependencyTooltip('Bloqueia', dependency)
              }, renderDependencyBadge(dependency).children)),
              ...(d.dependedOnBy.length > 2
                ? [h('span', { class: 'text-[11px] text-muted' }, `+${d.dependedOnBy.length - 2}`)]
                : [])
          ])
        )
      }
      return h('div', { class: 'min-w-0' }, textNodes)
    },
  },
  {
    accessorKey: 'quarterLabel',
    header: 'Quarter / Tipo',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => {
      if (left.quarterYear !== right.quarterYear)
        return left.quarterYear - right.quarterYear

      if (left.quarterNumber !== right.quarterNumber)
        return left.quarterNumber - right.quarterNumber

      return left.type.localeCompare(right.type, 'pt-BR')
    }),
    enableColumnFilter: true,
    filterFn: (row, _colId, filterValue: string[]) => {
      if (!Array.isArray(filterValue) || !filterValue.length) return true
      return filterValue.includes(`${row.original.quarterNumber}-${row.original.quarterYear}`)
    },
    size: 126,
    meta: { style: { td: () => ({ width: listColWidth('quarterLabel', 126) }), th: () => ({ width: listColWidth('quarterLabel', 126) }) } },
    cell: ({ row }) => {
      const demand = row.original
      const quarterNode = demand.quarterYear === 0 && demand.quarterNumber === 0
        ? h('span', {
          class: 'text-[11px] font-bold uppercase tracking-[0.08em] text-black dark:text-white'
        }, 'Backlog')
        : h('span', { class: 'text-xs font-mono text-highlighted' }, demand.quarterLabel)

      return h('div', { class: 'flex min-w-0 flex-col gap-0.5' }, [
        quarterNode,
        h('span', { class: 'text-[11px] text-muted' }, typeLabels[demand.type])
      ])
    },
  },
  {
    accessorKey: 'status',
    header: 'Status',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => statusLabels[left.status].localeCompare(statusLabels[right.status], 'pt-BR')),
    enableColumnFilter: true,
    filterFn: (row, _colId, filterValue: string[]) => {
      if (!Array.isArray(filterValue) || !filterValue.length) return true
      return filterValue.includes(row.original.status)
    },
    size: 140,
    meta: { style: { td: () => ({ width: listColWidth('status', 140) }), th: () => ({ width: listColWidth('status', 140) }) } },
  },
  {
    accessorKey: 'classification',
    header: 'Classificação',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => classificationLabels[left.classification].localeCompare(classificationLabels[right.classification], 'pt-BR')),
    enableColumnFilter: true,
    filterFn: (row, _colId, filterValue: string[]) => {
      if (!Array.isArray(filterValue) || !filterValue.length) return true
      return filterValue.includes(row.original.classification)
    },
    size: 148,
    meta: { style: { td: () => ({ width: listColWidth('classification', 148) }), th: () => ({ width: listColWidth('classification', 148) }) } },
    cell: ({ row }) => h('span', { class: `inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${classificationBadgeClass[row.original.classification]}` }, classificationLabels[row.original.classification]),
  },
  {
    accessorKey: 'products',
    header: 'Produtos',
    enableSorting: false,
    enableColumnFilter: true,
    filterFn: (row, _colId, filterValue: string[]) => {
      if (!Array.isArray(filterValue) || !filterValue.length) return true
      return filterValue.some(productId => row.original.products.some(product => product.productId === productId))
    },
    size: 136,
    meta: { style: { td: () => ({ width: listColWidth('products', 136) }), th: () => ({ width: listColWidth('products', 136) }) } },
    cell: ({ row }) => {
      const prods = row.original.products
      if (!prods.length) return h('span', { class: 'text-xs text-muted' }, '—')
      return h('div', { class: 'flex flex-col gap-0.5' },
        prods.map(p => h('span', { class: 'text-xs text-muted' }, p.name))
      )
    },
  },
  {
    accessorKey: 'customers',
    header: 'Clientes',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => formatDemandCustomers(left.customers).localeCompare(formatDemandCustomers(right.customers), 'pt-BR')),
    enableColumnFilter: true,
    filterFn: (row, _colId, filterValue: string) => {
      const query = filterValue.toLowerCase()
      return !query || row.original.customers?.some(customer => customer.toLowerCase().includes(query)) || false
    },
    size: 140,
    meta: { style: { td: () => ({ width: listColWidth('customers', 150) }), th: () => ({ width: listColWidth('customers', 150) }) } },
    cell: ({ row }) => {
      const customers = row.original.customers
      if (!customers?.length) return h('span', { class: 'text-xs text-muted' }, '—')
      return h('div', { class: 'flex flex-wrap gap-1 max-w-[170px]' },
        customers.map(customer => h('span', { class: 'rounded-full border border-default bg-elevated px-2 py-0.5 text-xs text-muted' }, customer))
      )
    },
  },
  {
    accessorKey: 'hours',
    header: 'Horas',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => (left.hours ?? 0) - (right.hours ?? 0)),
    enableColumnFilter: false,
    size: 68,
    meta: { class: { td: 'text-right' }, style: { td: () => ({ width: listColWidth('hours', 68) }), th: () => ({ width: listColWidth('hours', 68) }) } },
    cell: ({ row }) => h('span', { class: 'text-xs text-muted' }, row.original.hours ? `${row.original.hours}h` : '—'),
  },
  {
    id: '_actions',
    header: '',
    enableSorting: false,
    enableColumnFilter: false,
    size: 112,
    meta: { class: { td: 'text-right' }, style: { td: () => ({ width: listColWidth('_actions', 112) }), th: () => ({ width: listColWidth('_actions', 112) }) } },
    cell: ({ row }) => {
      const demand = row.original
      const actions = []

      if (isBacklogDemand(demand)) {
        actions.push(
          h(UPopoverComp, {}, {
            default: () => h(UButtonComp, {
              size: 'xs',
              variant: 'ghost',
              color: 'primary'
            }, {
              default: () => h(UIconComp, { name: 'i-lucide-calendar-range', class: 'h-4 w-4' })
            }),
            content: () => h('div', { class: 'py-1 min-w-[200px]' }, planningQuarterOptions.value.map(option =>
              h('button', {
                class: 'w-full px-3 py-2 text-left text-sm text-highlighted transition-colors hover:bg-elevated',
                onClick: () => planDemandToQuarter(demand, option.value)
              }, option.label)
            ))
          })
        )
      }

      actions.push(
        h(UButtonComp, { size: 'xs', variant: 'ghost', color: 'neutral', onClick: () => openEditModal(demand) }, {
          default: () => h(UIconComp, { name: 'i-lucide-pencil', class: 'h-4 w-4' })
        })
      )

      actions.push(
        h(UButtonComp, { icon: 'i-lucide-trash-2', size: 'xs', variant: 'ghost', color: 'error', onClick: () => promptDelete(demand.id) })
      )

      return h('div', { class: 'flex items-center justify-end gap-1' }, actions)
    },
  },
]

// XLSX export for list view
let xlsxModule: typeof XLSXType | null = null
onMounted(() => { import('xlsx').then(m => { xlsxModule = m }) })

const listExportMenuOpen  = ref(false)
const listExportUrlVisible = ref('')
const listExportUrlFull    = ref('')

function buildListBlobUrl(rows: RoadmapDemand[]): string {
  const XLSX = xlsxModule
  if (!XLSX) return ''
  const cols = listOrderedCols.value.filter(c => c.id !== '_actions')
  const header = cols.map(c => c.label)
  const data = rows.map(row => cols.map(c => {
    if (c.id === 'priority') return priorityRankByDemandId.value[row.id] ? `#${priorityRankByDemandId.value[row.id]}` : ''
    if (c.id === 'status') return statusLabels[row.status]
    if (c.id === 'type') return typeLabels[row.type]
    if (c.id === 'classification') return classificationLabels[row.classification]
    if (c.id === 'products') return row.products.map(p => p.name).join(', ')
    if (c.id === 'customers') return formatDemandCustomers(row.customers)
    if (c.id === 'hours') return row.hours ? `${row.hours}h` : ''
    return (row as unknown as Record<string, unknown>)[c.id] ?? ''
  }))
  const ws = XLSX.utils.aoa_to_sheet([header, ...data])
  ws['!cols'] = cols.map((c, i) => ({ wch: Math.min(Math.max(c.label.length, ...data.map(r => String(r[i] ?? '').length)) + 2, 60) }))
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Roadmap')
  const buffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' }) as ArrayBuffer
  const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
  return URL.createObjectURL(blob)
}

function toggleListExportMenu() {
  if (listExportMenuOpen.value) { listExportMenuOpen.value = false; return }
  if (listExportUrlVisible.value) URL.revokeObjectURL(listExportUrlVisible.value)
  if (listExportUrlFull.value) URL.revokeObjectURL(listExportUrlFull.value)
  const api = listTableRef.value?.tableApi
  const visibleRows = api ? api.getSortedRowModel().rows.map(r => r.original) : demands.value
  listExportUrlVisible.value = buildListBlobUrl(visibleRows)
  listExportUrlFull.value    = buildListBlobUrl(demands.value)
  listExportMenuOpen.value   = true
}
function closeListExportMenu() { listExportMenuOpen.value = false }

onUnmounted(() => {
  destroyKanbanSortables()
  destroyListSortable()
  if (listExportUrlVisible.value) URL.revokeObjectURL(listExportUrlVisible.value)
  if (listExportUrlFull.value) URL.revokeObjectURL(listExportUrlFull.value)
})

// Default quarter filter to current quarter (client-side filtering)
filterQuarters.value = [`${currentQuarterNumber}-${currentYear}`]

// Load data
await Promise.all([
  roadmapStore.fetchProjects(),
  roadmapStore.fetchDemands(),
  roadmapStore.fetchDependencyOptions()
])
</script>

<template>
  <div class="space-y-4">
    <!-- Header -->
    <div class="flex items-center justify-between flex-wrap gap-2">
      <div>
        <h1 class="text-lg font-semibold text-highlighted tracking-tight">
          Roadmap
        </h1>
        <p class="text-xs text-muted mt-0.5">
          Planejamento por projeto, quarter e status
        </p>
      </div>
    </div>

    <!-- Project tabs + quarter filter -->
    <div class="flex items-center gap-2 flex-wrap">
      <div class="flex items-center gap-2 flex-wrap">
        <button
          v-for="project in projects"
          :key="project.id"
          class="px-4 py-1.5 rounded-full text-sm font-medium transition-all border"
          :class="selectedProjectId === project.id
            ? 'bg-primary text-white border-primary shadow-sm'
            : 'border-default text-muted hover:border-primary/40 hover:text-highlighted'"
          @click="roadmapStore.selectProject(project.id)"
        >
          {{ project.name }}
        </button>
      </div>
      <div class="flex items-center gap-2 flex-wrap ml-auto">
        <UPopover>
          <button class="flex items-center gap-1.5 text-sm border border-default rounded-lg px-3 py-1.5 bg-background hover:border-primary/40 transition-colors min-w-[220px]">
            <UIcon name="i-lucide-calendar" class="w-3.5 h-3.5 shrink-0 text-muted" />
            <span class="flex-1 text-left truncate text-highlighted">{{ quarterFilterLabel }}</span>
            <UBadge v-if="filterQuarters.length" size="xs" color="primary" variant="solid" class="shrink-0">{{ filterQuarters.length }}</UBadge>
            <UIcon name="i-lucide-chevron-down" class="w-3.5 h-3.5 shrink-0 text-muted" />
          </button>
          <template #content>
            <div class="py-1 min-w-[260px] max-h-72 overflow-y-auto">
              <button
                class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                :class="filterQuarters.length === 0 ? 'text-primary font-medium' : 'text-highlighted'"
                @click="filterQuarters = []"
              >
                <UIcon v-if="filterQuarters.length === 0" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0" />
                <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                Todos os quarters
              </button>
              <button
                v-for="opt in quarterOptions"
                :key="opt.value"
                class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                :class="filterQuarters.includes(opt.value) ? 'text-primary' : 'text-highlighted'"
                @click="toggleQuarterFilter(opt.value)"
              >
                <UIcon v-if="filterQuarters.includes(opt.value)" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0 text-primary" />
                <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                {{ opt.label }}
              </button>
            </div>
          </template>
        </UPopover>

        <div class="flex items-center border border-default rounded-lg overflow-hidden">
          <button
            class="px-3 py-1.5 text-sm flex items-center gap-1.5 transition-colors"
            :class="viewMode === 'kanban' ? 'bg-primary text-white' : 'text-muted hover:text-highlighted'"
            @click="viewMode = 'kanban'"
          >
            <UIcon name="i-lucide-layout-dashboard" class="w-4 h-4" />
            Kanban
          </button>
          <button
            class="px-3 py-1.5 text-sm flex items-center gap-1.5 border-l border-default transition-colors"
            :class="viewMode === 'list' ? 'bg-primary text-white' : 'text-muted hover:text-highlighted'"
            @click="viewMode = 'list'"
          >
            <UIcon name="i-lucide-list" class="w-4 h-4" />
            Lista
          </button>
        </div>

        <UButton
          icon="i-lucide-plus"
          label="Nova Demanda"
          @click="openCreateModal()"
        />
      </div>
    </div>

    <div class="rounded-[20px] border border-default bg-default px-3 py-2 shadow-sm">
      <div class="grid gap-x-3 gap-y-2 xl:grid-cols-[150px_minmax(0,1fr)_86px_112px_auto] xl:items-start">
        <div class="min-w-0">
          <p class="text-[10px] font-semibold uppercase tracking-[0.08em] text-primary/70">Capacity</p>
          <div class="mt-0.5 flex items-center gap-2">
            <span class="text-base font-semibold leading-none text-highlighted">{{ selectedProject?.name ?? 'Projeto' }}</span>
            <span class="rounded-full border border-default bg-elevated px-2 py-0.5 text-[11px] font-medium text-muted">
              {{ activeCapacityScope?.quarterLabel ?? 'Selecione 1 quarter' }}
            </span>
          </div>
        </div>

        <div class="min-w-0">
          <div class="flex flex-wrap items-start gap-x-2.5 gap-y-1">
            <div>
              <p class="text-[10px] font-semibold uppercase tracking-[0.08em] text-primary/70">Comprometido</p>
              <div class="mt-0.5 flex flex-wrap items-center gap-1.5">
                <span class="text-[1.05rem] font-semibold leading-none" :class="capacityCommittedTone">
                  {{ displayCapacitySummary?.committedHours.toLocaleString('pt-BR') ?? '0' }}h
                </span>
                <span class="text-base leading-none text-muted">/ {{ displayCapacitySummary?.capacityHours?.toLocaleString('pt-BR') ?? '—' }}h</span>
                <span
                  v-if="capacityConfigured"
                  class="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] font-semibold"
                  :class="capacityDeltaTone"
                >
                  <UIcon :name="capacityIsOver ? 'i-lucide-circle-alert' : 'i-lucide-circle-check'" class="h-3 w-3" />
                  {{ capacityDeltaLabel }}: {{ capacityDeltaValue?.toLocaleString('pt-BR') ?? '—' }}h
                </span>
              </div>
            </div>
          </div>

          <div class="mt-2 h-2 overflow-hidden rounded-full bg-elevated">
            <div class="h-full rounded-full transition-all duration-300" :class="capacityProgressTone" :style="{ width: `${capacityProgressBarPercent}%` }" />
          </div>
        </div>

        <div class="text-left xl:text-right">
          <p class="text-[10px] font-semibold uppercase tracking-[0.08em] text-primary/70">Capacity</p>
          <p class="mt-0.5 text-lg font-semibold leading-none" :class="capacityPercentTone">
            {{ capacityConfigured ? `${capacityProgressPercent.toFixed(0)}%` : '—' }}
          </p>
        </div>

        <div>
          <p class="text-[10px] font-semibold uppercase tracking-[0.08em] text-primary/70">Adicionais</p>
          <div class="mt-0.5 flex items-center gap-1 text-base font-semibold leading-none text-highlighted">
            <UIcon name="i-lucide-bolt" class="h-4 w-4 shrink-0 text-amber-500" />
            {{ displayCapacitySummary?.additionalHours.toLocaleString('pt-BR') ?? '0' }}h
          </div>
        </div>

        <div class="flex items-start justify-start gap-2 xl:justify-end">
          <UPopover v-if="displayCapacitySummary?.observation">
            <button
              type="button"
              class="inline-flex items-center justify-center rounded-full border border-default bg-default p-1.5 text-muted transition-colors hover:border-primary/40 hover:text-highlighted"
              title="Observação do capacity"
            >
              <UIcon name="i-lucide-message-square-more" class="h-4 w-4" />
            </button>
            <template #content>
              <div class="max-w-[320px] p-3">
                <p class="text-xs font-semibold uppercase tracking-[0.12em] text-muted">Observação do capacity</p>
                <p class="mt-2 text-sm leading-6 text-highlighted">{{ displayCapacitySummary.observation }}</p>
              </div>
            </template>
          </UPopover>

          <UButton
            :disabled="!activeCapacityScope"
            color="neutral"
            variant="ghost"
            size="sm"
            :icon="capacityConfigured ? 'i-lucide-pencil' : 'i-lucide-plus'"
            :label="capacityConfigured ? 'Editar' : 'Configurar'"
            @click="openCapacityModal"
          />
        </div>
      </div>
    </div>

    <div
      v-if="viewMode === 'kanban'"
      class="space-y-2"
    >
      <div class="flex items-center justify-between gap-2 rounded-lg border border-default bg-elevated/30 px-3 py-2">
        <div class="flex items-center gap-2 text-sm text-muted">
          <UIcon name="i-lucide-sliders-horizontal" class="h-4 w-4" />
          <span class="text-highlighted">Filtros do Kanban</span>
          <UBadge v-if="hasActiveFilters" size="xs" color="primary" variant="solid">{{ activeFilterCount }}</UBadge>
        </div>
        <UButton
          size="xs"
          variant="ghost"
          color="neutral"
          :icon="showKanbanFilters ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'"
          :label="showKanbanFilters ? 'Ocultar filtros' : 'Mostrar filtros'"
          @click="showKanbanFilters = !showKanbanFilters"
        />
      </div>

      <!-- Filter bar (Kanban only) -->
      <div
        v-if="showKanbanFilters"
        class="bg-elevated border border-default rounded-xl p-3 space-y-2.5"
      >
      <!-- Text search + Customer -->
      <div class="flex gap-2 flex-wrap">
        <UInput
          v-model="filterText"
          placeholder="Buscar por título ou descrição..."
          icon="i-lucide-search"
          class="flex-1 min-w-[200px]"
        />
        <UInput
          v-model="filterCustomer"
          placeholder="Filtrar por cliente..."
          icon="i-lucide-users"
          class="w-52"
        />
      </div>
      <div class="flex flex-wrap items-center gap-x-3 gap-y-2">
        <!-- Type multi-select -->
        <div class="flex items-center gap-1.5 min-w-0">
          <span class="text-xs text-muted shrink-0 w-14">Tipo:</span>
          <UPopover>
            <button class="flex items-center gap-1.5 text-xs border border-default rounded-lg px-2.5 py-1.5 bg-background hover:border-primary/40 transition-colors min-w-[160px] max-w-[220px]">
              <span class="flex-1 text-left truncate text-highlighted">
                {{ filterTypes.length === 0 ? 'Todos os tipos' : filterTypes.length === 1 ? typeLabels[filterTypes[0]] : `${filterTypes.length} tipos selecionados` }}
              </span>
              <UIcon name="i-lucide-chevron-down" class="w-3.5 h-3.5 shrink-0 text-muted" />
            </button>
            <template #content>
              <div class="py-1 min-w-[180px]">
                <button
                  class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                  :class="filterTypes.length === 0 ? 'text-primary font-medium' : 'text-highlighted'"
                  @click="filterTypes = []"
                >
                  <UIcon v-if="filterTypes.length === 0" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0" />
                  <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                  Todos os tipos
                </button>
                <button
                  v-for="t in demandTypes"
                  :key="t"
                  class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                  :class="filterTypes.includes(t) ? 'text-primary' : 'text-highlighted'"
                  @click="toggleTypeFilter(t)"
                >
                  <UIcon v-if="filterTypes.includes(t)" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0 text-primary" />
                  <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                  {{ typeLabels[t] }}
                </button>
              </div>
            </template>
          </UPopover>
        </div>
        <!-- Classification multi-select -->
        <div class="flex items-center gap-1.5 min-w-0">
          <span class="text-xs text-muted shrink-0 w-14">Class.:</span>
          <UPopover>
            <button class="flex items-center gap-1.5 text-xs border border-default rounded-lg px-2.5 py-1.5 bg-background hover:border-primary/40 transition-colors min-w-[180px] max-w-[240px]">
              <span class="flex-1 text-left truncate text-highlighted">
                {{ filterClassifications.length === 0 ? 'Todas as classificações' : filterClassifications.length === 1 ? classificationLabels[filterClassifications[0]] : `${filterClassifications.length} classificações` }}
              </span>
              <UIcon name="i-lucide-chevron-down" class="w-3.5 h-3.5 shrink-0 text-muted" />
            </button>
            <template #content>
              <div class="py-1 min-w-[200px]">
                <button
                  class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                  :class="filterClassifications.length === 0 ? 'text-primary font-medium' : 'text-highlighted'"
                  @click="filterClassifications = []"
                >
                  <UIcon v-if="filterClassifications.length === 0" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0" />
                  <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                  Todas as classificações
                </button>
                <button
                  v-for="c in demandClassifications"
                  :key="c"
                  class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                  :class="filterClassifications.includes(c) ? 'text-primary' : 'text-highlighted'"
                  @click="toggleClassificationFilter(c)"
                >
                  <UIcon v-if="filterClassifications.includes(c)" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0 text-primary" />
                  <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                  {{ classificationLabels[c] }}
                </button>
              </div>
            </template>
          </UPopover>
        </div>
        <!-- Product multi-select -->
        <div
          v-if="selectedProjectProducts.length"
          class="flex items-center gap-1.5 min-w-0"
        >
          <span class="text-xs text-muted shrink-0 w-14">Produtos:</span>
          <UPopover>
            <button class="flex items-center gap-1.5 text-xs border border-default rounded-lg px-2.5 py-1.5 bg-background hover:border-primary/40 transition-colors min-w-[180px] max-w-[240px]">
              <span class="flex-1 text-left truncate text-highlighted">
                {{ filterProducts.length === 0 ? 'Todos os produtos' : filterProducts.length === 1 ? selectedProjectProducts.find(product => product.id === filterProducts[0])?.name ?? '1 produto' : `${filterProducts.length} produtos` }}
              </span>
              <UIcon name="i-lucide-chevron-down" class="w-3.5 h-3.5 shrink-0 text-muted" />
            </button>
            <template #content>
              <div class="py-1 min-w-[220px] max-h-72 overflow-y-auto">
                <button
                  class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                  :class="filterProducts.length === 0 ? 'text-primary font-medium' : 'text-highlighted'"
                  @click="filterProducts = []"
                >
                  <UIcon v-if="filterProducts.length === 0" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0" />
                  <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                  Todos os produtos
                </button>
                <button
                  v-for="product in selectedProjectProducts"
                  :key="product.id"
                  class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                  :class="filterProducts.includes(product.id) ? 'text-primary' : 'text-highlighted'"
                  @click="toggleProductFilter(product.id)"
                >
                  <UIcon v-if="filterProducts.includes(product.id)" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0 text-primary" />
                  <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                  {{ product.name }}
                </button>
              </div>
            </template>
          </UPopover>
        </div>
        <div v-if="hasActiveFilters" class="flex items-center min-w-0 sm:ml-auto">
          <UButton
            variant="ghost"
            color="neutral"
            size="xs"
            icon="i-lucide-x"
            :label="`Limpar filtros (${activeFilterCount})`"
            @click="clearFilters"
          />
        </div>
      </div>
    </div>
    </div>

    <!-- Loading -->
    <div
      v-if="isLoading"
      class="flex items-center justify-center py-16"
    >
      <UIcon
        name="i-lucide-loader-circle"
        class="w-6 h-6 text-primary animate-spin"
      />
    </div>

    <template v-else>
      <!-- ── KANBAN VIEW ──────────────────────────────────────────────────── -->
      <div
        v-if="viewMode === 'kanban'"
        class="overflow-x-auto pb-4"
      >
        <div class="flex gap-4 min-w-max">
          <div
            v-for="col in statusColumns"
            :key="col.key"
            class="w-72 shrink-0 rounded-xl p-1 transition-all duration-150"
            :class="draggingId ? 'bg-primary/5 ring-2 ring-inset ring-primary/20' : 'ring-2 ring-inset ring-transparent'"
          >
            <!-- Column header -->
            <div class="flex items-center gap-2 px-1 mb-3">
              <UIcon
                :name="col.icon"
                class="w-4 h-4 text-muted"
              />
              <span class="text-sm font-semibold text-highlighted">{{ col.label }}</span>
              <span class="text-xs text-muted bg-elevated border border-default rounded-full px-1.5 ml-auto">
                {{ getDemandsForStatus(col.key).length }}
              </span>
            </div>

            <!-- Demand cards -->
            <div :ref="(el) => setKanbanListRef(col.key, el)" :data-status="col.key" class="space-y-2.5 min-h-24">
              <div
                v-for="demand in getDemandsForStatus(col.key)"
                :key="demand.id"
                :data-demand-id="demand.id"
                class="kanban-demand-item cursor-grab active:cursor-grabbing select-none transition-opacity"
                :class="draggingId === demand.id ? 'opacity-40' : ''"
              >
                <RoadmapDemandCard
                  :demand="demand"
                  :planning-quarter-options="planningQuarterOptions"
                  @edit="openEditModal"
                  @delete="promptDelete"
                  @plan="planDemandToQuarter"
                />
              </div>

              <div
                v-if="!getDemandsForStatus(col.key).length"
                class="border-2 border-dashed rounded-xl p-4 flex items-center justify-center text-xs text-muted transition-colors"
                :class="draggingId ? 'border-primary' : 'border-default'"
              >
                {{ hasActiveFilters ? 'Sem resultados' : 'Nenhuma demanda' }}
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- ── LIST VIEW ───────────────────────────────────────────────────── -->
      <UCard
        v-else
        :ui="{ header: 'px-3 py-2 sm:px-3 sm:py-2', body: 'p-0 sm:p-0' }"
        class="ring-default overflow-hidden"
      >
        <template #header>
          <div class="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p class="text-sm font-semibold text-highlighted">Demandas</p>
              <p class="mt-0.5 text-[11px] leading-none text-muted">
                <template v-if="listHasActiveFilters">
                  {{ listFilteredCount.toLocaleString('pt-BR') }} de {{ quarterFilteredDemands.length.toLocaleString('pt-BR') }} demandas
                </template>
                <template v-else>
                  {{ quarterFilteredDemands.length.toLocaleString('pt-BR') }} demandas
                </template>
              </p>
            </div>
            <div class="flex items-center gap-2">
              <UPopover v-if="selectedDemandCount">
                <UButton
                  size="sm"
                  color="primary"
                  variant="soft"
                  trailing-icon="i-lucide-chevron-down"
                  leading-icon="i-lucide-calendar-range"
                  :loading="isBulkPlanning"
                >
                  Mover {{ selectedDemandCount.toLocaleString('pt-BR') }} selecionadas
                </UButton>
                <template #content>
                  <div class="py-1 min-w-[220px]">
                    <button
                      v-for="option in bulkMoveQuarterOptions"
                      :key="option.value"
                      class="w-full px-3 py-2 text-left text-sm text-highlighted transition-colors hover:bg-elevated"
                      @click="planSelectedDemandsToQuarter(option.value)"
                    >
                      {{ option.label }}
                    </button>
                  </div>
                </template>
              </UPopover>
              <UButton
                v-if="listHasActiveFilters"
                size="sm"
                color="neutral"
                variant="ghost"
                leading-icon="i-lucide-filter-x"
                @click="clearListFilters"
              >
                Limpar filtros
                <UBadge size="xs" color="primary" variant="solid" class="ml-1">{{ listColumnFilters.length }}</UBadge>
              </UButton>
              <div class="relative">
                <UButton
                  variant="ghost"
                  size="sm"
                  trailing-icon="i-lucide-chevron-down"
                  leading-icon="i-lucide-download"
                  color="neutral"
                  @click="toggleListExportMenu"
                >
                  Exportar
                </UButton>
                <div v-if="listExportMenuOpen" class="fixed inset-0 z-40" @click="closeListExportMenu" />
                <div
                  v-show="listExportMenuOpen"
                  class="absolute right-0 top-full mt-1 z-50 min-w-48 rounded-lg border border-default bg-default shadow-lg p-1 flex flex-col gap-0.5"
                >
                  <a
                    :href="listExportUrlVisible"
                    download="roadmap-visiveis.xlsx"
                    class="flex items-center gap-2 px-3 py-2 rounded-md text-sm text-highlighted hover:bg-elevated cursor-pointer select-none no-underline"
                    @click="closeListExportMenu"
                  >
                    <UIcon name="i-lucide-table-2" class="size-4 text-muted shrink-0" />
                    Exportar dados visíveis
                  </a>
                  <a
                    :href="listExportUrlFull"
                    download="roadmap-completo.xlsx"
                    class="flex items-center gap-2 px-3 py-2 rounded-md text-sm text-highlighted hover:bg-elevated cursor-pointer select-none no-underline"
                    @click="closeListExportMenu"
                  >
                    <UIcon name="i-lucide-database" class="size-4 text-muted shrink-0" />
                    Exportar modelo completo
                  </a>
                </div>
              </div>
            </div>
          </div>
        </template>

        <div ref="listScrollContainerRef" :class="shouldConstrainListHeight ? 'max-h-[560px] overflow-x-auto overflow-y-auto' : 'overflow-x-auto overflow-y-visible'">
          <!-- Header externo fixo -->
          <div class="sticky top-0 z-10 border-b border-accented bg-default overflow-hidden">
            <table class="table-fixed text-sm" style="width: 100%">
              <thead>
                <tr ref="listHeaderRowRef">
                  <th
                    v-for="col in listOrderedCols"
                    :key="col.id"
                    :data-col-id="col.id"
                    class="relative px-3 py-2.5 text-xs text-highlighted font-medium overflow-hidden"
                    :class="col.alignRight ? 'text-right' : 'text-left'"
                    :style="{ width: listColWidth(col.id, col.defaultWidth) }"
                  >
                    <span
                      class="list-col-drag absolute left-1 top-1/2 -translate-y-1/2 cursor-grab text-muted opacity-25 hover:opacity-80 select-none"
                      title="Arrastar coluna"
                    >⠿</span>
                    <div class="flex flex-col pl-2" :class="col.alignRight ? 'items-end' : ''">
                      <span v-if="col.id === 'select'" class="block h-4 w-4" />
                      <UButton
                        v-else-if="col.id !== '_actions'"
                        :color="listSorting[0]?.id === col.id ? 'primary' : 'neutral'"
                        variant="ghost"
                        size="xs"
                        :label="col.label"
                        :trailing-icon="listSorting[0]?.id === col.id
                          ? (listSorting[0]?.desc ? 'i-lucide-arrow-down' : 'i-lucide-arrow-up')
                          : 'i-lucide-arrow-up-down'"
                        class="-mx-2 text-xs font-medium"
                        :disabled="col.disableSorting"
                        @click="!col.disableSorting && toggleListSort(col.id)"
                      />
                      <div v-if="col.id !== '_actions' && col.id !== 'select'">
                        <UPopover v-if="col.filterType === 'multi-select'">
                          <button class="mt-1 flex w-full items-center gap-1.5 rounded-md border border-default px-2 py-1 text-xs bg-background hover:border-primary/40 transition-colors">
                            <span class="flex-1 truncate text-left text-highlighted">{{ getListMultiFilterLabel(col) }}</span>
                            <UIcon name="i-lucide-chevron-down" class="w-3.5 h-3.5 shrink-0 text-muted" />
                          </button>
                          <template #content>
                            <div class="py-1 min-w-[220px] max-h-72 overflow-y-auto">
                              <button
                                class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                                :class="getListMultiFilter(col.id).length === 0 ? 'text-primary font-medium' : 'text-highlighted'"
                                @click="setListMultiFilter(col.id, [])"
                              >
                                <UIcon v-if="getListMultiFilter(col.id).length === 0" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0" />
                                <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                                {{ col.allLabel ?? 'Todos' }}
                              </button>
                              <button
                                v-for="option in (col.id === 'quarterLabel'
                                  ? quarterOptions
                                  : col.id === 'products'
                                  ? selectedProjectProducts.map(product => ({ value: product.id, label: product.name }))
                                  : (col.selectOptions ?? []))"
                                :key="option.value"
                                class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                                :class="getListMultiFilter(col.id).includes(option.value) ? 'text-primary' : 'text-highlighted'"
                                @click="toggleListMultiFilterValue(col.id, option.value)"
                              >
                                <UIcon v-if="getListMultiFilter(col.id).includes(option.value)" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0 text-primary" />
                                <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                                {{ option.label }}
                              </button>
                            </div>
                          </template>
                        </UPopover>
                        <UInput
                          v-else-if="col.filterType === 'text'"
                          :model-value="getListColFilter(col.id)"
                          placeholder="Filtrar…"
                          size="xs"
                          class="mt-1"
                          :class="col.alignRight ? 'text-right' : ''"
                          @update:model-value="(v: string) => setListColFilter(col.id, v)"
                        />
                      </div>
                    </div>
                    <span
                      v-if="col.id !== '_actions'"
                      class="absolute right-0 top-0 h-full w-[4px] cursor-col-resize hover:bg-primary active:bg-primary select-none"
                      @mousedown.prevent.stop="startListResize(col.id, $event)"
                    />
                  </th>
                </tr>
              </thead>
            </table>
          </div>

          <div ref="listTableRootRef">
            <UTable
              :key="listTableKey"
              ref="listTable"
              v-model:sorting="listSorting"
              v-model:column-filters="listColumnFilters"
              v-model:column-sizing="listColumnSizing"
              v-model:column-order="listColumnOrder"
              :data="quarterFilteredDemands"
              :columns="listTanstackColumns"
              :get-row-id="(row: RoadmapDemand) => row.id"
              :column-sizing-options="{ enableColumnResizing: true, columnResizeMode: 'onChange' }"
              :ui="{ base: 'w-full table-fixed', thead: 'hidden', td: 'py-2 overflow-hidden' }"
            >
              <template #status-cell="{ row }">
                <div
                  class="flex items-center gap-1.5"
                  :title="getDemandNotesTooltip(row.original) || statusLabels[row.original.status]"
                >
                  <span
                    class="inline-block h-2.5 w-2.5 shrink-0 rounded-full"
                    :class="statusDotClass[row.original.status]"
                    aria-hidden="true"
                  />
                  <span class="text-xs font-medium" :class="statusTextClass[row.original.status]">
                    {{ statusLabels[row.original.status] }}
                  </span>
                  <span
                    v-if="row.original.isBlocked"
                    class="inline-flex items-center justify-center rounded-full border border-red-200 bg-red-50 px-1.5 py-0.5 text-[11px] font-medium text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300"
                  >
                    <UIcon name="i-lucide-triangle-alert" class="w-3 h-3" />
                  </span>
                  <span
                    v-else-if="getDemandNotesTooltip(row.original)"
                    class="inline-flex items-center justify-center rounded-full border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[11px] font-medium text-amber-700 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300"
                  >
                    <UIcon name="i-lucide-message-square-warning" class="w-3 h-3" />
                  </span>
                </div>
              </template>
            </UTable>
          </div>
        </div>

        <!-- Totalizador -->
        <div class="border-t-2 border-default bg-elevated/60">
          <table class="w-full text-sm">
            <tfoot>
              <tr>
                <td class="px-4 py-2.5 text-right">
                  <span class="text-xs font-semibold text-highlighted">
                    {{ listFilteredCount.toLocaleString('pt-BR') }}
                    <span v-if="listHasActiveFilters" class="font-normal text-muted"> de {{ quarterFilteredDemands.length.toLocaleString('pt-BR') }}</span>
                    demandas
                  </span>
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
      </UCard>

      <section class="-mt-1 space-y-3">
        <div>
          <p class="text-sm font-semibold text-highlighted">Resumo do Quarter</p>
          <p class="text-xs text-muted">Totalizadores ordenados pelas maiores cargas horárias das demandas visíveis no quarter.</p>
        </div>

        <div class="grid items-stretch gap-4 xl:grid-cols-3">
          <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
            <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
              <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-violet-50 text-violet-600 dark:bg-violet-900/20 dark:text-violet-300">
                <UIcon name="i-lucide-chart-pie" class="h-4.5 w-4.5" />
              </div>
              <div>
                <p class="text-sm font-semibold text-highlighted">Classificação</p>
              </div>
            </div>

            <div v-if="classificationTotals.length" class="space-y-3 px-3.5 py-3.5">
              <div v-for="item in classificationTotals" :key="item.classification" class="space-y-1.5">
                <div class="flex items-center gap-2">
                  <span class="h-2.5 w-2.5 rounded-full" :class="classificationBadgeClass[item.classification]" />
                  <span class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.label }}</span>
                  <span class="text-[11px] text-muted">{{ item.hours.toLocaleString('pt-BR') }}h</span>
                  <span class="text-xs font-semibold text-highlighted">{{ item.percentage.toFixed(1) }}%</span>
                  <span class="rounded-full bg-elevated px-2 py-0.5 text-[11px] text-muted">{{ item.count }} dem.</span>
                </div>
                <div class="h-1.5 overflow-hidden rounded-full bg-elevated">
                  <div class="h-full rounded-full bg-primary transition-all duration-300" :style="{ width: `${Math.min(item.percentage, 100)}%` }" />
                </div>
              </div>
            </div>

            <div v-else class="px-3.5 py-5 text-sm text-muted">
              Nenhuma demanda com classificação no quarter selecionado.
            </div>
          </UCard>

          <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
            <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
              <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-blue-50 text-blue-600 dark:bg-blue-900/20 dark:text-blue-300">
                <UIcon name="i-lucide-users" class="h-4.5 w-4.5" />
              </div>
              <div>
                <p class="text-sm font-semibold text-highlighted">Demandas por Cliente</p>
              </div>
            </div>

            <div v-if="customerTotals.length" class="flex min-h-0 flex-1 flex-col px-3.5 py-3.5">
              <div class="min-h-0 flex-1 space-y-2 overflow-y-auto pr-1 pb-3">
                <div v-for="item in customerTotals" :key="item.name" class="rounded-lg border border-default bg-elevated/20 px-2.5 py-2">
                  <div class="flex items-center gap-2">
                    <p class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.name }}</p>
                    <span class="text-[11px] text-muted">{{ item.hours.toLocaleString('pt-BR') }}h</span>
                    <span class="rounded-full bg-blue-50 px-2 py-0.5 text-[11px] font-semibold text-blue-700 dark:bg-blue-900/30 dark:text-blue-300">
                      {{ item.percentage.toFixed(1) }}%
                    </span>
                    <span class="rounded-full bg-blue-100 px-2 py-0.5 text-[11px] font-semibold text-blue-700 dark:bg-blue-900/30 dark:text-blue-300">
                      {{ item.count }} dem.
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <div v-else class="px-3.5 py-5 text-sm text-muted">
              Nenhum cliente associado nas demandas deste quarter.
            </div>
          </UCard>

          <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
            <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
              <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-50 text-emerald-600 dark:bg-emerald-900/20 dark:text-emerald-300">
                <UIcon name="i-lucide-tags" class="h-4.5 w-4.5" />
              </div>
              <div>
                <p class="text-sm font-semibold text-highlighted">Demandas por Tipo</p>
              </div>
            </div>

            <div v-if="typeTotals.length" class="space-y-3 px-3.5 py-3.5">
              <div v-for="item in typeTotals" :key="item.type" class="flex items-center justify-between gap-3 rounded-xl border border-default bg-default px-3 py-2.5 shadow-sm">
                <div class="flex min-w-0 items-center gap-3">
                  <div class="flex h-8 w-8 items-center justify-center rounded-full border" :class="typeSummaryTone[item.type]">
                    <span class="h-2.5 w-2.5 rounded-full" :class="typeSummaryDot[item.type]" />
                  </div>
                  <div class="min-w-0">
                    <p class="truncate text-sm font-semibold text-highlighted">{{ item.label }}</p>
                    <p class="mt-0.5 text-[11px] text-muted">{{ item.hours.toLocaleString('pt-BR') }}h totais</p>
                  </div>
                </div>
                <div class="flex items-center gap-2">
                  <span class="rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-300">
                    {{ item.percentage.toFixed(1) }}%
                  </span>
                  <span class="rounded-full bg-elevated px-3 py-1 text-xs font-semibold text-highlighted">
                    {{ item.count }} demandas
                  </span>
                </div>
              </div>
            </div>

            <div v-else class="px-4 py-6 text-sm text-muted">
              Nenhum tipo de demanda registrado neste quarter.
            </div>
          </UCard>
        </div>
      </section>
    </template>

    <!-- Empty state (no projects) -->
    <div
      v-if="!isLoading && !projects.length"
      class="flex flex-col items-center justify-center py-20 gap-3"
    >
      <UIcon
        name="i-lucide-map"
        class="w-12 h-12 text-muted"
      />
      <p class="text-muted text-sm">
        Nenhum projeto encontrado.
      </p>
    </div>

    <!-- Create / Edit modal -->
    <RoadmapDemandFormModal
      v-model:open="modalOpen"
      :projects="projects"
      :dependency-options="dependencyOptions"
      :customer-suggestions="customerSuggestions"
      :demand="editingDemand"
      :default-project-id="selectedProjectId ?? undefined"
      :default-quarter-year="selectedDemandScope?.quarterYear ?? activeCapacityScope?.quarterYear ?? selectedQuarterYear ?? undefined"
      :default-quarter-number="selectedDemandScope?.quarterNumber ?? activeCapacityScope?.quarterNumber ?? selectedQuarterNumber ?? undefined"
      @submit="handleSubmit"
    />

    <RoadmapCapacityModal
      v-model:open="capacityModalOpen"
      :project-name="selectedProject?.name"
      :quarter-label="activeCapacityScope?.quarterLabel"
      :initial-value="capacityModalInitialValue"
      :is-saving="isSavingCapacity"
      @submit="handleCapacitySubmit"
    />

    <!-- Confirm delete modal -->
    <UModal
      v-model:open="confirmDeleteOpen"
      title="Remover Demanda"
      description="Tem certeza que deseja remover esta demanda? Esta ação não pode ser desfeita."
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
