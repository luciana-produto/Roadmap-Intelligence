<script setup lang="ts">
import type { RoadmapDemand, Kpi } from '~/types/roadmap'
import { useRoadmapPriority, statusTone, confDot, fmtScore } from '~/composables/useRoadmapPriority'
import type { PriorityRow, MatrixKind } from '~/composables/useRoadmapPriority'

const props = defineProps<{
  demands: RoadmapDemand[]
  allDemands: RoadmapDemand[]
  kpis: Kpi[]
}>()

const emit = defineEmits<{ openEpic: [epicId: string] }>()

// Mesma fonte de regras da Lista — a matriz é só uma forma de visualizar os `rows`.
const { rows } = useRoadmapPriority(toRef(props, 'demands'), toRef(props, 'allDemands'), toRef(props, 'kpis'))

// Rank global por score (mesma ordem padrão da Lista) para numerar os itens.
const rankByEpic = computed(() => {
  const ordered = [...rows.value].sort((a, b) => {
    const as = a.score ?? -1, bs = b.score ?? -1
    if (as !== bs) return bs - as
    return a.epicTitle.localeCompare(b.epicTitle, 'pt-BR')
  })
  const map = new Map<string, number>()
  ordered.forEach((r, i) => map.set(r.epicId, i + 1))
  return map
})

// Agrupa por quadrante, ordenando cada um por score desc.
const byKind = computed(() => {
  const map = new Map<MatrixKind, PriorityRow[]>()
  for (const r of rows.value) {
    if (!r.matrixKind) continue
    const arr = map.get(r.matrixKind) ?? []
    arr.push(r)
    map.set(r.matrixKind, arr)
  }
  for (const arr of map.values())
    arr.sort((a, b) => (b.score ?? -1) - (a.score ?? -1) || a.epicTitle.localeCompare(b.epicTitle, 'pt-BR'))
  return map
})
function rowsOf(kind: MatrixKind): PriorityRow[] {
  return byKind.value.get(kind) ?? []
}
const specialRows = computed(() => [...rowsOf('tecnico'), ...rowsOf('mandatorio')])
const hasAny = computed(() => rows.value.length > 0)

type Quadrant = {
  kind: MatrixKind
  title: string
  en: string
  sub: string
  icon: string
  border: string
  title_cls: string
  badge: string
}
// Ordem 2×2: topo = alto impacto (Ganhos | Apostas); base = baixo impacto (Preencher | Evitar).
const QUADRANTS: Quadrant[] = [
  { kind: 'ganhos', title: 'Ganhos Rápidos', en: 'Quick Wins', sub: 'Alto Impacto · Baixo Esforço', icon: 'i-lucide-rocket', border: 'border-emerald-200 dark:border-emerald-900/50', title_cls: 'text-emerald-700 dark:text-emerald-300', badge: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300' },
  { kind: 'apostas', title: 'Grandes Apostas', en: 'Big Bets', sub: 'Alto Impacto · Alto Esforço', icon: 'i-lucide-target', border: 'border-blue-200 dark:border-blue-900/50', title_cls: 'text-blue-700 dark:text-blue-300', badge: 'bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300' },
  { kind: 'quando', title: 'Preencher', en: 'Fill-ins', sub: 'Baixo Impacto · Baixo Esforço', icon: 'i-lucide-zap', border: 'border-amber-200 dark:border-amber-900/50', title_cls: 'text-amber-700 dark:text-amber-300', badge: 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300' },
  { kind: 'evitar', title: 'Evitar / Repensar', en: 'Money Pit', sub: 'Baixo Impacto · Alto Esforço', icon: 'i-lucide-alert-triangle', border: 'border-red-200 dark:border-red-900/50', title_cls: 'text-red-700 dark:text-red-300', badge: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300' }
]
</script>

<template>
  <div class="space-y-3">
    <!-- Cabeçalho (padrão de caixa de informação do app) -->
    <div class="flex items-center gap-2.5 rounded-lg border border-default bg-elevated/30 px-3 py-2">
      <UIcon name="i-lucide-layout-grid" class="h-4 w-4 shrink-0 text-primary" />
      <div class="min-w-0">
        <p class="text-xs font-semibold text-highlighted">Matriz de priorização — Impacto × Esforço</p>
        <p class="text-[11px] leading-relaxed text-muted">Mesma base da Lista — épicos posicionados por impacto (Score) e esforço (horas), comparados às medianas do escopo.</p>
      </div>
    </div>

    <div v-if="!hasAny" class="rounded-xl border border-default py-14 text-center text-sm text-muted">
      Nenhum épico no escopo selecionado.
    </div>

    <template v-else>
      <!-- Grade 2×2 com rótulos dos eixos -->
      <div class="flex gap-2">
        <div class="flex w-4 shrink-0 items-center justify-center">
          <span class="-rotate-90 whitespace-nowrap text-[10px] font-medium uppercase tracking-wide text-muted">Impacto estimado →</span>
        </div>
        <div class="grid min-w-0 flex-1 grid-cols-1 gap-3 lg:auto-rows-fr lg:grid-cols-2">
          <div v-for="q in QUADRANTS" :key="q.kind" class="min-w-0 rounded-xl border bg-default p-3" :class="q.border">
            <div class="mb-2 flex items-center gap-2">
              <span class="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg" :class="q.badge">
                <UIcon :name="q.icon" class="h-4 w-4" />
              </span>
              <div class="min-w-0">
                <p class="truncate text-sm font-semibold" :class="q.title_cls">
                  {{ q.title }} <span class="text-xs font-normal text-muted">({{ q.en }})</span>
                </p>
                <p class="text-[11px] text-muted">{{ q.sub }}</p>
              </div>
              <span class="ml-auto shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold" :class="q.badge">{{ rowsOf(q.kind).length }}</span>
            </div>

            <div class="space-y-0.5">
              <button
                v-for="row in rowsOf(q.kind)"
                :key="row.epicId"
                type="button"
                class="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left transition-colors hover:bg-elevated"
                :title="`${row.epicTitle} — clique para abrir os KPIs`"
                @click="emit('openEpic', row.epicId)"
              >
                <span class="w-7 shrink-0 text-[11px] font-semibold text-muted">#{{ rankByEpic.get(row.epicId) }}</span>
                <span class="w-24 shrink-0">
                  <span class="inline-flex items-center rounded-full px-1.5 py-0.5 text-[10px] font-medium" :class="statusTone[row.status]">{{ row.statusLabel }}</span>
                </span>
                <span class="min-w-0 flex-1 truncate text-xs font-medium text-highlighted">{{ row.epicTitle }}</span>
                <span class="w-24 shrink-0 whitespace-nowrap text-right text-[10px] text-muted">{{ row.quarterLabel }}</span>
                <span
                  class="w-12 shrink-0 text-right tabular-nums text-[11px]"
                  :class="row.effort === 0 ? 'font-semibold text-red-600 dark:text-red-400' : 'text-muted'"
                >{{ row.effort.toLocaleString('pt-BR') }}h</span>
                <span class="flex w-12 shrink-0 justify-end">
                  <span
                    class="inline-flex cursor-help items-center gap-1 rounded bg-elevated px-1.5 py-0.5 text-[11px] font-bold tabular-nums text-highlighted"
                    :title="row.scoreTooltip"
                  >
                    <span v-if="row.confidence" class="h-1.5 w-1.5 rounded-full" :class="confDot[row.confidence]" />
                    {{ fmtScore(row.score ?? 0) }}
                  </span>
                </span>
              </button>
              <p v-if="!rowsOf(q.kind).length" class="px-2 py-4 text-center text-[11px] text-muted">Nenhum épico aqui.</p>
            </div>
          </div>
        </div>
      </div>
      <p class="pl-6 text-center text-[10px] font-medium uppercase tracking-wide text-muted">Esforço estimado (horas) →</p>

      <!-- Fora da matriz: Técnico / Mandatório (sem score) -->
      <div v-if="specialRows.length" class="rounded-xl border border-dashed border-default p-3">
        <p class="mb-2 text-[11px] font-semibold uppercase tracking-wide text-muted">
          Fora da matriz — não competem por score
        </p>
        <div class="space-y-0.5">
          <button
            v-for="row in specialRows"
            :key="row.epicId"
            type="button"
            class="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left transition-colors hover:bg-elevated"
            :title="`${row.epicTitle} — clique para abrir os KPIs`"
            @click="emit('openEpic', row.epicId)"
          >
            <span class="w-7 shrink-0 text-[11px] font-semibold text-muted">#{{ rankByEpic.get(row.epicId) }}</span>
            <span class="w-24 shrink-0">
              <span class="inline-flex items-center rounded-full px-1.5 py-0.5 text-[10px] font-medium" :class="statusTone[row.status]">{{ row.statusLabel }}</span>
            </span>
            <span class="min-w-0 flex-1 truncate text-xs font-medium text-highlighted">{{ row.epicTitle }}</span>
            <span class="w-24 shrink-0 whitespace-nowrap text-right text-[10px] text-muted">{{ row.quarterLabel }}</span>
            <span class="w-12 shrink-0 text-right tabular-nums text-[11px] text-muted">{{ row.effort.toLocaleString('pt-BR') }}h</span>
            <span class="flex w-20 shrink-0 justify-end">
              <span
                v-if="row.matrixInfo"
                class="inline-flex cursor-help items-center rounded-full px-2 py-0.5 text-[10px] font-semibold"
                :class="row.matrixInfo.tone"
                :title="row.matrixInfo.desc"
              >{{ row.matrixInfo.label }}</span>
            </span>
          </button>
        </div>
      </div>
    </template>
  </div>
</template>
