<script setup lang="ts">
import type { RoadmapDemand, Kpi, KpiUnit, KpiIndicator, KpiOperation, KpiMeasurement, MeasurementResult, ConfidenceLevel } from '~/types/roadmap'
import type { DashboardSelection } from '~/types/roadmapDashboards'
import KpiImpactBarChart from '~/components/roadmap/KpiImpactBarChart.vue'
import KpiNameFilterPopover from '~/components/roadmap/KpiNameFilterPopover.vue'

const props = defineProps<{
  // Demandas já filtradas por time/quarter (demandas + épicos simples) — base dos totalizadores.
  demands: RoadmapDemand[]
  // Todas as demandas carregadas — necessário para resolver os épicos (donos dos KPIs).
  allDemands: RoadmapDemand[]
  // Catálogo de KPIs — necessário para mapear kpiId → indicador.
  kpis: Kpi[]
}>()

const emit = defineEmits<{
  select: [selection: DashboardSelection]
  openEpic: [epicId: string]
}>()

const kpiById = computed(() => new Map(props.kpis.map(kpi => [kpi.id, kpi] as const)))

// ─── Formatação ──────────────────────────────────────────────────────────────
function formatDate(value: string | null | undefined): string {
  if (!value)
    return '—'
  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day)
    return value
  return new Intl.DateTimeFormat('pt-BR').format(new Date(year, month - 1, day))
}

function formatValue(value: number | null | undefined, unit: KpiUnit): string {
  if (value == null)
    return '—'
  if (unit === 'Currency') {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', maximumFractionDigits: 0 }).format(value)
  }
  const formatted = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value)
  if (unit === 'Percentage')
    return `${formatted}%`
  if (unit === 'TimeSeconds')
    return `${formatted}s`
  return formatted
}

function formatAttainment(value: number | null): string {
  if (value == null || !Number.isFinite(value))
    return '—'
  return `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 }).format(value)}%`
}

const kpiIndicatorLabels: Record<KpiIndicator, string> = {
  Mrr: 'MRR', Stores: 'Lojas', Time: 'Tempo', Clicks: 'Cliques', StepsScreens: 'Etapas/Telas'
}
const confidenceLabels: Record<ConfidenceLevel, string> = {
  High: 'Alta', Medium: 'Média', Low: 'Baixa'
}

// % de atingimento da meta, orientado pela operação do KPI:
// "Quanto maior melhor" => apurado / meta; "Quanto menor melhor" => meta / apurado.
// >100% significa que superou a meta. Retorna null quando não dá para calcular.
function attainmentPct(operation: KpiOperation, estimated: number | null, apurado: number | null): number | null {
  if (estimated == null || apurado == null)
    return null
  if (operation === 'LowerIsBetter') {
    if (apurado === 0)
      return estimated === 0 ? 100 : Infinity
    return (estimated / apurado) * 100
  }
  if (estimated === 0)
    return apurado === 0 ? 100 : Infinity
  return (apurado / estimated) * 100
}

// ─── Épicos em escopo ─────────────────────────────────────────────────────────
// Todos os épicos donos das demandas do escopo (independente do status de KPI).
const allScopedEpics = computed(() => {
  const ids = new Set<string>()
  for (const d of props.demands) {
    if (d.itemType === 'Epic')
      ids.add(d.id)
    else if (d.epicId)
      ids.add(d.epicId)
  }
  return props.allDemands.filter(e => e.itemType === 'Epic' && ids.has(e.id))
})

// Só os épicos QUE TÊM KPI (base dos rankings/resultados).
const scopedEpics = computed(() =>
  allScopedEpics.value.filter(e => !e.hasNoKpi && e.kpiLinks.length > 0)
)

// ─── Cobertura de KPI dos épicos ─────────────────────────────────────────────
// comKpi: tem ≥1 KPI · naoAplicavel: marcado como "sem KPI" (hasNoKpi) · pendente: falta associar.
type KpiCoverage = 'comKpi' | 'pendente' | 'naoAplicavel'
function epicKpiCoverage(epic: RoadmapDemand): KpiCoverage {
  if (epic.hasNoKpi)
    return 'naoAplicavel'
  if (epic.kpiLinks.length > 0)
    return 'comKpi'
  return 'pendente'
}
function pct(count: number, total: number): number {
  return total > 0 ? (count / total) * 100 : 0
}

const coverageCounts = computed(() => {
  let comKpi = 0
  let pendente = 0
  let naoAplicavel = 0
  for (const epic of allScopedEpics.value) {
    const coverage = epicKpiCoverage(epic)
    if (coverage === 'comKpi') comKpi++
    else if (coverage === 'pendente') pendente++
    else naoAplicavel++
  }
  return { comKpi, pendente, naoAplicavel, total: allScopedEpics.value.length }
})

// Concluídos sem apuração (épicos): status Done, com KPI e sem nenhuma medição.
const doneNoApuracaoCount = computed(() =>
  allScopedEpics.value.filter(e =>
    e.status === 'Done' && !e.hasNoKpi && e.kpiLinks.length > 0 && (e.kpiMeasurements?.length ?? 0) === 0
  ).length
)

// Distribuição da cobertura (para o dashboard "Cobertura de KPI").
const coverageDistribution = computed(() => {
  const { comKpi, pendente, naoAplicavel, total } = coverageCounts.value
  return [
    { key: 'comKpi', label: 'Com KPI', count: comKpi, percentage: pct(comKpi, total), bar: 'bg-emerald-500', dot: 'bg-emerald-500 dark:bg-emerald-400' },
    { key: 'pendente', label: 'KPI pendente', count: pendente, percentage: pct(pendente, total), bar: 'bg-amber-500', dot: 'bg-amber-500 dark:bg-amber-400' },
    { key: 'naoAplicavel', label: 'KPI não aplicável', count: naoAplicavel, percentage: pct(naoAplicavel, total), bar: 'bg-slate-400', dot: 'bg-slate-400 dark:bg-slate-500' }
  ]
})

// Classificação dos épicos marcados como "sem KPI" (não aplicável).
const noKpiClassificationLabels: Record<string, string> = {
  Relationship: 'Relacionamento', Mandatory: 'Mandatório', Technical: 'Técnico'
}
const noKpiClassificationTotals = computed(() => {
  const map = new Map<string, number>()
  let total = 0
  for (const epic of allScopedEpics.value) {
    if (!epic.hasNoKpi)
      continue
    total++
    const key = epic.noKpiClassification ?? 'Unclassified'
    map.set(key, (map.get(key) ?? 0) + 1)
  }
  return {
    total,
    items: [...map.entries()]
      .map(([key, count]) => ({ key, label: noKpiClassificationLabels[key] ?? 'Sem classificação', count, percentage: pct(count, total) }))
      .sort((a, b) => b.count - a.count)
  }
})

function latestMeasurement(epic: RoadmapDemand, kpiId: string): KpiMeasurement | null {
  let best: KpiMeasurement | null = null
  for (const m of epic.kpiMeasurements ?? []) {
    if (m.kpiId !== kpiId)
      continue
    if (!best
      || m.measurementDate > best.measurementDate
      || (m.measurementDate === best.measurementDate && m.createdAt > best.createdAt)) {
      best = m
    }
  }
  return best
}

// ─── Ranking de impacto por indicador (maior valor apurado) ──────────────────
// O foco é "qual épico teve maior impacto" no indicador, independente da meta.
type RankingRow = {
  key: string
  epicId: string
  epicTitle: string
  kpiName: string
  unit: KpiUnit
  impact: number | null
  impactDate: string | null
}

function rankingRows(indicator: KpiIndicator, selectedKpiIds: string[]): RankingRow[] {
  const useAll = selectedKpiIds.length === 0
  const rows: RankingRow[] = []
  for (const epic of scopedEpics.value) {
    for (const link of epic.kpiLinks) {
      const kpi = kpiById.value.get(link.kpiId)
      if (!kpi || kpi.indicator !== indicator)
        continue
      if (!useAll && !selectedKpiIds.includes(link.kpiId))
        continue
      const measurement = latestMeasurement(epic, link.kpiId)
      rows.push({
        key: `${epic.id}:${link.kpiId}`,
        epicId: epic.id,
        epicTitle: epic.title,
        kpiName: link.kpiName,
        unit: link.unit,
        impact: measurement?.measuredValue ?? null,
        impactDate: measurement?.measurementDate ?? null
      })
    }
  }
  // Maior impacto primeiro; itens ainda sem apuração vão para o fim.
  return rows.sort((a, b) => {
    if (a.impact == null && b.impact == null)
      return a.epicTitle.localeCompare(b.epicTitle, 'pt-BR')
    if (a.impact == null)
      return 1
    if (b.impact == null)
      return -1
    return b.impact - a.impact
  })
}

// Filtros de KPI por card (dentro de cada dashboard). Vazio = todos.
const storesKpiFilter = ref<string[]>([])
const mrrKpiFilter = ref<string[]>([])

const storesRanking = computed(() => rankingRows('Stores', storesKpiFilter.value))
const mrrRanking = computed(() => rankingRows('Mrr', mrrKpiFilter.value))

// Opções de KPI disponíveis por indicador (apenas os presentes no escopo atual).
function kpiOptionsForIndicator(indicator: KpiIndicator): { id: string, name: string }[] {
  const byId = new Map<string, string>()
  for (const epic of scopedEpics.value) {
    for (const link of epic.kpiLinks) {
      const kpi = kpiById.value.get(link.kpiId)
      if (kpi && kpi.indicator === indicator)
        byId.set(link.kpiId, link.kpiName)
    }
  }
  return [...byId.entries()]
    .map(([id, name]) => ({ id, name }))
    .sort((a, b) => a.name.localeCompare(b.name, 'pt-BR'))
}
const storesKpiOptions = computed(() => kpiOptionsForIndicator('Stores'))
const mrrKpiOptions = computed(() => kpiOptionsForIndicator('Mrr'))


// ─── Resultados positivos / negativos (última apuração de cada KPI) ──────────
type ResultRow = {
  key: string
  epicId: string
  epicTitle: string
  kpiId: string
  kpiName: string
  indicatorLabel: string
  unit: KpiUnit
  estimated: number | null
  apurado: number | null
  confidence: ConfidenceLevel
  date: string | null
  attainment: number | null
}

// Resultado calculado AO VIVO a partir da operação do KPI + meta + apurado
// (mesma regra do workspace do épico). NÃO usa o measurement.result armazenado,
// que pode estar defasado de uma apuração anterior.
function liveResult(operation: KpiOperation, estimated: number | null, apurado: number | null): MeasurementResult {
  if (estimated == null || apurado == null)
    return 'Neutral'
  if (operation === 'LowerIsBetter')
    return apurado <= estimated ? 'Positive' : 'Negative'
  return apurado >= estimated ? 'Positive' : 'Negative'
}

function resultRows(target: MeasurementResult): ResultRow[] {
  const rows: ResultRow[] = []
  for (const epic of scopedEpics.value) {
    for (const link of epic.kpiLinks) {
      const kpi = kpiById.value.get(link.kpiId)
      if (!kpi)
        continue
      // Considera apenas a última apuração de cada KPI.
      const measurement = latestMeasurement(epic, link.kpiId)
      if (!measurement)
        continue
      const estimated = link.estimatedImpact ?? null
      if (liveResult(kpi.operation, estimated, measurement.measuredValue) !== target)
        continue
      rows.push({
        key: `${epic.id}:${link.kpiId}`,
        epicId: epic.id,
        epicTitle: epic.title,
        kpiId: link.kpiId,
        kpiName: link.kpiName,
        indicatorLabel: kpiIndicatorLabels[kpi.indicator],
        unit: link.unit,
        estimated,
        apurado: measurement.measuredValue,
        confidence: link.confidenceLevel,
        date: measurement.measurementDate,
        attainment: attainmentPct(kpi.operation, estimated, measurement.measuredValue)
      })
    }
  }
  // Positivos: maior % de atingimento primeiro. Negativos: pior (menor %) primeiro.
  // Linhas sem % calculável vão para o fim.
  const direction = target === 'Positive' ? -1 : 1
  return rows.sort((a, b) => {
    if (a.attainment == null && b.attainment == null)
      return 0
    if (a.attainment == null)
      return 1
    if (b.attainment == null)
      return -1
    return (a.attainment - b.attainment) * direction
  })
}

// Filtros de KPI por card de resultado (vazio = todos).
const positiveKpiFilter = ref<string[]>([])
const negativeKpiFilter = ref<string[]>([])

const positiveRowsAll = computed(() => resultRows('Positive'))
const negativeRowsAll = computed(() => resultRows('Negative'))

function kpiOptionsFromRows(rows: ResultRow[]): { id: string, name: string }[] {
  const byId = new Map<string, string>()
  for (const row of rows)
    byId.set(row.kpiId, row.kpiName)
  return [...byId.entries()]
    .map(([id, name]) => ({ id, name }))
    .sort((a, b) => a.name.localeCompare(b.name, 'pt-BR'))
}
const positiveKpiOptions = computed(() => kpiOptionsFromRows(positiveRowsAll.value))
const negativeKpiOptions = computed(() => kpiOptionsFromRows(negativeRowsAll.value))

const positiveRows = computed(() =>
  positiveKpiFilter.value.length
    ? positiveRowsAll.value.filter(r => positiveKpiFilter.value.includes(r.kpiId))
    : positiveRowsAll.value
)
const negativeRows = computed(() =>
  negativeKpiFilter.value.length
    ? negativeRowsAll.value.filter(r => negativeKpiFilter.value.includes(r.kpiId))
    : negativeRowsAll.value
)
</script>

<template>
  <div class="space-y-4">
    <!-- Totalizadores (épicos) -->
    <div class="grid gap-4 sm:grid-cols-3">
      <UCard class="ring-default" :ui="{ body: 'p-3.5' }">
        <div class="flex items-center gap-2">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-amber-50 text-amber-600 dark:bg-amber-900/20 dark:text-amber-300">
            <UIcon name="i-lucide-target" class="h-4.5 w-4.5" />
          </div>
          <p class="text-sm font-semibold text-highlighted">KPI pendente</p>
        </div>
        <div class="mt-3 p-1.5">
          <p class="text-2xl font-bold leading-none text-amber-600 dark:text-amber-400">{{ coverageCounts.pendente.toLocaleString('pt-BR') }}</p>
          <p class="mt-1 text-[11px] leading-tight text-muted">épicos sem KPI — falta associar</p>
        </div>
      </UCard>

      <UCard class="ring-default" :ui="{ body: 'p-3.5' }">
        <div class="flex items-center gap-2">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-slate-100 text-slate-600 dark:bg-slate-800/40 dark:text-slate-300">
            <UIcon name="i-lucide-circle-slash" class="h-4.5 w-4.5" />
          </div>
          <p class="text-sm font-semibold text-highlighted">KPI não aplicável</p>
        </div>
        <div class="mt-3 p-1.5">
          <p class="text-2xl font-bold leading-none text-slate-600 dark:text-slate-300">{{ coverageCounts.naoAplicavel.toLocaleString('pt-BR') }}</p>
          <p class="mt-1 text-[11px] leading-tight text-muted">épicos marcados como sem KPI (justificado)</p>
        </div>
      </UCard>

      <UCard class="ring-default" :ui="{ body: 'p-3.5' }">
        <div class="flex items-center gap-2">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-pink-50 text-pink-600 dark:bg-pink-900/20 dark:text-pink-300">
            <UIcon name="i-lucide-clipboard-x" class="h-4.5 w-4.5" />
          </div>
          <p class="text-sm font-semibold text-highlighted">Concluídos sem apuração</p>
        </div>
        <button
          type="button"
          class="mt-3 block w-full rounded-md p-1.5 text-left transition-colors hover:bg-elevated"
          title="Abrir no roadmap os concluídos sem apuração de KPI"
          @click="emit('select', { kind: 'problem', value: 'doneNoKpi' })"
        >
          <p class="text-2xl font-bold leading-none text-pink-600 dark:text-pink-400">{{ doneNoApuracaoCount.toLocaleString('pt-BR') }}</p>
          <p class="mt-1 text-[11px] leading-tight text-muted">épicos concluídos sem KPI apurado</p>
        </button>
      </UCard>
    </div>

    <!-- Cobertura de KPI + Classificação dos sem KPI -->
    <div class="grid items-stretch gap-4 xl:grid-cols-2">
      <UCard class="flex flex-col ring-default" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-50 text-emerald-600 dark:bg-emerald-900/20 dark:text-emerald-300">
            <UIcon name="i-lucide-pie-chart" class="h-4.5 w-4.5" />
          </div>
          <p class="flex-1 text-sm font-semibold text-highlighted">Cobertura de KPI</p>
          <span class="shrink-0 text-[11px] text-muted">{{ coverageCounts.total }} épicos</span>
        </div>
        <div v-if="coverageCounts.total" class="space-y-2.5 px-3.5 py-3">
          <div v-for="item in coverageDistribution" :key="item.key">
            <div class="flex items-center gap-2">
              <span class="h-2.5 w-2.5 shrink-0 rounded-full" :class="item.dot" />
              <span class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.label }}</span>
              <span class="shrink-0 text-xs font-semibold text-highlighted">{{ item.percentage.toFixed(1) }}%</span>
              <span class="shrink-0 rounded-full bg-elevated px-1.5 py-0.5 text-[11px] text-muted">{{ item.count }} ép.</span>
            </div>
            <div class="mt-1 h-1.5 overflow-hidden rounded-full bg-elevated">
              <div class="h-full rounded-full transition-all duration-300" :class="item.bar" :style="{ width: `${Math.min(item.percentage, 100)}%` }" />
            </div>
          </div>
        </div>
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhum épico no escopo selecionado.</div>
      </UCard>

      <UCard class="flex flex-col ring-default" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-slate-100 text-slate-600 dark:bg-slate-800/40 dark:text-slate-300">
            <UIcon name="i-lucide-tags" class="h-4.5 w-4.5" />
          </div>
          <p class="flex-1 text-sm font-semibold text-highlighted">Classificação — KPI não aplicável</p>
          <span class="shrink-0 text-[11px] text-muted">{{ noKpiClassificationTotals.total }} épicos</span>
        </div>
        <div v-if="noKpiClassificationTotals.items.length" class="space-y-2.5 px-3.5 py-3">
          <div v-for="item in noKpiClassificationTotals.items" :key="item.key">
            <div class="flex items-center gap-2">
              <span class="flex-1 truncate text-sm font-medium text-highlighted">{{ item.label }}</span>
              <span class="shrink-0 text-xs font-semibold text-highlighted">{{ item.percentage.toFixed(1) }}%</span>
              <span class="shrink-0 rounded-full bg-elevated px-1.5 py-0.5 text-[11px] text-muted">{{ item.count }} ép.</span>
            </div>
            <div class="mt-1 h-1.5 overflow-hidden rounded-full bg-elevated">
              <div class="h-full rounded-full bg-slate-400 transition-all duration-300 dark:bg-slate-500" :style="{ width: `${Math.min(item.percentage, 100)}%` }" />
            </div>
          </div>
        </div>
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhum épico marcado como "sem KPI" no escopo.</div>
      </UCard>
    </div>

    <div class="grid items-stretch gap-4 xl:grid-cols-2">
      <!-- Ranking de impacto: Lojas -->
      <UCard class="flex h-full flex-col ring-default xl:h-[26rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-indigo-50 text-indigo-600 dark:bg-indigo-900/20 dark:text-indigo-300">
            <UIcon name="i-lucide-store" class="h-4.5 w-4.5" />
          </div>
          <div>
            <p class="text-sm font-semibold text-highlighted">Maior impacto · Lojas</p>
            <p class="text-[11px] text-muted">Épicos ordenados pelo último valor apurado</p>
          </div>
          <KpiNameFilterPopover
            v-if="storesKpiOptions.length"
            v-model="storesKpiFilter"
            :options="storesKpiOptions"
            class="ml-auto"
          />
        </div>
        <KpiImpactBarChart
          v-if="storesRanking.length"
          :rows="storesRanking"
          variant="indigo"
          class="min-h-0 flex-1"
          @open-epic="emit('openEpic', $event)"
        />
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhum épico com KPI do indicador Lojas no escopo selecionado.</div>
      </UCard>

      <!-- Ranking de impacto: MRR -->
      <UCard class="flex h-full flex-col ring-default xl:h-[26rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-50 text-emerald-600 dark:bg-emerald-900/20 dark:text-emerald-300">
            <UIcon name="i-lucide-dollar-sign" class="h-4.5 w-4.5" />
          </div>
          <div>
            <p class="text-sm font-semibold text-highlighted">Maior impacto · MRR</p>
            <p class="text-[11px] text-muted">Épicos ordenados pelo último valor apurado</p>
          </div>
          <KpiNameFilterPopover
            v-if="mrrKpiOptions.length"
            v-model="mrrKpiFilter"
            :options="mrrKpiOptions"
            class="ml-auto"
          />
        </div>
        <KpiImpactBarChart
          v-if="mrrRanking.length"
          :rows="mrrRanking"
          variant="emerald"
          class="min-h-0 flex-1"
          @open-epic="emit('openEpic', $event)"
        />
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhum épico com KPI do indicador MRR no escopo selecionado.</div>
      </UCard>

      <!-- Resultados positivos -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-green-50 text-green-600 dark:bg-green-900/20 dark:text-green-300">
            <UIcon name="i-lucide-trending-up" class="h-4.5 w-4.5" />
          </div>
          <div>
            <p class="text-sm font-semibold text-highlighted">Resultados positivos</p>
            <p class="text-[11px] text-muted">{{ positiveRows.length }} {{ positiveRows.length === 1 ? 'resultado' : 'resultados' }} · Impacto estimado × Apurado</p>
          </div>
          <KpiNameFilterPopover
            v-if="positiveKpiOptions.length"
            v-model="positiveKpiFilter"
            :options="positiveKpiOptions"
            class="ml-auto"
          />
        </div>
        <div v-if="positiveRows.length" class="min-h-0 flex-1 space-y-2 overflow-y-auto px-3.5 py-3.5">
          <button
            v-for="row in positiveRows"
            :key="row.key"
            type="button"
            class="block w-full rounded-lg border border-default bg-elevated/20 px-2.5 py-2 text-left transition-colors hover:bg-elevated"
            :title="`Abrir o workspace de KPIs de ${row.epicTitle}`"
            @click="emit('openEpic', row.epicId)"
          >
            <div class="flex items-center gap-2">
              <span class="shrink-0 rounded bg-elevated px-1.5 py-0.5 text-[10px] font-semibold text-muted">{{ row.indicatorLabel }}</span>
              <p class="flex-1 truncate text-sm font-medium text-highlighted">{{ row.epicTitle }}</p>
              <span class="shrink-0 text-sm font-bold text-green-600 dark:text-green-400">{{ formatAttainment(row.attainment) }}</span>
            </div>
            <div class="mt-1 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-[11px] text-muted">
              <span>Estimado: <span class="font-semibold text-highlighted">{{ formatValue(row.estimated, row.unit) }}</span></span>
              <span>Apurado: <span class="font-semibold text-highlighted">{{ formatValue(row.apurado, row.unit) }}</span></span>
              <span>Confiança: <span class="font-semibold text-highlighted">{{ confidenceLabels[row.confidence] }}</span></span>
              <span class="ml-auto shrink-0">{{ formatDate(row.date) }}</span>
            </div>
          </button>
        </div>
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhum KPI com resultado positivo na última apuração.</div>
      </UCard>

      <!-- Resultados negativos -->
      <UCard class="flex h-full flex-col ring-default xl:h-[24rem]" :ui="{ body: 'p-0 h-full flex flex-col min-h-0' }">
        <div class="-mt-1 flex items-center gap-2 border-b border-default px-2.5 py-1.5">
          <div class="flex h-7 w-7 items-center justify-center rounded-lg bg-red-50 text-red-600 dark:bg-red-900/20 dark:text-red-300">
            <UIcon name="i-lucide-trending-down" class="h-4.5 w-4.5" />
          </div>
          <div>
            <p class="text-sm font-semibold text-highlighted">Resultados negativos</p>
            <p class="text-[11px] text-muted">{{ negativeRows.length }} {{ negativeRows.length === 1 ? 'resultado' : 'resultados' }} · Impacto estimado × Apurado</p>
          </div>
          <KpiNameFilterPopover
            v-if="negativeKpiOptions.length"
            v-model="negativeKpiFilter"
            :options="negativeKpiOptions"
            class="ml-auto"
          />
        </div>
        <div v-if="negativeRows.length" class="min-h-0 flex-1 space-y-2 overflow-y-auto px-3.5 py-3.5">
          <button
            v-for="row in negativeRows"
            :key="row.key"
            type="button"
            class="block w-full rounded-lg border border-default bg-elevated/20 px-2.5 py-2 text-left transition-colors hover:bg-elevated"
            :title="`Abrir o workspace de KPIs de ${row.epicTitle}`"
            @click="emit('openEpic', row.epicId)"
          >
            <div class="flex items-center gap-2">
              <span class="shrink-0 rounded bg-elevated px-1.5 py-0.5 text-[10px] font-semibold text-muted">{{ row.indicatorLabel }}</span>
              <p class="flex-1 truncate text-sm font-medium text-highlighted">{{ row.epicTitle }}</p>
              <span class="shrink-0 text-sm font-bold text-red-600 dark:text-red-400">{{ formatAttainment(row.attainment) }}</span>
            </div>
            <div class="mt-1 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-[11px] text-muted">
              <span>Estimado: <span class="font-semibold text-highlighted">{{ formatValue(row.estimated, row.unit) }}</span></span>
              <span>Apurado: <span class="font-semibold text-highlighted">{{ formatValue(row.apurado, row.unit) }}</span></span>
              <span>Confiança: <span class="font-semibold text-highlighted">{{ confidenceLabels[row.confidence] }}</span></span>
              <span class="ml-auto shrink-0">{{ formatDate(row.date) }}</span>
            </div>
          </button>
        </div>
        <div v-else class="px-3.5 py-5 text-sm text-muted">Nenhum KPI com resultado negativo na última apuração.</div>
      </UCard>
    </div>
  </div>
</template>
