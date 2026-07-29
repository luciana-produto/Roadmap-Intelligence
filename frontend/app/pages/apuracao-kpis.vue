<script setup lang="ts">
import {
  BACKLOG_QUARTER,
  PRIORITIZED_BACKLOG_QUARTER,
  PRE_REGISTERED_QUARTER_END_YEAR,
  buildPreRegisteredQuarterYears,
  buildQuarterValue,
  formatQuarterLabel,
  parseQuarterValue
} from '~/utils/roadmapQuarter'
import {
  KPI_INDICATOR_LABELS,
  CONFIDENCE_LABELS,
  liveKpiResult,
  kpiAttainmentPct,
  formatKpiValue,
  formatKpiAttainment,
  formatKpiDate
} from '~/utils/kpiApuracao'
import ColumnMultiFilter from '~/components/roadmap/ColumnMultiFilter.vue'
import type { KpiIndicator, KpiUnit, ConfidenceLevel, MeasurementResult, KpiMeasurement, RoadmapDemand } from '~/types/roadmap'

useSeoMeta({ title: 'Apuração KPIs · ProductHub' })

const roadmapStore = useRoadmapStore()
const kpiStore = useKpiStore()
const { projects, demands, isLoading, selectedProjectId, selectedQuarterYear, selectedQuarterNumber } = storeToRefs(roadmapStore)
const { kpis } = storeToRefs(kpiStore)

const now = new Date()
const currentYear = now.getFullYear()

// Filtros do topo (mantidos): time + quarter.
const filterTeams = ref<string[]>([])
const filterQuarters = ref<string[]>([1, 2, 3, 4].map(q => buildQuarterValue(currentYear, q)))

// Filtros nas colunas.
const searchEpic = ref('')
const filterIndicators = ref<string[]>([])
const filterKpis = ref<string[]>([])
const filterResults = ref<string[]>([])
const filterConfidence = ref<string[]>([])

onMounted(async () => {
  selectedProjectId.value = null
  selectedQuarterYear.value = null
  selectedQuarterNumber.value = null
  await roadmapStore.fetchProjects()
  await roadmapStore.fetchDemands()
  await kpiStore.fetchKpis()
})

const sortedProjects = computed(() =>
  [...projects.value].sort((left, right) => left.name.localeCompare(right.name, 'pt-BR'))
)
const projectNamesById = computed(() => new Map(projects.value.map(p => [p.id, p.name])))
const teamOptions = computed(() => sortedProjects.value.map(p => ({ value: p.id, label: p.name })))

// Opções de quarter (topo + filtro de coluna).
function quarterShortLabel(value: string) {
  const { quarterYear, quarterNumber } = parseQuarterValue(value)
  return formatQuarterLabel(quarterYear, quarterNumber)
}

const quarterOptions = computed(() => [
  { value: BACKLOG_QUARTER.value, label: BACKLOG_QUARTER.label },
  { value: PRIORITIZED_BACKLOG_QUARTER.value, label: PRIORITIZED_BACKLOG_QUARTER.label },
  ...buildPreRegisteredQuarterYears(currentYear, PRE_REGISTERED_QUARTER_END_YEAR).flatMap(y =>
    [1, 2, 3, 4].map(q => ({ value: buildQuarterValue(y, q), label: `Q${q}/${String(y).slice(2)}` }))
  )
])

const quarterYearOptions = computed(() =>
  buildPreRegisteredQuarterYears(currentYear, PRE_REGISTERED_QUARTER_END_YEAR).map(year => ({
    year,
    values: [1, 2, 3, 4].map(q => buildQuarterValue(year, q))
  }))
)

const teamsLabel = computed(() => {
  if (!filterTeams.value.length) return 'Todos os times'
  if (filterTeams.value.length === 1)
    return projects.value.find(p => p.id === filterTeams.value[0])?.name ?? '1 time'
  return `${filterTeams.value.length} times`
})

const quartersLabel = computed(() => {
  if (!filterQuarters.value.length) return 'Todos os quarters'
  if (filterQuarters.value.length === 1) return quarterShortLabel(filterQuarters.value[0]!)
  if (filterQuarters.value.length === 2) return filterQuarters.value.map(quarterShortLabel).join(', ')
  return `${filterQuarters.value.length} quarters`
})

function toggleTeam(id: string) {
  filterTeams.value = filterTeams.value.includes(id)
    ? filterTeams.value.filter(t => t !== id)
    : [...filterTeams.value, id]
}
function toggleQuarter(value: string) {
  filterQuarters.value = filterQuarters.value.includes(value)
    ? filterQuarters.value.filter(q => q !== value)
    : [...filterQuarters.value, value]
}
function isQuarterYearFullySelected(values: string[]) {
  return values.every(v => filterQuarters.value.includes(v))
}
function toggleQuarterYear(values: string[]) {
  if (isQuarterYearFullySelected(values)) {
    filterQuarters.value = filterQuarters.value.filter(v => !values.includes(v))
    return
  }
  const next = new Set(filterQuarters.value)
  for (const v of values) next.add(v)
  filterQuarters.value = [...next]
}

// ─── Escopo (demandas + épicos simples) → épicos donos dos KPIs ───────────────
const scopedDemands = computed(() => {
  const base = demands.value.filter(item => item.itemType === 'Demand' || (item.itemType === 'Epic' && item.isSimple))
  const teamFiltered = filterTeams.value.length
    ? base.filter(d => d.itemType === 'Epic'
        ? (d.projectIds ?? []).some(pid => filterTeams.value.includes(pid))
        : filterTeams.value.includes(d.projectId ?? ''))
    : base
  if (!filterQuarters.value.length) return teamFiltered
  return teamFiltered.filter(d => filterQuarters.value.includes(buildQuarterValue(d.quarterYear, d.quarterNumber)))
})

const scopedEpics = computed(() => {
  const ids = new Set<string>()
  for (const d of scopedDemands.value) {
    if (d.itemType === 'Epic') ids.add(d.id)
    else if (d.epicId) ids.add(d.epicId)
  }
  return demands.value.filter(e => e.itemType === 'Epic' && ids.has(e.id) && !e.hasNoKpi && e.kpiLinks.length > 0)
})

const kpiById = computed(() => new Map(kpis.value.map(k => [k.id, k])))

// Demandas-filhas por épico (para derivar o quarter mais recente de épicos compostos).
const childDemandsByEpic = computed(() => {
  const map = new Map<string, RoadmapDemand[]>()
  for (const d of demands.value) {
    if (d.itemType === 'Demand' && d.epicId) {
      const arr = map.get(d.epicId) ?? []
      arr.push(d)
      map.set(d.epicId, arr)
    }
  }
  return map
})

function latestMeasurement(epic: RoadmapDemand, kpiId: string): KpiMeasurement | null {
  let best: KpiMeasurement | null = null
  for (const m of epic.kpiMeasurements ?? []) {
    if (m.kpiId !== kpiId) continue
    if (!best
      || m.measurementDate > best.measurementDate
      || (m.measurementDate === best.measurementDate && m.createdAt > best.createdAt)) {
      best = m
    }
  }
  return best
}

function epicTeamLabel(epic: RoadmapDemand): string {
  const ids = epic.projectIds?.length ? epic.projectIds : (epic.projectId ? [epic.projectId] : [])
  const names = ids.map(id => projectNamesById.value.get(id)).filter((n): n is string => !!n)
  return names.length ? names.join(', ') : '—'
}

// Quarter do épico: mais recente entre as demandas-filhas (simples usa o próprio).
function epicQuarterInfo(epic: RoadmapDemand): { label: string, order: number } {
  let year = epic.quarterYear
  let numberQ = epic.quarterNumber
  if (!epic.isSimple) {
    const children = childDemandsByEpic.value.get(epic.id) ?? []
    if (children.length) {
      let latest = children[0]!
      for (const child of children) {
        if (child.quarterYear * 4 + child.quarterNumber > latest.quarterYear * 4 + latest.quarterNumber)
          latest = child
      }
      year = latest.quarterYear
      numberQ = latest.quarterNumber
    }
  }
  return { label: formatQuarterLabel(year, numberQ), order: year * 4 + numberQ }
}

// ─── Opções de filtro de coluna ──────────────────────────────────────────────
const indicatorOptions = computed(() => {
  const set = new Set<string>()
  for (const epic of scopedEpics.value)
    for (const link of epic.kpiLinks) {
      const kpi = kpiById.value.get(link.kpiId)
      if (kpi) set.add(kpi.indicator)
    }
  return [...set]
    .map(indicator => ({ value: indicator, label: KPI_INDICATOR_LABELS[indicator as KpiIndicator] }))
    .sort((a, b) => a.label.localeCompare(b.label, 'pt-BR'))
})

// KPIs presentes no escopo, limitados aos indicadores marcados.
const kpiFilterOptions = computed(() => {
  const byId = new Map<string, string>()
  for (const epic of scopedEpics.value) {
    for (const link of epic.kpiLinks) {
      const kpi = kpiById.value.get(link.kpiId)
      if (!kpi) continue
      if (filterIndicators.value.length && !filterIndicators.value.includes(kpi.indicator)) continue
      byId.set(link.kpiId, link.kpiName)
    }
  }
  return [...byId.entries()]
    .map(([value, label]) => ({ value, label }))
    .sort((a, b) => a.label.localeCompare(b.label, 'pt-BR'))
})

// Ao mudar os indicadores, remove KPIs selecionados que saíram do escopo.
watch(filterIndicators, () => {
  const valid = new Set(kpiFilterOptions.value.map(o => o.value))
  filterKpis.value = filterKpis.value.filter(id => valid.has(id))
})

const resultOptions = [
  { value: 'Positive', label: 'Positivo' },
  { value: 'Negative', label: 'Negativo' },
  { value: 'Neutral', label: 'Neutro' },
  { value: 'none', label: 'Sem apuração' }
]
const confidenceOptions = [
  { value: 'High', label: 'Alta' },
  { value: 'Medium', label: 'Média' },
  { value: 'Low', label: 'Baixa' }
]

// ─── Linhas ──────────────────────────────────────────────────────────────────
type ReportRow = {
  key: string
  epicId: string
  epicTitle: string
  teamLabel: string
  quarterLabel: string
  quarterOrder: number
  kpiName: string
  indicator: string
  indicatorLabel: string
  unit: KpiUnit
  estimated: number | null
  apurado: number | null
  result: MeasurementResult | null
  attainment: number | null
  confidence: ConfidenceLevel
  deliveryDate: string | null
  date: string | null
}

const filteredRows = computed<ReportRow[]>(() => {
  const epicText = searchEpic.value.trim().toLowerCase()
  const list: ReportRow[] = []
  for (const epic of scopedEpics.value) {
    const quarter = epicQuarterInfo(epic)
    const team = epicTeamLabel(epic)
    for (const link of epic.kpiLinks) {
      const kpi = kpiById.value.get(link.kpiId)
      if (!kpi) continue
      if (filterIndicators.value.length && !filterIndicators.value.includes(kpi.indicator)) continue
      if (filterKpis.value.length && !filterKpis.value.includes(link.kpiId)) continue
      if (epicText && !epic.title.toLowerCase().includes(epicText)) continue

      const measurement = latestMeasurement(epic, link.kpiId)
      const estimated = link.estimatedImpact ?? null
      const apurado = measurement?.measuredValue ?? null
      const result = measurement ? liveKpiResult(kpi.operation, estimated, apurado) : null

      if (filterResults.value.length && !filterResults.value.includes(result ?? 'none')) continue
      if (filterConfidence.value.length && !filterConfidence.value.includes(link.confidenceLevel)) continue

      list.push({
        key: `${epic.id}:${link.kpiId}`,
        epicId: epic.id,
        epicTitle: epic.title,
        teamLabel: team,
        quarterLabel: quarter.label,
        quarterOrder: quarter.order,
        kpiName: link.kpiName,
        indicator: kpi.indicator,
        indicatorLabel: KPI_INDICATOR_LABELS[kpi.indicator],
        unit: link.unit,
        estimated,
        apurado,
        result,
        attainment: measurement ? kpiAttainmentPct(kpi.operation, estimated, apurado) : null,
        confidence: link.confidenceLevel,
        deliveryDate: epic.deliveryDate ?? null,
        date: measurement?.measurementDate ?? null
      })
    }
  }
  return list
})

// ─── Ordenação ───────────────────────────────────────────────────────────────
type SortKey = 'epic' | 'team' | 'quarter' | 'indicator' | 'kpi' | 'estimated' | 'apurado' | 'result' | 'attainment' | 'confidence' | 'delivery' | 'date'

const sortKey = ref<SortKey>('date')
const sortDir = ref<'asc' | 'desc'>('desc')

function toggleSort(key: SortKey) {
  if (sortKey.value === key) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  }
  else {
    sortKey.value = key
    sortDir.value = 'asc'
  }
}
function sortIcon(key: SortKey): string {
  if (sortKey.value !== key) return 'i-lucide-chevrons-up-down'
  return sortDir.value === 'asc' ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'
}

const resultRank: Record<MeasurementResult, number> = { Positive: 3, Neutral: 2, Negative: 1 }
const confidenceRank: Record<ConfidenceLevel, number> = { High: 3, Medium: 2, Low: 1 }

function sortValue(row: ReportRow, key: SortKey): string | number | null {
  switch (key) {
    case 'epic': return row.epicTitle
    case 'team': return row.teamLabel === '—' ? '' : row.teamLabel
    case 'quarter': return row.quarterOrder
    case 'indicator': return row.indicatorLabel
    case 'kpi': return row.kpiName
    case 'estimated': return row.estimated
    case 'apurado': return row.apurado
    case 'result': return row.result ? resultRank[row.result] : null
    case 'attainment': return row.attainment
    case 'confidence': return confidenceRank[row.confidence]
    case 'delivery': return row.deliveryDate
    case 'date': return row.date
    default: return null
  }
}

function isEmpty(value: string | number | null): boolean {
  return value == null || value === '' || (typeof value === 'number' && !Number.isFinite(value) && Number.isNaN(value))
}

const sortedRows = computed<ReportRow[]>(() => {
  const key = sortKey.value
  const dir = sortDir.value === 'asc' ? 1 : -1
  return [...filteredRows.value].sort((a, b) => {
    const av = sortValue(a, key)
    const bv = sortValue(b, key)
    const ae = isEmpty(av)
    const be = isEmpty(bv)
    if (ae && be) return tieBreak(a, b)
    if (ae) return 1 // vazios sempre por último
    if (be) return -1
    let r: number
    if (typeof av === 'number' && typeof bv === 'number') r = av - bv
    else r = String(av).localeCompare(String(bv), 'pt-BR')
    if (r === 0) return tieBreak(a, b)
    return r * dir
  })
})

function tieBreak(a: ReportRow, b: ReportRow): number {
  const byEpic = a.epicTitle.localeCompare(b.epicTitle, 'pt-BR')
  return byEpic !== 0 ? byEpic : a.kpiName.localeCompare(b.kpiName, 'pt-BR')
}

// ─── Rótulos / estilos de resultado ──────────────────────────────────────────
function resultLabel(result: MeasurementResult | null): string {
  if (result === 'Positive') return 'Positivo'
  if (result === 'Negative') return 'Negativo'
  if (result === 'Neutral') return 'Neutro'
  return '—'
}
function resultToneClass(result: MeasurementResult | null): string {
  if (result === 'Positive') return 'bg-green-50 text-green-700 dark:bg-green-900/30 dark:text-green-300'
  if (result === 'Negative') return 'bg-red-50 text-red-700 dark:bg-red-900/30 dark:text-red-300'
  return 'bg-elevated text-muted'
}

function openEpic(epicId: string) {
  const epic = demands.value.find(item => item.id === epicId)
  const projectId = epic?.projectId ?? epic?.projectIds?.[0] ?? ''
  const params = new URLSearchParams({ kpiDemandId: epicId })
  if (projectId) params.set('projectId', projectId)
  window.open(`/roadmap?${params.toString()}`, '_blank')
}

// ─── Exportação CSV (respeita filtros + ordenação) ────────────────────────────
function csvCell(value: string): string {
  return `"${String(value ?? '').replace(/"/g, '""')}"`
}
function exportCsv() {
  const headers = ['Épico', 'Time', 'Quarter', 'Indicador', 'KPI', 'Estimado', 'Apurado', 'Resultado', '% atingimento', 'Confiança', 'Dt Entrega', 'Dt Apuração']
  const lines = sortedRows.value.map(row => [
    row.epicTitle,
    row.teamLabel,
    row.quarterLabel,
    row.indicatorLabel,
    row.kpiName,
    row.estimated != null ? formatKpiValue(row.estimated, row.unit) : '',
    row.apurado != null ? formatKpiValue(row.apurado, row.unit) : '',
    resultLabel(row.result),
    row.result != null ? formatKpiAttainment(row.attainment) : '',
    CONFIDENCE_LABELS[row.confidence],
    row.deliveryDate ? formatKpiDate(row.deliveryDate) : '',
    row.date ? formatKpiDate(row.date) : ''
  ])
  const csv = [headers, ...lines].map(cols => cols.map(csvCell).join(';')).join('\r\n')
  const blob = new Blob([`﻿${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `apuracao-kpis-${new Date().toISOString().slice(0, 10)}.csv`
  anchor.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div class="space-y-5">
    <!-- Cabeçalho + filtros do topo (time/quarter) -->
    <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">Apuração de KPIs</h1>
        <p class="mt-1 text-sm text-muted">
          Última apuração de cada KPI. Ordene clicando nos títulos e filtre nas colunas. Clique numa linha para abrir o épico.
        </p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <!-- Times -->
        <UPopover :content="{ side: 'bottom', align: 'end', sideOffset: 8 }">
          <button class="flex items-center gap-1.5 rounded-lg border border-default bg-default px-3 py-1.5 text-sm transition-colors hover:border-primary/40">
            <UIcon name="i-lucide-users" class="h-3.5 w-3.5 shrink-0 text-muted" />
            <span class="truncate text-left text-highlighted">{{ teamsLabel }}</span>
            <UBadge v-if="filterTeams.length" size="xs" color="primary" variant="solid" class="shrink-0">{{ filterTeams.length }}</UBadge>
            <UIcon name="i-lucide-chevron-down" class="h-3.5 w-3.5 shrink-0 text-muted" />
          </button>
          <template #content>
            <div class="max-h-72 min-w-[240px] overflow-y-auto py-1">
              <button
                class="flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors hover:bg-elevated"
                :class="filterTeams.length === 0 ? 'font-medium text-primary' : 'text-highlighted'"
                @click="filterTeams = []"
              >
                <UIcon v-if="filterTeams.length === 0" name="i-lucide-check" class="h-3.5 w-3.5 shrink-0" />
                <span v-else class="inline-block h-3.5 w-3.5 shrink-0" />
                Todos os times
              </button>
              <button
                v-for="project in sortedProjects"
                :key="project.id"
                class="flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors hover:bg-elevated"
                :class="filterTeams.includes(project.id) ? 'text-primary' : 'text-highlighted'"
                @click="toggleTeam(project.id)"
              >
                <UIcon v-if="filterTeams.includes(project.id)" name="i-lucide-check" class="h-3.5 w-3.5 shrink-0 text-primary" />
                <span v-else class="inline-block h-3.5 w-3.5 shrink-0" />
                {{ project.name }}
              </button>
            </div>
          </template>
        </UPopover>

        <!-- Quarter -->
        <UPopover :content="{ side: 'bottom', align: 'end', sideOffset: 8 }">
          <button class="flex items-center gap-1.5 rounded-lg border border-default bg-default px-3 py-1.5 text-sm transition-colors hover:border-primary/40">
            <UIcon name="i-lucide-calendar" class="h-3.5 w-3.5 shrink-0 text-muted" />
            <span class="truncate text-left text-highlighted">{{ quartersLabel }}</span>
            <UBadge v-if="filterQuarters.length" size="xs" color="primary" variant="solid" class="shrink-0">{{ filterQuarters.length }}</UBadge>
            <UIcon name="i-lucide-chevron-down" class="h-3.5 w-3.5 shrink-0 text-muted" />
          </button>
          <template #content>
            <div class="min-w-[260px]">
              <div class="flex flex-wrap items-center gap-1.5 border-b border-default px-3 py-2">
                <span class="text-[11px] font-medium text-muted">Anos:</span>
                <button
                  v-for="group in quarterYearOptions"
                  :key="`year-${group.year}`"
                  type="button"
                  class="rounded-full border px-2 py-0.5 text-[11px] font-medium transition-colors"
                  :class="isQuarterYearFullySelected(group.values) ? 'border-primary/50 bg-primary/10 text-primary' : 'border-default text-highlighted hover:border-primary/40'"
                  @click="toggleQuarterYear(group.values)"
                >
                  {{ group.year }}
                </button>
              </div>
              <div class="max-h-72 overflow-y-auto py-1">
                <button
                  class="flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors hover:bg-elevated"
                  :class="filterQuarters.length === 0 ? 'font-medium text-primary' : 'text-highlighted'"
                  @click="filterQuarters = []"
                >
                  <UIcon v-if="filterQuarters.length === 0" name="i-lucide-check" class="h-3.5 w-3.5 shrink-0" />
                  <span v-else class="inline-block h-3.5 w-3.5 shrink-0" />
                  Todos os quarters
                </button>
                <button
                  v-for="opt in quarterOptions"
                  :key="opt.value"
                  class="flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors hover:bg-elevated"
                  :class="filterQuarters.includes(opt.value) ? 'text-primary' : 'text-highlighted'"
                  @click="toggleQuarter(opt.value)"
                >
                  <UIcon v-if="filterQuarters.includes(opt.value)" name="i-lucide-check" class="h-3.5 w-3.5 shrink-0 text-primary" />
                  <span v-else class="inline-block h-3.5 w-3.5 shrink-0" />
                  {{ opt.label }}
                </button>
              </div>
            </div>
          </template>
        </UPopover>

        <UButton
          icon="i-lucide-download"
          label="Exportar CSV"
          color="neutral"
          variant="outline"
          size="sm"
          :disabled="!sortedRows.length"
          @click="exportCsv"
        />
      </div>
    </div>

    <!-- Relatório -->
    <div v-if="isLoading && !demands.length" class="flex items-center justify-center py-20">
      <UIcon name="i-lucide-loader-circle" class="h-6 w-6 animate-spin text-muted" />
    </div>

    <div v-else class="overflow-x-auto rounded-xl border border-default">
      <table class="w-full text-sm">
        <thead class="border-b border-default bg-elevated/40 text-left text-[11px] text-muted">
          <tr class="align-top">
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('epic')">
                Épico <UIcon :name="sortIcon('epic')" class="h-3 w-3" />
              </button>
              <input v-model="searchEpic" type="text" placeholder="filtrar…" class="mt-1 w-full rounded border border-default bg-default px-1.5 py-0.5 text-[11px] font-normal normal-case text-highlighted focus:border-primary/50 focus:outline-none">
            </th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('team')">
                Time <UIcon :name="sortIcon('team')" class="h-3 w-3" />
              </button>
              <div class="mt-1">
                <ColumnMultiFilter v-model="filterTeams" :options="teamOptions" all-label="Todos" />
              </div>
            </th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('quarter')">
                Quarter <UIcon :name="sortIcon('quarter')" class="h-3 w-3" />
              </button>
              <div class="mt-1">
                <ColumnMultiFilter v-model="filterQuarters" :options="quarterOptions" all-label="Todos" />
              </div>
            </th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('indicator')">
                Indicador <UIcon :name="sortIcon('indicator')" class="h-3 w-3" />
              </button>
              <div class="mt-1">
                <ColumnMultiFilter v-model="filterIndicators" :options="indicatorOptions" all-label="Todos" />
              </div>
            </th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('kpi')">
                KPI <UIcon :name="sortIcon('kpi')" class="h-3 w-3" />
              </button>
              <div class="mt-1">
                <ColumnMultiFilter v-model="filterKpis" :options="kpiFilterOptions" all-label="Todos" />
              </div>
            </th>
            <th class="px-3 py-2 text-right">
              <button type="button" class="flex w-full items-center justify-end gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('estimated')">
                Estimado <UIcon :name="sortIcon('estimated')" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2 text-right">
              <button type="button" class="flex w-full items-center justify-end gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('apurado')">
                Apurado <UIcon :name="sortIcon('apurado')" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('result')">
                Resultado <UIcon :name="sortIcon('result')" class="h-3 w-3" />
              </button>
              <div class="mt-1">
                <ColumnMultiFilter v-model="filterResults" :options="resultOptions" all-label="Todos" />
              </div>
            </th>
            <th class="px-3 py-2 text-right">
              <button type="button" class="flex w-full items-center justify-end gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('attainment')">
                % ating. <UIcon :name="sortIcon('attainment')" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('confidence')">
                Confiança <UIcon :name="sortIcon('confidence')" class="h-3 w-3" />
              </button>
              <div class="mt-1">
                <ColumnMultiFilter v-model="filterConfidence" :options="confidenceOptions" all-label="Todas" />
              </div>
            </th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('delivery')">
                Dt Entrega <UIcon :name="sortIcon('delivery')" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('date')">
                Dt Apuração <UIcon :name="sortIcon('date')" class="h-3 w-3" />
              </button>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!sortedRows.length">
            <td colspan="12" class="px-3 py-10 text-center text-sm text-muted">Nenhum KPI encontrado para os filtros selecionados.</td>
          </tr>
          <tr
            v-for="row in sortedRows"
            :key="row.key"
            class="cursor-pointer border-b border-default/60 transition-colors last:border-0 hover:bg-elevated"
            :title="`Abrir o workspace de KPIs de ${row.epicTitle}`"
            @click="openEpic(row.epicId)"
          >
            <td class="max-w-[15rem] truncate px-3 py-2 font-medium text-highlighted">{{ row.epicTitle }}</td>
            <td class="max-w-[10rem] truncate px-3 py-2 text-muted">{{ row.teamLabel }}</td>
            <td class="px-3 py-2 text-muted">{{ row.quarterLabel }}</td>
            <td class="px-3 py-2">
              <span class="rounded bg-elevated px-1.5 py-0.5 text-[11px] font-semibold text-muted">{{ row.indicatorLabel }}</span>
            </td>
            <td class="max-w-[13rem] truncate px-3 py-2 text-highlighted">{{ row.kpiName }}</td>
            <td class="px-3 py-2 text-right tabular-nums text-highlighted">{{ formatKpiValue(row.estimated, row.unit) }}</td>
            <td class="px-3 py-2 text-right tabular-nums text-highlighted">{{ formatKpiValue(row.apurado, row.unit) }}</td>
            <td class="px-3 py-2">
              <span class="inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-semibold" :class="resultToneClass(row.result)">{{ resultLabel(row.result) }}</span>
            </td>
            <td class="px-3 py-2 text-right tabular-nums" :class="row.result === 'Negative' ? 'text-red-600 dark:text-red-400' : row.result === 'Positive' ? 'text-green-600 dark:text-green-400' : 'text-muted'">
              {{ row.result != null ? formatKpiAttainment(row.attainment) : '—' }}
            </td>
            <td class="px-3 py-2 text-muted">{{ CONFIDENCE_LABELS[row.confidence] }}</td>
            <td class="px-3 py-2 text-muted">{{ formatKpiDate(row.deliveryDate) }}</td>
            <td class="px-3 py-2 text-muted">{{ formatKpiDate(row.date) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
