<script setup lang="ts">
import type {
  RoadmapDemand,
  Kpi,
  DemandFormData,
  DemandClassification,
  NoKpiClassification,
  DemandKpiLink,
  DemandKpiLinkInput,
  ConfidenceLevel,
  KpiMeasurement,
  MeasurementResult,
  CreateDemandKpiMeasurementInput,
  UpdateDemandKpiMeasurementInput
} from '~/types/roadmap'

type ImpactDisplayType = 'Percentage' | 'Number' | 'Currency'

type EditableDemandKpiLink = DemandKpiLinkInput & {
  impactDisplayType: ImpactDisplayType
  estimatedImpactInput: string
  observationInput: string
}

type KpiLinkDraftState = EditableDemandKpiLink & {
  draftId: string
}

type MeasurementEditorState = {
  id?: string
  kpiId: string
  measuredValueInput: string
  measurementDate: string
  result: MeasurementResult
  observation: string
}

const props = defineProps<{
  demand?: RoadmapDemand | null
  availableKpis?: Kpi[]
}>()

const kpiStore = useKpiStore()
const roadmapStore = useRoadmapStore()
const toast = useToast()

const formState = reactive({
  hasNoKpi: false,
  noKpiClassification: undefined as NoKpiClassification | undefined
})

const kpiLinkDrafts = ref<KpiLinkDraftState[]>([])
const kpiMeasurements = ref<KpiMeasurement[]>([])
const measurementDrafts = ref<Record<string, MeasurementEditorState>>({})
const isSavingSetup = ref(false)
const kpiLinkSavingDraftId = ref<string | null>(null)
const kpiLinkDeletingId = ref<string | null>(null)
const editingPersistedKpiLinkId = ref<string | null>(null)
const measurementSavingKpiId = ref<string | null>(null)
const measurementDeletingId = ref<string | null>(null)

type AutomaticMeasurementResult = MeasurementResult

const impactTypeOptions = [
  { value: 'Percentage', label: 'Percentual' },
  { value: 'Number', label: 'Número' },
  { value: 'Currency', label: 'Valor R$' }
]

const confidenceLevelOptions = [
  { value: 'High', label: 'Alta' },
  { value: 'Medium', label: 'Média' },
  { value: 'Low', label: 'Baixa' }
]

const noKpiClassificationOptions = [
  { value: 'Relationship', label: 'Relacionamento' },
  { value: 'Mandatory', label: 'Mandatório' },
  { value: 'Technical', label: 'Técnico' }
] as const

const confidenceLevelHelp = [
  {
    title: 'Alta confiança',
    color: 'text-emerald-600 dark:text-emerald-400',
    emoji: '🟢',
    summary: 'Evidência forte',
    signals: 'Já fez antes, benchmark claro ou dado consistente.',
    detail: 'Ex.: Melhorar performance do PDV -> reduz tempo de atendimento.'
  },
  {
    title: 'Média confiança',
    color: 'text-amber-600 dark:text-amber-400',
    emoji: '🟡',
    summary: 'Hipótese razoável',
    signals: 'Feedback de clientes, evidência indireta ou algo ainda não testado.',
    detail: 'Ex.: Melhorar UX do upsell -> aumentar ticket médio.'
  },
  {
    title: 'Baixa confiança',
    color: 'text-rose-600 dark:text-rose-400',
    emoji: '🔴',
    summary: 'Alta incerteza',
    signals: 'Ideia nova, sem dado ou muito dependente do comportamento do usuário.',
    detail: 'Ex.: Novo modelo de recomendação inteligente.'
  }
] as const

watch(() => props.demand, (demand) => {
  formState.hasNoKpi = demand?.hasNoKpi ?? false
  formState.noKpiClassification = demand?.noKpiClassification ?? undefined
  kpiLinkDrafts.value = []
  kpiMeasurements.value = sortMeasurements(demand?.kpiMeasurements ?? [])
  measurementDrafts.value = {}
}, { immediate: true })

watch(() => formState.hasNoKpi, (hasNoKpi) => {
  if (!hasNoKpi)
    formState.noKpiClassification = undefined
})

function buildDemandFormData(demand: RoadmapDemand, overrides?: Partial<DemandFormData>): DemandFormData {
  return {
    title: demand.title,
    description: demand.description ?? '',
    projectId: demand.projectId,
    quarterYear: demand.quarterYear,
    quarterNumber: demand.quarterNumber,
    type: demand.type,
    classification: demand.classification,
    productIds: demand.products.map(product => product.productId),
    status: demand.status,
    observation: demand.observation ?? '',
    jiraIssue: demand.jiraIssue ?? '',
    hours: demand.hours,
    promisedDate: demand.promisedDate ?? '',
    customers: demand.customers ?? [],
    dependencyDemandIds: demand.dependsOn.map(item => item.demandId),
    isBlocked: demand.isBlocked,
    blockedReason: demand.blockedReason ?? '',
    deliveryDate: demand.deliveryDate ?? '',
    problemClarity: demand.problemClarity ?? undefined,
    hasNoKpi: demand.hasNoKpi ?? false,
    noKpiClassification: demand.noKpiClassification ?? undefined,
    ...overrides
  }
}

function getNoKpiClassificationLabel(value: NoKpiClassification | undefined) {
  switch (value) {
    case 'Relationship':
      return 'Relacionamento'
    case 'Mandatory':
      return 'Mandatório'
    case 'Technical':
      return 'Técnico'
    default:
      return ''
  }
}

function computeAutomaticMeasurementResult(kpiId: string, measuredValue: number): AutomaticMeasurementResult {
  const link = persistedKpiLinks.value.find(item => item.kpiId === kpiId)
  const kpi = getKpiById(kpiId)

  if (link?.estimatedImpact == null || !kpi)
    return 'Neutral'

  if (kpi.objective === 'Increase')
    return measuredValue >= link.estimatedImpact ? 'Positive' : 'Negative'

  return measuredValue <= link.estimatedImpact ? 'Positive' : 'Negative'
}

function normalizeDecimalInput(value: string) {
  const trimmed = value.trim()
  if (!trimmed)
    return ''

  if (trimmed.includes(',') && trimmed.includes('.'))
    return trimmed.replace(/\./g, '').replace(',', '.')

  if (trimmed.includes(','))
    return trimmed.replace(',', '.')

  return trimmed
}

function formatEstimatedImpactInput(value: number | undefined) {
  if (value == null)
    return ''

  return new Intl.NumberFormat('pt-BR', {
    useGrouping: false,
    minimumFractionDigits: 0,
    maximumFractionDigits: 4
  }).format(value)
}

function parseMaskedNumber(value: string | number | null | undefined) {
  if (typeof value === 'number')
    return Number.isNaN(value) ? undefined : value

  if (value == null)
    return undefined

  const digitsOnly = String(value).replace(/[^\d,.-]/g, '')
  if (!digitsOnly)
    return undefined

  const normalized = normalizeDecimalInput(digitsOnly)
  const parsed = Number(normalized)
  return Number.isNaN(parsed) ? undefined : parsed
}

function formatEstimatedImpact(value: number | undefined, displayType: ImpactDisplayType) {
  if (value == null)
    return ''

  if (displayType === 'Currency') {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value)
  }

  if (displayType === 'Percentage') {
    return `${new Intl.NumberFormat('pt-BR', {
      minimumFractionDigits: 0,
      maximumFractionDigits: 2
    }).format(value)}%`
  }

  return new Intl.NumberFormat('pt-BR', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2
  }).format(value)
}

function formatMeasurementValue(value: number) {
  return new Intl.NumberFormat('pt-BR', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2
  }).format(value)
}

function formatMeasurementDate(value: string) {
  if (!value)
    return ''

  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day)
    return value

  return new Intl.DateTimeFormat('pt-BR').format(new Date(year, month - 1, day))
}

function toEditableKpiLink(link?: Partial<DemandKpiLinkInput>): EditableDemandKpiLink {
  const estimatedImpact = link?.estimatedImpact
  const impactDisplayType: ImpactDisplayType = 'Number'

  return {
    kpiId: link?.kpiId ?? '',
    impactType: link?.impactType ?? 'Increase',
    estimatedImpact,
    confidenceLevel: link?.confidenceLevel ?? 'Medium',
    impactDisplayType,
    estimatedImpactInput: formatEstimatedImpactInput(estimatedImpact),
    observationInput: link?.observation ?? ''
  }
}

function updateImpactDisplayType(index: number, value: string | undefined) {
  const link = kpiLinkDrafts.value[index]
  if (!link)
    return

  const nextType = (value ?? 'Number') as ImpactDisplayType
  link.impactDisplayType = nextType
}

function updateEstimatedImpactInput(index: number, value: string | number | null | undefined) {
  const link = kpiLinkDrafts.value[index]
  if (!link)
    return

  const rawValue = typeof value === 'number' ? String(value) : (value ?? '')
  const parsed = parseMaskedNumber(rawValue)
  link.estimatedImpact = parsed
  link.estimatedImpactInput = String(rawValue ?? '')
}

function createKpiLinkDraft(link?: Partial<DemandKpiLinkInput>): KpiLinkDraftState {
  return {
    draftId: crypto.randomUUID(),
    ...toEditableKpiLink(link)
  }
}

function addKpiLinkDraft() {
  kpiLinkDrafts.value.push(createKpiLinkDraft())
}

const availableKpisForLink = computed(() => {
  const usedIds = new Set([
    ...persistedKpiLinks.value.map(link => link.kpiId),
    ...kpiLinkDrafts.value.map(link => link.kpiId).filter(Boolean)
  ])
  return (props.availableKpis ?? []).filter(kpi => !usedIds.has(kpi.id))
})

const kpiOptions = computed(() =>
  (props.availableKpis ?? []).map(kpi => ({ value: kpi.id, label: kpi.name }))
)

function getKpiOptionsForRow(selectedKpiId: string) {
  const selectedOption = kpiOptions.value.filter(option => option.value === selectedKpiId)
  const availableOptions = kpiOptions.value.filter(option =>
    option.value === selectedKpiId || availableKpisForLink.value.some(kpi => kpi.id === option.value)
  )

  return [...selectedOption, ...availableOptions.filter(option => option.value !== selectedKpiId)]
}

function getDraftIndex(draftId: string) {
  return kpiLinkDrafts.value.findIndex(item => item.draftId === draftId)
}

function updateDraftKpiId(draftId: string, value: string | undefined) {
  const draft = kpiLinkDrafts.value.find(item => item.draftId === draftId)
  if (!draft)
    return

  draft.kpiId = value ?? ''
}

function updateDraftObservation(draftId: string, value: string | number | null | undefined) {
  const draft = kpiLinkDrafts.value.find(item => item.draftId === draftId)
  if (!draft)
    return

  draft.observationInput = String(value ?? '')
}

function getKpiById(kpiId: string) {
  return (props.availableKpis ?? []).find(kpi => kpi.id === kpiId)
}

function getKpiObjectiveLabel(kpiId: string) {
  return getKpiById(kpiId)?.objective === 'Decrease' ? 'Reduzir' : 'Aumentar'
}

function getKpiConfidenceSummary(confidenceLevel: ConfidenceLevel) {
  switch (confidenceLevel) {
    case 'High':
      return '(forte confiança)'
    case 'Medium':
      return '(é possível, mas não é garantido)'
    case 'Low':
      return '(não sabemos se é possível)'
  }
}

function getKpiArticle(kpiName: string, includePreposition: boolean) {
  const normalized = kpiName.trim().toLowerCase()
  const feminineStarts = ['taxa', 'receita', 'margem', 'nota', 'média', 'quantidade', 'conversão', 'retenção']

  if (feminineStarts.some(word => normalized.startsWith(word)))
    return includePreposition ? 'da' : 'a'

  return includePreposition ? 'do' : 'o'
}

function getKpiImpactSummary(link: EditableDemandKpiLink) {
  if (!link.kpiId)
    return null

  const kpi = getKpiById(link.kpiId)
  if (!kpi)
    return null

  const impactDefined = link.estimatedImpact != null
  const impactValue = !impactDefined
    ? '[NÃO DEFINIDO]'
    : formatEstimatedImpact(link.estimatedImpact, link.impactDisplayType)

  return `${getKpiObjectiveLabel(link.kpiId)} ${impactValue} ${getKpiArticle(kpi.name, impactDefined)} ${kpi.name} ${getKpiConfidenceSummary(link.confidenceLevel)}`
}

function getPersistedKpiImpactSummary(link: DemandKpiLink) {
  const kpi = getKpiById(link.kpiId)
  if (!kpi)
    return null

  const impactValue = link.estimatedImpact == null
    ? '[NÃO DEFINIDO]'
    : formatMeasurementValue(link.estimatedImpact)

  return `${getKpiObjectiveLabel(link.kpiId)} ${impactValue} ${getKpiArticle(kpi.name, link.estimatedImpact != null)} ${kpi.name}`
}

function hasMeasurementForKpi(kpiId: string) {
  return getMeasurementsForKpi(kpiId).length > 0
}

function isKpiLinkComplete(link: EditableDemandKpiLink) {
  return !!link.kpiId
}

const setupSubmitReason = computed(() => {
  if (!props.demand)
    return 'Demanda não encontrada.'
  if (formState.hasNoKpi)
    return null
  if (!formState.hasNoKpi && persistedKpiLinks.value.length === 0 && kpiLinkDrafts.value.length === 0)
    return 'Adicione ao menos um KPI impactado ou marque a demanda como sem KPI.'

  return null
})

async function saveKpiSetup() {
  if (!props.demand || setupSubmitReason.value)
    return

  isSavingSetup.value = true

  try {
    const validLinks = formState.hasNoKpi
      ? []
      : persistedKpiLinks.value.map(link => ({
          kpiId: link.kpiId,
          impactType: link.impactType,
          estimatedImpact: link.estimatedImpact,
          confidenceLevel: link.confidenceLevel,
          observation: link.observation
        }))

    await roadmapStore.updateDemand(props.demand.id, buildDemandFormData(props.demand, {
      hasNoKpi: formState.hasNoKpi,
      noKpiClassification: formState.hasNoKpi ? formState.noKpiClassification : undefined
    }))

    await kpiStore.updateDemandKpiLinks(props.demand.id, validLinks)
    await roadmapStore.fetchDemands()
    toast.add({ title: 'Registro de KPI atualizado', color: 'success' })
  }
  catch {
    // error handled by useApi
  }
  finally {
    isSavingSetup.value = false
  }
}

async function persistKpiLinks(links: DemandKpiLinkInput[]) {
  if (!props.demand)
    return

  await roadmapStore.updateDemand(props.demand.id, buildDemandFormData(props.demand, {
    hasNoKpi: formState.hasNoKpi,
    noKpiClassification: formState.hasNoKpi ? formState.noKpiClassification : undefined
  }))

  await kpiStore.updateDemandKpiLinks(props.demand.id, links)
  await roadmapStore.fetchDemands()
}

function cancelKpiLinkDraft(draftId: string) {
  kpiLinkDrafts.value = kpiLinkDrafts.value.filter(item => item.draftId !== draftId)
}

async function saveKpiLinkDraft(draftId: string) {
  if (!props.demand)
    return

  const draft = kpiLinkDrafts.value.find(item => item.draftId === draftId)
  if (!draft || !isKpiLinkComplete(draft)) {
    toast.add({ title: 'Preencha o KPI antes de salvar', color: 'warning' })
    return
  }

  kpiLinkSavingDraftId.value = draftId

  try {
    const nextLinks = [
      ...persistedKpiLinks.value.map(link => ({
        kpiId: link.kpiId,
        impactType: link.impactType,
        estimatedImpact: link.estimatedImpact,
        confidenceLevel: link.confidenceLevel,
        observation: link.observation
      })),
      {
        kpiId: draft.kpiId,
        impactType: draft.impactType,
        estimatedImpact: draft.estimatedImpact,
        confidenceLevel: draft.confidenceLevel,
        observation: draft.observationInput || undefined
      }
    ]

    await persistKpiLinks(nextLinks)
    cancelKpiLinkDraft(draftId)
    editingPersistedKpiLinkId.value = null
    toast.add({ title: 'KPI vinculado à demanda', color: 'success' })
  }
  catch {
    // error handled by useApi
  }
  finally {
    kpiLinkSavingDraftId.value = null
  }
}

async function deletePersistedKpiLink(linkId: string) {
  if (!props.demand)
    return

  kpiLinkDeletingId.value = linkId

  try {
    const nextLinks = persistedKpiLinks.value
      .filter(link => link.id !== linkId)
      .map(link => ({
        kpiId: link.kpiId,
        impactType: link.impactType,
        estimatedImpact: link.estimatedImpact,
        confidenceLevel: link.confidenceLevel,
        observation: link.observation
      }))

    await persistKpiLinks(nextLinks)
    toast.add({ title: 'KPI removido da demanda', color: 'success' })
  }
  catch {
    // error handled by useApi
  }
  finally {
    kpiLinkDeletingId.value = null
  }
}

function startEditPersistedKpiLink(link: DemandKpiLink) {
  editingPersistedKpiLinkId.value = link.id

  const existingDraftIndex = kpiLinkDrafts.value.findIndex(item => item.draftId === link.id)
  const nextDraft = {
    draftId: link.id,
    ...toEditableKpiLink({
      kpiId: link.kpiId,
      impactType: link.impactType,
      estimatedImpact: link.estimatedImpact,
      confidenceLevel: link.confidenceLevel,
      observation: link.observation
    })
  }

  if (existingDraftIndex >= 0) {
    kpiLinkDrafts.value.splice(existingDraftIndex, 1, nextDraft)
    return
  }

  kpiLinkDrafts.value.push(nextDraft)
}

async function savePersistedKpiLink(linkId: string) {
  if (!props.demand)
    return

  const draft = kpiLinkDrafts.value.find(item => item.draftId === linkId)
  const persisted = persistedKpiLinks.value.find(item => item.id === linkId)
  if (!draft || !persisted || !isKpiLinkComplete(draft)) {
    toast.add({ title: 'Preencha o KPI antes de salvar', color: 'warning' })
    return
  }

  kpiLinkSavingDraftId.value = linkId

  try {
    const nextLinks = persistedKpiLinks.value.map(link => {
      if (link.id !== linkId) {
        return {
          kpiId: link.kpiId,
          impactType: link.impactType,
          estimatedImpact: link.estimatedImpact,
          confidenceLevel: link.confidenceLevel,
          observation: link.observation
        }
      }

      return {
        kpiId: draft.kpiId,
        impactType: draft.impactType,
        estimatedImpact: draft.estimatedImpact,
        confidenceLevel: draft.confidenceLevel,
        observation: draft.observationInput || undefined
      }
    })

    await persistKpiLinks(nextLinks)
    cancelKpiLinkDraft(linkId)
    editingPersistedKpiLinkId.value = null
    toast.add({ title: 'KPI atualizado', color: 'success' })
  }
  catch {
    // error handled by useApi
  }
  finally {
    kpiLinkSavingDraftId.value = null
  }
}

function getMeasurementResultLabel(result: MeasurementResult) {
  switch (result) {
    case 'Positive':
      return 'Positivo'
    case 'Neutral':
      return 'Neutro'
    case 'Negative':
      return 'Negativo'
  }
}

function getMeasurementResultTone(result: MeasurementResult) {
  switch (result) {
    case 'Positive':
      return 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300'
    case 'Neutral':
      return 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300'
    case 'Negative':
      return 'border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-900 dark:bg-rose-950/40 dark:text-rose-300'
  }
}

function sortMeasurements(measurements: KpiMeasurement[]) {
  return [...measurements].sort((left, right) => {
    const dateCompare = right.measurementDate.localeCompare(left.measurementDate)
    if (dateCompare !== 0)
      return dateCompare

    return right.createdAt.localeCompare(left.createdAt)
  })
}

const persistedKpiLinks = computed(() => props.demand?.kpiLinks ?? [])

const measurementSectionState = computed(() => {
  if (!props.demand?.id)
    return { enabled: false, message: 'Demanda não encontrada.', tone: 'default' as const }

  if (formState.hasNoKpi || persistedKpiLinks.value.length === 0)
    return { enabled: false, message: 'A apuração só fica disponível para demandas com KPI vinculado.', tone: 'default' as const }

  if (props.demand.status !== 'Done')
    return { enabled: false, message: 'A apuração fica disponível após a entrega da demanda.', tone: 'error' as const }

  return { enabled: true, message: '', tone: 'default' as const }
})

function getMeasurementsForKpi(kpiId: string) {
  return kpiMeasurements.value.filter(measurement => measurement.kpiId === kpiId)
}

function getCurrentMeasurement(kpiId: string) {
  return getMeasurementsForKpi(kpiId)[0] ?? null
}

function buildMeasurementDraft(kpiId: string, measurement?: KpiMeasurement): MeasurementEditorState {
  return {
    id: measurement?.id,
    kpiId,
    measuredValueInput: measurement != null ? String(measurement.measuredValue).replace('.', ',') : '',
    measurementDate: measurement?.measurementDate ?? new Date().toISOString().slice(0, 10),
    result: measurement?.result ?? 'Neutral',
    observation: measurement?.observation ?? ''
  }
}

function openMeasurementDraft(kpiId: string, measurement?: KpiMeasurement) {
  measurementDrafts.value = {
    ...measurementDrafts.value,
    [kpiId]: buildMeasurementDraft(kpiId, measurement)
  }
}

function cancelMeasurementDraft(kpiId: string) {
  const nextDrafts = { ...measurementDrafts.value }
  delete nextDrafts[kpiId]
  measurementDrafts.value = nextDrafts
}

function getMeasurementDraft(kpiId: string) {
  return measurementDrafts.value[kpiId] ?? null
}

function upsertMeasurement(measurement: KpiMeasurement) {
  const nextMeasurements = kpiMeasurements.value.filter(item => item.id !== measurement.id)
  nextMeasurements.push(measurement)
  kpiMeasurements.value = sortMeasurements(nextMeasurements)
}

async function refreshDemandMeasurements() {
  if (!props.demand?.id)
    return

  kpiMeasurements.value = sortMeasurements(await kpiStore.fetchDemandKpiMeasurements(props.demand.id))
  await roadmapStore.fetchDemands()
}

async function saveMeasurement(kpiId: string) {
  const draft = getMeasurementDraft(kpiId)
  if (!draft || !props.demand?.id)
    return

  const measuredValue = parseMaskedNumber(draft.measuredValueInput)
  if (measuredValue == null || !draft.measurementDate) {
    toast.add({ title: 'Preencha valor e data da apuração', color: 'warning' })
    return
  }

  const result = computeAutomaticMeasurementResult(kpiId, measuredValue)

  measurementSavingKpiId.value = kpiId

  try {
    if (draft.id) {
      const payload: UpdateDemandKpiMeasurementInput = {
        measuredValue,
        measurementDate: draft.measurementDate,
        result,
        observation: draft.observation || undefined
      }

      upsertMeasurement(await kpiStore.updateDemandKpiMeasurement(draft.id, payload))
      toast.add({ title: 'Apuração atualizada', color: 'success' })
    }
    else {
      const payload: CreateDemandKpiMeasurementInput = {
        kpiId,
        measuredValue,
        measurementDate: draft.measurementDate,
        result,
        observation: draft.observation || undefined
      }

      upsertMeasurement(await kpiStore.createDemandKpiMeasurement(props.demand.id, payload))
      toast.add({ title: 'Apuração registrada', color: 'success' })
    }

    cancelMeasurementDraft(kpiId)
    await refreshDemandMeasurements()
  }
  catch {
    // error handled by useApi
  }
  finally {
    measurementSavingKpiId.value = null
  }
}

async function deleteMeasurement(measurementId: string) {
  measurementDeletingId.value = measurementId

  try {
    await kpiStore.deleteDemandKpiMeasurement(measurementId)
    kpiMeasurements.value = kpiMeasurements.value.filter(item => item.id !== measurementId)
    toast.add({ title: 'Apuração removida', color: 'success' })
    await refreshDemandMeasurements()
  }
  catch {
    // error handled by useApi
  }
  finally {
    measurementDeletingId.value = null
  }
}
</script>

<template>
  <div class="space-y-6">
    <UCard :ui="{ body: 'p-5 sm:p-6' }">
      <div class="space-y-5">
        <div>
          <h2 class="text-base font-semibold text-highlighted">Configuração de KPIs</h2>
          <p class="mt-1 text-sm text-muted">
            Defina o vínculo com indicadores e a expectativa de impacto da entrega.
          </p>
        </div>

        <div class="grid gap-3 md:grid-cols-[minmax(0,18rem)_minmax(0,29rem)] md:items-start">
          <UFormField label="Registro de KPI">
            <label class="flex min-h-9 items-center gap-2 cursor-pointer select-none">
              <input
                v-model="formState.hasNoKpi"
                type="checkbox"
                class="h-4 w-4 accent-primary"
              >
              <span class="text-sm" :class="formState.hasNoKpi ? 'text-warning font-medium' : 'text-muted'">
                Marcar demanda como sem KPI
              </span>
            </label>
          </UFormField>

          <div v-if="formState.hasNoKpi" class="grid gap-3 md:grid-cols-[minmax(0,29rem)_auto] md:items-end">
            <UFormField
              label="Classificação da demanda sem KPI"
              required
              class="w-full"
            >
              <USelect
                v-model="formState.noKpiClassification"
                :items="noKpiClassificationOptions"
                placeholder="Selecione"
                class="w-full"
              />
            </UFormField>

            <div class="flex items-end pb-0.5">
              <UButton
                type="button"
                icon="i-lucide-save"
                :loading="isSavingSetup"
                :disabled="!formState.noKpiClassification"
                label="Salvar registro de KPI"
                @click="saveKpiSetup"
              />
            </div>
          </div>
        </div>

        <div v-if="formState.hasNoKpi" class="text-sm text-muted">
          Esta demanda foi marcada como sem KPI vinculado.
        </div>

        <div v-else class="space-y-3">
          <div v-if="persistedKpiLinks.length" class="space-y-3">
            <article
              v-for="link in persistedKpiLinks"
              :key="link.id"
              class="rounded-xl border border-default bg-elevated p-4"
            >
              <div v-if="editingPersistedKpiLinkId !== link.id" class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                <div class="space-y-1">
                  <p class="text-sm font-semibold text-highlighted">{{ link.kpiName }}</p>
                  <p v-if="getPersistedKpiImpactSummary(link)" class="text-xs text-muted">
                    Impacto esperado: {{ getPersistedKpiImpactSummary(link) }}
                  </p>
                  <p class="text-xs text-muted">
                    Confiança: {{ confidenceLevelOptions.find(option => option.value === link.confidenceLevel)?.label ?? link.confidenceLevel }}
                  </p>
                  <p v-if="link.observation" class="text-xs text-muted">
                    Observação: {{ link.observation }}
                  </p>
                </div>

                <div class="flex justify-end gap-1">
                  <UButton
                    type="button"
                    icon="i-lucide-pencil"
                    variant="ghost"
                    size="xs"
                    color="neutral"
                    @click="startEditPersistedKpiLink(link)"
                  />
                  <UButton
                    type="button"
                    icon="i-lucide-trash-2"
                    variant="ghost"
                    size="xs"
                    color="error"
                    :loading="kpiLinkDeletingId === link.id"
                    @click="deletePersistedKpiLink(link.id)"
                  />
                </div>
              </div>

              <div
                v-else
                class="grid gap-3 rounded-lg border border-default bg-default p-3 md:grid-cols-[minmax(0,1.3fr)_minmax(0,0.7fr)_minmax(0,0.8fr)_minmax(0,0.8fr)_auto]"
              >
                <UFormField label="KPI relacionado" required>
                  <USelect
                    :model-value="kpiLinkDrafts.find(item => item.draftId === link.id)?.kpiId ?? link.kpiId"
                    :items="getKpiOptionsForRow(kpiLinkDrafts.find(item => item.draftId === link.id)?.kpiId ?? link.kpiId)"
                    :disabled="hasMeasurementForKpi(link.kpiId)"
                    placeholder="Selecione um KPI"
                    class="w-full"
                    @update:model-value="(value) => updateDraftKpiId(link.id, value as string | undefined)"
                  />
                </UFormField>

                <UFormField label="Tipo de impacto">
                  <USelect
                    :model-value="kpiLinkDrafts.find(item => item.draftId === link.id)?.impactDisplayType ?? 'Number'"
                    :items="impactTypeOptions"
                    class="w-full"
                    @update:model-value="(value) => updateImpactDisplayType(getDraftIndex(link.id), value as string | undefined)"
                  />
                </UFormField>

                <UFormField label="Impacto estimado">
                  <UInput
                    :model-value="kpiLinkDrafts.find(item => item.draftId === link.id)?.estimatedImpactInput ?? ''"
                    class="w-full"
                    @update:model-value="(value) => updateEstimatedImpactInput(getDraftIndex(link.id), value)"
                  />
                </UFormField>

                <UFormField label="">
                  <div class="space-y-1.5">
                    <div class="flex items-center gap-1.5">
                      <span class="text-sm text-highlighted">Nível de confiança</span>
                      <UPopover :content="{ side: 'bottom', align: 'end', sideOffset: 8 }">
                        <UButton
                          type="button"
                          icon="i-lucide-circle-help"
                          variant="ghost"
                          color="neutral"
                          size="xs"
                          aria-label="Explicar níveis de confiança"
                        />
                        <template #content>
                          <div class="max-h-[70vh] w-[min(28rem,calc(100vw-2rem))] space-y-2.5 overflow-y-auto p-3 text-sm text-highlighted">
                            <p class="font-medium text-highlighted">
                              Nível de confiança = o quanto você acredita que aquele épico realmente vai impactar o KPI.
                            </p>
                            <div
                              v-for="item in confidenceLevelHelp"
                              :key="item.title"
                              class="space-y-1 rounded-lg border border-default p-2.5"
                            >
                              <p class="font-semibold" :class="item.color">{{ item.emoji }} {{ item.title }}</p>
                              <p>👉 {{ item.summary }}</p>
                              <p class="text-muted">{{ item.signals }}</p>
                              <p class="text-muted">{{ item.detail }}</p>
                            </div>
                          </div>
                        </template>
                      </UPopover>
                    </div>
                    <USelect
                      :model-value="kpiLinkDrafts.find(item => item.draftId === link.id)?.confidenceLevel ?? link.confidenceLevel"
                      :items="confidenceLevelOptions"
                      class="w-full"
                      @update:model-value="(value) => {
                        const draft = kpiLinkDrafts.find(item => item.draftId === link.id)
                        if (draft) draft.confidenceLevel = (value ?? 'Medium') as ConfidenceLevel
                      }"
                    />
                  </div>
                </UFormField>

                <div class="md:col-span-5 flex flex-col gap-2 md:flex-row md:items-end">
                  <UFormField label="Observação" class="flex-1">
                    <UTextarea
                      :model-value="kpiLinkDrafts.find(item => item.draftId === link.id)?.observationInput ?? ''"
                      :rows="2"
                      placeholder="Observação sobre o vínculo do KPI (opcional)"
                      class="w-full"
                      @update:model-value="(value) => updateDraftObservation(link.id, value)"
                    />
                  </UFormField>

                  <div class="flex justify-end gap-2 pb-0.5">
                    <UButton
                      type="button"
                      variant="ghost"
                      color="neutral"
                      label="Cancelar"
                      @click="cancelKpiLinkDraft(link.id); editingPersistedKpiLinkId = null"
                    />
                    <UButton
                      type="button"
                      icon="i-lucide-save"
                      label="Salvar KPI"
                      :loading="kpiLinkSavingDraftId === link.id"
                      @click="savePersistedKpiLink(link.id)"
                    />
                  </div>
                </div>
              </div>
            </article>
          </div>

          <article
            v-for="draft in kpiLinkDrafts"
            :key="draft.draftId"
            class="rounded-xl border border-dashed border-primary/30 bg-primary/5 p-4"
          >
            <div class="grid gap-3 rounded-lg border border-default bg-default p-3 md:grid-cols-[minmax(0,1.3fr)_minmax(0,0.7fr)_minmax(0,0.8fr)_minmax(0,0.8fr)_auto]">
              <UFormField label="KPI relacionado" required>
                <USelect
                  :model-value="draft.kpiId"
                  :items="getKpiOptionsForRow(draft.kpiId)"
                  placeholder="Selecione um KPI"
                  class="w-full"
                  @update:model-value="(value) => updateDraftKpiId(draft.draftId, value as string | undefined)"
                />
              </UFormField>

              <UFormField label="Tipo de impacto">
                <USelect
                  :model-value="draft.impactDisplayType"
                  :items="impactTypeOptions"
                  class="w-full"
                  @update:model-value="(value) => updateImpactDisplayType(getDraftIndex(draft.draftId), value as string | undefined)"
                />
              </UFormField>

              <UFormField label="Impacto estimado">
                <UInput
                  :model-value="draft.estimatedImpactInput"
                  :placeholder="draft.impactDisplayType === 'Percentage'
                    ? 'Ex: 12,5%'
                    : draft.impactDisplayType === 'Currency'
                      ? 'Ex: R$ 1.500,00'
                      : 'Ex: 1500'"
                  class="w-full"
                  @update:model-value="(value) => updateEstimatedImpactInput(getDraftIndex(draft.draftId), value)"
                />
              </UFormField>

              <UFormField label="">
                <div class="space-y-1.5">
                  <div class="flex items-center gap-1.5">
                    <span class="text-sm text-highlighted">Nível de confiança</span>
                    <UPopover :content="{ side: 'bottom', align: 'end', sideOffset: 8 }">
                      <UButton
                        type="button"
                        icon="i-lucide-circle-help"
                        variant="ghost"
                        color="neutral"
                        size="xs"
                        aria-label="Explicar níveis de confiança"
                      />
                      <template #content>
                        <div class="max-h-[70vh] w-[min(28rem,calc(100vw-2rem))] space-y-2.5 overflow-y-auto p-3 text-sm text-highlighted">
                          <p class="font-medium text-highlighted">
                            Nível de confiança = o quanto você acredita que aquele épico realmente vai impactar o KPI.
                          </p>
                          <div
                            v-for="item in confidenceLevelHelp"
                            :key="item.title"
                            class="space-y-1 rounded-lg border border-default p-2.5"
                          >
                            <p class="font-semibold" :class="item.color">{{ item.emoji }} {{ item.title }}</p>
                            <p>👉 {{ item.summary }}</p>
                            <p class="text-muted">{{ item.signals }}</p>
                            <p class="text-muted">{{ item.detail }}</p>
                          </div>
                        </div>
                      </template>
                    </UPopover>
                  </div>
                  <USelect
                    v-model="draft.confidenceLevel"
                    :items="confidenceLevelOptions"
                    class="w-full"
                  />
                </div>
              </UFormField>

              <div class="md:col-span-5 flex flex-col gap-2 md:flex-row md:items-end">
                <UFormField label="Observação" class="flex-1">
                  <UTextarea
                    :model-value="draft.observationInput"
                    :rows="2"
                    placeholder="Observação sobre o vínculo do KPI (opcional)"
                    class="w-full"
                    @update:model-value="(value) => updateDraftObservation(draft.draftId, value)"
                  />
                </UFormField>

                <div class="flex justify-end gap-2 pb-0.5">
                  <UButton
                    type="button"
                    variant="ghost"
                    color="neutral"
                    label="Cancelar"
                    @click="cancelKpiLinkDraft(draft.draftId)"
                  />
                  <UButton
                    type="button"
                    icon="i-lucide-save"
                    label="Salvar KPI"
                    :loading="kpiLinkSavingDraftId === draft.draftId"
                    @click="saveKpiLinkDraft(draft.draftId)"
                  />
                </div>
              </div>

              <p v-if="getKpiImpactSummary(draft)" class="text-xs leading-relaxed text-muted md:col-span-5">
                {{ getKpiImpactSummary(draft) }}
              </p>
            </div>
          </article>

          <div class="flex flex-wrap items-center gap-3">
            <UButton
              v-if="availableKpisForLink.length"
              type="button"
              icon="i-lucide-plus"
              label="Adicionar KPI impactado"
              variant="soft"
              size="sm"
              @click="addKpiLinkDraft"
            />
          </div>

          <div v-if="setupSubmitReason" class="rounded-lg border border-warning/30 bg-warning/5 px-3 py-2 text-sm text-muted">
            {{ setupSubmitReason }}
          </div>

          <p v-if="!(props.availableKpis ?? []).length" class="text-xs text-muted italic">
            Nenhum KPI cadastrado. Cadastre na página de KPIs antes de vincular a demanda.
          </p>
        </div>
      </div>
    </UCard>

    <UCard :ui="{ body: 'p-5 sm:p-6' }">
      <div class="space-y-4">
        <div>
          <h2 class="text-base font-semibold text-highlighted">Apuração pós-entrega</h2>
          <p class="mt-1 text-sm text-muted">
            Registre as apurações dos KPIs vinculados. A mais recente passa a ser considerada a apuração atual.
          </p>
        </div>

        <div
          v-if="!measurementSectionState.enabled"
          class="rounded-lg border border-dashed p-3 text-sm"
          :class="measurementSectionState.tone === 'error'
            ? 'border-error/40 bg-error/5 text-error'
            : 'border-default bg-elevated text-muted'"
        >
          {{ measurementSectionState.message }}
        </div>

        <div v-else class="space-y-4">
          <article
            v-for="link in persistedKpiLinks"
            :key="link.id"
            class="rounded-xl border border-default bg-elevated p-4"
          >
            <div class="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
              <div class="space-y-1">
                <p class="text-sm font-semibold text-highlighted">{{ link.kpiName }}</p>
                <p v-if="getPersistedKpiImpactSummary(link)" class="text-xs text-muted">
                  Impacto esperado: {{ getPersistedKpiImpactSummary(link) }}
                </p>
                <p class="text-xs text-muted">
                  Confiança: {{ confidenceLevelOptions.find(option => option.value === link.confidenceLevel)?.label ?? link.confidenceLevel }}
                </p>
              </div>

              <div class="flex flex-wrap items-center gap-2">
                <span
                  v-if="getCurrentMeasurement(link.kpiId)"
                  class="inline-flex items-center rounded-full border border-primary/20 bg-primary/10 px-2.5 py-1 text-xs font-medium text-primary"
                >
                  Atual: {{ formatMeasurementValue(getCurrentMeasurement(link.kpiId)!.measuredValue) }} em {{ formatMeasurementDate(getCurrentMeasurement(link.kpiId)!.measurementDate) }}
                </span>
                <UButton
                  type="button"
                  size="xs"
                  variant="soft"
                  icon="i-lucide-plus"
                  label="Nova apuração"
                  @click="openMeasurementDraft(link.kpiId)"
                />
              </div>
            </div>

            <div
              v-if="getMeasurementDraft(link.kpiId)"
              class="mt-4 grid gap-3 rounded-lg border border-default bg-default p-3 md:grid-cols-[minmax(0,1fr)_12rem]"
            >
              <UFormField label="Impacto apurado" required>
                <UInput
                  :model-value="getMeasurementDraft(link.kpiId)?.measuredValueInput ?? ''"
                  placeholder="Ex: 12,5"
                  class="w-full"
                  @update:model-value="(value) => {
                    const draft = getMeasurementDraft(link.kpiId)
                    if (draft) draft.measuredValueInput = String(value ?? '')
                  }"
                />
              </UFormField>

              <UFormField label="Data de apuração" required>
                <UInput
                  :model-value="getMeasurementDraft(link.kpiId)?.measurementDate ?? ''"
                  type="date"
                  class="w-full"
                  @update:model-value="(value) => {
                    const draft = getMeasurementDraft(link.kpiId)
                    if (draft) draft.measurementDate = String(value ?? '')
                  }"
                />
              </UFormField>

              <div class="md:col-span-2 flex flex-col gap-2 md:flex-row md:items-end">
                <UFormField label="Observação" class="flex-1">
                  <UTextarea
                    :model-value="getMeasurementDraft(link.kpiId)?.observation ?? ''"
                    :rows="2"
                    placeholder="Contexto da apuração (opcional)"
                    class="w-full"
                    @update:model-value="(value) => {
                      const draft = getMeasurementDraft(link.kpiId)
                      if (draft) draft.observation = String(value ?? '')
                    }"
                  />
                </UFormField>

                <div class="flex justify-end gap-2 pb-0.5">
                  <UButton
                    type="button"
                    variant="ghost"
                    color="neutral"
                    label="Cancelar"
                    @click="cancelMeasurementDraft(link.kpiId)"
                  />
                  <UButton
                    type="button"
                    :loading="measurementSavingKpiId === link.kpiId"
                    icon="i-lucide-save"
                    :label="getMeasurementDraft(link.kpiId)?.id ? 'Atualizar apuração' : 'Salvar apuração'"
                    @click="saveMeasurement(link.kpiId)"
                  />
                </div>
              </div>
            </div>

            <div v-if="getMeasurementsForKpi(link.kpiId).length" class="mt-4 space-y-2">
              <article
                v-for="(measurement, measurementIndex) in getMeasurementsForKpi(link.kpiId)"
                :key="measurement.id"
                class="rounded-lg border border-default bg-default p-3"
              >
                <div class="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                  <div class="space-y-2">
                    <div class="flex flex-wrap items-center gap-2">
                      <span
                        v-if="measurementIndex === 0"
                        class="inline-flex items-center rounded-full border border-primary/20 bg-primary/10 px-2 py-0.5 text-[11px] font-semibold uppercase tracking-[0.08em] text-primary"
                      >
                        Atual
                      </span>
                      <span
                        class="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium"
                        :class="getMeasurementResultTone(measurement.result)"
                      >
                        {{ getMeasurementResultLabel(measurement.result) }}
                      </span>
                    </div>
                    <p class="text-sm font-medium text-highlighted">
                      {{ formatMeasurementValue(measurement.measuredValue) }} em {{ formatMeasurementDate(measurement.measurementDate) }}
                    </p>
                    <p v-if="measurement.observation" class="text-xs leading-relaxed text-muted">
                      {{ measurement.observation }}
                    </p>
                  </div>

                  <div class="flex items-center gap-1 self-end md:self-start">
                    <UButton
                      type="button"
                      size="xs"
                      variant="ghost"
                      color="neutral"
                      icon="i-lucide-pencil"
                      @click="openMeasurementDraft(link.kpiId, measurement)"
                    />
                    <UButton
                      type="button"
                      size="xs"
                      variant="ghost"
                      color="error"
                      icon="i-lucide-trash-2"
                      :loading="measurementDeletingId === measurement.id"
                      @click="deleteMeasurement(measurement.id)"
                    />
                  </div>
                </div>
              </article>
            </div>

            <p v-else class="mt-4 text-xs italic text-muted">
              Nenhuma apuração registrada para este KPI.
            </p>
          </article>
        </div>
      </div>
    </UCard>
  </div>
</template>