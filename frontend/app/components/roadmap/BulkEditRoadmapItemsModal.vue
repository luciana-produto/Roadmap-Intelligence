<script setup lang="ts">
import type { BulkEditRoadmapItemsData, DemandDependencyOption, DeprioritizationReason, DemandStatus, DemandType, RoadmapDemand } from '~/types/roadmap'
import {
  BACKLOG_QUARTER,
  PRIORITIZED_BACKLOG_QUARTER,
  PRE_REGISTERED_QUARTER_END_YEAR,
  buildPreRegisteredQuarterYears,
  buildQuarterValue,
  formatQuarterLabel,
  parseQuarterValue
} from '~/utils/roadmapQuarter'

const props = defineProps<{
  open: boolean
  isSaving?: boolean
  selectedItems: RoadmapDemand[]
  dependencyOptions?: DemandDependencyOption[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  submit: [payload: BulkEditRoadmapItemsData]
}>()

const statusOptions: Array<{ value: DemandStatus, label: string }> = [
  { value: 'Backlog', label: 'Backlog' },
  { value: 'InProgress', label: 'Doing' },
  { value: 'Done', label: 'Concluído' },
  { value: 'Deprioritized', label: 'Despriorizado' },
  { value: 'Blocked', label: 'Impedido' }
]

const typeOptions: Array<{ value: DemandType, label: string }> = [
  { value: 'Planned', label: 'Planejado' },
  { value: 'Spillover', label: 'Transbordo' },
  { value: 'Unplanned', label: 'Não Planejado' },
  { value: 'Additional', label: 'Adicional' }
]

const deprioritizationReasonOptions: Array<{ value: DeprioritizationReason, label: string }> = [
  { value: 'Strategic', label: 'Estratégico' },
  { value: 'MandatoryUrgent', label: 'Mandatório/Urgente' },
  { value: 'LowImpact', label: 'Baixo impacto' },
  { value: 'LackOfCapacity', label: 'Falta de capacidade' },
  { value: 'ContextChange', label: 'Mudança de contexto' },
  { value: 'Customizacao', label: 'Customização' }
]

const now = new Date()
const currentYear = now.getFullYear()
const quarterOptions = [
  { value: BACKLOG_QUARTER.value, label: BACKLOG_QUARTER.label },
  { value: PRIORITIZED_BACKLOG_QUARTER.value, label: PRIORITIZED_BACKLOG_QUARTER.label },
  ...buildPreRegisteredQuarterYears(currentYear, PRE_REGISTERED_QUARTER_END_YEAR).flatMap(year =>
    [1, 2, 3, 4].map(number => ({
      value: buildQuarterValue(year, number),
      label: formatQuarterLabel(year, number)
    }))
  )
]

const applyStatus = ref(false)
const applyPromisedDate = ref(false)
const applyType = ref(false)
const applyQuarter = ref(false)
const status = ref<DemandStatus | undefined>()
const promisedDate = ref('')
const deliveryDate = ref('')
const type = ref<DemandType | undefined>()
const observation = ref('')
const deprioritizationReason = ref<DeprioritizationReason | undefined>()
const replacementDemandId = ref('')
const blockedReason = ref('')
const selectedQuarter = ref('')

const selectedDemandCount = computed(() => props.selectedItems.filter(item => item.itemType === 'Demand').length)
const selectedEpicCount = computed(() => props.selectedItems.filter(item => item.itemType === 'Epic').length)
const hasSelectedDemands = computed(() => selectedDemandCount.value > 0)
const replacementDemandOptions = computed(() => {
  const selectedItemIds = new Set(props.selectedItems.map(item => item.id))

  return (props.dependencyOptions ?? [])
    .filter(option => !selectedItemIds.has(option.demandId))
    .map(option => ({
      value: option.demandId,
      label: `${option.projectName} · ${option.title}`
    }))
})

const selectionSummary = computed(() => {
  const parts: string[] = []

  if (selectedEpicCount.value)
    parts.push(`${selectedEpicCount.value} épico${selectedEpicCount.value > 1 ? 's' : ''}`)

  if (selectedDemandCount.value)
    parts.push(`${selectedDemandCount.value} demanda${selectedDemandCount.value > 1 ? 's' : ''}`)

  return parts.join(' e ')
})

const missingSubmitReason = computed(() => {
  if (!props.selectedItems.length)
    return 'Selecione ao menos um épico ou demanda'

  if (!applyStatus.value && !applyPromisedDate.value && !applyType.value && !applyQuarter.value)
    return 'Selecione ao menos um campo para alterar'

  if (applyStatus.value && !status.value)
    return 'Selecione o status'

  if (applyStatus.value && status.value === 'Done' && !deliveryDate.value)
    return 'Informe a data de entrega para concluir os itens'

  if (applyStatus.value && status.value === 'Blocked' && !blockedReason.value.trim())
    return 'Preencha o motivo do impedimento'

  if (applyStatus.value && status.value === 'Deprioritized' && !deprioritizationReason.value)
    return 'Selecione o motivo da despriorização'

  if (applyStatus.value && status.value === 'Deprioritized' && !observation.value.trim())
    return 'Preencha a observação da despriorização'

  if (applyType.value && !hasSelectedDemands.value)
    return 'Não há demandas selecionadas para alterar o tipo'

  if (applyType.value && !type.value)
    return 'Selecione o tipo da demanda'

  if (applyQuarter.value && !hasSelectedDemands.value)
    return 'Não há demandas selecionadas para alterar o quarter'

  if (applyQuarter.value && !selectedQuarter.value)
    return 'Selecione o quarter das demandas'

  return null
})

function resetState() {
  applyStatus.value = false
  applyPromisedDate.value = false
  applyType.value = false
  applyQuarter.value = false
  status.value = undefined
  promisedDate.value = ''
  deliveryDate.value = ''
  type.value = undefined
  observation.value = ''
  deprioritizationReason.value = undefined
  replacementDemandId.value = ''
  blockedReason.value = ''
  selectedQuarter.value = ''
}

watch(() => props.open, (open) => {
  if (open) {
    resetState()
    return
  }

  resetState()
})

watch(hasSelectedDemands, (hasDemands) => {
  if (hasDemands)
    return

  applyType.value = false
  applyQuarter.value = false
  type.value = undefined
  selectedQuarter.value = ''
})

watch(status, (value) => {
  if (value === 'Done') {
    blockedReason.value = ''
    deprioritizationReason.value = undefined
    replacementDemandId.value = ''
    return
  }

  if (value === 'Blocked') {
    deliveryDate.value = ''
    deprioritizationReason.value = undefined
    replacementDemandId.value = ''
    observation.value = ''
    return
  }

  if (value === 'Deprioritized') {
    deliveryDate.value = ''
    blockedReason.value = ''
    return
  }

  blockedReason.value = ''
  deprioritizationReason.value = undefined
  replacementDemandId.value = ''
})

function closeModal() {
  emit('update:open', false)
}

function handleSubmit() {
  if (missingSubmitReason.value || props.isSaving)
    return

  const payload: BulkEditRoadmapItemsData = {}

  if (applyStatus.value && status.value) {
    payload.status = status.value

    if (status.value === 'Done')
      payload.deliveryDate = deliveryDate.value

    if (status.value === 'Blocked')
      payload.blockedReason = blockedReason.value.trim()

    if (status.value === 'Deprioritized') {
      payload.observation = observation.value.trim()
      payload.deprioritizationReason = deprioritizationReason.value

      if (replacementDemandId.value)
        payload.replacementDemandId = replacementDemandId.value
    }
  }

  if (applyPromisedDate.value)
    payload.promisedDate = promisedDate.value

  if (applyType.value && type.value)
    payload.type = type.value

  if (applyQuarter.value && selectedQuarter.value) {
    const { quarterYear, quarterNumber } = parseQuarterValue(selectedQuarter.value)
    payload.quarterYear = quarterYear
    payload.quarterNumber = quarterNumber
  }

  emit('submit', payload)
}
</script>

<template>
  <UModal :open="open" :ui="{ content: 'sm:max-w-2xl' }" @update:open="emit('update:open', $event)">
    <template #header>
      <div>
        <h3 class="text-lg font-semibold text-highlighted">Edição em lote</h3>
        <p class="mt-1 text-sm text-muted">
          Aplicar alterações em {{ selectionSummary || 'itens selecionados' }}.
        </p>
      </div>
    </template>

    <template #body>
      <div class="space-y-4 p-4">
        <div class="rounded-2xl bg-elevated/35 px-4 py-3 text-sm text-muted shadow-sm ring-1 ring-inset ring-default/60">
          <p class="text-highlighted">As alterações serão aplicadas igualmente aos itens selecionados.</p>
          <p v-if="hasSelectedDemands" class="mt-1">Quarter e tipo continuam restritos apenas às demandas.</p>
        </div>

        <div class="grid gap-4 md:grid-cols-2">
          <div class="space-y-3 rounded-xl border border-default bg-default p-3">
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="text-sm font-medium text-highlighted">Status</p>
                <p class="text-xs text-muted">Atualiza épicos e demandas.</p>
              </div>
              <USwitch v-model="applyStatus" />
            </div>

            <div v-if="applyStatus" class="space-y-3">
              <UFormField label="Novo status" required>
                <USelect v-model="status" :items="statusOptions" value-key="value" option-attribute="label" placeholder="Selecione" class="w-full" />
              </UFormField>

              <UFormField v-if="status === 'Done'" label="Data de entrega" required>
                <UInput v-model="deliveryDate" type="date" class="w-full" />
              </UFormField>

              <UFormField v-if="status === 'Blocked'" label="Motivo do impedimento" required>
                <UInput v-model="blockedReason" placeholder="Descreva o motivo do impedimento" class="w-full" />
              </UFormField>

              <template v-if="status === 'Deprioritized'">
                <UFormField label="Motivo da despriorização" required>
                  <USelect v-model="deprioritizationReason" :items="deprioritizationReasonOptions" value-key="value" option-attribute="label" placeholder="Selecione" class="w-full" />
                </UFormField>

                <UFormField label="Demanda priorizada no lugar" hint="Opcional">
                  <USelect v-model="replacementDemandId" :items="replacementDemandOptions" value-key="value" option-attribute="label" placeholder="Selecione uma demanda" class="w-full" />
                </UFormField>

                <UFormField label="Observação" required>
                  <UTextarea v-model="observation" :rows="4" class="w-full" />
                </UFormField>
              </template>
            </div>
          </div>

          <div class="space-y-3 rounded-xl border border-default bg-default p-3">
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="text-sm font-medium text-highlighted">Data prometida</p>
                <p class="text-xs text-muted">Atualiza o mesmo prazo para todos os itens.</p>
              </div>
              <USwitch v-model="applyPromisedDate" />
            </div>

            <UFormField v-if="applyPromisedDate" label="Nova data prometida">
              <UInput v-model="promisedDate" type="date" class="w-full" />
              <p class="mt-1 text-xs text-muted">
                Deixe em branco para remover a data prometida atual.
              </p>
            </UFormField>
          </div>

          <div class="space-y-3 rounded-xl border border-default bg-default p-3" :class="!hasSelectedDemands ? 'opacity-60' : ''">
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="text-sm font-medium text-highlighted">Tipo da demanda</p>
                <p class="text-xs text-muted">Aplicado apenas às demandas selecionadas.</p>
              </div>
              <USwitch v-model="applyType" :disabled="!hasSelectedDemands" />
            </div>

            <UFormField v-if="applyType" label="Novo tipo" required>
              <USelect v-model="type" :items="typeOptions" value-key="value" option-attribute="label" placeholder="Selecione" class="w-full" />
            </UFormField>
          </div>

          <div class="space-y-3 rounded-xl border border-default bg-default p-3" :class="!hasSelectedDemands ? 'opacity-60' : ''">
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="text-sm font-medium text-highlighted">Quarter da demanda</p>
                <p class="text-xs text-muted">Aplicado apenas às demandas selecionadas.</p>
              </div>
              <USwitch v-model="applyQuarter" :disabled="!hasSelectedDemands" />
            </div>

            <UFormField v-if="applyQuarter" label="Novo quarter" required>
              <USelect v-model="selectedQuarter" :items="quarterOptions" value-key="value" option-attribute="label" placeholder="Selecione" class="w-full" />
            </UFormField>
          </div>
        </div>

        <p v-if="missingSubmitReason" class="text-sm text-warning">
          {{ missingSubmitReason }}
        </p>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton label="Cancelar" color="neutral" variant="ghost" @click="closeModal" />
        <UButton label="Aplicar alterações" icon="i-lucide-save" :disabled="!!missingSubmitReason" :loading="isSaving" @click="handleSubmit" />
      </div>
    </template>
  </UModal>
</template>