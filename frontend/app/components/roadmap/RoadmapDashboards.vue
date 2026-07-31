<script setup lang="ts">
import type { RoadmapDemand, DemandStatus, DemandType, DemandClassification, DemandDependency, RoadmapCapacitySummary } from '~/types/roadmap'
import type { DashboardSelection, DashboardActiveFilters } from '~/types/roadmapDashboards'
import type { ApiResponse } from '~/types/api'
import { isSpecialBacklogQuarter, formatQuarterLabel } from '~/utils/roadmapQuarter'

const props = defineProps<{
  // Demandas já filtradas por time/quarter (equivale ao quarterFilteredDemands da planejamento).
  demands: RoadmapDemand[]
  // Todas as demandas carregadas — necessário para resolver épicos/dependências por id.
  allDemands: RoadmapDemand[]
  activeFilters?: DashboardActiveFilters
  // Quando true, renderiza só os 5 totalizadores do topo (usado na planejamento do roadmap).
  onlyCounters?: boolean
}>()

const emit = defineEmits<{
  select: [selection: DashboardSelection]
  report: [tipo: 'atraso-transbordo' | 'deprioritization']
}>()

function emitSelect(selection: DashboardSelection) {
  emit('select', selection)
}

const itemsById = computed(() => new Map(props.allDemands.map(item => [item.id, item] as const)))
const demandItems = computed(() => props.allDemands.filter(item => item.itemType === 'Demand'))

// ─── Rótulos e estilos ─────────────────────────────────────────────────────────
const statusLabels: Record<DemandStatus, string> = {
  Backlog: 'Backlog', InProgress: 'Doing', Done: 'Concluído', Deprioritized: 'Despriorizado', Blocked: 'Impedido', Spillover: 'Transbordo', UX: 'UX', Prioritized: 'Priorizado'
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
const classificationLabels: Record<DemandClassification, string> = {
  TechnicalDebtSecurity: 'Débito Técnico', Strategic: 'Estratégico', Evolution: 'Evolução',
  ImprovementGap: 'Melhoria/Gap', Mandatory: 'Mandatório', Homologation: 'Homologação', Customizacao: 'Customização'
}

function getDeprioritizationReasonLabel(value: string | undefined) {
  switch (value) {
    case 'Strategic': return 'Estratégico'
    case 'MandatoryUrgent': return 'Mandatório/Urgente'
    case 'LowImpact': return 'Baixo impacto'
    case 'LackOfCapacity': return 'Falta de capacidade'
    case 'ContextChange': return 'Mudança de contexto'
    case 'Customizacao': return 'Customização'
    case 'StrategyChange': return 'Mudança de estratégia'
    case 'HigherValuePrioritization': return 'Priorização de maior valor'
    case 'LowCustomerDemand': return 'Baixa demanda de clientes'
    case 'LowExpectedReturn': return 'Baixo retorno esperado'
    case 'BusinessDefinitionDependency': return 'Dependência de definição de negócio'
    case 'AlternativeSolutionAvailable': return 'Solução alternativa disponível'
    case 'RegulatoryRequirementChanged': return 'Requisito regulatório alterado'
    case 'CustomerWithdrew': return 'Cliente desistiu'
    case 'ReplacedByOtherInitiative': return 'Substituída por outra iniciativa'
    case 'UndefinedScope': return 'Escopo indefinido'
    default: return ''
  }
}

function getSpilloverReasonLabel(value: string | undefined) {
  switch (value) {
    case 'ScopeChange': return 'Mudança de escopo'
    case 'PriorityChangeNoTradeOff': return 'Mudança de prioridade (sem trade-off)'
    case 'ExternalDependency': return 'Dependência externa'
    case 'TechnicalBlock': return 'Impedimento técnico'
    case 'IncorrectEstimate': return 'Estimativa incorreta'
    case 'InsufficientCapacity': return 'Capacidade insuficiente'
    case 'QualityIssues': return 'Problemas de qualidade'
    default: return ''
  }
}

// ─── Helpers (espelham a planejamento) ──────────────────────────────────────────
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

function getDisplayIssueLinks(demand: Pick<RoadmapDemand, 'issueLinks' | 'jiraIssue'>) {
  if (demand.issueLinks?.length)
    return demand.issueLinks
  if (demand.jiraIssue?.trim())
    return [{ key: demand.jiraIssue.trim() }]
  return []
}

function getEffectiveProjectId(item: Pick<RoadmapDemand, 'projectId' | 'projectIds' | 'isSimple'>) {
  return item.projectId ?? (item.isSimple ? item.projectIds?.[0] : undefined) ?? null
}

function compareQuarterPosition(left: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber'>, right: Pick<DemandDependency, 'quarterYear' | 'quarterNumber'>) {
  if (left.quarterYear !== right.quarterYear)
    return left.quarterYear - right.quarterYear
  return left.quarterNumber - right.quarterNumber
}

type DependencyQuarter = { quarterYear: number, quarterNumber: number } | 'unplanned'

function effectiveDependencyQuarter(itemId: string, fallback: { quarterYear: number, quarterNumber: number }): DependencyQuarter {
  const item = itemsById.value.get(itemId)
  if (!item)
    return fallback
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

function isOrderInconsistent(dependent: { id: string, quarterYear: number, quarterNumber: number }, dependency: DemandDependency) {
  const dependentQuarter = effectiveDependencyQuarter(dependent.id, { quarterYear: dependent.quarterYear, quarterNumber: dependent.quarterNumber })
  if (dependentQuarter === 'unplanned' || isSpecialBacklogQuarter(dependentQuarter.quarterYear, dependentQuarter.quarterNumber))
    return false
  const dependencyQuarter = effectiveDependencyQuarter(dependency.demandId, { quarterYear: dependency.quarterYear, quarterNumber: dependency.quarterNumber })
  if (dependencyQuarter === 'unplanned' || isSpecialBacklogQuarter(dependencyQuarter.quarterYear, dependencyQuarter.quarterNumber))
    return true
  return compareQuarterPosition(dependentQuarter, dependencyQuarter) < 0
}

function isDependencyInconsistent(demand: RoadmapDemand, dependency: DemandDependency) {
  return isOrderInconsistent(demand, dependency)
}

function isReverseDependencyInconsistent(demand: RoadmapDemand, dep: DemandDependency) {
  return isOrderInconsistent(
    { id: dep.demandId, quarterYear: dep.quarterYear, quarterNumber: dep.quarterNumber },
    { ...dep, demandId: demand.id, quarterYear: demand.quarterYear, quarterNumber: demand.quarterNumber }
  )
}

function hasInconsistentDependency(demand: RoadmapDemand) {
  return demand.dependsOn.some(dependency => isDependencyInconsistent(demand, dependency))
    || (demand.dependedOnBy ?? []).some(dep => isReverseDependencyInconsistent(demand, dep))
}

function getDemandProblemKeys(demand: RoadmapDemand): string[] {
  if (demand.itemType === 'Epic') {
    const epicKeys: string[] = []
    if (demand.hasNoKpi || demand.kpiLinks.length === 0)
      epicKeys.push('noKpi')
    if (!getDisplayIssueLinks(demand).length)
      epicKeys.push('noJira')
    if (demand.isSimple) {
      if (demand.hours == null)
        epicKeys.push('noHours')
      if (demand.status !== 'Done' && demand.isOverdue)
        epicKeys.push('overdueOpen')
      if (demand.status === 'Done' && demand.isDeliveredLate)
        epicKeys.push('deliveredLate')
    }
    if (demand.status === 'Done' && !demand.hasNoKpi && demand.kpiLinks.length > 0 && (demand.kpiMeasurements?.length ?? 0) === 0)
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

// ─── Totais ──────────────────────────────────────────────────────────────────
const scopedTotalHours = computed(() =>
  props.demands.reduce((total, demand) => total + (demand.hours ?? 0), 0)
)

const statusTotals = computed(() => {
  const totalHours = scopedTotalHours.value
  const totals = new Map<DemandStatus, { status: DemandStatus, label: string, hours: number, count: number }>()
  for (const item of props.demands) {
    const current = totals.get(item.status) ?? { status: item.status, label: statusLabels[item.status], hours: 0, count: 0 }
    current.hours += item.hours ?? 0
    current.count += 1
    totals.set(item.status, current)
  }
  return [...totals.values()]
    .map(item => ({ ...item, percentage: totalHours > 0 ? (item.hours / totalHours) * 100 : 0 }))
    .sort((left, right) => right.hours - left.hours)
})

const classificationTotals = computed(() => {
  const totalHours = scopedTotalHours.value
  const totals = new Map<DemandClassification, { classification: DemandClassification, label: string, hours: number, count: number }>()
  for (const demand of props.demands) {
    const classification = String(getEffectiveDemandClassification(demand) ?? '').trim() as DemandClassification
    if (!classification || !(classification in classificationLabels)) continue
    const current = totals.get(classification) ?? { classification, label: classificationLabels[classification], hours: 0, count: 0 }
    current.hours += demand.hours ?? 0
    current.count += 1
    totals.set(classification, current)
  }
  return [...totals.values()]
    .map(item => ({ ...item, percentage: totalHours > 0 ? (item.hours / totalHours) * 100 : 0 }))
    .sort((left, right) => right.hours - left.hours)
})

const customerTotals = computed(() => {
  const totalHours = scopedTotalHours.value
  const totals = new Map<string, { name: string, hours: number, count: number }>()
  for (const demand of props.demands) {
    for (const customer of getEffectiveDemandCustomers(demand)) {
      const current = totals.get(customer) ?? { name: customer, hours: 0, count: 0 }
      current.hours += demand.hours ?? 0
      current.count += 1
      totals.set(customer, current)
    }
  }
  return [...totals.values()]
    .map(item => ({ ...item, percentage: totalHours > 0 ? (item.hours / totalHours) * 100 : 0 }))
    .sort((left, right) => right.hours - left.hours)
})

const typeTotals = computed(() => {
  const totalHours = scopedTotalHours.value
  return (['Planned', 'Spillover', 'Unplanned', 'Additional'] as DemandType[]).map((type) => {
    const scopedDemands = props.demands.filter(demand => demand.type === type)
    const hours = scopedDemands.reduce((total, demand) => total + (demand.hours ?? 0), 0)
    return { type, label: typeLabels[type], hours, count: scopedDemands.length, percentage: totalHours > 0 ? (hours / totalHours) * 100 : 0 }
  })
    .filter(item => item.count > 0)
    .sort((left, right) => right.hours - left.hours)
})

const delayedTotals = computed(() => {
  let overdueOpen = 0
  let deliveredLate = 0
  for (const item of props.demands) {
    const keys = getDemandProblemKeys(item)
    if (keys.includes('overdueOpen')) overdueOpen += 1
    if (keys.includes('deliveredLate')) deliveredLate += 1
  }
  return { overdueOpen, deliveredLate }
})

const inconsistentDependencyCount = computed(() => props.demands.filter(hasInconsistentDependency).length)
const missingIssueCount = computed(() => props.demands.filter(item => getDemandProblemKeys(item).includes('noJira')).length)
const noKpiCount = computed(() => props.demands.filter(item => getDemandProblemKeys(item).includes('noKpi')).length)
const doneNoKpiCount = computed(() => props.demands.filter(item => getDemandProblemKeys(item).includes('doneNoKpi')).length)

function buildReasonTotals(items: RoadmapDemand[], getReason: (item: RoadmapDemand) => string | undefined, getLabel: (value: string | undefined) => string) {
  const withReason = items.filter(item => !!getReason(item))
  const totalCount = withReason.length
  const totals = new Map<string, { reason: string, label: string, hours: number, count: number }>()
  for (const item of withReason) {
    const reason = getReason(item)!
    const current = totals.get(reason) ?? { reason, label: getLabel(reason), hours: 0, count: 0 }
    current.hours += item.hours ?? 0
    current.count += 1
    totals.set(reason, current)
  }
  return {
    totalCount,
    items: [...totals.values()]
      .map(item => ({ ...item, percentage: totalCount > 0 ? (item.count / totalCount) * 100 : 0 }))
      .sort((left, right) => right.count - left.count)
  }
}

const deprioritizationReasonTotals = computed(() =>
  buildReasonTotals(props.demands.filter(item => item.status === 'Deprioritized'), item => item.deprioritizationReason ?? undefined, getDeprioritizationReasonLabel)
)
// Atraso + Transbordo unificados (mesmo enum de motivo): transbordo = itens em Spillover;
// atraso = concluídos entregues fora do prazo.
const atrasoTransbordoTotals = computed(() =>
  buildReasonTotals(
    props.demands.filter(item => (item.status === 'Spillover' && item.spilloverReason) || (item.status === 'Done' && item.delayReason)),
    item => item.status === 'Spillover' ? (item.spilloverReason ?? undefined) : (item.delayReason ?? undefined),
    getSpilloverReasonLabel
  )
)

// ─── Status: donut ───────────────────────────────────────────────────────────
const statusHexColor: Record<DemandStatus, string> = {
  Backlog: '#a3a3a3', InProgress: '#3b82f6', Done: '#22c55e', Deprioritized: '#ec4899',
  Blocked: '#ef4444', Spillover: '#f97316', UX: '#a855f7', Prioritized: '#06b6d4'
}
const DONUT_RADIUS = 52
const DONUT_CIRCUMFERENCE = 2 * Math.PI * DONUT_RADIUS

// Segmentos proporcionais (só status com horas > 0).
const donutSegments = computed(() => {
  const total = scopedTotalHours.value
  if (total <= 0) return []
  let acc = 0
  const segments = []
  for (const item of statusTotals.value) {
    if (item.hours <= 0) continue
    const fraction = item.hours / total
    segments.push({
      status: item.status,
      color: statusHexColor[item.status],
      dash: fraction * DONUT_CIRCUMFERENCE,
      offset: -acc * DONUT_CIRCUMFERENCE
    })
    acc += fraction
  }
  return segments
})
// ─── Tipo: impacto não previsto (Não planejado + Transbordo) ─────────────────
const unplannedImpactPct = computed(() => {
  const total = scopedTotalHours.value
  if (total <= 0) return 0
  const hours = props.demands
    .filter(d => d.type === 'Unplanned' || d.type === 'Spillover')
    .reduce((sum, d) => sum + (d.hours ?? 0), 0)
  return (hours / total) * 100
})

// ─── Status: capacity × executado × tempo restante do quarter ────────────────
const api = useApi()

// Quarter único do escopo (ignora backlog/priorizado); null se houver mais de um.
const scopeQuarter = computed<{ year: number, number: number } | null>(() => {
  const set = new Set<string>()
  for (const d of props.demands) {
    if (isSpecialBacklogQuarter(d.quarterYear, d.quarterNumber)) continue
    set.add(`${d.quarterYear}-${d.quarterNumber}`)
  }
  if (set.size !== 1) return null
  const [only] = [...set]
  const [year, number] = only!.split('-').map(Number)
  return { year: year!, number: number! }
})

// Times presentes no escopo (para somar a capacity cadastrada).
const scopeProjectIds = computed(() => {
  const set = new Set<string>()
  for (const d of props.demands) {
    if (d.itemType === 'Epic') (d.projectIds ?? []).forEach(id => set.add(id))
    else if (d.projectId) set.add(d.projectId)
  }
  return [...set].sort()
})

const capacityHours = ref<number | null>(null)
const capacityKey = computed(() => {
  const q = scopeQuarter.value
  return `${q ? `${q.year}-${q.number}` : 'none'}|${scopeProjectIds.value.join(',')}`
})

watch(capacityKey, async () => {
  capacityHours.value = null
  if (props.onlyCounters) return // só totalizadores: o card Status (que usa capacity) não é renderizado
  const q = scopeQuarter.value
  const ids = scopeProjectIds.value
  if (!q || !ids.length) return
  const key = capacityKey.value
  try {
    const results = await Promise.all(ids.map(pid =>
      api.get<ApiResponse<RoadmapCapacitySummary>>(`/api/roadmap/capacity?projectId=${pid}&quarterYear=${q.year}&quarterNumber=${q.number}`)
        .then(r => r.data)
        .catch(() => null)
    ))
    if (capacityKey.value !== key) return // o escopo mudou durante a busca
    let sum = 0
    let any = false
    for (const result of results) {
      if (result?.capacityHours != null) { sum += result.capacityHours; any = true }
    }
    capacityHours.value = any ? sum : null
  }
  catch {
    capacityHours.value = null
  }
}, { immediate: true })

const executedHours = computed(() =>
  props.demands.filter(d => d.status === 'Done').reduce((sum, d) => sum + (d.hours ?? 0), 0)
)

// Dias restantes até o fim do quarter do escopo (Q1→mar, Q2→jun, Q3→set, Q4→dez).
const quarterDaysRemaining = computed(() => {
  const q = scopeQuarter.value
  if (!q) return null
  const end = new Date(q.year, q.number * 3, 0)
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  return Math.ceil((end.getTime() - today.getTime()) / 86400000)
})

const statusFooter = computed(() => {
  const capacity = capacityHours.value
  const base = capacity != null ? capacity : scopedTotalHours.value
  if (base <= 0) return null
  const exec = executedHours.value
  const pct = Math.round((exec / base) * 100)
  const fmt = (value: number) => value.toLocaleString('pt-BR')
  const head = capacity != null
    ? `Capacity ${fmt(base)}h · ${fmt(exec)}h executadas (${pct}%)`
    : `${fmt(exec)}h de ${fmt(base)}h planejadas (${pct}%)`
  const q = scopeQuarter.value
  const days = quarterDaysRemaining.value
  let tail = ''
  if (q && days != null) {
    const label = formatQuarterLabel(q.year, q.number)
    tail = days > 0 ? ` · faltam ${days} dias no ${label}` : ` · ${label} encerrado`
  }
  return `${head}${tail}`
})

// ─── Classificação: cor da barra por classificação ───────────────────────────
const classificationBarColor: Record<DemandClassification, string> = {
  TechnicalDebtSecurity: 'bg-slate-500', Strategic: 'bg-indigo-500', Evolution: 'bg-sky-500',
  ImprovementGap: 'bg-emerald-500', Mandatory: 'bg-red-500', Homologation: 'bg-violet-500', Customizacao: 'bg-orange-500'
}
const classificationInsight = computed(() => {
  const [first, second] = classificationTotals.value
  if (!first) return null
  if (!second) return { text: `${first.label} representa ${first.percentage.toFixed(1)}%` }
  return { text: `${first.label} + ${second.label} somam ${(first.percentage + second.percentage).toFixed(1)}%` }
})

// ─── Cliente: avatar + insight ───────────────────────────────────────────────
const avatarPalette = [
  'bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300',
  'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300',
  'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300',
  'bg-violet-100 text-violet-700 dark:bg-violet-900/40 dark:text-violet-300',
  'bg-pink-100 text-pink-700 dark:bg-pink-900/40 dark:text-pink-300',
  'bg-cyan-100 text-cyan-700 dark:bg-cyan-900/40 dark:text-cyan-300'
]
function customerInitials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean)
  if (words.length >= 2)
    return (words[0]![0]! + words[1]![0]!).toUpperCase()
  return name.trim().slice(0, 2).toUpperCase()
}
function avatarClass(name: string): string {
  let hash = 0
  for (let i = 0; i < name.length; i++)
    hash = (hash * 31 + name.charCodeAt(i)) >>> 0
  return avatarPalette[hash % avatarPalette.length]!
}
const customerInsight = computed(() => {
  const [first, second] = customerTotals.value
  if (!first) return null
  if (!second) return `${first.name} representa ${first.percentage.toFixed(1)}% das horas totais`
  return `${first.name} e ${second.name} representam ${(first.percentage + second.percentage).toFixed(1)}% das horas totais`
})

// ─── Totalizadores (cards de saúde) ──────────────────────────────────────────
// Badge: verde "OK" quando a contagem é zero; senão o rótulo de ação na cor do card.
const okBadgeClass = 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300'
type CardTheme = { box: string, icon: string, number: string, badge: string, cta: string, activeRing: string }
const cardThemes: Record<'red' | 'amber' | 'blue' | 'violet' | 'pink', CardTheme> = {
  red: {
    box: 'bg-gradient-to-b from-red-50 to-transparent ring-red-200/70 dark:from-red-900/15 dark:ring-red-900/40',
    icon: 'bg-red-100 text-red-600 dark:bg-red-900/30 dark:text-red-300',
    number: 'text-red-600 dark:text-red-400',
    badge: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300',
    cta: 'text-red-600 hover:text-red-700 dark:text-red-400',
    activeRing: 'ring-red-400 dark:ring-red-600'
  },
  amber: {
    box: 'bg-gradient-to-b from-amber-50 to-transparent ring-amber-200/70 dark:from-amber-900/15 dark:ring-amber-900/40',
    icon: 'bg-amber-100 text-amber-600 dark:bg-amber-900/30 dark:text-amber-300',
    number: 'text-amber-600 dark:text-amber-400',
    badge: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300',
    cta: 'text-amber-600 hover:text-amber-700 dark:text-amber-400',
    activeRing: 'ring-amber-400 dark:ring-amber-600'
  },
  blue: {
    box: 'bg-gradient-to-b from-blue-50 to-transparent ring-blue-200/70 dark:from-blue-900/15 dark:ring-blue-900/40',
    icon: 'bg-blue-100 text-blue-600 dark:bg-blue-900/30 dark:text-blue-300',
    number: 'text-blue-600 dark:text-blue-400',
    badge: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300',
    cta: 'text-blue-600 hover:text-blue-700 dark:text-blue-400',
    activeRing: 'ring-blue-400 dark:ring-blue-600'
  },
  violet: {
    box: 'bg-gradient-to-b from-violet-50 to-transparent ring-violet-200/70 dark:from-violet-900/15 dark:ring-violet-900/40',
    icon: 'bg-violet-100 text-violet-600 dark:bg-violet-900/30 dark:text-violet-300',
    number: 'text-violet-600 dark:text-violet-400',
    badge: 'bg-violet-100 text-violet-700 dark:bg-violet-900/30 dark:text-violet-300',
    cta: 'text-violet-600 hover:text-violet-700 dark:text-violet-400',
    activeRing: 'ring-violet-400 dark:ring-violet-600'
  },
  pink: {
    box: 'bg-gradient-to-b from-pink-50 to-transparent ring-pink-200/70 dark:from-pink-900/15 dark:ring-pink-900/40',
    icon: 'bg-pink-100 text-pink-600 dark:bg-pink-900/30 dark:text-pink-300',
    number: 'text-pink-600 dark:text-pink-400',
    badge: 'bg-pink-100 text-pink-700 dark:bg-pink-900/30 dark:text-pink-300',
    cta: 'text-pink-600 hover:text-pink-700 dark:text-pink-400',
    activeRing: 'ring-pink-400 dark:ring-pink-600'
  }
}

// ─── Destaque do item ativo ─────────────────────────────────────────────────────
const af = computed(() => props.activeFilters ?? {})
function isStatusActive(v: string) { return (af.value.statuses ?? []).includes(v) }
function isClassificationActive(v: string) { return (af.value.classifications ?? []).includes(v) }
function isCustomerActive(v: string) { return (af.value.customers ?? []).includes(v) }
function isTypeActive(v: string) { return (af.value.types ?? []).includes(v) }
function isProblemActive(v: string) { return (af.value.problems ?? []).includes(v) }
</script>

<template>
  <div class="space-y-4">
    <!-- Contadores de saúde -->
    <div class="grid gap-4 grid-cols-2 sm:grid-cols-3 xl:grid-cols-5">
      <!-- Atrasos -->
      <div
        class="flex flex-col rounded-xl p-3.5 ring-1 transition-shadow"
        :class="[cardThemes.red.box, (isProblemActive('overdueOpen') || isProblemActive('deliveredLate')) ? `ring-2 ${cardThemes.red.activeRing}` : '']"
      >
        <div class="flex items-center gap-2">
          <div class="flex h-8 w-8 items-center justify-center rounded-lg" :class="cardThemes.red.icon">
            <UIcon name="i-lucide-circle-alert" class="h-4.5 w-4.5" />
          </div>
          <p class="flex-1 text-sm font-semibold text-highlighted">Atrasos</p>
          <span class="inline-flex items-center gap-0.5 rounded-full px-2 py-0.5 text-[10px] font-semibold" :class="(delayedTotals.overdueOpen + delayedTotals.deliveredLate) === 0 ? okBadgeClass : cardThemes.red.badge">
            <UIcon v-if="(delayedTotals.overdueOpen + delayedTotals.deliveredLate) === 0" name="i-lucide-check" class="h-3 w-3" />
            {{ (delayedTotals.overdueOpen + delayedTotals.deliveredLate) === 0 ? 'OK' : 'Atenção' }}
          </span>
        </div>
        <div class="mt-3 flex flex-1 items-stretch gap-2">
          <button
            type="button"
            class="min-w-0 flex-1 rounded-lg p-1.5 text-left transition-colors hover:bg-red-100/40 dark:hover:bg-red-900/20"
            :class="isProblemActive('overdueOpen') ? 'bg-red-100/50 ring-1 ring-red-300 dark:bg-red-900/20 dark:ring-red-800' : ''"
            @click="emitSelect({ kind: 'problem', value: 'overdueOpen' })"
          >
            <p class="text-2xl font-bold leading-none text-red-600 dark:text-red-400">{{ delayedTotals.overdueOpen.toLocaleString('pt-BR') }}</p>
            <p class="mt-1 text-[11px] leading-tight text-muted">atrasados (em aberto)</p>
          </button>
          <button
            type="button"
            class="min-w-0 flex-1 rounded-lg p-1.5 text-left transition-colors hover:bg-amber-100/40 dark:hover:bg-amber-900/20"
            :class="isProblemActive('deliveredLate') ? 'bg-amber-100/50 ring-1 ring-amber-300 dark:bg-amber-900/20 dark:ring-amber-800' : ''"
            @click="emitSelect({ kind: 'problem', value: 'deliveredLate' })"
          >
            <p class="text-2xl font-bold leading-none text-amber-600 dark:text-amber-400">{{ delayedTotals.deliveredLate.toLocaleString('pt-BR') }}</p>
            <p class="mt-1 text-[11px] leading-tight text-muted">entregues c/ atraso</p>
          </button>
        </div>
        <button type="button" class="mt-3 flex items-center justify-between gap-2 text-xs font-semibold transition-colors" :class="cardThemes.red.cta" @click="emitSelect({ kind: 'problem', value: 'overdueOpen' })">
          <span>Ver demandas</span>
          <UIcon name="i-lucide-arrow-right" class="h-3.5 w-3.5" />
        </button>
      </div>

      <!-- Dependências -->
      <div
        class="flex flex-col rounded-xl p-3.5 ring-1 transition-shadow"
        :class="[cardThemes.amber.box, af.inconsistentDeps ? `ring-2 ${cardThemes.amber.activeRing}` : '']"
      >
        <div class="flex items-center gap-2">
          <div class="flex h-8 w-8 items-center justify-center rounded-lg" :class="cardThemes.amber.icon">
            <UIcon name="i-lucide-git-branch" class="h-4.5 w-4.5" />
          </div>
          <p class="flex-1 text-sm font-semibold text-highlighted">Dependências</p>
          <span class="inline-flex items-center gap-0.5 rounded-full px-2 py-0.5 text-[10px] font-semibold" :class="inconsistentDependencyCount === 0 ? okBadgeClass : cardThemes.amber.badge">
            <UIcon v-if="inconsistentDependencyCount === 0" name="i-lucide-check" class="h-3 w-3" />
            {{ inconsistentDependencyCount === 0 ? 'OK' : 'Atenção' }}
          </span>
        </div>
        <div class="mt-3 flex-1">
          <p class="text-3xl font-bold leading-none" :class="cardThemes.amber.number">{{ inconsistentDependencyCount.toLocaleString('pt-BR') }}</p>
          <p class="mt-1 text-[11px] leading-tight text-muted">itens com dependência agendada depois / não priorizada</p>
        </div>
        <button type="button" class="mt-3 flex items-center justify-between gap-2 text-xs font-semibold transition-colors" :class="cardThemes.amber.cta" @click="emitSelect({ kind: 'inconsistentDeps' })">
          <span>Verificar roadmap</span>
          <UIcon name="i-lucide-arrow-right" class="h-3.5 w-3.5" />
        </button>
      </div>

      <!-- Sem Issues -->
      <div
        class="flex flex-col rounded-xl p-3.5 ring-1 transition-shadow"
        :class="[cardThemes.blue.box, isProblemActive('noJira') ? `ring-2 ${cardThemes.blue.activeRing}` : '']"
      >
        <div class="flex items-center gap-2">
          <div class="flex h-8 w-8 items-center justify-center rounded-lg" :class="cardThemes.blue.icon">
            <UIcon name="i-lucide-circle-help" class="h-4.5 w-4.5" />
          </div>
          <p class="flex-1 text-sm font-semibold text-highlighted">Sem Issues</p>
          <span class="inline-flex items-center gap-0.5 rounded-full px-2 py-0.5 text-[10px] font-semibold" :class="missingIssueCount === 0 ? okBadgeClass : cardThemes.blue.badge">
            <UIcon v-if="missingIssueCount === 0" name="i-lucide-check" class="h-3 w-3" />
            {{ missingIssueCount === 0 ? 'OK' : 'Vínculo' }}
          </span>
        </div>
        <div class="mt-3 flex-1">
          <p class="text-3xl font-bold leading-none" :class="cardThemes.blue.number">{{ missingIssueCount.toLocaleString('pt-BR') }}</p>
          <p class="mt-1 text-[11px] leading-tight text-muted">itens sem issue Jira vinculada</p>
        </div>
        <button type="button" class="mt-3 flex items-center justify-between gap-2 text-xs font-semibold transition-colors" :class="cardThemes.blue.cta" @click="emitSelect({ kind: 'problem', value: 'noJira' })">
          <span>Vincular Jira</span>
          <UIcon name="i-lucide-arrow-right" class="h-3.5 w-3.5" />
        </button>
      </div>

      <!-- Sem KPIs -->
      <div
        class="flex flex-col rounded-xl p-3.5 ring-1 transition-shadow"
        :class="[cardThemes.violet.box, isProblemActive('noKpi') ? `ring-2 ${cardThemes.violet.activeRing}` : '']"
      >
        <div class="flex items-center gap-2">
          <div class="flex h-8 w-8 items-center justify-center rounded-lg" :class="cardThemes.violet.icon">
            <UIcon name="i-lucide-target" class="h-4.5 w-4.5" />
          </div>
          <p class="flex-1 text-sm font-semibold text-highlighted">Sem KPIs</p>
          <span class="inline-flex items-center gap-0.5 rounded-full px-2 py-0.5 text-[10px] font-semibold" :class="noKpiCount === 0 ? okBadgeClass : cardThemes.violet.badge">
            <UIcon v-if="noKpiCount === 0" name="i-lucide-check" class="h-3 w-3" />
            {{ noKpiCount === 0 ? 'OK' : 'Sem Métrica' }}
          </span>
        </div>
        <div class="mt-3 flex-1">
          <p class="text-3xl font-bold leading-none" :class="cardThemes.violet.number">{{ noKpiCount.toLocaleString('pt-BR') }}</p>
          <p class="mt-1 text-[11px] leading-tight text-muted">itens sem KPI associado</p>
        </div>
        <button type="button" class="mt-3 flex items-center justify-between gap-2 text-xs font-semibold transition-colors" :class="cardThemes.violet.cta" @click="emitSelect({ kind: 'problem', value: 'noKpi' })">
          <span>Mapear KPIs</span>
          <UIcon name="i-lucide-arrow-right" class="h-3.5 w-3.5" />
        </button>
      </div>

      <!-- Sem Apuração -->
      <div
        class="flex flex-col rounded-xl p-3.5 ring-1 transition-shadow"
        :class="[cardThemes.pink.box, isProblemActive('doneNoKpi') ? `ring-2 ${cardThemes.pink.activeRing}` : '']"
      >
        <div class="flex items-center gap-2">
          <div class="flex h-8 w-8 items-center justify-center rounded-lg" :class="cardThemes.pink.icon">
            <UIcon name="i-lucide-clipboard-check" class="h-4.5 w-4.5" />
          </div>
          <p class="flex-1 text-sm font-semibold text-highlighted">Sem Apuração</p>
          <span class="inline-flex items-center gap-0.5 rounded-full px-2 py-0.5 text-[10px] font-semibold" :class="doneNoKpiCount === 0 ? okBadgeClass : cardThemes.pink.badge">
            <UIcon v-if="doneNoKpiCount === 0" name="i-lucide-check" class="h-3 w-3" />
            {{ doneNoKpiCount === 0 ? 'OK' : 'Apurar' }}
          </span>
        </div>
        <div class="mt-3 flex-1">
          <p class="text-3xl font-bold leading-none" :class="cardThemes.pink.number">{{ doneNoKpiCount.toLocaleString('pt-BR') }}</p>
          <p class="mt-1 text-[11px] leading-tight text-muted">concluídos sem KPI apurado</p>
        </div>
        <button type="button" class="mt-3 flex items-center justify-between gap-2 text-xs font-semibold transition-colors" :class="cardThemes.pink.cta" @click="emitSelect({ kind: 'problem', value: 'doneNoKpi' })">
          <span>Apurar resultados</span>
          <UIcon name="i-lucide-arrow-right" class="h-3.5 w-3.5" />
        </button>
      </div>
    </div>

    <div v-if="!onlyCounters" class="grid items-stretch gap-4 xl:grid-cols-3">
      <!-- Status (donut) -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-slate-100 text-slate-600 dark:bg-slate-800/40 dark:text-slate-300">
            <UIcon name="i-lucide-list-checks" class="h-4.5 w-4.5" />
          </div>
          <p class="flex-1 text-sm font-semibold text-highlighted">Demandas por Status</p>
          <span class="shrink-0 text-[11px] text-muted">{{ demands.length }} itens no total</span>
        </div>
        <div v-if="statusTotals.length" class="flex min-h-0 flex-1 items-center gap-3 px-3 py-2">
          <div class="relative h-32 w-32 shrink-0">
            <svg viewBox="0 0 120 120" class="h-32 w-32 -rotate-90">
              <circle cx="60" cy="60" :r="DONUT_RADIUS" fill="none" stroke-width="15" stroke="currentColor" class="text-neutral-200 dark:text-neutral-800" />
              <circle
                v-for="seg in donutSegments"
                :key="seg.status"
                cx="60" cy="60" :r="DONUT_RADIUS" fill="none" stroke-width="15"
                :stroke="seg.color"
                :stroke-dasharray="`${seg.dash} ${DONUT_CIRCUMFERENCE - seg.dash}`"
                :stroke-dashoffset="seg.offset"
              />
            </svg>
            <div class="absolute inset-0 flex flex-col items-center justify-center">
              <span class="text-lg font-bold text-highlighted">{{ scopedTotalHours.toLocaleString('pt-BR') }}h</span>
              <span class="text-[9px] font-medium uppercase tracking-wide text-muted">Total horas</span>
            </div>
          </div>
          <div class="min-h-0 flex-1 space-y-0.5 self-stretch overflow-y-auto py-1">
            <button
              v-for="item in statusTotals"
              :key="item.status"
              type="button"
              class="flex w-full items-center gap-2 rounded-md px-1.5 py-1 text-left transition-colors"
              :class="isStatusActive(item.status) ? 'bg-primary/10 ring-1 ring-primary/40' : 'hover:bg-elevated'"
              :title="`Filtrar por status ${item.label}`"
              @click="emitSelect({ kind: 'status', value: item.status })"
            >
              <span class="h-2.5 w-2.5 shrink-0 rounded-full" :class="statusDotClass[item.status]" />
              <span class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.label }}</span>
              <span class="shrink-0 text-xs font-semibold text-highlighted">{{ item.hours.toLocaleString('pt-BR') }}h</span>
              <span class="shrink-0 rounded-full bg-elevated px-1.5 py-0.5 text-[11px] text-muted">{{ item.count }} dem.</span>
              <UIcon name="i-lucide-chevron-right" class="h-3.5 w-3.5 shrink-0 text-muted" />
            </button>
          </div>
        </div>
        <div v-else class="flex-1 px-3.5 py-5 text-sm text-muted">Nenhuma demanda no quarter selecionado.</div>
        <div v-if="statusFooter" class="flex items-center gap-1.5 border-t border-default px-3.5 py-2 text-[11px] text-muted">
          <UIcon name="i-lucide-gauge" class="h-3.5 w-3.5 shrink-0" />
          <span class="truncate">{{ statusFooter }}</span>
        </div>
      </UCard>

      <!-- Classificação -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-violet-50 text-violet-600 dark:bg-violet-900/20 dark:text-violet-300">
            <UIcon name="i-lucide-chart-pie" class="h-4.5 w-4.5" />
          </div>
          <p class="flex-1 text-sm font-semibold text-highlighted">Classificação</p>
          <span class="shrink-0 text-[11px] text-muted">{{ classificationTotals.length }} categorias</span>
        </div>
        <div v-if="classificationTotals.length" class="min-h-0 flex-1 space-y-1.5 overflow-y-auto px-3.5 py-2.5">
          <button
            v-for="item in classificationTotals"
            :key="item.classification"
            type="button"
            class="block w-full space-y-1 rounded-md p-1.5 text-left transition-colors"
            :class="isClassificationActive(item.classification) ? 'bg-primary/10 ring-1 ring-primary/40' : 'hover:bg-elevated'"
            :title="`Filtrar por classificação ${item.label}`"
            @click="emitSelect({ kind: 'classification', value: item.classification })"
          >
            <div class="flex items-center gap-2">
              <span class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.label }}</span>
              <span class="shrink-0 text-[11px] text-muted">{{ item.hours.toLocaleString('pt-BR') }}h</span>
              <span class="shrink-0 text-xs font-semibold text-highlighted">{{ item.percentage.toFixed(1) }}%</span>
              <span class="shrink-0 rounded-full bg-elevated px-1.5 py-0.5 text-[11px] text-muted">{{ item.count }} dem.</span>
            </div>
            <div class="h-1.5 overflow-hidden rounded-full bg-elevated">
              <div class="h-full rounded-full transition-all duration-300" :class="classificationBarColor[item.classification]" :style="{ width: `${Math.min(item.percentage, 100)}%` }" />
            </div>
          </button>
        </div>
        <div v-else class="flex-1 px-3.5 py-5 text-sm text-muted">Nenhuma demanda com classificação no quarter selecionado.</div>
        <div v-if="classificationInsight" class="border-t border-default px-3.5 py-2 text-[11px] text-muted">
          {{ classificationInsight.text }}
        </div>
      </UCard>

      <!-- Cliente -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-blue-50 text-blue-600 dark:bg-blue-900/20 dark:text-blue-300">
            <UIcon name="i-lucide-users" class="h-4.5 w-4.5" />
          </div>
          <p class="flex-1 text-sm font-semibold text-highlighted">Demandas por Cliente</p>
        </div>
        <div v-if="customerTotals.length" class="min-h-0 flex-1 space-y-1.5 overflow-y-auto px-3 py-2.5">
          <button
            v-for="item in customerTotals"
            :key="item.name"
            type="button"
            class="flex w-full items-center gap-2.5 rounded-lg border px-2 py-1.5 text-left transition-colors"
            :class="isCustomerActive(item.name) ? 'border-primary/50 bg-primary/10 ring-1 ring-primary/40' : 'border-default bg-elevated/20 hover:bg-elevated'"
            :title="`Filtrar por cliente ${item.name}`"
            @click="emitSelect({ kind: 'customer', value: item.name })"
          >
            <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-[11px] font-bold" :class="avatarClass(item.name)">{{ customerInitials(item.name) }}</span>
            <div class="min-w-0 flex-1">
              <p class="truncate text-sm font-medium text-highlighted">{{ item.name }}</p>
              <div class="mt-1 h-1 overflow-hidden rounded-full bg-elevated">
                <div class="h-full rounded-full bg-primary" :style="{ width: `${Math.min(item.percentage, 100)}%` }" />
              </div>
            </div>
            <div class="shrink-0 text-right">
              <p class="text-sm font-bold text-highlighted">{{ item.hours.toLocaleString('pt-BR') }}h</p>
              <p class="text-[11px] text-muted">{{ item.percentage.toFixed(1) }}%</p>
            </div>
            <span class="shrink-0 rounded-full bg-blue-100 px-2 py-0.5 text-[11px] font-semibold text-blue-700 dark:bg-blue-900/30 dark:text-blue-300">{{ item.count }} dem.</span>
            <UIcon name="i-lucide-chevron-right" class="h-3.5 w-3.5 shrink-0 text-muted" />
          </button>
        </div>
        <div v-else class="flex-1 px-3.5 py-5 text-sm text-muted">Nenhum cliente associado nas demandas deste quarter.</div>
        <div v-if="customerInsight" class="flex items-center justify-between gap-2 border-t border-default px-3.5 py-2 text-[11px] text-muted">
          <span class="truncate">{{ customerInsight }}</span>
          <span class="shrink-0">{{ customerTotals.length }} clientes ativos</span>
        </div>
      </UCard>

      <!-- Tipo -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-50 text-emerald-600 dark:bg-emerald-900/20 dark:text-emerald-300">
            <UIcon name="i-lucide-tags" class="h-4.5 w-4.5" />
          </div>
          <div><p class="text-sm font-semibold text-highlighted">Demandas por Tipo</p></div>
        </div>
        <div v-if="typeTotals.length" class="min-h-0 flex-1 space-y-1.5 overflow-y-auto px-3.5 py-3">
          <button
            v-for="item in typeTotals"
            :key="item.type"
            type="button"
            class="flex w-full items-center gap-2 rounded-lg px-2 py-2 text-left transition-colors"
            :class="isTypeActive(item.type) ? 'bg-primary/10 ring-1 ring-primary/40' : 'border border-default bg-default hover:bg-elevated'"
            :title="`Filtrar por tipo ${item.label}`"
            @click="emitSelect({ kind: 'type', value: item.type })"
          >
            <span class="h-2.5 w-2.5 shrink-0 rounded-full" :class="typeSummaryDot[item.type]" />
            <span class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.label }}</span>
            <span class="shrink-0 text-[11px] text-muted">{{ item.hours.toLocaleString('pt-BR') }}h</span>
            <span class="shrink-0 text-xs font-semibold text-highlighted">{{ item.percentage.toFixed(1) }}%</span>
            <span class="shrink-0 rounded-full bg-elevated px-1.5 py-0.5 text-[11px] text-muted">{{ item.count }} dem.</span>
          </button>
        </div>
        <div v-else class="flex-1 px-4 py-6 text-sm text-muted">Nenhum tipo de demanda registrado neste quarter.</div>
        <div class="flex items-center gap-1.5 border-t border-default px-3.5 py-2 text-[11px] text-muted">
          <UIcon name="i-lucide-triangle-alert" class="h-3.5 w-3.5 shrink-0 text-amber-500" />
          <span class="cursor-help decoration-dotted underline-offset-2 [text-decoration-line:underline]" title="Não planejadas + Transbordos">Impacto não previsto: <span class="font-semibold text-highlighted">{{ unplannedImpactPct.toFixed(1) }}%</span></span>
        </div>
      </UCard>

      <!-- Motivos de Atraso e Transbordo (unificado) -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-amber-50 text-amber-600 dark:bg-amber-900/20 dark:text-amber-300">
            <UIcon name="i-lucide-clock" class="h-4.5 w-4.5" />
          </div>
          <div class="min-w-0">
            <p class="text-sm font-semibold text-highlighted">Motivos de Atraso e Transbordo</p>
            <p class="text-[11px] text-muted">{{ atrasoTransbordoTotals.totalCount }} {{ atrasoTransbordoTotals.totalCount === 1 ? 'item' : 'itens' }} <span class="text-muted/70">(atrasos + transbordos)</span></p>
          </div>
          <button type="button" class="ml-auto inline-flex shrink-0 items-center gap-1 rounded-md border border-default px-2 py-1 text-[11px] font-medium text-muted transition-colors hover:border-primary/40 hover:text-primary" title="Abrir relatório detalhado" @click="emit('report', 'atraso-transbordo')">
            <UIcon name="i-lucide-file-text" class="h-3.5 w-3.5" /> Relatório
          </button>
        </div>
        <div v-if="atrasoTransbordoTotals.items.length" class="min-h-0 flex-1 space-y-2 overflow-y-auto px-3.5 py-3.5">
          <div
            v-for="item in atrasoTransbordoTotals.items"
            :key="item.reason"
            class="block w-full space-y-1.5 rounded-md p-1.5"
          >
            <div class="flex items-center gap-2">
              <span class="h-2.5 w-2.5 rounded-full bg-amber-500 dark:bg-amber-400" />
              <span class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.label }}</span>
              <span class="text-[11px] text-muted">{{ item.hours.toLocaleString('pt-BR') }}h</span>
              <span class="text-xs font-semibold text-highlighted">{{ item.percentage.toFixed(1) }}%</span>
              <span class="rounded-full bg-elevated px-2 py-0.5 text-[11px] text-muted">{{ item.count }} it.</span>
            </div>
            <div class="h-1.5 overflow-hidden rounded-full bg-elevated">
              <div class="h-full rounded-full bg-amber-500 transition-all duration-300 dark:bg-amber-400" :style="{ width: `${Math.min(item.percentage, 100)}%` }" />
            </div>
          </div>
        </div>
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhum atraso ou transbordo com motivo no quarter selecionado.</div>
      </UCard>

      <!-- Motivos da Despriorização -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-pink-50 text-pink-600 dark:bg-pink-900/20 dark:text-pink-300">
            <UIcon name="i-lucide-chevrons-down" class="h-4.5 w-4.5" />
          </div>
          <div class="min-w-0">
            <p class="text-sm font-semibold text-highlighted">Motivos da Despriorização</p>
            <p class="text-[11px] text-muted">{{ deprioritizationReasonTotals.totalCount }} {{ deprioritizationReasonTotals.totalCount === 1 ? 'item despriorizado' : 'itens despriorizados' }}</p>
          </div>
          <button type="button" class="ml-auto inline-flex shrink-0 items-center gap-1 rounded-md border border-default px-2 py-1 text-[11px] font-medium text-muted transition-colors hover:border-primary/40 hover:text-primary" title="Abrir relatório detalhado" @click="emit('report', 'deprioritization')">
            <UIcon name="i-lucide-file-text" class="h-3.5 w-3.5" /> Relatório
          </button>
        </div>
        <div v-if="deprioritizationReasonTotals.items.length" class="min-h-0 flex-1 space-y-2 overflow-y-auto px-3.5 py-3.5">
          <div
            v-for="item in deprioritizationReasonTotals.items"
            :key="item.reason"
            class="block w-full space-y-1.5 rounded-md p-1.5"
          >
            <div class="flex items-center gap-2">
              <span class="h-2.5 w-2.5 rounded-full bg-pink-500 dark:bg-pink-400" />
              <span class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.label }}</span>
              <span class="text-[11px] text-muted">{{ item.hours.toLocaleString('pt-BR') }}h</span>
              <span class="text-xs font-semibold text-highlighted">{{ item.percentage.toFixed(1) }}%</span>
              <span class="rounded-full bg-elevated px-2 py-0.5 text-[11px] text-muted">{{ item.count }} it.</span>
            </div>
            <div class="h-1.5 overflow-hidden rounded-full bg-elevated">
              <div class="h-full rounded-full bg-pink-500 transition-all duration-300 dark:bg-pink-400" :style="{ width: `${Math.min(item.percentage, 100)}%` }" />
            </div>
          </div>
        </div>
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhum item despriorizado com motivo no quarter selecionado.</div>
      </UCard>
    </div>
  </div>
</template>
