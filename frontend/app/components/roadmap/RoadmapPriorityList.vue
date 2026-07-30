<script setup lang="ts">
import type { RoadmapDemand, Kpi } from '~/types/roadmap'
import { formatKpiDate } from '~/utils/kpiApuracao'
import ColumnMultiFilter from '~/components/roadmap/ColumnMultiFilter.vue'
import { useRoadmapPriority, statusTone, confDot, fmtScore } from '~/composables/useRoadmapPriority'
import type { PriorityRow } from '~/composables/useRoadmapPriority'

const props = defineProps<{
  demands: RoadmapDemand[]
  allDemands: RoadmapDemand[]
  kpis: Kpi[]
}>()

const emit = defineEmits<{ openEpic: [epicId: string], editEpic: [epicId: string] }>()

// Regras (score/esforço/quarter/matriz) vêm do composable — fonte única compartilhada com a Matriz.
const { rows } = useRoadmapPriority(toRef(props, 'demands'), toRef(props, 'allDemands'), toRef(props, 'kpis'))

// ─── Filtro de status ────────────────────────────────────────────────────────
const statusFilter = ref<string[]>([])
const statusOptions = computed(() => {
  const seen = new Map<string, string>()
  for (const r of rows.value) seen.set(r.status, r.statusLabel)
  return [...seen.entries()]
    .map(([value, label]) => ({ value, label }))
    .sort((a, b) => a.label.localeCompare(b.label, 'pt-BR'))
})
const filteredRows = computed(() =>
  statusFilter.value.length ? rows.value.filter(r => statusFilter.value.includes(r.status)) : rows.value
)

// ─── Ordenação ────────────────────────────────────────────────────────────────
type SortKey = 'epic' | 'status' | 'quarter' | 'promised' | 'effort' | 'score' | 'quadrant'
const sortKey = ref<SortKey>('score')
const sortDir = ref<'asc' | 'desc'>('desc')

function toggleSort(key: SortKey) {
  if (sortKey.value === key) sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  else { sortKey.value = key; sortDir.value = (key === 'epic' || key === 'status' || key === 'quarter' || key === 'promised') ? 'asc' : 'desc' }
}
function sortIcon(key: SortKey): string {
  if (sortKey.value !== key) return 'i-lucide-chevrons-up-down'
  return sortDir.value === 'asc' ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'
}

function sortValue(row: PriorityRow, key: SortKey): string | number | null {
  switch (key) {
    case 'epic': return row.epicTitle
    case 'status': return row.statusLabel
    case 'quarter': return row.quarterOrder
    case 'promised': return row.promisedDate
    case 'effort': return row.effort
    case 'score': return row.score
    case 'quadrant': return row.matrixRank
    default: return null
  }
}
function tieBreak(a: PriorityRow, b: PriorityRow): number {
  const as = a.score ?? -1
  const bs = b.score ?? -1
  if (as !== bs) return bs - as
  return a.epicTitle.localeCompare(b.epicTitle, 'pt-BR')
}

const sortedRows = computed(() => {
  const dir = sortDir.value === 'asc' ? 1 : -1
  const key = sortKey.value
  return [...filteredRows.value].sort((a, b) => {
    const av = sortValue(a, key)
    const bv = sortValue(b, key)
    const ae = av == null || av === ''
    const be = bv == null || bv === ''
    if (ae && be) return tieBreak(a, b)
    if (ae) return 1
    if (be) return -1
    let r = typeof av === 'number' && typeof bv === 'number' ? av - bv : String(av).localeCompare(String(bv), 'pt-BR')
    if (r === 0) return tieBreak(a, b)
    return r * dir
  })
})
</script>

<template>
  <div class="space-y-3">
    <div class="max-h-[70vh] overflow-auto rounded-xl border border-default">
      <table class="w-full text-xs">
        <thead class="sticky top-0 z-10 border-b border-default bg-elevated text-left text-[11px] text-muted">
          <tr>
            <th class="px-3 py-2 font-semibold">#</th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('epic')">
                Épico <UIcon :name="sortIcon('epic')" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2">
              <div class="flex items-center gap-1.5">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('status')">
                  Status <UIcon :name="sortIcon('status')" class="h-3 w-3" />
                </button>
                <ColumnMultiFilter v-model="statusFilter" :options="statusOptions" all-label="Todos os status" />
              </div>
            </th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('quarter')">
                Quarter <UIcon :name="sortIcon('quarter')" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('promised')">
                Dt prometida <UIcon :name="sortIcon('promised')" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2 text-right">
              <button type="button" class="flex w-full items-center justify-end gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('effort')">
                Horas <UIcon :name="sortIcon('effort')" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2 text-right">
              <button type="button" class="flex w-full items-center justify-end gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('score')">
                Score <UIcon :name="sortIcon('score')" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2">
              <button type="button" class="flex items-center gap-1 font-semibold uppercase tracking-[0.05em]" @click="toggleSort('quadrant')">
                Matriz <UIcon :name="sortIcon('quadrant')" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2 font-semibold uppercase tracking-[0.05em]">KPIs (meta · confiança · score)</th>
            <th class="px-3 py-2 text-right font-semibold uppercase tracking-[0.05em]">Ações</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!sortedRows.length">
            <td colspan="10" class="px-3 py-10 text-center text-sm text-muted">Nenhum épico no escopo selecionado.</td>
          </tr>
          <tr
            v-for="(row, index) in sortedRows"
            :key="row.epicId"
            class="border-b border-default/60 align-top transition-colors last:border-0 hover:bg-elevated"
          >
            <td class="px-3 py-2 text-[11px] font-semibold text-muted">{{ index + 1 }}º</td>
            <td class="px-3 py-2">
              <div class="max-w-[24rem] truncate font-medium text-highlighted" :title="row.epicTitle">{{ row.epicTitle }}</div>
            </td>
            <td class="px-3 py-2">
              <span class="inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium" :class="statusTone[row.status]">{{ row.statusLabel }}</span>
            </td>
            <td class="whitespace-nowrap px-3 py-2 text-muted">{{ row.quarterLabel }}</td>
            <td class="px-3 py-2 text-muted">{{ formatKpiDate(row.promisedDate) }}</td>
            <td class="px-3 py-2 text-right tabular-nums" :class="row.effort === 0 ? 'font-semibold text-red-600 dark:text-red-400' : 'text-highlighted'" :title="row.effort === 0 ? 'Sem horas estimadas — dado inconsistente para priorizar' : undefined">
              {{ row.effort.toLocaleString('pt-BR') }}h
            </td>
            <td class="px-3 py-2 text-right" :title="row.scoreTooltip">
              <span v-if="row.score != null" class="inline-flex cursor-help items-center gap-1 font-bold tabular-nums text-highlighted">
                <span v-if="row.confidence" class="h-2 w-2 rounded-full" :class="row.confidence ? confDot[row.confidence] : ''" />
                {{ fmtScore(row.score) }}
              </span>
              <span v-else class="cursor-help text-muted">—</span>
            </td>
            <td class="px-3 py-2">
              <span v-if="row.matrixInfo" class="inline-flex cursor-help items-center rounded-full px-2 py-0.5 text-[11px] font-semibold" :class="row.matrixInfo.tone" :title="row.matrixInfo.desc">
                {{ row.matrixInfo.label }}
              </span>
              <span v-else class="text-[11px] text-muted">—</span>
            </td>
            <td class="px-3 py-1.5">
              <div v-if="row.kpis.length" class="space-y-0.5">
                <div v-for="k in row.kpis" :key="k.key" class="flex items-center gap-2 text-[11px]">
                  <span class="h-2 w-2 shrink-0 cursor-help rounded-full" :class="k.isBusiness ? 'bg-amber-500 dark:bg-amber-400' : 'bg-slate-400 dark:bg-slate-500'" :title="k.typeTooltip" />
                  <span class="w-14 shrink-0 truncate text-muted">{{ k.indicatorLabel }}</span>
                  <span class="shrink-0 cursor-help font-medium text-highlighted" :title="k.metaTooltip">{{ k.metaText }}</span>
                  <span class="ml-auto shrink-0 cursor-help text-muted" :title="k.confTooltip">{{ k.confLabel }}</span>
                  <span class="shrink-0 rounded bg-elevated px-1 py-0.5 font-semibold text-highlighted">{{ fmtScore(k.score) }}</span>
                </div>
              </div>
              <span v-else class="text-[11px]" :class="row.isPendente ? 'font-semibold text-red-600 dark:text-red-400' : 'text-muted'" :title="row.isPendente ? 'Épico sem KPI — dado inconsistente para priorizar' : undefined">{{ row.kpiFallbackText }}</span>
            </td>
            <td class="px-3 py-2">
              <div class="flex items-center justify-end gap-1">
                <button
                  type="button"
                  class="inline-flex h-7 w-7 items-center justify-center rounded-md border border-default text-muted transition-colors hover:border-primary/40 hover:text-primary"
                  title="Editar épico"
                  @click="emit('editEpic', row.epicId)"
                >
                  <UIcon name="i-lucide-pencil" class="h-3.5 w-3.5" />
                </button>
                <button
                  type="button"
                  class="inline-flex h-7 w-7 items-center justify-center rounded-md border border-default text-muted transition-colors hover:border-primary/40 hover:text-primary"
                  title="Abrir KPIs do épico"
                  @click="emit('openEpic', row.epicId)"
                >
                  <UIcon name="i-lucide-line-chart" class="h-3.5 w-3.5" />
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
