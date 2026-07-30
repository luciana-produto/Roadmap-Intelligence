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
import { buildPlanningDashboardUrl } from '~/utils/roadmapDashboardLink'
import type { DashboardSelection } from '~/types/roadmapDashboards'
import KpiApuracaoDashboards from '~/components/roadmap/KpiApuracaoDashboards.vue'

useSeoMeta({ title: 'Dashboard KPIs · ProductHub' })

const roadmapStore = useRoadmapStore()
const kpiStore = useKpiStore()
const { projects, demands, isLoading, selectedProjectId, selectedQuarterYear, selectedQuarterNumber } = storeToRefs(roadmapStore)
const { kpis } = storeToRefs(kpiStore)

const now = new Date()
const currentYear = now.getFullYear()

// Default: todos os times (vazio = todos) + os 4 quarters do ano atual.
const filterTeams = ref<string[]>([])
const filterQuarters = ref<string[]>([1, 2, 3, 4].map(q => buildQuarterValue(currentYear, q)))

onMounted(async () => {
  // Carrega TODOS os times e quarters (filtragem é feita no cliente), como na home.
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

// ─── Opções de quarter ──────────────────────────────────────────────────────────
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

// ─── Escopo (mesma regra da home: demandas + épicos simples) ─────────────────────
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

// Totalizadores: abre uma nova aba com a planejamento filtrada pelo problema clicado.
function handleSelect(selection: DashboardSelection) {
  const url = buildPlanningDashboardUrl({
    teams: filterTeams.value,
    quarters: filterQuarters.value,
    selection
  })
  window.open(url, '_blank')
}

// Comparativo/resultados: abre o workspace de KPIs do épico numa nova aba.
function handleOpenEpic(epicId: string) {
  const epic = demands.value.find(item => item.id === epicId)
  const projectId = epic?.projectId ?? epic?.projectIds?.[0] ?? ''
  const params = new URLSearchParams({ kpiDemandId: epicId })
  if (projectId)
    params.set('projectId', projectId)
  window.open(`/roadmap?${params.toString()}`, '_blank')
}
</script>

<template>
  <div class="space-y-5">
    <!-- Cabeçalho + filtros -->
    <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">Dashboard de KPIs</h1>
        <p class="text-sm text-muted mt-1">
          Apuração dos KPIs por épico. Clique nos totalizadores para ver os itens no roadmap, ou num épico para abrir seu workspace de KPIs.
        </p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <!-- Atalho para o dashboard da home -->
        <UButton
          to="/home"
          icon="i-lucide-trending-up"
          label="Dashboard Roadmap"
          color="primary"
          variant="soft"
          size="sm"
        />

        <!-- Times -->
        <UPopover :content="{ side: 'bottom', align: 'end', sideOffset: 8 }">
          <button class="flex items-center gap-1.5 rounded-lg border border-default bg-default px-3 py-1.5 text-sm transition-colors hover:border-primary/40">
            <UIcon name="i-lucide-users" class="w-3.5 h-3.5 shrink-0 text-muted" />
            <span class="text-left truncate text-highlighted">{{ teamsLabel }}</span>
            <UBadge v-if="filterTeams.length" size="xs" color="primary" variant="solid" class="shrink-0">{{ filterTeams.length }}</UBadge>
            <UIcon name="i-lucide-chevron-down" class="w-3.5 h-3.5 shrink-0 text-muted" />
          </button>
          <template #content>
            <div class="py-1 min-w-[240px] max-h-72 overflow-y-auto">
              <button
                class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                :class="filterTeams.length === 0 ? 'text-primary font-medium' : 'text-highlighted'"
                @click="filterTeams = []"
              >
                <UIcon v-if="filterTeams.length === 0" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0" />
                <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                Todos os times
              </button>
              <button
                v-for="project in sortedProjects"
                :key="project.id"
                class="w-full text-left px-3 py-2 text-sm flex items-center gap-2 hover:bg-elevated transition-colors"
                :class="filterTeams.includes(project.id) ? 'text-primary' : 'text-highlighted'"
                @click="toggleTeam(project.id)"
              >
                <UIcon v-if="filterTeams.includes(project.id)" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0 text-primary" />
                <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                {{ project.name }}
              </button>
            </div>
          </template>
        </UPopover>

        <!-- Quarter -->
        <UPopover :content="{ side: 'bottom', align: 'end', sideOffset: 8 }">
          <button class="flex items-center gap-1.5 rounded-lg border border-default bg-default px-3 py-1.5 text-sm transition-colors hover:border-primary/40">
            <UIcon name="i-lucide-calendar" class="w-3.5 h-3.5 shrink-0 text-muted" />
            <span class="text-left truncate text-highlighted">{{ quartersLabel }}</span>
            <UBadge v-if="filterQuarters.length" size="xs" color="primary" variant="solid" class="shrink-0">{{ filterQuarters.length }}</UBadge>
            <UIcon name="i-lucide-chevron-down" class="w-3.5 h-3.5 shrink-0 text-muted" />
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
                  @click="toggleQuarter(opt.value)"
                >
                  <UIcon v-if="filterQuarters.includes(opt.value)" name="i-lucide-check" class="w-3.5 h-3.5 shrink-0 text-primary" />
                  <span v-else class="inline-block w-3.5 h-3.5 shrink-0" />
                  {{ opt.label }}
                </button>
              </div>
            </div>
          </template>
        </UPopover>
      </div>
    </div>

    <!-- Dashboards -->
    <div v-if="isLoading && !demands.length" class="flex items-center justify-center py-20">
      <UIcon name="i-lucide-loader-circle" class="h-6 w-6 animate-spin text-muted" />
    </div>
    <KpiApuracaoDashboards
      v-else
      :demands="scopedDemands"
      :all-demands="demands"
      :kpis="kpis"
      @select="handleSelect"
      @open-epic="handleOpenEpic"
    />
  </div>
</template>
