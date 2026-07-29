<script setup lang="ts">
import type { RoadmapDemand, DemandStatus, DemandType, DemandClassification, DemandDependency } from '~/types/roadmap'
import type { DashboardSelection, DashboardActiveFilters } from '~/types/roadmapDashboards'
import { isSpecialBacklogQuarter } from '~/utils/roadmapQuarter'

const props = defineProps<{
  // Demandas já filtradas por time/quarter (equivale ao quarterFilteredDemands da planejamento).
  demands: RoadmapDemand[]
  // Todas as demandas carregadas — necessário para resolver épicos/dependências por id.
  allDemands: RoadmapDemand[]
  activeFilters?: DashboardActiveFilters
}>()

const emit = defineEmits<{ select: [selection: DashboardSelection] }>()

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
const classificationBadgeClass: Record<DemandClassification, string> = {
  TechnicalDebtSecurity: 'bg-slate-100 text-slate-700 border-slate-200 dark:bg-slate-800/60 dark:text-slate-300 dark:border-slate-700',
  Strategic: 'bg-indigo-100 text-indigo-700 border-indigo-200 dark:bg-indigo-900/30 dark:text-indigo-300 dark:border-indigo-800',
  Evolution: 'bg-sky-100 text-sky-700 border-sky-200 dark:bg-sky-900/30 dark:text-sky-300 dark:border-sky-800',
  ImprovementGap: 'bg-emerald-100 text-emerald-700 border-emerald-200 dark:bg-emerald-900/30 dark:text-emerald-300 dark:border-emerald-800',
  Mandatory: 'bg-red-100 text-red-700 border-red-200 dark:bg-red-900/30 dark:text-red-300 dark:border-red-800',
  Homologation: 'bg-violet-100 text-violet-700 border-violet-200 dark:bg-violet-900/30 dark:text-violet-300 dark:border-violet-800',
  Customizacao: 'bg-orange-100 text-orange-700 border-orange-200 dark:bg-orange-900/30 dark:text-orange-300 dark:border-orange-800',
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

const spilloverReasonTotals = computed(() =>
  buildReasonTotals(props.demands.filter(item => item.status === 'Spillover'), item => item.spilloverReason ?? undefined, getSpilloverReasonLabel)
)
const deprioritizationReasonTotals = computed(() =>
  buildReasonTotals(props.demands.filter(item => item.status === 'Deprioritized'), item => item.deprioritizationReason ?? undefined, getDeprioritizationReasonLabel)
)

// ─── Destaque do item ativo ─────────────────────────────────────────────────────
const af = computed(() => props.activeFilters ?? {})
function isStatusActive(v: string) { return (af.value.statuses ?? []).includes(v) }
function isClassificationActive(v: string) { return (af.value.classifications ?? []).includes(v) }
function isCustomerActive(v: string) { return (af.value.customers ?? []).includes(v) }
function isTypeActive(v: string) { return (af.value.types ?? []).includes(v) }
function isProblemActive(v: string) { return (af.value.problems ?? []).includes(v) }
function isSpilloverReasonActive(v: string) { return (af.value.spilloverReasons ?? []).includes(v) }
function isDeprioritizationReasonActive(v: string) { return (af.value.deprioritizationReasons ?? []).includes(v) }
</script>

<template>
  <div class="space-y-4">
    <!-- Contadores de saúde -->
    <div class="grid gap-4 grid-cols-2 sm:grid-cols-3 xl:grid-cols-5">
      <UCard class="ring-default" :ui="{ body: 'p-3.5' }">
        <div class="flex items-center gap-2">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-red-50 text-red-600 dark:bg-red-900/20 dark:text-red-300">
            <UIcon name="i-lucide-alarm-clock-off" class="h-4.5 w-4.5" />
          </div>
          <p class="text-sm font-semibold text-highlighted">Atrasos</p>
        </div>
        <div class="mt-3 flex items-stretch gap-2">
          <button
            type="button"
            class="min-w-0 flex-1 rounded-md p-1.5 text-left transition-colors"
            :class="isProblemActive('overdueOpen') ? 'bg-red-50 ring-1 ring-red-300 dark:bg-red-900/20 dark:ring-red-800' : 'hover:bg-elevated'"
            title="Itens atrasados (em aberto)"
            @click="emitSelect({ kind: 'problem', value: 'overdueOpen' })"
          >
            <p class="text-2xl font-bold leading-none text-red-600 dark:text-red-400">{{ delayedTotals.overdueOpen.toLocaleString('pt-BR') }}</p>
            <p class="mt-1 text-[11px] leading-tight text-muted">atrasados (em aberto)</p>
          </button>
          <button
            type="button"
            class="min-w-0 flex-1 rounded-md p-1.5 text-left transition-colors"
            :class="isProblemActive('deliveredLate') ? 'bg-amber-50 ring-1 ring-amber-300 dark:bg-amber-900/20 dark:ring-amber-800' : 'hover:bg-elevated'"
            title="Itens entregues com atraso"
            @click="emitSelect({ kind: 'problem', value: 'deliveredLate' })"
          >
            <p class="text-2xl font-bold leading-none text-amber-600 dark:text-amber-400">{{ delayedTotals.deliveredLate.toLocaleString('pt-BR') }}</p>
            <p class="mt-1 text-[11px] leading-tight text-muted">entregues com atraso</p>
          </button>
        </div>
      </UCard>

      <UCard class="ring-default" :ui="{ body: 'p-3.5' }">
        <div class="flex items-center gap-2">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-orange-50 text-orange-600 dark:bg-orange-900/20 dark:text-orange-300">
            <UIcon name="i-lucide-unlink" class="h-4.5 w-4.5" />
          </div>
          <p class="text-sm font-semibold text-highlighted">Dependências inconsistentes</p>
        </div>
        <button
          type="button"
          class="mt-3 block w-full rounded-md p-1.5 text-left transition-colors"
          :class="af.inconsistentDeps ? 'bg-orange-50 ring-1 ring-orange-300 dark:bg-orange-900/20 dark:ring-orange-800' : 'hover:bg-elevated'"
          title="Itens com dependência inconsistente"
          @click="emitSelect({ kind: 'inconsistentDeps' })"
        >
          <p class="text-2xl font-bold leading-none text-orange-600 dark:text-orange-400">{{ inconsistentDependencyCount.toLocaleString('pt-BR') }}</p>
          <p class="mt-1 text-[11px] leading-tight text-muted">itens com dependência agendada depois / não priorizada</p>
        </button>
      </UCard>

      <UCard class="ring-default" :ui="{ body: 'p-3.5' }">
        <div class="flex items-center gap-2">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-sky-50 text-sky-600 dark:bg-sky-900/20 dark:text-sky-300">
            <UIcon name="i-simple-icons-jira" class="h-4 w-4" />
          </div>
          <p class="text-sm font-semibold text-highlighted">Demandas sem issues</p>
        </div>
        <button
          type="button"
          class="mt-3 block w-full rounded-md p-1.5 text-left transition-colors"
          :class="isProblemActive('noJira') ? 'bg-sky-50 ring-1 ring-sky-300 dark:bg-sky-900/20 dark:ring-sky-800' : 'hover:bg-elevated'"
          title="Itens sem issue Jira"
          @click="emitSelect({ kind: 'problem', value: 'noJira' })"
        >
          <p class="text-2xl font-bold leading-none text-sky-600 dark:text-sky-400">{{ missingIssueCount.toLocaleString('pt-BR') }}</p>
          <p class="mt-1 text-[11px] leading-tight text-muted">itens sem issue Jira vinculada</p>
        </button>
      </UCard>

      <UCard class="ring-default" :ui="{ body: 'p-3.5' }">
        <div class="flex items-center gap-2">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-amber-50 text-amber-600 dark:bg-amber-900/20 dark:text-amber-300">
            <UIcon name="i-lucide-target" class="h-4.5 w-4.5" />
          </div>
          <p class="text-sm font-semibold text-highlighted">Itens sem KPIs</p>
        </div>
        <button
          type="button"
          class="mt-3 block w-full rounded-md p-1.5 text-left transition-colors"
          :class="isProblemActive('noKpi') ? 'bg-amber-50 ring-1 ring-amber-300 dark:bg-amber-900/20 dark:ring-amber-800' : 'hover:bg-elevated'"
          title="Itens sem KPI associado"
          @click="emitSelect({ kind: 'problem', value: 'noKpi' })"
        >
          <p class="text-2xl font-bold leading-none text-amber-600 dark:text-amber-400">{{ noKpiCount.toLocaleString('pt-BR') }}</p>
          <p class="mt-1 text-[11px] leading-tight text-muted">itens sem KPI associado</p>
        </button>
      </UCard>

      <UCard class="ring-default" :ui="{ body: 'p-3.5' }">
        <div class="flex items-center gap-2">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-pink-50 text-pink-600 dark:bg-pink-900/20 dark:text-pink-300">
            <UIcon name="i-lucide-clipboard-x" class="h-4.5 w-4.5" />
          </div>
          <p class="text-sm font-semibold text-highlighted">Concluídos sem KPIs apurados</p>
        </div>
        <button
          type="button"
          class="mt-3 block w-full rounded-md p-1.5 text-left transition-colors"
          :class="isProblemActive('doneNoKpi') ? 'bg-pink-50 ring-1 ring-pink-300 dark:bg-pink-900/20 dark:ring-pink-800' : 'hover:bg-elevated'"
          title="Concluídos sem apuração de KPI"
          @click="emitSelect({ kind: 'problem', value: 'doneNoKpi' })"
        >
          <p class="text-2xl font-bold leading-none text-pink-600 dark:text-pink-400">{{ doneNoKpiCount.toLocaleString('pt-BR') }}</p>
          <p class="mt-1 text-[11px] leading-tight text-muted">concluídos sem KPI apurado</p>
        </button>
      </UCard>
    </div>

    <div class="grid items-stretch gap-4 xl:grid-cols-3">
      <!-- Status -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-slate-100 text-slate-600 dark:bg-slate-800/40 dark:text-slate-300">
            <UIcon name="i-lucide-list-checks" class="h-4.5 w-4.5" />
          </div>
          <p class="text-sm font-semibold text-highlighted">Demandas por Status</p>
        </div>
        <div v-if="statusTotals.length" class="min-h-0 flex-1 space-y-2 overflow-y-auto px-3.5 py-3.5">
          <button
            v-for="item in statusTotals"
            :key="item.status"
            type="button"
            class="block w-full space-y-1.5 rounded-md p-1.5 text-left transition-colors"
            :class="isStatusActive(item.status) ? 'bg-primary/10 ring-1 ring-primary/40' : 'hover:bg-elevated'"
            :title="`Filtrar por status ${item.label}`"
            @click="emitSelect({ kind: 'status', value: item.status })"
          >
            <div class="flex items-center gap-2">
              <span class="h-2.5 w-2.5 rounded-full" :class="statusDotClass[item.status]" />
              <span class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.label }}</span>
              <span class="text-[11px] text-muted">{{ item.hours.toLocaleString('pt-BR') }}h</span>
              <span class="text-xs font-semibold text-highlighted">{{ item.percentage.toFixed(1) }}%</span>
              <span class="rounded-full bg-elevated px-2 py-0.5 text-[11px] text-muted">{{ item.count }} dem.</span>
            </div>
            <div class="h-1.5 overflow-hidden rounded-full bg-elevated">
              <div class="h-full rounded-full transition-all duration-300" :class="statusDotClass[item.status]" :style="{ width: `${Math.min(item.percentage, 100)}%` }" />
            </div>
          </button>
        </div>
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhuma demanda no quarter selecionado.</div>
      </UCard>

      <!-- Classificação -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-violet-50 text-violet-600 dark:bg-violet-900/20 dark:text-violet-300">
            <UIcon name="i-lucide-chart-pie" class="h-4.5 w-4.5" />
          </div>
          <div><p class="text-sm font-semibold text-highlighted">Classificação</p></div>
        </div>
        <div v-if="classificationTotals.length" class="min-h-0 flex-1 space-y-2 overflow-y-auto px-3.5 py-3.5">
          <button
            v-for="item in classificationTotals"
            :key="item.classification"
            type="button"
            class="block w-full space-y-1.5 rounded-md p-1.5 text-left transition-colors"
            :class="isClassificationActive(item.classification) ? 'bg-primary/10 ring-1 ring-primary/40' : 'hover:bg-elevated'"
            :title="`Filtrar por classificação ${item.label}`"
            @click="emitSelect({ kind: 'classification', value: item.classification })"
          >
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
          </button>
        </div>
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhuma demanda com classificação no quarter selecionado.</div>
      </UCard>

      <!-- Cliente -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-blue-50 text-blue-600 dark:bg-blue-900/20 dark:text-blue-300">
            <UIcon name="i-lucide-users" class="h-4.5 w-4.5" />
          </div>
          <div><p class="text-sm font-semibold text-highlighted">Demandas por Cliente</p></div>
        </div>
        <div v-if="customerTotals.length" class="flex min-h-0 flex-1 flex-col px-3.5 py-3.5">
          <div class="min-h-0 flex-1 space-y-2 overflow-y-auto pr-1 pb-3">
            <button
              v-for="item in customerTotals"
              :key="item.name"
              type="button"
              class="block w-full rounded-lg border px-2.5 py-2 text-left transition-colors"
              :class="isCustomerActive(item.name) ? 'border-primary/50 bg-primary/10 ring-1 ring-primary/40' : 'border-default bg-elevated/20 hover:bg-elevated'"
              :title="`Filtrar por cliente ${item.name}`"
              @click="emitSelect({ kind: 'customer', value: item.name })"
            >
              <div class="flex items-center gap-2">
                <p class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.name }}</p>
                <span class="text-[11px] text-muted">{{ item.hours.toLocaleString('pt-BR') }}h</span>
                <span class="rounded-full bg-blue-50 px-2 py-0.5 text-[11px] font-semibold text-blue-700 dark:bg-blue-900/30 dark:text-blue-300">{{ item.percentage.toFixed(1) }}%</span>
                <span class="rounded-full bg-blue-100 px-2 py-0.5 text-[11px] font-semibold text-blue-700 dark:bg-blue-900/30 dark:text-blue-300">{{ item.count }} dem.</span>
              </div>
            </button>
          </div>
        </div>
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhum cliente associado nas demandas deste quarter.</div>
      </UCard>

      <!-- Tipo -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-50 text-emerald-600 dark:bg-emerald-900/20 dark:text-emerald-300">
            <UIcon name="i-lucide-tags" class="h-4.5 w-4.5" />
          </div>
          <div><p class="text-sm font-semibold text-highlighted">Demandas por Tipo</p></div>
        </div>
        <div v-if="typeTotals.length" class="space-y-3 px-3.5 py-3.5">
          <button
            v-for="item in typeTotals"
            :key="item.type"
            type="button"
            class="flex w-full items-center justify-between gap-3 rounded-xl border px-3 py-2.5 text-left shadow-sm transition-colors"
            :class="isTypeActive(item.type) ? 'border-primary/50 bg-primary/10 ring-1 ring-primary/40' : 'border-default bg-default hover:bg-elevated'"
            :title="`Filtrar por tipo ${item.label}`"
            @click="emitSelect({ kind: 'type', value: item.type })"
          >
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
              <span class="rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-300">{{ item.percentage.toFixed(1) }}%</span>
              <span class="rounded-full bg-elevated px-3 py-1 text-xs font-semibold text-highlighted">{{ item.count }} demandas</span>
            </div>
          </button>
        </div>
        <div v-else class="px-4 py-6 text-sm text-muted">Nenhum tipo de demanda registrado neste quarter.</div>
      </UCard>

      <!-- Motivos de Transbordo -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-orange-50 text-orange-600 dark:bg-orange-900/20 dark:text-orange-300">
            <UIcon name="i-lucide-waves" class="h-4.5 w-4.5" />
          </div>
          <div>
            <p class="text-sm font-semibold text-highlighted">Motivos de Transbordo</p>
            <p class="text-[11px] text-muted">{{ spilloverReasonTotals.totalCount }} {{ spilloverReasonTotals.totalCount === 1 ? 'item em transbordo' : 'itens em transbordo' }}</p>
          </div>
        </div>
        <div v-if="spilloverReasonTotals.items.length" class="min-h-0 flex-1 space-y-2 overflow-y-auto px-3.5 py-3.5">
          <button
            v-for="item in spilloverReasonTotals.items"
            :key="item.reason"
            type="button"
            class="block w-full space-y-1.5 rounded-md p-1.5 text-left transition-colors"
            :class="isSpilloverReasonActive(item.reason) ? 'bg-primary/10 ring-1 ring-primary/40' : 'hover:bg-elevated'"
            :title="`Filtrar por motivo de transbordo ${item.label}`"
            @click="emitSelect({ kind: 'spilloverReason', value: item.reason })"
          >
            <div class="flex items-center gap-2">
              <span class="h-2.5 w-2.5 rounded-full bg-orange-500 dark:bg-orange-400" />
              <span class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.label }}</span>
              <span class="text-[11px] text-muted">{{ item.hours.toLocaleString('pt-BR') }}h</span>
              <span class="text-xs font-semibold text-highlighted">{{ item.percentage.toFixed(1) }}%</span>
              <span class="rounded-full bg-elevated px-2 py-0.5 text-[11px] text-muted">{{ item.count }} it.</span>
            </div>
            <div class="h-1.5 overflow-hidden rounded-full bg-elevated">
              <div class="h-full rounded-full bg-orange-500 transition-all duration-300 dark:bg-orange-400" :style="{ width: `${Math.min(item.percentage, 100)}%` }" />
            </div>
          </button>
        </div>
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhum item em transbordo com motivo no quarter selecionado.</div>
      </UCard>

      <!-- Motivos da Despriorização -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-pink-50 text-pink-600 dark:bg-pink-900/20 dark:text-pink-300">
            <UIcon name="i-lucide-chevrons-down" class="h-4.5 w-4.5" />
          </div>
          <div>
            <p class="text-sm font-semibold text-highlighted">Motivos da Despriorização</p>
            <p class="text-[11px] text-muted">{{ deprioritizationReasonTotals.totalCount }} {{ deprioritizationReasonTotals.totalCount === 1 ? 'item despriorizado' : 'itens despriorizados' }}</p>
          </div>
        </div>
        <div v-if="deprioritizationReasonTotals.items.length" class="min-h-0 flex-1 space-y-2 overflow-y-auto px-3.5 py-3.5">
          <button
            v-for="item in deprioritizationReasonTotals.items"
            :key="item.reason"
            type="button"
            class="block w-full space-y-1.5 rounded-md p-1.5 text-left transition-colors"
            :class="isDeprioritizationReasonActive(item.reason) ? 'bg-primary/10 ring-1 ring-primary/40' : 'hover:bg-elevated'"
            :title="`Filtrar por motivo de despriorização ${item.label}`"
            @click="emitSelect({ kind: 'deprioritizationReason', value: item.reason })"
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
          </button>
        </div>
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhum item despriorizado com motivo no quarter selecionado.</div>
      </UCard>
    </div>
  </div>
</template>
