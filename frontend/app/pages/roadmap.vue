<script setup lang="ts">
import { h, nextTick, resolveComponent, useTemplateRef, onMounted, onUnmounted } from 'vue'
import Sortable from 'sortablejs'
import type { TableColumn } from '@nuxt/ui'
import type { SortingState, ColumnFiltersState, ColumnSizingState } from '@tanstack/vue-table'
import type * as XLSXType from 'xlsx'
import type { ApiResponse } from '~/types/api'
import type { RoadmapDemand, DemandDependency, RoadmapCapacitySummary, DemandFormData, CapacityFormData, DemandStatus, DemandType, DemandClassification, NoKpiClassification, RoadmapItemType, BulkEditRoadmapItemsData, CustomerRename, DeprioritizationReason } from '~/types/roadmap'
import BulkEditRoadmapItemsModal from '~/components/roadmap/BulkEditRoadmapItemsModal.vue'
import RoadmapHierarchyPage from '~/components/roadmap/RoadmapHierarchyPage.vue'
import RoadmapDashboards from '~/components/roadmap/RoadmapDashboards.vue'
import type { DashboardSelection } from '~/types/roadmapDashboards'
import { getLatestPromisedDate } from '~/utils/roadmapPromisedDate'
import {
  BACKLOG_QUARTER,
  PRIORITIZED_BACKLOG_QUARTER,
  PRE_REGISTERED_QUARTER_END_YEAR,
  buildPreRegisteredQuarterYears,
  buildQuarterValue,
  formatQuarterLabel,
  isSpecialBacklogQuarter,
  parseQuarterValue
} from '~/utils/roadmapQuarter'

useSeoMeta({ title: 'Roadmap · ProductHub' })

const route = useRoute()
const api = useApi()
const roadmapStore = useRoadmapStore()
const kpiStore = useKpiStore()
const accessStore = useAccessStore()
const toast = useToast()

// Permissão de edição do roadmap. Sem ela, a tela fica somente leitura (ações escondidas/bloqueadas).
const canEditRoadmap = computed(() => accessStore.canEditRoadmap)

const { projects, demands, dependencyOptions, customerSuggestions, capacitySummary, selectedProject, selectedProjectId, selectedQuarterYear, selectedQuarterNumber, isLoading, isCapacityLoading } = storeToRefs(roadmapStore)
const { kpis: availableKpis } = storeToRefs(kpiStore)
const sortedProjects = computed(() =>
  [...projects.value].sort((left, right) => right.name.localeCompare(left.name, 'pt-BR'))
)
const projectNameById = computed(() =>
  new Map(projects.value.map(project => [project.id, project.name] as const))
)
const roadmapItems = computed(() => demands.value.filter(item => item.itemType === 'Roadmap'))
const epicItems = computed(() => demands.value.filter(item => item.itemType === 'Epic'))
const demandItems = computed(() => demands.value.filter(item => item.itemType === 'Demand'))
const itemsById = computed(() => new Map(demands.value.map(item => [item.id, item] as const)))

// ─── View mode ───────────────────────────────────────────────────────────────
const viewMode = ref<'list' | 'hierarchy'>(route.query.view === 'hierarchy' ? 'hierarchy' : 'list')
const planningBulkEditModalOpen = ref(false)

// ─── Quarter phase ────────────────────────────────────────────────────────────
const now = new Date()
const currentYear = now.getFullYear()
const currentQuarterNumber = Math.ceil((now.getMonth() + 1) / 3)

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
    { value: BACKLOG_QUARTER.value, label: BACKLOG_QUARTER.label },
    { value: PRIORITIZED_BACKLOG_QUARTER.value, label: PRIORITIZED_BACKLOG_QUARTER.label },
    ...buildPreRegisteredQuarterYears(currentYear, PRE_REGISTERED_QUARTER_END_YEAR).flatMap(y =>
      [1, 2, 3, 4].map(q => ({
        value: buildQuarterValue(y, q),
        label: `Q${q}/${String(y).slice(2)} — ${quarterPhaseConfig[getQuarterPhase(y, q)].label}`
      }))
    )
  ]
)

// Facilitador: um "chip" por ano que seleciona/desmarca os 4 quarters daquele ano de uma vez.
const quarterYearOptions = computed(() =>
  buildPreRegisteredQuarterYears(currentYear, PRE_REGISTERED_QUARTER_END_YEAR).map(year => ({
    year,
    values: [1, 2, 3, 4].map(q => buildQuarterValue(year, q))
  }))
)

function isQuarterYearFullySelected(values: string[]) {
  return values.every(value => filterQuarters.value.includes(value))
}

function toggleQuarterYear(values: string[]) {
  if (isQuarterYearFullySelected(values)) {
    filterQuarters.value = filterQuarters.value.filter(value => !values.includes(value))
    return
  }
  const next = new Set(filterQuarters.value)
  for (const value of values)
    next.add(value)
  filterQuarters.value = [...next]
}

const planningQuarterOptions = computed(() =>
  quarterOptions.value.filter(option => ![BACKLOG_QUARTER.value, PRIORITIZED_BACKLOG_QUARTER.value].includes(option.value))
)
const bulkMoveQuarterOptions = computed(() => quarterOptions.value)

const deprioritizationReasonOptions = [
  { value: 'StrategyChange', label: 'Mudança de estratégia' },
  { value: 'HigherValuePrioritization', label: 'Priorização de maior valor' },
  { value: 'LowCustomerDemand', label: 'Baixa demanda de clientes' },
  { value: 'LowExpectedReturn', label: 'Baixo retorno esperado' },
  { value: 'BusinessDefinitionDependency', label: 'Dependência de definição de negócio' },
  { value: 'AlternativeSolutionAvailable', label: 'Solução alternativa disponível' },
  { value: 'RegulatoryRequirementChanged', label: 'Requisito regulatório alterado' },
  { value: 'CustomerWithdrew', label: 'Cliente desistiu' },
  { value: 'ReplacedByOtherInitiative', label: 'Substituída por outra iniciativa' },
  { value: 'UndefinedScope', label: 'Escopo indefinido' }
] as const satisfies Array<{ value: DeprioritizationReason, label: string }>

const CACHE_KEY_PLANNING_PROJECTS = 'roadmap:planning:projectIds'
const CACHE_KEY_PLANNING_QUARTERS = 'roadmap:planning:quarters'
const CACHE_KEY_PLANNING_GROUP_BY_EPIC = 'roadmap:planning:groupByEpic'

function readCacheJson<T>(key: string): T | null {
  try { return JSON.parse(localStorage.getItem(key) ?? 'null') as T }
  catch { return null }
}

const filterQuarters = ref<string[]>([])
const filterListProjectIds = ref<string[]>([])

function quarterShortLabel(val: string): string {
  const { quarterYear, quarterNumber } = parseQuarterValue(val)
  return formatQuarterLabel(quarterYear, quarterNumber)
}

function planningQuarterDisplayLabel(demand: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'quarterLabel'>) {
  return isSpecialBacklogQuarter(demand.quarterYear, demand.quarterNumber)
    ? 'Backlog'
    : demand.quarterLabel
}

const quarterFilterLabel = computed(() => {
  if (!filterQuarters.value.length) return 'Todos os quarters'
  if (filterQuarters.value.length === 1) return quarterShortLabel(filterQuarters.value[0]!)
  if (filterQuarters.value.length === 2) return filterQuarters.value.map(quarterShortLabel).join(', ')
  return `${filterQuarters.value.length} quarters`
})

const filterListProjectsLabel = computed(() => {
  if (!filterListProjectIds.value.length) return 'Todos os times'
  if (filterListProjectIds.value.length === 1)
    return projects.value.find(p => p.id === filterListProjectIds.value[0])?.name ?? '1 time'
  return `${filterListProjectIds.value.length} times`
})

function formatDemandCustomers(customers?: string[]): string {
  return customers?.join(', ') ?? ''
}

const INLINE_LIST_AVG_CHAR_PX = 6.4
const INLINE_LIST_SIDE_PADDING_PX = 22
const INLINE_LIST_MORE_BADGE_PX = 28

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

function getEpicForDemand(demand: RoadmapDemand): RoadmapDemand | null {
  if (!demand.epicId) return null
  const epic = itemsById.value.get(demand.epicId) ?? null
  return epic?.itemType === 'Epic' ? epic : null
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

// Inline classification editing targets the epic (classification is an epic-level attribute):
// for a demand linked to an epic, edits go to that epic; otherwise to the item itself.
function getPlanningClassificationTarget(demand: RoadmapDemand): RoadmapDemand {
  if (demand.itemType === 'Demand')
    return getEpicForDemand(demand) ?? demand

  return demand
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
  if (!canEditRoadmap.value) return
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
    label: '+KPI',
    tone: 'border-error/40 bg-error/10 text-error',
    actionLabel: 'Incluir KPI'
  }
}

const listProblemOptions = [
  { value: 'overdueOpen', label: 'Itens atrasados' },
  { value: 'deliveredLate', label: 'Itens entregues com atraso' },
  { value: 'noKpi', label: 'Itens sem KPIs' },
  { value: 'doneNoKpi', label: 'Concluídos sem KPI apurado' },
  { value: 'noJira', label: 'Itens sem issue Jira' },
  { value: 'noHours', label: 'Itens sem horas' },
  { value: 'crossTeamInconsistentDep', label: 'Dependência entre times inconsistente' },
] as const

const listProblemLabels: Record<string, string> = {
  overdueOpen: 'Item atrasado',
  deliveredLate: 'Entregue com atraso',
  noKpi: 'Sem KPIs associados',
  doneNoKpi: 'Concluído sem KPI apurado',
  noJira: 'Sem issue Jira associada',
  noHours: 'Sem horas estimadas',
  crossTeamInconsistentDep: 'Dependência entre times inconsistente',
}

const listProblemFilter = ref<string[]>([])
// Filtro dedicado (acionado pelo dashboard): itens com qualquer dependência inconsistente.
// O filtro de "Problemas" só cobre cross-team; este cobre todas as inconsistências.
const filterInconsistentDeps = ref(false)
// Filtros dedicados de motivo (acionados pelos dashboards de motivos), aplicados sobre a lista.
const filterSpilloverReasons = ref<string[]>([])
const filterDeprioritizationReasons = ref<string[]>([])

const listProblemFilterLabel = computed(() => {
  if (!listProblemFilter.value.length)
    return 'Problemas'
  if (listProblemFilter.value.includes('__all__'))
    return 'Todos os problemas'
  if (listProblemFilter.value.length === 1)
    return listProblemOptions.find(o => o.value === listProblemFilter.value[0])?.label ?? '1 problema'
  return `${listProblemFilter.value.length} problemas`
})

function getDemandProblemKeys(demand: RoadmapDemand) {
  // Epics (simple or empty composite) have their own set of problems.
  if (demand.itemType === 'Epic') {
    const epicKeys: string[] = []

    if (demand.hasNoKpi || demand.kpiLinks.length === 0)
      epicKeys.push('noKpi')

    if (!getDisplayIssueLinks(demand).length)
      epicKeys.push('noJira')

    // Hours/quarter only exist on simple epics.
    if (demand.isSimple) {
      if (demand.hours == null)
        epicKeys.push('noHours')
      if (demand.status !== 'Done' && demand.isOverdue)
        epicKeys.push('overdueOpen')
      if (demand.status === 'Done' && demand.isDeliveredLate)
        epicKeys.push('deliveredLate')
    }

    if (demand.status === 'Done' && !demand.hasNoKpi && demand.kpiLinks.length > 0
      && (demand.kpiMeasurements?.length ?? 0) === 0)
      epicKeys.push('doneNoKpi')

    const effectivePid = getEffectiveProjectId(demand)
    if (demand.dependsOn.some(dep => isDependencyInconsistent(demand, dep) && dep.projectId !== effectivePid))
      epicKeys.push('crossTeamInconsistentDep')

    return epicKeys
  }

  if (demand.itemType !== 'Demand')
    return []

  const keys: string[] = []
  const targetEpic = demand.epicId ? itemsById.value.get(demand.epicId) ?? null : null

  if (demand.status !== 'Done' && demand.isOverdue)
    keys.push('overdueOpen')

  if (demand.status === 'Done' && demand.isDeliveredLate)
    keys.push('deliveredLate')

  if (!targetEpic || targetEpic.itemType !== 'Epic' || targetEpic.hasNoKpi || targetEpic.kpiLinks.length === 0)
    keys.push('noKpi')

  if (!getDisplayIssueLinks(demand).length)
    keys.push('noJira')

  if (demand.hours == null)
    keys.push('noHours')

  if (targetEpic?.itemType === 'Epic' && targetEpic.status === 'Done' && demand.status === 'Done' && !targetEpic.hasNoKpi && targetEpic.kpiLinks.length > 0) {
    const hasApuratedKpi = (targetEpic.kpiMeasurements?.length ?? 0) > 0
    if (!hasApuratedKpi)
      keys.push('doneNoKpi')
  }

  if (demand.dependsOn.some(dep => isDependencyInconsistent(demand, dep) && dep.projectId !== demand.projectId))
    keys.push('crossTeamInconsistentDep')

  return keys
}

function getDemandProblemTooltip(demand: RoadmapDemand) {
  return getDemandProblemKeys(demand).map(k => listProblemLabels[k] ?? k).join('\n')
}

function toggleListProblemFilter(problem: string) {
  if (listProblemFilter.value.includes(problem)) {
    listProblemFilter.value = listProblemFilter.value.filter(v => v !== problem)
    return
  }
  if (problem === '__all__') {
    listProblemFilter.value = ['__all__']
    return
  }
  listProblemFilter.value = [...listProblemFilter.value.filter(v => v !== '__all__'), problem]
}

function clearListProblemFilter() {
  listProblemFilter.value = []
  filterInconsistentDeps.value = false
}

function toggleInconsistentDepsFilter() {
  filterInconsistentDeps.value = !filterInconsistentDeps.value
}

function toggleSpilloverReasonFilter(reason: string) {
  filterSpilloverReasons.value = filterSpilloverReasons.value.includes(reason)
    ? filterSpilloverReasons.value.filter(item => item !== reason)
    : [...filterSpilloverReasons.value, reason]
}

function toggleDeprioritizationReasonFilter(reason: string) {
  filterDeprioritizationReasons.value = filterDeprioritizationReasons.value.includes(reason)
    ? filterDeprioritizationReasons.value.filter(item => item !== reason)
    : [...filterDeprioritizationReasons.value, reason]
}

// Há algum filtro ativo sobre a lista? (não inclui a seleção de time/quarter do topo)
const hasActiveListFilters = computed(() =>
  listColumnFilters.value.length > 0
  || listProblemFilter.value.length > 0
  || filterInconsistentDeps.value
  || filterSpilloverReasons.value.length > 0
  || filterDeprioritizationReasons.value.length > 0
)

// Limpa TODOS os filtros da lista (coluna + saúde/problemas + dependências + motivos),
// mantendo apenas a seleção de time e quarter.
function clearAllListFilters() {
  listColumnFilters.value = []
  listProblemFilter.value = []
  filterInconsistentDeps.value = false
  filterSpilloverReasons.value = []
  filterDeprioritizationReasons.value = []
}

// Estado ativo dos filtros para destacar o item selecionado no componente de dashboards.
const dashboardActiveFilters = computed(() => ({
  statuses: getListMultiFilter('status'),
  classifications: getTitleFilter().classifications,
  customers: getListMultiFilter('customers'),
  types: getQuarterTypeFilter().types,
  problems: listProblemFilter.value,
  inconsistentDeps: filterInconsistentDeps.value,
  spilloverReasons: filterSpilloverReasons.value,
  deprioritizationReasons: filterDeprioritizationReasons.value
}))

// Clique num item do dashboard (na planejamento) → aplica/alterna o filtro correspondente na lista.
function handleDashboardSelect(selection: DashboardSelection) {
  switch (selection.kind) {
    case 'status': toggleListMultiFilterValue('status', selection.value); break
    case 'classification': toggleTitleClassification(selection.value); break
    case 'customer': toggleListMultiFilterValue('customers', selection.value); break
    case 'type': toggleTypeFilterValue(selection.value); break
    case 'problem': toggleListProblemFilter(selection.value); break
    case 'inconsistentDeps': toggleInconsistentDepsFilter(); break
    case 'spilloverReason': toggleSpilloverReasonFilter(selection.value); break
    case 'deprioritizationReason': toggleDeprioritizationReasonFilter(selection.value); break
  }
}

// Abre o dashboard completo (home) em nova aba, levando os filtros de Time e Quarter atuais.
function openFullDashboard() {
  const params = new URLSearchParams()
  params.set('teams', filterListProjectIds.value.join(','))
  params.set('quarters', filterQuarters.value.join(','))
  window.open(`/home?${params.toString()}`, '_blank')
}

function getEpicDisplayGroupKey(demand: Pick<RoadmapDemand, 'roadmapId' | 'epicId' | 'quarterYear' | 'quarterNumber' | 'type'>) {
  // Group by epicId only — demands from the same epic must share a single grouper
  // regardless of which quarter they belong to.
  return demand.epicId ?? `no-epic:${demand.roadmapId ?? 'none'}:${demand.quarterYear}:${demand.quarterNumber}:${getDemandGroupKey(demand)}`
}

// Priority lives per Team + Quarter, so an epic's position must be computed per quarter. This
// key buckets an epic's demands by the quarter they sit in (an epic with demands in Q1 and Q2
// gets one bucket per quarter, prioritized independently).
function getEpicQuarterOrderKey(demand: Pick<RoadmapDemand, 'epicId' | 'quarterYear' | 'quarterNumber'>) {
  return `${demand.epicId}:${demand.quarterYear}:${demand.quarterNumber}`
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

    // A cluster is one epic within ONE quarter. Stop if we cross into another quarter so an
    // epic that spans quarters (last in Q1 / first in Q2) doesn't bleed its demands together.
    if (demand.quarterYear !== anchorDemand.quarterYear || demand.quarterNumber !== anchorDemand.quarterNumber)
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

function getEpicQuarterMoveScopeKey(demand: Pick<RoadmapDemand, 'projectId' | 'projectIds' | 'isSimple' | 'type'>) {
  const pid = demand.isSimple ? (demand.projectIds?.[0] ?? demand.projectId) : demand.projectId
  return `${pid}:${getDemandGroupKey(demand)}`
}

function getEpicHeaderMeta(anchorDemand?: RoadmapDemand) {
  // Epic rows injected as their own row (simple epics and empty composite epics): the
  // anchorDemand itself IS the epic. Otherwise resolve the parent epic from epicId.
  const resolvedEpic = (anchorDemand?.itemType === 'Epic')
    ? anchorDemand
    : (anchorDemand?.epicId ? (itemsById.value.get(anchorDemand.epicId) ?? null) : null)

  const epic = resolvedEpic?.itemType === 'Epic' ? resolvedEpic : null
  if (!epic)
    return null

  const groupedDemands = getVisibleEpicDemandCluster(anchorDemand)
  const totalHours = epic.isSimple
    ? (typeof epic.hours === 'number' ? epic.hours : 0)
    : groupedDemands.reduce((sum, demand) => sum + (typeof demand.hours === 'number' ? demand.hours : 0), 0)
  const groupedProductNames = epic.isSimple
    ? (() => {
        const epicProductIds = new Set(epic.products.map(p => p.productId))
        return projects.value.flatMap(proj => proj.products).filter(p => epicProductIds.has(p.id)).map(p => p.name)
      })()
    : Array.from(new Set(groupedDemands.flatMap(demand => demand.products.map(product => product.name))))
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
    case 'StrategyChange':
      return 'Mudança de estratégia'
    case 'HigherValuePrioritization':
      return 'Priorização de maior valor'
    case 'LowCustomerDemand':
      return 'Baixa demanda de clientes'
    case 'LowExpectedReturn':
      return 'Baixo retorno esperado'
    case 'BusinessDefinitionDependency':
      return 'Dependência de definição de negócio'
    case 'AlternativeSolutionAvailable':
      return 'Solução alternativa disponível'
    case 'RegulatoryRequirementChanged':
      return 'Requisito regulatório alterado'
    case 'CustomerWithdrew':
      return 'Cliente desistiu'
    case 'ReplacedByOtherInitiative':
      return 'Substituída por outra iniciativa'
    case 'UndefinedScope':
      return 'Escopo indefinido'
    default:
      return ''
  }
}

function getSpilloverReasonLabel(value: string | undefined) {
  switch (value) {
    case 'ScopeChange':
      return 'Mudança de escopo'
    case 'PriorityChangeNoTradeOff':
      return 'Mudança de prioridade (sem trade-off)'
    case 'ExternalDependency':
      return 'Dependência externa'
    case 'TechnicalBlock':
      return 'Impedimento técnico'
    case 'IncorrectEstimate':
      return 'Estimativa incorreta'
    case 'InsufficientCapacity':
      return 'Capacidade insuficiente'
    case 'QualityIssues':
      return 'Problemas de qualidade'
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
      return 'border-pink-200 bg-pink-50 text-pink-700 dark:border-pink-800 dark:bg-pink-900/20 dark:text-pink-300'
    case 'Blocked':
      return 'border-red-200 bg-red-50 text-red-700 dark:border-red-800 dark:bg-red-900/20 dark:text-red-300'
    case 'Spillover':
      return 'border-orange-200 bg-orange-50 text-orange-700 dark:border-orange-800 dark:bg-orange-900/20 dark:text-orange-300'
    case 'UX':
      return 'border-purple-200 bg-purple-50 text-purple-700 dark:border-purple-800 dark:bg-purple-900/20 dark:text-purple-300'
    case 'Prioritized':
      return 'border-cyan-200 bg-cyan-50 text-cyan-700 dark:border-cyan-800 dark:bg-cyan-900/20 dark:text-cyan-300'
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
  if (demand.status === 'Blocked' && demand.blockedReason)
    notes.push(`Impedimento\n${demand.blockedReason}`)
  if (demand.status === 'Deprioritized' && demand.observation)
    notes.push(`Despriorização${demand.deprioritizationReason ? ` · ${getDeprioritizationReasonLabel(demand.deprioritizationReason)}` : ''}\n${demand.observation}`)
  if (demand.status === 'Spillover' && demand.spilloverObservation)
    notes.push(`Transbordo${demand.spilloverReason ? ` · ${getSpilloverReasonLabel(demand.spilloverReason)}` : ''}\n${demand.spilloverObservation}`)
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

// Quarter used for dependency ordering. Demands and simple epics use their own quarter. A composite
// epic has no own quarter, so we use its LAST (latest) demand's quarter — recomputed LIVE from the
// store, so adding/removing demands keeps the inconsistency correct. A composite epic with no
// demands is "unplanned" → it can never be "done", so depending on it is always inconsistent.
type DependencyQuarter = { quarterYear: number, quarterNumber: number } | 'unplanned'

function effectiveDependencyQuarter(
  itemId: string,
  fallback: { quarterYear: number, quarterNumber: number }
): DependencyQuarter {
  const item = itemsById.value.get(itemId)
  if (!item)
    return fallback // not loaded (rare) → fall back to the snapshot quarter

  if (item.itemType !== 'Epic' || item.isSimple)
    return { quarterYear: item.quarterYear, quarterNumber: item.quarterNumber }

  const epicDemands = demandItems.value.filter(child => child.epicId === itemId)
  if (!epicDemands.length)
    return 'unplanned'

  let latest = epicDemands[0]!
  for (const child of epicDemands)
    if ((child.quarterYear * 4 + child.quarterNumber) > (latest.quarterYear * 4 + latest.quarterNumber))
      latest = child
  return { quarterYear: latest.quarterYear, quarterNumber: latest.quarterNumber }
}

// `dependent` is scheduled before `dependency` is done → inconsistent. Handles epics (effective
// quarter), backlog (unplanned) and no-demand epics (never done).
function isOrderInconsistent(
  dependent: { id: string, quarterYear: number, quarterNumber: number },
  dependency: DemandDependency
) {
  const dependentQuarter = effectiveDependencyQuarter(dependent.id, { quarterYear: dependent.quarterYear, quarterNumber: dependent.quarterNumber })
  if (dependentQuarter === 'unplanned' || isSpecialBacklogQuarter(dependentQuarter.quarterYear, dependentQuarter.quarterNumber))
    return false // the dependent isn't planned → no ordering to violate

  const dependencyQuarter = effectiveDependencyQuarter(dependency.demandId, { quarterYear: dependency.quarterYear, quarterNumber: dependency.quarterNumber })
  if (dependencyQuarter === 'unplanned' || isSpecialBacklogQuarter(dependencyQuarter.quarterYear, dependencyQuarter.quarterNumber))
    return true // the dependency can't be "done" before the dependent starts

  return compareQuarterPosition(dependentQuarter, dependencyQuarter) < 0
}

function isDependencyInconsistent(demand: RoadmapDemand, dependency: DemandDependency) {
  // `demand` depends on `dependency`.
  return isOrderInconsistent(demand, dependency)
}

function isReverseDependencyInconsistent(demand: RoadmapDemand, dep: DemandDependency) {
  // `dep` (its demandId) depends on `demand` — i.e. `demand` blocks `dep`.
  return isOrderInconsistent(
    { id: dep.demandId, quarterYear: dep.quarterYear, quarterNumber: dep.quarterNumber },
    { ...dep, demandId: demand.id, quarterYear: demand.quarterYear, quarterNumber: demand.quarterNumber }
  )
}

function hasInconsistentDependency(demand: RoadmapDemand) {
  return demand.dependsOn.some(dependency => isDependencyInconsistent(demand, dependency))
    || (demand.dependedOnBy ?? []).some(dep => isReverseDependencyInconsistent(demand, dep))
}

function getEpicTypeLabel(epicId?: string): string {
  if (!epicId) return ''
  const epicDemands = demandItems.value.filter(d => d.epicId === epicId)
  if (!epicDemands.length) return ''
  const types = new Set(epicDemands.map(d => d.type))
  if (types.size === 1) return typeLabels[epicDemands[0]!.type]
  return 'Múltiplos'
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

function createFilledSvgIcon(paths: string[], className = 'h-3 w-3 shrink-0') {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg')
  svg.setAttribute('viewBox', '0 0 24 24')
  svg.setAttribute('fill', 'currentColor')
  svg.setAttribute('class', className)

  paths.forEach((pathValue) => {
    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path')
    path.setAttribute('d', pathValue)
    svg.appendChild(path)
  })

  return svg
}

// Lucide "palette" icon (blob outline + 4 color dots) — standard icon for row-color actions.
function createPaletteIcon(className = 'h-4 w-4') {
  const ns = 'http://www.w3.org/2000/svg'
  const svg = document.createElementNS(ns, 'svg')
  svg.setAttribute('viewBox', '0 0 24 24')
  svg.setAttribute('fill', 'none')
  svg.setAttribute('stroke', 'currentColor')
  svg.setAttribute('stroke-width', '2')
  svg.setAttribute('stroke-linecap', 'round')
  svg.setAttribute('stroke-linejoin', 'round')
  svg.setAttribute('class', className)

  const blob = document.createElementNS(ns, 'path')
  blob.setAttribute('d', 'M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2')
  svg.appendChild(blob)

  for (const [cx, cy] of [['13.5', '6.5'], ['17.5', '10.5'], ['6.5', '12.5'], ['8.5', '7.5']] as const) {
    const dot = document.createElementNS(ns, 'circle')
    dot.setAttribute('cx', cx)
    dot.setAttribute('cy', cy)
    dot.setAttribute('r', '.5')
    dot.setAttribute('fill', 'currentColor')
    dot.setAttribute('stroke', 'none')
    svg.appendChild(dot)
  }

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
    : getDependencyTooltip('Bloqueia', dependency)
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
  // Single compact line — avoids increasing row height
  row.className = 'mt-0.5 flex min-w-0 items-center gap-1 overflow-hidden'

  const badge = createEpicDependencyBadge(demand, dependencies[0]!, relation)
  badge.className = badge.className.replace('max-w-[14rem]', 'max-w-[10rem]')
  row.appendChild(badge)

  if (dependencies.length > 1) {
    const more = document.createElement('span')
    more.className = 'shrink-0 text-[10px] text-muted'
    more.textContent = `+${dependencies.length - 1}`
    row.appendChild(more)
  }

  container.appendChild(row)
}

function toggleQuarterFilter(val: string) {
  const idx = filterQuarters.value.indexOf(val)
  if (idx >= 0) filterQuarters.value.splice(idx, 1)
  else filterQuarters.value.push(val)
}

function toggleListProjectFilter(id: string) {
  const idx = filterListProjectIds.value.indexOf(id)
  if (idx >= 0) filterListProjectIds.value.splice(idx, 1)
  else filterListProjectIds.value.push(id)
}

function isBacklogDemand(demand: RoadmapDemand): boolean {
  return isSpecialBacklogQuarter(demand.quarterYear, demand.quarterNumber)
}

function isAdditionalDemand(demand: Pick<RoadmapDemand, 'type'>): boolean {
  return demand.type === 'Additional'
}

function getDemandGroupKey(demand: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'type'>): 'regular' | 'additional' {
  if (isAdditionalDemand(demand)) return 'additional'
  return 'regular'
}

function compareListDemandGroups(left: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'type'>, right: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber' | 'type'>) {
  // Inconsistent epics (composite without demands) always sort after everything else.
  const leftInconsistent = emptyCompositeEpicIds.value.has((left as RoadmapDemand).id)
  const rightInconsistent = emptyCompositeEpicIds.value.has((right as RoadmapDemand).id)
  if (leftInconsistent !== rightInconsistent) return leftInconsistent ? 1 : -1

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

    // When grouped by epic, sort by the epic's best (minimum) sortOrder first so
    // all demands of the same epic stay contiguous regardless of their individual
    // sortOrder values relative to demands from other epics.
    if (groupDemandsByEpic.value) {
      const epicA = rowA.original.epicId
      const epicB = rowB.original.epicId
      if (epicA && epicB && epicA !== epicB) {
        // Use the epic's minimum sortOrder within THIS quarter (rows compared here are already
        // in the same quarter), so a cross-quarter demand can't drag the epic's position.
        const minA = epicMinSortOrderById.value[getEpicQuarterOrderKey(rowA.original)] ?? rowA.original.sortOrder
        const minB = epicMinSortOrderById.value[getEpicQuarterOrderKey(rowB.original)] ?? rowB.original.sortOrder
        if (minA !== minB) return minA - minB
        // Tiebreaker: keep all demands of the same epic contiguous even when two
        // epics (from different projects) share the same minimum sortOrder.
        return epicA < epicB ? -1 : 1
      }
    }

    return compareWithinGroup(rowA.original, rowB.original)
  }
}

// ─── Filters ──────────────────────────────────────────────────────────────────
const filterProducts = ref<string[]>([])

const demandTypes: DemandType[] = ['Planned', 'Spillover', 'Unplanned', 'Additional']
const demandClassifications: DemandClassification[] = [
  'TechnicalDebtSecurity', 'Strategic', 'Evolution', 'ImprovementGap', 'Mandatory', 'Homologation', 'Customizacao'
]

const selectedProjectProducts = computed(() => {
  const projectId = filterListProjectIds.value.length === 1 ? filterListProjectIds.value[0] : null
  return projects.value.find(p => p.id === projectId)?.products ?? []
})

const activeCapacityScope = computed(() => {
  if (filterListProjectIds.value.length !== 1 || filterQuarters.value.length !== 1) return null

  const { quarterYear, quarterNumber } = parseQuarterValue(filterQuarters.value[0]!)
  if (isSpecialBacklogQuarter(quarterYear, quarterNumber)) return null

  return {
    projectId: filterListProjectIds.value[0]!,
    quarterYear,
    quarterNumber,
    quarterLabel: quarterShortLabel(filterQuarters.value[0]!)
  }
})

// Motivo do bloqueio do botão "Capacity" (exibido como tooltip). Vazio quando editável.
const capacityDisabledReason = computed(() => {
  if (activeCapacityScope.value) return ''

  const reasons: string[] = []

  if (filterListProjectIds.value.length === 0)
    reasons.push('selecione um time')
  else if (filterListProjectIds.value.length > 1)
    reasons.push('selecione apenas um time (há mais de um selecionado)')

  if (filterQuarters.value.length === 0) {
    reasons.push('selecione um quarter')
  }
  else if (filterQuarters.value.length > 1) {
    reasons.push('selecione apenas um quarter (há mais de um selecionado)')
  }
  else {
    const { quarterYear, quarterNumber } = parseQuarterValue(filterQuarters.value[0]!)
    if (isSpecialBacklogQuarter(quarterYear, quarterNumber))
      reasons.push('o Backlog / Backlog prioritário não possui capacity')
  }

  return `Configurar capacity indisponível: ${reasons.join('; ')}.`
})

const capacityProjectName = computed(() =>
  projects.value.find(p => p.id === activeCapacityScope.value?.projectId)?.name ?? 'Projeto'
)

const selectedDemandScope = computed(() => {
  if (filterQuarters.value.length !== 1) return null

  const { quarterYear, quarterNumber } = parseQuarterValue(filterQuarters.value[0]!)

  return {
    quarterYear,
    quarterNumber
  }
})

const quarterScopedDemands = computed(() => {
  if (!activeCapacityScope.value) return []

  const scope = activeCapacityScope.value!
  const demands = demandItems.value.filter(d =>
    d.projectId === scope.projectId
    && d.quarterYear === scope.quarterYear
    && d.quarterNumber === scope.quarterNumber
  )
  const simpleEpics = epicItems.value.filter(e =>
    e.isSimple
    && (e.projectIds ?? []).includes(scope.projectId)
    && e.quarterYear === scope.quarterYear
    && e.quarterNumber === scope.quarterNumber
  )
  return [...demands, ...simpleEpics]
})

const capacityScopedDemands = computed(() =>
  quarterScopedDemands.value.filter(demand => demand.status !== 'Deprioritized')
)

const displayCapacitySummary = computed<RoadmapCapacitySummary | null>(() => {
  if (!activeCapacityScope.value) return null

  const committedHours = capacityScopedDemands.value
    .filter(demand => demand.type !== 'Additional' && !demand.excludeFromCapacity)
    .reduce((total, demand) => total + (demand.hours ?? 0), 0)

  const additionalHours = capacityScopedDemands.value
    .filter(demand => demand.type === 'Additional' && !demand.excludeFromCapacity)
    .reduce((total, demand) => total + (demand.hours ?? 0), 0)

  const nonEstimatedDemandCount = capacityScopedDemands.value
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
  clearTypeFilterPart()
  clearTitleClassifications()
  setListMultiFilter('products', [])
  setListMultiFilter('customers', [])
  filterSpilloverReasons.value = []
  filterDeprioritizationReasons.value = []
})

watch(filterListProjectIds, (val) => {
  filterProducts.value = []
  setListMultiFilter('status', [])
  clearTypeFilterPart()
  clearTitleClassifications()
  setListMultiFilter('products', [])
  setListMultiFilter('customers', [])
  filterSpilloverReasons.value = []
  filterDeprioritizationReasons.value = []
  localStorage.setItem(CACHE_KEY_PLANNING_PROJECTS, JSON.stringify(val))
})

watch(filterQuarters, (val) => {
  localStorage.setItem(CACHE_KEY_PLANNING_QUARTERS, JSON.stringify(val))
})

// Composite epics that have no demands linked. The planning list is built from demands
// (which carry the quarter), so these epics would otherwise disappear. We surface them at
// the end under an "inconsistent epics" group — but only when the quarter filter is
// "Todos os quarters" or one of the Backlog quarters (they have no real quarter to place them).
const emptyCompositeEpics = computed(() => {
  const allowsInconsistentEpics = filterQuarters.value.length === 0
    || filterQuarters.value.includes(BACKLOG_QUARTER.value)
    || filterQuarters.value.includes(PRIORITIZED_BACKLOG_QUARTER.value)
  if (!allowsInconsistentEpics)
    return []

  const epicIdsWithDemands = new Set(
    demandItems.value.map(demand => demand.epicId).filter((id): id is string => !!id)
  )
  let list = epicItems.value.filter(epic => !epic.isSimple && !epicIdsWithDemands.has(epic.id))
  if (filterListProjectIds.value.length)
    list = list.filter(epic => (epic.projectIds ?? []).some(pid => filterListProjectIds.value.includes(pid)))
  return list
})
const emptyCompositeEpicIds = computed(() => new Set(emptyCompositeEpics.value.map(epic => epic.id)))

const quarterFilteredDemands = computed(() => {
  const simpleEpics = epicItems.value.filter(e => e.isSimple)
  const allItems = [...demandItems.value, ...simpleEpics]
  const orderedItems = allItems.sort((left, right) => {
    const groupComparison = compareListDemandGroups(left, right)
    if (groupComparison !== 0)
      return groupComparison

    return left.sortOrder - right.sortOrder
  })
  const projectFiltered = filterListProjectIds.value.length
    ? orderedItems.filter(d => {
        if (d.itemType === 'Epic')
          return (d.projectIds ?? []).some(pid => filterListProjectIds.value.includes(pid))
        return filterListProjectIds.value.includes(d.projectId ?? '')
      })
    : orderedItems
  if (!filterQuarters.value.length) return projectFiltered
  // Use buildQuarterValue so the special Backlog quarters ('backlog' / 'backlog-prioritario')
  // match the filter option values — a raw `${number}-${year}` would never match them.
  return projectFiltered.filter(d =>
    filterQuarters.value.includes(buildQuarterValue(d.quarterYear, d.quarterNumber))
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
// True while a priority drag is in progress — used to defer the idle/soft refresh.
const isListDragging = ref(false)

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

function getEffectiveProjectId(item: Pick<RoadmapDemand, 'projectId' | 'projectIds' | 'isSimple'>) {
  return item.projectId ?? (item.isSimple ? item.projectIds?.[0] : undefined) ?? null
}

function isSameDemandScope(
  left: Pick<RoadmapDemand, 'projectId' | 'projectIds' | 'isSimple' | 'quarterYear' | 'quarterNumber'>,
  right: Pick<RoadmapDemand, 'projectId' | 'projectIds' | 'isSimple' | 'quarterYear' | 'quarterNumber'>
) {
  return getEffectiveProjectId(left) === getEffectiveProjectId(right)
    && left.quarterYear === right.quarterYear
    && left.quarterNumber === right.quarterNumber
}

function isSameDemandGroup(
  left: Pick<RoadmapDemand, 'projectId' | 'projectIds' | 'isSimple' | 'quarterYear' | 'quarterNumber' | 'type'>,
  right: Pick<RoadmapDemand, 'projectId' | 'projectIds' | 'isSimple' | 'quarterYear' | 'quarterNumber' | 'type'>
) {
  return isSameDemandScope(left, right)
    && getDemandGroupKey(left) === getDemandGroupKey(right)
}

function getScopedDemandIds(demand: RoadmapDemand) {
  const simpleEpics = epicItems.value.filter(e => e.isSimple)
  return [...demandItems.value, ...simpleEpics]
    .filter(item => isSameDemandScope(item, demand))
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map(item => item.id)
}

function getDemandScopeKey(demand: Pick<RoadmapDemand, 'projectId' | 'projectIds' | 'isSimple' | 'quarterYear' | 'quarterNumber' | 'type'>) {
  return `${getEffectiveProjectId(demand)}:${demand.quarterYear}:${demand.quarterNumber}:${getDemandGroupKey(demand)}`
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

  if (status === 'Blocked' && !demand.blockedReason) {
    toast.add({ title: 'Informe o motivo do impedimento antes de alterar o status', color: 'warning' })
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

// Resolves which quarter a dropped row now belongs to by walking up the DOM. The first
// data row or quarter/additional divider carrying a quarterKey marks the row's section.
// This disambiguates dropping at the end of a quarter (just above the next quarter's
// divider) from dropping as the first item of the next quarter — flat index math can't.
function resolveDropQuarterKey(item: HTMLElement): string | null {
  let el = item.previousElementSibling as HTMLElement | null
  while (el) {
    if ((el.classList.contains('list-demand-row') || el.classList.contains('list-section-divider'))
      && el.dataset.quarterKey)
      return el.dataset.quarterKey
    el = el.previousElementSibling as HTMLElement | null
  }

  // Top of the list: fall back to the nearest following data row.
  el = item.nextElementSibling as HTMLElement | null
  while (el) {
    if (el.classList.contains('list-demand-row') && el.dataset.quarterKey)
      return el.dataset.quarterKey
    el = el.nextElementSibling as HTMLElement | null
  }

  return null
}

function parseQuarterKey(quarterKey: string | null): { quarterYear: number, quarterNumber: number } | null {
  if (!quarterKey)
    return null
  const [quarterYear, quarterNumber] = quarterKey.split(':').map(Number)
  return Number.isFinite(quarterYear) && Number.isFinite(quarterNumber)
    ? { quarterYear: quarterYear!, quarterNumber: quarterNumber! }
    : null
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
  // The DOM walk-back is the most reliable signal of the quarter the row was dropped into:
  // it disambiguates "last row of a quarter" from "first row of the next quarter", which the
  // onMove-provided targetQuarterKey gets wrong at section boundaries.
  const walkedQuarter = parseQuarterKey(resolveDropQuarterKey(item))
  const targetQuarterRef = walkedQuarter ?? targetQuarter ?? (fallbackQuarterRef
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

    // Placement among the TARGET quarter's items. Prefer the epic headers (visible even when the
    // groups are collapsed), so the dropped item keeps the priority position where it landed. Fall
    // back to the demand-row neighbors (works when groups are expanded / in non-grouped contexts).
    // Computed for BOTH simple and composite epics so each lands at the dropped priority.
    const placementHeaderRows = Array.from(tbody.querySelectorAll('.list-epic-divider')) as HTMLElement[]
    const placementDraggedIndex = placementHeaderRows.indexOf(item as HTMLElement)

    const targetEpicBoundaryId = (headerRow: HTMLElement, edge: 'first' | 'last'): string | null => {
      const headerAnchorId = headerRow.dataset.anchorDemandId
      if (!headerAnchorId)
        return null
      const headerAnchor = visibleListRows.value.find(row => row.id === headerAnchorId)
      if (!headerAnchor || headerAnchor.epicId === anchorDemand.epicId)
        return null
      if (getEffectiveProjectId(headerAnchor) !== getEffectiveProjectId(anchorDemand))
        return null
      if (headerAnchor.quarterYear !== targetQuarterRef.quarterYear || headerAnchor.quarterNumber !== targetQuarterRef.quarterNumber)
        return null
      if (getDemandGroupKey(headerAnchor) !== getDemandGroupKey(anchorDemand))
        return null
      const cluster = (headerAnchor.itemType === 'Epic' && headerAnchor.isSimple)
        ? [headerAnchor]
        : getVisibleEpicDemandCluster(headerAnchor)
      if (!cluster.length)
        return null
      return edge === 'first' ? cluster[0]!.id : cluster[cluster.length - 1]!.id
    }

    let headerBeforeId: string | null = null
    if (placementDraggedIndex >= 0)
      for (let index = placementDraggedIndex + 1; index < placementHeaderRows.length && !headerBeforeId; index++)
        headerBeforeId = targetEpicBoundaryId(placementHeaderRows[index]!, 'first')

    let headerAfterId: string | null = null
    if (placementDraggedIndex >= 0)
      for (let index = placementDraggedIndex - 1; index >= 0 && !headerAfterId; index--)
        headerAfterId = targetEpicBoundaryId(placementHeaderRows[index]!, 'last')

    const demandBeforeId = targetDemand && sameGroupInTarget(targetDemand) && !targetInsertAfter
      ? targetDemand.id
      : (nextDemand && sameGroupInTarget(nextDemand) ? nextDemand.id : null)
    const demandAfterId = demandBeforeId
      ? null
      : (targetDemand && sameGroupInTarget(targetDemand) && targetInsertAfter
          ? targetDemand.id
          : (previousDemand && sameGroupInTarget(previousDemand) ? previousDemand.id : null))

    const beforeId = headerBeforeId ?? demandBeforeId
    const afterId = beforeId ? null : (headerAfterId ?? demandAfterId)

    // Simple epics carry their own quarter: move it via updateDemand, then place it at the drop
    // position (same ordering scope as demands). Empty composite epics have nothing to move.
    if (anchorDemand.itemType === 'Epic') {
      if (anchorDemand.isSimple) {
        const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
        const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
        isBulkPlanning.value = true
        try {
          const updatedEpic = await roadmapStore.updateDemand(
            anchorDemand.id,
            buildDemandFormData(anchorDemand, {
              quarterYear: targetQuarterRef.quarterYear,
              quarterNumber: targetQuarterRef.quarterNumber
            })
          )
          await persistDemandPriority(updatedEpic, updatedEpic.status, beforeId, afterId)
          await refreshListPresentation(listScrollTop, listScrollLeft)
        }
        catch { /* handled by useApi */ }
        finally { isBulkPlanning.value = false }
      }
      return
    }

    await planEpicDemandsToQuarter(
      // Only this quarter's demands of the epic move (the source-quarter cluster) — an epic that
      // also has demands in other quarters keeps those untouched.
      currentCluster.map(clusterDemand => clusterDemand.id),
      // Use buildQuarterValue so backlog quarters serialize to their special tokens
      // ('backlog' / 'backlog-prioritario'); a raw "${number}-${year}" breaks for the
      // prioritized backlog (number -1 → "-1-0") and yields an invalid quarter.
      buildQuarterValue(targetQuarterRef.quarterYear, targetQuarterRef.quarterNumber),
      { beforeId, afterId }
    )
    return
  }

  // Empty composite epics (sem demanda) can't be reordered within a quarter: skip silently.
  if (anchorDemand.itemType === 'Epic' && !anchorDemand.isSimple)
    return

  // Same-quarter reorder in grouped mode. Dragging an epic header only moves that header row in
  // the DOM (its demand rows stay put), so we read the new position from the order of the epic
  // headers (.list-epic-divider). The dragged "unit" (a simple epic = itself; a composite epic =
  // its demand cluster) is moved within the authoritative scope list via moveDemandCluster, which
  // preserves the set — so the backend never rejects on a scope mismatch.
  const scopedIds = getScopedDemandIds(anchorDemand)
  const scopedIdSet = new Set(scopedIds)

  const unitScopedIds = (epicAnchor: RoadmapDemand) => {
    const ids = (epicAnchor.itemType === 'Epic' && epicAnchor.isSimple)
      ? [epicAnchor.id]
      : getVisibleEpicDemandCluster(epicAnchor).map(demand => demand.id)
    return ids.filter(id => scopedIdSet.has(id))
  }

  const headerUnitScopedIds = (headerRow: HTMLElement) => {
    const headerAnchorId = headerRow.dataset.anchorDemandId
    if (!headerAnchorId)
      return []
    const headerAnchor = visibleListRows.value.find(demand => demand.id === headerAnchorId)
    return headerAnchor ? unitScopedIds(headerAnchor) : []
  }

  const draggedUnitIds = unitScopedIds(anchorDemand)
  const headerRows = Array.from(tbody.querySelectorAll('.list-epic-divider')) as HTMLElement[]
  const draggedHeaderIndex = headerRows.indexOf(item as HTMLElement)
  if (draggedHeaderIndex < 0 || !draggedUnitIds.length)
    return

  const draggedUnitSet = new Set(draggedUnitIds)

  let beforeId: string | null = null
  for (let index = draggedHeaderIndex + 1; index < headerRows.length && !beforeId; index++) {
    const unit = headerUnitScopedIds(headerRows[index]!)
    if (unit.length && !draggedUnitSet.has(unit[0]!))
      beforeId = unit[0]!
  }

  let afterId: string | null = null
  for (let index = draggedHeaderIndex - 1; index >= 0 && !afterId; index--) {
    const unit = headerUnitScopedIds(headerRows[index]!)
    if (unit.length && !draggedUnitSet.has(unit[unit.length - 1]!))
      afterId = unit[unit.length - 1]!
  }

  const orderedDemandIds = moveDemandCluster(scopedIds, draggedUnitIds, beforeId, afterId)

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

  // Determine target quarter from the dropped row's real DOM position. The walk-back reads
  // the quarter of the nearest data row / quarter divider above the drop point, which
  // correctly resolves "last row of a quarter" (just above the next quarter's divider)
  // instead of wrongly switching to the next quarter. Fall back to the flat-index neighbor.
  const fallbackQuarterRow = remainingRows[newIndex] ?? (newIndex > 0 ? remainingRows[newIndex - 1] : null)
  const targetQuarter = parseQuarterKey(resolveDropQuarterKey(item))
    ?? (fallbackQuarterRow
      ? { quarterYear: fallbackQuarterRow.quarterYear, quarterNumber: fallbackQuarterRow.quarterNumber }
      : null)

  if (!targetQuarter) {
    await forceListRerender()
    return
  }

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
        description: `${demand.title} movida para ${quarterShortLabel(buildQuarterValue(targetQuarter.quarterYear, targetQuarter.quarterNumber))}`,
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

  // Sem permissão de edição: não habilita o arrastar para repriorizar.
  if (!canEditRoadmap.value) return

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
    onStart: () => {
      isListDragging.value = true
    },
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
      const scrollTop = listScrollContainerRef.value?.scrollTop ?? null
      const scrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
      const draggedItem = event.item as HTMLElement | null

      try {
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
          await handleListSortEnd(draggedItem)
      }
      finally {
        isListDragging.value = false
        // Always rebuild the DOM from the model after a drop. Successful reorders self-heal
        // via the data-hash watcher, but no-op drops and early returns would otherwise leave
        // SortableJS's DOM mutation stuck on screen.
        await forceListRerender(scrollTop, scrollLeft)
      }
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

    const cols = listOrderedCols.value
    const existingCols = Array.from(colgroup.children) as HTMLTableColElement[]

    cols.forEach((col, index) => {
      const width = listColWidth(col.id, col.defaultWidth)
      if (existingCols[index]) {
        existingCols[index].style.width = width
      }
      else {
        const colEl = document.createElement('col')
        colEl.style.width = width
        colgroup!.appendChild(colEl)
      }
    })

    while (colgroup.children.length > cols.length)
      colgroup.removeChild(colgroup.lastChild!)
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

    const isCollapsedRow = groupDemandsByEpic.value && !!demand.epicId
      && collapsedEpicIds.value.includes(epicQuarterKey(demand.epicId, demand.quarterYear, demand.quarterNumber))
    // Epic rows (simple epics and empty composite epics) in grouped mode are represented by
    // their group header; hide the redundant table row.
    const isEpicHeaderRow = groupDemandsByEpic.value && demand.itemType === 'Epic'
    const isHiddenRow = isCollapsedRow || isEpicHeaderRow
    row.dataset.demandId = demand.id
    row.dataset.dragScopeKey = getDemandDragScopeKey(demand)
    if (isHiddenRow)
      row.style.setProperty('display', 'none', 'important')
    else
      row.style.removeProperty('display')
    row.hidden = isHiddenRow
    row.classList.toggle('hidden', isHiddenRow)

    if (isHiddenRow) {
      row.classList.remove('list-demand-row')
      delete row.dataset.scopeKey
      delete row.dataset.quarterKey
    }
    else {
      row.classList.add('list-demand-row')
      row.dataset.scopeKey = getDemandScopeKey(demand)
      row.dataset.quarterKey = `${demand.quarterYear}:${demand.quarterNumber}`
    }

    const isGroupedDemandRow = groupDemandsByEpic.value && !!demand.epicId
    row.classList.toggle('bg-elevated/5', isGroupedDemandRow)
    row.classList.toggle('hover:bg-elevated/15', isGroupedDemandRow)
    const rowColorRgba = demand.rowColor
      ? (LIST_ROW_COLORS.find(c => c.id === demand.rowColor)?.rgba ?? null)
      : null
    row.style.backgroundColor = rowColorRgba ?? ''

    for (const cell of Array.from(row.children) as HTMLTableCellElement[]) {
      cell.classList.toggle('bg-elevated/5', isGroupedDemandRow)
      cell.classList.toggle('hover:bg-elevated/15', isGroupedDemandRow)
      cell.style.backgroundColor = rowColorRgba ?? ''
    }
  })

  const distinctQuarters = new Set(visibleRows.map(d => `${d.quarterYear}:${d.quarterNumber}`))
  const multipleQuarters = distinctQuarters.size > 1

  const dividerConfigs: Array<
    { rowIndex: number, label: string, kind: 'quarter' | 'additional' | 'inconsistent' }
    | { rowIndex: number, kind: 'epic', count: number, epicId?: string, roadmapTitle?: string | null, epicTitle?: string | null, collapsed: boolean }
  > = []
  let prevQuarterKey = ''
  let inconsistentSectionAdded = false

  for (let i = 0; i < visibleRows.length; i++) {
    const demand = visibleRows[i]!
    const quarterKey = `${demand.quarterYear}:${demand.quarterNumber}`
    const groupKey = getDemandGroupKey(demand)
    const isNewQuarter = quarterKey !== prevQuarterKey
    const epicHeader = visibleEpicHeaderByDemandId.value[demand.id]

    // Empty composite epics form their own "inconsistent" group at the very end: emit a
    // single section header before the first one, then their epic headers — skipping the
    // quarter/additional dividers (their backlog quarter is irrelevant here).
    if (emptyCompositeEpicIds.value.has(demand.id)) {
      if (!inconsistentSectionAdded) {
        dividerConfigs.push({ rowIndex: i, label: 'Épicos inconsistentes - sem demanda', kind: 'inconsistent' })
        inconsistentSectionAdded = true
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

      continue
    }

    if (isNewQuarter) {
      prevQuarterKey = quarterKey

      if (multipleQuarters) {
        const label = demand.quarterLabel
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
          const quarterLabel = demand.quarterLabel
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

    // Carry the section's quarter so drop-target resolution can tell which quarter a row
    // dropped against a divider belongs to (fixes dropping at the end of a quarter).
    const dividerDemandRef = visibleRows[config.rowIndex]
    if (dividerDemandRef && (kind === 'quarter' || kind === 'additional'))
      dividerRow.dataset.quarterKey = `${dividerDemandRef.quarterYear}:${dividerDemandRef.quarterNumber}`

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
    else if (kind === 'inconsistent') {
      dividerCell.className = 'border-y-2 border-red-300/50 bg-red-50/60 px-3 py-2.5 text-center text-[11px] font-semibold uppercase tracking-[0.14em] text-red-700 shadow-sm dark:border-red-800/50 dark:bg-red-950/20 dark:text-red-300'
      const labelSpan = document.createElement('span')
      labelSpan.textContent = config.label
      const hintSpan = document.createElement('span')
      hintSpan.className = 'ml-1 font-normal normal-case lowercase tracking-normal text-red-600/80 dark:text-red-300/80'
      hintSpan.textContent = ' (crie uma demanda ou transforme-o em épico simples para planejá-lo)'
      dividerCell.append(labelSpan, hintSpan)
    }
    else {
      const headerMeta = getEpicHeaderMeta(visibleRows[config.rowIndex])
      const anchorDemand = visibleRows[config.rowIndex]
      // Distinguishes this header instance when the same epic shows in multiple quarter sections,
      // so inline editing activates only on the clicked instance (not all of them).
      const epicScopeKey = anchorDemand ? `${anchorDemand.quarterYear}:${anchorDemand.quarterNumber}` : ''
      dividerRow.className = 'list-section-divider list-epic-divider border-y border-default bg-default'
      dividerRow.dataset.anchorDemandId = anchorDemand?.id ?? ''
      dividerRow.dataset.scopeKey = anchorDemand
        ? (anchorDemand.itemType === 'Epic' && anchorDemand.isSimple
          ? `${anchorDemand.projectIds?.[0] ?? anchorDemand.projectId}:${anchorDemand.quarterYear}:${anchorDemand.quarterNumber}:${getDemandGroupKey(anchorDemand)}`
          : getDemandScopeKey(anchorDemand))
        : ''
      dividerRow.dataset.dragScopeKey = anchorDemand ? getDemandDragScopeKey(anchorDemand) : ''
      dividerRow.dataset.quarterKey = anchorDemand ? `${anchorDemand.quarterYear}:${anchorDemand.quarterNumber}` : ''

      const fullRowCell = document.createElement('td')
      fullRowCell.colSpan = listOrderedCols.value.length
      fullRowCell.className = 'p-0'

      const grid = document.createElement('div')
      grid.className = 'grid items-start bg-default'
      // Épico despriorizado: linha inteira com transparência (mantém o tachado no título).
      if (headerMeta && getPlanningDraftDisplayItem(headerMeta.epic).status === 'Deprioritized')
        grid.className += ' opacity-50'
      grid.style.gridTemplateColumns = getListGridTemplateColumns()

      const createGridCell = (className = 'px-3 py-0.5 align-top') => {
        const cell = document.createElement('div')
        cell.className = className
        return cell
      }

      for (const column of listOrderedCols.value) {
        if (column.id === 'priority') {
          const cell = createGridCell('px-2 py-1 align-top')

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
              checkbox.checked = isEpicSelected(config.epicId, anchorDemand?.quarterYear, anchorDemand?.quarterNumber)
              checkbox.addEventListener('click', event => event.stopPropagation())
              checkbox.addEventListener('change', (event) => {
                toggleEpicSelection(config.epicId!, (event.target as HTMLInputElement).checked, anchorDemand?.quarterYear, anchorDemand?.quarterNumber)
              })
              handleWrap.appendChild(checkbox)
            }

            cell.appendChild(handleWrap)
          }

          grid.appendChild(cell)
          continue
        }

        if (column.id === 'quarterLabel') {
          const cell = createGridCell('px-3 py-1 align-top')

          if (anchorDemand) {
            const isSimpleEpicHeader = anchorDemand.itemType === 'Epic' && anchorDemand.isSimple
            const epic = isSimpleEpicHeader ? anchorDemand : null

            if (isSimpleEpicHeader && epic && isPlanningCellEditing(epic, 'quarterType', epicScopeKey)) {
              const container = document.createElement('div')
              container.className = 'relative z-20 flex min-w-0 flex-col gap-1'
              container.style.width = '18rem'
              container.style.maxWidth = '18rem'

              const quarterSelect = document.createElement('select')
              quarterSelect.className = 'min-w-0 rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted outline-none transition-colors focus:border-primary/40'
              quarterSelect.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value
              planningQuarterOptions.value.forEach((option) => {
                const optionNode = document.createElement('option')
                optionNode.value = option.value
                optionNode.textContent = option.label
                optionNode.selected = option.value === getPlanningInlineDraft(epic).quarterValue
                quarterSelect.appendChild(optionNode)
              })
              quarterSelect.addEventListener('click', event => event.stopPropagation())
              quarterSelect.addEventListener('change', (event) => {
                updatePlanningInlineDraft(epic, { quarterValue: (event.target as HTMLSelectElement).value })
                schedulePlanningGroupedHeaderSync()
              })

              const typeSelect = document.createElement('select')
              typeSelect.className = 'min-w-0 rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted outline-none transition-colors focus:border-primary/40'
              typeSelect.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value
              ;[
                { label: 'Planejado', value: 'Planned' },
                { label: 'Transbordo', value: 'Spillover' },
                { label: 'Não Planejado', value: 'Unplanned' },
                { label: 'Adicional', value: 'Additional' }
              ].forEach((option) => {
                const optionNode = document.createElement('option')
                optionNode.value = option.value
                optionNode.textContent = option.label
                optionNode.selected = option.value === getPlanningInlineDraft(epic).type
                typeSelect.appendChild(optionNode)
              })
              typeSelect.addEventListener('click', event => event.stopPropagation())
              typeSelect.addEventListener('blur', () => {
                deactivatePlanningCell(epic.id, 'quarterType')
                schedulePlanningGroupedHeaderSync()
              })
              typeSelect.addEventListener('change', (event) => {
                updatePlanningInlineDraft(epic, { type: (event.target as HTMLSelectElement).value as DemandType })
                schedulePlanningGroupedHeaderSync()
              })

              container.appendChild(quarterSelect)
              container.appendChild(typeSelect)
              cell.appendChild(container)
              requestAnimationFrame(() => quarterSelect.focus())
            }
            else {
              const displayItem = isSimpleEpicHeader && epic ? getPlanningDraftDisplayItem(epic) : anchorDemand
              const quarterNode = document.createElement('span')
              quarterNode.className = isSpecialBacklogQuarter(displayItem.quarterYear, displayItem.quarterNumber)
                ? 'text-[9px] font-semibold uppercase tracking-[0.08em] text-highlighted'
                : 'text-[10px] font-mono text-highlighted'
              quarterNode.textContent = planningQuarterDisplayLabel(displayItem)

              const typeLabel = isSimpleEpicHeader && epic
                ? typeLabels[getPlanningInlineDraft(epic).type]
                : getEpicTypeLabel(anchorDemand.epicId)
              const typeNode = document.createElement('span')
              typeNode.className = `whitespace-nowrap text-[10px] font-medium ${typeLabel === 'Múltiplos' ? 'text-muted' : typeColors[displayItem.type]}`
              typeNode.textContent = typeLabel

              const wrap = document.createElement('div')

              if (isSimpleEpicHeader && epic) {
                wrap.className = `flex min-w-0 flex-col gap-0.5 rounded-md border px-1 py-0.5 transition-colors ${getPlanningEditableCellButtonClass(epic)}`
                wrap.style.cursor = 'pointer'
                wrap.addEventListener('click', (event) => {
                  event.preventDefault()
                  event.stopPropagation()
                  activatePlanningCell(epic, 'quarterType', epicScopeKey)
                  schedulePlanningGroupedHeaderSync()
                })
              }
              else {
                // Match the demand quarter cell's box (border + padding) so the epic header
                // quarter aligns horizontally with the quarter of its demand rows.
                wrap.className = 'flex min-w-0 flex-col gap-0.5 rounded-md border border-transparent px-1 py-0.5'
              }

              wrap.append(quarterNode, typeNode)
              cell.appendChild(wrap)
            }
          }

          grid.appendChild(cell)
          continue
        }

        if (column.id === 'title') {
          const cell = createGridCell('px-3 py-1 align-top')
          const titleWrap = document.createElement('div')
          titleWrap.className = 'flex min-w-0 items-start gap-1.5'

          let toggleButton: HTMLButtonElement | null = null

          // Simple epics have no demands to collapse — skip toggle button
          if (config.epicId && !headerMeta?.epic.isSimple) {
            toggleButton = document.createElement('button')
            toggleButton.type = 'button'
            toggleButton.className = 'inline-flex h-4 w-4 shrink-0 items-center justify-center rounded border border-default bg-default text-muted transition-colors hover:text-highlighted'
            toggleButton.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              toggleEpicCollapse(config.epicId, anchorDemand?.quarterYear, anchorDemand?.quarterNumber)
            })

            const toggleIcon = document.createElement('span')
            toggleIcon.textContent = config.collapsed ? '▸' : '▾'
            toggleButton.appendChild(toggleIcon)
          }

          const scopeWrap = document.createElement('div')
          scopeWrap.className = 'min-w-0 flex-1'

          const metaRow = document.createElement('div')
          metaRow.className = 'flex min-w-0 items-center gap-1.5'
          const displayEpic = headerMeta ? getPlanningDraftDisplayItem(headerMeta.epic) : null

          // Toggle button goes first in the roadmap meta row so ▶ aligns with ★ below
          if (toggleButton)
            metaRow.appendChild(toggleButton)

          const roadmapTitleLabel = headerMeta?.epic.roadmapTitle ?? config.roadmapTitle
          if (roadmapTitleLabel) {
            const roadmapLabel = document.createElement('div')
            // Não usar flex-1: assim o contador fica logo após o título (e não empurrado à direita).
            roadmapLabel.className = 'min-w-0 max-w-[60%] shrink truncate text-[10px] text-muted'
            roadmapLabel.textContent = roadmapTitleLabel
            metaRow.appendChild(roadmapLabel)
          }

          if (!headerMeta?.epic.isSimple) {
            const countNode = document.createElement('span')
            countNode.className = 'inline-flex shrink-0 items-center rounded-md border border-default bg-elevated px-1.5 py-0.5 text-[9px] font-medium text-muted'
            countNode.textContent = String(config.count)
            metaRow.appendChild(countNode)
          }

          // Editable classification badge for the epic (inline, inside the title meta row).
          if (headerMeta && displayEpic) {
            const epic = headerMeta.epic
            if (isPlanningCellEditing(epic, 'classification', epicScopeKey)) {
              const select = document.createElement('select')
              select.className = 'ml-auto min-w-0 shrink-0 rounded-md border border-default bg-default px-1.5 py-0.5 text-[10px] text-highlighted outline-none transition-colors focus:border-primary/40'
              select.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value
              ;(Object.entries(classificationLabels) as Array<[DemandClassification, string]>).forEach(([value, label]) => {
                const optionNode = document.createElement('option')
                optionNode.value = value
                optionNode.textContent = label
                optionNode.selected = value === getPlanningInlineDraft(epic).classification
                select.appendChild(optionNode)
              })
              select.addEventListener('click', event => event.stopPropagation())
              select.addEventListener('blur', () => {
                deactivatePlanningCell(epic.id, 'classification')
                schedulePlanningGroupedHeaderSync()
              })
              select.addEventListener('change', (event) => {
                updatePlanningInlineDraft(epic, { classification: (event.target as HTMLSelectElement).value as DemandClassification })
              })
              metaRow.appendChild(select)
              requestAnimationFrame(() => select.focus())
            }
            else {
              const classificationBadge = document.createElement('button')
              classificationBadge.type = 'button'
              classificationBadge.className = `ml-auto inline-flex shrink-0 items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium transition-colors ${classificationBadgeClass[displayEpic.classification]} ${getPlanningEditableCellButtonClass(epic)}`
              classificationBadge.textContent = classificationLabels[displayEpic.classification]
              classificationBadge.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value
              classificationBadge.addEventListener('click', (event) => {
                event.preventDefault()
                event.stopPropagation()
                activatePlanningCell(epic, 'classification', epicScopeKey)
                schedulePlanningGroupedHeaderSync()
              })
              metaRow.appendChild(classificationBadge)
            }
          }

          const titleBlock = document.createElement('div')
          titleBlock.className = 'mt-0.5 min-w-0'

          const epicTitleRow = document.createElement('div')
          epicTitleRow.className = 'flex min-w-0 items-center gap-1.5'

          const epicIcon = document.createElement('span')
          epicIcon.className = 'inline-flex h-3.5 w-3.5 shrink-0 items-center justify-center text-amber-500'
          epicIcon.textContent = '★'
          epicTitleRow.appendChild(epicIcon)

          if (headerMeta && isPlanningCellEditing(headerMeta.epic, 'title', epicScopeKey)) {
            const epicTitleInput = document.createElement('input')
            epicTitleInput.type = 'text'
            epicTitleInput.value = getPlanningInlineDraft(headerMeta.epic).title
            epicTitleInput.className = 'min-w-0 w-full flex-1 rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted outline-none transition-colors focus:border-primary/40'
            epicTitleInput.disabled = isPlanningInlineSaving(headerMeta.epic.id) || isSavingAllPlanningInlineEdits.value
            epicTitleInput.addEventListener('click', event => event.stopPropagation())
            epicTitleInput.addEventListener('input', (event) => {
              updatePlanningInlineDraft(headerMeta.epic, { title: (event.target as HTMLInputElement).value })
            })
            epicTitleInput.addEventListener('keydown', (event) => {
              if (event.key === 'Escape' || event.key === 'Enter') {
                event.preventDefault()
                deactivatePlanningCell(headerMeta.epic.id, 'title')
                schedulePlanningGroupedHeaderSync()
              }
            })
            epicTitleRow.appendChild(epicTitleInput)
            requestAnimationFrame(() => {
              epicTitleInput.focus()
              epicTitleInput.select()
            })
          }
          else {
            const epicTitle = document.createElement('button')
            epicTitle.type = 'button'
            epicTitle.className = `min-w-0 w-full flex-1 truncate rounded-md border px-1 py-0.5 text-left text-[12px] font-medium transition-colors ${displayEpic?.status === 'Deprioritized' ? 'line-through text-muted' : 'text-highlighted'} ${headerMeta ? getPlanningEditableCellButtonClass(headerMeta.epic) : ''}`
                  epicTitle.textContent = displayEpic?.title ?? headerMeta?.epic.title ?? config.epicTitle ?? 'Sem épico'
            epicTitle.disabled = !headerMeta || isPlanningInlineSaving(headerMeta.epic.id) || isSavingAllPlanningInlineEdits.value
            if (headerMeta?.epic.description?.trim())
              epicTitle.title = headerMeta.epic.description.trim()
            epicTitle.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              if (!headerMeta)
                return

              activatePlanningCell(headerMeta.epic, 'title', epicScopeKey)
              schedulePlanningGroupedHeaderSync()
            })
            epicTitleRow.appendChild(epicTitle)
          }

          // Problem indicator (same warning icon used on demands).
          if (headerMeta && getDemandProblemKeys(headerMeta.epic).length) {
            const warnSpan = document.createElement('span')
            warnSpan.className = 'inline-flex shrink-0 items-center text-warning'
            warnSpan.title = getDemandProblemTooltip(headerMeta.epic)
            warnSpan.appendChild(createSvgIcon(['M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z', 'M12 9v4', 'M12 17h.01'], 'h-3.5 w-3.5'))
            epicTitleRow.appendChild(warnSpan)
          }

          const issueTrigger = createIssueTriggerElement(headerMeta?.issueLinks ?? [])
          if (issueTrigger) {
            epicTitleRow.appendChild(issueTrigger)
          }
          else {
            const noJira = document.createElement('button')
            noJira.type = 'button'
            noJira.className = 'inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-red-200 bg-default px-1.5 text-[10px] font-medium text-red-500 transition-colors hover:border-red-400 dark:border-red-800 dark:text-red-400'
            noJira.title = 'Sem issue Jira — clique para adicionar'
            if (headerMeta?.epic) {
              noJira.addEventListener('click', (event) => {
                event.preventDefault()
                event.stopPropagation()
                openEditModal(headerMeta.epic, undefined, 'jiraIssue')
              })
            }
            noJira.appendChild(createFilledSvgIcon(['M11.571 11.513H0a5.218 5.218 0 0 0 5.232 5.215h2.13v2.057A5.215 5.215 0 0 0 12.575 24V12.518a1.005 1.005 0 0 0-1.005-1.005zm5.723-5.756H5.736a5.215 5.215 0 0 0 5.215 5.214h2.129v2.058a5.218 5.218 0 0 0 5.215 5.214V6.758a1.001 1.001 0 0 0-1.001-1.001zM23.013 0H11.455a5.215 5.215 0 0 0 5.215 5.215h2.129v2.057A5.215 5.215 0 0 0 24 12.483V1.005A1.001 1.001 0 0 0 23.013 0Z'], 'h-3 w-3'))
            epicTitleRow.appendChild(noJira)
          }

          titleBlock.appendChild(epicTitleRow)

          if (headerMeta) {
            // Dependency icons inline: add after issue trigger
            const createDepIcon = (dep: DemandDependency, relation: 'dependsOn' | 'dependedOnBy') => {
              const inconsistent = relation === 'dependsOn' && isDependencyInconsistent(headerMeta.epic, dep)
              const btn = document.createElement('button')
              btn.type = 'button'
              btn.className = inconsistent
                ? 'inline-flex h-4 w-4 shrink-0 items-center justify-center rounded border border-red-200 bg-red-50 text-red-600 transition-colors hover:border-red-300 hover:bg-red-100 dark:border-red-800 dark:bg-red-900/30 dark:text-red-400'
                : 'inline-flex h-4 w-4 shrink-0 items-center justify-center rounded border border-red-200 bg-red-50 text-red-600 transition-colors hover:border-red-300 hover:bg-red-100 dark:border-red-800 dark:bg-red-900/30 dark:text-red-400'
              btn.title = relation === 'dependsOn'
                ? `${getDependencyTooltip('É bloqueado por', dep)}${inconsistent ? `\n\nInconsistência: a demanda vinculada está em ${dep.quarterLabel}, depois de ${headerMeta.epic.quarterLabel}, ou sem priorização.` : ''}`
                : getDependencyTooltip('Bloqueia', dep)
              const iconSvg = relation === 'dependsOn'
                ? '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="h-2.5 w-2.5"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>'
                : '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="h-2.5 w-2.5"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 9.9-1"/></svg>'
              btn.innerHTML = iconSvg
              btn.addEventListener('click', async (event) => {
                event.preventDefault()
                event.stopPropagation()
                await openDependencyDemand(dep)
              })
              return btn
            }
            for (const dep of headerMeta.epic.dependsOn)
              epicTitleRow.appendChild(createDepIcon(dep, 'dependsOn'))
            for (const dep of headerMeta.epic.dependedOnBy)
              epicTitleRow.appendChild(createDepIcon(dep, 'dependedOnBy'))

            const epicInconsistentDeps = headerMeta.epic.dependsOn.filter(dep => isDependencyInconsistent(headerMeta.epic, dep))
            const epicInconsistentReverseDeps = (headerMeta.epic.dependedOnBy ?? []).filter(dep => isReverseDependencyInconsistent(headerMeta.epic, dep))
            const totalEpicInconsistentCount = epicInconsistentDeps.length + epicInconsistentReverseDeps.length
            if (totalEpicInconsistentCount > 0) {
              const epicInconsistencyTooltip = [
                ...epicInconsistentDeps.map(dep =>
                  `${getDependencyTooltip('É bloqueado por', dep)}\n\nInconsistência: a demanda vinculada está em ${dep.quarterLabel}, depois de ${headerMeta.epic.quarterLabel}, ou sem priorização.`
                ),
                ...epicInconsistentReverseDeps.map(dep =>
                  `${getDependencyTooltip('Bloqueia', dep)}\n\nInconsistência: ${dep.title} está em ${dep.quarterLabel}, antes de ${headerMeta.epic.quarterLabel}.`
                ),
              ].join('\n\n')

              const inconsistencyBanner = document.createElement('div')
              inconsistencyBanner.className = 'mt-0.5 flex items-center gap-1 rounded border border-red-300/70 bg-red-50/60 px-1.5 py-0.5 text-[10px] font-medium text-red-700 dark:border-red-800/50 dark:bg-red-900/15 dark:text-red-300/90'
              inconsistencyBanner.title = epicInconsistencyTooltip

              const warnSvg = document.createElementNS('http://www.w3.org/2000/svg', 'svg')
              warnSvg.setAttribute('viewBox', '0 0 24 24')
              warnSvg.setAttribute('fill', 'none')
              warnSvg.setAttribute('stroke', 'currentColor')
              warnSvg.setAttribute('stroke-width', '2')
              warnSvg.setAttribute('stroke-linecap', 'round')
              warnSvg.setAttribute('stroke-linejoin', 'round')
              warnSvg.setAttribute('class', 'h-3 w-3 shrink-0')
              const warnPath = document.createElementNS('http://www.w3.org/2000/svg', 'path')
              warnPath.setAttribute('d', 'm21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3')
              const warnLine = document.createElementNS('http://www.w3.org/2000/svg', 'line')
              warnLine.setAttribute('x1', '12')
              warnLine.setAttribute('x2', '12')
              warnLine.setAttribute('y1', '9')
              warnLine.setAttribute('y2', '13')
              const warnLine2 = document.createElementNS('http://www.w3.org/2000/svg', 'line')
              warnLine2.setAttribute('x1', '12')
              warnLine2.setAttribute('x2', '12.01')
              warnLine2.setAttribute('y1', '17')
              warnLine2.setAttribute('y2', '17')
              warnSvg.append(warnPath, warnLine, warnLine2)
              inconsistencyBanner.appendChild(warnSvg)

              const bannerText = document.createElement('span')
              bannerText.textContent = 'Dependência inconsistente'
              inconsistencyBanner.appendChild(bannerText)

              const bannerCount = document.createElement('span')
              bannerCount.textContent = `(${totalEpicInconsistentCount})`
              inconsistencyBanner.appendChild(bannerCount)

              titleBlock.appendChild(inconsistencyBanner)
            }
          }

          scopeWrap.append(metaRow, titleBlock)
          titleWrap.append(scopeWrap)
          cell.appendChild(titleWrap)
          grid.appendChild(cell)
          continue
        }

        if (column.id === 'kpis') {
          // Match the demand KPI cell padding (px-3) so the epic KPI aligns with its demands.
          const cell = createGridCell('px-3 py-1 align-top')

          if (headerMeta) {
            const container = document.createElement('div')
            container.className = 'flex min-w-0 flex-col items-start gap-1'

            const button = document.createElement('button')
            button.type = 'button'
            button.className = `inline-flex h-6 max-w-full items-center rounded-md border px-1.5 text-[10px] font-medium transition-colors hover:opacity-80 ${headerMeta.kpiSummary.tone}`
            button.textContent = headerMeta.kpiSummary.label
            button.title = headerMeta.kpiSummary.actionLabel
            button.disabled = !canEditRoadmap.value
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
          const cell = createGridCell('px-3 py-0.5 align-top')
          if (headerMeta) {
            const epic = headerMeta.epic

            if (epic.isSimple) {
              // Simple epics: editable product cell
              const wrapper = document.createElement('div')
              wrapper.className = 'relative inline-flex'

              const draftEntries = getPlanningDraftProductEntries(epic)
              const colWidth = getBaseListColPixelWidth('products', 124)
              const draftDisplay = getAdaptiveInlineListDisplay(draftEntries.map(p => p.label), colWidth, ' · ')

              const triggerBtn = document.createElement('button')
              triggerBtn.type = 'button'
              triggerBtn.className = `inline-flex max-w-full items-center gap-1 rounded-md border px-1 py-0.5 text-[10px] text-highlighted transition-colors ${getPlanningEditableCellButtonClass(epic)}`
              triggerBtn.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value
              if (draftDisplay.items.length) {
                const labelSpan = document.createElement('span')
                labelSpan.className = 'max-w-[120px] truncate'
                labelSpan.textContent = draftDisplay.previewLabel
                triggerBtn.appendChild(labelSpan)
                if (!draftDisplay.allVisible) {
                  const moreSpan = document.createElement('span')
                  moreSpan.className = 'shrink-0 text-muted'
                  moreSpan.textContent = `+${draftDisplay.hiddenCount}`
                  triggerBtn.appendChild(moreSpan)
                }
              }
              else {
                triggerBtn.textContent = '—'
              }
              triggerBtn.addEventListener('click', (event) => {
                event.preventDefault()
                event.stopPropagation()
                activatePlanningCell(epic, 'products', epicScopeKey)
                schedulePlanningGroupedHeaderSync()
              })
              wrapper.appendChild(triggerBtn)

              if (isPlanningCellEditing(epic, 'products', epicScopeKey)) {
                const panel = document.createElement('div')
                panel.className = 'absolute left-0 top-full z-30 mt-1 flex min-w-[14rem] flex-col gap-2 rounded-lg border border-default bg-default p-3 shadow-xl'
                panel.addEventListener('click', event => event.stopPropagation())

                getPlanningEditableProductOptions(epic).forEach((product) => {
                  const label = document.createElement('label')
                  label.className = 'flex items-center gap-2 text-[11px] text-highlighted'
                  const checkbox = document.createElement('input')
                  checkbox.type = 'checkbox'
                  checkbox.className = 'h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary'
                  checkbox.checked = getPlanningInlineDraft(epic).productIds.includes(product.value)
                  checkbox.addEventListener('change', () => {
                    togglePlanningDraftProduct(epic, product.value, checkbox.checked)
                    schedulePlanningGroupedHeaderSync()
                  })
                  const nameSpan = document.createElement('span')
                  nameSpan.className = 'truncate'
                  nameSpan.textContent = product.label
                  label.appendChild(checkbox)
                  label.appendChild(nameSpan)
                  panel.appendChild(label)
                })

                wrapper.appendChild(panel)

                document.addEventListener('click', () => {
                  deactivatePlanningCell(epic.id, 'products')
                  schedulePlanningGroupedHeaderSync()
                }, { once: true })
              }

              cell.appendChild(wrapper)
            }
            else {
              // Regular epics: read-only aggregate. Wrap in a box that mirrors the demand /
              // simple-epic product cell (border + padding) so the column stays aligned.
              const products = headerMeta.products
              const colWidth = getBaseListColPixelWidth('products', 124)
              const display = getAdaptiveInlineListDisplay(products, colWidth)

              const box = document.createElement('div')
              box.className = 'inline-flex max-w-full items-center rounded-md border border-transparent px-1 py-0.5'
              cell.appendChild(box)

              if (!display.items.length) {
                const empty = document.createElement('span')
                empty.className = 'text-xs text-muted'
                empty.textContent = '—'
                box.appendChild(empty)
              }
              else if (display.allVisible) {
                const span = document.createElement('span')
                span.className = 'block max-w-full truncate text-[10px] text-muted opacity-60'
                span.title = display.fullLabel
                span.textContent = display.previewLabel
                box.appendChild(span)
              }
              else {
                const wrapper = document.createElement('div')
                wrapper.className = 'relative inline-flex items-center gap-1'

                const preview = document.createElement('span')
                preview.className = 'max-w-[120px] truncate text-[10px] text-muted opacity-60'
                preview.textContent = display.previewLabel

                const more = document.createElement('button')
                more.type = 'button'
                more.className = 'shrink-0 rounded border border-default bg-elevated px-1 py-0 text-[9px] text-muted transition-colors hover:border-primary/40 hover:text-highlighted'
                more.textContent = `+${display.hiddenCount}`
                more.title = display.fullLabel

                const panel = document.createElement('div')
                panel.className = 'absolute left-0 top-full z-20 mt-1 hidden min-w-[10rem] flex-col gap-1 rounded-lg border border-default bg-default p-2 shadow-lg'
                products.forEach((product) => {
                  const item = document.createElement('span')
                  item.className = 'text-[11px] text-highlighted'
                  item.textContent = product
                  panel.appendChild(item)
                })

                more.addEventListener('click', (event) => {
                  event.preventDefault()
                  event.stopPropagation()
                  panel.classList.toggle('hidden')
                  panel.classList.toggle('flex')
                })

                document.addEventListener('click', () => {
                  panel.classList.add('hidden')
                  panel.classList.remove('flex')
                }, { once: true })

                wrapper.appendChild(preview)
                wrapper.appendChild(more)
                wrapper.appendChild(panel)
                box.appendChild(wrapper)
              }
            }
          }
          grid.appendChild(cell)
          continue
        }

        if (column.id === 'hours') {
          const cell = createGridCell('px-3 py-1 text-right align-top')
          if (headerMeta) {
            const epic = headerMeta.epic
            if (epic.isSimple) {
              if (isPlanningCellEditing(epic, 'hours', epicScopeKey)) {
                const input = document.createElement('input')
                input.type = 'text'
                input.inputMode = 'decimal'
                input.value = getPlanningInlineDraft(epic).hoursInput
                input.className = 'w-full rounded-md border border-default bg-default px-2 py-1 text-right text-xs text-highlighted outline-none transition-colors focus:border-primary/40'
                input.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value
                input.addEventListener('click', event => event.stopPropagation())
                input.addEventListener('blur', () => {
                  deactivatePlanningCell(epic.id, 'hours')
                  schedulePlanningGroupedHeaderSync()
                })
                input.addEventListener('keydown', (event) => {
                  if (event.key === 'Escape' || event.key === 'Enter') {
                    event.preventDefault()
                    deactivatePlanningCell(epic.id, 'hours')
                    schedulePlanningGroupedHeaderSync()
                  }
                })
                input.addEventListener('input', (event) => {
                  updatePlanningInlineDraft(epic, { hoursInput: (event.target as HTMLInputElement).value })
                })
                cell.appendChild(input)
                requestAnimationFrame(() => input.focus())
              }
              else {
                const draft = getPlanningInlineDraft(epic)
                const btn = document.createElement('button')
                btn.type = 'button'
                btn.className = `rounded-md border text-right text-[10px] font-semibold transition-colors ${getPlanningEditableCellButtonClass(epic)}`
                // Reflect the draft directly: a cleared field shows "—" (no hours), not the old value.
                btn.textContent = draft.hoursInput ? `${draft.hoursInput}h` : '—'
                btn.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value
                btn.addEventListener('click', (event) => {
                  event.preventDefault()
                  event.stopPropagation()
                  activatePlanningCell(epic, 'hours', epicScopeKey)
                  schedulePlanningGroupedHeaderSync()
                })
                cell.appendChild(btn)
              }
            }
            else {
              // Regular epics: read-only sum
              const text = document.createElement('span')
              text.className = 'text-[10px] font-semibold text-muted opacity-60'
              text.textContent = `${headerMeta.totalHours.toLocaleString('pt-BR')}h`
              cell.appendChild(text)
            }
          }
          grid.appendChild(cell)
          continue
        }

        if (column.id === 'status') {
          const cell = createGridCell('px-3 py-1 align-top')
          if (headerMeta) {
            const epic = headerMeta.epic
            const draftEpic = getPlanningDraftDisplayItem(epic)
            const container = document.createElement('div')
            container.className = 'flex flex-col gap-1'
            container.title = getDemandNotesTooltip(draftEpic) || statusLabels[draftEpic.status]

            const statusRow = document.createElement('div')
            statusRow.className = 'flex items-center gap-1.5'

            if (isPlanningCellEditing(epic, 'status', epicScopeKey)) {
              const select = document.createElement('select')
              select.className = 'min-w-0 rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted outline-none transition-colors focus:border-primary/40'
              select.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value

              getStatusOptionsForItem(epic).forEach((option) => {
                const optionNode = document.createElement('option')
                optionNode.value = option.value
                optionNode.textContent = option.label
                optionNode.selected = option.value === getPlanningInlineDraft(epic).status
                select.appendChild(optionNode)
              })

              select.addEventListener('click', event => event.stopPropagation())
              select.addEventListener('blur', () => {
                deactivatePlanningCell(epic.id, 'status')
                schedulePlanningGroupedHeaderSync()
              })
              select.addEventListener('change', (event) => {
                handlePlanningStatusChange(epic, (event.target as HTMLSelectElement).value as DemandStatus)
                schedulePlanningGroupedHeaderSync()
              })
              statusRow.appendChild(select)
              requestAnimationFrame(() => select.focus())
            }
            else {
              const badge = document.createElement('button')
              badge.type = 'button'
              badge.className = `inline-flex items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium transition-colors ${getStatusBadgeClass(draftEpic.status)} ${getPlanningEditableCellButtonClass(epic)}`
              badge.textContent = statusLabels[draftEpic.status]
              badge.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value
              badge.addEventListener('click', (event) => {
                event.preventDefault()
                event.stopPropagation()
                activatePlanningCell(epic, 'status', epicScopeKey)
                schedulePlanningGroupedHeaderSync()
              })
              statusRow.appendChild(badge)
            }

            container.appendChild(statusRow)

            cell.appendChild(container)
          }
          grid.appendChild(cell)
          continue
        }

        if (column.id === 'conclusion') {
          const cell = createGridCell('px-3 py-1 align-top')
          if (headerMeta) {
            const epic = headerMeta.epic
            const displayEpic = getPlanningDraftDisplayItem(epic)
            const container = document.createElement('div')
            container.className = 'flex flex-col gap-1'

            if (isPlanningCellEditing(epic, 'dueDate', epicScopeKey)) {
              const input = document.createElement('input')
              input.type = 'date'
              input.value = getPlanningInlineDraft(epic).dueDate
              input.className = 'w-full rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted outline-none transition-colors focus:border-primary/40'
              input.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value
              input.addEventListener('click', event => event.stopPropagation())
              input.addEventListener('blur', () => {
                deactivatePlanningCell(epic.id, 'dueDate')
                schedulePlanningGroupedHeaderSync()
              })
              input.addEventListener('keydown', (event) => {
                if (event.key === 'Escape' || event.key === 'Enter') {
                  event.preventDefault()
                  deactivatePlanningCell(epic.id, 'dueDate')
                  schedulePlanningGroupedHeaderSync()
                }
              })
              input.addEventListener('input', (event) => {
                updatePlanningInlineDraft(epic, { dueDate: (event.target as HTMLInputElement).value })
              })
              container.appendChild(input)
              requestAnimationFrame(() => input.focus())
            }
            else {
              const dueButton = document.createElement('button')
              dueButton.type = 'button'
              dueButton.className = `flex w-fit items-center gap-1 rounded-md border px-1 py-0.5 text-[11px] transition-colors ${getPlanningEditableCellButtonClass(epic)}`
              dueButton.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value
              dueButton.addEventListener('click', (event) => {
                event.preventDefault()
                event.stopPropagation()
                activatePlanningCell(epic, 'dueDate', epicScopeKey)
                schedulePlanningGroupedHeaderSync()
              })

              const displayedConclusion = getDisplayedConclusionDate(displayEpic)
              if (displayedConclusion) {
                dueButton.appendChild(createSvgIcon(['M8 2v4', 'M16 2v4', 'M3 10h18', 'M8 14h.01', 'M12 14h.01', 'M16 14h.01', 'M8 18h.01', 'M12 18h.01', 'M16 18h.01', 'M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2'], 'h-3 w-3'))
                const dueText = document.createElement('span')
                dueText.className = displayEpic.status === 'Done' && displayEpic.deliveryDate
                  ? 'text-green-600 dark:text-green-400'
                  : 'text-muted'
                dueText.textContent = formatDemandDate(displayedConclusion)
                dueButton.appendChild(dueText)
              }
              else {
                const empty = document.createElement('span')
                empty.className = 'text-xs text-muted'
                empty.textContent = '—'
                dueButton.appendChild(empty)
              }

              container.appendChild(dueButton)
            }

            if (showDemandDelayMarker(displayEpic)) {
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
          const cell = createGridCell('px-3 py-1 align-top')
          if (headerMeta) {
            const epic = headerMeta.epic
            const customerDisplay = getPlanningDraftCustomerDisplay(epic)

            if (isPlanningCellEditing(epic, 'customers', epicScopeKey)) {
              const wrapper = document.createElement('div')
              wrapper.className = 'relative inline-flex max-w-full'

              const trigger = document.createElement('button')
              trigger.type = 'button'
              trigger.className = `inline-flex max-w-full items-center gap-1 rounded-md border text-[10px] text-highlighted transition-colors ${getPlanningEditableCellButtonClass(epic)}`
              trigger.title = customerDisplay.fullLabel || 'Clientes do épico'

              const label = document.createElement('span')
              label.className = 'max-w-[140px] truncate'
              label.textContent = customerDisplay.items.length ? customerDisplay.previewLabel : '—'
              trigger.appendChild(label)

              if (customerDisplay.items.length && !customerDisplay.allVisible) {
                const more = document.createElement('span')
                more.className = 'shrink-0 text-muted'
                more.textContent = `+${customerDisplay.hiddenCount}`
                trigger.appendChild(more)
              }

              wrapper.appendChild(trigger)

              const editor = document.createElement('div')
              const editorWidth = getPlanningCustomerEditorPixelWidth()
              editor.className = 'absolute left-0 top-full z-30 mt-2 space-y-2 rounded-lg border border-default bg-default p-3 shadow-xl'
              editor.style.width = `${editorWidth}px`
              editor.style.maxWidth = `min(${editorWidth}px, calc(100vw - 2rem))`
              editor.addEventListener('click', event => event.stopPropagation())

              const selectedList = document.createElement('div')
              selectedList.className = 'flex max-h-24 flex-wrap gap-1 overflow-y-auto'

              getPlanningInlineDraft(epic).customers.forEach((customer) => {
                const row = document.createElement('span')
                row.className = 'inline-flex items-center gap-1 rounded-full border border-primary/20 bg-primary/10 px-2 py-0.5 text-[10px] text-primary'

                const labelText = document.createElement('span')
                labelText.textContent = customer
                row.appendChild(labelText)

                const removeButton = document.createElement('button')
                removeButton.type = 'button'
                removeButton.className = 'inline-flex h-3.5 w-3.5 items-center justify-center rounded-full hover:bg-primary/15'
                removeButton.appendChild(createSvgIcon(['M18 6 6 18', 'M6 6l12 12'], 'h-3 w-3'))
                removeButton.addEventListener('click', (event) => {
                  event.preventDefault()
                  event.stopPropagation()
                  removePlanningCustomer(epic, customer)
                  schedulePlanningGroupedHeaderSync()
                })
                row.appendChild(removeButton)

                selectedList.appendChild(row)
              })

              if (!selectedList.childElementCount) {
                const empty = document.createElement('div')
                empty.className = 'text-xs text-muted'
                empty.textContent = 'Nenhum cliente associado.'
                selectedList.appendChild(empty)
              }

              editor.appendChild(selectedList)

              const inputRow = document.createElement('div')
              inputRow.className = 'flex items-center gap-2'

              const input = document.createElement('input')
              input.type = 'text'
              input.value = planningCustomerInputs.value[epic.id] ?? ''
              input.placeholder = 'Digite um novo cliente'
              input.className = 'min-w-0 flex-1 rounded-md border border-default bg-default px-2 py-1.5 text-xs text-highlighted outline-none transition-colors focus:border-primary/40'
              input.addEventListener('click', event => event.stopPropagation())
              input.addEventListener('input', (event) => {
                planningCustomerInputs.value = {
                  ...planningCustomerInputs.value,
                  [epic.id]: (event.target as HTMLInputElement).value
                }
                schedulePlanningGroupedHeaderSync()
              })
              input.addEventListener('keydown', (event) => {
                if (event.key === 'Enter') {
                  event.preventDefault()
                  addPlanningCustomer(epic, planningCustomerInputs.value[epic.id] ?? '')
                  schedulePlanningGroupedHeaderSync()
                }
                else if (event.key === 'Escape') {
                  event.preventDefault()
                  deactivatePlanningCell(epic.id, 'customers')
                  schedulePlanningGroupedHeaderSync()
                }
              })
              inputRow.appendChild(input)

              const addButton = document.createElement('button')
              addButton.type = 'button'
              addButton.className = 'inline-flex items-center rounded-md border border-primary/20 bg-primary/10 px-2 py-1.5 text-[11px] font-medium text-primary transition-colors hover:bg-primary/15 disabled:cursor-not-allowed disabled:opacity-50'
              addButton.textContent = 'Adicionar'
              addButton.disabled = !(planningCustomerInputs.value[epic.id] ?? '').trim()
              addButton.addEventListener('click', (event) => {
                event.preventDefault()
                event.stopPropagation()
                addPlanningCustomer(epic, planningCustomerInputs.value[epic.id] ?? '')
                schedulePlanningGroupedHeaderSync()
              })
              inputRow.appendChild(addButton)
              editor.appendChild(inputRow)

              const suggestions = getFilteredPlanningCustomerSuggestions(epic)
              if (suggestions.length) {
                const suggestionsWrap = document.createElement('div')
                suggestionsWrap.className = 'max-h-32 overflow-y-auto rounded border border-default bg-elevated/40'

                suggestions.forEach((customer) => {
                  const option = document.createElement('button')
                  option.type = 'button'
                  option.className = 'flex w-full px-2 py-1.5 text-left text-[11px] text-highlighted hover:bg-elevated'
                  option.textContent = customer
                  option.addEventListener('click', (event) => {
                    event.preventDefault()
                    event.stopPropagation()
                    addPlanningCustomer(epic, customer)
                    schedulePlanningGroupedHeaderSync()
                  })
                  suggestionsWrap.appendChild(option)
                })

                editor.appendChild(suggestionsWrap)
              }

              wrapper.appendChild(editor)
              cell.appendChild(wrapper)

              requestAnimationFrame(() => input.focus())
            }
            else {
              const trigger = document.createElement('button')
              trigger.type = 'button'
              trigger.className = `inline-flex max-w-full items-center gap-1 rounded-md border text-[10px] text-highlighted transition-colors ${getPlanningEditableCellButtonClass(epic)}`
              trigger.title = customerDisplay.fullLabel || 'Clientes do épico'
              trigger.disabled = isPlanningInlineSaving(epic.id) || isSavingAllPlanningInlineEdits.value
              trigger.addEventListener('click', (event) => {
                event.preventDefault()
                event.stopPropagation()
                activatePlanningCell(epic, 'customers', epicScopeKey)
                schedulePlanningGroupedHeaderSync()
              })

              const label = document.createElement('span')
              label.className = 'max-w-[140px] truncate'
              label.textContent = customerDisplay.items.length ? customerDisplay.previewLabel : '—'
              trigger.appendChild(label)

              if (customerDisplay.items.length && !customerDisplay.allVisible) {
                const more = document.createElement('span')
                more.className = 'shrink-0 text-muted'
                more.textContent = `+${customerDisplay.hiddenCount}`
                trigger.appendChild(more)
              }

              cell.appendChild(trigger)
            }
          }
          grid.appendChild(cell)
          continue
        }

        if (column.id === '_actions') {
          const cell = createGridCell('relative overflow-visible !px-0 self-stretch')
          if (headerMeta) {
            // Wrapper with group class for hover-based overlay (same pattern as demand rows)
            const wrapper = document.createElement('div')
            wrapper.className = 'group absolute inset-0 flex items-center justify-center'

            const dots = document.createElement('span')
            dots.className = 'pointer-events-none select-none text-[10px] text-muted/40 transition-opacity group-hover:opacity-0'
            dots.textContent = '···'
            wrapper.appendChild(dots)

            const actions = document.createElement('div')
            actions.className = 'pointer-events-none absolute inset-y-0 right-0 z-30 flex items-center gap-0.5 rounded-md border border-default/60 bg-default/95 px-1 opacity-0 shadow-md backdrop-blur-sm transition-opacity group-hover:pointer-events-auto group-hover:opacity-100'

            const createButton = document.createElement('button')
            createButton.type = 'button'
            createButton.className = 'inline-flex h-6 w-6 items-center justify-center rounded-md text-primary transition-colors hover:text-primary/80'
            createButton.title = 'Nova demanda'
            createButton.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              startCreateDemandForEpic(headerMeta.epic)
            })
            createButton.appendChild(createSvgIcon(['M12 5v14', 'M5 12h14'], 'h-4 w-4'))
            if (canEditRoadmap.value)
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
            kpiSvg.setAttribute('class', 'h-4 w-4')

            const kpiPath1 = document.createElementNS('http://www.w3.org/2000/svg', 'path')
            kpiPath1.setAttribute('d', 'M3 3v18h18')
            const kpiPath2 = document.createElementNS('http://www.w3.org/2000/svg', 'path')
            kpiPath2.setAttribute('d', 'm19 9-5 5-4-4-3 3')

            kpiSvg.append(kpiPath1, kpiPath2)
            kpiButton.appendChild(kpiSvg)
            if (canEditRoadmap.value)
              actions.appendChild(kpiButton)

            const colorDetails = document.createElement('details')
            colorDetails.className = 'relative inline-flex shrink-0'
            colorDetails.addEventListener('click', event => event.stopPropagation())

            const colorSummary = document.createElement('summary')
            colorSummary.className = 'inline-flex h-6 w-6 list-none cursor-pointer items-center justify-center rounded-md text-muted transition-colors hover:text-highlighted'
            colorSummary.title = 'Cor da linha'
            const epicColor = LIST_ROW_COLORS.find(c => c.id === headerMeta.epic.rowColor)
            if (epicColor)
              colorSummary.style.color = epicColor.hex
            colorSummary.appendChild(createPaletteIcon('h-4 w-4'))
            colorDetails.appendChild(colorSummary)

            const colorPanel = document.createElement('div')
            colorPanel.className = 'absolute right-0 top-full z-30 mt-1 rounded-lg border border-default bg-default p-2 shadow-lg'
            const colorPanelLabel = document.createElement('p')
            colorPanelLabel.className = 'mb-2 text-xs font-medium text-muted'
            colorPanelLabel.textContent = 'Cor da linha'
            colorPanel.appendChild(colorPanelLabel)
            const swatchRow = document.createElement('div')
            swatchRow.className = 'flex flex-wrap gap-1.5'

            const noneBtn = document.createElement('button')
            noneBtn.type = 'button'
            noneBtn.className = 'flex h-5 w-5 items-center justify-center rounded border border-default transition-colors hover:border-primary/40'
            noneBtn.title = 'Sem cor'
            noneBtn.appendChild(createSvgIcon(['M18 6 6 18', 'M6 6l12 12'], 'h-3 w-3 text-muted'))
            noneBtn.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              setDemandRowColor(headerMeta.epic, null)
              colorDetails.removeAttribute('open')
            })
            swatchRow.appendChild(noneBtn)

            LIST_ROW_COLORS.forEach((color) => {
              const swatch = document.createElement('button')
              swatch.type = 'button'
              swatch.className = `h-5 w-5 rounded-full transition-all hover:scale-110${headerMeta.epic.rowColor === color.id ? ' ring-2 ring-offset-1 ring-highlighted' : ''}`
              swatch.style.backgroundColor = color.hex
              swatch.title = color.label
              swatch.addEventListener('click', (event) => {
                event.preventDefault()
                event.stopPropagation()
                setDemandRowColor(headerMeta.epic, color.id)
                colorDetails.removeAttribute('open')
              })
              swatchRow.appendChild(swatch)
            })

            colorPanel.appendChild(swatchRow)
            colorDetails.appendChild(colorPanel)
            if (canEditRoadmap.value)
              actions.appendChild(colorDetails)

            // Composite epic with no demands: allow converting it back to a simple epic.
            if (canEditRoadmap.value && emptyCompositeEpicIds.value.has(headerMeta.epic.id)) {
              const convertButton = document.createElement('button')
              convertButton.type = 'button'
              convertButton.className = 'inline-flex h-6 w-6 items-center justify-center rounded-md text-primary transition-colors hover:text-primary/80'
              convertButton.title = 'Transformar em épico simples'
              convertButton.addEventListener('click', (event) => {
                event.preventDefault()
                event.stopPropagation()
                promptConvertEpicToSimple(headerMeta.epic)
              })
              convertButton.appendChild(createSvgIcon(['M4 14h6v6', 'M20 10h-6V4', 'm14 10 7-7', 'm3 21 7-7'], 'h-4 w-4'))
              actions.appendChild(convertButton)
            }

            const editButton = document.createElement('button')
            editButton.type = 'button'
            editButton.className = 'inline-flex h-6 w-6 items-center justify-center rounded-md text-muted transition-colors hover:text-highlighted'
            editButton.title = 'Editar épico'
            editButton.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              openEditModal(headerMeta.epic)
            })

            // Standard Lucide "pencil" icon (same as used in every other edit action).
            editButton.appendChild(createSvgIcon(['M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z', 'm15 5 4 4'], 'h-4 w-4'))
            if (canEditRoadmap.value)
              actions.appendChild(editButton)

            const copyButton = document.createElement('button')
            copyButton.type = 'button'
            copyButton.className = 'inline-flex h-6 w-6 items-center justify-center rounded-md text-muted transition-colors hover:text-highlighted'
            copyButton.title = 'Copiar épico'
            copyButton.addEventListener('click', (event) => {
              event.preventDefault()
              event.stopPropagation()
              openCopyModal(headerMeta.epic)
            })
            // Standard Lucide "copy" icon.
            copyButton.appendChild(createSvgIcon(['M10 8h10a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H10a2 2 0 0 1-2-2V10a2 2 0 0 1 2-2z', 'M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2'], 'h-4 w-4'))
            if (canEditRoadmap.value)
              actions.appendChild(copyButton)

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
            deleteSvg.setAttribute('class', 'h-4 w-4')

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
            if (canEditRoadmap.value)
              actions.appendChild(deleteButton)

            wrapper.appendChild(actions)
            cell.appendChild(wrapper)
          }
          grid.appendChild(cell)
          continue
        }

        grid.appendChild(createGridCell(column.alignRight ? 'px-3 py-2 text-right align-top' : 'px-3 py-2 align-top'))
      }

      fullRowCell.appendChild(grid)
      dividerRow.appendChild(fullRowCell)

      const epicRowColorRgba = headerMeta?.epic.rowColor
        ? (LIST_ROW_COLORS.find(c => c.id === headerMeta.epic.rowColor)?.rgba ?? null)
        : null
      if (epicRowColorRgba) {
        dividerRow.style.backgroundColor = epicRowColorRgba
        grid.style.backgroundColor = epicRowColorRgba
      }

      tbody.insertBefore(dividerRow, targetRow)
      return
    }

    dividerRow.appendChild(dividerCell)
    tbody.insertBefore(dividerRow, targetRow)
  })
}

// ─── Modal ────────────────────────────────────────────────────────────────────
const modalOpen = ref(false)
const capacityModalOpen = ref(false)
const isSavingCapacity = ref(false)
const isSavingDemand = ref(false)
const editingDemand = ref<RoadmapDemand | null>(null)
const copySource = ref<RoadmapDemand | null>(null)
const modalEditFocusField = ref<string | undefined>()
const createItemType = ref<RoadmapItemType | undefined>()
const defaultParentDemandId = ref<string | undefined>()
const defaultProjectId = ref<string | undefined>()
const defaultProjectIds = ref<string[]>([])
const createDefaultQuarterYear = ref<number | undefined>()
const createDefaultQuarterNumber = ref<number | undefined>()
const createDefaultType = ref<RoadmapDemand['type'] | undefined>()
const createDefaultHours = ref<number | undefined>()
const createDefaultProductIds = ref<string[]>([])
const forceSimpleEpic = ref(false)
const convertSourceEpic = ref<RoadmapDemand | null>(null)
const confirmConvertToCompositeOpen = ref(false)
const deleteId = ref<string | null>(null)
const confirmDeleteOpen = ref(false)
// Dependency links of the item being deleted (both directions) — shown in the confirm dialog so the
// user knows these links will be removed before the item is deleted.
const deleteDependencyLinks = computed(() => {
  if (!deleteId.value)
    return []
  const target = demands.value.find(demand => demand.id === deleteId.value)
  if (!target)
    return []

  const seen = new Set<string>()
  const links: Array<{ demandId: string, title: string, projectName: string, itemType: RoadmapItemType, relation: 'dependsOn' | 'dependedOnBy' }> = []
  for (const dep of target.dependsOn ?? [])
    if (!seen.has(dep.demandId)) { seen.add(dep.demandId); links.push({ demandId: dep.demandId, title: dep.title, projectName: dep.projectName, itemType: dep.itemType, relation: 'dependsOn' }) }
  for (const dep of target.dependedOnBy ?? [])
    if (!seen.has(dep.demandId)) { seen.add(dep.demandId); links.push({ demandId: dep.demandId, title: dep.title, projectName: dep.projectName, itemType: dep.itemType, relation: 'dependedOnBy' }) }
  return links
})
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

function resetCreateModalDefaults() {
  createDefaultQuarterYear.value = undefined
  createDefaultQuarterNumber.value = undefined
  createDefaultType.value = undefined
  createDefaultHours.value = undefined
  createDefaultProductIds.value = []
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
  if (!canEditRoadmap.value) return
  createItemType.value = itemType
  defaultParentDemandId.value = parentDemandId
  defaultProjectId.value = defaults?.projectId
  defaultProjectIds.value = defaults?.projectIds ?? []
  createDefaultQuarterYear.value = defaults?.quarterYear
  createDefaultQuarterNumber.value = defaults?.quarterNumber
  createDefaultType.value = defaults?.type
  createDefaultHours.value = defaults?.hours
  createDefaultProductIds.value = defaults?.productIds ?? []
  forceSimpleEpic.value = false
  editingDemand.value = null
  copySource.value = null
  modalOpen.value = true
}

// Copiar demanda/épico: abre a modal em modo criação com os campos do item original
// pré-preenchidos (título vazio e status Backlog aplicados dentro da modal).
function openCopyModal(item: RoadmapDemand) {
  if (!canEditRoadmap.value) return
  resetCreateModalDefaults()
  createItemType.value = item.itemType
  defaultParentDemandId.value = item.parentDemandId
  forceSimpleEpic.value = false
  modalEditFocusField.value = undefined
  editingDemand.value = null
  copySource.value = item
  modalOpen.value = true
}

// Adding the first demand to a simple epic turns it into a composite epic: warn the user
// (its quarter/type/hours/product will be lost) before opening the prefilled demand form.
function startCreateDemandForEpic(epic: RoadmapDemand) {
  if (!canEditRoadmap.value) return
  if (epic.isSimple) {
    convertSourceEpic.value = epic
    confirmConvertToCompositeOpen.value = true
    return
  }

  openCreateModal('Demand', epic.id, { projectId: epic.projectId ?? epic.projectIds?.[0] })
}

function confirmCreateDemandInSimpleEpic() {
  const epic = convertSourceEpic.value
  if (!epic)
    return

  confirmConvertToCompositeOpen.value = false
  convertSourceEpic.value = null

  openCreateModal('Demand', epic.id, {
    projectId: epic.projectId,
    quarterYear: epic.quarterYear,
    quarterNumber: epic.quarterNumber,
    type: epic.type,
    hours: epic.hours ?? undefined,
    productIds: epic.products.map(product => product.productId)
  })
}

// A composite epic can become simple again only when it has no demands linked.
function promptConvertEpicToSimple(epic: RoadmapDemand) {
  openEditModal(epic, undefined, undefined, { forceSimpleEpic: true })
}

function openListView() {
  roadmapStore.selectProject(null)
  groupDemandsByEpic.value = true
  collapsedEpicIds.value = [...visibleEpicQuarterKeys.value]
  hasInitializedCollapsedEpicIds.value = true

  navigateTo({ path: '/roadmap' })
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
  if (!canEditRoadmap.value || !activeCapacityScope.value) return
  capacityModalOpen.value = true
}

function openEditModal(demand: RoadmapDemand, nextStatus?: DemandStatus, focusField?: string, options?: { forceSimpleEpic?: boolean }) {
  if (!canEditRoadmap.value) return
  editingDemand.value = nextStatus ? { ...demand, status: nextStatus } : demand
  copySource.value = null
  defaultParentDemandId.value = undefined
  defaultProjectId.value = undefined
  defaultProjectIds.value = []
  resetCreateModalDefaults()
  forceSimpleEpic.value = options?.forceSimpleEpic ?? false
  modalEditFocusField.value = focusField
  modalOpen.value = true
}

function promptDelete(id: string) {
  if (!canEditRoadmap.value) return
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
  const isBacklogDemand = demand.itemType === 'Demand' && isSpecialBacklogQuarter(baseQuarterYear, baseQuarterNumber)

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
    hoursRed: demand.hoursRed ?? false,
    isSimple: demand.isSimple ?? false,
    rowColor: demand.rowColor ?? null,
    promisedDate: isBacklogDemand ? '' : (demand.promisedDate ?? ''),
    customers: demand.itemType === 'Demand' ? [] : (demand.customers ?? []),
    dependencyDemandIds: demand.dependsOn.map(item => item.demandId),
    isBlocked: demand.isBlocked,
    blockedReason: demand.blockedReason ?? '',
    deliveryDate: demand.deliveryDate ?? '',
    problemClarity: demand.itemType === 'Epic' ? demand.problemClarity ?? undefined : undefined,
    hasNoKpi: demand.hasNoKpi,
    noKpiClassification: demand.noKpiClassification ?? undefined,
    excludeFromCapacity: demand.excludeFromCapacity,
    spilloverReason: demand.spilloverReason ?? undefined,
    spilloverObservation: demand.spilloverObservation ?? '',
    ...overrides
  }
}

async function toggleExcludeFromCapacity(demand: RoadmapDemand) {
  if (isPlanningInlineSaving(demand.id) || isSavingAllPlanningInlineEdits.value) return
  await roadmapStore.updateDemand(demand.id, buildDemandFormData(demand, {
    excludeFromCapacity: !demand.excludeFromCapacity
  }))
}

async function setDemandRowColor(demand: RoadmapDemand, color: string | null) {
  if (isPlanningInlineSaving(demand.id) || isSavingAllPlanningInlineEdits.value) return
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
  try {
    planningInlineSavingIds.value = [...planningInlineSavingIds.value, demand.id]
    await roadmapStore.updateDemand(demand.id, buildDemandFormData(demand, { rowColor: color }))
    await refreshListPresentation(listScrollTop, listScrollLeft)
  }
  finally {
    planningInlineSavingIds.value = planningInlineSavingIds.value.filter(id => id !== demand.id)
  }
}

// --- Criar Transbordo modal ---
const spilloverModalOpen = ref(false)
const spilloverModalDemand = ref<RoadmapDemand | null>(null)
const spilloverTargetYear = ref<number | null>(null)
const spilloverTargetNumber = ref<number | null>(null)
const spilloverReason = ref<string | null>(null)
const spilloverObservation = ref('')
const isCreatingSpillover = ref(false)
// Restore mode: item already has a spillover copy (successorDemandId); we only re-collect
// motivo/observação and update the inline draft — no new copy is created.
const spilloverRestoreMode = ref(false)

function openSpilloverModal(demand: RoadmapDemand) {
  spilloverModalDemand.value = demand
  spilloverRestoreMode.value = false
  spilloverTargetYear.value = demand.quarterYear
  spilloverTargetNumber.value = demand.quarterNumber === 4 ? 1 : demand.quarterNumber + 1
  if (demand.quarterNumber === 4) spilloverTargetYear.value = demand.quarterYear + 1
  spilloverReason.value = null
  spilloverObservation.value = ''
  spilloverModalOpen.value = true
}

function openPlanningSpilloverRestoreModal(item: RoadmapDemand) {
  const draft = getPlanningInlineDraft(item)
  spilloverModalDemand.value = item
  spilloverRestoreMode.value = true
  spilloverTargetYear.value = item.quarterYear
  spilloverTargetNumber.value = item.quarterNumber
  spilloverReason.value = draft.spilloverReason ?? item.spilloverReason ?? null
  spilloverObservation.value = draft.spilloverObservation ?? item.spilloverObservation ?? ''
  spilloverModalOpen.value = true
}

function closeSpilloverModal() {
  spilloverModalOpen.value = false
  spilloverRestoreMode.value = false
  spilloverModalDemand.value = null
}

async function confirmCreateSpillover() {
  if (!spilloverModalDemand.value) return
  if (!spilloverReason.value || !spilloverObservation.value.trim()) return

  if (spilloverRestoreMode.value) {
    // Item already has a spillover copy — just re-apply the status and new motivo/observação
    // to the inline draft. The normal save flow persists it (no createSpillover call).
    updatePlanningInlineDraft(spilloverModalDemand.value, {
      status: 'Spillover',
      spilloverReason: spilloverReason.value,
      spilloverObservation: spilloverObservation.value.trim(),
      deliveryDate: '',
      blockedReason: '',
      deprioritizationReason: undefined,
      replacementDemandId: undefined,
      observation: ''
    })
    closeSpilloverModal()
    return
  }

  if (!spilloverTargetYear.value || !spilloverTargetNumber.value) return
  isCreatingSpillover.value = true
  try {
    const demandId = spilloverModalDemand.value.id
    await roadmapStore.createSpillover(
      demandId,
      spilloverTargetYear.value,
      spilloverTargetNumber.value,
      spilloverReason.value,
      spilloverObservation.value.trim()
    )
    clearPlanningInlineDraft(demandId)
    spilloverModalOpen.value = false
  }
  finally {
    isCreatingSpillover.value = false
    spilloverModalDemand.value = null
  }
}
// --- fim Criar Transbordo ---

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

async function planDemandToQuarter(demand: RoadmapDemand, quarterValue: string) {
  const { quarterYear, quarterNumber } = parseQuarterValue(quarterValue)
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
  movedDemandIds: string[],
  quarterValue: string,
  placement?: { beforeId: string | null, afterId: string | null }
) {
  const movedDemands = movedDemandIds
    .map(id => itemsById.value.get(id))
    .filter((demand): demand is RoadmapDemand => !!demand)
  if (!movedDemands.length || isBulkPlanning.value)
    return

  const { quarterYear, quarterNumber } = parseQuarterValue(quarterValue)
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
  const epicTitle = movedDemands[0]?.epicTitle ?? 'Épico'
  const movedSet = new Set(movedDemandIds)
  const project = getEffectiveProjectId(movedDemands[0]!)

  // Desired final order of the target quarter scope: the demands already there (same team) plus the
  // moved block, with the block positioned at the drop point.
  const targetScopeIds = [...demandItems.value, ...epicItems.value.filter(epic => epic.isSimple)]
    .filter(item =>
      getEffectiveProjectId(item) === project
      && item.quarterYear === quarterYear
      && item.quarterNumber === quarterNumber
      && !movedSet.has(item.id))
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map(item => item.id)

  const orderedDemandIds = moveDemandCluster(
    [...targetScopeIds, ...movedDemandIds],
    movedDemandIds,
    placement?.beforeId ?? null,
    placement?.afterId ?? null
  )

  try {
    isBulkPlanning.value = true

    // Single request + single local mutation — no per-demand flicker.
    await roadmapStore.bulkMoveDemandsToQuarter(movedDemandIds, quarterYear, quarterNumber, orderedDemandIds)

    await refreshListPresentation(listScrollTop, listScrollLeft)

    toast.add({
      title: 'Épico planejado no quarter',
      description: `${epicTitle} movido para ${quarterShortLabel(quarterValue)} com ${movedDemands.length.toLocaleString('pt-BR')} demandas`,
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

async function handleFormSpillover(demandId: string, targetYear: number, targetNumber: number, reason: string, observation: string) {
  if (isSavingDemand.value) return
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
  try {
    isSavingDemand.value = true
    modalOpen.value = false
    await roadmapStore.createSpillover(demandId, targetYear, targetNumber, reason, observation)
    clearPlanningInlineDraft(demandId)
    toast.add({ title: 'Transbordo criado', color: 'success' })
    queueMicrotask(() => {
      void refreshListPresentation(listScrollTop, listScrollLeft)
    })
  }
  catch (error) {
    toast.add({ title: 'Erro ao criar transbordo', color: 'error' })
  }
  finally {
    isSavingDemand.value = false
  }
}

async function handleSubmit(data: DemandFormData) {
  if (isSavingDemand.value)
    return

  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
  const customerRenames = sanitizeCustomerRenames(data.customerRenames)

  try {
    isSavingDemand.value = true
    modalOpen.value = false

    if (editingDemand.value) {
      await roadmapStore.updateDemand(editingDemand.value.id, data)

      if (editingDemand.value.itemType === 'Epic')
        await propagateEpicCustomerRenames(editingDemand.value.id, customerRenames)

      toast.add({ title: 'Item atualizado', color: 'success' })
    }
    else {
      // Detect whether the new demand's parent epic was a simple epic; creating the
      // demand converts it to composite on the backend, so we must refetch to reflect it.
      const parentEpic = data.itemType === 'Demand' && data.parentDemandId
        ? demands.value.find(demand => demand.id === data.parentDemandId)
        : undefined
      const convertedSimpleEpic = !!parentEpic?.isSimple

      await roadmapStore.createDemand(data)

      if (convertedSimpleEpic)
        await roadmapStore.fetchDemands()

      toast.add({ title: 'Item criado', color: 'success' })
    }
    queueMicrotask(() => {
      void refreshListPresentation(listScrollTop, listScrollLeft)
    })
  }
  catch {
    modalOpen.value = true
    // error handled by useApi
  }
  finally {
    isSavingDemand.value = false
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
  Backlog: 'Backlog', InProgress: 'Doing', Done: 'Concluído', Deprioritized: 'Despriorizado', Blocked: 'Impedido', Spillover: 'Transbordo', UX: 'UX', Prioritized: 'Priorizado'
}
const statusTextClass: Record<DemandStatus, string> = {
  Backlog: 'text-muted',
  InProgress: 'text-blue-600 dark:text-blue-400',
  Done: 'text-green-600 dark:text-green-400',
  Deprioritized: 'text-pink-600 dark:text-pink-400',
  Blocked: 'text-red-600 dark:text-red-400',
  Spillover: 'text-orange-600 dark:text-orange-400',
  UX: 'text-purple-600 dark:text-purple-400',
  Prioritized: 'text-cyan-600 dark:text-cyan-400'
}
const statusDotClass: Record<DemandStatus, string> = {
  Backlog: 'bg-neutral-400 dark:bg-neutral-500',
  InProgress: 'bg-blue-500 dark:bg-blue-400',
  Done: 'bg-green-500 dark:bg-green-400',
  Deprioritized: 'bg-pink-500 dark:bg-pink-400',
  Blocked: 'bg-red-500 dark:bg-red-400',
  Spillover: 'bg-orange-500 dark:bg-orange-400',
  UX: 'bg-purple-500 dark:bg-purple-400',
  Prioritized: 'bg-cyan-500 dark:bg-cyan-400'
}
const typeLabels: Record<DemandType, string> = {
  Planned: 'Planejado', Spillover: 'Transbordo', Unplanned: 'Não Planejado', Additional: 'Adicional'
}
const typeColors: Record<DemandType, string> = {
  Planned: 'text-emerald-600 dark:text-emerald-400',
  Spillover: 'text-rose-600 dark:text-rose-400',
  Unplanned: 'text-rose-600 dark:text-rose-400',
  Additional: 'text-violet-600 dark:text-violet-400'
}
const classificationLabels: Record<DemandClassification, string> = {
  TechnicalDebtSecurity: 'Débito Técnico', Strategic: 'Estratégico', Evolution: 'Evolução',
  ImprovementGap: 'Melhoria/Gap', Mandatory: 'Mandatório', Homologation: 'Homologação', Customizacao: 'Customização'
}
const classificationSelectOptions = (Object.entries(classificationLabels) as Array<[DemandClassification, string]>)
  .map(([value, label]) => ({ value, label }))

// ─── List view — TanStack table ──────────────────────────────────────────────────────────────────
const listSorting = ref<SortingState>([])
const listColumnFilters = ref<ColumnFiltersState>([])
const listColumnSizing = ref<ColumnSizingState>({})
const listColumnOrder = ref<string[]>([])
const groupDemandsByEpic = ref(true)

watch(groupDemandsByEpic, (val) => {
  localStorage.setItem(CACHE_KEY_PLANNING_GROUP_BY_EPIC, JSON.stringify(val))
})

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

// Collapse state is keyed per (epic + quarter): the same epic spanning Q1/Q2 has an independent
// collapse state in each quarter, so expanding it in one quarter doesn't expand the other.
const visibleEpicQuarterKeys = computed(() => {
  if (!groupDemandsByEpic.value)
    return []

  return Array.from(new Set(
    quarterFilteredDemands.value
      .filter(demand => !!demand.epicId)
      .map(demand => epicQuarterKey(demand.epicId!, demand.quarterYear, demand.quarterNumber))
  ))
})

const tableDemands = computed(() => {
  const base = (() => {
    if (!groupDemandsByEpic.value)
      return quarterFilteredDemands.value

    // Pre-group demands by epic so that even with no active sort column all demands
    // of the same epic are contiguous. Groups are ordered by the minimum sortOrder
    // of their member demands (same logic used by withListGroupSorting).
    const epicMinOrder: Record<string, number> = {}
    for (const demand of quarterFilteredDemands.value) {
      if (!demand.epicId) continue
      const key = getEpicQuarterOrderKey(demand)
      const current = epicMinOrder[key]
      if (current === undefined || demand.sortOrder < current)
        epicMinOrder[key] = demand.sortOrder
    }

    return [...quarterFilteredDemands.value].sort((left, right) => {
      const groupComparison = compareListDemandGroups(left, right)
      if (groupComparison !== 0) return groupComparison

      const epicA = left.epicId
      const epicB = right.epicId
      if (epicA && epicB && epicA !== epicB) {
        // Compare epics by their minimum sortOrder within this quarter (left/right are already
        // in the same quarter here), so demands in other quarters don't skew the position.
        const minA = epicMinOrder[getEpicQuarterOrderKey(left)] ?? left.sortOrder
        const minB = epicMinOrder[getEpicQuarterOrderKey(right)] ?? right.sortOrder
        if (minA !== minB) return minA - minB
        // Tiebreaker: keep all demands of the same epic contiguous even when two
        // epics (from different projects) share the same minimum sortOrder.
        return epicA < epicB ? -1 : 1
      }

      return left.sortOrder - right.sortOrder
    })
  })()

  const matchesProblemFilter = (item: RoadmapDemand) => {
    const hasDepFilter = filterInconsistentDeps.value
    const hasProblemFilter = listProblemFilter.value.length > 0
    if (!hasDepFilter && !hasProblemFilter)
      return true
    // Grupo de "problemas/saúde" combina com OU (mesmo comportamento do filtro Problemas):
    // o item passa se casar com qualquer um dos filtros ativos.
    if (hasDepFilter && hasInconsistentDependency(item))
      return true
    if (!hasProblemFilter)
      return false
    const problemKeys = getDemandProblemKeys(item)
    if (listProblemFilter.value.includes('__all__'))
      return problemKeys.length > 0
    return listProblemFilter.value.some(p => problemKeys.includes(p))
  }

  // Filtros de motivo (transbordo/despriorização): item passa se casar com qualquer motivo
  // ativo (OU entre motivos e entre os dois grupos, já que os status são mutuamente exclusivos).
  const matchesReasonFilter = (item: RoadmapDemand) => {
    const hasSpilloverReasonFilter = filterSpilloverReasons.value.length > 0
    const hasDeprioReasonFilter = filterDeprioritizationReasons.value.length > 0
    if (!hasSpilloverReasonFilter && !hasDeprioReasonFilter)
      return true
    if (hasSpilloverReasonFilter && item.status === 'Spillover' && !!item.spilloverReason && filterSpilloverReasons.value.includes(item.spilloverReason))
      return true
    if (hasDeprioReasonFilter && item.status === 'Deprioritized' && !!item.deprioritizationReason && filterDeprioritizationReasons.value.includes(item.deprioritizationReason))
      return true
    return false
  }

  const matchesExtraFilters = (item: RoadmapDemand) => matchesProblemFilter(item) && matchesReasonFilter(item)

  const filtered = base.filter(matchesExtraFilters)

  // Append empty composite epics at the end (in both grouped and non-grouped modes) so they
  // remain findable. They render under the "Épicos inconsistentes - sem demanda" section,
  // but still respect the active problem/reason filters.
  const inconsistentEpics = emptyCompositeEpics.value.filter(matchesExtraFilters)
  if (inconsistentEpics.length)
    return [...filtered, ...inconsistentEpics]

  return filtered
})

// When grouping by epic, the minimum sortOrder of each epic's demands determines
// the position of the whole epic group in the list.
const epicMinSortOrderById = computed(() => {
  const result: Record<string, number> = {}
  for (const demand of quarterFilteredDemands.value) {
    if (!demand.epicId) continue
    const key = getEpicQuarterOrderKey(demand)
    const current = result[key]
    if (current === undefined || demand.sortOrder < current)
      result[key] = demand.sortOrder
  }
  return result
})

function isCollapsedRepresentative(demand: RoadmapDemand) {
  return groupDemandsByEpic.value && !!demand.epicId
    && collapsedEpicIds.value.includes(epicQuarterKey(demand.epicId, demand.quarterYear, demand.quarterNumber))
}

function toggleEpicCollapse(epicId?: string, quarterYear?: number, quarterNumber?: number) {
  if (!epicId || quarterYear == null || quarterNumber == null)
    return

  const key = epicQuarterKey(epicId, quarterYear, quarterNumber)
  if (collapsedEpicIds.value.includes(key)) {
    collapsedEpicIds.value = collapsedEpicIds.value.filter(existing => existing !== key)
    return
  }

  collapsedEpicIds.value = [...collapsedEpicIds.value, key]
}

function collapseAllEpicGroups() {
  collapsedEpicIds.value = [...visibleEpicQuarterKeys.value]
}

function expandAllEpicGroups() {
  collapsedEpicIds.value = []
}

const areAllEpicGroupsCollapsed = computed(() => {
  if (!groupDemandsByEpic.value)
    return false

  return visibleEpicQuarterKeys.value.length > 0
    && visibleEpicQuarterKeys.value.every(key => collapsedEpicIds.value.includes(key))
})

watch(quarterFilteredDemands, (demands) => {
  const availableKeys = new Set(
    demands
      .filter(demand => !!demand.epicId)
      .map(demand => epicQuarterKey(demand.epicId!, demand.quarterYear, demand.quarterNumber))
  )
  if (!hasInitializedCollapsedEpicIds.value) {
    if (availableKeys.size === 0)
      return

    collapsedEpicIds.value = Array.from(availableKeys)
    hasInitializedCollapsedEpicIds.value = true
    return
  }

  collapsedEpicIds.value = collapsedEpicIds.value.filter(key => availableKeys.has(key))
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
const visibleEpicHeaderByDemandId = computed(() => {
  const result: Record<string, { showHeader: boolean, count: number, epicId?: string, roadmapTitle?: string | null, epicTitle?: string | null, collapsed: boolean }> = {}

  if (!groupDemandsByEpic.value) {
    for (const demand of visibleListRows.value)
      result[demand.id] = { showHeader: false, count: 0, collapsed: false }

    return result
  }

  // Track the last quarter in which each epic header was shown.
  // When the quarter changes, the same epic must get a new header.
  const lastEpicQuarter = new Map<string, string>()

  for (let index = 0; index < visibleListRows.value.length; index++) {
    const demand = visibleListRows.value[index]!
    const epicId = demand.epicId

    // Epic rows injected as their own row (simple epics and empty composite epics):
    // in grouped mode each one shows its own header (no collapse); in flat mode no header.
    if (demand.itemType === 'Epic') {
      result[demand.id] = {
        showHeader: groupDemandsByEpic.value,
        count: 0,
        epicId: demand.id,
        roadmapTitle: demand.roadmapTitle,
        epicTitle: demand.title,
        collapsed: false
      }
      continue
    }

    // No-epic demands: show a header each time the group key changes (original logic).
    if (!epicId) {
      const previous = index > 0 ? visibleListRows.value[index - 1]! : null
      const groupKey = getEpicDisplayGroupKey(demand)
      const previousGroupKey = previous ? getEpicDisplayGroupKey(previous) : null
      const showHeader = groupKey !== previousGroupKey
      result[demand.id] = { showHeader, count: showHeader ? 1 : 0, collapsed: false }
      continue
    }

    // Epic demands: show header at the first occurrence per quarter.
    const quarterKey = `${demand.quarterYear}:${demand.quarterNumber}`
    const showHeader = lastEpicQuarter.get(epicId) !== quarterKey
    lastEpicQuarter.set(epicId, quarterKey)

    if (!showHeader) {
      result[demand.id] = { showHeader: false, count: 0, collapsed: false }
      continue
    }

    // Count = visible demands of this epic in this quarter.
    const count = getVisibleEpicDemands(epicId).filter(d => d.quarterYear === demand.quarterYear && d.quarterNumber === demand.quarterNumber).length

    result[demand.id] = {
      showHeader: true,
      count,
      epicId: demand.epicId,
      roadmapTitle: demand.roadmapTitle,
      epicTitle: demand.epicTitle,
      collapsed: collapsedEpicIds.value.includes(epicQuarterKey(epicId, demand.quarterYear, demand.quarterNumber))
    }
  }

  return result
})


const listHasActiveFilters = computed(() => listColumnFilters.value.length > 0)
const planningGroupedRenderNonce = ref(0)
const listTableKey = computed(() =>
  tableDemands.value
    .map(demand => `${demand.id}:${demand.parentDemandId ?? 'none'}:${demand.epicId ?? 'none'}:${demand.roadmapId ?? 'none'}:${demand.title}:${demand.quarterYear}:${demand.quarterNumber}:${demand.status}:${demand.sortOrder}:${demand.updatedAt ?? ''}`)
    .join('|') + `::${groupDemandsByEpic.value ? 'grouped' : 'flat'}::${collapsedEpicIds.value.join('|')}::${planningGroupedRenderNonce.value}`
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
// Composite epic selection is per (epic + quarter): the same epic can sit in two quarters with
// distinct demands, and selecting its header in one quarter must select only that quarter's
// demands. Keys are `${epicId}:${quarterYear}:${quarterNumber}`.
const selectedEpicQuarters = ref<string[]>([])
function epicQuarterKey(epicId: string, quarterYear: number, quarterNumber: number) {
  return `${epicId}:${quarterYear}:${quarterNumber}`
}
// Demands "locked" by a selected composite-epic-quarter: they show as selected and can't be
// unchecked individually (the whole epic-quarter moves/edits as a unit).
const epicLockedDemandIds = computed(() => {
  const locked = new Set<string>()
  if (!selectedEpicQuarters.value.length)
    return locked
  const keys = new Set(selectedEpicQuarters.value)
  for (const demand of demandItems.value)
    if (demand.epicId && keys.has(epicQuarterKey(demand.epicId, demand.quarterYear, demand.quarterNumber)))
      locked.add(demand.id)
  return locked
})
const effectiveSelectedDemandIds = computed(() => {
  const ids = new Set(selectedDemandIds.value)
  for (const id of epicLockedDemandIds.value)
    ids.add(id)
  return ids
})
const isBulkPlanning = ref(false)
const isSavingAllPlanningInlineEdits = ref(false)
type PlanningInlineDraft = {
  title: string
  status: DemandStatus
  classification: DemandClassification
  quarterValue: string
  type: DemandType
  dueDate: string
  hoursInput: string
  hoursRed: boolean
  productIds: string[]
  customers: string[]
  observation: string
  blockedReason: string
  deliveryDate: string
  deprioritizationReason?: DeprioritizationReason
  replacementDemandId?: string
  spilloverReason?: string
  spilloverObservation?: string
}
type PlanningEditableField = 'title' | 'status' | 'classification' | 'quarterType' | 'products' | 'hours' | 'customers' | 'dueDate'
type PlanningActiveCell = {
  itemId: string
  field: PlanningEditableField
  // Disambiguates the same epic rendered in more than one quarter section (grouped mode):
  // only the header instance whose scopeKey matches becomes editable.
  scopeKey?: string
}
const planningInlineDrafts = ref<Record<string, PlanningInlineDraft>>({})
const planningInlineSavingIds = ref<string[]>([])
const activePlanningCell = ref<PlanningActiveCell | null>(null)
const planningCustomerInputs = ref<Record<string, string>>({})
const planningStatusModalOpen = ref(false)
const planningStatusModalItemId = ref<string | null>(null)
const planningStatusModalSnapshot = ref<PlanningInlineDraft | null>(null)
const visibleListDemandIds = computed(() => visibleListRows.value.map(demand => demand.id))
const selectedDemands = computed(() => {
  const selectedIds = effectiveSelectedDemandIds.value
  // Includes demands selected individually, simple epics (rows), and demands locked by a selected
  // composite-epic-quarter.
  return demands.value.filter(item =>
    selectedIds.has(item.id) &&
    (item.itemType === 'Demand' || (item.itemType === 'Epic' && item.isSimple))
  )
})
const selectedSimpleEpics = computed(() => {
  // Simple epics selected via the epic checkbox (grouped mode)
  const selectedIds = new Set(selectedEpicIds.value)
  return epicItems.value.filter(epic => selectedIds.has(epic.id) && epic.isSimple)
})
// Composite epics whose header was selected (in any quarter). Used by bulk EDIT, which edits the
// epic entity itself (its demands are handled by bulk MOVE).
const selectedCompositeEpicIds = computed(() => {
  const ids = new Set<string>()
  for (const key of selectedEpicQuarters.value) {
    const epicId = key.split(':')[0]
    if (epicId)
      ids.add(epicId)
  }
  return ids
})
const selectedCompositeEpics = computed(() =>
  epicItems.value.filter(epic => !epic.isSimple && selectedCompositeEpicIds.value.has(epic.id))
)
// Items that can be MOVED to a different quarter: selected demands (individual + locked by a
// composite-epic-quarter) + simple epics.
const movablePlanningItems = computed(() => {
  const selectedById = new Map<string, RoadmapDemand>()
  selectedDemands.value.forEach(item => selectedById.set(item.id, item))
  selectedSimpleEpics.value.forEach(epic => selectedById.set(epic.id, epic))
  return Array.from(selectedById.values())
})
// Items for bulk EDIT: selected demands (individual + the composite epic's quarter-demands) +
// simple epics + the composite EPIC entities. Selecting a composite epic edits the epic AND its
// demands in that quarter.
const selectedPlanningItems = computed(() => {
  const selectedById = new Map<string, RoadmapDemand>()
  selectedDemands.value.forEach(item => selectedById.set(item.id, item))
  selectedSimpleEpics.value.forEach(epic => selectedById.set(epic.id, epic))
  selectedCompositeEpics.value.forEach(epic => selectedById.set(epic.id, epic))
  return Array.from(selectedById.values())
})
const selectedPlanningItemCount = computed(() => selectedPlanningItems.value.length)
const planningInlineEditableItems = computed(() =>
  [...epicItems.value, ...demandItems.value]
)
const planningInlineDirtyIds = computed(() =>
  planningInlineEditableItems.value
    .filter(item => isPlanningInlineDirty(item))
    .map(item => item.id)
)
const planningPendingEditCount = computed(() => planningInlineDirtyIds.value.length)
const planningPendingEditLabel = computed(() => {
  const count = planningPendingEditCount.value
  return `Salvar ${count.toLocaleString('pt-BR')} ${count === 1 ? 'edição' : 'edições'}`
})
const planningStatusModalItem = computed(() =>
  planningStatusModalItemId.value
    ? planningInlineEditableItems.value.find(item => item.id === planningStatusModalItemId.value) ?? null
    : null
)
const planningStatusModalDraft = computed(() =>
  planningStatusModalItem.value ? getPlanningInlineDraft(planningStatusModalItem.value) : null
)
const planningStatusModalRequiresDeliveryDate = computed(() => planningStatusModalDraft.value?.status === 'Done')
const planningStatusModalRequiresBlockedReason = computed(() => planningStatusModalDraft.value?.status === 'Blocked')
const planningStatusModalRequiresDeprioritization = computed(() => planningStatusModalDraft.value?.status === 'Deprioritized')
const planningStatusReplacementDemandOptions = computed(() => {
  const currentItemId = planningStatusModalItem.value?.id

  return dependencyOptions.value
    .filter(option => option.demandId !== currentItemId)
    .map(option => ({
      value: option.demandId,
      label: `${option.projectName} · ${option.title}`
    }))
})

const LIST_COL_MIN = 60

const LIST_ROW_COLORS = [
  { id: 'red',    label: 'Vermelho', hex: '#ef4444', rgba: 'rgba(239, 68, 68, 0.10)' },
  { id: 'orange', label: 'Laranja',  hex: '#f97316', rgba: 'rgba(249, 115, 22, 0.10)' },
  { id: 'amber',  label: 'Âmbar',   hex: '#f59e0b', rgba: 'rgba(245, 158, 11, 0.10)' },
  { id: 'green',  label: 'Verde',   hex: '#22c55e', rgba: 'rgba(34, 197, 94, 0.10)' },
  { id: 'blue',   label: 'Azul',    hex: '#3b82f6', rgba: 'rgba(59, 130, 246, 0.10)' },
  { id: 'violet', label: 'Roxo',    hex: '#8b5cf6', rgba: 'rgba(139, 92, 246, 0.10)' },
  { id: 'pink',   label: 'Rosa',    hex: '#ec4899', rgba: 'rgba(236, 72, 153, 0.10)' },
] as const

interface ListColMeta {
  id: string
  label: string
  defaultWidth: number
  filterType?: 'text' | 'select' | 'multi-select' | 'text-classification' | 'quarter-type'
  selectOptions?: { label: string; value: string }[]
  allLabel?: string
  itemLabelPlural?: string
  alignRight?: boolean
  disableFilter?: boolean
  disableSorting?: boolean
}

const STATUS_SELECT_OPTIONS_BASE = [
  { label: 'Backlog',       value: 'Backlog' },
  { label: 'Doing',  value: 'InProgress' },
  { label: 'Concluído',     value: 'Done' },
  { label: 'Despriorizado', value: 'Deprioritized' },
  { label: 'Impedido',      value: 'Blocked' },
  { label: 'UX',            value: 'UX' },
  { label: 'Priorizado',    value: 'Prioritized' },
]
const STATUS_SELECT_OPTIONS = [
  ...STATUS_SELECT_OPTIONS_BASE,
  { label: 'Transbordo',    value: 'Spillover' },
]

function getStatusOptionsForItem(item: RoadmapDemand) {
  const isDemandOrSimpleEpic = item.itemType === 'Demand' || (item.itemType === 'Epic' && item.isSimple)
  const canSpillover = isDemandOrSimpleEpic && !item.successorDemandId
  const hadSpillover = isDemandOrSimpleEpic && !!item.successorDemandId
  if (canSpillover || hadSpillover || item.status === 'Spillover') return STATUS_SELECT_OPTIONS
  return STATUS_SELECT_OPTIONS_BASE
}
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
  { id: 'title',          label: 'Demanda',       defaultWidth: 360, filterType: 'text-classification' },
  { id: 'quarterLabel',   label: 'Quarter / Tipo', defaultWidth: 112, filterType: 'quarter-type', allLabel: 'Todos os quarters', itemLabelPlural: 'quarters' },
  { id: 'products',       label: 'Produtos',      defaultWidth: 148, filterType: 'multi-select', allLabel: 'Todos os produtos', itemLabelPlural: 'produtos', disableSorting: true },
  { id: 'hours',          label: 'Hrs',           defaultWidth: 60, disableFilter: true, alignRight: true },
  { id: 'customers',      label: 'Clientes',      defaultWidth: 110, filterType: 'multi-select', allLabel: 'Todos os clientes', itemLabelPlural: 'clientes' },
  { id: 'status',         label: 'Status',        defaultWidth: 124, filterType: 'multi-select', selectOptions: STATUS_SELECT_OPTIONS, allLabel: 'Todos os status', itemLabelPlural: 'status' },
  { id: 'conclusion',     label: 'Conclusão',     defaultWidth: 118, disableFilter: true },
  { id: 'kpis',           label: 'KPI',           defaultWidth: 100, disableFilter: true },
  { id: '_actions',       label: '',              defaultWidth: 40, disableFilter: true, disableSorting: true, alignRight: true },
]

listColumnOrder.value = LIST_COL_DEFS.map(column => column.id)

const listOrderedCols = ref<ListColMeta[]>([...LIST_COL_DEFS])
const listHeaderRowRef = ref<HTMLElement | null>(null)

const listQuarterFilterOptions = computed(() => {
  const seen = new Set<string>()

  return tableDemands.value
    .map((demand) => {
      const value = buildQuarterValue(demand.quarterYear, demand.quarterNumber)
      return {
        value,
        label: quarterShortLabel(value)
      }
    })
    .filter((option) => {
      if (seen.has(option.value))
        return false

      seen.add(option.value)
      return true
    })
    .sort((left, right) => left.label.localeCompare(right.label, 'pt-BR'))
})

// Clientes presentes no escopo (time/quarter) — usados no filtro multi-seleção da coluna Clientes.
const listCustomerFilterOptions = computed(() => {
  const seen = new Set<string>()
  for (const demand of quarterFilteredDemands.value)
    for (const customer of getEffectiveDemandCustomers(demand))
      seen.add(customer)
  return [...seen]
    .map(name => ({ value: name, label: name }))
    .sort((left, right) => left.label.localeCompare(right.label, 'pt-BR'))
})

onMounted(() => {
  updateListViewportWidth()
  startSoftRefreshWatchers()

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
  if (col.id === 'customers')
    return selected.length === 1 ? (selected[0] ?? '1 cliente') : `${selected.length} clientes`
  if (selected.length === 1) {
    return col.selectOptions?.find(option => option.value === selected[0])?.label ?? '1 selecionado'
  }
  return `${selected.length} ${col.itemLabelPlural ?? 'selecionados'}`
}

// ─── Filtros compostos ────────────────────────────────────────────────────────
// Coluna "Quarter / Tipo": filtra por quarter E por tipo (valor composto na mesma coluna).
// Coluna "Demanda": filtra por texto (título/descrição/Jira/épico) E por classificação (lista).
// Dentro de cada lista a combinação é "OU"; entre atributos diferentes é "E".
interface QuarterTypeFilter { quarters: string[], types: string[] }
interface TitleFilter { text: string, classifications: string[] }

const listTypeFilterOptions = demandTypes.map(type => ({ value: type as string, label: typeLabels[type] }))
const listClassificationFilterOptions = (Object.entries(classificationLabels) as Array<[DemandClassification, string]>)
  .map(([value, label]) => ({ value, label }))

function getQuarterTypeFilter(): QuarterTypeFilter {
  const value = listColumnFilters.value.find(f => f.id === 'quarterLabel')?.value as QuarterTypeFilter | undefined
  return { quarters: value?.quarters ?? [], types: value?.types ?? [] }
}
function setQuarterTypeFilter(next: QuarterTypeFilter) {
  const others = listColumnFilters.value.filter(f => f.id !== 'quarterLabel')
  listColumnFilters.value = (next.quarters.length || next.types.length)
    ? [...others, { id: 'quarterLabel', value: next }]
    : others
}
function toggleQuarterFilterValue(value: string) {
  const current = getQuarterTypeFilter()
  const quarters = current.quarters.includes(value)
    ? current.quarters.filter(item => item !== value)
    : [...current.quarters, value]
  setQuarterTypeFilter({ ...current, quarters })
}
function toggleTypeFilterValue(value: string) {
  const current = getQuarterTypeFilter()
  const types = current.types.includes(value)
    ? current.types.filter(item => item !== value)
    : [...current.types, value]
  setQuarterTypeFilter({ ...current, types })
}
function clearQuarterFilterPart() {
  setQuarterTypeFilter({ ...getQuarterTypeFilter(), quarters: [] })
}
function clearTypeFilterPart() {
  setQuarterTypeFilter({ ...getQuarterTypeFilter(), types: [] })
}
function getQuarterTypeFilterLabel(): string {
  const { quarters, types } = getQuarterTypeFilter()
  const parts: string[] = []
  if (quarters.length)
    parts.push(quarters.length === 1 ? quarterShortLabel(quarters[0]!) : `${quarters.length} quarters`)
  if (types.length)
    parts.push(types.length === 1 ? typeLabels[types[0] as DemandType] : `${types.length} tipos`)
  return parts.length ? parts.join(' · ') : 'Todos'
}

function getTitleFilter(): TitleFilter {
  const value = listColumnFilters.value.find(f => f.id === 'title')?.value as TitleFilter | undefined
  return { text: value?.text ?? '', classifications: value?.classifications ?? [] }
}
function setTitleFilter(next: TitleFilter) {
  const others = listColumnFilters.value.filter(f => f.id !== 'title')
  listColumnFilters.value = (next.text.trim() || next.classifications.length)
    ? [...others, { id: 'title', value: next }]
    : others
}
function setTitleTextFilter(text: string) {
  setTitleFilter({ ...getTitleFilter(), text })
}
function toggleTitleClassification(value: string) {
  const current = getTitleFilter()
  const classifications = current.classifications.includes(value)
    ? current.classifications.filter(item => item !== value)
    : [...current.classifications, value]
  setTitleFilter({ ...current, classifications })
}
function clearTitleClassifications() {
  setTitleFilter({ ...getTitleFilter(), classifications: [] })
}
function clearListFilters() {
  listColumnFilters.value = []
}
function sanitizeSelectedDemands() {
  const availableIds = new Set([
    ...demandItems.value.map(demand => demand.id),
    ...epicItems.value.filter(e => e.isSimple).map(e => e.id)
  ])
  selectedDemandIds.value = selectedDemandIds.value.filter(id => availableIds.has(id))

  const availableEpicIds = new Set(visibleListRows.value.map(demand => demand.epicId).filter((value): value is string => !!value))
  selectedEpicIds.value = selectedEpicIds.value.filter(id => availableEpicIds.has(id))

  // Drop composite-epic-quarter keys that no longer have any demand (epic emptied or moved away).
  const availableEpicQuarterKeys = new Set(
    demandItems.value
      .filter(demand => !!demand.epicId)
      .map(demand => epicQuarterKey(demand.epicId!, demand.quarterYear, demand.quarterNumber))
  )
  selectedEpicQuarters.value = selectedEpicQuarters.value.filter(key => availableEpicQuarterKeys.has(key))
}
function isDemandSelected(demandId: string) {
  return effectiveSelectedDemandIds.value.has(demandId)
}
// Locked = selected via a composite-epic-quarter; the individual checkbox is shown but disabled.
function isDemandLocked(demandId: string) {
  return epicLockedDemandIds.value.has(demandId)
}
function toggleDemandSelection(demandId: string, selected: boolean) {
  if (isDemandLocked(demandId))
    return

  if (selected) {
    if (!selectedDemandIds.value.includes(demandId))
      selectedDemandIds.value = [...selectedDemandIds.value, demandId]

    return
  }

  selectedDemandIds.value = selectedDemandIds.value.filter(id => id !== demandId)
}

function isEpicSelected(epicId?: string, quarterYear?: number, quarterNumber?: number) {
  if (!epicId)
    return false

  const epic = epicItems.value.find(item => item.id === epicId)
  // Simple epics keep their own (row-like) selection.
  if (epic?.isSimple)
    return selectedEpicIds.value.includes(epicId)

  // Composite epics: selection is per quarter instance.
  if (quarterYear == null || quarterNumber == null)
    return false
  return selectedEpicQuarters.value.includes(epicQuarterKey(epicId, quarterYear, quarterNumber))
}

function toggleEpicSelection(epicId: string, selected: boolean, quarterYear?: number, quarterNumber?: number) {
  const epic = epicItems.value.find(item => item.id === epicId)

  // Simple epic: behaves like selecting a single row.
  if (epic?.isSimple) {
    if (selected) {
      if (!selectedEpicIds.value.includes(epicId))
        selectedEpicIds.value = [...selectedEpicIds.value, epicId]
    }
    else {
      selectedEpicIds.value = selectedEpicIds.value.filter(id => id !== epicId)
    }
    return
  }

  // Composite epic: select/deselect this quarter's demands as a locked unit.
  if (quarterYear == null || quarterNumber == null)
    return
  const key = epicQuarterKey(epicId, quarterYear, quarterNumber)
  if (selected) {
    if (!selectedEpicQuarters.value.includes(key))
      selectedEpicQuarters.value = [...selectedEpicQuarters.value, key]
  }
  else {
    selectedEpicQuarters.value = selectedEpicQuarters.value.filter(existing => existing !== key)
    // Drop any of this epic-quarter's demands that were also individually selected, so unchecking
    // the epic fully clears its demands.
    const dropIds = new Set(
      demandItems.value
        .filter(demand => demand.epicId === epicId && demand.quarterYear === quarterYear && demand.quarterNumber === quarterNumber)
        .map(demand => demand.id)
    )
    if (dropIds.size)
      selectedDemandIds.value = selectedDemandIds.value.filter(id => !dropIds.has(id))
  }
}

function clearSelectedDemands() {
  selectedDemandIds.value = []
  selectedEpicIds.value = []
  selectedEpicQuarters.value = []

  queueMicrotask(() => {
    syncListSectionDividers()
  })
}

function getPlanningItemProjectIds(item: Pick<RoadmapDemand, 'projectId' | 'projectIds'>) {
  return Array.from(new Set([...(item.projectIds ?? []), ...(item.projectId ? [item.projectId] : [])]))
}

function getPlanningProductEntries(item: Pick<RoadmapDemand, 'products'>) {
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

function getPlanningEditableProductOptions(item: Pick<RoadmapDemand, 'projectId' | 'projectIds'>) {
  const optionsMap = new Map<string, string>()

  for (const projectId of getPlanningItemProjectIds(item)) {
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

function requiresPlanningStatusDetails(status: DemandStatus) {
  return status === 'Done' || status === 'Blocked' || status === 'Deprioritized'
}

function clonePlanningInlineDraft(draft: PlanningInlineDraft): PlanningInlineDraft {
  return {
    ...draft,
    productIds: [...draft.productIds],
    customers: [...draft.customers]
  }
}

function createPlanningInlineDraft(item: RoadmapDemand): PlanningInlineDraft {
  return {
    title: item.title,
    status: item.status,
    classification: item.classification,
    quarterValue: buildQuarterValue(item.quarterYear, item.quarterNumber),
    type: item.type,
    dueDate: item.status === 'Done' ? (item.deliveryDate ?? '') : (item.promisedDate ?? ''),
    hoursInput: item.hours != null ? String(item.hours) : '',
    hoursRed: item.hoursRed ?? false,
    productIds: getPlanningProductEntries(item).map(product => product.value),
    customers: normalizeCustomerList(item.customers),
    observation: item.observation ?? '',
    blockedReason: item.blockedReason ?? '',
    deliveryDate: item.deliveryDate ?? '',
    deprioritizationReason: item.deprioritizationReason ?? undefined,
    replacementDemandId: item.replacementDemandId ?? undefined,
    spilloverReason: item.spilloverReason ?? undefined,
    spilloverObservation: item.spilloverObservation ?? ''
  }
}

function getPlanningInlineDraft(item: RoadmapDemand) {
  return planningInlineDrafts.value[item.id] ?? createPlanningInlineDraft(item)
}

function updatePlanningInlineDraft(item: RoadmapDemand, patch: Partial<PlanningInlineDraft>) {
  planningInlineDrafts.value = {
    ...planningInlineDrafts.value,
    [item.id]: {
      ...getPlanningInlineDraft(item),
      ...patch
    }
  }
}

function clearPlanningInlineDraft(itemId: string) {
  const { [itemId]: _removed, ...rest } = planningInlineDrafts.value
  planningInlineDrafts.value = rest

  if (viewMode.value === 'list' && groupDemandsByEpic.value)
    schedulePlanningGroupedHeaderSync()
}

function clearAllPlanningInlineDrafts() {
  planningInlineDrafts.value = {}

  if (viewMode.value === 'list' && groupDemandsByEpic.value)
    schedulePlanningGroupedHeaderSync()
}

function activatePlanningCell(item: RoadmapDemand, field: PlanningEditableField, scopeKey?: string) {
  if (!canEditRoadmap.value) return
  const epicEditableFields: PlanningEditableField[] = ['title', 'status', 'classification', 'products', 'customers', 'dueDate']
  const simpleEpicEditableFields: PlanningEditableField[] = [...epicEditableFields, 'hours', 'quarterType']

  if (field === 'customers' && item.itemType === 'Epic') {
    planningCustomerInputs.value = {
      ...planningCustomerInputs.value,
      [item.id]: ''
    }

    activePlanningCell.value = { itemId: item.id, field, scopeKey }
    return
  }

  if (item.itemType === 'Epic') {
    const allowedFields = item.isSimple ? simpleEpicEditableFields : epicEditableFields
    if (!allowedFields.includes(field))
      return

    activePlanningCell.value = { itemId: item.id, field, scopeKey }
    return
  }

  if (item.itemType !== 'Demand')
    return

  if (field === 'customers') {
    planningCustomerInputs.value = {
      ...planningCustomerInputs.value,
      [item.id]: ''
    }
  }

  activePlanningCell.value = { itemId: item.id, field, scopeKey }
}

function deactivatePlanningCell(itemId?: string, field?: PlanningEditableField) {
  if (!activePlanningCell.value)
    return

  if (itemId && activePlanningCell.value.itemId !== itemId)
    return

  if (field && activePlanningCell.value.field !== field)
    return

  if (activePlanningCell.value.field === 'customers') {
    const customerItemId = activePlanningCell.value.itemId
    const { [customerItemId]: _removed, ...rest } = planningCustomerInputs.value
    planningCustomerInputs.value = rest
  }

  activePlanningCell.value = null
}

function handlePlanningPopoverOpenChange(item: RoadmapDemand, field: Extract<PlanningEditableField, 'products' | 'customers'>, open: boolean) {
  if (open) {
    activatePlanningCell(item, field)
    return
  }

  deactivatePlanningCell(item.id, field)
}

function isPlanningCellEditing(item: RoadmapDemand, field: PlanningEditableField, scopeKey?: string) {
  return activePlanningCell.value?.itemId === item.id
    && activePlanningCell.value?.field === field
    && (activePlanningCell.value?.scopeKey ?? undefined) === (scopeKey ?? undefined)
}

function parsePlanningInlineHours(item: RoadmapDemand) {
  const normalized = getPlanningInlineDraft(item).hoursInput.trim().replace(',', '.')
  if (!normalized)
    return undefined

  const parsed = Number(normalized)
  if (!Number.isFinite(parsed) || parsed < 0)
    return Number.NaN

  return Number(parsed.toFixed(2))
}

function isPlanningInlineSaving(itemId: string) {
  return planningInlineSavingIds.value.includes(itemId)
}

function getPlanningDraftDisplayItem(item: RoadmapDemand): RoadmapDemand {
  const draft = planningInlineDrafts.value[item.id]
  if (!draft)
    return item

  const isDoneStatus = draft.status === 'Done'

  return {
    ...item,
    title: draft.title,
    classification: draft.classification,
    type: draft.type,
    customers: draft.customers,
    quarterYear: parseQuarterValue(draft.quarterValue).quarterYear,
    quarterNumber: parseQuarterValue(draft.quarterValue).quarterNumber,
    quarterLabel: quarterShortLabel(draft.quarterValue),
    status: draft.status,
    promisedDate: isDoneStatus ? item.promisedDate : draft.dueDate,
    effectivePromisedDate: isDoneStatus ? item.effectivePromisedDate : draft.dueDate,
    deliveryDate: isDoneStatus ? draft.dueDate : ''
  }
}

function getPlanningDraftProductEntries(item: RoadmapDemand) {
  const draft = getPlanningInlineDraft(item)
  const allowedOptions = getPlanningEditableProductOptions(item)
  const allowedNameById = new Map(allowedOptions.map(option => [option.value, option.label] as const))
  const currentNameById = new Map(getPlanningProductEntries(item).map(product => [product.value, product.label] as const))

  return draft.productIds
    .map((productId) => {
      const label = allowedNameById.get(productId) ?? currentNameById.get(productId)
      return label ? { value: productId, label } : null
    })
    .filter((product): product is { value: string, label: string } => !!product)
}

function getPlanningDraftProductDisplay(item: RoadmapDemand) {
  return getAdaptiveInlineListDisplay(
    getPlanningDraftProductEntries(item).map(product => product.label),
    getBaseListColPixelWidth('products', 124),
    ' · '
  )
}

function getPlanningDraftCustomerDisplay(item: RoadmapDemand) {
  return getAdaptiveInlineListDisplay(getPlanningInlineDraft(item).customers, getBaseListColPixelWidth('customers', 110))
}

function getPlanningCustomerCellDisplay(item: RoadmapDemand) {
  return getAdaptiveInlineListDisplay(
    getEffectiveDemandCustomers(item),
    getBaseListColPixelWidth('customers', 110)
  )
}

function addPlanningCustomer(item: RoadmapDemand, customer: string) {
  const normalized = customer.trim()
  if (!normalized)
    return

  const nextCustomers = Array.from(new Set([...getPlanningInlineDraft(item).customers, normalized]))
  updatePlanningInlineDraft(item, { customers: nextCustomers })
  planningCustomerInputs.value = {
    ...planningCustomerInputs.value,
    [item.id]: ''
  }
}

function removePlanningCustomer(item: RoadmapDemand, customer: string) {
  updatePlanningInlineDraft(item, {
    customers: getPlanningInlineDraft(item).customers.filter(currentCustomer => currentCustomer !== customer)
  })
}

function getFilteredPlanningCustomerSuggestions(item: RoadmapDemand) {
  const query = planningCustomerInputs.value[item.id]?.trim().toLowerCase() ?? ''
  if (!query)
    return []

  const selected = new Set(getPlanningInlineDraft(item).customers.map(customer => customer.toLowerCase()))

  return customerSuggestions.value
    .filter(customer => !selected.has(customer.toLowerCase()))
    .filter(customer => customer.toLowerCase().includes(query))
    .slice(0, 6)
}

let pendingSyncDividerFrame: number | null = null
let pendingSyncDividerFull = false

function schedulePlanningGroupedHeaderSync(widthOnly = false) {
  if (!widthOnly)
    pendingSyncDividerFull = true
  if (pendingSyncDividerFrame !== null)
    cancelAnimationFrame(pendingSyncDividerFrame)
  pendingSyncDividerFrame = requestAnimationFrame(() => {
    pendingSyncDividerFrame = null
    const runFull = pendingSyncDividerFull
    pendingSyncDividerFull = false
    if (runFull)
      syncListSectionDividers()
    else
      updateSectionDividerWidths()
  })
}

function togglePlanningDraftProduct(item: RoadmapDemand, productId: string, checked: boolean) {
  const allowedProductIds = new Set(getPlanningEditableProductOptions(item).map(product => product.value))
  const currentSelection = new Set(getPlanningInlineDraft(item).productIds.filter(currentProductId => allowedProductIds.has(currentProductId)))

  if (checked)
    currentSelection.add(productId)
  else
    currentSelection.delete(productId)

  updatePlanningInlineDraft(item, { productIds: Array.from(currentSelection) })
}

function getPlanningEditableCellButtonClass(item: RoadmapDemand) {
  return isPlanningInlineDirty(item)
    ? 'border-primary/40 ring-1 ring-primary/10 hover:border-primary/60 hover:bg-primary/5'
    : 'border-transparent text-highlighted hover:border-primary/30 hover:bg-elevated'
}

function isPlanningInlineDirty(item: RoadmapDemand) {
  const draft = planningInlineDrafts.value[item.id]
  if (!draft)
    return false

  const originalDueDate = item.status === 'Done' ? (item.deliveryDate ?? '') : (item.promisedDate ?? '')
  const hours = parsePlanningInlineHours(item)

  if (Number.isNaN(hours))
    return true

  return draft.status !== item.status
    || draft.title.trim() !== item.title.trim()
    || draft.classification !== item.classification
    || draft.quarterValue !== buildQuarterValue(item.quarterYear, item.quarterNumber)
    || draft.type !== item.type
    || draft.dueDate !== originalDueDate
    || (draft.status === 'Done' && draft.deliveryDate !== (item.deliveryDate ?? ''))
    || (draft.status === 'Blocked' && draft.blockedReason.trim() !== (item.blockedReason ?? '').trim())
    || (draft.status === 'Deprioritized' && draft.observation.trim() !== (item.observation ?? '').trim())
    || (draft.status === 'Deprioritized' && draft.deprioritizationReason !== item.deprioritizationReason)
    || (draft.status === 'Deprioritized' && draft.replacementDemandId !== item.replacementDemandId)
    || draft.productIds.slice().sort().join('|') !== getPlanningProductEntries(item).map(product => product.value).sort().join('|')
    || draft.customers.slice().sort((left, right) => left.localeCompare(right, 'pt-BR')).join('|') !== normalizeCustomerList(item.customers).slice().sort((left, right) => left.localeCompare(right, 'pt-BR')).join('|')
    || hours !== item.hours
    || draft.hoursRed !== (item.hoursRed ?? false)
}

function closePlanningStatusModal(options?: { restoreSnapshot?: boolean }) {
  const restoreSnapshot = options?.restoreSnapshot ?? false
  const item = planningStatusModalItem.value
  const snapshot = planningStatusModalSnapshot.value

  if (restoreSnapshot && item && snapshot) {
    planningInlineDrafts.value = {
      ...planningInlineDrafts.value,
      [item.id]: clonePlanningInlineDraft(snapshot)
    }

    if (viewMode.value === 'list' && groupDemandsByEpic.value)
      schedulePlanningGroupedHeaderSync()
  }

  planningStatusModalOpen.value = false
  planningStatusModalItemId.value = null
  planningStatusModalSnapshot.value = null
}

function openPlanningStatusModal(item: RoadmapDemand, status: Extract<DemandStatus, 'Done' | 'Blocked' | 'Deprioritized'>) {
  const draft = getPlanningInlineDraft(item)

  planningStatusModalSnapshot.value = clonePlanningInlineDraft(draft)
  planningStatusModalItemId.value = item.id
  planningStatusModalOpen.value = true

  updatePlanningInlineDraft(item, {
    status,
    dueDate: status === 'Done' ? (draft.deliveryDate || item.deliveryDate || '') : draft.dueDate,
    blockedReason: status === 'Blocked' ? draft.blockedReason : '',
    deprioritizationReason: status === 'Deprioritized' ? draft.deprioritizationReason : undefined,
    replacementDemandId: status === 'Deprioritized' ? draft.replacementDemandId : undefined,
    observation: status === 'Deprioritized' ? draft.observation : draft.observation
  })
}

function handlePlanningStatusChange(item: RoadmapDemand, nextStatus: DemandStatus) {
  const currentDraft = getPlanningInlineDraft(item)

  if (nextStatus === currentDraft.status && !requiresPlanningStatusDetails(nextStatus)) {
    deactivatePlanningCell(item.id, 'status')
    return
  }

  if (nextStatus === 'Spillover') {
    if (item.successorDemandId) {
      // Already has a spillover copy — open planning status modal to collect new reason/observation
      openPlanningSpilloverRestoreModal(item)
    }
    else {
      openSpilloverModal(item)
    }
    deactivatePlanningCell(item.id, 'status')
    return
  }

  if (requiresPlanningStatusDetails(nextStatus)) {
    openPlanningStatusModal(item, nextStatus)
    deactivatePlanningCell(item.id, 'status')
    return
  }

  updatePlanningInlineDraft(item, {
    status: nextStatus,
    deliveryDate: '',
    blockedReason: '',
    deprioritizationReason: undefined,
    replacementDemandId: undefined,
    observation: ''
  })
  deactivatePlanningCell(item.id, 'status')
}

function confirmPlanningStatusModal() {
  const item = planningStatusModalItem.value
  const draft = planningStatusModalDraft.value

  if (!item || !draft)
    return

  if (draft.status === 'Done' && !draft.dueDate) {
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

    if ((draft.deprioritizationReason === 'ReplacedByOtherInitiative' || draft.deprioritizationReason === 'HigherValuePrioritization') && !draft.replacementDemandId) {
      toast.add({ title: 'Selecione a demanda priorizada no lugar', color: 'warning' })
      return
    }

    if (!draft.observation.trim()) {
      toast.add({ title: 'Informe a observação da despriorização', color: 'warning' })
      return
    }
  }

  closePlanningStatusModal()
}

async function discardAllPlanningInlineEdits() {
  if (isSavingAllPlanningInlineEdits.value || planningInlineSavingIds.value.length)
    return

  const discardedChanges = planningPendingEditCount.value
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
  deactivatePlanningCell()
  clearAllPlanningInlineDrafts()

  if (viewMode.value === 'list' && groupDemandsByEpic.value) {
    await nextTick()
    syncListSectionDividers()
  }

  if (discardedChanges > 0) {
    toast.add({
      title: 'Edição cancelada',
      description: `${discardedChanges.toLocaleString('pt-BR')} alteração(ões) descartada(s).`,
      color: 'warning'
    })
  }
}

async function savePlanningInline(
  item: RoadmapDemand,
  options?: { reloadAfterSave?: boolean, showSuccessToast?: boolean }
) {
  if (!isPlanningInlineDirty(item) || isPlanningInlineSaving(item.id))
    return true

  const reloadAfterSave = options?.reloadAfterSave ?? true
  const showSuccessToast = options?.showSuccessToast ?? true
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
  const draft = getPlanningInlineDraft(item)
  const hours = parsePlanningInlineHours(item)
  const nextQuarter = parseQuarterValue(draft.quarterValue)

  if (Number.isNaN(hours)) {
    toast.add({
      title: 'Horas inválidas',
      description: 'Informe um valor numérico maior que zero.',
      color: 'warning'
    })
    return false
  }

  if (hours === 0) {
    toast.add({
      title: 'Horas inválidas',
      description: '0h não é aceito. Informe um valor válido ou deixe as horas em branco.',
      color: 'warning'
    })
    return false
  }

  if (draft.status === 'Done' && !draft.dueDate) {
    toast.add({ title: 'Informe a data de entrega', color: 'warning' })
    return false
  }

  if (draft.status === 'Blocked' && !draft.blockedReason.trim()) {
    toast.add({ title: 'Informe o motivo do impedimento', color: 'warning' })
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
    planningInlineSavingIds.value = [...planningInlineSavingIds.value, item.id]

    await roadmapStore.updateDemand(item.id, buildDemandFormData(item, {
      title: draft.title.trim() || item.title,
      quarterYear: nextQuarter.quarterYear,
      quarterNumber: nextQuarter.quarterNumber,
      status: draft.status,
      classification: draft.classification,
      type: draft.type,
      productIds: draft.productIds,
      customers: draft.customers,
      observation: draft.status === 'Deprioritized' ? draft.observation : '',
      blockedReason: draft.status === 'Blocked' ? draft.blockedReason : '',
      deprioritizationReason: draft.status === 'Deprioritized' ? draft.deprioritizationReason : undefined,
      replacementDemandId: draft.status === 'Deprioritized' ? draft.replacementDemandId : undefined,
      spilloverReason: draft.status === 'Spillover' ? (draft.spilloverReason as any) : undefined,
      spilloverObservation: draft.status === 'Spillover' ? draft.spilloverObservation : undefined,
      hours,
      hoursRed: draft.hoursRed,
      promisedDate: isDoneStatus ? (item.promisedDate ?? '') : draft.dueDate,
      deliveryDate: isDoneStatus ? draft.dueDate : ''
    }))

    clearPlanningInlineDraft(item.id)
    if (reloadAfterSave)
      await refreshListPresentation(listScrollTop, listScrollLeft)

    if (showSuccessToast)
      toast.add({ title: 'Item atualizado', color: 'success' })

    return true
  }
  catch {
    return false
  }
  finally {
    planningInlineSavingIds.value = planningInlineSavingIds.value.filter(currentId => currentId !== item.id)
  }
}

async function saveAllPlanningInlineEdits() {
  if (!planningPendingEditCount.value || isSavingAllPlanningInlineEdits.value)
    return

  const dirtyIds = new Set(planningInlineDirtyIds.value)
  const itemsToSave = planningInlineEditableItems.value.filter(item => dirtyIds.has(item.id))
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null

  try {
    isSavingAllPlanningInlineEdits.value = true

    for (const item of itemsToSave) {
      const saved = await savePlanningInline(item, { reloadAfterSave: false, showSuccessToast: false })
      if (!saved)
        return
    }

    await refreshListPresentation(listScrollTop, listScrollLeft)
    deactivatePlanningCell()
    toast.add({
      title: 'Edições salvas',
      description: `${itemsToSave.length.toLocaleString('pt-BR')} item(ns) atualizado(s).`,
      color: 'success'
    })
  }
  finally {
    isSavingAllPlanningInlineEdits.value = false
  }
}

function buildBulkEditOverrides(demand: RoadmapDemand, changes: BulkEditRoadmapItemsData): Partial<DemandFormData> {
  const overrides: Partial<DemandFormData> = {}

  if (changes.status) {
    overrides.status = changes.status

    if (changes.status === 'Done')
      overrides.deliveryDate = changes.deliveryDate ?? demand.deliveryDate ?? ''

    if (changes.status === 'Blocked')
      overrides.blockedReason = changes.blockedReason ?? demand.blockedReason ?? ''

    if (changes.status === 'Deprioritized') {
      overrides.observation = changes.observation ?? demand.observation ?? ''
      overrides.deprioritizationReason = changes.deprioritizationReason ?? demand.deprioritizationReason ?? undefined

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

  if (demand.itemType === 'Demand' || (demand.itemType === 'Epic' && demand.isSimple)) {
    if (changes.type)
      overrides.type = changes.type

    if (changes.quarterYear != null && changes.quarterNumber != null) {
      overrides.quarterYear = changes.quarterYear
      overrides.quarterNumber = changes.quarterNumber
    }
  }

  if (Object.prototype.hasOwnProperty.call(changes, 'rowColor'))
    overrides.rowColor = changes.rowColor

  return overrides
}

async function handlePlanningBulkEdit(changes: BulkEditRoadmapItemsData) {
  if (!selectedPlanningItems.value.length || isBulkPlanning.value)
    return

  const updatedCount = selectedPlanningItems.value.length
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null
  const updates = selectedPlanningItems.value.map(item => ({
    id: item.id,
    data: buildDemandFormData(item, buildBulkEditOverrides(item, changes))
  }))
  isBulkPlanning.value = true

  try {
    // Single request + single local mutation (no per-item flicker/slowness).
    await roadmapStore.bulkUpdateDemands(updates)

    planningBulkEditModalOpen.value = false
    clearSelectedDemands()
    await refreshListPresentation(listScrollTop, listScrollLeft)
    toast.add({
      title: 'Itens atualizados em lote',
      description: `${updatedCount.toLocaleString('pt-BR')} itens atualizados com sucesso.`,
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

// Forces the planning table to remount and rebuild the DOM in model order. Needed after a
// drag that didn't change the data (a no-op drop): SortableJS already mutated the DOM, but
// the data-hash watcher won't fire, so the moved row would otherwise stay stuck out of place.
async function forceListRerender(scrollTop?: number | null, scrollLeft?: number | null) {
  planningGroupedRenderNonce.value++
  await refreshListPresentation(scrollTop, scrollLeft)
}

// ─── Soft auto-refresh ──────────────────────────────────────────────────────────
// Re-fetches the planning data (without a full page reload) to reduce stale views when
// another tab/view changed the data. Triggers: 15 min of idle, returning to the tab, and
// switching back into the planning view. Always deferred while the user is "busy" so we never
// discard unsaved work (open form, inline edit, drag, or save in progress).
const SOFT_REFRESH_IDLE_MS = 15 * 60 * 1000
let lastPlanningActivityAt = Date.now()
let lastPlanningSoftRefreshAt = Date.now()
let softRefreshIntervalId: ReturnType<typeof setInterval> | null = null

const isPlanningBusy = computed(() =>
  modalOpen.value
  || activePlanningCell.value != null
  || isBulkPlanning.value
  || isSavingAllPlanningInlineEdits.value
  || isListDragging.value
  || roadmapStore.isLoading
)

// Any "real work" (open form, inline edit, drag, save) counts as activity and pushes back the
// idle timer — and finishing it too, so we don't refresh the instant a modal closes.
watch(isPlanningBusy, () => {
  lastPlanningActivityAt = Date.now()
})

async function softRefreshPlanningData() {
  if (viewMode.value !== 'list' || isPlanningBusy.value)
    return

  lastPlanningSoftRefreshAt = Date.now()
  const scrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const scrollLeft = listScrollContainerRef.value?.scrollLeft ?? null

  try {
    await roadmapStore.fetchDemands()
    await refreshListPresentation(scrollTop, scrollLeft)
  }
  catch {
    // handled by useApi
  }
}

function handlePlanningVisibilityChange() {
  if (document.visibilityState === 'visible')
    void softRefreshPlanningData()
}

function startSoftRefreshWatchers() {
  softRefreshIntervalId = setInterval(() => {
    const idleSince = Math.max(lastPlanningActivityAt, lastPlanningSoftRefreshAt)
    if (Date.now() - idleSince >= SOFT_REFRESH_IDLE_MS)
      void softRefreshPlanningData()
  }, 60 * 1000)

  document.addEventListener('visibilitychange', handlePlanningVisibilityChange)
}

function stopSoftRefreshWatchers() {
  if (softRefreshIntervalId != null) {
    clearInterval(softRefreshIntervalId)
    softRefreshIntervalId = null
  }
  document.removeEventListener('visibilitychange', handlePlanningVisibilityChange)
}

// Switching back into the planning view refreshes its (otherwise frozen) data. Not immediate, so
// it never double-fetches on initial mount (initializeRoadmapPage already loads the data once).
watch(viewMode, (mode) => {
  if (mode === 'list')
    void softRefreshPlanningData()
})

async function planSelectedDemandsToQuarter(quarterValue: string) {
  if (!movablePlanningItems.value.length || isBulkPlanning.value)
    return

  const { quarterYear, quarterNumber } = parseQuarterValue(quarterValue)
  const movedIds = movablePlanningItems.value.map(item => item.id)
  const movedCount = movedIds.length
  const movedSet = new Set(movedIds)
  const listScrollTop = listScrollContainerRef.value?.scrollTop ?? null
  const listScrollLeft = listScrollContainerRef.value?.scrollLeft ?? null

  // Place the moved items at the END of the target quarter (existing items first, moved appended).
  // The backend sets sortOrder per index; ordering only matters within each team scope, so a
  // mixed-team list stays correct (existing < moved within every scope).
  const existingTargetIds = [...demandItems.value, ...epicItems.value.filter(epic => epic.isSimple)]
    .filter(item =>
      item.quarterYear === quarterYear
      && item.quarterNumber === quarterNumber
      && !movedSet.has(item.id))
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map(item => item.id)
  const orderedDemandIds = [...existingTargetIds, ...movedIds]

  isBulkPlanning.value = true

  try {
    // Single request + single local mutation — no per-item flicker/slowness.
    await roadmapStore.bulkMoveDemandsToQuarter(movedIds, quarterYear, quarterNumber, orderedDemandIds)

    // Clear the selection BEFORE rebuilding the list, otherwise the (imperative) epic header
    // checkboxes get rebuilt still marked and stay visually selected.
    selectedDemandIds.value = []
    selectedEpicIds.value = []
    selectedEpicQuarters.value = []

    await refreshListPresentation(listScrollTop, listScrollLeft)

    toast.add({
      title: 'Itens planejados no quarter',
      description: `${movedCount.toLocaleString('pt-BR')} ${movedCount === 1 ? 'item movido' : 'itens movidos'} para ${quarterShortLabel(quarterValue)}`,
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
function getListSortIcon(colId: string) {
  const active = listSorting.value.find(s => s.id === colId)

  if (!active)
    return 'i-lucide-arrow-up-down'

  return active.desc ? 'i-lucide-arrow-down' : 'i-lucide-arrow-up'
}

function getListFilterOptions(col: ListColMeta) {
  if (col.id === 'products') {
    return selectedProjectProducts.value.map(product => ({
      label: product.name,
      value: product.id
    }))
  }

  if (col.id === 'quarterLabel')
    return listQuarterFilterOptions.value

  if (col.id === 'customers')
    return listCustomerFilterOptions.value

  return col.selectOptions ?? []
}

function renderIssueTrigger(issueLinks: Array<{ key: string, url?: string }>) {
  if (!issueLinks.length)
    return null

  if (issueLinks.length === 1 && issueLinks[0]?.url) {
    return h('a', {
      href: issueLinks[0].url,
      target: '_blank',
      rel: 'noopener noreferrer',
      class: 'inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-default bg-default px-1.5 text-[10px] font-medium text-primary transition-colors hover:border-primary/40'
    }, [
      h(UIconComp, { name: 'i-simple-icons-jira', class: 'h-3 w-3' })
    ])
  }

  return h(UPopoverComp, { content: { side: 'bottom', align: 'start', sideOffset: 8 } }, {
    default: () => [
      h('button', {
        type: 'button',
        class: 'inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-default bg-default px-1.5 text-[10px] font-medium text-primary transition-colors hover:border-primary/40'
      }, [
        h(UIconComp, { name: 'i-simple-icons-jira', class: 'h-3 w-3' })
      ])
    ],
    content: () => [
      h('div', { class: 'flex min-w-40 flex-col gap-1 p-1' }, issueLinks.map(issue => issue.url
        ? h('a', {
            href: issue.url,
            target: '_blank',
            rel: 'noopener noreferrer',
            class: 'inline-flex items-center rounded-md border border-default bg-default px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:border-primary/40'
          }, issue.key)
        : h('span', {
            class: 'inline-flex items-center rounded-md border border-default bg-default px-2 py-1.5 text-xs font-medium text-primary'
          }, issue.key)
      ))
    ]
  })
}

function createIssueTriggerElement(issueLinks: Array<{ key: string, url?: string }>) {
  if (!issueLinks.length)
    return null

  if (issueLinks.length === 1 && issueLinks[0]?.url) {
    const anchor = document.createElement('a')
    anchor.href = issueLinks[0].url
    anchor.target = '_blank'
    anchor.rel = 'noopener noreferrer'
    anchor.className = 'inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-default bg-default px-1.5 text-[10px] font-medium text-primary transition-colors hover:border-primary/40'
    anchor.appendChild(createFilledSvgIcon(['M11.571 11.513H0a5.218 5.218 0 0 0 5.232 5.215h2.13v2.057A5.215 5.215 0 0 0 12.575 24V12.518a1.005 1.005 0 0 0-1.005-1.005zm5.723-5.756H5.736a5.215 5.215 0 0 0 5.215 5.214h2.129v2.058a5.218 5.218 0 0 0 5.215 5.214V6.758a1.001 1.001 0 0 0-1.001-1.001zM23.013 0H11.455a5.215 5.215 0 0 0 5.215 5.215h2.129v2.057A5.215 5.215 0 0 0 24 12.483V1.005A1.001 1.001 0 0 0 23.013 0Z'], 'h-3 w-3'))
    return anchor
  }

  const details = document.createElement('details')
  details.className = 'relative inline-flex shrink-0'

  const summary = document.createElement('summary')
  summary.className = 'inline-flex h-5 list-none items-center gap-1 rounded-md border border-default bg-default px-1.5 text-[10px] font-medium text-primary transition-colors hover:border-primary/40 cursor-pointer'
  summary.appendChild(createFilledSvgIcon(['M11.571 11.513H0a5.218 5.218 0 0 0 5.232 5.215h2.13v2.057A5.215 5.215 0 0 0 12.575 24V12.518a1.005 1.005 0 0 0-1.005-1.005zm5.723-5.756H5.736a5.215 5.215 0 0 0 5.215 5.214h2.129v2.058a5.218 5.218 0 0 0 5.215 5.214V6.758a1.001 1.001 0 0 0-1.001-1.001zM23.013 0H11.455a5.215 5.215 0 0 0 5.215 5.215h2.129v2.057A5.215 5.215 0 0 0 24 12.483V1.005A1.001 1.001 0 0 0 23.013 0Z'], 'h-3 w-3'))
  details.appendChild(summary)

  const panel = document.createElement('div')
  panel.className = 'absolute left-0 top-full z-20 mt-2 flex min-w-40 flex-col gap-1 rounded-lg border border-default bg-default p-1 shadow-lg'

  issueLinks.forEach((issue) => {
    const item = issue.url ? document.createElement('a') : document.createElement('span')
    item.className = 'inline-flex items-center rounded-md border border-default bg-default px-2 py-1.5 text-xs font-medium text-primary transition-colors hover:border-primary/40'
    item.textContent = issue.key

    if (item instanceof HTMLAnchorElement) {
      item.href = issue.url
      item.target = '_blank'
      item.rel = 'noopener noreferrer'
    }

    panel.appendChild(item)
  })

  details.appendChild(panel)
  return details
}

function getAdaptiveInlineListDisplay(items: string[], columnWidth: number, separator = ', ') {
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

    if (!visibleItems.length)
      break
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

function renderCustomerTrigger(customers: string[], columnWidth: number) {
  const display = getAdaptiveInlineListDisplay(customers, columnWidth)
  if (!display.items.length)
    return null

  if (display.allVisible) {
    return h('span', {
      class: 'block max-w-full truncate text-[11px] text-highlighted',
      title: display.fullLabel
    }, display.previewLabel)
  }

  return h(UPopoverComp, { content: { side: 'bottom', align: 'start', sideOffset: 8 } }, {
    default: () => [
      h('button', {
        type: 'button',
        class: 'inline-flex max-w-full items-center gap-1 truncate bg-transparent p-0 text-[11px] text-muted transition-colors hover:text-highlighted',
        title: display.fullLabel
      }, [
        h('span', { class: 'max-w-[140px] truncate' }, display.previewLabel),
        h('span', { class: 'shrink-0 text-muted' }, `+${display.hiddenCount}`)
      ])
    ],
    content: () => [
      h('div', { class: 'flex min-w-44 flex-col gap-1 p-2' }, display.items.map(customer => h('span', {
        class: 'text-xs text-highlighted'
      }, customer)))
    ]
  })
}

function createCustomerTriggerElement(customers: string[], columnWidth: number) {
  const display = getAdaptiveInlineListDisplay(customers, columnWidth)
  if (!display.items.length)
    return null

  if (display.allVisible) {
    const label = document.createElement('span')
    label.className = 'block max-w-full truncate text-[11px] text-highlighted'
    label.textContent = display.previewLabel
    label.title = display.fullLabel
    return label
  }

  const details = document.createElement('details')
  details.className = 'relative inline-flex max-w-full'

  const summary = document.createElement('summary')
  summary.className = 'inline-flex max-w-full list-none items-center gap-1 truncate bg-transparent p-0 text-[11px] text-muted transition-colors hover:text-highlighted cursor-pointer'
  summary.title = display.fullLabel

  const previewLabel = document.createElement('span')
  previewLabel.className = 'max-w-[140px] truncate'
  previewLabel.textContent = display.previewLabel
  summary.appendChild(previewLabel)

  const more = document.createElement('span')
  more.className = 'shrink-0 text-muted'
  more.textContent = `+${display.hiddenCount}`
  summary.appendChild(more)
  details.appendChild(summary)

  const panel = document.createElement('div')
  panel.className = 'absolute left-0 top-full z-20 mt-2 flex min-w-44 flex-col gap-1 rounded-lg border border-default bg-default p-2 shadow-lg'

  display.items.forEach((customer) => {
    const item = document.createElement('span')
    item.className = 'text-xs text-highlighted'
    item.textContent = customer
    panel.appendChild(item)
  })

  details.appendChild(panel)
  return details
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

function getPlanningCustomerEditorPixelWidth(): number {
  const columnWidth = getBaseListColPixelWidth('customers', 110)
  return Math.max(240, Math.min(352, columnWidth + 88))
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

watch(
  () => `${viewMode.value}|${quarterFilteredDemands.value.map(demand => `${demand.id}:${demand.parentDemandId ?? 'none'}:${demand.epicId ?? 'none'}:${demand.roadmapId ?? 'none'}:${demand.title}:${demand.quarterYear}:${demand.quarterNumber}:${demand.status}:${demand.sortOrder}:${demand.updatedAt ?? ''}`).join('|')}|${emptyCompositeEpics.value.map(epic => `${epic.id}:${epic.title}:${epic.status}:${epic.updatedAt ?? ''}`).join('|')}|${JSON.stringify(listSorting.value)}|${JSON.stringify(listColumnFilters.value)}|${JSON.stringify(listProblemFilter.value)}|${groupDemandsByEpic.value}|${collapsedEpicIds.value.join('|')}`,
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

  let resizeDebounceTimer: ReturnType<typeof setTimeout> | null = null
  listWidthObserver = new ResizeObserver(() => {
    if (resizeDebounceTimer !== null)
      clearTimeout(resizeDebounceTimer)
    resizeDebounceTimer = setTimeout(() => {
      resizeDebounceTimer = null
      const prevWidth = listViewportWidth.value
      updateListViewportWidth()
      if (listViewportWidth.value !== prevWidth)
        schedulePlanningGroupedHeaderSync(true)
    }, 150)
  })
  listWidthObserver.observe(element)
}, { flush: 'post' })

function updateSectionDividerWidths() {
  const tbody = listTableRootRef.value?.querySelector('tbody')
  if (!tbody) return

  const table = tbody.closest<HTMLTableElement>('table')
  const headerTable = listHeaderRowRef.value?.closest<HTMLTableElement>('table')

  const applyColgroup = (targetTable: HTMLTableElement | null) => {
    if (!targetTable) return
    const colgroup = targetTable.querySelector('colgroup')
    if (!colgroup) return
    const existingCols = Array.from(colgroup.children) as HTMLTableColElement[]
    listOrderedCols.value.forEach((col, index) => {
      if (existingCols[index])
        existingCols[index].style.width = listColWidth(col.id, col.defaultWidth)
    })
  }

  applyColgroup(table)
  applyColgroup(headerTable)

  const gridTemplate = getListGridTemplateColumns()
  tbody.querySelectorAll<HTMLElement>('.list-section-divider .grid').forEach((el) => {
    el.style.gridTemplateColumns = gridTemplate
  })
}

watch(
  () => listTitleExtraWidth.value,
  async () => {
    await nextTick()
    // Only update widths in-place instead of full sync to avoid height changes
    // that would cause scrollbar to appear/disappear, creating a feedback loop
    updateSectionDividerWidths()
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

// Pre-resolve components for use inside cell h() renderers
const UButtonComp = resolveComponent('UButton')
const UIconComp   = resolveComponent('UIcon')
const UPopoverComp = resolveComponent('UPopover')
const UInputComp = resolveComponent('UInput')
const USelectComp = resolveComponent('USelect')

// Inline, editable classification badge shown inside the "Demanda" cell (non-grouped mode).
// Classification is an epic-level attribute, so for a demand it targets its epic.
function renderPlanningClassificationBadge(demand: RoadmapDemand) {
  const target = getPlanningClassificationTarget(demand)
  const classification = getPlanningDraftDisplayItem(target).classification

  if (isPlanningCellEditing(target, 'classification')) {
    return h(USelectComp, {
      modelValue: getPlanningInlineDraft(target).classification,
      items: classificationSelectOptions,
      valueKey: 'value',
      optionAttribute: 'label',
      size: 'xs',
      class: 'w-40 shrink-0',
      disabled: isPlanningInlineSaving(target.id) || isSavingAllPlanningInlineEdits.value,
      'onUpdate:modelValue': (value?: DemandClassification) => {
        if (value)
          updatePlanningInlineDraft(target, { classification: value })
      },
      onBlur: () => deactivatePlanningCell(target.id, 'classification')
    })
  }

  return h('button', {
    type: 'button',
    class: `inline-flex shrink-0 items-center rounded-full border px-1.5 py-0.5 text-[10px] font-medium transition-colors ${classificationBadgeClass[classification]} ${getPlanningEditableCellButtonClass(target)}`,
    disabled: isPlanningInlineSaving(target.id) || isSavingAllPlanningInlineEdits.value,
    title: target.itemType === 'Epic' && target.id !== demand.id ? 'Classificação do épico' : 'Classificação',
    onClick: (event: MouseEvent) => {
      event.preventDefault()
      event.stopPropagation()
      activatePlanningCell(target, 'classification')
    }
  }, classificationLabels[classification])
}

function renderDependencyIcon(dependency: DemandDependency, relation: 'dependsOn' | 'dependedOnBy', demand?: RoadmapDemand) {
  const inconsistent = relation === 'dependsOn' && !!demand && isDependencyInconsistent(demand, dependency)
  const title = relation === 'dependsOn'
    ? `${getDependencyTooltip('É bloqueado por', dependency)}${inconsistent ? `\n\nInconsistência: a demanda vinculada está em ${dependency.quarterLabel}, depois de ${demand!.quarterLabel}, ou sem priorização.` : ''}`
    : getDependencyTooltip('Bloqueia', dependency)

  return h('button', {
    type: 'button',
    class: inconsistent
      ? 'inline-flex h-4 w-4 shrink-0 items-center justify-center rounded border border-red-200 bg-red-50 text-red-600 transition-colors hover:border-red-300 hover:bg-red-100 dark:border-red-800 dark:bg-red-900/30 dark:text-red-400'
      : 'inline-flex h-4 w-4 shrink-0 items-center justify-center rounded border border-red-200 bg-red-50 text-red-600 transition-colors hover:border-red-300 hover:bg-red-100 dark:border-red-800 dark:bg-red-900/30 dark:text-red-400',
    title,
    onClick: () => openDependencyDemand(dependency)
  }, [
    h(UIconComp, {
      name: relation === 'dependsOn' ? 'i-lucide-lock' : 'i-lucide-lock-open',
      class: 'h-2.5 w-2.5'
    })
  ])
}

function renderDependencyBadge(dependency: DemandDependency) {
  return renderDependencyIcon(dependency, 'dependedOnBy')
}

function renderDependsOnBadge(demand: RoadmapDemand, dependency: DemandDependency) {
  return renderDependencyIcon(dependency, 'dependsOn', demand)
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

      return h('div', { class: 'flex flex-col items-center justify-center gap-0.5' }, [
        h('div', { class: 'flex items-center justify-center gap-1.5' }, [
          h('span', {
            class: 'list-priority-handle inline-flex h-5 w-5 shrink-0 items-center justify-center rounded border border-default bg-elevated text-muted transition-colors hover:border-primary/40 hover:text-highlighted cursor-grab active:cursor-grabbing',
            title: 'Arrastar para repriorizar'
          }, [h(UIconComp, { name: 'i-lucide-grip-vertical', class: 'h-3 w-3' })]),
          h('label', { class: 'flex shrink-0 items-center justify-center' }, [
            h('input', {
              type: 'checkbox',
              class: ['h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary', isDemandLocked(demand.id) ? 'cursor-not-allowed opacity-70' : ''],
              checked: isDemandSelected(demand.id),
              disabled: isDemandLocked(demand.id),
              title: isDemandLocked(demand.id) ? 'Selecionado pelo épico — desmarque o épico para alterar' : undefined,
              onClick: (event: Event) => event.stopPropagation(),
              onChange: (event: Event) => toggleDemandSelection(demand.id, (event.target as HTMLInputElement).checked)
            })
          ])
        ]),
        h('span', { class: 'text-[9px] font-medium text-muted' }, String(priorityRankByDemandId.value[demand.id] ?? ''))
      ])
    }
  },
  {
    accessorKey: 'title',
    header: 'Demanda',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => left.title.localeCompare(right.title, 'pt-BR')),
    enableColumnFilter: true,
    filterFn: (row, _colId, filterValue: { text?: string, classifications?: string[] }) => {
      const d = row.original
      const q = (filterValue?.text ?? '').trim().toLowerCase()
      const classifications = filterValue?.classifications ?? []

      if (classifications.length) {
        const classification = getEffectiveDemandClassification(d)
        if (!classification || !classifications.includes(classification))
          return false
      }

      if (!q)
        return true

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
    size: 360,
    meta: { style: { td: () => ({ width: listColWidth('title', 360) }), th: () => ({ width: listColWidth('title', 360) }) } },
    cell: ({ row }) => {
      const d = row.original
      const isDeprioritized = d.status === 'Deprioritized'
      const displayItem = getPlanningDraftDisplayItem(d)
      const textNodes = []
      const issueLinks = getDisplayIssueLinks(d)
      const isSimpleEpic = d.itemType === 'Epic' && d.isSimple
      if (isCollapsedRepresentative(d))
        return h('span', { class: 'hidden' })
      // For simple epics in non-grouped mode: show roadmap as "parent" above
      if (!groupDemandsByEpic.value && isSimpleEpic && d.roadmapTitle) {
        textNodes.push(
          h('div', { class: 'mb-0.5 flex min-w-0 items-center gap-1.5' }, [
            h(UIconComp, { name: 'i-lucide-layout-list', class: 'h-3.5 w-3.5 shrink-0 text-muted' }),
            h('span', { class: 'min-w-0 flex-1 truncate text-[10px] text-muted', title: d.roadmapTitle }, d.roadmapTitle),
          ])
        )
      }
      // For regular demands in non-grouped mode: show parent epic info above (skip for simple epics to avoid self-reference)
      if (!groupDemandsByEpic.value && d.epicTitle && !isSimpleEpic) {
        const epic = d.epicId ? itemsById.value.get(d.epicId) : null
        const isEpicDeprioritized = epic?.itemType === 'Epic' && epic.status === 'Deprioritized'
        textNodes.push(
          h('div', { class: 'mb-0.5 flex min-w-0 items-center gap-1.5' }, [
            h(UIconComp, { name: 'i-lucide-star', class: 'h-3.5 w-3.5 shrink-0 text-amber-500' }),
            h('span', {
              class: `min-w-0 flex-1 truncate text-[10px] ${isEpicDeprioritized ? 'line-through text-muted opacity-50' : 'text-muted'}`,
              title: d.epicTitle
            }, d.epicTitle),
          ])
        )
      }
      if (groupDemandsByEpic.value && d.epicId && !isSimpleEpic) {
        const groupedContentNodes = [
          h('div', { class: 'flex items-start gap-1.5' }, [
            h(UIconComp, { name: 'i-lucide-list-todo', class: 'mt-0.5 h-3.5 w-3.5 shrink-0 text-sky-600' }),
            h('div', { class: 'min-w-0 flex-1' }, [
              h('div', { class: 'flex min-w-0 items-center gap-1.5' }, [
                isPlanningCellEditing(d, 'title')
                  ? h(UInputComp, {
                      modelValue: getPlanningInlineDraft(d).title,
                      size: 'xs',
                      autofocus: true,
                      class: 'min-w-0 flex-1',
                      disabled: isPlanningInlineSaving(d.id) || isSavingAllPlanningInlineEdits.value,
                      'onUpdate:modelValue': (value?: string | number) => updatePlanningInlineDraft(d, { title: String(value ?? '') }),
                      onBlur: () => deactivatePlanningCell(d.id, 'title'),
                      onKeydown: (event: KeyboardEvent) => {
                        if (event.key === 'Escape' || event.key === 'Enter')
                          deactivatePlanningCell(d.id, 'title')
                      }
                    })
                  : h('button', {
                      type: 'button',
                      class: `min-w-0 w-full flex-1 truncate rounded-md border px-1 py-0.5 text-left text-[12px] font-medium transition-colors ${isDeprioritized ? 'line-through text-muted' : 'text-highlighted'} ${getPlanningEditableCellButtonClass(d)}`,
                      title: d.description || undefined,
                      disabled: isPlanningInlineSaving(d.id) || isSavingAllPlanningInlineEdits.value,
                      onClick: () => activatePlanningCell(d, 'title')
                    }, displayItem.title),
                ...d.dependsOn.map(dep => renderDependencyIcon(dep, 'dependsOn', d)),
                ...d.dependedOnBy.map(dep => renderDependencyIcon(dep, 'dependedOnBy')),
                ...(getDemandProblemKeys(d).length ? [h(UIconComp, { name: 'i-lucide-triangle-alert', class: 'h-3.5 w-3.5 shrink-0 text-warning', title: getDemandProblemTooltip(d) })] : []),
                ...(renderIssueTrigger(issueLinks) ? [renderIssueTrigger(issueLinks)!] : [
                  h('button', { type: 'button', class: 'inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-red-200 bg-default px-1.5 text-[10px] font-medium text-red-500 transition-colors hover:border-red-400 dark:border-red-800 dark:text-red-400', title: 'Sem issue Jira — clique para adicionar', onClick: () => openEditModal(d, undefined, 'jiraIssue') }, [h(UIconComp, { name: 'i-simple-icons-jira', class: 'h-3 w-3' })])
                ])
              ])
            ])
          ])
        ]

        textNodes.push(h('div', { class: 'pl-5' }, groupedContentNodes))
      }
      else {
        textNodes.push(h('div', { class: 'flex min-w-0 items-center gap-1.5' }, [
          h(UIconComp, { name: isSimpleEpic ? 'i-lucide-star' : 'i-lucide-list-todo', class: `h-3.5 w-3.5 shrink-0 ${isSimpleEpic ? 'text-amber-500' : 'text-sky-600'}` }),
          isPlanningCellEditing(d, 'title')
            ? h(UInputComp, {
                modelValue: getPlanningInlineDraft(d).title,
                size: 'xs',
                autofocus: true,
                class: 'min-w-0 flex-1',
                disabled: isPlanningInlineSaving(d.id) || isSavingAllPlanningInlineEdits.value,
                'onUpdate:modelValue': (value?: string | number) => updatePlanningInlineDraft(d, { title: String(value ?? '') }),
                onBlur: () => deactivatePlanningCell(d.id, 'title'),
                onKeydown: (event: KeyboardEvent) => {
                  if (event.key === 'Escape' || event.key === 'Enter')
                    deactivatePlanningCell(d.id, 'title')
                }
              })
            : h('button', {
                type: 'button',
                class: `min-w-0 flex-1 truncate rounded-md border px-1 py-0.5 text-left text-[12px] font-medium transition-colors ${isDeprioritized ? 'line-through text-muted' : 'text-highlighted'} ${getPlanningEditableCellButtonClass(d)}`,
                title: d.description || undefined,
                disabled: isPlanningInlineSaving(d.id) || isSavingAllPlanningInlineEdits.value,
                onClick: () => activatePlanningCell(d, 'title')
              }, displayItem.title),
          ...d.dependsOn.map(dep => renderDependencyIcon(dep, 'dependsOn', d)),
          ...d.dependedOnBy.map(dep => renderDependencyIcon(dep, 'dependedOnBy')),
          // When NOT grouped, classification + problem + jira move to the right-aligned stack.
          ...(groupDemandsByEpic.value && getDemandProblemKeys(d).length ? [h(UIconComp, { name: 'i-lucide-triangle-alert', class: 'h-3.5 w-3.5 shrink-0 text-warning', title: getDemandProblemTooltip(d) })] : []),
          ...(d.successorDemandId
            ? [h('span', {
                class: 'inline-flex shrink-0 items-center gap-0.5 rounded border border-amber-200 bg-amber-50 px-1 py-0 text-[8px] font-medium text-amber-600 dark:border-amber-800 dark:bg-amber-900/20 dark:text-amber-300',
                title: 'Possui transbordo'
              }, [
                h(UIconComp, { name: 'i-lucide-forward', class: 'h-2.5 w-2.5' }),
                (() => { const s = demandItems.value.find(x => x.id === d.successorDemandId); return s ? `→ ${s.quarterLabel}` : '→' })()
              ])]
            : []),
          ...(groupDemandsByEpic.value
            ? (renderIssueTrigger(issueLinks) ? [renderIssueTrigger(issueLinks)!] : [
                !issueLinks.length ? h('button', { type: 'button', class: 'inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-red-200 bg-default px-1.5 text-[10px] font-medium text-red-500 transition-colors hover:border-red-400 dark:border-red-800 dark:text-red-400', title: 'Sem issue Jira — clique para adicionar', onClick: () => openEditModal(d, undefined, 'jiraIssue') }, [h(UIconComp, { name: 'i-simple-icons-jira', class: 'h-3 w-3' })]) : null
              ].filter(Boolean))
            : [])
        ]))
      }

      const inconsistentDeps = d.dependsOn.filter(dep => isDependencyInconsistent(d, dep))
      const inconsistentReverseDeps = (d.dependedOnBy ?? []).filter(dep => isReverseDependencyInconsistent(d, dep))
      const totalInconsistentCount = inconsistentDeps.length + inconsistentReverseDeps.length
      if (totalInconsistentCount > 0) {
        const tooltipText = [
          ...inconsistentDeps.map(dep =>
            `${getDependencyTooltip('É bloqueado por', dep)}\n\nInconsistência: a demanda vinculada está em ${dep.quarterLabel}, depois de ${d.quarterLabel}, ou sem priorização.`
          ),
          ...inconsistentReverseDeps.map(dep =>
            `${getDependencyTooltip('Bloqueia', dep)}\n\nInconsistência: ${dep.title} está em ${dep.quarterLabel}, antes de ${d.quarterLabel}.`
          ),
        ].join('\n\n')
        textNodes.push(
          h('div', {
            class: 'mt-0.5 flex items-center gap-1 rounded border border-red-300/70 bg-red-50/60 px-1.5 py-0.5 text-[10px] font-medium text-red-700 dark:border-red-800/50 dark:bg-red-900/15 dark:text-red-300/90',
            title: tooltipText
          }, [
            h(UIconComp, { name: 'i-lucide-triangle-alert', class: 'h-3 w-3 shrink-0' }),
            h('span', {}, 'Dependência inconsistente'),
            h('span', {}, `(${totalInconsistentCount})`)
          ])
        )
      }

      // Non-grouped right-aligned stack (per prototype):
      //   row 1: [classification] [epic's jira issue]
      //   row 2:                  [problem?] [demand's jira issue]   (demands only)
      if (!groupDemandsByEpic.value) {
        const makeJiraNode = (item: RoadmapDemand, emptyTitle: string) =>
          renderIssueTrigger(getDisplayIssueLinks(item))
          ?? h('button', { type: 'button', class: 'inline-flex h-5 shrink-0 items-center gap-1 rounded-md border border-red-200 bg-default px-1.5 text-[10px] font-medium text-red-500 transition-colors hover:border-red-400 dark:border-red-800 dark:text-red-400', title: emptyTitle, onClick: () => openEditModal(item, undefined, 'jiraIssue') }, [h(UIconComp, { name: 'i-simple-icons-jira', class: 'h-3 w-3' })])

        // Epic-level jira: parent epic for a demand, the epic itself for a simple epic.
        const epicForJira = isSimpleEpic ? d : (d.epicId ? itemsById.value.get(d.epicId) ?? null : null)
        const epicJiraNode = epicForJira ? makeJiraNode(epicForJira, 'Épico sem issue Jira — clique para adicionar') : null

        // Problem indicator applies to demands and simple epics; the demand-level jira only to demands.
        const problemNode = getDemandProblemKeys(d).length
          ? h(UIconComp, { name: 'i-lucide-triangle-alert', class: 'h-3.5 w-3.5 shrink-0 text-warning', title: getDemandProblemTooltip(d) })
          : null
        const demandJiraNode = !isSimpleEpic ? makeJiraNode(d, 'Sem issue Jira — clique para adicionar') : null

        // Simple epics have no demand-level jira; reserve an invisible slot (same size as the
        // jira button) so the problem icon stays aligned with the rows that DO have one.
        const jiraSlot = demandJiraNode
          ?? (isSimpleEpic && problemNode
            ? h('span', { 'class': 'invisible inline-flex h-5 shrink-0 items-center gap-1 rounded-md border px-1.5', 'aria-hidden': 'true' }, [h(UIconComp, { name: 'i-simple-icons-jira', class: 'h-3 w-3' })])
            : null)

        const topRow = [renderPlanningClassificationBadge(d), epicJiraNode].filter(Boolean)
        const bottomRow = [problemNode, jiraSlot].filter(Boolean)

        return h('div', { class: 'flex min-w-0 items-start justify-between gap-2' }, [
          h('div', { class: 'min-w-0 flex-1' }, textNodes),
          h('div', { class: 'flex shrink-0 flex-col items-end gap-0.5' }, [
            h('div', { class: 'flex items-center gap-1' }, topRow),
            bottomRow.length ? h('div', { class: 'flex items-center gap-1' }, bottomRow) : null
          ].filter(Boolean))
        ])
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
      const isClickable = summary.actionLabel !== 'Associe a demanda a um épico' && canEditRoadmap.value

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
    filterFn: (row, _colId, filterValue: { quarters?: string[], types?: string[] }) => {
      const quarters = filterValue?.quarters ?? []
      const types = filterValue?.types ?? []
      if (quarters.length && !quarters.includes(buildQuarterValue(row.original.quarterYear, row.original.quarterNumber)))
        return false
      if (types.length && !types.includes(row.original.type))
        return false
      return true
    },
    size: 112,
    meta: { style: { td: () => ({ width: listColWidth('quarterLabel', 112) }), th: () => ({ width: listColWidth('quarterLabel', 112) }) } },
    cell: ({ row }) => {
      const demand = row.original
      if (isCollapsedRepresentative(demand))
        return h('span', { class: 'text-xs text-muted' }, '—')

      const displayItem = getPlanningDraftDisplayItem(demand)
      const quarterNode = isSpecialBacklogQuarter(displayItem.quarterYear, displayItem.quarterNumber)
        ? h('span', {
          class: 'text-[9px] font-semibold uppercase tracking-[0.08em] text-highlighted'
        }, planningQuarterDisplayLabel(displayItem))
        : h('span', { class: 'text-[10px] font-mono text-highlighted' }, planningQuarterDisplayLabel(displayItem))

      if (isPlanningCellEditing(demand, 'quarterType')) {
        return h('div', {
          class: 'relative z-20 flex min-w-0 flex-col gap-1',
          style: {
            width: '18rem',
            maxWidth: '18rem'
          }
        }, [
          h(USelectComp, {
            modelValue: getPlanningInlineDraft(demand).quarterValue,
            items: planningQuarterOptions.value,
            valueKey: 'value',
            optionAttribute: 'label',
            size: 'xs',
            class: 'w-full',
            disabled: isPlanningInlineSaving(demand.id) || isSavingAllPlanningInlineEdits.value,
            'onUpdate:modelValue': (value?: string) => {
              if (value)
                updatePlanningInlineDraft(demand, { quarterValue: value })
            }
          }),
          h(USelectComp, {
            modelValue: getPlanningInlineDraft(demand).type,
            items: [
              { label: 'Planejado', value: 'Planned' },
              { label: 'Transbordo', value: 'Spillover' },
              { label: 'Não Planejado', value: 'Unplanned' },
              { label: 'Adicional', value: 'Additional' }
            ],
            valueKey: 'value',
            optionAttribute: 'label',
            size: 'xs',
            class: 'w-full',
            disabled: isPlanningInlineSaving(demand.id) || isSavingAllPlanningInlineEdits.value,
            'onUpdate:modelValue': (value?: DemandType) => {
              if (value)
                updatePlanningInlineDraft(demand, { type: value })
            },
            onBlur: () => deactivatePlanningCell(demand.id, 'quarterType')
          })
        ])
      }

      return h('div', { class: 'flex min-w-0 flex-col gap-0.5' }, [
        h('button', {
          type: 'button',
          class: `flex min-w-0 flex-col items-start gap-0.5 rounded-md border px-1 py-0.5 text-left transition-colors ${getPlanningEditableCellButtonClass(demand)}`,
          disabled: isPlanningInlineSaving(demand.id) || isSavingAllPlanningInlineEdits.value,
          onClick: () => activatePlanningCell(demand, 'quarterType')
        }, [
          quarterNode,
          h('span', { class: `whitespace-nowrap text-[10px] font-medium ${typeColors[getPlanningInlineDraft(demand).type]}` }, typeLabels[getPlanningInlineDraft(demand).type])
        ])
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
    size: 148,
    meta: { style: { td: () => ({ width: listColWidth('products', 148) }), th: () => ({ width: listColWidth('products', 148) }) } },
  },
  {
    accessorKey: 'customers',
    header: 'Clientes',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => formatDemandCustomers(getEffectiveDemandCustomers(left)).localeCompare(formatDemandCustomers(getEffectiveDemandCustomers(right)), 'pt-BR')),
    enableColumnFilter: true,
    filterFn: (row, _colId, filterValue: string[]) => {
      if (!Array.isArray(filterValue) || !filterValue.length) return true
      const customers = getEffectiveDemandCustomers(row.original)
      return filterValue.some(name => customers.includes(name))
    },
    size: 110,
    meta: { style: { td: () => ({ width: listColWidth('customers', 110) }), th: () => ({ width: listColWidth('customers', 110) }) } },
    cell: ({ row }) => {
      if (isCollapsedRepresentative(row.original))
        return h('span', { class: 'text-xs text-muted' }, '—')

      const customers = getEffectiveDemandCustomers(row.original)
      if (!customers.length)
        return h('span', { class: 'text-xs text-muted' }, '—')

      return renderCustomerTrigger(customers, getBaseListColPixelWidth('customers', 110))
    },
  },
  {
    id: 'conclusion',
    header: 'Conclusão',
    accessorFn: row => getDisplayedConclusionDate(row) ?? '',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => (getDisplayedConclusionDate(left) ?? '').localeCompare(getDisplayedConclusionDate(right) ?? '', 'pt-BR')),
    enableColumnFilter: false,
    size: 118,
    meta: { style: { td: () => ({ width: listColWidth('conclusion', 118) }), th: () => ({ width: listColWidth('conclusion', 118) }) } },
  },
  {
    accessorKey: 'hours',
    header: 'Hrs',
    enableSorting: true,
    sortingFn: withListGroupSorting((left, right) => (left.hours ?? 0) - (right.hours ?? 0)),
    enableColumnFilter: false,
    size: 60,
    meta: { class: { td: 'text-right' }, style: { td: () => ({ width: listColWidth('hours', 60) }), th: () => ({ width: listColWidth('hours', 60) }) } },
  },
  {
    id: '_actions',
    header: '',
    enableSorting: false,
    enableColumnFilter: false,
    size: 40,
    meta: { class: { td: 'overflow-visible relative !px-0' }, style: { td: () => ({ width: listColWidth('_actions', 40) }), th: () => ({ width: listColWidth('_actions', 40) }) } },
    cell: ({ row }) => {
      const demand = row.original
      if (isCollapsedRepresentative(demand)) {
        return h('div', { class: 'flex items-center justify-center py-0.5' }, [
          h(UButtonComp, {
            size: 'xs',
            variant: 'ghost',
            color: 'neutral',
            class: 'h-6 w-6 rounded-md border border-default bg-default p-0',
            onClick: () => toggleEpicCollapse(demand.epicId, demand.quarterYear, demand.quarterNumber)
          }, {
            default: () => h(UIconComp, { name: 'i-lucide-chevron-right', class: 'h-4 w-4' })
          })
        ])
      }

      const actionSlots = []
      const kpiSummary = getDemandKpiSummary(demand)

      // 2nd: Agendar (backlog only)
      if (isBacklogDemand(demand) && canEditRoadmap.value) {
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
            content: () => h('div', { class: 'max-h-72 w-64 overflow-y-auto py-1' }, planningQuarterOptions.value.map(option =>
              h('button', {
                class: 'w-full truncate px-3 py-2 text-left text-sm text-highlighted transition-colors hover:bg-elevated',
                onClick: () => planDemandToQuarter(demand, option.value)
              }, option.label)
            ))
          })
        )
      }

      // 3rd: KPI (abrir workspace é edição — só com permissão)
      if (canEditRoadmap.value && kpiSummary.actionLabel !== 'Associe a demanda a um épico') {
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

      // Nth: Cor da linha
      if (!isCollapsedRepresentative(demand) && canEditRoadmap.value) {
        actionSlots.push(
          h(UPopoverComp, { content: { side: 'left', sideOffset: 8 } }, {
            default: () => h(UButtonComp, {
              size: 'xs',
              variant: 'ghost',
              color: 'neutral',
              class: 'h-6 w-6 p-0',
              title: 'Cor da linha',
              style: demand.rowColor ? { color: LIST_ROW_COLORS.find(c => c.id === demand.rowColor)?.hex } : {}
            }, { default: () => h(UIconComp, { name: 'i-lucide-palette', class: 'h-4 w-4' }) }),
            content: () => h('div', { class: 'p-2' }, [
              h('p', { class: 'mb-2 text-xs font-medium text-muted' }, 'Cor da linha'),
              h('div', { class: 'flex flex-wrap gap-1.5' }, [
                h('button', {
                  type: 'button',
                  class: 'flex h-5 w-5 items-center justify-center rounded border border-default transition-colors hover:border-primary/40',
                  title: 'Sem cor',
                  onClick: () => setDemandRowColor(demand, null)
                }, [h(UIconComp, { name: 'i-lucide-x', class: 'h-3 w-3 text-muted' })]),
                ...LIST_ROW_COLORS.map(color =>
                  h('button', {
                    type: 'button',
                    class: `h-5 w-5 rounded-full transition-all hover:scale-110 ${demand.rowColor === color.id ? 'ring-2 ring-offset-1 ring-highlighted' : ''}`,
                    style: { backgroundColor: color.hex },
                    title: color.label,
                    onClick: () => setDemandRowColor(demand, color.id)
                  })
                )
              ])
            ])
          })
        )
      }

      // 4th: Editar / 5th: Copiar / 6th: Excluir — só com permissão de edição
      if (canEditRoadmap.value) {
        actionSlots.push(
          h(UButtonComp, {
            size: 'xs',
            variant: 'ghost',
            color: 'neutral',
            class: 'h-6 w-6 p-0',
            title: 'Editar demanda',
            onClick: () => openEditModal(demand)
          }, {
            default: () => h(UIconComp, { name: 'i-lucide-pencil', class: 'h-4 w-4' })
          })
        )

        actionSlots.push(
          h(UButtonComp, {
            size: 'xs',
            variant: 'ghost',
            color: 'neutral',
            class: 'h-6 w-6 p-0',
            title: 'Copiar demanda',
            onClick: () => openCopyModal(demand)
          }, {
            default: () => h(UIconComp, { name: 'i-lucide-copy', class: 'h-4 w-4' })
          })
        )

        actionSlots.push(
          h(UButtonComp, { icon: 'i-lucide-trash-2', size: 'xs', variant: 'ghost', color: 'error', class: 'h-6 w-6 p-0', title: 'Excluir demanda', onClick: () => promptDelete(demand.id) })
        )
      }

      return h('div', {
        class: 'group absolute inset-0 flex items-center justify-center'
      }, [
        // Dots indicator — visible when not hovering
        h('span', {
          class: 'pointer-events-none select-none text-[10px] text-muted/40 transition-opacity group-hover:opacity-0'
        }, '···'),
        // Action panel — absolutely positioned, floats left on hover
        h('div', {
          class: 'pointer-events-none absolute inset-y-0 right-0 z-30 flex items-center gap-0.5 rounded-md border border-default/60 bg-default/95 px-1 opacity-0 shadow-md backdrop-blur-sm transition-opacity group-hover:pointer-events-auto group-hover:opacity-100'
        }, actionSlots)
      ])
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
    if (c.id === 'conclusion') {
      const conclusionDate = getDisplayedConclusionDate(row)
      const conclusionLabel = conclusionDate ? formatDemandDate(conclusionDate) : ''
      return showDemandDelayMarker(row) ? `${conclusionLabel}${conclusionLabel ? ' · ' : ''}Atrasado` : conclusionLabel
    }
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
  stopSoftRefreshWatchers()
  listWidthObserver?.disconnect()
  if (listExportUrlVisible.value) URL.revokeObjectURL(listExportUrlVisible.value)
  if (listExportUrlFull.value) URL.revokeObjectURL(listExportUrlFull.value)
})

async function initializeRoadmapPage() {
  // Parâmetros vindos da home (dashboard): abrem a planejamento já filtrada e têm
  // precedência sobre os filtros em cache. Esquema: ?teams=&quarters=&dfk=&dfv=
  const queryStr = (key: string) => (typeof route.query[key] === 'string' ? route.query[key] as string : null)
  const urlTeams = queryStr('teams')?.split(',').map(v => v.trim()).filter(Boolean) ?? null
  const urlQuarters = queryStr('quarters')?.split(',').map(v => v.trim()).filter(Boolean) ?? null
  const dfKind = queryStr('dfk')
  const dfValue = queryStr('dfv')

  // Restore filters that don't require loaded data first (quarter + display mode).
  const cachedQuarters = readCacheJson<string[]>(CACHE_KEY_PLANNING_QUARTERS)
  filterQuarters.value = urlQuarters ?? (cachedQuarters?.length ? cachedQuarters : [`${currentQuarterNumber}-${currentYear}`])

  const cachedGroupByEpic = readCacheJson<boolean>(CACHE_KEY_PLANNING_GROUP_BY_EPIC)
  if (cachedGroupByEpic !== null) groupDemandsByEpic.value = cachedGroupByEpic

  await roadmapStore.fetchProjects()

  // Validate project IDs (da URL ou do cache) against loaded projects.
  const cachedProjectIds = readCacheJson<string[]>(CACHE_KEY_PLANNING_PROJECTS)
  filterListProjectIds.value = (urlTeams ?? cachedProjectIds ?? []).filter(id => projects.value.some(p => p.id === id))

  const queryProjectId = typeof route.query.projectId === 'string'
    ? route.query.projectId
    : null

  // For hierarchy view, restore project selection from URL
  // For list view, keep selectedProjectId = null so all demands are loaded
  if (viewMode.value === 'hierarchy' && queryProjectId && projects.value.some(project => project.id === queryProjectId))
    selectedProjectId.value = queryProjectId

  await Promise.all([
    roadmapStore.fetchDemands(),
    roadmapStore.fetchDependencyOptions(),
    roadmapStore.fetchCustomerSuggestions()
  ])

  // Aplica o filtro do item clicado no dashboard da home. Feito por último: o watcher de
  // filterListProjectIds (que zera filtros ao trocar de time) já rodou, então este sobrevive.
  applyDashboardQueryFilter(dfKind, dfValue)

  if (activeDemandKpiId.value)
    await kpiStore.fetchKpis()
}

// Aplica na lista o filtro correspondente ao item de dashboard vindo na URL (dfk/dfv).
function applyDashboardQueryFilter(kind: string | null, value: string | null) {
  if (!kind) return
  switch (kind) {
    case 'status': if (value) toggleListMultiFilterValue('status', value); break
    case 'classification': if (value) toggleTitleClassification(value); break
    case 'customer': if (value) toggleListMultiFilterValue('customers', value); break
    case 'type': if (value) toggleTypeFilterValue(value); break
    case 'problem': if (value) toggleListProblemFilter(value); break
    case 'inconsistentDeps': toggleInconsistentDepsFilter(); break
    case 'spilloverReason': if (value) toggleSpilloverReasonFilter(value); break
    case 'deprioritizationReason': if (value) toggleDeprioritizationReasonFilter(value); break
  }
}

void initializeRoadmapPage()

watch(() => route.query.view, (value) => {
  viewMode.value = value === 'hierarchy' ? 'hierarchy' : 'list'
}, { immediate: true })

watch(activeDemandKpiId, async (value) => {
  if (!value) {
    // Returning from the KPI workspace: refetch so KPI links/measurements (and the resulting
    // problem flags, e.g. "Concluído sem KPI apurado") are fresh in the planning list.
    await roadmapStore.fetchDemands()
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
          @saved="closeDemandKpiWorkspace"
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
          <UDropdownMenu v-if="canEditRoadmap" :items="createMenuItems">
            <UButton icon="i-lucide-plus" label="Novo Item" />
          </UDropdownMenu>
        </div>
      </div>

      <div class="mt-4 flex flex-wrap items-center gap-2">
        <template v-if="viewMode === 'hierarchy'">
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
        </template>

        <template v-else>
          <!-- Time -->
          <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
            <button class="flex items-center gap-1.5 rounded-lg border border-default bg-background px-3 py-1.5 text-sm transition-colors hover:border-primary/40">
              <UIcon name="i-lucide-folder-kanban" class="w-3.5 h-3.5 shrink-0 text-muted" />
              <span class="text-left truncate text-highlighted">{{ filterListProjectsLabel }}</span>
              <UBadge v-if="filterListProjectIds.length" size="xs" color="primary" variant="solid" class="shrink-0">{{ filterListProjectIds.length }}</UBadge>
              <UIcon name="i-lucide-chevron-down" class="w-3.5 h-3.5 shrink-0 text-muted" />
            </button>
            <template #content>
              <div class="py-1 min-w-[220px] max-h-72 overflow-y-auto">
                <button
                  class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                  :class="filterListProjectIds.length === 0 ? 'text-primary font-medium' : 'text-highlighted'"
                  @click="filterListProjectIds = []"
                >
                  <UIcon v-if="filterListProjectIds.length === 0" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0" />
                  <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                  Todos os times
                </button>
                <button
                  v-for="project in sortedProjects"
                  :key="project.id"
                  class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                  :class="filterListProjectIds.includes(project.id) ? 'text-primary' : 'text-highlighted'"
                  @click="toggleListProjectFilter(project.id)"
                >
                  <UIcon v-if="filterListProjectIds.includes(project.id)" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0 text-primary" />
                  <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                  {{ project.name }}
                </button>
              </div>
            </template>
          </UPopover>

          <!-- Quarter -->
          <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
            <button class="flex items-center gap-1.5 rounded-lg border border-default bg-background px-3 py-1.5 text-sm transition-colors hover:border-primary/40">
              <UIcon name="i-lucide-calendar" class="w-3.5 h-3.5 shrink-0 text-muted" />
              <span class="text-left truncate text-highlighted">{{ quarterFilterLabel }}</span>
              <UBadge v-if="filterQuarters.length" size="xs" color="primary" variant="solid" class="shrink-0">{{ filterQuarters.length }}</UBadge>
              <UIcon name="i-lucide-chevron-down" class="w-3.5 h-3.5 shrink-0 text-muted" />
            </button>
            <template #content>
              <div class="min-w-[260px]">
                <!-- Facilitador por ano: seleciona todos os quarters do ano de uma vez -->
                <div class="flex flex-wrap items-center gap-1.5 border-b border-default px-3 py-2">
                  <span class="text-[11px] font-medium text-muted">Anos:</span>
                  <button
                    v-for="group in quarterYearOptions"
                    :key="`year-${group.year}`"
                    type="button"
                    class="rounded-full border px-2 py-0.5 text-[11px] font-medium transition-colors"
                    :class="isQuarterYearFullySelected(group.values) ? 'border-primary/50 bg-primary/10 text-primary' : 'border-default text-highlighted hover:border-primary/40'"
                    :title="`Selecionar todos os quarters de ${group.year}`"
                    @click="toggleQuarterYear(group.values)"
                  >
                    {{ group.year }}
                  </button>
                </div>
                <div class="py-1 max-h-72 overflow-y-auto">
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
              </div>
            </template>
          </UPopover>

          <!-- Exibição -->
          <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
            <button
              class="flex items-center gap-1.5 rounded-lg border bg-background px-3 py-1.5 text-sm transition-colors hover:border-primary/40"
              :class="!groupDemandsByEpic ? 'border-primary/40 text-primary' : 'border-default'"
            >
              <UIcon name="i-lucide-layout-list" class="w-3.5 h-3.5 shrink-0 text-muted" />
              <span class="text-highlighted">Exibição</span>
              <UIcon name="i-lucide-chevron-down" class="w-3.5 h-3.5 shrink-0 text-muted" />
            </button>
            <template #content>
              <div class="min-w-48 space-y-0.5 p-1">
                <button
                  type="button"
                  class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                  :class="groupDemandsByEpic ? 'text-primary' : 'text-highlighted'"
                  @click="groupDemandsByEpic = !groupDemandsByEpic"
                >
                  <UIcon v-if="groupDemandsByEpic" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                  <span v-else class="inline-block h-4 w-4 shrink-0" />
                  Agrupar por épico
                </button>
                <button
                  v-if="groupDemandsByEpic"
                  type="button"
                  class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                  :class="visibleEpicQuarterKeys.length ? 'text-highlighted' : 'cursor-not-allowed opacity-40 text-muted'"
                  :disabled="!visibleEpicQuarterKeys.length"
                  @click="areAllEpicGroupsCollapsed ? expandAllEpicGroups() : collapseAllEpicGroups()"
                >
                  <UIcon name="i-lucide-chevrons-up-down" class="h-4 w-4 shrink-0 text-muted" />
                  {{ areAllEpicGroupsCollapsed ? 'Expandir épicos' : 'Recolher épicos' }}
                </button>
              </div>
            </template>
          </UPopover>

          <!-- Problemas -->
          <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
            <button class="flex items-center gap-1.5 rounded-lg border bg-background px-3 py-1.5 text-sm transition-colors hover:border-primary/40" :class="listProblemFilter.length ? 'border-primary/40 text-primary' : 'border-default'">
              <UIcon name="i-lucide-triangle-alert" class="w-3.5 h-3.5 shrink-0 text-muted" />
              <span class="text-highlighted">{{ listProblemFilterLabel }}</span>
              <UBadge v-if="listProblemFilter.length" size="xs" color="primary" variant="solid" class="shrink-0">{{ listProblemFilter.length }}</UBadge>
              <UIcon name="i-lucide-chevron-down" class="w-3.5 h-3.5 shrink-0 text-muted" />
            </button>
            <template #content>
              <div class="min-w-56 space-y-0.5 p-1">
                <button type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="listProblemFilter.length === 0 ? 'text-primary' : 'text-highlighted'" @click="clearListProblemFilter">
                  <UIcon v-if="listProblemFilter.length === 0" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                  <span v-else class="inline-block h-4 w-4 shrink-0" />
                  Sem filtro
                </button>
                <button type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="listProblemFilter.includes('__all__') ? 'text-primary' : 'text-highlighted'" @click="toggleListProblemFilter('__all__')">
                  <UIcon v-if="listProblemFilter.includes('__all__')" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                  <span v-else class="inline-block h-4 w-4 shrink-0" />
                  Todos os problemas
                </button>
                <button v-for="prob in listProblemOptions" :key="prob.value" type="button" class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated" :class="listProblemFilter.includes(prob.value) ? 'text-primary' : 'text-highlighted'" @click="toggleListProblemFilter(prob.value)">
                  <UIcon v-if="listProblemFilter.includes(prob.value)" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                  <span v-else class="inline-block h-4 w-4 shrink-0" />
                  {{ prob.label }}
                </button>
              </div>
            </template>
          </UPopover>
        </template>
      </div>
    </div>

    <div class="rounded-[20px] border border-default bg-default px-3 py-2 shadow-sm">
      <div class="flex flex-col gap-2 xl:flex-row xl:items-center xl:justify-between">
        <div class="flex flex-wrap items-center gap-1.5 text-[11px] text-muted">
          <span class="inline-flex items-center gap-1 rounded-full border border-default bg-elevated px-2.5 py-0.5">
            <UIcon name="i-lucide-folder-kanban" class="h-3.5 w-3.5 text-primary" />
            <span class="font-medium text-highlighted">{{ capacityProjectName }}</span>
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

          <!-- Wrapper com title: em navegadores como o Chrome o botão desabilitado não exibe
               tooltip; o span (com o botão sem pointer-events quando bloqueado) garante o hover. -->
          <span v-if="canEditRoadmap" :title="capacityDisabledReason || undefined" class="inline-flex">
            <UButton
              type="button"
              size="xs"
              color="neutral"
              variant="soft"
              icon="i-lucide-sliders-horizontal"
              label="Capacity"
              :disabled="!activeCapacityScope"
              :class="{ 'pointer-events-none': !activeCapacityScope }"
              @click="openCapacityModal"
            />
          </span>
        </div>
      </div>

      <div class="mt-3 overflow-visible rounded-xl border border-default bg-default shadow-sm">
        <div class="sticky top-14 z-20 overflow-hidden rounded-t-xl border-b border-default bg-elevated/95 shadow-sm backdrop-blur supports-[backdrop-filter]:bg-elevated/85 md:top-0">
          <table class="w-full table-fixed border-collapse" :style="{ width: listTableWidth, transform: `translateX(-${listHeaderScrollLeft}px)` }">
            <thead>
              <tr ref="listHeaderRowRef">
                <th
                  v-for="col in listOrderedCols"
                  :key="col.id"
                  class="relative border-b border-default bg-elevated/40 text-left align-top"
                  :style="{ width: listColWidth(col.id, col.defaultWidth) }"
                >
                  <div class="flex items-center gap-1 px-3 py-1 text-xs font-semibold text-muted">
                    <button
                      v-if="!col.disableSorting"
                      type="button"
                      class="flex min-w-0 flex-1 items-center gap-1 text-left transition-colors hover:text-highlighted"
                      @click="toggleListSort(col.id)"
                    >
                      <span class="truncate">{{ col.label }}</span>
                      <UIcon :name="getListSortIcon(col.id)" class="h-3.5 w-3.5 shrink-0" />
                    </button>
                    <span v-else class="truncate">{{ col.label }}</span>
                    <button
                      v-if="col.id !== '_actions'"
                      type="button"
                      class="list-col-drag inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-md text-muted transition-colors hover:bg-default hover:text-highlighted"
                      title="Reordenar coluna"
                    >
                      <UIcon name="i-lucide-grip-vertical" class="h-3.5 w-3.5" />
                    </button>
                  </div>
                  <span
                    v-if="col.id !== '_actions'"
                    class="absolute right-0 top-0 h-full w-[4px] cursor-col-resize select-none hover:bg-primary active:bg-primary"
                    @mousedown.prevent.stop="startListResize(col.id, $event)"
                  />
                </th>
              </tr>
              <tr>
                <th
                  v-for="col in listOrderedCols"
                  :key="`${col.id}-filter`"
                  class="border-b border-default bg-elevated/35 px-3 py-1 align-top"
                  :style="{ width: listColWidth(col.id, col.defaultWidth) }"
                >
                  <input
                    v-if="col.filterType === 'text'"
                    :value="getListColFilter(col.id)"
                    type="text"
                    :placeholder="col.label"
                    class="w-full rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted outline-none transition-colors placeholder:text-muted focus:border-primary/40"
                    @input="setListColFilter(col.id, ($event.target as HTMLInputElement).value)"
                  >

                  <!-- Coluna "Demanda": texto (título) + filtro de classificação (lista) -->
                  <div v-else-if="col.filterType === 'text-classification'" class="flex items-center gap-1">
                    <input
                      :value="getTitleFilter().text"
                      type="text"
                      :placeholder="col.label"
                      class="w-full min-w-0 flex-1 rounded-md border border-default bg-default px-2 py-1 text-xs text-highlighted outline-none transition-colors placeholder:text-muted focus:border-primary/40"
                      @input="setTitleTextFilter(($event.target as HTMLInputElement).value)"
                    >
                    <UPopover :content="{ side: 'bottom', align: 'end', sideOffset: 8 }">
                      <button
                        type="button"
                        class="inline-flex h-[26px] w-7 shrink-0 items-center justify-center rounded-md border transition-colors hover:border-primary/40"
                        :class="getTitleFilter().classifications.length ? 'border-primary/50 bg-primary/5 text-primary' : 'border-default bg-default text-muted'"
                        title="Filtrar por classificação"
                      >
                        <UIcon name="i-lucide-list-filter" class="h-3.5 w-3.5" />
                      </button>
                      <template #content>
                        <div class="min-w-48 space-y-1 p-1">
                          <p class="px-2.5 pb-0.5 pt-1 text-[10px] font-semibold uppercase tracking-wide text-muted">Classificação</p>
                          <button
                            type="button"
                            class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                            :class="getTitleFilter().classifications.length === 0 ? 'text-primary' : 'text-highlighted'"
                            @click="clearTitleClassifications"
                          >
                            <UIcon v-if="getTitleFilter().classifications.length === 0" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                            <span v-else class="inline-block h-4 w-4 shrink-0" />
                            Todas
                          </button>
                          <button
                            v-for="option in listClassificationFilterOptions"
                            :key="`classification-${option.value}`"
                            type="button"
                            class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                            :class="getTitleFilter().classifications.includes(option.value) ? 'text-primary' : 'text-highlighted'"
                            @click="toggleTitleClassification(option.value)"
                          >
                            <UIcon v-if="getTitleFilter().classifications.includes(option.value)" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                            <span v-else class="inline-block h-4 w-4 shrink-0" />
                            {{ option.label }}
                          </button>
                        </div>
                      </template>
                    </UPopover>
                  </div>

                  <!-- Coluna "Quarter / Tipo": um popover com as seções Quarter e Tipo -->
                  <UPopover v-else-if="col.filterType === 'quarter-type'" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                    <button class="flex w-full items-center gap-1.5 rounded-md border border-default bg-default px-2 py-1 text-xs transition-colors hover:border-primary/40">
                      <span class="flex-1 truncate text-left text-highlighted">{{ getQuarterTypeFilterLabel() }}</span>
                      <UIcon name="i-lucide-chevron-down" class="h-3.5 w-3.5 shrink-0 text-muted" />
                    </button>
                    <template #content>
                      <div class="min-w-48 space-y-1 p-1">
                        <p class="px-2.5 pb-0.5 pt-1 text-[10px] font-semibold uppercase tracking-wide text-muted">Quarter</p>
                        <button
                          type="button"
                          class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                          :class="getQuarterTypeFilter().quarters.length === 0 ? 'text-primary' : 'text-highlighted'"
                          @click="clearQuarterFilterPart"
                        >
                          <UIcon v-if="getQuarterTypeFilter().quarters.length === 0" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          Todos os quarters
                        </button>
                        <button
                          v-for="option in listQuarterFilterOptions"
                          :key="`quarter-${option.value}`"
                          type="button"
                          class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                          :class="getQuarterTypeFilter().quarters.includes(option.value) ? 'text-primary' : 'text-highlighted'"
                          @click="toggleQuarterFilterValue(option.value)"
                        >
                          <UIcon v-if="getQuarterTypeFilter().quarters.includes(option.value)" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          {{ option.label }}
                        </button>

                        <div class="my-1 border-t border-default" />

                        <p class="px-2.5 pb-0.5 pt-1 text-[10px] font-semibold uppercase tracking-wide text-muted">Tipo</p>
                        <button
                          type="button"
                          class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                          :class="getQuarterTypeFilter().types.length === 0 ? 'text-primary' : 'text-highlighted'"
                          @click="clearTypeFilterPart"
                        >
                          <UIcon v-if="getQuarterTypeFilter().types.length === 0" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          Todos os tipos
                        </button>
                        <button
                          v-for="option in listTypeFilterOptions"
                          :key="`type-${option.value}`"
                          type="button"
                          class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                          :class="getQuarterTypeFilter().types.includes(option.value) ? 'text-primary' : 'text-highlighted'"
                          @click="toggleTypeFilterValue(option.value)"
                        >
                          <UIcon v-if="getQuarterTypeFilter().types.includes(option.value)" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          {{ option.label }}
                        </button>
                      </div>
                    </template>
                  </UPopover>

                  <UPopover v-else-if="col.filterType === 'multi-select'" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                    <button class="flex w-full items-center gap-1.5 rounded-md border border-default bg-default px-2 py-1 text-xs transition-colors hover:border-primary/40">
                      <span class="flex-1 truncate text-left text-highlighted">{{ getListMultiFilterLabel(col) }}</span>
                      <UIcon name="i-lucide-chevron-down" class="h-3.5 w-3.5 shrink-0 text-muted" />
                    </button>
                    <template #content>
                      <div class="min-w-48 space-y-1 p-1">
                        <button
                          type="button"
                          class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                          :class="getListMultiFilter(col.id).length === 0 ? 'text-primary' : 'text-highlighted'"
                          @click="setListMultiFilter(col.id, [])"
                        >
                          <UIcon v-if="getListMultiFilter(col.id).length === 0" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          {{ col.allLabel ?? 'Todos' }}
                        </button>
                        <button
                          v-for="option in getListFilterOptions(col)"
                          :key="`${col.id}-${option.value}`"
                          type="button"
                          class="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm transition-colors hover:bg-elevated"
                          :class="getListMultiFilter(col.id).includes(option.value) ? 'text-primary' : 'text-highlighted'"
                          @click="toggleListMultiFilterValue(col.id, option.value)"
                        >
                          <UIcon v-if="getListMultiFilter(col.id).includes(option.value)" name="i-lucide-check" class="h-4 w-4 shrink-0" />
                          <span v-else class="inline-block h-4 w-4 shrink-0" />
                          {{ option.label }}
                        </button>
                      </div>
                    </template>
                  </UPopover>

                  <div v-else-if="listHasActiveFilters && col.id === '_actions'" class="flex justify-end">
                    <UButton
                      size="xs"
                      color="neutral"
                      variant="ghost"
                      icon="i-lucide-filter-x"
                      @click="clearListFilters"
                    >
                      Limpar
                    </UButton>
                  </div>

                  <span v-else class="block h-8" />
                </th>
              </tr>
            </thead>
          </table>
        </div>
        <div ref="listScrollContainerRef" class="overflow-x-auto overflow-y-visible" @scroll="syncListHeaderScroll">
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
              :meta="{ class: { tr: (row: any) => getPlanningDraftDisplayItem(row.original).status === 'Deprioritized' ? 'opacity-50' : '' } }"
              :ui="{ base: 'w-full table-fixed', thead: 'hidden', td: 'border-b border-default px-3 py-0.5 align-top overflow-hidden' }"
            >
              <template #status-cell="{ row }">
                <div
                  class="flex flex-col gap-1"
                  :title="getDemandNotesTooltip(getPlanningDraftDisplayItem(row.original)) || getDisplayedDemandStatus(getPlanningDraftDisplayItem(row.original)).label"
                >
                  <template v-if="!isCollapsedRepresentative(row.original)">
                    <USelect
                      v-if="isPlanningCellEditing(row.original, 'status')"
                      :model-value="getPlanningInlineDraft(row.original).status"
                      :items="getStatusOptionsForItem(row.original)"
                      value-key="value"
                      option-attribute="label"
                      size="xs"
                      class="w-full"
                      :disabled="isPlanningInlineSaving(row.original.id) || isSavingAllPlanningInlineEdits"
                      @blur="deactivatePlanningCell(row.original.id, 'status')"
                      @update:model-value="(value) => value && handlePlanningStatusChange(row.original, value as DemandStatus)"
                    />
                    <div v-else class="flex items-center gap-1.5">
                      <button
                        type="button"
                        class="inline-flex w-fit items-center rounded-md border px-1.5 py-0.5 text-[10px] font-medium transition-colors"
                        :class="[getStatusBadgeClass(getPlanningInlineDraft(row.original).status), getPlanningEditableCellButtonClass(row.original)]"
                        :disabled="isPlanningInlineSaving(row.original.id) || isSavingAllPlanningInlineEdits"
                        @click="activatePlanningCell(row.original, 'status')"
                      >
                        {{ statusLabels[getPlanningInlineDraft(row.original).status] }}
                      </button>
                    </div>
                  </template>
                  <span v-else class="text-xs text-muted">—</span>
                </div>
              </template>
              <template #products-cell="{ row }">
                <div v-if="isCollapsedRepresentative(row.original)" class="text-xs text-muted">—</div>
                <UPopover
                  v-else
                  :open="isPlanningCellEditing(row.original, 'products')"
                  :content="{ side: 'bottom', align: 'start', sideOffset: 8 }"
                  @update:open="(open) => handlePlanningPopoverOpenChange(row.original, 'products', open)"
                >
                  <button
                    v-if="getPlanningDraftProductDisplay(row.original).items.length"
                    type="button"
                    class="inline-flex max-w-full items-center gap-1 rounded-md border px-1 py-0.5 text-[10px] text-highlighted transition-colors"
                    :class="getPlanningEditableCellButtonClass(row.original)"
                    :title="getPlanningDraftProductDisplay(row.original).fullLabel"
                    :disabled="isPlanningInlineSaving(row.original.id) || isSavingAllPlanningInlineEdits"
                    @click="activatePlanningCell(row.original, 'products')"
                  >
                    <span class="max-w-[140px] truncate">{{ getPlanningDraftProductDisplay(row.original).previewLabel }}</span>
                    <span v-if="!getPlanningDraftProductDisplay(row.original).allVisible" class="shrink-0 text-muted">+{{ getPlanningDraftProductDisplay(row.original).hiddenCount }}</span>
                  </button>
                  <button
                    v-else
                    type="button"
                    class="inline-flex items-center rounded-md border px-1 py-0.5 text-xs transition-colors"
                    :class="getPlanningEditableCellButtonClass(row.original)"
                    :disabled="isPlanningInlineSaving(row.original.id) || isSavingAllPlanningInlineEdits"
                    @click="activatePlanningCell(row.original, 'products')"
                  >
                    —
                  </button>

                  <template #content>
                    <div class="flex min-w-44 flex-col gap-2 p-3">
                      <label v-for="product in getPlanningEditableProductOptions(row.original)" :key="product.value" class="flex items-center gap-2 text-[11px] text-highlighted">
                        <input type="checkbox" class="h-3.5 w-3.5 rounded border-default text-primary focus:ring-primary" :checked="getPlanningInlineDraft(row.original).productIds.includes(product.value)" @change="togglePlanningDraftProduct(row.original, product.value, ($event.target as HTMLInputElement).checked)">
                        <span class="truncate">{{ product.label }}</span>
                      </label>
                    </div>
                  </template>
                </UPopover>
              </template>
              <template #customers-cell="{ row }">
                <div v-if="isCollapsedRepresentative(row.original)" class="text-xs text-muted">—</div>
                <template v-else>
                  <!-- Não agrupado: edita clientes do épico correspondente -->
                  <template v-if="!groupDemandsByEpic && getEpicForDemand(row.original)">
                    <UPopover
                      :open="isPlanningCellEditing(getEpicForDemand(row.original)!, 'customers')"
                      :content="{ side: 'bottom', align: 'start', sideOffset: 8 }"
                      @update:open="(open) => handlePlanningPopoverOpenChange(getEpicForDemand(row.original)!, 'customers', open)"
                    >
                      <button
                        v-if="getPlanningDraftCustomerDisplay(getEpicForDemand(row.original)!).items.length"
                        type="button"
                        class="inline-flex max-w-full items-center gap-1 rounded-md border text-[10px] text-highlighted transition-colors"
                        :class="getPlanningEditableCellButtonClass(getEpicForDemand(row.original)!)"
                        :title="getPlanningDraftCustomerDisplay(getEpicForDemand(row.original)!).fullLabel"
                        :disabled="isPlanningInlineSaving(getEpicForDemand(row.original)!) || isSavingAllPlanningInlineEdits"
                        @click="activatePlanningCell(getEpicForDemand(row.original)!, 'customers')"
                      >
                        <span class="max-w-[140px] truncate">{{ getPlanningDraftCustomerDisplay(getEpicForDemand(row.original)!).previewLabel }}</span>
                        <span v-if="!getPlanningDraftCustomerDisplay(getEpicForDemand(row.original)!).allVisible" class="shrink-0 text-muted">+{{ getPlanningDraftCustomerDisplay(getEpicForDemand(row.original)!).hiddenCount }}</span>
                      </button>
                      <button
                        v-else
                        type="button"
                        class="text-xs transition-colors"
                        :class="getPlanningEditableCellButtonClass(getEpicForDemand(row.original)!)"
                        :disabled="isPlanningInlineSaving(getEpicForDemand(row.original)!) || isSavingAllPlanningInlineEdits"
                        @click="activatePlanningCell(getEpicForDemand(row.original)!, 'customers')"
                      >—</button>
                      <template #content>
                        <div class="w-[22rem] max-w-[min(22rem,calc(100vw-2rem))] space-y-2 p-3">
                          <div class="flex max-h-24 flex-wrap gap-1 overflow-y-auto">
                            <span
                              v-for="customer in getPlanningInlineDraft(getEpicForDemand(row.original)!).customers"
                              :key="`ne-${row.original.epicId}-${customer}`"
                              class="inline-flex items-center gap-1 rounded-full border border-primary/20 bg-primary/10 px-2 py-0.5 text-[10px] text-primary"
                            >
                              {{ customer }}
                              <button type="button" class="inline-flex h-3.5 w-3.5 items-center justify-center rounded-full hover:bg-primary/15" @click.prevent.stop="removePlanningCustomer(getEpicForDemand(row.original)!, customer)">
                                <UIcon name="i-lucide-x" class="h-3 w-3" />
                              </button>
                            </span>
                            <span v-if="!getPlanningInlineDraft(getEpicForDemand(row.original)!).customers.length" class="text-xs text-muted">Nenhum cliente associado.</span>
                          </div>
                          <div class="flex items-center gap-2">
                            <input
                              type="text"
                              :value="planningCustomerInputs[row.original.epicId!] ?? ''"
                              placeholder="Digite um novo cliente"
                              class="min-w-0 flex-1 rounded-md border border-default bg-default px-2 py-1.5 text-xs text-highlighted outline-none transition-colors focus:border-primary/40"
                              @click.stop
                              @input="planningCustomerInputs[row.original.epicId!] = ($event.target as HTMLInputElement).value"
                              @keydown.enter.prevent="addPlanningCustomer(getEpicForDemand(row.original)!, planningCustomerInputs[row.original.epicId!] ?? '')"
                              @keydown.esc.prevent="deactivatePlanningCell(row.original.epicId!, 'customers')"
                            >
                            <button
                              type="button"
                              class="inline-flex items-center rounded-md border border-primary/20 bg-primary/10 px-2 py-1.5 text-[11px] font-medium text-primary transition-colors hover:bg-primary/15 disabled:cursor-not-allowed disabled:opacity-50"
                              :disabled="!(planningCustomerInputs[row.original.epicId!] ?? '').trim()"
                              @click.prevent.stop="addPlanningCustomer(getEpicForDemand(row.original)!, planningCustomerInputs[row.original.epicId!] ?? '')"
                            >Adicionar</button>
                          </div>
                          <div v-if="getFilteredPlanningCustomerSuggestions(getEpicForDemand(row.original)!).length" class="max-h-32 overflow-y-auto rounded border border-default bg-elevated/40">
                            <button
                              v-for="customer in getFilteredPlanningCustomerSuggestions(getEpicForDemand(row.original)!)"
                              :key="customer"
                              type="button"
                              class="flex w-full px-2 py-1.5 text-left text-[11px] text-highlighted hover:bg-elevated"
                              @click="addPlanningCustomer(getEpicForDemand(row.original)!, customer)"
                            >{{ customer }}</button>
                          </div>
                        </div>
                      </template>
                    </UPopover>
                  </template>

                  <!-- Agrupado (demanda) ou sem épico: somente leitura -->
                  <template v-else>
                    <span
                      v-if="getPlanningCustomerCellDisplay(row.original).allVisible && getPlanningCustomerCellDisplay(row.original).items.length"
                      class="block max-w-full truncate text-[10px] text-muted opacity-60"
                      :title="getPlanningCustomerCellDisplay(row.original).fullLabel"
                    >{{ getPlanningCustomerCellDisplay(row.original).previewLabel }}</span>
                    <UPopover v-else-if="getPlanningCustomerCellDisplay(row.original).items.length" :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                      <button type="button" class="inline-flex max-w-full items-center gap-1 truncate bg-transparent p-0 text-[10px] text-muted opacity-60 cursor-default" :title="getPlanningCustomerCellDisplay(row.original).fullLabel">
                        <span class="max-w-[140px] truncate">{{ getPlanningCustomerCellDisplay(row.original).previewLabel }}</span>
                        <span class="shrink-0">+{{ getPlanningCustomerCellDisplay(row.original).hiddenCount }}</span>
                      </button>
                      <template #content>
                        <div class="flex max-w-xs flex-col gap-1 p-2">
                          <span v-for="customer in getPlanningCustomerCellDisplay(row.original).items" :key="`${row.original.id}-${customer}`" class="text-xs text-highlighted">{{ customer }}</span>
                        </div>
                      </template>
                    </UPopover>
                    <div v-else class="text-xs text-muted">—</div>
                  </template>
                </template>
              </template>
              <template #conclusion-cell="{ row }">
                <div class="flex flex-col gap-1">
                  <template v-if="!isCollapsedRepresentative(row.original)">
                    <UInput
                      v-if="isPlanningCellEditing(row.original, 'dueDate')"
                      :model-value="getPlanningInlineDraft(row.original).dueDate"
                      type="date"
                      size="xs"
                      class="w-full"
                      autofocus
                      :disabled="isPlanningInlineSaving(row.original.id) || isSavingAllPlanningInlineEdits"
                      @blur="deactivatePlanningCell(row.original.id, 'dueDate')"
                      @keydown.esc.prevent="deactivatePlanningCell(row.original.id, 'dueDate')"
                      @keydown.enter.prevent="deactivatePlanningCell(row.original.id, 'dueDate')"
                      @update:model-value="(value) => updatePlanningInlineDraft(row.original, { dueDate: String(value ?? '') })"
                    />
                    <button
                      v-else
                      type="button"
                      class="flex w-fit items-center gap-1 rounded-md border px-1 py-0.5 text-[11px] transition-colors"
                      :class="getPlanningEditableCellButtonClass(row.original)"
                      :disabled="isPlanningInlineSaving(row.original.id) || isSavingAllPlanningInlineEdits"
                      @click="activatePlanningCell(row.original, 'dueDate')"
                    >
                      <template v-if="getDisplayedConclusionDate(getPlanningDraftDisplayItem(row.original))">
                        <UIcon :name="getPlanningDraftDisplayItem(row.original).status === 'Done' && getPlanningDraftDisplayItem(row.original).deliveryDate ? 'i-lucide-calendar-check' : 'i-lucide-calendar-clock'" class="h-3 w-3" />
                        <span :class="getPlanningDraftDisplayItem(row.original).status === 'Done' && getPlanningDraftDisplayItem(row.original).deliveryDate ? 'text-green-600 dark:text-green-400' : 'text-muted'">{{ formatDemandDate(getDisplayedConclusionDate(getPlanningDraftDisplayItem(row.original))) }}</span>
                      </template>
                      <span v-else class="text-xs text-muted">—</span>
                    </button>
                  <div
                    v-if="showDemandDelayMarker(getPlanningDraftDisplayItem(row.original))"
                    class="flex items-center gap-1 text-[11px] font-medium text-amber-600 dark:text-amber-400"
                  >
                    <UIcon name="i-lucide-triangle-alert" class="h-3 w-3" />
                    <span>Atrasado</span>
                  </div>
                  </template>
                  <span v-else class="text-xs text-muted">—</span>
                </div>
              </template>
              <template #hours-cell="{ row }">
                <div v-if="isCollapsedRepresentative(row.original)" class="text-xs text-muted">—</div>
                <div v-else-if="isPlanningCellEditing(row.original, 'hours')" class="ml-auto w-full max-w-[84px]">
                  <UInput
                    :model-value="getPlanningInlineDraft(row.original).hoursInput"
                    type="text"
                    inputmode="decimal"
                    size="xs"
                    autofocus
                    class="w-full text-right"
                    :disabled="isPlanningInlineSaving(row.original.id) || isSavingAllPlanningInlineEdits"
                    @blur="deactivatePlanningCell(row.original.id, 'hours')"
                    @keydown.esc.prevent="deactivatePlanningCell(row.original.id, 'hours')"
                    @keydown.enter.prevent="deactivatePlanningCell(row.original.id, 'hours')"
                    @update:model-value="(value) => updatePlanningInlineDraft(row.original, { hoursInput: String(value ?? '') })"
                  />
                </div>
                <div v-else class="flex flex-col items-end gap-0.5">
                  <button
                    type="button"
                    class="rounded-md border text-[10px] font-semibold transition-colors"
                    :class="[
                      row.original.status === 'Deprioritized' ? 'line-through text-muted' : row.original.excludeFromCapacity ? 'line-through text-muted opacity-50' : (getPlanningInlineDraft(row.original).hoursRed ? 'text-red-500' : 'text-highlighted'),
                      isPlanningInlineDirty(row.original) ? 'border-primary/40 ring-1 ring-primary/10 hover:border-primary/60 hover:bg-primary/5' : 'border-transparent hover:border-primary/30 hover:bg-elevated'
                    ]"
                    :disabled="isPlanningInlineSaving(row.original.id) || isSavingAllPlanningInlineEdits"
                    @click="activatePlanningCell(row.original, 'hours')"
                  >
                    {{ getPlanningInlineDraft(row.original).hoursInput ? `${getPlanningInlineDraft(row.original).hoursInput}h` : '—' }}
                  </button>
                  <div class="flex items-center gap-1">
                    <button
                      type="button"
                      class="inline-flex h-2.5 w-2.5 shrink-0 items-center justify-center rounded-full transition-colors"
                      :class="getPlanningInlineDraft(row.original).hoursRed ? 'bg-red-500 hover:bg-red-600' : 'border border-muted bg-transparent hover:border-red-400'"
                      title="Destacar horas em vermelho"
                      :disabled="isPlanningInlineSaving(row.original.id) || isSavingAllPlanningInlineEdits"
                      @click.stop="updatePlanningInlineDraft(row.original, { hoursRed: !getPlanningInlineDraft(row.original).hoursRed })"
                    />
                    <button
                      type="button"
                      class="inline-flex h-3.5 w-3.5 items-center justify-center rounded text-muted/40 transition-colors hover:text-muted"
                      :class="row.original.excludeFromCapacity ? '!text-amber-500 hover:!text-amber-600' : ''"
                      :title="row.original.excludeFromCapacity ? 'Excluído do capacity. Clique para incluir.' : 'Clique para excluir do capacity.'"
                      :disabled="isPlanningInlineSaving(row.original.id) || isSavingAllPlanningInlineEdits"
                      @click.stop="toggleExcludeFromCapacity(row.original)"
                    >
                      <UIcon :name="row.original.excludeFromCapacity ? 'i-lucide-eye-off' : 'i-lucide-eye'" class="h-2.5 w-2.5" />
                    </button>
                  </div>
                </div>
              </template>
            </UTable>
          </div>
        </div>
      </div>

        <div v-if="planningPendingEditCount" class="pointer-events-none fixed inset-x-0 bottom-4 z-40 flex justify-center px-4">
          <div class="pointer-events-auto flex w-full max-w-3xl items-center justify-between gap-3 rounded-2xl border border-primary/20 bg-default/95 px-4 py-3 shadow-2xl backdrop-blur">
            <div>
              <p class="text-sm font-semibold text-highlighted">Alterações pendentes no planejamento</p>
              <p class="text-xs text-muted">{{ planningPendingEditCount.toLocaleString('pt-BR') }} alteração(ões) aguardando confirmação.</p>
            </div>
            <div class="flex items-center gap-2">
              <UButton
                color="neutral"
                variant="outline"
                :disabled="isSavingAllPlanningInlineEdits"
                @click="discardAllPlanningInlineEdits"
              >
                Descartar
              </UButton>
              <UButton
                color="primary"
                :loading="isSavingAllPlanningInlineEdits"
                @click="saveAllPlanningInlineEdits"
              >
                {{ planningPendingEditLabel }}
              </UButton>
            </div>
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
        <div class="flex items-start justify-between gap-3">
          <div>
            <p class="text-sm font-semibold text-highlighted">Pontos de atenção do roadmap</p>
            <p class="text-xs text-muted">Clique em qualquer item para filtrar a lista acima (clique de novo para remover).</p>
          </div>
          <div class="flex shrink-0 items-center gap-2">
            <UButton
              size="xs"
              color="primary"
              variant="soft"
              icon="i-lucide-layout-dashboard"
              label="Dashboard completo"
              trailing-icon="i-lucide-external-link"
              title="Abrir o dashboard completo em nova aba, com os filtros de Time e Quarter atuais"
              @click="openFullDashboard"
            />
            <UButton
              v-if="hasActiveListFilters"
              size="xs"
              color="neutral"
              variant="soft"
              icon="i-lucide-filter-x"
              label="Limpar filtros"
              @click="clearAllListFilters"
            />
          </div>
        </div>

        <RoadmapDashboards
          :demands="quarterFilteredDemands"
          :all-demands="demands"
          :active-filters="dashboardActiveFilters"
          only-counters
          @select="handleDashboardSelect"
        />
      </section>

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
      <RoadmapHierarchyPage v-model:project-ids="filterListProjectIds" />
    </template>

    <!-- Floating bulk-actions bar: overlays the bottom of the viewport so selecting items
         never pushes the grid down. -->
    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="translate-y-3 opacity-0"
      enter-to-class="translate-y-0 opacity-100"
      leave-active-class="transition duration-100 ease-in"
      leave-from-class="translate-y-0 opacity-100"
      leave-to-class="translate-y-3 opacity-0"
    >
      <div
        v-if="canEditRoadmap && viewMode === 'list' && (selectedPlanningItemCount || movablePlanningItems.length)"
        class="pointer-events-none fixed inset-x-0 z-50 flex justify-center px-4"
        :class="planningPendingEditCount ? 'bottom-28' : 'bottom-6'"
      >
        <div class="pointer-events-auto flex flex-wrap items-center gap-2 rounded-full border border-default bg-default/95 px-3 py-2 shadow-lg ring-1 ring-black/5 backdrop-blur supports-[backdrop-filter]:bg-default/85">
          <span v-if="selectedPlanningItemCount" class="px-2 text-sm font-medium text-highlighted">
            {{ selectedPlanningItemCount.toLocaleString('pt-BR') }} {{ selectedPlanningItemCount === 1 ? 'selecionado' : 'selecionados' }}
          </span>

          <UPopover v-if="movablePlanningItems.length">
            <UButton
              size="xs"
              color="primary"
              variant="soft"
              trailing-icon="i-lucide-chevron-down"
              leading-icon="i-lucide-calendar-range"
              :loading="isBulkPlanning"
            >
              Mover {{ movablePlanningItems.length.toLocaleString('pt-BR') }} {{ movablePlanningItems.length === 1 ? 'item' : 'itens' }}
            </UButton>
            <template #content>
              <div class="max-h-72 w-64 overflow-y-auto py-1">
                <button
                  v-for="option in bulkMoveQuarterOptions"
                  :key="option.value"
                  class="w-full truncate px-3 py-2 text-left text-sm text-highlighted transition-colors hover:bg-elevated"
                  @click="planSelectedDemandsToQuarter(option.value)"
                >
                  {{ option.label }}
                </button>
              </div>
            </template>
          </UPopover>

          <UButton
            v-if="selectedPlanningItemCount"
            size="xs"
            color="neutral"
            variant="soft"
            icon="i-lucide-pencil-ruler"
            :loading="isBulkPlanning"
            @click="planningBulkEditModalOpen = true"
          >
            Editar {{ selectedPlanningItemCount.toLocaleString('pt-BR') }} itens
          </UButton>

          <UButton
            v-if="selectedPlanningItemCount"
            size="xs"
            color="neutral"
            variant="ghost"
            leading-icon="i-lucide-square-minus"
            @click="clearSelectedDemands"
          >
            Desmarcar todos
          </UButton>
        </div>
      </div>
    </Transition>

    <BulkEditRoadmapItemsModal
      v-model:open="planningBulkEditModalOpen"
      :selected-items="selectedPlanningItems"
      :dependency-options="dependencyOptions"
      :is-saving="isBulkPlanning"
      @submit="handlePlanningBulkEdit"
    />

    <UModal v-model:open="planningStatusModalOpen" :title="planningStatusModalDraft ? `Completar status: ${statusLabels[planningStatusModalDraft.status]}` : 'Completar status'" :ui="{ content: 'sm:max-w-2xl' }" @update:open="(open) => { if (!open) closePlanningStatusModal({ restoreSnapshot: true }) }">
      <template #body>
        <div v-if="planningStatusModalDraft" class="space-y-4">
          <p class="text-sm text-muted">Alguns status exigem informações adicionais antes de salvar a edição.</p>

          <UFormField v-if="planningStatusModalRequiresDeliveryDate" label="Data de entrega" required>
            <UInput
              :model-value="planningStatusModalDraft.dueDate"
              type="date"
              class="w-full"
              @update:model-value="(value) => planningStatusModalItem && updatePlanningInlineDraft(planningStatusModalItem, { dueDate: String(value ?? '') })"
            />
          </UFormField>

          <UFormField v-if="planningStatusModalRequiresBlockedReason" label="Motivo do impedimento" required>
            <UTextarea
              :model-value="planningStatusModalDraft.blockedReason"
              :rows="4"
              class="w-full"
              @update:model-value="(value) => planningStatusModalItem && updatePlanningInlineDraft(planningStatusModalItem, { blockedReason: String(value ?? '') })"
            />
          </UFormField>

          <template v-if="planningStatusModalRequiresDeprioritization">
            <UFormField label="Motivo da despriorização" required>
              <USelect
                :model-value="planningStatusModalDraft.deprioritizationReason"
                :items="deprioritizationReasonOptions"
                value-key="value"
                option-attribute="label"
                class="w-full"
                @update:model-value="(value) => planningStatusModalItem && updatePlanningInlineDraft(planningStatusModalItem, { deprioritizationReason: value as DeprioritizationReason | undefined })"
              />
            </UFormField>

            <UFormField
              label="Demanda priorizada no lugar"
              :required="planningStatusModalDraft.deprioritizationReason === 'ReplacedByOtherInitiative' || planningStatusModalDraft.deprioritizationReason === 'HigherValuePrioritization'"
            >
              <USelect
                :model-value="planningStatusModalDraft.replacementDemandId"
                :items="planningStatusReplacementDemandOptions"
                value-key="value"
                option-attribute="label"
                :placeholder="(planningStatusModalDraft.deprioritizationReason === 'ReplacedByOtherInitiative' || planningStatusModalDraft.deprioritizationReason === 'HigherValuePrioritization') ? 'Selecione uma demanda' : 'Opcional'"
                class="w-full"
                @update:model-value="(value) => planningStatusModalItem && updatePlanningInlineDraft(planningStatusModalItem, { replacementDemandId: value ? String(value) : undefined })"
              />
            </UFormField>

            <UFormField label="Observação despriorização" required>
              <UTextarea
                :model-value="planningStatusModalDraft.observation"
                :rows="4"
                class="w-full"
                @update:model-value="(value) => planningStatusModalItem && updatePlanningInlineDraft(planningStatusModalItem, { observation: String(value ?? '') })"
              />
            </UFormField>
          </template>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton color="neutral" variant="outline" @click="closePlanningStatusModal({ restoreSnapshot: true })">
            Cancelar
          </UButton>
          <UButton color="primary" @click="confirmPlanningStatusModal">
            Confirmar
          </UButton>
        </div>
      </template>
    </UModal>

    <!-- Create / Edit modal -->
    <RoadmapDemandFormModal
      v-model:open="modalOpen"
      :projects="sortedProjects"
      :dependency-options="dependencyOptions"
      :customer-suggestions="customerSuggestions"
      :demand="editingDemand"
      :copy-source="copySource"
      :default-item-type="createItemType"
      :default-parent-demand-id="defaultParentDemandId"
      :default-project-id="defaultProjectId ?? (filterListProjectIds.length === 1 ? filterListProjectIds[0] : selectedProjectId) ?? undefined"
      :default-project-ids="defaultProjectIds"
      :roadmap-options="roadmapParentOptions"
      :epic-options="epicParentOptions"
      :default-quarter-year="createDefaultQuarterYear ?? selectedDemandScope?.quarterYear ?? activeCapacityScope?.quarterYear ?? undefined"
      :default-quarter-number="createDefaultQuarterNumber ?? selectedDemandScope?.quarterNumber ?? activeCapacityScope?.quarterNumber ?? undefined"
      :default-type="createDefaultType"
      :default-hours="createDefaultHours"
      :default-product-ids="createDefaultProductIds"
      :force-simple-epic="forceSimpleEpic"
      :is-saving="isSavingDemand"
      :focus-field="modalEditFocusField"
      @trade-off-deleted="handleTradeOffDeleted"
      @submit="handleSubmit"
      @create-spillover="handleFormSpillover"
    />

    <RoadmapCapacityModal
      v-model:open="capacityModalOpen"
      :project-name="capacityProjectName"
      :quarter-label="activeCapacityScope?.quarterLabel"
      :initial-value="capacityModalInitialValue"
      :is-saving="isSavingCapacity"
      @submit="handleCapacitySubmit"
    />

    <!-- Confirm convert simple epic to composite -->
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

    <!-- Confirm delete modal -->
    <UModal
      v-model:open="confirmDeleteOpen"
      title="Remover Demanda"
      :description="deleteDependencyLinks.length ? undefined : 'Tem certeza que deseja remover esta demanda? Esta ação não pode ser desfeita.'"
    >
      <template v-if="deleteDependencyLinks.length" #body>
        <div class="space-y-3">
          <p class="text-sm text-muted">
            Tem certeza que deseja remover esta demanda? Esta ação não pode ser desfeita.
          </p>
          <div class="rounded-lg border border-amber-200/70 bg-amber-50/60 p-3 dark:border-amber-800/50 dark:bg-amber-900/15">
            <p class="text-sm font-medium text-amber-800 dark:text-amber-300">
              Este item possui {{ deleteDependencyLinks.length === 1 ? 'um vínculo' : `${deleteDependencyLinks.length} vínculos` }} de dependência que {{ deleteDependencyLinks.length === 1 ? 'será removido' : 'serão removidos' }} ao excluir:
            </p>
            <ul class="mt-2 space-y-1">
              <li
                v-for="link in deleteDependencyLinks"
                :key="link.demandId"
                class="flex items-center gap-1.5 text-xs text-amber-800/90 dark:text-amber-300/90"
              >
                <UIcon :name="link.relation === 'dependsOn' ? 'i-lucide-lock' : 'i-lucide-lock-open'" class="h-3 w-3 shrink-0" />
                <span>
                  {{ link.relation === 'dependsOn' ? 'Depende de' : 'Bloqueia' }}:
                  <strong>{{ link.itemType === 'Epic' ? 'Épico' : 'Demanda' }} {{ link.title }}</strong>
                  <template v-if="link.projectName"> · {{ link.projectName }}</template>
                </span>
              </li>
            </ul>
          </div>
        </div>
      </template>

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

    <UModal
      v-model:open="spilloverModalOpen"
      :title="spilloverRestoreMode ? 'Restaurar Transbordo' : (spilloverModalDemand?.status === 'Deprioritized' ? 'Repriorizar em outro quarter' : 'Criar Transbordo')"
      :ui="{ content: 'sm:max-w-md' }"
      @update:open="(open) => { if (!open) closeSpilloverModal() }"
    >
      <template #body>
        <div class="space-y-4">
          <p class="text-sm text-muted">
            <template v-if="spilloverRestoreMode">
              {{ spilloverModalDemand?.itemType === 'Epic' ? 'O épico' : 'A demanda' }} <strong class="text-highlighted">{{ spilloverModalDemand?.title }}</strong> voltará ao status <em>Transbordo</em>.
              Informe o novo motivo e observação. A cópia já existente no quarter de destino é mantida.
            </template>
            <template v-else-if="spilloverModalDemand?.status === 'Deprioritized'">
              {{ spilloverModalDemand?.itemType === 'Epic' ? 'O épico' : 'A demanda' }} <strong class="text-highlighted">{{ spilloverModalDemand?.title }}</strong> será repriorizado
              com uma cópia do tipo <em>Spillover</em> no quarter de destino. O registro original permanece preservado.
            </template>
            <template v-else>
              O histórico {{ spilloverModalDemand?.itemType === 'Epic' ? 'do épico' : 'da demanda' }} <strong class="text-highlighted">{{ spilloverModalDemand?.title }}</strong> será preservado
              no quarter atual e uma cópia do tipo <em>Spillover</em> será criada no quarter de destino.
            </template>
          </p>
          <div v-if="!spilloverRestoreMode" class="grid grid-cols-2 gap-3">
            <UFormField label="Ano">
              <UInput
                v-model="spilloverTargetYear"
                type="number"
                :min="2020"
                :max="2040"
                placeholder="Ano"
              />
            </UFormField>
            <UFormField label="Quarter">
              <USelect
                v-model="spilloverTargetNumber"
                :items="[{ label: 'Q1', value: 1 }, { label: 'Q2', value: 2 }, { label: 'Q3', value: 3 }, { label: 'Q4', value: 4 }]"
                value-key="value"
                label-key="label"
              />
            </UFormField>
          </div>
          <UFormField label="Motivo do transbordo" required>
            <USelect
              v-model="spilloverReason"
              :items="[
                { label: 'Mudança de escopo', value: 'ScopeChange' },
                { label: 'Mudança de prioridade (sem trade-off)', value: 'PriorityChangeNoTradeOff' },
                { label: 'Dependência externa', value: 'ExternalDependency' },
                { label: 'Impedimento técnico', value: 'TechnicalBlock' },
                { label: 'Estimativa incorreta', value: 'IncorrectEstimate' },
                { label: 'Capacidade insuficiente', value: 'InsufficientCapacity' },
                { label: 'Problemas de qualidade', value: 'QualityIssues' }
              ]"
              value-key="value"
              option-attribute="label"
              placeholder="Selecione o motivo"
              class="w-full"
            />
          </UFormField>
          <UFormField label="Observação" required>
            <UTextarea
              v-model="spilloverObservation"
              :rows="3"
              placeholder="Descreva o motivo do transbordo"
              class="w-full"
            />
          </UFormField>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            variant="outline"
            color="neutral"
            label="Cancelar"
            :disabled="isCreatingSpillover"
            @click="closeSpilloverModal"
          />
          <UButton
            color="primary"
            icon="i-lucide-forward"
            :label="spilloverRestoreMode ? 'Restaurar' : (spilloverModalDemand?.status === 'Deprioritized' ? 'Repriorizar' : 'Criar Transbordo')"
            :loading="isCreatingSpillover"
            @click="confirmCreateSpillover"
          />
        </div>
      </template>
    </UModal>
    </template>
  </div>
</template>
