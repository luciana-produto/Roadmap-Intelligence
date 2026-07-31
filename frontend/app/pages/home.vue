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
import { buildPlanningDashboardUrl, buildReasonReportUrl } from '~/utils/roadmapDashboardLink'
import type { DashboardSelection } from '~/types/roadmapDashboards'
import RoadmapDashboards from '~/components/roadmap/RoadmapDashboards.vue'

useSeoMeta({ title: 'Dashboard · ProductHub' })

const authStore = useAuthStore()
const roadmapStore = useRoadmapStore()
const route = useRoute()
const { projects, demands, isLoading, selectedProjectId, selectedQuarterYear, selectedQuarterNumber } = storeToRefs(roadmapStore)

const now = new Date()
const currentYear = now.getFullYear()

// Default: todos os times (vazio = todos) + os 4 quarters do ano atual.
const filterTeams = ref<string[]>([])
const filterQuarters = ref<string[]>([1, 2, 3, 4].map(q => buildQuarterValue(currentYear, q)))

onMounted(async () => {
  // Garante que a home carregue TODOS os times e quarters (filtragem é feita no cliente).
  selectedProjectId.value = null
  selectedQuarterYear.value = null
  selectedQuarterNumber.value = null
  await roadmapStore.fetchProjects()
  await roadmapStore.fetchDemands()

  // Filtros vindos do atalho "Dashboard completo" do roadmap (?teams=&quarters=).
  const queryTeams = typeof route.query.teams === 'string' ? route.query.teams : null
  const queryQuarters = typeof route.query.quarters === 'string' ? route.query.quarters : null
  if (queryTeams !== null) {
    filterTeams.value = queryTeams.split(',').map(v => v.trim())
      .filter(Boolean)
      .filter(id => projects.value.some(p => p.id === id))
  }
  if (queryQuarters !== null)
    filterQuarters.value = queryQuarters.split(',').map(v => v.trim()).filter(Boolean)
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

// ─── Escopo (mesma regra da planejamento: demandas + épicos simples) ─────────────
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

// Ao clicar num item do dashboard, abre uma nova aba com a planejamento filtrada.
function handleSelect(selection: DashboardSelection) {
  const url = buildPlanningDashboardUrl({
    teams: filterTeams.value,
    quarters: filterQuarters.value,
    selection
  })
  window.open(url, '_blank')
}

function handleReport(tipo: 'atraso-transbordo' | 'deprioritization') {
  const url = buildReasonReportUrl({ tipo, teams: filterTeams.value, quarters: filterQuarters.value })
  window.open(url, '_blank')
}
</script>

<template>
  <div class="space-y-5">
    <!-- Cabeçalho + filtros -->
    <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">
          Olá, {{ authStore.user?.firstName ?? 'usuário' }} 👋
        </h1>
        <p class="text-sm text-muted mt-1">
          Visão geral do roadmap. Clique em qualquer item do dashboard para abrir a planejamento filtrada em uma nova aba.
        </p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <!-- Atalho para o Dashboard de KPIs -->
        <UButton
          to="/dashboard-kpis"
          icon="i-lucide-trending-up"
          label="Dashboard de KPIs"
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
    <RoadmapDashboards
      v-else
      :demands="scopedDemands"
      :all-demands="demands"
      @select="handleSelect"
      @report="handleReport"
    />
  </div>
</template>
