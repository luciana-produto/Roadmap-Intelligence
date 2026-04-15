<script setup lang="ts">
import type { RoadmapDemand, DemandStatus, DemandType, DemandClassification } from '~/types/roadmap'

const props = defineProps<{
  demand: RoadmapDemand
  planningQuarterOptions?: { value: string, label: string }[]
}>()
const emit = defineEmits<{ edit: [demand: RoadmapDemand], delete: [id: string], plan: [demand: RoadmapDemand, quarterValue: string], dependencyClick: [dependency: RoadmapDemand['dependsOn'][number]] }>()

const statusConfig: Record<DemandStatus, { color: string, dot: string, label: string }> = {
  Backlog:      { color: 'text-muted', dot: 'bg-neutral-400 dark:bg-neutral-500', label: 'Backlog' },
  InProgress:   { color: 'text-blue-500', dot: 'bg-blue-500 dark:bg-blue-400', label: 'Em andamento' },
  Done:         { color: 'text-green-500', dot: 'bg-green-500 dark:bg-green-400', label: 'Concluído' },
  Deprioritized:{ color: 'text-amber-500', dot: 'bg-amber-500 dark:bg-amber-400', label: 'Despriorizado' }
}

const typeConfig: Record<DemandType, { color: string, label: string }> = {
  Planned:    { color: 'bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300', label: 'Planejado' },
  Spillover:  { color: 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300', label: 'Transbordo' },
  Unplanned:  { color: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300', label: 'Não Planejado' },
  Additional: { color: 'bg-purple-100 text-purple-700 dark:bg-purple-900/40 dark:text-purple-300', label: 'Adicional' }
}

const classificationConfig: Record<DemandClassification, { color: string, label: string }> = {
  TechnicalDebtSecurity: { color: 'bg-slate-100 text-slate-700 border-slate-200 dark:bg-slate-800/60 dark:text-slate-300 dark:border-slate-700', label: 'Débito Técnico' },
  Strategic: { color: 'bg-indigo-100 text-indigo-700 border-indigo-200 dark:bg-indigo-900/40 dark:text-indigo-300 dark:border-indigo-800', label: 'Estratégico' },
  Evolution: { color: 'bg-sky-100 text-sky-700 border-sky-200 dark:bg-sky-900/40 dark:text-sky-300 dark:border-sky-800', label: 'Evolução' },
  ImprovementGap: { color: 'bg-emerald-100 text-emerald-700 border-emerald-200 dark:bg-emerald-900/40 dark:text-emerald-300 dark:border-emerald-800', label: 'Melhoria/Gap' },
  Mandatory: { color: 'bg-red-100 text-red-700 border-red-200 dark:bg-red-900/40 dark:text-red-300 dark:border-red-800', label: 'Mandatório' },
  Homologation: { color: 'bg-violet-100 text-violet-700 border-violet-200 dark:bg-violet-900/40 dark:text-violet-300 dark:border-violet-800', label: 'Homologação' }
}

const customerTags = computed(() =>
  props.demand.customers ?? []
)

const isAdditionalDemand = computed(() => props.demand.type === 'Additional')
const isBacklogQuarterDemand = computed(() => props.demand.quarterYear === 0 && props.demand.quarterNumber === 0)
const hasInconsistentDependency = computed(() =>
  props.demand.dependsOn.some(dependency => isDependencyInconsistent(dependency))
)

const statusTooltip = computed(() => {
  const notes = []
  if (props.demand.isBlocked && props.demand.blockedReason)
    notes.push(`Impedimento\n${props.demand.blockedReason}`)
  if (props.demand.status === 'Deprioritized' && props.demand.observation)
    notes.push(`Despriorização\n${props.demand.observation}`)
  return notes.join('\n\n')
})

function formatDependencySummary(dependency: RoadmapDemand['dependsOn'][number]) {
  return `${dependency.projectName} · ${dependency.title}`
}

function formatDependencyBadgeLabel(prefix: 'Bloqueado por' | 'Bloqueia', dependency: RoadmapDemand['dependsOn'][number]) {
  return `${prefix} ${dependency.projectName}`
}

function compareQuarterPosition(
  demand: Pick<RoadmapDemand, 'quarterYear' | 'quarterNumber'>,
  dependency: Pick<RoadmapDemand['dependsOn'][number], 'quarterYear' | 'quarterNumber'>
) {
  if (demand.quarterYear !== dependency.quarterYear)
    return demand.quarterYear - dependency.quarterYear

  return demand.quarterNumber - dependency.quarterNumber
}

function isDependencyInconsistent(dependency: RoadmapDemand['dependsOn'][number]) {
  if (isBacklogQuarterDemand.value)
    return false

  const isDependencyBacklog = dependency.quarterYear === 0 && dependency.quarterNumber === 0
  if (isDependencyBacklog)
    return true

  return compareQuarterPosition(props.demand, dependency) < 0
}

function getDependencyTooltip(prefix: 'É bloqueado por' | 'Bloqueia', dependency: RoadmapDemand['dependsOn'][number]) {
  return `${prefix} ${dependency.projectName}: ${dependency.title}`
}
</script>

<template>
  <div
    class="rounded-xl border p-3.5 space-y-2.5 transition-colors group"
    :class="hasInconsistentDependency
      ? 'border-red-300 bg-red-50/80 shadow-[0_0_0_1px_rgba(239,68,68,0.08)] hover:border-red-400 dark:border-red-800 dark:bg-red-950/20'
      : 'bg-default border-default hover:border-primary/40'"
  >
    <div v-if="isBacklogQuarterDemand || isAdditionalDemand" class="flex flex-wrap items-center gap-1.5">
      <div
        v-if="isBacklogQuarterDemand"
        class="inline-flex items-center rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-700 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300"
      >
        BACKLOG
      </div>
      <div
        v-if="isAdditionalDemand"
        class="inline-flex items-center rounded-full border border-red-200 bg-red-50 px-2 py-0.5 text-xs font-medium text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300"
      >
        NÃO COMPROMETIDO
      </div>
    </div>

    <!-- Header: status + blocked badge + actions -->
    <div class="flex items-start justify-between gap-2">
      <div class="flex items-start gap-2 min-w-0">
        <div class="flex items-center gap-1.5 min-w-0 flex-wrap" :title="statusTooltip || undefined">
          <span class="inline-block h-2.5 w-2.5 shrink-0 rounded-full" :class="statusConfig[demand.status].dot" />
          <span class="text-xs truncate" :class="statusConfig[demand.status].color">
            {{ statusConfig[demand.status].label }}
          </span>
          <span
            v-if="demand.isBlocked"
            class="flex items-center gap-0.5 text-xs bg-red-100 text-red-600 dark:bg-red-900/40 dark:text-red-400 rounded-full px-1.5 py-0.5 font-medium"
          >
            <UIcon name="i-lucide-triangle-alert" class="w-3 h-3" />
            Impedido
          </span>
          <span
            v-if="statusTooltip"
            class="inline-flex items-center justify-center rounded-full border border-amber-200 bg-amber-50 px-1.5 py-0.5 text-[11px] font-medium text-amber-700 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300"
          >
            <UIcon name="i-lucide-message-square-warning" class="w-3 h-3" />
          </span>
        </div>
      </div>
      <div class="flex items-start gap-1.5 shrink-0">
        <span class="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium max-w-[150px] truncate" :class="classificationConfig[demand.classification].color">
          {{ classificationConfig[demand.classification].label }}
        </span>
        <div class="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
          <UPopover v-if="isBacklogQuarterDemand && planningQuarterOptions?.length">
            <UButton
              size="xs"
              variant="ghost"
              color="primary"
            >
              <UIcon name="i-lucide-calendar-range" class="w-4 h-4" />
            </UButton>
            <template #content>
              <div class="py-1 min-w-[200px]">
                <button
                  v-for="option in planningQuarterOptions"
                  :key="option.value"
                  class="w-full px-3 py-2 text-left text-sm text-highlighted transition-colors hover:bg-elevated"
                  @click="$emit('plan', demand, option.value)"
                >
                  {{ option.label }}
                </button>
              </div>
            </template>
          </UPopover>

          <UButton
            size="xs"
            variant="ghost"
            color="neutral"
            @click="$emit('edit', demand)"
          >
            <UIcon name="i-lucide-pencil" class="w-4 h-4" />
          </UButton>
          <UButton
            icon="i-lucide-trash-2"
            size="xs"
            variant="ghost"
            color="error"
            @click="$emit('delete', demand.id)"
          />
        </div>
      </div>
    </div>

    <!-- Title -->
    <p class="text-sm font-medium text-highlighted leading-snug" :title="demand.description || undefined">
      {{ demand.title }}
    </p>

    <div v-if="demand.dependsOn.length || demand.dependedOnBy.length" class="space-y-1.5">
      <div v-if="demand.dependsOn.length" class="flex flex-wrap gap-1">
          <button
            v-for="dependency in demand.dependsOn.slice(0, 2)"
            :key="dependency.demandId"
            type="button"
            class="inline-flex max-w-full cursor-pointer items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium transition-colors hover:brightness-[0.97]"
            :class="isDependencyInconsistent(dependency)
              ? 'border border-red-200 bg-red-50 text-red-700 dark:border-red-800 dark:bg-red-900/30 dark:text-red-300'
              : 'border border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300'"
            :title="`${getDependencyTooltip('É bloqueado por', dependency)}${isDependencyInconsistent(dependency) ? `\n\nInconsistência: a demanda vinculada está em ${dependency.quarterLabel}, depois de ${demand.quarterLabel}, ou sem priorização.` : ''}`"
            @click.stop="emit('dependencyClick', dependency)"
          >
            <UIcon name="i-lucide-link" class="h-3 w-3 shrink-0" />
            <span class="min-w-0 max-w-[14rem] truncate">{{ formatDependencyBadgeLabel('Bloqueado por', dependency) }}</span>
            <UIcon v-if="isDependencyInconsistent(dependency)" name="i-lucide-triangle-alert" class="h-3 w-3 shrink-0" />
            <span v-if="isDependencyInconsistent(dependency)" class="shrink-0 font-semibold">Inconsistente</span>
          </button>
          <span v-if="demand.dependsOn.length > 2" class="text-[11px] text-muted">+{{ demand.dependsOn.length - 2 }}</span>
      </div>

      <div v-if="demand.dependedOnBy.length" class="flex flex-wrap gap-1">
          <button
            v-for="dependency in demand.dependedOnBy.slice(0, 2)"
            :key="dependency.demandId"
            type="button"
            class="inline-flex max-w-full cursor-pointer items-center gap-1 rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-[11px] font-medium text-amber-700 transition-colors hover:border-amber-300 hover:bg-amber-100 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300 dark:hover:border-amber-700 dark:hover:bg-amber-900/50"
            :title="getDependencyTooltip('Bloqueia', dependency)"
            @click.stop="emit('dependencyClick', dependency)"
          >
            <UIcon name="i-lucide-link" class="h-3 w-3 shrink-0" />
            <span class="min-w-0 max-w-[14rem] truncate">{{ formatDependencyBadgeLabel('Bloqueia', dependency) }}</span>
          </button>
          <span v-if="demand.dependedOnBy.length > 2" class="text-[11px] text-muted">+{{ demand.dependedOnBy.length - 2 }}</span>
      </div>
    </div>

    <!-- Quarter + Type -->
    <div class="flex items-center gap-1.5 flex-wrap">
      <span
        class="text-xs border rounded-full px-2 py-0.5"
        :class="isBacklogQuarterDemand
          ? 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-800 dark:bg-amber-900/30 dark:text-amber-300'
          : 'border-default bg-elevated text-muted'"
      >
        {{ demand.quarterLabel }}
      </span>
      <span
        class="text-xs rounded-full px-2 py-0.5 font-medium"
        :class="typeConfig[demand.type].color"
      >
        {{ typeConfig[demand.type].label }}
      </span>
    </div>

    <!-- Products -->
    <div
      v-if="demand.products.length"
      class="flex flex-wrap gap-1"
    >
      <span
        v-for="p in demand.products"
        :key="p.productId"
        class="text-xs bg-primary/10 text-primary rounded-full px-2 py-0.5"
      >
        {{ p.name }}
      </span>
    </div>

    <!-- Extra metadata -->
    <div class="flex flex-wrap items-center gap-2 pt-0.5">
      <span
        v-if="demand.jiraIssue"
        class="flex items-center gap-1 text-xs text-blue-600 dark:text-blue-400 font-mono"
      >
        <UIcon name="i-lucide-link" class="w-3 h-3" />
        {{ demand.jiraIssue }}
      </span>
      <span
        v-if="demand.hours"
        class="flex items-center gap-1 text-xs text-muted"
      >
        <UIcon name="i-lucide-clock" class="w-3 h-3" />
        {{ demand.hours }}h
      </span>
      <span
        v-if="demand.deliveryDate"
        class="flex items-center gap-1 text-xs text-green-600 dark:text-green-400"
      >
        <UIcon name="i-lucide-calendar-check" class="w-3 h-3" />
        {{ demand.deliveryDate }}
      </span>
      <div
        v-if="customerTags.length"
        class="flex items-center gap-1 text-xs text-muted"
      >
        <UIcon name="i-lucide-users" class="w-3 h-3" />
        <div class="flex flex-wrap gap-1">
          <span
            v-for="customer in customerTags"
            :key="customer"
            class="rounded-full border border-default bg-elevated px-2 py-0.5 text-xs text-muted"
          >
            {{ customer }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>
