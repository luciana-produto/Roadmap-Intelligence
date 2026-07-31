<script setup lang="ts">
import type { RoadmapDemand } from '~/types/roadmap'
import { buildQuarterValue } from '~/utils/roadmapQuarter'
import { delayReasonLabel } from '~/utils/roadmapDelay'

type ReasonType = 'atraso-transbordo' | 'deprioritization'

const route = useRoute()
const roadmapStore = useRoadmapStore()
const { projects, demands, isLoading, selectedProjectId, selectedQuarterYear, selectedQuarterNumber } = storeToRefs(roadmapStore)

// 'deprioritization' → relatório de despriorização; qualquer outro valor → atraso + transbordo.
const tipo = computed<ReasonType>(() => route.query.tipo === 'deprioritization' ? 'deprioritization' : 'atraso-transbordo')
const isDeprio = computed(() => tipo.value === 'deprioritization')
const teams = computed(() => String(route.query.teams ?? '').split(',').filter(Boolean))
const quarters = computed(() => String(route.query.quarters ?? '').split(',').filter(Boolean))

onMounted(async () => {
  selectedProjectId.value = null
  selectedQuarterYear.value = null
  selectedQuarterNumber.value = null
  await roadmapStore.fetchProjects()
  await roadmapStore.fetchDemands()
})

const reportTitle = computed(() => isDeprio.value ? 'Relatório de Motivos da Despriorização' : 'Relatório de Motivos de Atraso e Transbordo')
const scopeLabel = computed(() => isDeprio.value ? 'itens despriorizados' : 'atrasos e transbordos')
const accent = computed(() => isDeprio.value ? 'text-pink-600 dark:text-pink-400' : 'text-amber-600 dark:text-amber-400')

useSeoMeta({ title: () => `${reportTitle.value} · ProductHub` })

// Rótulos de despriorização (enum próprio, diferente do transbordo/atraso).
function deprioritizationReasonLabel(value?: string | null): string {
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
    default: return value ?? ''
  }
}

const projectNameById = computed(() => new Map(projects.value.map(p => [p.id, p.name] as const)))
const demandById = computed(() => new Map(demands.value.map(d => [d.id, d] as const)))
const demandTitleById = computed(() => new Map(demands.value.map(d => [d.id, d.title] as const)))

function teamName(d: RoadmapDemand): string {
  const id = d.itemType === 'Epic' ? (d.projectIds?.[0] ?? '') : (d.projectId ?? '')
  return projectNameById.value.get(id) ?? '—'
}
function quarterRank(year: number, number: number): number {
  return year * 4 + number
}

const now = new Date()
const todayISO = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
function fmtDate(iso?: string | null): string {
  if (!iso) return '—'
  const [y, m, d] = iso.split('-')
  return (y && m && d) ? `${d}/${m}/${y}` : iso
}
function daysBetween(fromISO: string, toISO: string): number {
  const a = new Date(`${fromISO}T00:00:00`).getTime()
  const b = new Date(`${toISO}T00:00:00`).getTime()
  return Math.round((b - a) / 86_400_000)
}
// Segue a cadeia de sucessores (transbordos encadeados) até a demanda final "viva".
function outcomeDemand(d: RoadmapDemand): RoadmapDemand {
  let current = d
  const seen = new Set<string>()
  while (current.successorDemandId && !seen.has(current.id)) {
    seen.add(current.id)
    const next = demandById.value.get(current.successorDemandId)
    if (!next) break
    current = next
  }
  return current
}

// Escopo: demandas + épicos simples, filtrados por time e quarter (mesma régua dos dashboards).
const scopedDemands = computed(() => {
  const base = demands.value.filter(item => item.itemType === 'Demand' || (item.itemType === 'Epic' && item.isSimple))
  const teamFiltered = teams.value.length
    ? base.filter(d => d.itemType === 'Epic'
        ? (d.projectIds ?? []).some(pid => teams.value.includes(pid))
        : teams.value.includes(d.projectId ?? ''))
    : base
  if (!quarters.value.length) return teamFiltered
  return teamFiltered.filter(d => quarters.value.includes(buildQuarterValue(d.quarterYear, d.quarterNumber)))
})

type Row = {
  id: string
  itemType: RoadmapDemand['itemType']
  title: string
  category: 'Atraso' | 'Transbordo' | ''
  team: string
  origem: string
  origemOrder: number
  destino: string
  destinoOrder: number | null
  products: string
  hours: number
  reasonLabel: string
  expectedISO: string
  deliveredISO: string
  daysLateNum: number | null
  replacement: string
  observation: string
}

function baseRow(d: RoadmapDemand) {
  return {
    id: d.id,
    itemType: d.itemType,
    title: d.title,
    team: teamName(d),
    origem: d.quarterLabel,
    origemOrder: quarterRank(d.quarterYear, d.quarterNumber),
    products: d.products.map(p => p.name).join(', ') || '—',
    hours: d.hours ?? 0
  }
}

const rows = computed<Row[]>(() => {
  if (isDeprio.value) {
    return scopedDemands.value
      .filter(d => d.status === 'Deprioritized' && !!d.deprioritizationReason)
      .map(d => ({
        ...baseRow(d),
        category: '' as const,
        destino: '—', destinoOrder: null,
        reasonLabel: deprioritizationReasonLabel(d.deprioritizationReason),
        expectedISO: '', deliveredISO: '', daysLateNum: null,
        replacement: d.replacementDemandId ? (demandTitleById.value.get(d.replacementDemandId) ?? '—') : '—',
        observation: d.observation?.trim() || '—'
      }))
  }

  return scopedDemands.value
    .filter(d => (d.status === 'Spillover' && !!d.spilloverReason) || (d.status === 'Done' && !!d.delayReason))
    .map((d) => {
      const isSpillover = d.status === 'Spillover'
      const expectedISO = d.effectivePromisedDate ?? d.promisedDate ?? ''
      const final = outcomeDemand(d)
      const concluded = final.status === 'Done' && !!final.deliveryDate
      const deliveredISO = concluded ? (final.deliveryDate ?? '') : ''
      const daysLateNum = expectedISO
        ? Math.max(0, daysBetween(expectedISO, concluded ? deliveredISO : todayISO))
        : null
      const successor = isSpillover && d.successorDemandId ? demandById.value.get(d.successorDemandId) : undefined
      return {
        ...baseRow(d),
        category: (isSpillover ? 'Transbordo' : 'Atraso') as 'Atraso' | 'Transbordo',
        destino: successor ? successor.quarterLabel : '—',
        destinoOrder: successor ? quarterRank(successor.quarterYear, successor.quarterNumber) : null,
        reasonLabel: delayReasonLabel(isSpillover ? d.spilloverReason : d.delayReason),
        expectedISO,
        deliveredISO,
        daysLateNum,
        replacement: '—',
        observation: (isSpillover ? d.spilloverObservation : d.delayObservation)?.trim() || '—'
      }
    })
})

// ─── Ordenação ────────────────────────────────────────────────────────────────
type SortKey = 'item' | 'category' | 'team' | 'origem' | 'destino' | 'produto' | 'horas' | 'motivo' | 'esperada' | 'entregue' | 'dias' | 'replacement' | 'obs'
const sortKey = ref<SortKey | null>(null)
const sortDir = ref<'asc' | 'desc'>('asc')
function toggleSort(key: SortKey) {
  if (sortKey.value === key) sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  else { sortKey.value = key; sortDir.value = 'asc' }
}
function sortIcon(key: SortKey): string {
  if (sortKey.value !== key) return 'i-lucide-chevrons-up-down'
  return sortDir.value === 'asc' ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'
}
function sortVal(row: Row, key: SortKey): string | number | null {
  switch (key) {
    case 'item': return row.title
    case 'category': return row.category
    case 'team': return row.team
    case 'origem': return row.origemOrder
    case 'destino': return row.destinoOrder
    case 'produto': return row.products
    case 'horas': return row.hours
    case 'motivo': return row.reasonLabel
    case 'esperada': return row.expectedISO
    case 'entregue': return row.deliveredISO
    case 'dias': return row.daysLateNum
    case 'replacement': return row.replacement
    case 'obs': return row.observation
    default: return null
  }
}
const sortedRows = computed(() => {
  const key = sortKey.value
  if (!key) {
    // Ordem padrão: motivo, categoria, título.
    return [...rows.value].sort((a, b) =>
      a.reasonLabel.localeCompare(b.reasonLabel, 'pt-BR') || a.category.localeCompare(b.category) || a.title.localeCompare(b.title, 'pt-BR'))
  }
  const dir = sortDir.value === 'asc' ? 1 : -1
  return [...rows.value].sort((a, b) => {
    const av = sortVal(a, key)
    const bv = sortVal(b, key)
    const ae = av == null || av === ''
    const be = bv == null || bv === ''
    if (ae && be) return 0
    if (ae) return 1
    if (be) return -1
    const r = typeof av === 'number' && typeof bv === 'number' ? av - bv : String(av).localeCompare(String(bv), 'pt-BR')
    return r * dir
  })
})

// Totais por motivo (contagem, %, horas).
const totals = computed(() => {
  const total = rows.value.length
  const map = new Map<string, { label: string, count: number, hours: number }>()
  for (const r of rows.value) {
    const cur = map.get(r.reasonLabel) ?? { label: r.reasonLabel, count: 0, hours: 0 }
    cur.count += 1
    cur.hours += r.hours
    map.set(r.reasonLabel, cur)
  }
  return {
    total,
    totalHours: rows.value.reduce((s, r) => s + r.hours, 0),
    items: [...map.values()]
      .map(i => ({ ...i, pct: total > 0 ? (i.count / total) * 100 : 0 }))
      .sort((a, b) => b.count - a.count)
  }
})

function exportCsv() {
  const escape = (v: string | number) => {
    const s = String(v ?? '')
    return /[";\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s
  }
  const header = isDeprio.value
    ? ['Tipo item', 'Item', 'Time', 'Q Origem', 'Produto', 'Horas', 'Motivo', 'Demanda priorizada no lugar', 'Observação']
    : ['Tipo item', 'Item', 'Categoria', 'Time', 'Q Origem', 'Q Destino', 'Produto', 'Horas', 'Motivo', 'Data esperada', 'Data entregue', 'Dias de atraso', 'Observação']
  const lines = [header.join(';')]
  for (const r of sortedRows.value) {
    const itemTipo = r.itemType === 'Epic' ? 'Épico' : 'Demanda'
    const dias = r.daysLateNum == null ? '—' : String(r.daysLateNum)
    const cols = isDeprio.value
      ? [itemTipo, r.title, r.team, r.origem, r.products, r.hours, r.reasonLabel, r.replacement, r.observation]
      : [itemTipo, r.title, r.category, r.team, r.origem, r.destino, r.products, r.hours, r.reasonLabel, fmtDate(r.expectedISO), fmtDate(r.deliveredISO), dias, r.observation]
    lines.push(cols.map(escape).join(';'))
  }
  // BOM (﻿) para o Excel reconhecer acentos.
  const blob = new Blob([`﻿${lines.join('\r\n')}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `relatorio-${tipo.value}.csv`
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div class="space-y-5">
    <!-- Cabeçalho -->
    <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
      <div class="min-w-0">
        <h1 class="text-2xl font-bold text-highlighted">{{ reportTitle }}</h1>
        <p class="mt-1 text-sm text-muted">
          {{ totals.total }} {{ scopeLabel }} · {{ totals.totalHours.toLocaleString('pt-BR') }}h no escopo selecionado.
        </p>
      </div>
      <div class="flex items-center gap-2">
        <UButton icon="i-lucide-download" label="Exportar CSV" color="neutral" variant="soft" size="sm" :disabled="!rows.length" @click="exportCsv" />
      </div>
    </div>

    <div v-if="isLoading && !demands.length" class="flex items-center justify-center py-20">
      <UIcon name="i-lucide-loader-circle" class="h-6 w-6 animate-spin text-muted" />
    </div>

    <template v-else>
      <!-- Totais por motivo -->
      <UCard v-if="totals.items.length" class="ring-default" :ui="{ body: 'p-3.5' }">
        <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">Totais por motivo</p>
        <div class="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
          <div v-for="t in totals.items" :key="t.label" class="rounded-lg border border-default bg-elevated/30 px-3 py-2">
            <div class="flex items-center gap-2">
              <span class="min-w-0 flex-1 truncate text-sm font-medium text-highlighted" :title="t.label">{{ t.label }}</span>
              <span class="shrink-0 text-[11px] text-muted">{{ t.hours.toLocaleString('pt-BR') }}h</span>
              <span class="shrink-0 text-xs font-semibold" :class="accent">{{ t.pct.toFixed(1) }}%</span>
              <span class="shrink-0 rounded-full bg-elevated px-2 py-0.5 text-[11px] text-muted">{{ t.count }} it.</span>
            </div>
          </div>
        </div>
      </UCard>

      <!-- Legenda da regra de "Dias de atraso" (só no relatório de atraso/transbordo) -->
      <div v-if="!isDeprio" class="rounded-lg border border-default bg-elevated/30 px-3 py-2 text-[11px] leading-relaxed text-muted">
        <span class="font-semibold text-highlighted">Como calculamos "Dias de atraso":</span>
        diferença entre a <span class="font-medium text-highlighted">Data esperada</span> e — se o item foi <span class="font-medium text-highlighted">concluído</span>, a <span class="font-medium text-highlighted">Data entregue</span>; se <span class="font-medium text-highlighted">ainda aberto</span>, a data de hoje.
        A <span class="font-medium text-highlighted">Data esperada</span> é a data prometida ou, na ausência, o último dia do quarter de origem.
        Em <span class="font-medium text-highlighted">transbordos</span>, o desfecho é seguido pela cadeia de cópias até a demanda final (por isso a Data entregue pode vir da cópia).
      </div>

      <!-- Tabela detalhada (ordenável) -->
      <div class="overflow-x-auto rounded-xl border border-default">
        <table class="w-full text-sm">
          <thead class="border-b border-default bg-elevated/40 text-left text-[11px] uppercase tracking-[0.05em] text-muted">
            <tr>
              <th class="px-3 py-2">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase" @click="toggleSort('item')">Demanda / Épico <UIcon :name="sortIcon('item')" class="h-3 w-3" /></button>
              </th>
              <th v-if="!isDeprio" class="px-3 py-2">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase" @click="toggleSort('category')">Categoria <UIcon :name="sortIcon('category')" class="h-3 w-3" /></button>
              </th>
              <th class="px-3 py-2">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase" @click="toggleSort('team')">Time <UIcon :name="sortIcon('team')" class="h-3 w-3" /></button>
              </th>
              <th class="px-3 py-2">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase" @click="toggleSort('origem')">{{ isDeprio ? 'Quarter' : 'Q Origem' }} <UIcon :name="sortIcon('origem')" class="h-3 w-3" /></button>
              </th>
              <th v-if="!isDeprio" class="px-3 py-2">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase" @click="toggleSort('destino')">Q Destino <UIcon :name="sortIcon('destino')" class="h-3 w-3" /></button>
              </th>
              <th class="px-3 py-2">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase" @click="toggleSort('produto')">Produto <UIcon :name="sortIcon('produto')" class="h-3 w-3" /></button>
              </th>
              <th class="px-3 py-2 text-right">
                <button type="button" class="flex w-full items-center justify-end gap-1 font-semibold uppercase" @click="toggleSort('horas')">Horas <UIcon :name="sortIcon('horas')" class="h-3 w-3" /></button>
              </th>
              <th class="px-3 py-2">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase" @click="toggleSort('motivo')">Motivo <UIcon :name="sortIcon('motivo')" class="h-3 w-3" /></button>
              </th>
              <th v-if="!isDeprio" class="px-3 py-2">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase" @click="toggleSort('esperada')">Data esperada <UIcon :name="sortIcon('esperada')" class="h-3 w-3" /></button>
              </th>
              <th v-if="!isDeprio" class="px-3 py-2">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase" @click="toggleSort('entregue')">Data entregue <UIcon :name="sortIcon('entregue')" class="h-3 w-3" /></button>
              </th>
              <th v-if="!isDeprio" class="px-3 py-2 text-right">
                <button type="button" class="flex w-full items-center justify-end gap-1 font-semibold uppercase" @click="toggleSort('dias')">Dias de atraso <UIcon :name="sortIcon('dias')" class="h-3 w-3" /></button>
              </th>
              <th v-if="isDeprio" class="px-3 py-2">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase" @click="toggleSort('replacement')">Demanda priorizada no lugar <UIcon :name="sortIcon('replacement')" class="h-3 w-3" /></button>
              </th>
              <th class="px-3 py-2">
                <button type="button" class="flex items-center gap-1 font-semibold uppercase" @click="toggleSort('obs')">Observação <UIcon :name="sortIcon('obs')" class="h-3 w-3" /></button>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="!sortedRows.length">
              <td :colspan="isDeprio ? 8 : 12" class="px-3 py-10 text-center text-sm text-muted">Nenhum item no escopo selecionado.</td>
            </tr>
            <tr v-for="r in sortedRows" :key="r.id" class="border-b border-default/60 align-top last:border-0 hover:bg-elevated">
              <td class="px-3 py-2">
                <div class="flex items-center gap-2">
                  <span class="shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-medium" :class="r.itemType === 'Epic' ? 'bg-violet-100 text-violet-700 dark:bg-violet-900/30 dark:text-violet-300' : 'bg-neutral-100 text-neutral-600 dark:bg-neutral-800 dark:text-neutral-300'">{{ r.itemType === 'Epic' ? 'Épico' : 'Demanda' }}</span>
                  <span class="font-medium text-highlighted">{{ r.title }}</span>
                </div>
              </td>
              <td v-if="!isDeprio" class="px-3 py-2">
                <span class="rounded-full px-2 py-0.5 text-[10px] font-medium" :class="r.category === 'Transbordo' ? 'bg-orange-100 text-orange-700 dark:bg-orange-900/30 dark:text-orange-300' : 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300'">{{ r.category }}</span>
              </td>
              <td class="whitespace-nowrap px-3 py-2 text-muted">{{ r.team }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-muted">{{ r.origem }}</td>
              <td v-if="!isDeprio" class="whitespace-nowrap px-3 py-2 text-muted">{{ r.destino }}</td>
              <td class="px-3 py-2 text-muted">{{ r.products }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right tabular-nums text-highlighted">{{ r.hours.toLocaleString('pt-BR') }}h</td>
              <td class="px-3 py-2 font-medium text-highlighted">{{ r.reasonLabel }}</td>
              <td v-if="!isDeprio" class="whitespace-nowrap px-3 py-2 text-muted">{{ fmtDate(r.expectedISO) }}</td>
              <td v-if="!isDeprio" class="whitespace-nowrap px-3 py-2 text-muted">{{ fmtDate(r.deliveredISO) }}</td>
              <td v-if="!isDeprio" class="whitespace-nowrap px-3 py-2 text-right tabular-nums text-highlighted">{{ r.daysLateNum == null ? '—' : r.daysLateNum }}</td>
              <td v-if="isDeprio" class="px-3 py-2 text-muted">{{ r.replacement }}</td>
              <td class="px-3 py-2 text-muted">{{ r.observation }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>
