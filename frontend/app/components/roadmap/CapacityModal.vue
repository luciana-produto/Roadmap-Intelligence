<script setup lang="ts">
import type { CapacityFormData } from '~/types/roadmap'

const props = defineProps<{
  open: boolean
  projectName?: string
  quarterLabel?: string
  initialValue?: CapacityFormData | null
  isSaving?: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  submit: [data: CapacityFormData]
}>()

const form = reactive<CapacityFormData>({
  projectId: '',
  quarterYear: new Date().getFullYear(),
  quarterNumber: 1,
  capacityHours: 0,
  technicalDebtPercent: 20,
  observation: ''
})

const hasValidCapacityHours = computed(() => Number.isFinite(form.capacityHours) && form.capacityHours >= 0)
const techDebtPercent = computed(() => {
  const p = Number(form.technicalDebtPercent)
  return Number.isFinite(p) ? Math.min(Math.max(p, 0), 100) : 0
})
const hasValidTechDebtPercent = computed(() => {
  const p = Number(form.technicalDebtPercent)
  return Number.isFinite(p) && p >= 0 && p <= 100
})
// Os 3 valores derivados exibidos na configuração.
const techDebtHours = computed(() => (Number(form.capacityHours) || 0) * techDebtPercent.value / 100)
const projectsHours = computed(() => Math.max((Number(form.capacityHours) || 0) - techDebtHours.value, 0))
function fmtHours(n: number) {
  return `${n.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}h`
}

watch(() => props.open, (open) => {
  if (!open || !props.initialValue) return

  form.projectId = props.initialValue.projectId
  form.quarterYear = props.initialValue.quarterYear
  form.quarterNumber = props.initialValue.quarterNumber
  form.capacityHours = props.initialValue.capacityHours
  form.technicalDebtPercent = props.initialValue.technicalDebtPercent ?? 20
  form.observation = props.initialValue.observation ?? ''
})

const isSubmitDisabled = computed(() => !form.projectId || !hasValidCapacityHours.value || !hasValidTechDebtPercent.value)

function handleSubmit() {
  if (isSubmitDisabled.value) return

  emit('submit', {
    projectId: form.projectId,
    quarterYear: form.quarterYear,
    quarterNumber: form.quarterNumber,
    capacityHours: form.capacityHours,
    technicalDebtPercent: techDebtPercent.value,
    observation: form.observation?.trim() || undefined
  })
}
</script>

<template>
  <UModal
    :open="open"
    title="Configurar capacity"
    :description="projectName && quarterLabel ? `${projectName} · ${quarterLabel}` : 'Defina o capacity do quarter em horas.'"
    @update:open="emit('update:open', $event)"
  >
    <template #body>
      <form class="space-y-4" @submit.prevent="handleSubmit">
        <UFormField label="Capacity em horas" required>
          <UInput
            v-model.number="form.capacityHours"
            type="number"
            min="0"
            step="0.5"
            placeholder="Ex: 320"
            class="w-full"
            :class="!hasValidCapacityHours ? 'ring-2 ring-red-400' : ''"
          />
          <p v-if="!hasValidCapacityHours" class="mt-1 text-xs text-red-500">
            Informe um valor igual ou maior que zero.
          </p>
        </UFormField>

        <UFormField label="% para Débito Técnico" required>
          <UInput
            v-model.number="form.technicalDebtPercent"
            type="number"
            min="0"
            max="100"
            step="1"
            placeholder="Ex: 20"
            class="w-full"
            :class="!hasValidTechDebtPercent ? 'ring-2 ring-red-400' : ''"
          />
          <p class="mt-1 text-xs text-muted">Parte do capacity reservada a Débito Técnico (0 a 100%).</p>
        </UFormField>

        <!-- 3 valores derivados -->
        <div class="grid grid-cols-3 gap-2">
          <div class="rounded-lg border border-default bg-elevated/40 px-3 py-2 text-center">
            <p class="text-[11px] uppercase tracking-wide text-muted">Capacity total</p>
            <p class="mt-0.5 text-sm font-semibold text-highlighted">{{ fmtHours(Number(form.capacityHours) || 0) }}</p>
          </div>
          <div class="rounded-lg border border-violet-200 bg-violet-50 px-3 py-2 text-center dark:border-violet-900/50 dark:bg-violet-900/20">
            <p class="text-[11px] uppercase tracking-wide text-violet-700 dark:text-violet-300">Débito Técnico</p>
            <p class="mt-0.5 text-sm font-semibold text-violet-700 dark:text-violet-300">{{ fmtHours(techDebtHours) }}</p>
          </div>
          <div class="rounded-lg border border-blue-200 bg-blue-50 px-3 py-2 text-center dark:border-blue-900/50 dark:bg-blue-900/20">
            <p class="text-[11px] uppercase tracking-wide text-blue-700 dark:text-blue-300">Projetos</p>
            <p class="mt-0.5 text-sm font-semibold text-blue-700 dark:text-blue-300">{{ fmtHours(projectsHours) }}</p>
          </div>
        </div>

        <UFormField label="Observação">
          <UTextarea
            v-model="form.observation"
            :rows="3"
            class="w-full"
            placeholder="Opcional: explique o contexto do capacity deste quarter"
          />
        </UFormField>
      </form>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          variant="outline"
          color="neutral"
          label="Cancelar"
          @click="emit('update:open', false)"
        />
        <UButton
          color="primary"
          icon="i-lucide-save"
          :loading="isSaving"
          :disabled="isSubmitDisabled"
          label="Salvar capacity"
          @click="handleSubmit"
        />
      </div>
    </template>
  </UModal>
</template>