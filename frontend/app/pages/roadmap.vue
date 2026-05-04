<script setup lang="ts">
import { h, nextTick, resolveComponent, useTemplateRef, onMounted, onUnmounted } from 'vue'
import Sortable from 'sortablejs'
import type { TableColumn } from '@nuxt/ui'
import type { SortingState, ColumnFiltersState, ColumnSizingState } from '@tanstack/vue-table'
import type * as XLSXType from 'xlsx'
import type { RoadmapDemand, DemandDependency, RoadmapCapacitySummary, DemandFormData, CapacityFormData, DemandStatus, DemandType, DemandClassification, NoKpiClassification, RoadmapItemType } from '~/types/roadmap'
import RoadmapHierarchyPage from '~/components/roadmap/RoadmapHierarchyPage.vue'
import { getLatestPromisedDate } from '~/utils/roadmapPromisedDate'

useSeoMeta({ title: 'Roadmap · ProductHub' })

const route = useRoute()
const roadmapStore = useRoadmapStore()
const kpiStore = useKpiStore()
const toast = useToast()

const { projects, demands, dependencyOptions, customerSuggestions, capacitySummary, selectedProject, selectedProjectId, selectedQuarterYear, selectedQuarterNumber, isLoading, isCapacityLoading } = storeToRefs(roadmapStore)
const { kpis: availableKpis } = storeToRefs(kpiStore)
const sortedProjects = computed(() =>
  [...projects.value].sort((left, right) => right.name.localeCompare(left.name, 'pt-BR'))
)
const roadmapItems = computed(() => demands.value.filter(item => item.itemType === 'Roadmap'))
const epicItems = computed(() => demands.value.filter(item => item.itemType === 'Epic'))
const demandItems = computed(() => demands.value.filter(item => item.itemType === 'Demand'))
const itemsById = computed(() => new Map(demands.value.map(item => [item.id, item] as const)))

// ─── View mode ───────────────────────────────────────────────────────────────
const viewMode = ref<'list' | 'hierarchy'>(route.query.view === 'hierarchy' ? 'hierarchy' : 'list')

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

function normalizeCustomerList(customers?: string[]): string[] {
  return (customers ?? []).map(customer => customer.trim()).filter(Boolean)
}

function getEffectiveDemandCustomers(demand: Pick<RoadmapDemand, 'itemType' | 'customers' | 'epicId'>): string[] {
  if (demand.itemType === 'Epic')
    return normalizeCustomerList(demand.customers)

  if (!demand.epicId)
    return []

  const epic = itemsById.value.get(demand.epicId) ?? null
  if (!epic || epic.itemType !== 'Epic')
    return []

  return normalizeCustomerList(epic.customers)
}

function getEffectiveDemandClassification(demand: Pick<RoadmapDemand, 'itemType' | 'classification' | 'epicId'>): DemandClassification | undefined {
  if (demand.itemType === 'Epic')
    return demand.classification

  if (!demand.epicId)
    return demand.classification

  const epic = itemsById.value.get(demand.epicId) ?? null
  if (!epic || epic.itemType !== 'Epic')
    return demand.classification

  return epic.classification
}

function formatDemandDate(value?: string) {
  if (!value)
    return ''

  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day)
    return value

  return new Intl.DateTimeFormat('pt-BR').format(new Date(year, month - 1, day))
}

function openDemandKpiWorkspace(demand: RoadmapDemand) {
  const targetEpicId = demand.itemType === 'Epic' ? demand.id : demand.epicId
  if (!targetEpicId)
    return

  const query: Record<string, string> = {
    kpiDemandId: targetEpicId
  }

  if (selectedProjectId.value ?? demand.projectId)
    query.projectId = selectedProjectId.value ?? demand.projectId ?? ''

  if (viewMode.value === 'hierarchy')
    query.view = 'hierarchy'

  navigateTo({
    path: '/roadmap',
    query
  })
}

function closeDemandKpiWorkspace() {
  const query: Record<string, string> = {}

  if (selectedProjectId.value)
    query.projectId = selectedProjectId.value

  if (viewMode.value === 'hierarchy')
    query.view = 'hierarchy'

  navigateTo({
    path: '/roadmap',
    query: Object.keys(query).length ? query : undefined
  })
}

function getDemandKpiSummary(demand: RoadmapDemand) {
  const targetEpic = demand.itemType === 'Epic'
    ? demand
    : (demand.epicId ? itemsById.value.get(demand.epicId) ?? null : null)

  if (!targetEpic || targetEpic.itemType !== 'Epic') {
    return {
      label: 'Sem épico',
      tone: 'border-default bg-elevated text-muted',
      actionLabel: 'Associe a demanda a um épico'
    }
  }

  if (targetEpic.hasNoKpi) {
    return {
      label: 'SEM KPI',
      tone: 'border-warning/40 bg-warning/10 text-warning',
      actionLabel: 'Editar registro de KPI do épico'
    }
  }

  if (targetEpic.kpiLinks.length > 0) {
    return {
      label: `${targetEpic.kpiLinks.length} KPI${targetEpic.kpiLinks.length > 1 ? 's' : ''}`,
      tone: 'border-primary/20 bg-primary/10 text-primary',
      actionLabel: 'Abrir registro de KPI do épico'
    }
  }

  return {
    label: 'Incluir KPI',
    tone: 'border-error/40 bg-error/10 text-error',
    actionLabel: 'Incluir KPI'
  }
}

function getEpicDisplayGroupKey(demand: Pick<RoadmapDemand, 'roadmapId' | 'epicId' | 'quarterYear' | 'quarterNumber' | 'type'>) {
  return [
    demand.roadmapId ?? 'none',
    demand.epicId ?? 'none',
    demand.quarterYear,
    demand.quarterNumber,
    getDemandGroupKey(demand)
  ].join(':')
}

function getDisplayIssueLinks(demand: Pick<RoadmapDemand, 'issueLinks' | 'jiraIssue'>) {
  if (demand.issueLinks?.length)
    return demand.issueLinks

  if (demand.jiraIssue?.trim())
    return [{ key: demand.jiraIssue.trim() }]

  return []
}

function getVisibleEpicDemandCluster(anchorDemand?: RoadmapDemand) {
  if (!anchorDemand)
    return []

  if (!groupDemandsByEpic.value)
    return [anchorDemand]

  const anchorIndex = visibleListRows.value.findIndex(demand => demand.id === anchorDemand.id)
  if (anchorIndex < 0)
    return []

  const groupKey = getEpicDisplayGroupKey(anchorDemand)
  const cluster: RoadmapDemand[] = []

  for (let index = anchorIndex; index < visibleListRows.value.length; index++) {
    const demand = visibleListRows.value[index]!
    if (getEpicDisplayGroupKey(demand) !== groupKey)
      break

    cluster.push(demand)
  }

  return cluster
}

function getVisibleEpicDemands(epicId?: string) {
  if (!epicId)
    return []

  return visibleListRows.value.filter(demand => demand.epicId === epicId)
}

function getEpicQuarterMoveScopeKey(demand: Pick<RoadmapDemand, 'projectId' | 'type'>) {
  return `${demand.projectId}:${getDemandGroupKey(demand)}`
}

function getEpicHeaderMeta(anchorDemand?: RoadmapDemand) {
  const epicId = anchorDemand?.epicId
  const epic = epicId ? itemsById.value.get(epicId) ?? null : null
  if (!epic || epic.itemType !== 'Epic')
    return null

  const groupedDemands = getVisibleEpicDemandCluster(anchorDemand)
  const totalHours = groupedDemands.reduce((sum, demand) => {
    return sum + (typeof demand.hours === 'number' ? demand.hours : 0)
  }, 0)
  const groupedProductNames = Array.from(new Set(groupedDemands.flatMap(demand => demand.products.map(product => product.name))))
  const groupedCustomers = normalizeCustomerList(epic.customers)

  return {
    epic,
    kpiSummary: getDemandKpiSummary(epic),
    products: groupedProductNames,
    productsLabel: groupedProductNames.join(' · '),
    customers: groupedCustomers,
    customersLabel: formatDemandCustomers(groupedCustomers),
    issueLinks: getDisplayIssueLinks(epic),
    totalHours
  }
}

function getNoKpiClassificationLabel(value: NoKpiClassification | undefined) {
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

function getDeprioritizationReasonLabel(value: string | undefined) {
  switch (value) {
    case 'Strategic':
      return 'Estratégico'
    case 'MandatoryUrgent':
      return 'Mandatório/Urgente'
    case 'LowImpact':
      return 'Baixo impacto'
    case 'LackOfCapacity':
      return 'Falta de capacidade'
    case 'ContextChange':
      return 'Mudança de contexto'
    case 'Customizacao':
      return 'Customização'
    default:
      return ''
  }
}

const activeDemandKpiId = computed(() => {
  const value = route.query.kpiDemandId
  return typeof value === 'string' ? value : ''
})

const activeDemandKpi = computed(() =>
  epicItems.value.find(demand => demand.id === activeDemandKpiId.value) ?? null
)

function getDisplayedDemandStatus(demand: RoadmapDemand) {
  return {
    label: statusLabels[demand.status],
    textClass: statusTextClass[demand.status],
    dotClass: statusDotClass[demand.status]
  }
}

function getStatusBadgeClass(status: DemandStatus) {
  switch (status) {
    case 'Done':
      return 'border-green-200 bg-green-50 text-green-700 dark:border-green-800 dark:bg-green-900/20 dark:text-green-300'
    case 'InProgress':
      return 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-800 dark:bg-blue-900/20 dark:text-blue-300'
    case 'Deprioritized':
      return 'border-red-200 bg-red-50 text-red-700 dark:border-red-800 dark:bg-red-900/20 dark:text-red-300'
    default:
      return 'border-default bg-default text-muted'
  }
}

function getDerivedEpicPromisedDate(epic: RoadmapDemand) {
  return getLatestPromisedDate(demandItems.value.filter(demand => demand.epicId === epic.id))
}

function getDisplayedPromisedDate(demand: RoadmapDemand) {
  const directPromisedDate = demand.effectivePromisedDate ?? demand.promisedDate ?? ''
  if (directPromisedDate || demand.itemType !== 'Epic')
    return directPromisedDate

  return getDerivedEpicPromisedDate(demand)
}

function getDisplayedConclusionDate(demand: RoadmapDemand) {
  if (demand.status === 'Done' && demand.deliveryDate)
    return demand.deliveryDate

  return getDisplayedPromisedDate(demand)
}

function showDemandDelayMarker(demand: RoadmapDemand) {
  return demand.isOverdue || demand.isDeliveredLate
}

function getDemandNotesTooltip(demand: RoadmapDemand): string {
  const notes = []
  if (demand.isBlocked && demand.blockedReason)
    notes.push(`Impedimento\n${demand.blockedReason}`)
  if (demand.status === 'Deprioritized' && demand.observation)
    notes.push(`Despriorização${demand.deprioritizationReason ? ` · ${getDeprioritizationReasonLabel(demand.deprioritizationReason)}` : ''}\n${demand.observation}`)
  return notes.join('\n\n')
}

function formatDependencySummaryLine(dependency: DemandDependency) {
  return dependency.projectName ? `${dependency.projectName} · ${dependency.title}` : dependency.title
}

function formatDependencyBadgeLabel(prefix: 'Bloqueado por' | 'Bloqueia', dependency: DemandDependency) {
  return dependency.projectName ? `${prefix} ${dependency.projectName}` : `${prefix} ${dependency.itemType === 'Epic' ? 'épico' : 'demanda'}`
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

function hasInconsistentDependency(demand: RoadmapDemand) {
  return demand.dependsOn.some(dependency => isDependencyInconsistent(demand, dependency))
}

function isDemandEstimated(demand: Pick<RoadmapDemand, 'hours'>) {
  return typeof demand.hours === 'number' && demand.hours > 0
}

function findDependencyTarget(dependency: DemandDependency) {
  const dependencyId = dependency.demandId.toLowerCase()

  return demands.value.find((demand) => {
    return demand.id.toLowerCase() === dependencyId && demand.itemType === dependency.itemType
  }) ?? null
}

async function openDependencyDemand(dependency: DemandDependency) {
  let targetDemand = findDependencyTarget(dependency)

  if (!targetDemand) {
    const dependencyOption = dependencyOptions.value.find((option) => {
      return option.demandId.toLowerCase() === dependency.demandId.toLowerCase()
        && option.itemType === dependency.itemType
    })

    const targetProjectId = dependency.projectId ?? dependencyOption?.projectId ?? null
    if (targetProjectId)
      selectedProjectId.value = targetProjectId
    selectedQuarterYear.value = null
    selectedQuarterNumber.value = null
    await roadmapStore.fetchDemands()
    targetDemand = findDependencyTarget(dependency)

    if (!targetDemand && dependencyOption) {
      targetDemand = demands.value.find((demand) => {
        return demand.itemType === dependencyOption.itemType
          && demand.title === dependencyOption.title
          && demand.projectId === dependencyOption.projectId
      }) ?? null
    }

    if (!targetDemand) {
      const projectIdsToTry = Array.from(new Set([
        ...(targetProjectId ? [targetProjectId] : []),
        ...projects.value.map(project => project.id)
      ]))

      for (const projectId of projectIdsToTry) {
        if (selectedProjectId.value !== projectId)
          selectedProjectId.value = projectId

        await roadmapStore.fetchDemands()
        targetDemand = findDependencyTarget(dependency)

        if (!targetDemand && dependencyOption) {
          targetDemand = demands.value.find((demand) => {
            return demand.itemType === dependencyOption.itemType
              && demand.title === dependencyOption.title
              && demand.projectId === dependencyOption.projectId
          }) ?? null
        }

        if (targetDemand)
          break
      }
    }
  }

  if (!targetDemand) {
    toast.add({ title: `${dependency.itemType === 'Epic' ? 'Épico' : 'Demanda'} vinculado não encontrado`, color: 'warning' })
    return
  }

  openEditModal(targetDemand)
}

function getDependencyTooltip(prefix: 'É bloqueado por' | 'Bloqueia', dependency: DemandDependency) {
  return dependency.projectName ? `${prefix} ${dependency.projectName}: ${dependency.title}` : `${prefix}: ${dependency.title}`
}

function createSvgIcon(paths: string[], className = 'h-3 w-3 shrink-0') {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg')
  svg.setAttribute('viewBox', '0 0 24 24')
  svg.setAttribute('fill', 'none')
  svg.setAttribute('stroke', 'currentColor')
  svg.setAttribute('stroke-width', '2')
  svg.setAttribute('stroke-linecap', 'round')
  svg.setAttribute('stroke-linejoin', 'round')
  svg.setAttribute('class', className)

  paths.forEach((pathValue) => {
    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path')
    path.setAttribute('d', pathValue)
    svg.appendChild(path)
  })

  return svg
}

function createEpicDependencyBadge(demand: RoadmapDemand, dependency: DemandDependency, relation: 'dependsOn' | 'dependedOnBy') {
  const inconsistent = relation === 'dependsOn' && isDependencyInconsistent(demand, dependency)
  const button = document.createElement('button')
  button.type = 'button'
  button.className = inconsistent
    ? 'inline-flex max-w-full cursor-pointer items-center gap-1 rounded-full border border-red-200 bg-red-50 px-2 py-0.5 text-[11px] font-medium text-red-700 transition-colors hover:border-red-300 hover:bg-red-100 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300 dark:hover:border-red-700 dark:hover:bg-red-900/50'
    : 'inline-flex max-w-full cursor-pointer items-center gap-1 rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-[11px] font-medium text-amber-700 transition-colors hover:border-amber-300 hover:bg-amber-100 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300 dark:hover:border-amber-700 dark:hover:bg-amber-900/50'
  button.title = relation === 'dependsOn'
    ? `${getDependencyTooltip('É bloqueado por', dependency)}${inconsistent ? `\n\nInconsistência: a demanda vinculada está em ${dependency.quarterLabel}, depois de ${demand.quarterLabel}, ou sem priorização.` : ''}`
    : formatDependencySummaryLine(dependency)
  button.addEventListener('click', async (event) => {
    event.preventDefault()
    event.stopPropagation()
    await openDependencyDemand(dependency)
  })

  button.appendChild(createSvgIcon(['M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71', 'M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71']))

  const label = document.createElement('span')
  label.className = 'min-w-0 max-w-[14rem] truncate'
  label.textContent = formatDependencyBadgeLabel(relation === 'dependsOn' ? 'Bloqueado por' : 'Bloqueia', dependency)
  button.appendChild(label)

  if (inconsistent) {
    button.appendChild(createSvgIcon(['M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z', 'M12 9v4', 'M12 17h.01']))

    const text = document.createElement('span')
    text.className = 'shrink-0 font-semibold'
    text.textContent = 'Inconsistente'
    button.appendChild(text)
  }

  return button
}

function appendEpicDependencyRow(container: HTMLElement, demand: RoadmapDemand, dependencies: DemandDependency[], relation: 'dependsOn' | 'dependedOnBy') {
  if (!dependencies.length)
    return

  const row = document.createElement('div')
  row.className = 'mt-1 flex flex-wrap gap-1'

  dependencies.slice(0, 2).forEach((dependency) => {
    row.appendChild(createEpicDependencyBadge(demand, dependency, relation))
  })

  if (dependencies.length > 2) {
    const more = document.createElement('span')
    more.className = 'text-[11px] text-muted'
    more.textContent = `+${dependencies.length - 2}`
    row.appendChild(more)
  }

  container.appendChild(row)
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

function getDemandGroupKey(demand: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'type'>): 'regular' | 'additional' {
  if (isAdditionalDemand(demand)) return 'additional'
  return 'regular'
}

function compareListDemandGroups(left: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'type'>, right: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'type'>) {
  const leftBacklog = isBacklogDemand(left as RoadmapDemand)
  const rightBacklog = isBacklogDemand(right as RoadmapDemand)
  if (leftBacklog !== rightBacklog) return leftBacklog ? 1 : -1

  if (left.quarterYear !== right.quarterYear) return left.quarterYear - right.quarterYear
  if (left.quarterNumber !== right.quarterNumber) return left.quarterNumber - right.quarterNumber

  const leftAdditional = isAdditionalDemand(left) ? 1 : 0
  const rightAdditional = isAdditionalDemand(right) ? 1 : 0
  return leftAdditional - rightAdditional
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
const filterProducts = ref<string[]>([])

const demandTypes: DemandType[] = ['Planned', 'Spillover', 'Unplanned', 'Additional']
const demandClassifications: DemandClassification[] = [
  'TechnicalDebtSecurity', 'Strategic', 'Evolution', 'ImprovementGap', 'Mandatory', 'Homologation', 'Customizacao'
]

const selectedProjectProducts = computed(() =>
  projects.value.find(p => p.id === selectedProjectId.value)?.products ?? []
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

  return demandItems.value.filter(demand =>
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

  const nonEstimatedDemandCount = quarterScopedDemands.value
    .filter(demand => !isDemandEstimated(demand))
    .length

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
    nonEstimatedDemandCount,
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

const capacityUnestimatedTone = computed(() => {
  if ((displayCapacitySummary.value?.nonEstimatedDemandCount ?? 0) > 0)
    return 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300'

  return 'border-default bg-default text-muted'
})

const capacityUnestimatedLabel = computed(() => {
  const count = displayCapacitySummary.value?.nonEstimatedDemandCount ?? 0
  return `${count} ${count === 1 ? 'demanda sem estimativa' : 'demandas sem estimativa'}`
})

const scopedDemandTotalHours = computed(() =>
  quarterFilteredDemands.value.reduce((total, demand) => total + (demand.hours ?? 0), 0)
)

const classificationTotals = computed(() => {
  const totalHours = scopedDemandTotalHours.value

  const totals = new Map<DemandClassification, { classification: DemandClassification, label: string, hours: number, count: number }>()

  for (const demand of quarterFilteredDemands.value) {
    const classification = String(getEffectiveDemandClassification(demand) ?? '').trim() as DemandClassification
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
    const customers = getEffectiveDemandCustomers(demand)
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
  const orderedDemands = [...demandItems.value].sort((left, right) => {
    const groupComparison = compareListDemandGroups(left, right)
    if (groupComparison !== 0)
      return groupComparison

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
    list = list.filter(d => getEffectiveDemandCustomers(d).some(customer => customer.toLowerCase().includes(q)))
  }
  if (filterTypes.value.length)
    list = list.filter(d => filterTypes.value.includes(d.type))
  if (filterClassifications.value.length)
    list = list.filter((demand) => {
      const classification = getEffectiveDemandClassification(demand)
      return classification ? filterClassifications.value.includes(classification) : false
    })
  return list
})

// ─── Drag and Drop ────────────────────────────────────────────────────────────
const listScrollContainerRef = ref<HTMLElement | null>(null)
const listTableRootRef = ref<HTMLElement | null>(null)
const listViewportWidth = ref(0)
const listHeaderScrollLeft = ref(0)
let listBodySortable: Sortable | null = null
let listWidthObserver: ResizeObserver | null = null

function updateListViewportWidth() {
  listViewportWidth.value = listScrollContainerRef.value?.clientWidth ?? 0
}

function syncListHeaderScroll() {
  listHeaderScrollLeft.value = listScrollContainerRef.value?.scrollLeft ?? 0
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

function moveDemandCluster(
  ids: string[],
  movedIds: string[],
  beforeId: string | null,
  afterId: string | null
) {
  const movedIdSet = new Set(movedIds)
  const nextIds = ids.filter(id => !movedIdSet.has(id))

  if (beforeId) {
    const targetIndex = nextIds.indexOf(beforeId)
    if (targetIndex >= 0) {
      nextIds.splice(targetIndex, 0, ...movedIds)
      return nextIds
    }
  }

  if (afterId) {
    const targetIndex = nextIds.indexOf(afterId)
    if (targetIndex >= 0) {
      nextIds.splice(targetIndex + 1, 0, ...movedIds)
      return nextIds
    }
  }

  nextIds.push(...movedIds)
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
  return demandItems.value
    .filter(item => isSameDemandScope(item, demand))
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map(item => item.id)
}

function getDemandScopeKey(demand: Pick<RoadmapDemand, 'projectId' | 'quarterYear' | 'quarterNumber' | 'type'>) {
  return `${demand.projectId}:${demand.quarterYear}:${demand.quarterNumber}:${getDemandGroupKey(demand)}`
}

function getDemandDragScopeKey(demand: RoadmapDemand) {
  return groupDemandsByEpic.value
    ? getEpicDisplayGroupKey(demand)
    : getDemandScopeKey(demand)
}

function ensureDemandCanMoveToStatus(demand: RoadmapDemand, status: DemandStatus) {
  if (status === 'Done' && !demand.deliveryDate) {
    toast.add({ title: 'Informe a data de entrega antes de concluir', color: 'warning' })
    openEditModal(demand, status)
    return false
  }

  if (status === 'Deprioritized' && !demand.observation) {
    toast.add({ title: 'Informe o motivo e a observação da despriorização', color: 'warning' })
    openEditModal(demand, status)
    return false
  }

  if (status === 'Deprioritized' && !demand.deprioritizationReason) {
    toast.add({ title: 'Informe o motivo e a observação da despriorização', color: 'warning' })
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

async function persistEpicClusterPriority(
  anchorDemand: RoadmapDemand,
  beforeId: string | null,
  afterId: string | null
) {
  const currentCluster = getVisibleEpicDemandCluster(anchorDemand)
  if (!currentCluster.length)
    return

  const scopedDemandIds = getScopedDemandIds(anchorDemand)
  const movedIds = currentCluster.map(demand => demand.id)
  const orderedDemandIds = moveDemandCluster(scopedDemandIds, movedIds, beforeId, afterId)
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null

  try {
    await roadmapStore.reorderDemand(anchorDemand.id, anchorDemand.status, orderedDemandIds)
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

async function handleEpicSortEnd(item: HTMLElement) {
  const anchorDemandId = item.dataset.anchorDemandId
  if (!anchorDemandId)
    return

  const anchorDemand = visibleListRows.value.find(demand => demand.id === anchorDemandId)
  if (!anchorDemand) {
    await roadmapStore.fetchDemands()
    return
  }

  const currentCluster = getVisibleEpicDemandCluster(anchorDemand)
  const tbody = item.parentElement
  if (!tbody)
    return

  const targetQuarterKey = item.dataset.targetQuarterKey
  const targetDemandId = item.dataset.targetDemandId
  const targetInsertAfter = item.dataset.targetInsertAfter === 'true'
  const targetDemand = targetDemandId
    ? visibleSortableRows.value.find(demand => demand.id === targetDemandId)
    : null

  const tbodyRows = Array.from(tbody.children) as HTMLTableRowElement[]
  const currentIndex = tbodyRows.indexOf(item as HTMLTableRowElement)
  if (currentIndex < 0)
    return

  const nextDemandId = tbodyRows.slice(currentIndex + 1)
    .map(row => row.dataset.demandId)
    .find((value): value is string => !!value)
  const previousDemandId = [...tbodyRows.slice(0, currentIndex)].reverse()
    .map(row => row.dataset.demandId)
    .find((value): value is string => !!value)

  const nextDemand = nextDemandId
    ? visibleSortableRows.value.find(demand => demand.id === nextDemandId)
    : null
  const previousDemand = previousDemandId
    ? visibleSortableRows.value.find(demand => demand.id === previousDemandId)
    : null

  const fallbackQuarterRef = targetDemand ?? nextDemand ?? previousDemand
  const targetQuarter = targetQuarterKey
    ? (() => {
        const [quarterYear, quarterNumber] = targetQuarterKey.split(':').map(Number)
        return Number.isFinite(quarterYear) && Number.isFinite(quarterNumber)
          ? { quarterYear, quarterNumber }
          : null
      })()
    : null
  const targetQuarterRef = targetQuarter ?? (fallbackQuarterRef
    ? { quarterYear: fallbackQuarterRef.quarterYear, quarterNumber: fallbackQuarterRef.quarterNumber }
    : null)
  if (!targetQuarterRef)
    return

  const quarterChanged = anchorDemand.quarterYear !== targetQuarterRef.quarterYear || anchorDemand.quarterNumber !== targetQuarterRef.quarterNumber
  if (quarterChanged) {
    const sameGroupInTarget = (row: RoadmapDemand) =>
      row.id !== anchorDemand.id
      && !currentCluster.some(clusterDemand => clusterDemand.id === row.id)
      && row.quarterYear === targetQuarterRef.quarterYear
      && row.quarterNumber === targetQuarterRef.quarterNumber
      && getDemandGroupKey(row) === getDemandGroupKey(anchorDemand)

    const beforeId = targetDemand && sameGroupInTarget(targetDemand) && !targetInsertAfter
      ? targetDemand.id
      : (nextDemand && sameGroupInTarget(nextDemand) ? nextDemand.id : null)
    const afterId = beforeId
      ? null
      : (targetDemand && sameGroupInTarget(targetDemand) && targetInsertAfter
          ? targetDemand.id
          : (previousDemand && sameGroupInTarget(previousDemand) ? previousDemand.id : null))

    await planEpicDemandsToQuarter(
      anchorDemand.epicId ?? '',
      `${targetQuarterRef.quarterNumber}-${targetQuarterRef.quarterYear}`,
      {
        anchorDemandId: anchorDemand.id,
        beforeId,
        afterId
      }
    )
    return
  }

  const epicRows = Array.from(tbody.querySelectorAll('.list-epic-divider')) as HTMLTableRowElement[]
  const epicRowIndex = epicRows.indexOf(item as HTMLTableRowElement)
  if (epicRowIndex < 0)
    return

  const nextAnchorDemand = epicRows.slice(epicRowIndex + 1)
    .map(row => row.dataset.anchorDemandId)
    .find((value): value is string => !!value)
  const previousAnchorDemand = [...epicRows.slice(0, epicRowIndex)].reverse()
    .map(row => row.dataset.anchorDemandId)
    .find((value): value is string => !!value)

  const scopeKey = item.dataset.scopeKey
  if (!scopeKey)
    return

  const nextAnchorRow = epicRows.slice(epicRowIndex + 1).find(row =>
    row.dataset.scopeKey === scopeKey
    && row.dataset.anchorDemandId
    && row.dataset.anchorDemandId !== anchorDemandId
  )
  const previousAnchorRow = [...epicRows.slice(0, epicRowIndex)].reverse().find(row =>
    row.dataset.scopeKey === scopeKey
    && row.dataset.anchorDemandId
    && row.dataset.anchorDemandId !== anchorDemandId
  )

  let beforeId: string | null = null
  let afterId: string | null = null

  if (nextAnchorRow?.dataset.anchorDemandId) {
    const nextAnchorDemand = visibleListRows.value.find(demand => demand.id === nextAnchorRow.dataset.anchorDemandId)
    beforeId = getVisibleEpicDemandCluster(nextAnchorDemand)[0]?.id ?? null
  }

  if (previousAnchorRow?.dataset.anchorDemandId) {
    const previousAnchorDemand = visibleListRows.value.find(demand => demand.id === previousAnchorRow.dataset.anchorDemandId)
    const previousCluster = getVisibleEpicDemandCluster(previousAnchorDemand)
    afterId = previousCluster[previousCluster.length - 1]?.id ?? null
  }

  if (!beforeId && !afterId)
    return

  await persistEpicClusterPriority(anchorDemand, beforeId, afterId)
}

function getEpicClusterAnchors(anchorDemand?: RoadmapDemand) {
  if (!anchorDemand)
    return []

  return visibleListRows.value.filter(row => {
    const header = visibleEpicHeaderByDemandId.value[row.id]
    return !!header?.showHeader && isSameDemandGroup(row, anchorDemand)
  })
}

function getEpicClusterMoveState(anchorDemand?: RoadmapDemand) {
  const anchors = getEpicClusterAnchors(anchorDemand)
  const currentIndex = anchorDemand
    ? anchors.findIndex(row => row.id === anchorDemand.id)
    : -1

  return {
    canMoveUp: currentIndex > 0,
    canMoveDown: currentIndex >= 0 && currentIndex < anchors.length - 1
  }
}

async function moveEpicCluster(anchorDemand: RoadmapDemand, direction: 'up' | 'down') {
  const anchors = getEpicClusterAnchors(anchorDemand)
  const currentIndex = anchors.findIndex(row => row.id === anchorDemand.id)
  if (currentIndex < 0)
    return

  const targetAnchor = direction === 'up'
    ? anchors[currentIndex - 1]
    : anchors[currentIndex + 1]

  if (!targetAnchor)
    return

  const currentCluster = getVisibleEpicDemandCluster(anchorDemand)
  const targetCluster = getVisibleEpicDemandCluster(targetAnchor)
  if (!currentCluster.length || !targetCluster.length)
    return

  const scopedDemandIds = getScopedDemandIds(anchorDemand)
  const movedIds = currentCluster.map(demand => demand.id)
  const orderedDemandIds = direction === 'up'
    ? moveDemandCluster(scopedDemandIds, movedIds, targetCluster[0]!.id, null)
    : moveDemandCluster(scopedDemandIds, movedIds, null, targetCluster[targetCluster.length - 1]!.id)

  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null

  try {
    await roadmapStore.reorderDemand(anchorDemand.id, anchorDemand.status, orderedDemandIds)
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

async function handleListSortEnd(item: HTMLElement) {
  const demandId = item.dataset.demandId
  if (!demandId)
    return

  const visibleRows = visibleSortableRows.value
  const oldIndex = visibleRows.findIndex(row => row.id === demandId)
  const demand = oldIndex >= 0 ? visibleRows[oldIndex] : null
  if (!demand) {
    await roadmapStore.fetchDemands()
    return
  }

  const tbody = item.parentElement
  if (!tbody)
    return

  const orderedDemandIds = Array.from(tbody.querySelectorAll('.list-demand-row'))
    .map(row => (row as HTMLTableRowElement).dataset.demandId)
    .filter((value): value is string => !!value)
  const newIndex = orderedDemandIds.indexOf(demand.id)
  if (newIndex < 0 || newIndex === oldIndex)
    return

  const remainingRows = visibleRows.filter((_, index) => index !== oldIndex)

  // Determine target quarter from drop position
  const rowAtNewIndex = remainingRows[newIndex]
  const rowBeforeNewIndex = newIndex > 0 ? remainingRows[newIndex - 1] : null
  const targetQuarterRef = rowAtNewIndex ?? rowBeforeNewIndex

  if (!targetQuarterRef) {
    await roadmapStore.fetchDemands()
    return
  }

  const targetQuarter = { quarterYear: targetQuarterRef.quarterYear, quarterNumber: targetQuarterRef.quarterNumber }
  const quarterChanged = demand.quarterYear !== targetQuarter.quarterYear || demand.quarterNumber !== targetQuarter.quarterNumber

  listSorting.value = []

  if (quarterChanged) {
    const scrollTop = listScrollContainerRef.value?.scrollTop ?? null
    const scrollLeft = listScrollContainerRef.value?.scrollLeft ?? null

    // Find anchors in target quarter among same-group demands
    const sameGroupInTarget = (item: RoadmapDemand) =>
      item.quarterYear === targetQuarter.quarterYear
      && item.quarterNumber === targetQuarter.quarterNumber
      && getDemandGroupKey(item) === getDemandGroupKey(demand)

    const nextScopedRow = remainingRows.slice(newIndex).find(sameGroupInTarget)
    const previousScopedRow = [...remainingRows.slice(0, newIndex)].reverse().find(sameGroupInTarget)
    const beforeId = nextScopedRow?.id ?? null
    const afterId = beforeId ? null : previousScopedRow?.id ?? null

    try {
      const updatedDemand = await roadmapStore.updateDemand(
        demand.id,
        buildDemandFormData(demand, {
          quarterYear: targetQuarter.quarterYear,
          quarterNumber: targetQuarter.quarterNumber,
          status: demand.status
        })
      )

      await persistDemandPriority(updatedDemand, updatedDemand.status, beforeId, afterId)
      await refreshListPresentation(scrollTop, scrollLeft)

      toast.add({
        title: 'Demanda movida de quarter',
        description: `${demand.title} movida para ${quarterShortLabel(`${targetQuarter.quarterNumber}-${targetQuarter.quarterYear}`)}`,
        color: 'success'
      })
    }
    catch {
      // handled by useApi
    }
  }
  else {
    const nextScopedRow = remainingRows.slice(newIndex).find(item => isSameDemandGroup(item, demand))
    const previousScopedRow = [...remainingRows.slice(0, newIndex)].reverse().find(item => isSameDemandGroup(item, demand))
    const beforeId = nextScopedRow?.id ?? null
    const afterId = beforeId ? null : previousScopedRow?.id ?? null

    await persistDemandPriority(demand, demand.status, beforeId, afterId)
  }
}

function destroyListSortable() {
  listBodySortable?.destroy()
  listBodySortable = null
}

function initListSortable() {
  destroyListSortable()

  const tbody = listTableRootRef.value?.querySelector('tbody')
  if (!tbody) return

  syncListSectionDividers()

  listBodySortable = Sortable.create(tbody, {
    animation: 150,
    draggable: '.list-demand-row,.list-epic-divider',
    handle: '.list-priority-handle,.list-epic-priority-handle',
    ghostClass: 'opacity-40',
    forceFallback: true,
    fallbackOnBody: true,
    fallbackTolerance: 4,
    filter: 'a,input,textarea,[role="button"]',
    preventOnFilter: false,
    onMove: (event) => {
      const dragged = event.dragged as HTMLElement | null
      const related = event.related as HTMLElement | null

      if (!related?.dataset.demandId && !related?.dataset.anchorDemandId)
        return false

      const draggedIsEpic = !!dragged?.dataset.anchorDemandId

      if (draggedIsEpic) {
        const draggedAnchor = dragged?.dataset.anchorDemandId
          ? visibleListRows.value.find(demand => demand.id === dragged.dataset.anchorDemandId)
          : null
        const relatedAnchorId = related.dataset.anchorDemandId ?? related.dataset.demandId
        const relatedDemand = relatedAnchorId
          ? visibleListRows.value.find(demand => demand.id === relatedAnchorId)
          : null

        if (!draggedAnchor || !relatedDemand)
          return false

        if (dragged) {
          dragged.dataset.targetQuarterKey = relatedDemand.quarterYear != null && relatedDemand.quarterNumber != null
            ? `${relatedDemand.quarterYear}:${relatedDemand.quarterNumber}`
            : ''
          dragged.dataset.targetDemandId = relatedDemand.id
          dragged.dataset.targetInsertAfter = String(Boolean(event.willInsertAfter))
        }

        return getEpicQuarterMoveScopeKey(draggedAnchor) === getEpicQuarterMoveScopeKey(relatedDemand)
      }

      if (!related.dataset.demandId)
        return false

      const draggedQuarter = dragged?.dataset.quarterKey
      const relatedQuarter = related.dataset.quarterKey

      // Cross-quarter: always allow for demand rows
      if (draggedQuarter !== relatedQuarter)
        return true

      // Same quarter: when grouped, keep demand reordering inside the visible epic group.
      return dragged?.dataset.dragScopeKey === related.dataset.dragScopeKey
    },
    onEnd: async (event) => {
      const draggedItem = event.item as HTMLElement | null
      if (draggedItem?.dataset.anchorDemandId) {
        await handleEpicSortEnd(draggedItem)
        delete draggedItem.dataset.targetQuarterKey
        delete draggedItem.dataset.targetDemandId
        delete draggedItem.dataset.targetInsertAfter
        return
      }

      const oldIndex = event.oldDraggableIndex ?? event.oldIndex
      const newIndex = event.newDraggableIndex ?? event.newIndex

      if (oldIndex == null || newIndex == null || oldIndex === newIndex)
        return

      if (draggedItem)
        handleListSortEnd(draggedItem)
    }
  })
}

function syncListSectionDividers() {
  const tbody = listTableRootRef.value?.querySelector('tbody')
  if (!tbody) return

  const table = tbody.closest('table')
  const headerTable = listHeaderRowRef.value?.closest('table')

  const syncColgroup = (targetTable: HTMLTableElement | null) => {
    if (!targetTable)
      return

    let colgroup = targetTable.querySelector('colgroup')
    if (!colgroup) {
      colgroup = document.createElement('colgroup')
      targetTable.insertBefore(colgroup, targetTable.firstChild)
    }

    colgroup.innerHTML = ''
    for (const col of listOrderedCols.value) {
      const colEl = document.createElement('col')
      colEl.style.width = listColWidth(col.id, col.defaultWidth)
      colgroup.appendChild(colEl)
    }
  }

  syncColgroup(table)
  syncColgroup(headerTable as HTMLTableElement | null)

  tbody.querySelectorAll('.list-section-divider').forEach(node => node.remove())

  const rows = Array.from(tbody.querySelectorAll('tr')) as HTMLTableRowElement[]
  const visibleRows = listTableRef.value?.tableApi?.getSortedRowModel().rows.map(row => row.original) ?? tableDemands.value

  rows.forEach((row, index) => {
    const demand = visibleRows[index]
    if (!demand) {
      row.classList.remove('list-demand-row')
      row.classList.remove('bg-elevated/5', 'hover:bg-elevated/15')
      row.classList.remove('bg-red-50/70', 'dark:bg-red-950/20')
      row.style.display = ''
      row.hidden = false
      delete row.dataset.demandId
      delete row.dataset.scopeKey
      delete row.dataset.quarterKey
      return
    }

    const isCollapsedRow = groupDemandsByEpic.value && !!demand.epicId && collapsedEpicIds.value.includes(demand.epicId)
    row.dataset.demandId = demand.id
    row.dataset.dragScopeKey = getDemandDragScopeKey(demand)
    if (isCollapsedRow)
      row.style.setProperty('display', 'none', 'important')
    else
      row.style.removeProperty('display')
    row.hidden = isCollapsedRow
    row.classList.toggle('hidden', isCollapsedRow)

    if (isCollapsedRow) {
      row.classList.remove('list-demand-row')
      delete row.dataset.scopeKey
      delete row.dataset.quarterKey
    }
    else {
      row.classList.add('list-demand-row')
      row.dataset.scopeKey = getDemandScopeKey(demand)
      row.dataset.quarterKey = `${demand.quarterYear}:${demand.quarterNumber}`
    }

    const inconsistent = hasInconsistentDependency(demand)
    const isGroupedDemandRow = groupDemandsByEpic.value && !!demand.epicId
    row.classList.toggle('bg-elevated/5', isGroupedDemandRow && !inconsistent)
    row.classList.toggle('hover:bg-elevated/15', isGroupedDemandRow && !inconsistent)
    row.classList.toggle('bg-red-50/70', inconsistent)
    row.classList.toggle('dark:bg-red-950/20', inconsistent)

    for (const cell of Array.from(row.children) as HTMLTableCellElement[]) {
      cell.classList.toggle('bg-elevated/5', isGroupedDemandRow && !inconsistent)
      cell.classList.toggle('hover:bg-elevated/15', isGroupedDemandRow && !inconsistent)
      cell.classList.toggle('bg-red-50/70', inconsistent)
      cell.classList.toggle('dark:bg-red-950/20', inconsistent)
    }
  })

  const distinctQuarters = new Set(visibleRows.map(d => `${d.quarterYear}:${d.quarterNumber}`))
  const multipleQuarters = distinctQuarters.size > 1

  const dividerConfigs: Array<
    { rowIndex: number, label: string, kind: 'quarter' | 'additional' }
    | { rowIndex: number, kind: 'epic', count: number, epicId?: string, roadmapTitle?: string | null, epicTitle?: string | null, collapsed: boolean }
  > = []
  let prevQuarterKey = ''

  for (let i = 0; i < visibleRows.length; i++) {
    const demand = visibleRows[i]!
    const quarterKey = `${demand.quarterYear}:${demand.quarterNumber}`
    const groupKey = getDemandGroupKey(demand)
    const isNewQuarter = quarterKey !== prevQuarterKey
    const epicHeader = visibleEpicHeaderByDemandId.value[demand.id]

    if (isNewQuarter) {
      prevQuarterKey = quarterKey

      if (multipleQuarters) {
        const label = isBacklogDemand(demand) ? 'Backlog' : demand.quarterLabel
        dividerConfigs.push({ rowIndex: i, label, kind: 'quarter' })
      }
    }

    if (groupKey === 'additional') {
      const isFirstAdditional = !visibleRows.slice(0, i).some(d =>
        `${d.quarterYear}:${d.quarterNumber}` === quarterKey && getDemandGroupKey(d) === 'additional'
      )
      if (isFirstAdditional) {
        const hasRegularInQuarter = visibleRows.some(d =>
          `${d.quarterYear}:${d.quarterNumber}` === quarterKey && getDemandGroupKey(d) === 'regular'
        )
        if (hasRegularInQuarter) {
          const quarterLabel = isBacklogDemand(demand) ? 'Backlog' : demand.quarterLabel
          const label = multipleQuarters
            ? `${quarterLabel} — Adicionais - Não comprometidas`
            : 'Adicionais - Não comprometidas'
          dividerConfigs.push({ rowIndex: i, label, kind: 'additional' })
        }
      }
    }

    if (groupDemandsByEpic.value && epicHeader?.showHeader) {
      dividerConfigs.push({
        rowIndex: i,
        kind: 'epic',
        count: epicHeader.count,
        epicId: epicHeader.epicId,
        roadmapTitle: epicHeader.roadmapTitle,
        epicTitle: epicHeader.epicTitle,
        collapsed: epicHeader.collapsed
      })
    }
  }

  dividerConfigs.forEach((config) => {
    const { kind } = config
    const targetRow = rows[config.rowIndex]
    if (!targetRow) return

    const dividerRow = document.createElement('tr')
    dividerRow.className = 'list-section-divider'

    const dividerCell = document.createElement('td')
    dividerCell.colSpan = listOrderedCols.value.length

    if (kind === 'quarter') {
      dividerCell.className = 'border-y-2 border-primary/25 bg-primary/10 px-3 py-2.5 text-center text-[11px] font-semibold uppercase tracking-[0.14em] text-primary shadow-sm'
      dividerCell.textContent = config.label
    }
    else if (kind === 'additional') {
      dividerCell.className = 'border-y border-amber-200/50 bg-amber-50/40 px-3 py-1.5 text-center text-[11px] font-semibold uppercase tracking-[0.12em] text-amber-700/90 dark:border-amber-800/50 dark:bg-amber-900/10 dark:text-amber-300/90'
      dividerCell.textContent = config.label
    }
    else {
      const headerMeta = getEpicHeaderMeta(visibleRows[config.rowIndex])
      const anchorDemand = visibleRows[config.rowIndex]
      dividerRow.className = 'list-section-divider list-epic-divider border-y border-default bg-default'
      dividerRow.dataset.anchorDemandId = anchorDemand?.id ?? ''
      dividerRow.dataset.scopeKey = anchorDemand ? getDemandScopeKey(anchorDemand) : ''
      dividerRow.dataset.dragScopeKey = anchorDemand ? getDemandDragScopeKey(anchorDemand) : ''
      dividerRow.dataset.quarterKey = anchorDemand ? `${anchorDemand.quarterYear}:${anchorDemand.quarterNumber}` : ''

      const fullRowCell = document.createElement('td')
      fullRowCell.colSpan = listOrderedCols.value.length
      fullRowCell.className = 'p-0'

      const grid = document.createElement('div')
      grid.className = 'grid items-start bg-default'
      grid.style.gridTemplateColumns = getListGridTemplateColumns()

      const createGridCell = (className = 'px-4 py-2.5 align-top') => {
        const cell = document.createElement('div')
        cell.className = className
        return cell
      }

      for (const column of listOrderedCols.value) {
        if (column.id === 'priority') {
          const cell = createGridCell('px-2 py-2.5 align-top')

          if (headerMeta) {
            const handleWrap = document.createElement('div')
            handleWrap.className = 'flex items-start justify-center gap-1.5'

            const dragHandle = document.createElement('span')
            dragHandle.className = 'list-epic-priority-handle inline-flex h-6 w-6 items-center justify-center rounded-md border border-default bg-elevated text-muted transition-colors hover:border-primary/40 hover:text-highlighted cursor-grab active:cursor-grabbing'
            dragHandle.title = 'Arrastar para repriorizar o épico'

            const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg')
            svg.setAttribute('viewBox', '0 0 24 24')
            svg.setAttribute('fill', 'none')
            svg.setAttribute('stroke', 'currentColor')
            svg.setAttribute('stroke-width', '2')
            svg.setAttribute('stroke-linecap', 'round')
            svg.setAttribute('stroke-linejoin', 'round')
            svg.setAttribute('class', 'h-3.5 w-3.5')

            for (const [cx, cy] of [['9', '6'], ['9', '12'], ['9', '18'], ['15', '6'], ['15', '12'], ['15', '18']] as const) {
              const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle')
              circle.setAttribute('cx', cx)
              circle.setAttribute('cy', cy)
              circle.setAttribute('r', '1')
              svg.appendChild(circle)
            }

            dragHandle.appendChild(svg)
            handleWrap.appendChild(dragHandle)

            if (config.epicId) {
              const checkbox = document.createElement('input')
              checkbox.type = 'checkbox'
              checkbox.className = 'mt-1 h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary'
              checkbox.checked = isEpicSelected(config.epicId)
              checkbox.addEventListener('click', event => event.stopPropagation())
              checkbox.addEventListener('change', (event) => {
                toggleEpicSelection(config.epicId!, (event.target as HTMLInputElement).checked)
              })
              handleWrap.appendChild(checkbox)
            }

            cell.appendChild(handleWrap)
          }

          grid.appendChild(cell)
          continue
        }

        if (column.id === 'quarterLabel') {
          const cell = createGridCell('px-4 py-2.5 align-top')

          if (anchorDemand) {
            const quarterNode = anchorDemand.quarterYear === 0 && anchorDemand.quarterNumber === 0
              ? document.createElement('span')
              : document.createElement('span')

            quarterNode.className = anchorDemand.quarterYear === 0 && anchorDemand.quarterNumber === 0
              ? 'inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-highlighted'
              : 'inline-flex items-center rounded-md border border-default bg-default px-2 py-0.5 text-[10px] font-mono text-highlighted'
            quarterNode.textContent = anchorDemand.quarterLabel || 'Backlog'

            const typeNode = document.createElement('span')
            typeNode.className = 'text-[11px] text-muted'
            typeNode.textContent = typeLabels[anchorDemand.type]

            const wrap = document.createElement('div')
            wrap.className = 'flex min-w-0 flex-col gap-0.5'
            wrap.append(quarterNode, typeNode)
            cell.appendChild(wrap)
          }

          grid.appendChild(cell)
          continue
        }

        if (column.id === 'title') {
          const cell = createGridCell('px-4 py-2.5 align-top')
          const titleWrap = document.createElement('div')
          titleWrap.className = 'flex min-w-0 items-start gap-1.5'

          if (config.epicId) {
            const toggleButton = document.createElement('button')
            toggleButton.type = 'button'
            toggleButton.className = 'mt-0.5 inline-flex h-4 w-4 shrink-0 items-center justify-center rounded border border-default bg-default text-muted transition-colors hover:text-highlighted'
            toggleButton.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              toggleEpicCollapse(config.epicId)
            })

            const toggleIcon = document.createElement('span')
            toggleIcon.textContent = config.collapsed ? '▸' : '▾'
            toggleButton.appendChild(toggleIcon)
            titleWrap.appendChild(toggleButton)
          }

          const epicIcon = document.createElement('span')
          epicIcon.className = 'mt-0.5 inline-flex h-3.5 w-3.5 shrink-0 items-center justify-center text-amber-500'
          epicIcon.textContent = '★'

          const scopeWrap = document.createElement('div')
          scopeWrap.className = 'min-w-0 flex-1'

          const metaRow = document.createElement('div')
          metaRow.className = 'flex flex-wrap items-center gap-1.5'

          const epicLabel = document.createElement('span')
          epicLabel.className = 'inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-muted'
          epicLabel.textContent = 'Épico'
          metaRow.appendChild(epicLabel)

          const countNode = document.createElement('span')
          countNode.className = 'inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[9px] font-medium text-muted'
          countNode.textContent = `${config.count} dem.`
          metaRow.appendChild(countNode)

          if (headerMeta) {
            const classificationBadge = document.createElement('span')
            classificationBadge.className = `inline-flex items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium ${classificationBadgeClass[headerMeta.epic.classification]}`
            classificationBadge.textContent = classificationLabels[headerMeta.epic.classification]
            metaRow.appendChild(classificationBadge)

            headerMeta.issueLinks.forEach((issueLink) => {
              const jiraTag = issueLink.url ? document.createElement('a') : document.createElement('span')
              jiraTag.className = 'inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] font-medium text-primary transition-colors hover:border-primary/40'
              jiraTag.textContent = issueLink.key

              if (jiraTag instanceof HTMLAnchorElement) {
                jiraTag.href = issueLink.url
                jiraTag.target = '_blank'
                jiraTag.rel = 'noopener noreferrer'
              }

              metaRow.appendChild(jiraTag)
            })
          }

          const titleBlock = document.createElement('div')
          titleBlock.className = 'mt-0.5 min-w-0'

          const roadmapTitleLabel = headerMeta?.epic.roadmapTitle ?? config.roadmapTitle
          if (roadmapTitleLabel) {
            const roadmapLabel = document.createElement('div')
            roadmapLabel.className = 'truncate text-[11px] text-muted'
            roadmapLabel.textContent = roadmapTitleLabel
            titleBlock.appendChild(roadmapLabel)
          }

          const epicTitle = document.createElement('div')
          epicTitle.className = 'truncate text-[13px] font-medium text-highlighted'
          epicTitle.textContent = headerMeta?.epic.title ?? config.epicTitle ?? 'Sem épico'
          if (headerMeta?.epic.description?.trim())
            epicTitle.title = headerMeta.epic.description.trim()
          titleBlock.appendChild(epicTitle)

          if (headerMeta) {
            appendEpicDependencyRow(titleBlock, headerMeta.epic, headerMeta.epic.dependsOn, 'dependsOn')
            appendEpicDependencyRow(titleBlock, headerMeta.epic, headerMeta.epic.dependedOnBy, 'dependedOnBy')
          }

          scopeWrap.append(metaRow, titleBlock)
          titleWrap.append(epicIcon, scopeWrap)
          cell.appendChild(titleWrap)
          grid.appendChild(cell)
          continue
        }

        if (column.id === 'kpis') {
          const cell = createGridCell('pl-3 pr-2 py-2.5 align-top')

          if (headerMeta) {
            const container = document.createElement('div')
            container.className = 'flex min-w-0 flex-col items-start gap-1'

            const button = document.createElement('button')
            button.type = 'button'
            button.className = `inline-flex h-6 max-w-full items-center rounded-md border px-1.5 text-[10px] font-medium transition-colors hover:opacity-80 ${headerMeta.kpiSummary.tone}`
            button.textContent = headerMeta.kpiSummary.label
            button.title = headerMeta.kpiSummary.actionLabel
            button.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              openDemandKpiWorkspace(headerMeta.epic)
            })
            container.appendChild(button)

            if (headerMeta.epic.hasNoKpi && headerMeta.epic.noKpiClassification) {
              const note = document.createElement('span')
              note.className = 'text-[11px] text-muted'
              note.textContent = getNoKpiClassificationLabel(headerMeta.epic.noKpiClassification)
              container.appendChild(note)
            }

            cell.appendChild(container)
          }

          grid.appendChild(cell)
          continue
        }

        if (column.id === 'products') {
          const cell = createGridCell('px-4 py-2.5 align-top')
          if (headerMeta?.products.length) {
            const productsWrap = document.createElement('div')
            productsWrap.className = 'flex flex-wrap gap-1'

            headerMeta.products.slice(0, 2).forEach((product) => {
              const badge = document.createElement('span')
              badge.className = 'inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[10px] text-highlighted'
              badge.textContent = product
              productsWrap.appendChild(badge)
            })

            if (headerMeta.products.length > 2) {
              const more = document.createElement('span')
              more.className = 'text-[11px] text-muted'
              more.textContent = `+${headerMeta.products.length - 2}`
              productsWrap.appendChild(more)
            }

            cell.appendChild(productsWrap)
          }
          else {
            const empty = document.createElement('span')
            empty.className = 'text-xs text-muted'
            empty.textContent = '—'
            cell.appendChild(empty)
          }
          grid.appendChild(cell)
          continue
        }

        if (column.id === 'hours') {
          const cell = createGridCell('px-4 py-2.5 text-right align-top')
          if (headerMeta) {
            const text = document.createElement('div')
            text.className = 'inline-flex items-center rounded-md border border-default bg-default px-2 py-0.5 text-[10px] font-semibold text-highlighted'
            text.textContent = `${headerMeta.totalHours.toLocaleString('pt-BR')}h`
            cell.appendChild(text)
          }
          grid.appendChild(cell)
          continue
        }

        if (column.id === 'status') {
          const cell = createGridCell('px-4 py-2.5 align-top')
          if (headerMeta) {
            const container = document.createElement('div')
            container.className = 'flex flex-col gap-1'
            container.title = getDemandNotesTooltip(headerMeta.epic) || statusLabels[headerMeta.epic.status]

            const statusRow = document.createElement('div')
            statusRow.className = 'flex items-center gap-1.5'

            const badge = document.createElement('span')
            badge.className = `inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium ${getStatusBadgeClass(headerMeta.epic.status)}`
            badge.textContent = statusLabels[headerMeta.epic.status]
            statusRow.appendChild(badge)

            if (headerMeta.epic.isBlocked) {
              const blockedBadge = document.createElement('span')
              blockedBadge.className = 'inline-flex items-center justify-center rounded-full border border-red-200 bg-red-50 px-1.5 py-0.5 text-[11px] font-medium text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300'
              blockedBadge.appendChild(createSvgIcon(['M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z', 'M12 9v4', 'M12 17h.01'], 'h-3 w-3'))
              statusRow.appendChild(blockedBadge)
            }
            else if (getDemandNotesTooltip(headerMeta.epic)) {
              const noteBadge = document.createElement('span')
              noteBadge.className = 'inline-flex items-center justify-center rounded-full border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[11px] font-medium text-amber-700 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300'
              noteBadge.appendChild(createSvgIcon(['M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z'], 'h-3 w-3'))
              statusRow.appendChild(noteBadge)
            }

            container.appendChild(statusRow)

            const promisedDate = getDisplayedConclusionDate(headerMeta.epic)
            if (promisedDate) {
              const promisedRow = document.createElement('div')
              promisedRow.className = 'flex items-center gap-1 text-[11px] text-muted'
              promisedRow.appendChild(createSvgIcon(['M8 2v4', 'M16 2v4', 'M3 10h18', 'M8 14h.01', 'M12 14h.01', 'M16 14h.01', 'M8 18h.01', 'M12 18h.01', 'M16 18h.01', 'M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2'], 'h-3 w-3'))
              const promisedText = document.createElement('span')
              promisedText.textContent = formatDemandDate(promisedDate)
              promisedRow.appendChild(promisedText)
              container.appendChild(promisedRow)
            }

            if (showDemandDelayMarker(headerMeta.epic)) {
              const delayRow = document.createElement('div')
              delayRow.className = 'flex items-center gap-1 text-[11px] font-medium text-amber-600 dark:text-amber-400'
              delayRow.appendChild(createSvgIcon(['M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z', 'M12 9v4', 'M12 17h.01'], 'h-3 w-3'))
              const delayText = document.createElement('span')
              delayText.textContent = 'Atrasado'
              delayRow.appendChild(delayText)
              container.appendChild(delayRow)
            }

            cell.appendChild(container)
          }
          grid.appendChild(cell)
          continue
        }

        if (column.id === 'customers') {
          const cell = createGridCell('px-4 py-2.5 align-top')
          if (headerMeta?.customers.length) {
            const customersWrap = document.createElement('div')
            customersWrap.className = 'flex flex-wrap gap-1'

            headerMeta.customers.slice(0, 2).forEach((customer) => {
              const badge = document.createElement('span')
              badge.className = 'inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[10px] text-highlighted'
              badge.textContent = customer
              customersWrap.appendChild(badge)
            })

            if (headerMeta.customers.length > 2) {
              const more = document.createElement('span')
              more.className = 'text-[11px] text-muted'
              more.textContent = `+${headerMeta.customers.length - 2}`
              customersWrap.appendChild(more)
            }

            cell.appendChild(customersWrap)
          }
          else {
            const empty = document.createElement('span')
            empty.className = 'text-xs text-muted'
            empty.textContent = '—'
            cell.appendChild(empty)
          }
          grid.appendChild(cell)
          continue
        }

        if (column.id === 'classification') {
          const cell = createGridCell('px-4 py-2.5 align-top')
          if (headerMeta) {
            const classificationBadge = document.createElement('span')
            classificationBadge.className = `inline-flex items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium ${classificationBadgeClass[headerMeta.epic.classification]}`
            classificationBadge.textContent = classificationLabels[headerMeta.epic.classification]
            cell.appendChild(classificationBadge)
          }
          grid.appendChild(cell)
          continue
        }

        if (column.id === '_actions') {
          const cell = createGridCell('px-2 py-2.5 text-right align-top')
          if (headerMeta) {
            const actions = document.createElement('div')
            actions.className = 'ml-auto flex items-center justify-end gap-1'

            const createButton = document.createElement('button')
            createButton.type = 'button'
            createButton.className = 'inline-flex h-6 w-6 items-center justify-center rounded-md border border-primary/30 bg-primary/10 text-sm font-semibold leading-none text-primary transition-colors hover:border-primary/50 hover:bg-primary/15'
            createButton.title = 'Nova demanda neste épico'
            createButton.textContent = '+'
            createButton.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              openCreateModal('Demand', headerMeta.epic.id, { projectId: headerMeta.epic.projectId })
            })
            actions.appendChild(createButton)

            const kpiButton = document.createElement('button')
            kpiButton.type = 'button'
            kpiButton.className = 'inline-flex h-6 w-6 items-center justify-center rounded-md text-primary transition-colors hover:text-primary/80'
            kpiButton.title = 'Abrir KPIs do épico'
            kpiButton.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              openDemandKpiWorkspace(headerMeta.epic)
            })

            const kpiSvg = document.createElementNS('http://www.w3.org/2000/svg', 'svg')
            kpiSvg.setAttribute('viewBox', '0 0 24 24')
            kpiSvg.setAttribute('fill', 'none')
            kpiSvg.setAttribute('stroke', 'currentColor')
            kpiSvg.setAttribute('stroke-width', '2')
            kpiSvg.setAttribute('stroke-linecap', 'round')
            kpiSvg.setAttribute('stroke-linejoin', 'round')
            kpiSvg.setAttribute('class', 'h-3.5 w-3.5')

            const kpiPath1 = document.createElementNS('http://www.w3.org/2000/svg', 'path')
            kpiPath1.setAttribute('d', 'M3 3v18h18')
            const kpiPath2 = document.createElementNS('http://www.w3.org/2000/svg', 'path')
            kpiPath2.setAttribute('d', 'm19 9-5 5-4-4-3 3')

            kpiSvg.append(kpiPath1, kpiPath2)
            kpiButton.appendChild(kpiSvg)
            actions.appendChild(kpiButton)

            const editButton = document.createElement('button')
            editButton.type = 'button'
            editButton.className = 'inline-flex h-6 w-6 items-center justify-center rounded-md text-muted transition-colors hover:text-highlighted'
            editButton.title = 'Editar épico'
            editButton.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              openEditModal(headerMeta.epic)
            })

            const editSvg = document.createElementNS('http://www.w3.org/2000/svg', 'svg')
            editSvg.setAttribute('viewBox', '0 0 24 24')
            editSvg.setAttribute('fill', 'none')
            editSvg.setAttribute('stroke', 'currentColor')
            editSvg.setAttribute('stroke-width', '2')
            editSvg.setAttribute('stroke-linecap', 'round')
            editSvg.setAttribute('stroke-linejoin', 'round')
            editSvg.setAttribute('class', 'h-3.5 w-3.5')

            const editPathBody = document.createElementNS('http://www.w3.org/2000/svg', 'path')
            editPathBody.setAttribute('d', 'M12 20h9')

            const editPathTip = document.createElementNS('http://www.w3.org/2000/svg', 'path')
            editPathTip.setAttribute('d', 'M16.5 3.5a2.12 2.12 0 1 1 3 3L7 19l-4 1 1-4Z')

            editSvg.append(editPathBody, editPathTip)
            editButton.appendChild(editSvg)
            actions.appendChild(editButton)

            const deleteButton = document.createElement('button')
            deleteButton.type = 'button'
            deleteButton.className = 'inline-flex h-6 w-6 items-center justify-center rounded-md text-error transition-colors hover:text-error/80'
            deleteButton.title = 'Excluir épico'
            deleteButton.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              promptDelete(headerMeta.epic.id)
            })

            const deleteSvg = document.createElementNS('http://www.w3.org/2000/svg', 'svg')
            deleteSvg.setAttribute('viewBox', '0 0 24 24')
            deleteSvg.setAttribute('fill', 'none')
            deleteSvg.setAttribute('stroke', 'currentColor')
            deleteSvg.setAttribute('stroke-width', '2')
            deleteSvg.setAttribute('stroke-linecap', 'round')
            deleteSvg.setAttribute('stroke-linejoin', 'round')
            deleteSvg.setAttribute('class', 'h-3.5 w-3.5')

            const deletePathTop = document.createElementNS('http://www.w3.org/2000/svg', 'path')
            deletePathTop.setAttribute('d', 'M3 6h18')

            const deletePathBin = document.createElementNS('http://www.w3.org/2000/svg', 'path')
            deletePathBin.setAttribute('d', 'M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6')

            const deletePathLid = document.createElementNS('http://www.w3.org/2000/svg', 'path')
            deletePathLid.setAttribute('d', 'M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2')

            const deletePathLeft = document.createElementNS('http://www.w3.org/2000/svg', 'path')
            deletePathLeft.setAttribute('d', 'M10 11v6')

            const deletePathRight = document.createElementNS('http://www.w3.org/2000/svg', 'path')
            deletePathRight.setAttribute('d', 'M14 11v6')

            deleteSvg.append(deletePathTop, deletePathBin, deletePathLid, deletePathLeft, deletePathRight)
            deleteButton.appendChild(deleteSvg)
            actions.appendChild(deleteButton)

            cell.appendChild(actions)
          }
          grid.appendChild(cell)
          continue
        }

        grid.appendChild(createGridCell(column.alignRight ? 'px-3 py-2 text-right align-top' : 'px-3 py-2 align-top'))
      }

      fullRowCell.appendChild(grid)
      dividerRow.appendChild(fullRowCell)

      tbody.insertBefore(dividerRow, targetRow)
      return
    }

    dividerRow.appendChild(dividerCell)
    tbody.insertBefore(dividerRow, targetRow)
  })
}

watch(
  () => `${viewMode.value}|${quarterFilteredDemands.value.map(demand => `${demand.id}:${demand.parentDemandId ?? 'none'}:${demand.epicId ?? 'none'}:${demand.roadmapId ?? 'none'}:${demand.title}:${demand.quarterYear}:${demand.quarterNumber}:${demand.status}:${demand.sortOrder}:${demand.updatedAt ?? ''}`).join('|')}|${JSON.stringify(listSorting.value)}|${JSON.stringify(listColumnFilters.value)}|${groupDemandsByEpic.value}|${collapsedEpicIds.value.join('|')}`,
  async () => {
    await nextTick()
    syncListSectionDividers()
    initListSortable()
  },
  { flush: 'post' }
)

watch(listScrollContainerRef, async (element) => {
  listWidthObserver?.disconnect()
  listWidthObserver = null

  if (!element || typeof ResizeObserver === 'undefined') {
    updateListViewportWidth()
    syncListHeaderScroll()
    await nextTick()
    syncListSectionDividers()
    return
  }

  updateListViewportWidth()
  syncListHeaderScroll()
  await nextTick()
  syncListSectionDividers()

  listWidthObserver = new ResizeObserver(() => {
    updateListViewportWidth()
    requestAnimationFrame(() => syncListSectionDividers())
  })
  listWidthObserver.observe(element)
}, { flush: 'post' })

watch(
  () => listTitleExtraWidth.value,
  async () => {
    await nextTick()
    syncListSectionDividers()
  },
  { flush: 'post' }
)

watch(
  () => `${JSON.stringify(listColumnSizing.value)}|${listOrderedCols.value.map(col => col.id).join('|')}`,
  async () => {
    await nextTick()
    syncListSectionDividers()
  },
  { flush: 'post' }
)

// ─── Modal ────────────────────────────────────────────────────────────────────
const modalOpen = ref(false)
const capacityModalOpen = ref(false)
const isSavingCapacity = ref(false)
const isSavingDemand = ref(false)
const editingDemand = ref<RoadmapDemand | null>(null)
const createItemType = ref<RoadmapItemType | undefined>()
const defaultParentDemandId = ref<string | undefined>()
const defaultProjectId = ref<string | undefined>()
const defaultProjectIds = ref<string[]>([])
const deleteId = ref<string | null>(null)
const confirmDeleteOpen = ref(false)
const roadmapParentOptions = computed(() =>
  roadmapItems.value.map(item => ({
    id: item.id,
    title: item.title,
    projectId: item.projectId,
    projectIds: item.projectIds
  }))
)
const epicParentOptions = computed(() =>
  epicItems.value.map(item => ({
    id: item.id,
    title: item.title,
    roadmapTitle: item.roadmapTitle,
    status: item.status,
    projectId: item.projectId,
    projectIds: item.projectIds
  }))
)
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

function openListView() {
  groupDemandsByEpic.value = true
  collapsedEpicIds.value = [...visibleEpicIds.value]
  hasInitializedCollapsedEpicIds.value = true

  navigateTo({
    path: '/roadmap',
    query: selectedProjectId.value ? { projectId: selectedProjectId.value } : undefined
  })
}

function openHierarchyView() {
  navigateTo({
    path: '/roadmap',
    query: {
      ...(selectedProjectId.value ? { projectId: selectedProjectId.value } : {}),
      view: 'hierarchy'
    }
  })
}

function openCapacityModal() {
  if (!activeCapacityScope.value) return
  capacityModalOpen.value = true
}

function openEditModal(demand: RoadmapDemand, nextStatus?: DemandStatus) {
  editingDemand.value = nextStatus ? { ...demand, status: nextStatus } : demand
  defaultParentDemandId.value = undefined
  defaultProjectId.value = undefined
  defaultProjectIds.value = []
  modalOpen.value = true
}

function promptDelete(id: string) {
  const targetDemand = demands.value.find(demand => demand.id === id)
  if (targetDemand?.itemType === 'Roadmap' && demands.value.some(demand => demand.parentDemandId === id)) {
    toast.add({
      title: 'Exclusão não permitida',
      description: 'Este roadmap possui épicos vinculados e não pode ser removido.',
      color: 'warning'
    })
    return
  }

  if (targetDemand?.itemType === 'Epic' && demands.value.some(demand => demand.parentDemandId === id)) {
    toast.add({
      title: 'Exclusão não permitida',
      description: 'Este épico possui demandas vinculadas e não pode ser removido.',
      color: 'warning'
    })
    return
  }

  deleteId.value = id
  confirmDeleteOpen.value = true
}

function buildDemandFormData(demand: RoadmapDemand, overrides?: Partial<DemandFormData>): DemandFormData {
  const baseQuarterYear = overrides?.quarterYear ?? demand.quarterYear
  const baseQuarterNumber = overrides?.quarterNumber ?? demand.quarterNumber
  const isBacklogDemand = demand.itemType === 'Demand' && baseQuarterYear === 0 && baseQuarterNumber === 0

  return {
    itemType: demand.itemType,
    parentDemandId: demand.parentDemandId,
    title: demand.title,
    description: demand.description ?? '',
    projectId: demand.projectId,
    quarterYear: demand.quarterYear,
    quarterNumber: demand.quarterNumber,
    type: demand.type,
    classification: demand.classification,
    productIds: demand.products.map(product => product.productId),
    projectIds: demand.projectIds ?? (demand.projectId ? [demand.projectId] : []),
    status: demand.status,
    observation: demand.observation ?? '',
    deprioritizationReason: demand.deprioritizationReason ?? undefined,
    replacementDemandId: demand.replacementDemandId ?? undefined,
    jiraIssue: demand.jiraIssue ?? '',
    issueLinks: getDisplayIssueLinks(demand).filter((issue): issue is { key: string, url: string } => !!issue.url).map(issue => ({ key: issue.key, url: issue.url })),
    hours: demand.hours,
    promisedDate: isBacklogDemand ? '' : (demand.promisedDate ?? ''),
    customers: demand.itemType === 'Demand' ? [] : (demand.customers ?? []),
    dependencyDemandIds: demand.dependsOn.map(item => item.demandId),
    isBlocked: demand.isBlocked,
    blockedReason: demand.blockedReason ?? '',
    deliveryDate: demand.deliveryDate ?? '',
    problemClarity: demand.itemType === 'Epic' ? demand.problemClarity ?? undefined : undefined,
    hasNoKpi: demand.hasNoKpi,
    noKpiClassification: demand.noKpiClassification ?? undefined,
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

async function planEpicDemandsToQuarter(
  epicId: string,
  quarterValue: string,
  placement?: { anchorDemandId?: string, beforeId: string | null, afterId: string | null }
) {
  const visibleEpicDemands = getVisibleEpicDemands(epicId)
  if (!visibleEpicDemands.length || isBulkPlanning.value)
    return

  const [quarterNumber, quarterYear] = quarterValue.split('-').map(Number)
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
  const epicTitle = visibleEpicDemands[0]?.epicTitle ?? 'Épico'
  const targetDemandIds = new Set(visibleEpicDemands.map(demand => demand.id))

  try {
    isBulkPlanning.value = true

    let updatedAnchor: RoadmapDemand | null = null

    for (const demand of visibleEpicDemands) {
      const updatedDemand = await roadmapStore.updateDemand(
        demand.id,
        buildDemandFormData(demand, {
          quarterYear,
          quarterNumber,
          status: demand.status
        })
      )

      if (placement?.anchorDemandId === demand.id)
        updatedAnchor = updatedDemand
      else if (!updatedAnchor)
        updatedAnchor = updatedDemand
    }

    if (updatedAnchor && (placement?.beforeId || placement?.afterId)) {
      const scopedDemandIds = getScopedDemandIds(updatedAnchor)
      const movedIds = scopedDemandIds.filter(id => targetDemandIds.has(id))
      const orderedDemandIds = moveDemandCluster(scopedDemandIds, movedIds, placement.beforeId, placement.afterId)
      await roadmapStore.reorderDemand(updatedAnchor.id, updatedAnchor.status, orderedDemandIds)
    }

    await refreshListPresentation(listScrollTop, listScrollLeft)

    toast.add({
      title: 'Épico planejado no quarter',
      description: `${epicTitle} movido para ${quarterShortLabel(quarterValue)} com ${visibleEpicDemands.length.toLocaleString('pt-BR')} demandas visíveis`,
      color: 'success'
    })
  }
  catch {
    // error handled by useApi
  }
  finally {
    isBulkPlanning.value = false
  }
}

async function handleSubmit(data: DemandFormData) {
  if (isSavingDemand.value)
    return

  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null

  try {
    isSavingDemand.value = true
    if (editingDemand.value) {
      await roadmapStore.updateDemand(editingDemand.value.id, data)
      toast.add({ title: 'Item atualizado', color: 'success' })
    }
    else {
      await roadmapStore.createDemand(data)
      toast.add({ title: 'Item criado', color: 'success' })
    }
    modalOpen.value = false
    await refreshListPresentation(listScrollTop, listScrollLeft)
  }
  catch {
    // error handled by useApi
  }
  finally {
    isSavingDemand.value = false
  }
}

async function handleCapacitySubmit(data: CapacityFormData) {
  if (!Number.isFinite(data.capacityHours) || data.capacityHours <= 0) {
    toast.add({ title: 'Informe um capacity maior que zero', color: 'warning' })
    return
  }

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
  Backlog: 'Backlog', InProgress: 'Doing', Done: 'Concluído', Deprioritized: 'Despriorizado'
}
const statusTextClass: Record<DemandStatus, string> = {
  Backlog: 'text-muted',
  InProgress: 'text-blue-600 dark:text-blue-400',
  Done: 'text-green-600 dark:text-green-400',
  Deprioritized: 'text-red-600 dark:text-red-400'
}
const statusDotClass: Record<DemandStatus, string> = {
  Backlog: 'bg-neutral-400 dark:bg-neutral-500',
  InProgress: 'bg-blue-500 dark:bg-blue-400',
  Done: 'bg-green-500 dark:bg-green-400',
  Deprioritized: 'bg-red-500 dark:bg-red-400'
}
const typeLabels: Record<DemandType, string> = {
  Planned: 'Planejado', Spillover: 'Transbordo', Unplanned: 'Não Planejado', Additional: 'Adicional'
}
const classificationLabels: Record<DemandClassification, string> = {
  TechnicalDebtSecurity: 'Débito Técnico', Strategic: 'Estratégico', Evolution: 'Evolução',
  ImprovementGap: 'Melhoria/Gap', Mandatory: 'Mandatório', Homologation: 'Homologação', Customizacao: 'Customização'
}

// ─── List view — TanStack table ──────────────────────────────────────────────────────────────────
const listSorting = ref<SortingState>([])
const listColumnFilters = ref<ColumnFiltersState>([])
const listColumnSizing = ref<ColumnSizingState>({})
const listColumnOrder = ref<string[]>([])
const groupDemandsByEpic = ref(true)

const listTableRef = useTemplateRef<{
  tableApi: {
    getFilteredRowModel: () => { rows: { original: RoadmapDemand }[] }
    getSortedRowModel:   () => { rows: { original: RoadmapDemand }[] }
    setColumnOrder:      (updater: string[] | ((old: string[]) => string[])) => void
    getAllLeafColumns:   () => { id: string }[]
  }
}>('listTable')

const collapsedEpicIds = ref<string[]>([])
const hasInitializedCollapsedEpicIds = ref(false)

const visibleEpicIds = computed(() => {
  if (!groupDemandsByEpic.value)
    return []

  return Array.from(new Set(
    quarterFilteredDemands.value
      .map(demand => demand.epicId)
      .filter((value): value is string => !!value)
  ))
})

const tableDemands = computed(() => quarterFilteredDemands.value)

function isCollapsedRepresentative(demand: RoadmapDemand) {
  return groupDemandsByEpic.value && !!demand.epicId && collapsedEpicIds.value.includes(demand.epicId)
}

function toggleEpicCollapse(epicId?: string) {
  if (!epicId)
    return

  if (collapsedEpicIds.value.includes(epicId)) {
    collapsedEpicIds.value = collapsedEpicIds.value.filter(id => id !== epicId)
    return
  }

  collapsedEpicIds.value = [...collapsedEpicIds.value, epicId]
}

function collapseAllEpicGroups() {
  collapsedEpicIds.value = [...visibleEpicIds.value]
}

function expandAllEpicGroups() {
  collapsedEpicIds.value = []
}

const areAllEpicGroupsCollapsed = computed(() => {
  if (!groupDemandsByEpic.value)
    return false

  return visibleEpicIds.value.length > 0 && visibleEpicIds.value.every(epicId => collapsedEpicIds.value.includes(epicId))
})

watch(quarterFilteredDemands, (demands) => {
  const availableEpicIds = new Set(demands.map(demand => demand.epicId).filter((value): value is string => !!value))
  if (!hasInitializedCollapsedEpicIds.value) {
    if (availableEpicIds.size === 0)
      return

    collapsedEpicIds.value = Array.from(availableEpicIds)
    hasInitializedCollapsedEpicIds.value = true
    return
  }

  collapsedEpicIds.value = collapsedEpicIds.value.filter(id => availableEpicIds.has(id))
}, { immediate: true })

const listFilteredCount = computed(() => {
  void listColumnFilters.value
  return listTableRef.value?.tableApi?.getFilteredRowModel().rows.length ?? tableDemands.value.length
})
const visibleListRows = computed(() => {
  void listSorting.value
  void listColumnFilters.value
  return listTableRef.value?.tableApi?.getSortedRowModel().rows.map(row => row.original) ?? tableDemands.value
})
const visibleRoadmapCount = computed(() => new Set(visibleListRows.value.map(demand => demand.roadmapId).filter((value): value is string => !!value)).size)
const visibleEpicCount = computed(() => new Set(visibleListRows.value.map(demand => demand.epicId).filter((value): value is string => !!value)).size)
const visibleDemandCount = computed(() => visibleListRows.value.length)
const visibleSortableRows = computed(() => visibleListRows.value.filter(demand => !isCollapsedRepresentative(demand)))
const visibleListDemandCount = computed(() => {
  return visibleListRows.value.length
})
const visibleEpicHeaderByDemandId = computed(() => {
  const result: Record<string, { showHeader: boolean, count: number, epicId?: string, roadmapTitle?: string | null, epicTitle?: string | null, collapsed: boolean }> = {}

  if (!groupDemandsByEpic.value) {
    for (const demand of visibleListRows.value)
      result[demand.id] = { showHeader: false, count: 0, collapsed: false }

    return result
  }

  for (let index = 0; index < visibleListRows.value.length; index++) {
    const demand = visibleListRows.value[index]!
    const previous = index > 0 ? visibleListRows.value[index - 1]! : null
    const groupKey = getEpicDisplayGroupKey(demand)
    const previousGroupKey = previous ? getEpicDisplayGroupKey(previous) : null
    const showHeader = groupKey !== previousGroupKey

    if (!showHeader) {
      result[demand.id] = { showHeader: false, count: 0, collapsed: false }
      continue
    }

    const count = getVisibleEpicDemandCluster(demand).length

    result[demand.id] = {
      showHeader: true,
      count,
      epicId: demand.epicId,
      roadmapTitle: demand.roadmapTitle,
      epicTitle: demand.epicTitle,
      collapsed: !!demand.epicId && collapsedEpicIds.value.includes(demand.epicId)
    }
  }

  return result
})

const listHasActiveFilters = computed(() => listColumnFilters.value.length > 0)
const shouldConstrainListHeight = computed(() => visibleListDemandCount.value > 20)
const listTableKey = computed(() =>
  tableDemands.value
    .map(demand => `${demand.id}:${demand.parentDemandId ?? 'none'}:${demand.epicId ?? 'none'}:${demand.roadmapId ?? 'none'}:${demand.title}:${demand.quarterYear}:${demand.quarterNumber}:${demand.status}:${demand.sortOrder}:${demand.updatedAt ?? ''}`)
    .join('|') + `::${groupDemandsByEpic.value ? 'grouped' : 'flat'}::${collapsedEpicIds.value.join('|')}`
)
const priorityRankByDemandId = computed(() => {
  const result: Record<string, number> = {}
  const counterByQuarter: Record<string, number> = {}

  for (const demand of tableDemands.value) {
    const quarterKey = `${demand.quarterYear}:${demand.quarterNumber}`
    const counter = (counterByQuarter[quarterKey] ?? 0) + 1
    counterByQuarter[quarterKey] = counter
    result[demand.id] = counter
  }

  return result
})
const selectedDemandIds = ref<string[]>([])
const selectedEpicIds = ref<string[]>([])
const isBulkPlanning = ref(false)
const visibleListDemandIds = computed(() => visibleListRows.value.map(demand => demand.id))
const selectedDemands = computed(() => {
  const selectedIds = new Set(selectedDemandIds.value)
  return demandItems.value.filter(demand => selectedIds.has(demand.id))
})
const selectedEpicDemands = computed(() => {
  if (!groupDemandsByEpic.value || !selectedEpicIds.value.length)
    return []

  const selectedIds = new Set(selectedEpicIds.value)
  return visibleListRows.value.filter(demand => demand.epicId && selectedIds.has(demand.epicId))
})
const selectedPlanningDemands = computed(() => {
  const selectedById = new Map<string, RoadmapDemand>()

  selectedDemands.value.forEach(demand => selectedById.set(demand.id, demand))
  selectedEpicDemands.value.forEach(demand => selectedById.set(demand.id, demand))

  return Array.from(selectedById.values())
})
const selectedDemandCount = computed(() => selectedPlanningDemands.value.length)

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
  { label: 'Doing',  value: 'InProgress' },
  { label: 'Concluído',     value: 'Done' },
  { label: 'Despriorizado', value: 'Deprioritized' },
]
const TYPE_SELECT_OPTIONS = [
  { label: 'Planejado',     value: 'Planned' },
  { label: 'Transbordo',    value: 'Spillover' },
  { label: 'Não Planejado', value: 'Unplanned' },
  { label: 'Adicional',     value: 'Additional' },
]
const classificationBadgeClass: Record<DemandClassification, string> = {
  TechnicalDebtSecurity: 'bg-slate-100 text-slate-700 border-slate-200 dark:bg-slate-800/60 dark:text-slate-300 dark:border-slate-700',
  Strategic: 'bg-indigo-100 text-indigo-700 border-indigo-200 dark:bg-indigo-900/30 dark:text-indigo-300 dark:border-indigo-800',
  Evolution: 'bg-sky-100 text-sky-700 border-sky-200 dark:bg-sky-900/30 dark:text-sky-300 dark:border-sky-800',
  ImprovementGap: 'bg-emerald-100 text-emerald-700 border-emerald-200 dark:bg-emerald-900/30 dark:text-emerald-300 dark:border-emerald-800',
  Mandatory: 'bg-red-100 text-red-700 border-red-200 dark:bg-red-900/30 dark:text-red-300 dark:border-red-800',
  Homologation: 'bg-violet-100 text-violet-700 border-violet-200 dark:bg-violet-900/30 dark:text-violet-300 dark:border-violet-800',
  Customizacao: 'bg-orange-100 text-orange-700 border-orange-200 dark:bg-orange-900/30 dark:text-orange-300 dark:border-orange-800',
}

const LIST_COL_DEFS: ListColMeta[] = [
  { id: 'priority',       label: 'Prioridade',   defaultWidth: 64, disableFilter: true },
  { id: 'title',          label: 'Demanda',       defaultWidth: 360, filterType: 'text' },
  { id: 'quarterLabel',   label: 'Quarter / Tipo', defaultWidth: 96, filterType: 'multi-select', allLabel: 'Todos os quarters', itemLabelPlural: 'quarters' },
  { id: 'kpis',           label: 'KPI',           defaultWidth: 100, disableFilter: true },
  { id: 'products',       label: 'Produtos',      defaultWidth: 148, filterType: 'multi-select', allLabel: 'Todos os produtos', itemLabelPlural: 'produtos', disableSorting: true },
  { id: 'hours',          label: 'Hrs',           defaultWidth: 60, disableFilter: true, alignRight: true },
  { id: 'status',         label: 'Status',        defaultWidth: 124, filterType: 'multi-select', selectOptions: STATUS_SELECT_OPTIONS, allLabel: 'Todos os status', itemLabelPlural: 'status' },
  { id: 'customers',      label: 'Clientes',      defaultWidth: 110, filterType: 'text' },
  { id: '_actions',       label: '',              defaultWidth: 112, disableFilter: true, disableSorting: true, alignRight: true },
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
  const availableIds = new Set(demandItems.value.map(demand => demand.id))
  selectedDemandIds.value = selectedDemandIds.value.filter(id => availableIds.has(id))

  const availableEpicIds = new Set(visibleListRows.value.map(demand => demand.epicId).filter((value): value is string => !!value))
  selectedEpicIds.value = selectedEpicIds.value.filter(id => availableEpicIds.has(id))
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

function isEpicSelected(epicId?: string) {
  return !!epicId && selectedEpicIds.value.includes(epicId)
}

function toggleEpicSelection(epicId: string, selected: boolean) {
  if (selected) {
    if (!selectedEpicIds.value.includes(epicId))
      selectedEpicIds.value = [...selectedEpicIds.value, epicId]

    return
  }

  selectedEpicIds.value = selectedEpicIds.value.filter(id => id !== epicId)
}

function clearSelectedDemands() {
  selectedDemandIds.value = []
  selectedEpicIds.value = []
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
    syncListHeaderScroll()
  }
}

async function planSelectedDemandsToQuarter(quarterValue: string) {
  if (!selectedPlanningDemands.value.length || isBulkPlanning.value)
    return

  const [quarterNumber, quarterYear] = quarterValue.split('-').map(Number)
  const movedCount = selectedPlanningDemands.value.length
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
  isBulkPlanning.value = true

  try {
    for (const demand of selectedPlanningDemands.value) {
      await roadmapStore.updateDemand(
        demand.id,
        buildDemandFormData(demand, {
          quarterYear,
          quarterNumber,
          status: demand.status
        })
      )
    }

    await refreshListPresentation(listScrollTop, listScrollLeft)

    selectedDemandIds.value = []
    selectedEpicIds.value = []
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
  () => demandItems.value.map(demand => `${demand.id}:${demand.quarterYear}:${demand.quarterNumber}`).join('|'),
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
    requestAnimationFrame(() => syncListSectionDividers())
  }
  const onUp = () => { document.removeEventListener('mousemove', onMove); document.removeEventListener('mouseup', onUp) }
  document.addEventListener('mousemove', onMove)
  document.addEventListener('mouseup', onUp)
}
function getBaseListColPixelWidth(colId: string, fallback: number): number {
  return listColumnSizing.value[colId] ?? fallback
}

const listBaseWidth = computed(() => listOrderedCols.value.reduce((total, col) => {
  return total + getBaseListColPixelWidth(col.id, col.defaultWidth)
}, 0))
const listTitleExtraWidth = computed(() => Math.max(0, listViewportWidth.value - listBaseWidth.value))

function listColWidth(colId: string, fallback: number): string {
  const width = getBaseListColPixelWidth(colId, fallback) + (colId === 'title' ? listTitleExtraWidth.value : 0)
  return `${width}px`
}

function getListGridTemplateColumns() {
  return listOrderedCols.value
    .map(col => listColWidth(col.id, col.defaultWidth))
    .join(' ')
}

const listTableWidth = computed(() => `${listBaseWidth.value + listTitleExtraWidth.value}px`)

// Pre-resolve components for use inside cell h() renderers
const UButtonComp = resolveComponent('UButton')
const UIconComp   = resolveComponent('UIcon')
const UPopoverComp = resolveComponent('UPopover')

function renderDependencyBadge(dependency: DemandDependency) {
  return h('button', {
    type: 'button',
    class: 'inline-flex max-w-full cursor-pointer items-center gap-1 rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-[11px] font-medium text-amber-700 transition-colors hover:border-amber-300 hover:bg-amber-100 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300 dark:hover:border-amber-700 dark:hover:bg-amber-900/50',
    title: formatDependencySummaryLine(dependency),
    onClick: () => openDependencyDemand(dependency)
  }, [
    h(UIconComp, { name: 'i-lucide-link', class: 'h-3 w-3 shrink-0' }),
    h('span', { class: 'min-w-0 max-w-[14rem] truncate' }, formatDependencyBadgeLabel('Bloqueia', dependency))
  ])
}

function renderDependsOnBadge(demand: RoadmapDemand, dependency: DemandDependency) {
  const inconsistent = isDependencyInconsistent(demand, dependency)

  return h('button', {
    type: 'button',
    class: inconsistent
      ? 'inline-flex max-w-full cursor-pointer items-center gap-1 rounded-full border border-red-200 bg-red-50 px-2 py-0.5 text-[11px] font-medium text-red-700 transition-colors hover:border-red-300 hover:bg-red-100 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300 dark:hover:border-red-700 dark:hover:bg-red-900/50'
      : 'inline-flex max-w-full cursor-pointer items-center gap-1 rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-[11px] font-medium text-amber-700 transition-colors hover:border-amber-300 hover:bg-amber-100 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300 dark:hover:border-amber-700 dark:hover:bg-amber-900/50',
    title: `${getDependencyTooltip('É bloqueado por', dependency)}${inconsistent ? `\n\nInconsistência: a demanda vinculada está em ${dependency.quarterLabel}, depois de ${demand.quarterLabel}, ou sem priorização.` : ''}`,
    onClick: () => openDependencyDemand(dependency)
  }, [
    h(UIconComp, { name: 'i-lucide-link', class: 'h-3 w-3 shrink-0' }),
    h('span', { class: 'min-w-0 max-w-[14rem] truncate' }, formatDependencyBadgeLabel('Bloqueado por', dependency)),
    ...(inconsistent
      ? [
          h(UIconComp, { name: 'i-lucide-triangle-alert', class: 'h-3 w-3 shrink-0' }),
          h('span', { class: 'shrink-0 font-semibold' }, 'Inconsistente')
        ]
      : [])
  ])
}

const listTanstackColumns: TableColumn<RoadmapDemand>[] = [
  {
    id: 'priority',
    header: 'Prioridade',
    accessorFn: row => row.sortOrder,
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => left.sortOrder - right.sortOrder),
    enableColumnFilter: false,
    size: 64,
    meta: { style: { td: () => ({ width: listColWidth('priority', 64) }), th: () => ({ width: listColWidth('priority', 64) }) } },
    cell: ({ row }) => {
      const demand = row.original
      if (isCollapsedRepresentative(demand))
        return h('div', { class: 'text-xs text-muted' }, '—')

      return h('div', { class: 'flex items-center justify-center gap-1.5' }, [
        h('span', {
          class: 'list-priority-handle inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-md border border-default bg-elevated text-muted transition-colors hover:border-primary/40 hover:text-highlighted cursor-grab active:cursor-grabbing',
          title: 'Arrastar para repriorizar'
        }, [h(UIconComp, { name: 'i-lucide-grip-vertical', class: 'h-3.5 w-3.5' })]),
        h('label', { class: 'flex shrink-0 items-center justify-center' }, [
          h('input', {
            type: 'checkbox',
            class: 'h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary',
            checked: isDemandSelected(demand.id),
            onClick: (event: Event) => event.stopPropagation(),
            onChange: (event: Event) => toggleDemandSelection(demand.id, (event.target as HTMLInputElement).checked)
          })
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
      const q = filterValue.trim().toLowerCase()
      const epic = d.epicId ? itemsById.value.get(d.epicId) : null
      const epicIssueLinks = epic?.itemType === 'Epic' ? getDisplayIssueLinks(epic) : []
      return d.title.toLowerCase().includes(q)
        || (d.description?.toLowerCase().includes(q) ?? false)
        || (d.jiraIssue?.toLowerCase().includes(q) ?? false)
        || getDisplayIssueLinks(d).some(issue => issue.key.toLowerCase().includes(q))
        || epicIssueLinks.some(issue => issue.key.toLowerCase().includes(q))
        || (d.epicTitle?.toLowerCase().includes(q) ?? false)
        || (d.roadmapTitle?.toLowerCase().includes(q) ?? false)
    },
    size: 560,
    meta: { style: { td: () => ({ width: listColWidth('title', 560) }), th: () => ({ width: listColWidth('title', 560) }) } },
    cell: ({ row }) => {
      const d = row.original
      const isDeprioritized = d.status === 'Deprioritized'
      const textNodes = []
      const issueLinks = getDisplayIssueLinks(d)
      if (isCollapsedRepresentative(d))
        return h('span', { class: 'hidden' })
      if (!groupDemandsByEpic.value && (d.roadmapTitle || d.epicTitle)) {
        const hierarchyNodes = []

        if (d.roadmapTitle) {
          hierarchyNodes.push(h('div', { class: 'mb-1 flex min-w-0 items-center gap-1.5' }, [
            h(UIconComp, { name: 'i-lucide-map', class: 'h-3.5 w-3.5 shrink-0 text-primary' }),
            h('span', {
              class: 'min-w-0 flex-1 truncate text-[11px] text-muted',
              title: d.roadmapTitle
            }, d.roadmapTitle)
          ]))
        }

        if (d.epicTitle) {
          hierarchyNodes.push(h('div', { class: 'mb-1 flex min-w-0 items-center gap-1.5' }, [
            h(UIconComp, { name: 'i-lucide-star', class: 'h-3.5 w-3.5 shrink-0 text-amber-500' }),
            h('span', {
              class: 'min-w-0 flex-1 truncate text-[11px] text-highlighted',
              title: d.epicTitle
            }, d.epicTitle)
          ]))
        }

        textNodes.push(...hierarchyNodes)
      }
      if (groupDemandsByEpic.value && d.epicId) {
        const dependencyIndentClass = 'ml-5'
        const groupedContentNodes = [
          h('div', { class: 'flex items-start gap-1.5' }, [
            h(UIconComp, { name: 'i-lucide-list-todo', class: 'mt-0.5 h-3.5 w-3.5 shrink-0 text-sky-600' }),
            h('div', { class: 'min-w-0 flex-1' }, [
              h('div', { class: 'mb-1 flex flex-wrap items-center gap-1.5' }, [
                h('span', {
                  class: 'inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-muted'
                }, 'Demanda'),
                ...issueLinks.map(issue => issue.url
                  ? h('a', {
                      href: issue.url,
                      target: '_blank',
                      rel: 'noopener noreferrer',
                      class: 'inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] font-medium text-primary transition-colors hover:border-primary/40'
                    }, issue.key)
                  : h('span', {
                      class: 'inline-flex items-center rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] font-medium text-primary'
                    }, issue.key)
                )
              ]),
              h('p', { class: `truncate text-[13px] font-medium ${isDeprioritized ? 'line-through text-muted' : 'text-highlighted'}`, title: d.description || undefined }, d.title)
            ])
          ])
        ]

        if (d.dependsOn.length) {
          groupedContentNodes.push(
            h('div', { class: `mt-1 flex flex-wrap gap-1 ${dependencyIndentClass}` }, [
              ...d.dependsOn.slice(0, 2).map(dependency => renderDependsOnBadge(d, dependency)),
              ...(d.dependsOn.length > 2
                ? [h('span', { class: 'text-[11px] text-muted' }, `+${d.dependsOn.length - 2}`)]
                : [])
            ])
          )
        }

        if (d.dependedOnBy.length) {
          groupedContentNodes.push(
            h('div', { class: `mt-1 flex flex-wrap gap-1 ${dependencyIndentClass}` }, [
              ...d.dependedOnBy.slice(0, 2).map(dependency => renderDependencyBadge(dependency)),
              ...(d.dependedOnBy.length > 2
                ? [h('span', { class: 'text-[11px] text-muted' }, `+${d.dependedOnBy.length - 2}`)]
                : [])
            ])
          )
        }

        textNodes.push(h('div', { class: 'pl-8' }, groupedContentNodes))
      }
      else {
        textNodes.push(h('p', { class: `font-medium truncate ${isDeprioritized ? 'line-through text-muted' : 'text-highlighted'}`, title: d.description || undefined }, d.title))
      }

      if (!groupDemandsByEpic.value || !d.epicId) {
        const relationshipNodes = [
          ...issueLinks.map(issue => issue.url
            ? h('a', {
                href: issue.url,
                target: '_blank',
                rel: 'noopener noreferrer',
                class: 'inline-flex items-center gap-1 rounded-full border border-blue-200 bg-blue-50 px-2 py-0.5 text-xs font-mono text-blue-700 hover:border-blue-300 hover:bg-blue-100 dark:border-blue-800 dark:bg-blue-900/20 dark:text-blue-300'
              }, issue.key)
            : h('span', {
                class: 'inline-flex items-center gap-1 rounded-full border border-blue-200 bg-blue-50 px-2 py-0.5 text-xs font-mono text-blue-700 dark:border-blue-800 dark:bg-blue-900/20 dark:text-blue-300'
              }, issue.key)
          ),
          ...d.dependsOn.slice(0, 2).map(dependency => renderDependsOnBadge(d, dependency)),
          ...(d.dependsOn.length > 2
            ? [h('span', { class: 'text-[11px] text-muted' }, `+${d.dependsOn.length - 2}`)]
            : []),
          ...d.dependedOnBy.slice(0, 2).map(dependency => renderDependencyBadge(dependency)),
          ...(d.dependedOnBy.length > 2
            ? [h('span', { class: 'text-[11px] text-muted' }, `+${d.dependedOnBy.length - 2}`)]
            : [])
        ]

        if (relationshipNodes.length)
          textNodes.push(h('div', { class: 'mt-1 flex flex-wrap items-center gap-1' }, relationshipNodes))
      }
      return h('div', { class: 'min-w-0' }, textNodes)
    },
  },
  {
    id: 'kpis',
    header: 'KPI',
    accessorFn: row => {
      const summary = getDemandKpiSummary(row)
      return summary.label
    },
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => {
      return getDemandKpiSummary(left).label.localeCompare(getDemandKpiSummary(right).label, 'pt-BR')
    }),
    enableColumnFilter: false,
    size: 100,
    meta: { style: { td: () => ({ width: listColWidth('kpis', 100) }), th: () => ({ width: listColWidth('kpis', 100) }) } },
    cell: ({ row }) => {
      if (isCollapsedRepresentative(row.original))
        return h('span', { class: 'text-xs text-muted' }, '—')

      const summary = getDemandKpiSummary(row.original)
      const isClickable = summary.actionLabel !== 'Associe a demanda a um épico'

      return h('div', { class: 'flex min-w-0 flex-col items-start gap-1' }, [
        h('button', {
          type: 'button',
          class: `inline-flex h-6 max-w-full items-center rounded-md border px-1.5 text-[10px] font-medium transition-colors hover:opacity-80 ${summary.tone}`,
          title: summary.actionLabel,
          disabled: !isClickable,
          onClick: () => {
            if (isClickable)
              openDemandKpiWorkspace(row.original)
          }
        }, summary.label)
      ])
    }
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
    size: 96,
    meta: { style: { td: () => ({ width: listColWidth('quarterLabel', 96) }), th: () => ({ width: listColWidth('quarterLabel', 96) }) } },
    cell: ({ row }) => {
      const demand = row.original
      if (isCollapsedRepresentative(demand))
        return h('span', { class: 'text-xs text-muted' }, '—')

      const quarterNode = demand.quarterYear === 0 && demand.quarterNumber === 0
        ? h('span', {
          class: 'inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-[0.08em] text-highlighted'
        }, 'Backlog')
        : h('span', { class: 'inline-flex items-center rounded-md border border-default bg-default px-2 py-0.5 text-[10px] font-mono text-highlighted' }, demand.quarterLabel)

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
    size: 124,
    meta: { style: { td: () => ({ width: listColWidth('status', 124) }), th: () => ({ width: listColWidth('status', 124) }) } },
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
    size: 124,
    meta: { style: { td: () => ({ width: listColWidth('products', 124) }), th: () => ({ width: listColWidth('products', 124) }) } },
    cell: ({ row }) => {
      if (isCollapsedRepresentative(row.original))
        return h('span', { class: 'text-xs text-muted' }, '—')

      const prods = row.original.products
      if (!prods.length) return h('span', { class: 'text-xs text-muted' }, '—')
      return h('div', { class: 'flex flex-wrap gap-1' },
        prods.slice(0, 2).map(p => h('span', {
          class: 'inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[10px] text-highlighted'
        }, p.name)).concat(
          prods.length > 2
            ? [h('span', { class: 'text-[11px] text-muted' }, `+${prods.length - 2}`)]
            : []
        )
      )
    },
  },
  {
    accessorKey: 'customers',
    header: 'Clientes',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => formatDemandCustomers(getEffectiveDemandCustomers(left)).localeCompare(formatDemandCustomers(getEffectiveDemandCustomers(right)), 'pt-BR')),
    enableColumnFilter: true,
    filterFn: (row, _colId, filterValue: string) => {
      const query = filterValue.trim().toLowerCase()
      return !query || getEffectiveDemandCustomers(row.original).some(customer => customer.toLowerCase().includes(query))
    },
    size: 110,
    meta: { style: { td: () => ({ width: listColWidth('customers', 110) }), th: () => ({ width: listColWidth('customers', 110) }) } },
    cell: ({ row }) => {
      if (isCollapsedRepresentative(row.original))
        return h('span', { class: 'text-xs text-muted' }, '—')

      const customers = getEffectiveDemandCustomers(row.original)
      if (!customers.length)
        return h('span', { class: 'text-xs text-muted' }, '—')

      return h('div', { class: 'flex flex-wrap gap-1' },
        customers.slice(0, 2).map(customer => h('span', {
          class: 'inline-flex items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[10px] text-highlighted'
        }, customer)).concat(
          customers.length > 2
            ? [h('span', { class: 'text-[11px] text-muted' }, `+${customers.length - 2}`)]
            : []
        )
      )
    },
  },
  {
    accessorKey: 'hours',
    header: 'Hrs',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => (left.hours ?? 0) - (right.hours ?? 0)),
    enableColumnFilter: false,
    size: 60,
    meta: { class: { td: 'text-right' }, style: { td: () => ({ width: listColWidth('hours', 60) }), th: () => ({ width: listColWidth('hours', 60) }) } },
    cell: ({ row }) => isCollapsedRepresentative(row.original)
      ? h('span', { class: 'text-xs text-muted' }, '—')
      : isDemandEstimated(row.original)
        ? h('span', {
          class: 'inline-flex items-center rounded-md border border-default bg-default px-2 py-0.5 text-[10px] font-semibold text-highlighted'
        }, `${row.original.hours}h`)
        : h('span', {
          class: 'inline-flex items-center rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-[10px] font-semibold text-amber-700 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300'
        }, '0h'),
  },
  {
    id: '_actions',
    header: '',
    enableSorting: false,
    enableColumnFilter: false,
    size: 112,
    meta: { class: { td: 'text-right overflow-visible' }, style: { td: () => ({ width: listColWidth('_actions', 112) }), th: () => ({ width: listColWidth('_actions', 112) }) } },
    cell: ({ row }) => {
      const demand = row.original
      if (isCollapsedRepresentative(demand)) {
        return h('div', { class: 'flex items-center justify-end gap-1' }, [
          h(UButtonComp, {
            size: 'xs',
            variant: 'ghost',
            color: 'neutral',
            class: 'h-6 w-6 rounded-md border border-default bg-default p-0',
            onClick: () => toggleEpicCollapse(demand.epicId)
          }, {
            default: () => h(UIconComp, { name: 'i-lucide-chevron-right', class: 'h-4 w-4' })
          })
        ])
      }

      const actionSlots = []
      const kpiSummary = getDemandKpiSummary(demand)

      if (isBacklogDemand(demand)) {
        actionSlots.push(
          h(UPopoverComp, {}, {
            default: () => h(UButtonComp, {
              size: 'xs',
              variant: 'ghost',
              color: 'primary',
              class: 'h-6 w-6 p-0'
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
      else {
        actionSlots.push(h('span', { class: 'h-6 w-6' }))
      }

      if (kpiSummary.actionLabel !== 'Associe a demanda a um épico') {
        actionSlots.push(
          h(UButtonComp, {
            size: 'xs',
            variant: 'ghost',
            color: 'primary',
            class: 'h-6 w-6 p-0',
            icon: 'i-lucide-line-chart',
            title: 'Abrir KPIs do épico',
            onClick: () => openDemandKpiWorkspace(demand)
          })
        )
      }
      else {
        actionSlots.push(h('span', { class: 'h-6 w-6' }))
      }

      actionSlots.push(
        h(UButtonComp, { size: 'xs', variant: 'ghost', color: 'neutral', class: 'h-6 w-6 p-0', onClick: () => openEditModal(demand) }, {
          default: () => h(UIconComp, { name: 'i-lucide-pencil', class: 'h-4 w-4' })
        })
      )

      actionSlots.push(
        h(UButtonComp, { icon: 'i-lucide-trash-2', size: 'xs', variant: 'ghost', color: 'error', class: 'h-6 w-6 p-0', onClick: () => promptDelete(demand.id) })
      )

      return h('div', { class: 'ml-auto grid w-[104px] grid-cols-4 justify-items-end gap-0.5' }, actionSlots)
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
    if (c.id === 'kpis') return getDemandKpiSummary(row).label
    if (c.id === 'status') return getDisplayedDemandStatus(row).label
    if (c.id === 'type') return typeLabels[row.type]
    if (c.id === 'classification') {
      const classification = getEffectiveDemandClassification(row)
      return classification ? classificationLabels[classification] : ''
    }
    if (c.id === 'products') return row.products.map(p => p.name).join(', ')
    if (c.id === 'customers') return formatDemandCustomers(getEffectiveDemandCustomers(row))
    if (c.id === 'hours') return isDemandEstimated(row) ? `${row.hours}h` : 'Não estimada'
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
  const visibleRows = api ? api.getSortedRowModel().rows.map(r => r.original) : demandItems.value
  listExportUrlVisible.value = buildListBlobUrl(visibleRows)
  listExportUrlFull.value    = buildListBlobUrl(demandItems.value)
  listExportMenuOpen.value   = true
}
function closeListExportMenu() { listExportMenuOpen.value = false }

onUnmounted(() => {
  destroyListSortable()
  listWidthObserver?.disconnect()
  if (listExportUrlVisible.value) URL.revokeObjectURL(listExportUrlVisible.value)
  if (listExportUrlFull.value) URL.revokeObjectURL(listExportUrlFull.value)
})

// Default quarter filter to current quarter (client-side filtering)
filterQuarters.value = [`${currentQuarterNumber}-${currentYear}`]

// Load data
await roadmapStore.fetchProjects()

const queryProjectId = typeof route.query.projectId === 'string'
  ? route.query.projectId
  : null

if (queryProjectId && projects.value.some(project => project.id === queryProjectId))
  selectedProjectId.value = queryProjectId

await Promise.all([
  roadmapStore.fetchDemands(),
  roadmapStore.fetchDependencyOptions(),
  roadmapStore.fetchCustomerSuggestions()
])

if (activeDemandKpiId.value)
  await kpiStore.fetchKpis()

watch(() => route.query.view, (value) => {
  viewMode.value = value === 'hierarchy' ? 'hierarchy' : 'list'
}, { immediate: true })

watch(activeDemandKpiId, async (value) => {
  if (!value) {
    collapseAllEpicGroups()
    await nextTick()
    syncListSectionDividers()
    initListSortable()
    return
  }

  await kpiStore.fetchKpis()
})

</script>

<template>
  <div class="space-y-4">
    <template v-if="activeDemandKpiId">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="min-w-0">
          <UButton
            type="button"
            variant="ghost"
            color="neutral"
            size="sm"
            icon="i-lucide-arrow-left"
            label="Voltar para roadmap"
            @click="closeDemandKpiWorkspace"
          />

          <div class="mt-2">
            <h1 class="text-lg font-semibold tracking-tight text-highlighted">KPIs do épico</h1>
            <p class="mt-1 truncate text-xs text-muted">
              Tela dedicada para vínculo de indicadores e apuração contínua do épico.
            </p>
          </div>
        </div>
      </div>

      <template v-if="activeDemandKpi">
        <UCard :ui="{ body: 'p-4 sm:p-5' }">
          <div class="flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
            <div class="space-y-1">
              <p class="text-xs font-semibold uppercase tracking-[0.08em] text-primary/70">Épico</p>
              <h2 class="text-lg font-semibold text-highlighted">{{ activeDemandKpi.title }}</h2>
              <p class="text-xs text-muted">
                {{ selectedProject?.name ?? 'Projeto' }} · {{ activeDemandKpi.quarterLabel }}
              </p>
            </div>

            <div class="flex flex-wrap gap-2">
              <span class="inline-flex items-center rounded-md border px-2 py-1 text-xs font-medium" :class="statusTone[activeDemandKpi.status]">
                {{ statusLabels[activeDemandKpi.status] }}
              </span>
              <span class="inline-flex items-center rounded-full border px-2 py-1 text-xs font-medium" :class="classificationBadgeClass[activeDemandKpi.classification]">
                {{ classificationLabels[activeDemandKpi.classification] }}
              </span>
              <UBadge v-if="activeDemandKpi.hasNoKpi" variant="subtle" color="warning">
                {{ activeDemandKpi.noKpiClassification
                  ? `Sem KPI · ${getNoKpiClassificationLabel(activeDemandKpi.noKpiClassification)}`
                  : 'Sem KPI' }}
              </UBadge>
              <UBadge v-else variant="subtle" color="info">{{ activeDemandKpi.kpiLinks.length }} KPI(s)</UBadge>
            </div>
          </div>
        </UCard>

        <RoadmapDemandKpiWorkspace
          :demand="activeDemandKpi"
          :available-kpis="availableKpis"
        />
      </template>

      <UCard v-else :ui="{ body: 'p-8' }">
        <div class="space-y-3 text-center">
          <UIcon name="i-lucide-search-x" class="mx-auto h-10 w-10 text-muted" />
          <div>
            <h2 class="text-lg font-semibold text-highlighted">Épico não encontrado</h2>
            <p class="mt-1 text-sm text-muted">
              Não foi possível localizar o épico para abrir o registro de KPIs.
            </p>
          </div>
          <UButton
            type="button"
            label="Voltar para roadmap"
            icon="i-lucide-arrow-left"
            @click="closeDemandKpiWorkspace"
          />
        </div>
      </UCard>
    </template>

    <template v-else>
    <template v-if="viewMode === 'list'">
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
              icon="i-lucide-layout-list"
              :variant="viewMode === 'list' ? 'soft' : 'ghost'"
              label="Planejamento"
              @click="openListView"
            />
            <UButton
              size="sm"
              color="neutral"
              icon="i-lucide-workflow"
              :variant="viewMode === 'hierarchy' ? 'soft' : 'ghost'"
              label="Roadmap"
              @click="openHierarchyView"
            />
          </div>
          <UDropdownMenu :items="createMenuItems">
            <UButton icon="i-lucide-plus" label="Novo Item" />
          </UDropdownMenu>
        </div>
      </div>

      <div class="mt-4 flex flex-wrap items-center gap-2">
        <div class="flex items-center gap-2 flex-wrap">
          <button
            class="px-4 py-1.5 rounded-full text-sm font-medium transition-all border"
            :class="selectedProjectId === null
              ? 'bg-primary text-white border-primary shadow-sm'
              : 'border-default text-muted hover:border-primary/40 hover:text-highlighted'"
            @click="roadmapStore.selectProject(null)"
          >
            Todos os projetos
          </button>
          <button
            v-for="project in sortedProjects"
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

        <div class="ml-auto flex w-full flex-wrap items-center gap-2 sm:w-auto">
          <UPopover>
            <button class="flex w-full items-center gap-1.5 rounded-lg border border-default bg-background px-3 py-1.5 text-sm transition-colors hover:border-primary/40 sm:min-w-[220px] sm:w-auto">
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
        </div>
      </div>
    </div>

    <div class="rounded-[20px] border border-default bg-default px-3 py-2 shadow-sm">
      <div class="flex flex-col gap-2 xl:flex-row xl:items-center xl:justify-between">
        <div class="flex flex-wrap items-center gap-1.5 text-[11px] text-muted">
          <span class="inline-flex items-center gap-1 rounded-full border border-default bg-elevated px-2.5 py-0.5">
            <UIcon name="i-lucide-folder-kanban" class="h-3.5 w-3.5 text-primary" />
            <span class="font-medium text-highlighted">{{ selectedProject?.name ?? 'Projeto' }}</span>
          </span>
          <span class="rounded-full border border-default bg-elevated px-2.5 py-0.5">{{ activeCapacityScope?.quarterLabel ?? 'Selecione 1 quarter' }}</span>
          <span class="rounded-full border border-default bg-default px-2.5 py-0.5">
            Comprometido: <span class="font-semibold" :class="capacityCommittedTone">{{ displayCapacitySummary?.committedHours.toLocaleString('pt-BR') ?? '0' }}h</span>
            <span class="text-muted"> / {{ displayCapacitySummary?.capacityHours?.toLocaleString('pt-BR') ?? '—' }}h</span>
          </span>
          <span v-if="capacityConfigured" class="inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-[11px] font-semibold" :class="capacityDeltaTone">
            <UIcon :name="capacityIsOver ? 'i-lucide-circle-alert' : 'i-lucide-circle-check'" class="h-3.5 w-3.5" />
            {{ capacityDeltaLabel }}: {{ capacityDeltaValue?.toLocaleString('pt-BR') ?? '—' }}h
          </span>
          <span class="inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-[11px] font-semibold" :class="capacityUnestimatedTone">
            <UIcon name="i-lucide-triangle-alert" class="h-3.5 w-3.5" />
            {{ capacityUnestimatedLabel }}
          </span>
          <span class="inline-flex items-center gap-1 rounded-full border border-default bg-default px-2.5 py-0.5 text-[11px] font-semibold text-highlighted">
            <UIcon name="i-lucide-bolt" class="h-3.5 w-3.5 text-amber-500" />
            {{ displayCapacitySummary?.additionalHours.toLocaleString('pt-BR') ?? '0' }}h adicionais
          </span>
          <span class="rounded-full border border-default bg-default px-2.5 py-0.5 text-[11px] font-semibold" :class="capacityPercentTone">
            {{ capacityConfigured ? `${capacityProgressPercent.toFixed(0)}% do capacity` : 'Capacity não configurado' }}
          </span>
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
      <!-- ── LIST VIEW ───────────────────────────────────────────────────── -->
      <div class="overflow-visible rounded-2xl border border-default bg-default shadow-sm">
        <div class="border-b border-default bg-elevated/35 px-3 py-2.5">
          <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-end">
            <div class="flex flex-wrap items-center justify-end gap-1.5">
              <UPopover v-if="selectedDemandCount">
                <UButton
                  size="xs"
                  color="primary"
                  variant="soft"
                  trailing-icon="i-lucide-chevron-down"
                  leading-icon="i-lucide-calendar-range"
                  :loading="isBulkPlanning"
                >
                  Mover {{ selectedDemandCount.toLocaleString('pt-BR') }}
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
                v-if="selectedDemandCount"
                size="xs"
                color="neutral"
                variant="ghost"
                leading-icon="i-lucide-square-minus"
                @click="clearSelectedDemands"
              >
                Desmarcar todos
              </UButton>
              <UButton
                size="xs"
                color="neutral"
                variant="outline"
                :leading-icon="groupDemandsByEpic ? 'i-lucide-layers-3' : 'i-lucide-list'"
                @click="groupDemandsByEpic = !groupDemandsByEpic"
              >
                {{ groupDemandsByEpic ? 'Desagrupar épicos' : 'Agrupar por épico' }}
              </UButton>
              <UButton
                v-if="visibleEpicIds.length"
                size="xs"
                color="neutral"
                variant="outline"
                :leading-icon="areAllEpicGroupsCollapsed ? 'i-lucide-unfold-vertical' : 'i-lucide-fold-vertical'"
                @click="areAllEpicGroupsCollapsed ? expandAllEpicGroups() : collapseAllEpicGroups()"
              >
                {{ areAllEpicGroupsCollapsed ? 'Expandir tudo' : 'Recolher tudo' }}
              </UButton>
              <UButton
                v-if="listHasActiveFilters"
                size="xs"
                color="neutral"
                variant="ghost"
                leading-icon="i-lucide-filter-x"
                @click="clearListFilters"
              >
                Limpar filtros
                <UBadge size="xs" color="primary" variant="solid" class="ml-1">{{ listColumnFilters.length }}</UBadge>
              </UButton>
              <div class="relative shrink-0">
              <UButton
                variant="outline"
                size="xs"
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
        </div>

        <div class="sticky top-14 z-20 overflow-hidden rounded-t-2xl border-b border-default bg-elevated/95 shadow-sm backdrop-blur supports-[backdrop-filter]:bg-elevated/85 md:top-0">
          <table class="table-fixed text-sm will-change-transform" :style="{ width: listTableWidth, transform: `translateX(-${listHeaderScrollLeft}px)` }">
            <thead>
              <tr ref="listHeaderRowRef">
                <th
                  v-for="col in listOrderedCols"
                  :key="col.id"
                  :data-col-id="col.id"
                  class="relative overflow-hidden px-3 py-2 text-[11px] font-semibold uppercase tracking-[0.08em] text-muted"
                  :class="col.alignRight ? 'text-right' : 'text-left'"
                  :style="{ width: listColWidth(col.id, col.defaultWidth) }"
                >
                  <span
                    class="list-col-drag absolute left-1 top-1/2 -translate-y-1/2 cursor-grab text-muted opacity-25 hover:opacity-80 select-none"
                    title="Arrastar coluna"
                  >⠿</span>
                  <div class="flex min-h-[58px] flex-col justify-between pl-2" :class="col.alignRight ? 'items-end' : ''">
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
                        <button class="mt-1 flex w-full items-center gap-1.5 rounded-md border border-default bg-default px-2 py-1 text-xs hover:border-primary/40 transition-colors">
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
                        class="mt-1 w-full min-w-0"
                        :class="col.alignRight ? 'text-right' : ''"
                        @update:model-value="(v: string) => setListColFilter(col.id, v)"
                      />
                      <div v-else class="mt-1 h-7" />
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

        <div ref="listScrollContainerRef" :class="shouldConstrainListHeight ? 'max-h-[560px] overflow-x-auto overflow-y-auto' : 'overflow-x-auto overflow-y-visible'" @scroll="syncListHeaderScroll">
          <div ref="listTableRootRef" :style="{ width: listTableWidth }">
            <UTable
              :key="listTableKey"
              ref="listTable"
              v-model:sorting="listSorting"
              v-model:column-filters="listColumnFilters"
              v-model:column-sizing="listColumnSizing"
              v-model:column-order="listColumnOrder"
              :data="tableDemands"
              :columns="listTanstackColumns"
              :get-row-id="(row: RoadmapDemand) => row.id"
              :column-sizing-options="{ enableColumnResizing: true, columnResizeMode: 'onChange' }"
              :ui="{ base: 'w-full table-fixed', thead: 'hidden', td: 'border-b border-default py-2.5 align-top overflow-hidden' }"
            >
              <template #status-cell="{ row }">
                <div
                  class="flex flex-col gap-1"
                  :title="getDemandNotesTooltip(row.original) || getDisplayedDemandStatus(row.original).label"
                >
                  <template v-if="!isCollapsedRepresentative(row.original)">
                  <div class="flex items-center gap-1.5">
                    <span
                      class="inline-flex w-fit items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium"
                      :class="getStatusBadgeClass(row.original.status)"
                    >
                      {{ getDisplayedDemandStatus(row.original).label }}
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
                  <div
                    v-if="getDisplayedConclusionDate(row.original)"
                    class="flex items-center gap-1 text-[11px]"
                    :class="row.original.status === 'Done' && row.original.deliveryDate ? 'text-green-600 dark:text-green-400' : 'text-muted'"
                  >
                    <UIcon :name="row.original.status === 'Done' && row.original.deliveryDate ? 'i-lucide-calendar-check' : 'i-lucide-calendar-clock'" class="h-3 w-3" />
                    <span>{{ formatDemandDate(getDisplayedConclusionDate(row.original)) }}</span>
                  </div>
                  <div
                    v-if="showDemandDelayMarker(row.original)"
                    class="flex items-center gap-1 text-[11px] font-medium text-amber-600 dark:text-amber-400"
                  >
                    <UIcon name="i-lucide-triangle-alert" class="h-3 w-3" />
                    <span>Atrasado</span>
                  </div>
                  </template>
                  <span v-else class="text-xs text-muted">—</span>
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
                <td class="px-4 py-2.5">
                  <div class="flex flex-wrap items-center justify-between gap-2 text-xs">
                    <span class="font-semibold text-highlighted">
                      {{ listFilteredCount.toLocaleString('pt-BR') }}
                      <span v-if="listHasActiveFilters" class="font-normal text-muted"> de {{ quarterFilteredDemands.length.toLocaleString('pt-BR') }}</span>
                      demandas
                    </span>
                    <div class="flex flex-wrap items-center gap-1.5 text-muted">
                      <span class="rounded-full border border-default bg-default px-2 py-0.5">{{ visibleRoadmapCount.toLocaleString('pt-BR') }} roadmaps</span>
                      <span class="rounded-full border border-default bg-default px-2 py-0.5">{{ visibleEpicCount.toLocaleString('pt-BR') }} épicos</span>
                      <span class="rounded-full border border-default bg-default px-2 py-0.5">{{ visibleDemandCount.toLocaleString('pt-BR') }} demandas</span>
                    </div>
                  </div>
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

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
    </template>

    <template v-else>
      <RoadmapHierarchyPage />
    </template>

    <!-- Create / Edit modal -->
    <RoadmapDemandFormModal
      v-model:open="modalOpen"
      :projects="sortedProjects"
      :dependency-options="dependencyOptions"
      :customer-suggestions="customerSuggestions"
      :demand="editingDemand"
      :default-item-type="createItemType"
      :default-parent-demand-id="defaultParentDemandId"
      :default-project-id="defaultProjectId ?? selectedProjectId ?? undefined"
      :default-project-ids="defaultProjectIds"
      :roadmap-options="roadmapParentOptions"
      :epic-options="epicParentOptions"
      :default-quarter-year="selectedDemandScope?.quarterYear ?? activeCapacityScope?.quarterYear ?? undefined"
      :default-quarter-number="selectedDemandScope?.quarterNumber ?? activeCapacityScope?.quarterNumber ?? undefined"
      :is-saving="isSavingDemand"
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
    </template>
  </div>
</template>
