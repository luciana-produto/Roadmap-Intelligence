<script setup lang="ts">
import type { BulkEditRoadmapItemsData, DemandDependencyOption, DeprioritizationReason, DemandStatus, DemandType, RoadmapDemand, SpilloverReason } from '~/types/roadmap'
import { SPILLOVER_REASON_OPTIONS, isDeliveryLate } from '~/utils/roadmapDelay'

const delayReasonOptions = SPILLOVER_REASON_OPTIONS

const LIST_ROW_COLORS = [
  { id: 'red',    label: 'Vermelho', hex: '#ef4444' },
  { id: 'orange', label: 'Laranja',  hex: '#f97316' },
  { id: 'amber',  label: 'Âmbar',   hex: '#f59e0b' },
  { id: 'green',  label: 'Verde',   hex: '#22c55e' },
  { id: 'blue',   label: 'Azul',    hex: '#3b82f6' },
  { id: 'violet', label: 'Roxo',    hex: '#8b5cf6' },
  { id: 'pink',   label: 'Rosa',    hex: '#ec4899' },
] as const
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
  hideRowColor?: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  submit: [payload: BulkEditRoadmapItemsData]
}>()

const statusOptions: Array<{ value: DemandStatus, label: string }> = [
  { value: 'Backlog', label: 'Backlog' },
  { value: 'InProgress', label: 'Doing' },
  { value: 'Prioritized', label: 'Priorizado' },
  { value: 'UX', label: 'UX' },
  { value: 'Done', label: 'Concluído' },
  { value: 'Deprioritized', label: 'Despriorizado' },
  { value: 'Blocked', label: 'Impedido' },
  { value: 'Spillover', label: 'Transbordo' }
]

const typeOptions: Array<{ value: DemandType, label: string }> = [
  { value: 'Planned', label: 'Planejado' },
  { value: 'Spillover', label: 'Transbordo' },
  { value: 'Unplanned', label: 'Não Planejado' },
  { value: 'Additional', label: 'Adicional' }
]

const deprioritizationReasonOptions: Array<{ value: DeprioritizationReason, label: string }> = [
  { value: 'StrategyChange', label: 'Mudança de estratégia' },
  { value: 'HigherValuePrioritization', label: 'Priorização de maior valor' },
  { value: 'LowCustomerDemand', label: 'Baixa demanda de clientes' },
  { value: 'LowExpectedReturn', label: 'Baixo retorno esperado' },
  { value: 'BusinessDefinitionDependency', label: 'Dependência de definição de negócio' },
  { value: 'AlternativeSolutionAvailable', label: 'Solução alternativa disponível' },
  { value: 'RegulatoryRequirementChanged', label: 'Requisito regulatório alterado' },
  { value: 'CustomerWithdrew', label: 'Cliente desistiu' },
  { value: 'ReplacedByOtherInitiative', label: 'Substituída por outra iniciativa' },
  { value: 'UndefinedScope', label: 'Escopo indefinido' }
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

// Destino do transbordo: apenas quarters reais (backlog não é destino válido).
const spilloverQuarterOptions = buildPreRegisteredQuarterYears(currentYear, PRE_REGISTERED_QUARTER_END_YEAR).flatMap(year =>
  [1, 2, 3, 4].map(number => ({ value: buildQuarterValue(year, number), label: formatQuarterLabel(year, number) }))
)
// Próximo quarter a partir de hoje (default do destino do transbordo).
const currentQuarterNumber = Math.floor(now.getMonth() / 3) + 1
const defaultSpilloverTarget = currentQuarterNumber === 4
  ? buildQuarterValue(currentYear + 1, 1)
  : buildQuarterValue(currentYear, currentQuarterNumber + 1)

const applyStatus = ref(false)
const applyPromisedDate = ref(false)
const applyType = ref(false)
const applyQuarter = ref(false)
const applyRowColor = ref(false)
const rowColor = ref<string | null>(null)
const status = ref<DemandStatus | undefined>()
const promisedDate = ref('')
const deliveryDate = ref('')
const delayReason = ref<SpilloverReason | undefined>()
const delayObservation = ref('')
const type = ref<DemandType | undefined>()
const observation = ref('')
const deprioritizationReason = ref<DeprioritizationReason | undefined>()
const replacementDemandId = ref('')
const blockedReason = ref('')
const selectedQuarter = ref('')
const spilloverReason = ref<SpilloverReason | undefined>()
const spilloverObservation = ref('')
const spilloverTarget = ref('')

// Transbordo: só épicos simples e demandas, que ainda não sejam transbordo.
const spilloverEligibleItems = computed(() =>
  props.selectedItems.filter(item =>
    (item.itemType === 'Demand' || (item.itemType === 'Epic' && item.isSimple))
    && item.status !== 'Spillover'
    && !item.successorDemandId)
)
const spilloverSkippedCount = computed(() => props.selectedItems.length - spilloverEligibleItems.value.length)
const isSpilloverStatus = computed(() => applyStatus.value && status.value === 'Spillover')

const selectedDemandCount = computed(() => props.selectedItems.filter(item => item.itemType === 'Demand').length)
const selectedEpicCount = computed(() => props.selectedItems.filter(item => item.itemType === 'Epic').length)
const selectedSimpleEpicCount = computed(() => props.selectedItems.filter(item => item.itemType === 'Epic' && item.isSimple).length)
const hasSelectedDemands = computed(() => selectedDemandCount.value > 0)
const hasTypeQuarterEditable = computed(() => selectedDemandCount.value > 0 || selectedSimpleEpicCount.value > 0)
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

// Algum item selecionado ficaria atrasado com a data de entrega informada?
// (usa a data prometida nova, se estiver sendo aplicada; senão a do próprio item)
const bulkDeliveryLate = computed(() => {
  if (!applyStatus.value || status.value !== 'Done' || !deliveryDate.value)
    return false
  return props.selectedItems.some((item) => {
    const effectivePromised = applyPromisedDate.value ? promisedDate.value : (item.promisedDate ?? '')
    return isDeliveryLate(deliveryDate.value, effectivePromised, item.quarterYear, item.quarterNumber)
  })
})

const missingSubmitReason = computed(() => {
  if (!props.selectedItems.length)
    return 'Selecione ao menos um épico ou demanda'

  const hasAnyApply = applyStatus.value || applyPromisedDate.value || applyType.value || applyQuarter.value || (!props.hideRowColor && applyRowColor.value)
  if (!hasAnyApply)
    return 'Selecione ao menos um campo para alterar'

  if (applyStatus.value && !status.value)
    return 'Selecione o status'

  if (applyStatus.value && status.value === 'Done' && !deliveryDate.value)
    return 'Informe a data de entrega para concluir os itens'

  if (bulkDeliveryLate.value && !delayReason.value)
    return 'Selecione o motivo do atraso'

  if (applyStatus.value && status.value === 'Blocked' && !blockedReason.value.trim())
    return 'Preencha o motivo do impedimento'

  if (applyStatus.value && status.value === 'Deprioritized' && !deprioritizationReason.value)
    return 'Selecione o motivo da despriorização'

  if (applyStatus.value && status.value === 'Deprioritized' && (deprioritizationReason.value === 'ReplacedByOtherInitiative' || deprioritizationReason.value === 'HigherValuePrioritization') && !replacementDemandId.value)
    return 'Selecione a demanda priorizada no lugar'

  if (applyStatus.value && status.value === 'Deprioritized' && !observation.value.trim())
    return 'Preencha a observação da despriorização'

  if (isSpilloverStatus.value && !spilloverEligibleItems.value.length)
    return 'Nenhum épico simples ou demanda elegível para transbordo'
  if (isSpilloverStatus.value && !spilloverReason.value)
    return 'Selecione o motivo do transbordo'
  if (isSpilloverStatus.value && !spilloverTarget.value)
    return 'Selecione o quarter de destino do transbordo'

  if (applyType.value && !hasTypeQuarterEditable.value)
    return 'Não há demandas ou épicos simples selecionados para alterar o tipo'

  if (applyType.value && !type.value)
    return 'Selecione o tipo da demanda'

  if (applyQuarter.value && !hasTypeQuarterEditable.value)
    return 'Não há demandas ou épicos simples selecionados para alterar o quarter'

  if (applyQuarter.value && !selectedQuarter.value)
    return 'Selecione o quarter das demandas'

  return null
})

function resetState() {
  applyStatus.value = false
  applyPromisedDate.value = false
  applyType.value = false
  applyQuarter.value = false
  applyRowColor.value = false
  rowColor.value = null
  status.value = undefined
  promisedDate.value = ''
  deliveryDate.value = ''
  delayReason.value = undefined
  delayObservation.value = ''
  type.value = undefined
  observation.value = ''
  deprioritizationReason.value = undefined
  replacementDemandId.value = ''
  blockedReason.value = ''
  selectedQuarter.value = ''
  spilloverReason.value = undefined
  spilloverObservation.value = ''
  spilloverTarget.value = ''
}

watch(() => props.open, (open) => {
  if (open) {
    resetState()
    return
  }

  resetState()
})

watch(hasTypeQuarterEditable, (hasEditable) => {
  if (hasEditable)
    return

  applyType.value = false
  applyQuarter.value = false
  type.value = undefined
  selectedQuarter.value = ''
})

watch(status, (value) => {
  if (value !== 'Done') {
    delayReason.value = undefined
    delayObservation.value = ''
  }

  if (value !== 'Spillover') {
    spilloverReason.value = undefined
    spilloverObservation.value = ''
    spilloverTarget.value = ''
  }

  if (value === 'Spillover') {
    // Transbordo é exclusivo: cria cópias, então desabilita as demais alterações do lote.
    applyPromisedDate.value = false
    applyType.value = false
    applyQuarter.value = false
    applyRowColor.value = false
    deliveryDate.value = ''
    blockedReason.value = ''
    deprioritizationReason.value = undefined
    replacementDemandId.value = ''
    observation.value = ''
    if (!spilloverTarget.value)
      spilloverTarget.value = defaultSpilloverTarget
    return
  }

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

    if (status.value === 'Done') {
      payload.deliveryDate = deliveryDate.value
      if (delayReason.value)
        payload.delayReason = delayReason.value
      if (delayObservation.value.trim())
        payload.delayObservation = delayObservation.value.trim()
    }

    if (status.value === 'Blocked')
      payload.blockedReason = blockedReason.value.trim()

    if (status.value === 'Deprioritized') {
      payload.observation = observation.value.trim()
      payload.deprioritizationReason = deprioritizationReason.value

      if (replacementDemandId.value)
        payload.replacementDemandId = replacementDemandId.value
    }

    if (status.value === 'Spillover') {
      payload.spilloverReason = spilloverReason.value
      payload.spilloverObservation = spilloverObservation.value.trim() || undefined
      const { quarterYear, quarterNumber } = parseQuarterValue(spilloverTarget.value)
      payload.spilloverTargetYear = quarterYear
      payload.spilloverTargetNumber = quarterNumber
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

  if (applyRowColor.value)
    payload.rowColor = rowColor.value

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
          <p v-if="hasTypeQuarterEditable" class="mt-1">Quarter e tipo se aplicam às demandas e épicos simples selecionados.</p>
        </div>

        <div class="grid gap-4 md:grid-cols-2">
          <div v-if="!hideRowColor" class="space-y-3 rounded-xl border border-default bg-default p-3" :class="isSpilloverStatus ? 'opacity-60' : ''">
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="text-sm font-medium text-highlighted">Cor da linha</p>
                <p class="text-xs text-muted">Destaque visual para épicos e demandas.</p>
              </div>
              <USwitch v-model="applyRowColor" :disabled="isSpilloverStatus" />
            </div>

            <div v-if="applyRowColor" class="space-y-2">
              <p class="text-xs text-muted">Selecione uma cor ou remova o destaque:</p>
              <div class="flex flex-wrap gap-2">
                <button
                  type="button"
                  class="flex h-6 w-6 items-center justify-center rounded border-2 transition-colors"
                  :class="rowColor === null ? 'border-primary' : 'border-default hover:border-primary/40'"
                  title="Sem cor"
                  @click="rowColor = null"
                >
                  <UIcon name="i-lucide-x" class="h-3.5 w-3.5 text-muted" />
                </button>
                <button
                  v-for="color in LIST_ROW_COLORS"
                  :key="color.id"
                  type="button"
                  class="h-6 w-6 rounded-full transition-all hover:scale-110"
                  :class="rowColor === color.id ? 'ring-2 ring-offset-1 ring-highlighted' : ''"
                  :style="{ backgroundColor: color.hex }"
                  :title="color.label"
                  @click="rowColor = color.id"
                />
              </div>
            </div>
          </div>

          <div class="space-y-3 rounded-xl border border-default bg-default p-3" :class="isSpilloverStatus ? 'opacity-60' : ''">
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="text-sm font-medium text-highlighted">Data prometida</p>
                <p class="text-xs text-muted">Atualiza o mesmo prazo para todos os itens.</p>
              </div>
              <USwitch v-model="applyPromisedDate" :disabled="isSpilloverStatus" />
            </div>

            <UFormField v-if="applyPromisedDate" label="Nova data prometida">
              <UInput v-model="promisedDate" type="date" class="w-full" />
              <p class="mt-1 text-xs text-muted">
                Deixe em branco para remover a data prometida atual.
              </p>
            </UFormField>
          </div>

          <div class="space-y-3 rounded-xl border border-default bg-default p-3" :class="(!hasTypeQuarterEditable || isSpilloverStatus) ? 'opacity-60' : ''">
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="text-sm font-medium text-highlighted">Tipo da demanda</p>
                <p class="text-xs text-muted">Aplicado às demandas e épicos simples selecionados.</p>
              </div>
              <USwitch v-model="applyType" :disabled="!hasTypeQuarterEditable || isSpilloverStatus" />
            </div>

            <UFormField v-if="applyType" label="Novo tipo" required>
              <USelect v-model="type" :items="typeOptions" value-key="value" option-attribute="label" placeholder="Selecione" class="w-full" />
            </UFormField>
          </div>

          <div class="space-y-3 rounded-xl border border-default bg-default p-3" :class="(!hasTypeQuarterEditable || isSpilloverStatus) ? 'opacity-60' : ''">
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="text-sm font-medium text-highlighted">Quarter da demanda</p>
                <p class="text-xs text-muted">Aplicado às demandas e épicos simples selecionados.</p>
              </div>
              <USwitch v-model="applyQuarter" :disabled="!hasTypeQuarterEditable || isSpilloverStatus" />
            </div>

            <UFormField v-if="applyQuarter" label="Novo quarter" required>
              <USelect v-model="selectedQuarter" :items="quarterOptions" value-key="value" option-attribute="label" placeholder="Selecione" class="w-full" />
            </UFormField>
          </div>

          <div class="space-y-3 rounded-xl border border-default bg-default p-3 md:col-span-2">
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

              <template v-if="bulkDeliveryLate">
                <div class="flex items-center gap-1.5 rounded-md border border-amber-300 bg-amber-50 px-3 py-1.5 text-xs text-amber-700 dark:border-amber-800/60 dark:bg-amber-900/20 dark:text-amber-300">
                  <UIcon name="i-lucide-triangle-alert" class="h-4 w-4 shrink-0" />
                  Há itens entregues após o prazo — informe o motivo do atraso.
                </div>
                <UFormField label="Motivo do atraso" required>
                  <USelect v-model="delayReason" :items="delayReasonOptions" value-key="value" option-attribute="label" placeholder="Selecione" class="w-full" />
                </UFormField>
                <UFormField label="Observação atraso">
                  <UTextarea v-model="delayObservation" :rows="2" placeholder="Detalhe o atraso (opcional)" class="w-full" />
                </UFormField>
              </template>

              <UFormField v-if="status === 'Blocked'" label="Motivo do impedimento" required>
                <UInput v-model="blockedReason" placeholder="Descreva o motivo do impedimento" class="w-full" />
              </UFormField>

              <template v-if="status === 'Deprioritized'">
                <UFormField label="Motivo da despriorização" required>
                  <USelect v-model="deprioritizationReason" :items="deprioritizationReasonOptions" value-key="value" option-attribute="label" placeholder="Selecione" class="w-full" />
                </UFormField>

                <UFormField
                  label="Demanda priorizada no lugar"
                  :hint="(deprioritizationReason === 'ReplacedByOtherInitiative' || deprioritizationReason === 'HigherValuePrioritization') ? undefined : 'Opcional'"
                  :required="deprioritizationReason === 'ReplacedByOtherInitiative' || deprioritizationReason === 'HigherValuePrioritization'"
                >
                  <USelect v-model="replacementDemandId" :items="replacementDemandOptions" value-key="value" option-attribute="label" placeholder="Selecione uma demanda" class="w-full" />
                </UFormField>

                <UFormField label="Observação" required>
                  <UTextarea v-model="observation" :rows="3" class="w-full" />
                </UFormField>
              </template>

              <template v-if="status === 'Spillover'">
                <div v-if="spilloverSkippedCount" class="flex items-start gap-1.5 rounded-md border border-amber-300 bg-amber-50 px-3 py-1.5 text-xs text-amber-700 dark:border-amber-800/60 dark:bg-amber-900/20 dark:text-amber-300">
                  <UIcon name="i-lucide-triangle-alert" class="mt-0.5 h-4 w-4 shrink-0" />
                  <span>{{ spilloverSkippedCount }} {{ spilloverSkippedCount === 1 ? 'item não elegível será ignorado' : 'itens não elegíveis serão ignorados' }} (épicos compostos ou já em transbordo). O transbordo vale só para épicos simples e demandas.</span>
                </div>
                <UFormField label="Motivo do transbordo" required>
                  <USelect v-model="spilloverReason" :items="delayReasonOptions" value-key="value" option-attribute="label" placeholder="Selecione" class="w-full" />
                </UFormField>
                <UFormField label="Observação do transbordo" hint="Opcional">
                  <UTextarea v-model="spilloverObservation" :rows="2" placeholder="Detalhe o transbordo (opcional)" class="w-full" />
                </UFormField>
                <UFormField label="Quarter de destino" required>
                  <USelect v-model="spilloverTarget" :items="spilloverQuarterOptions" value-key="value" option-attribute="label" placeholder="Selecione" class="w-full" />
                </UFormField>
                <p class="text-[11px] text-muted">Cria uma cópia de transbordo de cada item elegível no quarter escolhido; os originais viram Transbordo.</p>
              </template>
            </div>
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