<script setup lang="ts">
import type { KpiUnit } from '~/types/roadmap'

interface ChartRow {
  key: string
  epicId: string
  epicTitle: string
  kpiName: string
  unit: KpiUnit
  impact: number | null
  impactDate: string | null
}

const props = defineProps<{
  // Já ordenado do maior impacto para o menor (itens sem apuração no fim).
  rows: ChartRow[]
  variant: 'indigo' | 'emerald'
}>()

const emit = defineEmits<{ openEpic: [epicId: string] }>()

// Só entram no gráfico os épicos com apuração.
const measured = computed(() =>
  props.rows.filter((row): row is ChartRow & { impact: number } => row.impact != null)
)
const unmeasuredCount = computed(() => props.rows.length - measured.value.length)
const axisUnit = computed<KpiUnit>(() => measured.value[0]?.unit ?? 'Number')

const maxValue = computed(() => measured.value.reduce((max, row) => Math.max(max, row.impact), 0))

// Arredonda o topo do eixo para um valor "redondo" acima do maior impacto,
// deixando folga para os rótulos e uma escala legível.
function niceCeil(value: number): number {
  if (value <= 0)
    return 1
  const pow = Math.pow(10, Math.floor(Math.log10(value)))
  const steps = [1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10]
  for (const step of steps) {
    if (step * pow >= value)
      return step * pow
  }
  return 10 * pow
}
const niceMax = computed(() => niceCeil(maxValue.value))

const ticks = computed(() =>
  [0, 0.25, 0.5, 0.75, 1].map(fraction => ({
    fraction,
    left: `${fraction * 100}%`,
    value: niceMax.value * fraction
  }))
)

function barPct(value: number): string {
  if (niceMax.value <= 0)
    return '0%'
  return `${Math.max((value / niceMax.value) * 100, 1)}%`
}

// ─── Formatação ──────────────────────────────────────────────────────────────
function formatValue(value: number, unit: KpiUnit): string {
  if (unit === 'Currency') {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency', currency: 'BRL', maximumFractionDigits: 0
    }).format(value)
  }
  const formatted = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value)
  if (unit === 'Percentage')
    return `${formatted}%`
  if (unit === 'TimeSeconds')
    return `${formatted}s`
  return formatted
}

function formatTick(value: number, unit: KpiUnit): string {
  const compact = new Intl.NumberFormat('pt-BR', { notation: 'compact', maximumFractionDigits: 1 }).format(value)
  if (unit === 'Currency')
    return `R$ ${compact}`
  if (unit === 'Percentage')
    return `${compact}%`
  if (unit === 'TimeSeconds')
    return `${compact}s`
  return compact
}

function formatDate(value: string | null): string {
  if (!value)
    return '—'
  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day)
    return value
  return new Intl.DateTimeFormat('pt-BR').format(new Date(year, month - 1, day))
}

const barClass = computed(() =>
  props.variant === 'indigo' ? 'bg-indigo-500 dark:bg-indigo-400' : 'bg-emerald-500 dark:bg-emerald-400'
)
const valueClass = computed(() =>
  props.variant === 'indigo' ? 'text-indigo-700 dark:text-indigo-300' : 'text-emerald-700 dark:text-emerald-300'
)

// Grade vertical (linhas em 25/50/75/100%) desenhada no fundo de cada barra,
// acompanhando o scroll naturalmente.
const trackStyle = {
  backgroundImage:
    'repeating-linear-gradient(to right, transparent 0, transparent calc(25% - 1px), rgba(128,128,128,0.16) calc(25% - 1px), rgba(128,128,128,0.16) 25%)'
}
</script>

<template>
  <div class="flex h-full min-h-0 flex-col">
    <template v-if="measured.length">
      <!-- Eixo de valores (topo) -->
      <div class="flex shrink-0 items-end gap-2 px-3.5 pt-2 pb-1">
        <span class="w-28 shrink-0" />
        <span class="relative h-4 flex-1">
          <span
            v-for="tick in ticks"
            :key="tick.fraction"
            class="absolute bottom-0 -translate-x-1/2 whitespace-nowrap text-[10px] text-muted"
            :style="{ left: tick.left }"
          >{{ formatTick(tick.value, axisUnit) }}</span>
        </span>
        <span class="w-24 shrink-0" />
      </div>

      <!-- Barras -->
      <div class="min-h-0 flex-1 space-y-1.5 overflow-y-auto px-3.5 pb-3 pt-1">
        <button
          v-for="row in measured"
          :key="row.key"
          type="button"
          class="flex w-full items-center gap-2 rounded-md py-0.5 text-left transition-colors hover:bg-elevated"
          :title="`${row.epicTitle} · ${row.kpiName} · ${formatValue(row.impact, row.unit)} · ${formatDate(row.impactDate)}`"
          @click="emit('openEpic', row.epicId)"
        >
          <span class="w-28 shrink-0 truncate pr-1 text-right text-xs font-medium text-highlighted">{{ row.epicTitle }}</span>
          <span class="relative h-5 flex-1 rounded-sm" :style="trackStyle">
            <span class="absolute inset-y-0.5 left-0 rounded-r-sm transition-all duration-300" :class="barClass" :style="{ width: barPct(row.impact) }" />
          </span>
          <span class="w-24 shrink-0 truncate text-right text-xs font-bold tabular-nums" :class="valueClass">{{ formatValue(row.impact, row.unit) }}</span>
        </button>
      </div>

      <div v-if="unmeasuredCount" class="shrink-0 border-t border-default/60 px-3.5 py-1.5 text-[11px] text-muted">
        +{{ unmeasuredCount }} {{ unmeasuredCount === 1 ? 'épico' : 'épicos' }} sem apuração
      </div>
    </template>

    <div v-else class="px-3.5 py-5 text-sm text-muted">Ainda não há apurações registradas para este indicador.</div>
  </div>
</template>
