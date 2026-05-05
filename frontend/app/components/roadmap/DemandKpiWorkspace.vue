<script setup lang="ts">
import type {
  RoadmapDemand,
  Kpi,
  DemandFormData,
  DemandStatus,
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

const persistedKpiLinksState = ref<DemandKpiLink[]>([])
const kpiLinkDrafts = ref<KpiLinkDraftState[]>([])
const kpiMeasurements = ref<KpiMeasurement[]>([])
const measurementDrafts = ref<Record<string, MeasurementEditorState>>({})
const measurementReferenceDrafts = ref<Record<string, string>>({})
const isSavingSetup = ref(false)
const kpiLinkSavingDraftId = ref<string | null>(null)
const kpiLinkDeletingId = ref<string | null>(null)
const editingPersistedKpiLinkId = ref<string | null>(null)
const measurementSavingKpiId = ref<string | null>(null)
const measurementDeletingId = ref<string | null>(null)
const helpModalType = ref<'confidence' | 'kpi-catalog' | null>(null)
const measurementReferenceModalLinkId = ref<string | null>(null)
const deleteConfirmation = ref<{ type: 'kpi-link' | 'measurement', id: string } | null>(null)

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

const kpiTypeLabels = {
  Business: 'Negócio',
  Product: 'Produto'
} as const

const kpiLeverLabels = {
  Growth: 'Crescer',
  Efficiency: 'Eficiência',
  Customer: 'Cliente'
} as const

const kpiObjectiveLabels = {
  Increase: 'Aumentar',
  Decrease: 'Reduzir'
} as const

const noKpiClassificationOptions = [
  { value: 'Relationship', label: 'Relacionamento' },
  { value: 'Mandatory', label: 'Mandatório' },
  { value: 'Technical', label: 'Técnico' }
] as const

const statusLabels: Record<DemandStatus, string> = {
  Backlog: 'Backlog',
  InProgress: 'Doing',
  Done: 'Concluído',
  Deprioritized: 'Despriorizado'
}

const statusTone: Record<DemandStatus, string> = {
  Backlog: 'border-default bg-elevated text-muted',
  InProgress: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-800 dark:bg-blue-900/20 dark:text-blue-300',
  Done: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-800 dark:bg-emerald-900/20 dark:text-emerald-300',
  Deprioritized: 'border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-800 dark:bg-rose-900/20 dark:text-rose-300'
}

const classificationLabels: Record<DemandClassification, string> = {
  TechnicalDebtSecurity: 'Débito Técnico',
  Strategic: 'Estratégico',
  Evolution: 'Evolução',
  ImprovementGap: 'Melhoria/Gap',
  Mandatory: 'Mandatório',
  Homologation: 'Homologação',
  Customizacao: 'Customização'
}

const classificationBadgeClass: Record<DemandClassification, string> = {
  TechnicalDebtSecurity: 'bg-slate-100 text-slate-700 border-slate-200 dark:bg-slate-800/60 dark:text-slate-300 dark:border-slate-700',
  Strategic: 'bg-indigo-100 text-indigo-700 border-indigo-200 dark:bg-indigo-900/30 dark:text-indigo-300 dark:border-indigo-800',
  Evolution: 'bg-sky-100 text-sky-700 border-sky-200 dark:bg-sky-900/30 dark:text-sky-300 dark:border-sky-800',
  ImprovementGap: 'bg-emerald-100 text-emerald-700 border-emerald-200 dark:bg-emerald-900/30 dark:text-emerald-300 dark:border-emerald-800',
  Mandatory: 'bg-red-100 text-red-700 border-red-200 dark:bg-red-900/30 dark:text-red-300 dark:border-red-800',
  Homologation: 'bg-violet-100 text-violet-700 border-violet-200 dark:bg-violet-900/30 dark:text-violet-300 dark:border-violet-800',
  Customizacao: 'bg-orange-100 text-orange-700 border-orange-200 dark:bg-orange-900/30 dark:text-orange-300 dark:border-orange-800'
}

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
  persistedKpiLinksState.value = demand?.kpiLinks ?? []
  kpiLinkDrafts.value = []
  kpiMeasurements.value = sortMeasurements(demand?.kpiMeasurements ?? [])
  measurementDrafts.value = {}
  measurementReferenceDrafts.value = Object.fromEntries(
    (demand?.kpiLinks ?? []).map(link => [link.id, link.measurementReferenceUrl ?? ''])
  )
}, { immediate: true })

watch(() => formState.hasNoKpi, (hasNoKpi) => {
  if (!hasNoKpi)
    formState.noKpiClassification = undefined
})

function buildDemandFormData(demand: RoadmapDemand, overrides?: Partial<DemandFormData>): DemandFormData {
  return {
    itemType: demand.itemType,
    parentDemandId: demand.parentDemandId,
    title: demand.title,
    description: demand.description ?? '',
    projectId: demand.projectId,
    projectIds: demand.projectIds ?? (demand.projectId ? [demand.projectId] : []),
    quarterYear: demand.quarterYear,
    quarterNumber: demand.quarterNumber,
    type: demand.type,
    classification: demand.classification,
    productIds: demand.products.map(product => product.productId),
    status: demand.status,
    observation: demand.observation ?? '',
    jiraIssue: demand.jiraIssue ?? '',
    issueLinks: demand.issueLinks?.map(issue => ({ key: issue.key, url: issue.url ?? '' })) ?? [],
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
    measurementReferenceUrl: link?.measurementReferenceUrl ?? '',
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
  if (!availableKpisForLink.value.length) {
    toast.add({
      title: 'Nenhum KPI disponível',
      description: 'Todos os KPIs disponíveis já estão vinculados a este épico.',
      color: 'warning'
    })
    return
  }

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

function updateMeasurementReferenceDraft(linkId: string, value: string | number | null | undefined) {
  measurementReferenceDrafts.value = {
    ...measurementReferenceDrafts.value,
    [linkId]: String(value ?? '')
  }
}

function syncDemandInStore(patch: Partial<Pick<RoadmapDemand, 'hasNoKpi' | 'noKpiClassification' | 'kpiLinks' | 'kpiMeasurements'>>) {
  if (!props.demand?.id)
    return

  const demandIndex = roadmapStore.demands.findIndex(demand => demand.id === props.demand?.id)
  if (demandIndex < 0)
    return

  roadmapStore.demands.splice(demandIndex, 1, {
    ...roadmapStore.demands[demandIndex]!,
    ...patch
  })
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

function getDisplayedMeasurementResult(kpiId: string, measurement: KpiMeasurement) {
  return computeAutomaticMeasurementResult(kpiId, measurement.measuredValue)
}

function getMeasurementsChronological(kpiId: string) {
  return [...getMeasurementsForKpi(kpiId)].sort((left, right) => {
    const dateCompare = left.measurementDate.localeCompare(right.measurementDate)
    if (dateCompare !== 0)
      return dateCompare

    return left.createdAt.localeCompare(right.createdAt)
  })
}

function getMeasurementsTimeline(kpiId: string) {
  return [...getMeasurementsForKpi(kpiId)].sort((left, right) => {
    const dateCompare = right.measurementDate.localeCompare(left.measurementDate)
    if (dateCompare !== 0)
      return dateCompare

    return right.createdAt.localeCompare(left.createdAt)
  })
}

function getMeasurementSequenceIndex(kpiId: string, measurementId: string) {
  return getMeasurementsTimeline(kpiId).findIndex(measurement => measurement.id === measurementId) + 1
}

function getMeasurementChartData(kpiId: string) {
  const measurements = getMeasurementsChronological(kpiId)
  if (!measurements.length)
    return null

  const width = 380
  const height = 116
  const paddingX = 18
  const paddingTop = 18
  const paddingBottom = 18
  const values = measurements.map(measurement => measurement.measuredValue)
  const estimatedImpact = persistedKpiLinks.value.find(link => link.kpiId === kpiId)?.estimatedImpact
  const chartValues = estimatedImpact == null ? values : [...values, estimatedImpact]
  const minValue = Math.min(...chartValues)
  const maxValue = Math.max(...chartValues)
  const range = maxValue - minValue || 1
  const plotWidth = width - paddingX * 2
  const plotHeight = height - paddingTop - paddingBottom
  const stepX = measurements.length === 1 ? 0 : plotWidth / (measurements.length - 1)

  const points = measurements.map((measurement, index) => {
    const x = paddingX + (stepX * index)
    const y = paddingTop + ((maxValue - measurement.measuredValue) / range) * plotHeight
    return {
      ...measurement,
      x,
      y,
      labelY: Math.max(11, y - 7),
      result: getDisplayedMeasurementResult(kpiId, measurement)
    }
  })

  const linePath = points.map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`).join(' ')
  const areaPath = `${linePath} L ${points[points.length - 1]!.x} ${height - paddingBottom} L ${points[0]!.x} ${height - paddingBottom} Z`
  const estimatedImpactY = estimatedImpact == null
    ? null
    : paddingTop + ((maxValue - estimatedImpact) / range) * plotHeight

  const gridLines = Array.from({ length: 4 }, (_, index) => {
    const ratio = index / 3
    return {
      id: `grid-${index}`,
      y: paddingTop + (plotHeight * ratio),
      value: maxValue - (range * ratio)
    }
  })

  return {
    width,
    height,
    points,
    linePath,
    areaPath,
    estimatedImpact,
    estimatedImpactY,
    gridLines,
    minValue,
    maxValue,
    firstLabel: formatMeasurementDate(measurements[0]!.measurementDate),
    lastLabel: formatMeasurementDate(measurements[measurements.length - 1]!.measurementDate)
  }
}

function hasMeasurementForKpi(kpiId: string) {
  return getMeasurementsForKpi(kpiId).length > 0
}

function isKpiLinkComplete(link: EditableDemandKpiLink) {
  return !!link.kpiId
}

const setupSubmitReason = computed(() => {
  if (!props.demand)
    return 'Épico não encontrado.'
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
          observation: link.observation,
          measurementReferenceUrl: link.measurementReferenceUrl
        }))

    await roadmapStore.updateDemand(props.demand.id, buildDemandFormData(props.demand, {
      hasNoKpi: formState.hasNoKpi,
      noKpiClassification: formState.hasNoKpi ? formState.noKpiClassification : undefined
    }))

    persistedKpiLinksState.value = await kpiStore.updateDemandKpiLinks(props.demand.id, validLinks)
    measurementReferenceDrafts.value = Object.fromEntries(
      persistedKpiLinksState.value.map(link => [link.id, link.measurementReferenceUrl ?? ''])
    )
    syncDemandInStore({
      hasNoKpi: formState.hasNoKpi,
      noKpiClassification: formState.hasNoKpi ? formState.noKpiClassification : undefined,
      kpiLinks: persistedKpiLinksState.value,
      kpiMeasurements: formState.hasNoKpi ? [] : kpiMeasurements.value
    })
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

  persistedKpiLinksState.value = await kpiStore.updateDemandKpiLinks(props.demand.id, links)
  measurementReferenceDrafts.value = Object.fromEntries(
    persistedKpiLinksState.value.map(link => [link.id, link.measurementReferenceUrl ?? ''])
  )
  syncDemandInStore({
    hasNoKpi: formState.hasNoKpi,
    noKpiClassification: formState.hasNoKpi ? formState.noKpiClassification : undefined,
    kpiLinks: persistedKpiLinksState.value
  })
}

async function recalculateMeasurementResults(kpiId: string) {
  const measurements = getMeasurementsForKpi(kpiId)
  if (!measurements.length)
    return

  let hasChanges = false

  for (const measurement of measurements) {
    const nextResult = computeAutomaticMeasurementResult(kpiId, measurement.measuredValue)
    if (nextResult === measurement.result)
      continue

    await kpiStore.updateDemandKpiMeasurement(measurement.id, {
      measuredValue: measurement.measuredValue,
      measurementDate: measurement.measurementDate,
      result: nextResult,
      observation: measurement.observation || undefined
    })
    hasChanges = true
  }

  if (hasChanges)
    await refreshDemandMeasurements()
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
        observation: link.observation,
        measurementReferenceUrl: link.measurementReferenceUrl
      })),
      {
        kpiId: draft.kpiId,
        impactType: draft.impactType,
        estimatedImpact: draft.estimatedImpact,
        confidenceLevel: draft.confidenceLevel,
        observation: draft.observationInput || undefined,
        measurementReferenceUrl: undefined
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

async function executeDeletePersistedKpiLink(linkId: string) {
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
        observation: link.observation,
        measurementReferenceUrl: link.measurementReferenceUrl
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
      observation: link.observation,
      measurementReferenceUrl: link.measurementReferenceUrl
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
          observation: link.observation,
          measurementReferenceUrl: link.measurementReferenceUrl
        }
      }

      return {
        kpiId: draft.kpiId,
        impactType: draft.impactType,
        estimatedImpact: draft.estimatedImpact,
        confidenceLevel: draft.confidenceLevel,
        observation: draft.observationInput || undefined,
        measurementReferenceUrl: persisted.measurementReferenceUrl
      }
    })

    await persistKpiLinks(nextLinks)
    await recalculateMeasurementResults(draft.kpiId)
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

async function saveMeasurementReference(linkId: string) {
  if (!props.demand)
    return

  kpiLinkSavingDraftId.value = linkId

  try {
    const nextLinks = persistedKpiLinks.value.map(link => {
      if (link.id !== linkId) {
        return {
          kpiId: link.kpiId,
          impactType: link.impactType,
          estimatedImpact: link.estimatedImpact,
          confidenceLevel: link.confidenceLevel,
          observation: link.observation,
          measurementReferenceUrl: link.measurementReferenceUrl
        }
      }

      return {
        kpiId: link.kpiId,
        impactType: link.impactType,
        estimatedImpact: link.estimatedImpact,
        confidenceLevel: link.confidenceLevel,
        observation: link.observation,
        measurementReferenceUrl: measurementReferenceDrafts.value[linkId]?.trim() || undefined
      }
    })

    await persistKpiLinks(nextLinks)
    measurementReferenceDrafts.value = Object.fromEntries(
      persistedKpiLinksState.value.map(link => [link.id, link.measurementReferenceUrl ?? ''])
    )
    closeMeasurementReferenceModal()
    toast.add({ title: 'Referência da apuração atualizada', color: 'success' })
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

const persistedKpiLinks = computed(() => persistedKpiLinksState.value)
const standaloneKpiLinkDrafts = computed(() => {
  const persistedIds = new Set(persistedKpiLinks.value.map(link => link.id))
  return kpiLinkDrafts.value.filter(draft => !persistedIds.has(draft.draftId))
})

const helpModalTitle = computed(() => {
  if (helpModalType.value === 'confidence')
    return 'Ajuda: Nível de confiança'
  if (helpModalType.value === 'kpi-catalog')
    return 'Ajuda: KPIs relacionados'
  return ''
})

const availableKpiCatalog = computed(() =>
  [...(props.availableKpis ?? [])].sort((left, right) => left.name.localeCompare(right.name, 'pt-BR'))
)

const helpModalOpen = computed({
  get: () => helpModalType.value !== null,
  set: (open: boolean) => {
    if (!open)
      helpModalType.value = null
  }
})

const measurementSectionState = computed(() => {
  if (!props.demand?.id)
    return { enabled: false, message: 'Épico não encontrado.', tone: 'default' as const }

  if (formState.hasNoKpi || persistedKpiLinks.value.length === 0)
    return { enabled: false, message: 'A apuração só fica disponível para épicos com KPI vinculado.', tone: 'default' as const }

  return { enabled: true, message: '', tone: 'default' as const }
})

function getMeasurementsForKpi(kpiId: string) {
  return kpiMeasurements.value.filter(measurement => measurement.kpiId === kpiId)
}

function openConfidenceHelp() {
  helpModalType.value = 'confidence'
}

function openKpiCatalogHelp() {
  helpModalType.value = 'kpi-catalog'
}

function closeHelpModal() {
  helpModalType.value = null
}

function cancelPersistedKpiLinkEdit(linkId: string) {
  cancelKpiLinkDraft(linkId)
  editingPersistedKpiLinkId.value = null
}

function getCurrentMeasurement(kpiId: string) {
  return getMeasurementsForKpi(kpiId)[0] ?? null
}

function getLinkById(linkId: string) {
  return persistedKpiLinks.value.find(link => link.id === linkId) ?? null
}

const activeMeasurementReferenceLink = computed(() =>
  measurementReferenceModalLinkId.value ? getLinkById(measurementReferenceModalLinkId.value) : null
)

const measurementReferenceModalOpen = computed({
  get: () => measurementReferenceModalLinkId.value !== null,
  set: (open: boolean) => {
    if (!open)
      measurementReferenceModalLinkId.value = null
  }
})

const deleteConfirmationOpen = computed({
  get: () => deleteConfirmation.value !== null,
  set: (open: boolean) => {
    if (!open)
      deleteConfirmation.value = null
  }
})

const deleteConfirmationTitle = computed(() =>
  deleteConfirmation.value?.type === 'kpi-link'
    ? 'Confirmar exclusão do KPI'
    : 'Confirmar exclusão da apuração'
)

const deleteConfirmationDescription = computed(() =>
  deleteConfirmation.value?.type === 'kpi-link'
    ? 'Deseja realmente excluir esta configuração de KPI do épico?'
    : 'Deseja realmente excluir esta apuração de KPI?'
)

function openMeasurementReferenceModal(linkId: string) {
  const link = getLinkById(linkId)
  measurementReferenceDrafts.value = {
    ...measurementReferenceDrafts.value,
    [linkId]: link?.measurementReferenceUrl ?? measurementReferenceDrafts.value[linkId] ?? ''
  }
  measurementReferenceModalLinkId.value = linkId
}

function closeMeasurementReferenceModal() {
  measurementReferenceModalLinkId.value = null
}

function requestDeletePersistedKpiLink(linkId: string) {
  deleteConfirmation.value = { type: 'kpi-link', id: linkId }
}

function requestDeleteMeasurement(measurementId: string) {
  deleteConfirmation.value = { type: 'measurement', id: measurementId }
}

async function confirmDeletion() {
  if (!deleteConfirmation.value)
    return

  const current = deleteConfirmation.value
  deleteConfirmation.value = null

  if (current.type === 'kpi-link') {
    await executeDeletePersistedKpiLink(current.id)
    return
  }

  await executeDeleteMeasurement(current.id)
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
  syncDemandInStore({ kpiMeasurements: kpiMeasurements.value })
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

async function executeDeleteMeasurement(measurementId: string) {
  measurementDeletingId.value = measurementId

  try {
    await kpiStore.deleteDemandKpiMeasurement(measurementId)
    kpiMeasurements.value = kpiMeasurements.value.filter(item => item.id !== measurementId)
    syncDemandInStore({ kpiMeasurements: kpiMeasurements.value })
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
  <div class="space-y-4">
    <UCard v-if="props.demand" :ui="{ body: 'p-4 sm:p-5' }">
      <div class="flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
        <div class="space-y-1">
          <p class="text-xs font-semibold uppercase tracking-[0.08em] text-primary/70">Épico</p>
          <h2 class="text-base font-semibold text-highlighted">{{ props.demand.title }}</h2>
        </div>

        <div class="flex flex-wrap gap-2">
          <span class="inline-flex items-center rounded-md border px-2 py-1 text-xs font-medium" :class="statusTone[props.demand.status]">
            {{ statusLabels[props.demand.status] }}
          </span>
          <span class="inline-flex items-center rounded-full border px-2 py-1 text-xs font-medium" :class="classificationBadgeClass[props.demand.classification]">
            {{ classificationLabels[props.demand.classification] }}
          </span>
        </div>
      </div>
    </UCard>

    <UCard :ui="{ body: 'p-4 sm:p-5' }">
      <div class="space-y-4">
        <div>
          <h2 class="text-sm font-semibold text-highlighted">Configuração de KPIs</h2>
          <p class="mt-1 text-xs text-muted">
            Defina o vínculo com indicadores e a expectativa de impacto da entrega.
          </p>
        </div>

        <div class="grid gap-3 md:grid-cols-[minmax(0,18rem)_minmax(0,29rem)] md:items-start">
          <UFormField v-if="persistedKpiLinks.length === 0" label="Registro de KPI">
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
              class="rounded-xl border border-default bg-elevated p-3"
            >
              <div v-if="editingPersistedKpiLinkId !== link.id" class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                <div class="space-y-1">
                  <div class="flex flex-wrap items-center gap-2">
                    <p class="text-sm font-semibold text-highlighted">{{ link.kpiName }}</p>
                    <p class="text-xs text-muted">
                      Confiança: {{ confidenceLevelOptions.find(option => option.value === link.confidenceLevel)?.label ?? link.confidenceLevel }}
                    </p>
                  </div>
                  <p v-if="getPersistedKpiImpactSummary(link)" class="text-xs text-muted">
                    Impacto esperado: {{ getPersistedKpiImpactSummary(link) }}
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
                    @click="requestDeletePersistedKpiLink(link.id)"
                  />
                </div>
              </div>

              <div
                v-else
                class="grid gap-3 rounded-lg border border-default bg-default p-3 md:grid-cols-[minmax(0,1.3fr)_minmax(0,0.7fr)_minmax(0,0.8fr)_minmax(0,0.8fr)_auto] md:items-start"
              >
                <div class="space-y-1">
                  <div class="flex items-center gap-1.5 text-sm">
                    <span class="text-highlighted">KPI relacionado</span>
                    <span class="text-error">*</span>
                    <UButton
                      type="button"
                      icon="i-lucide-circle-help"
                      variant="ghost"
                      color="neutral"
                      size="xs"
                      class="h-4 min-h-0 w-4 min-w-0 p-0"
                      aria-label="Abrir catálogo de KPIs"
                      @click="openKpiCatalogHelp"
                    />
                  </div>
                  <USelect
                    :model-value="kpiLinkDrafts.find(item => item.draftId === link.id)?.kpiId ?? link.kpiId"
                    :items="getKpiOptionsForRow(kpiLinkDrafts.find(item => item.draftId === link.id)?.kpiId ?? link.kpiId)"
                    :disabled="hasMeasurementForKpi(link.kpiId)"
                    placeholder="Selecione um KPI"
                    class="w-full"
                    @update:model-value="(value) => updateDraftKpiId(link.id, value as string | undefined)"
                  />
                </div>

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

                <div class="space-y-1">
                  <div class="flex items-center gap-1.5 text-sm">
                    <span class="text-highlighted">Nível de confiança</span>
                    <UButton
                      type="button"
                      icon="i-lucide-circle-help"
                      variant="ghost"
                      color="neutral"
                      size="xs"
                      class="h-4 min-h-0 w-4 min-w-0 p-0"
                      aria-label="Explicar níveis de confiança"
                      @click="openConfidenceHelp"
                    />
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

                <div class="md:col-span-4">
                  <UFormField label="Observação" class="w-full">
                    <UTextarea
                      :model-value="kpiLinkDrafts.find(item => item.draftId === link.id)?.observationInput ?? ''"
                      :rows="2"
                      placeholder="Observação sobre o vínculo do KPI (opcional)"
                      class="w-full"
                      @update:model-value="(value) => updateDraftObservation(link.id, value)"
                    />
                  </UFormField>
                </div>

                <div class="flex items-end justify-end gap-2 md:row-span-3 md:self-stretch">
                    <UButton
                      type="button"
                      variant="ghost"
                      color="neutral"
                      label="Cancelar"
                      @click="cancelPersistedKpiLinkEdit(link.id)"
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
            </article>
          </div>

          <article
            v-for="draft in standaloneKpiLinkDrafts"
            :key="draft.draftId"
            class="rounded-xl border border-dashed border-primary/30 bg-primary/5 p-3"
          >
            <div class="grid gap-3 rounded-lg border border-default bg-default p-3 md:grid-cols-[minmax(0,1.3fr)_minmax(0,0.7fr)_minmax(0,0.8fr)_minmax(0,0.8fr)_auto] md:items-start">
              <div class="space-y-1">
                <div class="flex items-center gap-1.5 text-sm">
                  <span class="text-highlighted">KPI relacionado</span>
                  <span class="text-error">*</span>
                  <UButton
                    type="button"
                    icon="i-lucide-circle-help"
                    variant="ghost"
                    color="neutral"
                    size="xs"
                    class="h-4 min-h-0 w-4 min-w-0 p-0"
                    aria-label="Abrir catálogo de KPIs"
                    @click="openKpiCatalogHelp"
                  />
                </div>
                <USelect
                  :model-value="draft.kpiId"
                  :items="getKpiOptionsForRow(draft.kpiId)"
                  placeholder="Selecione um KPI"
                  class="w-full"
                  @update:model-value="(value) => updateDraftKpiId(draft.draftId, value as string | undefined)"
                />
              </div>

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

              <div class="space-y-1">
                <div class="flex items-center gap-1.5 text-sm">
                  <span class="text-highlighted">Nível de confiança</span>
                  <UButton
                    type="button"
                    icon="i-lucide-circle-help"
                    variant="ghost"
                    color="neutral"
                    size="xs"
                    class="h-4 min-h-0 w-4 min-w-0 p-0"
                    aria-label="Explicar níveis de confiança"
                    @click="openConfidenceHelp"
                  />
                </div>
                <USelect
                  v-model="draft.confidenceLevel"
                  :items="confidenceLevelOptions"
                  class="w-full"
                />
              </div>

              <div class="md:col-span-4">
                <UFormField label="Observação" class="w-full">
                  <UTextarea
                    :model-value="draft.observationInput"
                    :rows="2"
                    placeholder="Observação sobre o vínculo do KPI (opcional)"
                    class="w-full"
                    @update:model-value="(value) => updateDraftObservation(draft.draftId, value)"
                  />
                </UFormField>
              </div>

              <div class="flex items-end justify-end gap-2 md:row-span-2 md:self-stretch">
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

              <p v-if="getKpiImpactSummary(draft)" class="text-xs leading-relaxed text-muted md:col-span-5">
                {{ getKpiImpactSummary(draft) }}
              </p>
            </div>
          </article>

          <div class="flex flex-wrap items-center gap-3">
            <UButton
              v-if="(props.availableKpis ?? []).length"
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

    <UCard :ui="{ body: 'p-4 sm:p-5' }">
      <div class="space-y-3">
        <div>
          <h2 class="text-sm font-semibold text-highlighted">Apuração de KPI</h2>
          <p class="mt-1 text-xs text-muted">
            Registre as apurações dos KPIs vinculados a qualquer momento. A mais recente passa a ser considerada a apuração atual.
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
            class="rounded-xl border border-default bg-elevated p-3"
          >
            <div class="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
              <div class="space-y-1">
                <div class="flex flex-wrap items-center gap-2">
                  <p class="text-sm font-semibold text-highlighted">{{ link.kpiName }}</p>
                  <p class="text-xs text-muted">
                    Confiança: {{ confidenceLevelOptions.find(option => option.value === link.confidenceLevel)?.label ?? link.confidenceLevel }}
                  </p>
                </div>
                <p v-if="getPersistedKpiImpactSummary(link)" class="text-xs text-muted">
                  Impacto esperado: {{ getPersistedKpiImpactSummary(link) }}
                </p>
                <p v-if="link.observation" class="text-xs leading-relaxed text-muted">
                  Observação da configuração: {{ link.observation }}
                </p>
              </div>

              <div class="flex flex-wrap items-center gap-2">
                <UButton
                  type="button"
                  size="xs"
                  variant="outline"
                  color="neutral"
                  icon="i-lucide-link"
                  label="Lógica da apuração"
                  @click="openMeasurementReferenceModal(link.id)"
                />
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

            <div v-if="getMeasurementsForKpi(link.kpiId).length" class="mt-4 space-y-3">
              <div v-if="getMeasurementChartData(link.kpiId)" class="rounded-lg border border-default bg-default p-4">
                <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <p class="text-sm font-semibold text-highlighted">Evolução da apuração</p>
                    <p class="text-xs text-muted">Linha temporal das apurações registradas para este KPI.</p>
                  </div>
                  <div class="flex flex-wrap items-center gap-3 text-xs text-muted">
                    <span>Menor: {{ formatMeasurementValue(getMeasurementChartData(link.kpiId)!.minValue) }}</span>
                    <span>Maior: {{ formatMeasurementValue(getMeasurementChartData(link.kpiId)!.maxValue) }}</span>
                    <span v-if="getMeasurementChartData(link.kpiId)!.estimatedImpact != null">Impacto esperado: {{ formatMeasurementValue(getMeasurementChartData(link.kpiId)!.estimatedImpact!) }}</span>
                  </div>
                </div>

                <svg
                  :viewBox="`0 0 ${getMeasurementChartData(link.kpiId)!.width} ${getMeasurementChartData(link.kpiId)!.height}`"
                  class="mx-auto block w-full max-w-xl"
                  :style="{ aspectRatio: `${getMeasurementChartData(link.kpiId)!.width} / ${getMeasurementChartData(link.kpiId)!.height}` }"
                  preserveAspectRatio="xMidYMid meet"
                >
                  <defs>
                    <linearGradient :id="`measurement-gradient-${link.kpiId}`" x1="0" x2="0" y1="0" y2="1">
                      <stop offset="0%" stop-color="rgb(59 130 246 / 0.32)" />
                      <stop offset="100%" stop-color="rgb(59 130 246 / 0.04)" />
                    </linearGradient>
                  </defs>

                  <g>
                    <line
                      v-for="gridLine in getMeasurementChartData(link.kpiId)!.gridLines"
                      :key="gridLine.id"
                      x1="18"
                      :x2="getMeasurementChartData(link.kpiId)!.width - 18"
                      :y1="gridLine.y"
                      :y2="gridLine.y"
                      stroke="rgb(148 163 184 / 0.18)"
                      stroke-dasharray="4 6"
                    />
                  </g>

                  <path
                    :d="getMeasurementChartData(link.kpiId)!.areaPath"
                    :fill="`url(#measurement-gradient-${link.kpiId})`"
                  />
                  <path
                    :d="getMeasurementChartData(link.kpiId)!.linePath"
                    fill="none"
                    stroke="rgb(59 130 246)"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="3"
                  />
                  <line
                    v-if="getMeasurementChartData(link.kpiId)!.estimatedImpactY != null"
                    x1="18"
                    :x2="getMeasurementChartData(link.kpiId)!.width - 18"
                    :y1="getMeasurementChartData(link.kpiId)!.estimatedImpactY!"
                    :y2="getMeasurementChartData(link.kpiId)!.estimatedImpactY!"
                    stroke="rgb(245 158 11)"
                    stroke-dasharray="6 6"
                    stroke-width="2"
                  />
                  <g v-for="point in getMeasurementChartData(link.kpiId)!.points" :key="point.id">
                    <text
                      :x="point.x"
                      :y="point.labelY"
                      text-anchor="middle"
                      fill="rgb(15 23 42)"
                      class="text-[10px] font-bold"
                    >
                      {{ formatMeasurementValue(point.measuredValue) }}
                    </text>
                    <circle :cx="point.x" :cy="point.y" r="5" fill="rgb(255 255 255)" stroke="rgb(59 130 246)" stroke-width="3" />
                    <title>{{ `${formatMeasurementDate(point.measurementDate)} - ${formatMeasurementValue(point.measuredValue)}` }}</title>
                  </g>
                </svg>

                <div class="mt-3 flex items-center justify-between text-xs text-muted">
                  <span>{{ getMeasurementChartData(link.kpiId)!.firstLabel }}</span>
                  <span>{{ getMeasurementChartData(link.kpiId)!.lastLabel }}</span>
                </div>
              </div>

              <div class="overflow-x-auto rounded-lg border border-default bg-default">
                <table class="w-full min-w-[720px] border-separate border-spacing-0 text-sm">
                  <thead class="bg-elevated/70 text-xs uppercase tracking-[0.08em] text-muted">
                    <tr>
                      <th class="border-b border-default px-3 py-2 text-left">Seq.</th>
                      <th class="border-b border-default px-3 py-2 text-left">Data</th>
                      <th class="border-b border-default px-3 py-2 text-left">Impacto apurado</th>
                      <th class="border-b border-default px-3 py-2 text-left">Resultado</th>
                      <th class="border-b border-default px-3 py-2 text-left">Observação</th>
                      <th class="border-b border-default px-3 py-2 text-right">Ações</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="measurement in getMeasurementsTimeline(link.kpiId)"
                      :key="measurement.id"
                      class="align-top hover:bg-elevated/30"
                    >
                      <td class="border-b border-default px-3 py-2 text-muted">
                        {{ getMeasurementSequenceIndex(link.kpiId, measurement.id) }}
                      </td>
                      <td class="border-b border-default px-3 py-2">
                        <div>
                          <span class="font-medium text-highlighted">{{ formatMeasurementDate(measurement.measurementDate) }}</span>
                        </div>
                      </td>
                      <td class="border-b border-default px-3 py-2 font-medium text-highlighted">
                        {{ formatMeasurementValue(measurement.measuredValue) }}
                      </td>
                      <td class="border-b border-default px-3 py-2">
                        <span class="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium" :class="getMeasurementResultTone(getDisplayedMeasurementResult(link.kpiId, measurement))">
                          {{ getMeasurementResultLabel(getDisplayedMeasurementResult(link.kpiId, measurement)) }}
                        </span>
                      </td>
                      <td class="border-b border-default px-3 py-2 text-muted">
                        {{ measurement.observation || '—' }}
                      </td>
                      <td class="border-b border-default px-3 py-2">
                        <div class="flex items-center justify-end gap-1">
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
                            @click="requestDeleteMeasurement(measurement.id)"
                          />
                        </div>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <p v-else class="mt-4 text-xs italic text-muted">
              Nenhuma apuração registrada para este KPI.
            </p>
          </article>
        </div>
      </div>
    </UCard>

    <UModal v-model:open="helpModalOpen" :ui="{ content: 'sm:max-w-4xl' }">
      <template #header>
        <h3 class="text-lg font-semibold text-highlighted">{{ helpModalTitle }}</h3>
      </template>

      <template #body>
        <div v-if="helpModalType === 'confidence'" class="space-y-3 p-4 text-sm text-highlighted">
          <p class="font-medium">
            Nível de confiança = o quanto você acredita que aquele épico realmente vai impactar o KPI.
          </p>
          <div
            v-for="item in confidenceLevelHelp"
            :key="item.title"
            class="space-y-1 rounded-lg border border-default p-3"
          >
            <p class="font-semibold" :class="item.color">{{ item.emoji }} {{ item.title }}</p>
            <p>{{ item.summary }}</p>
            <p class="text-muted">{{ item.signals }}</p>
            <p class="text-muted">{{ item.detail }}</p>
          </div>
        </div>

        <div v-else-if="helpModalType === 'kpi-catalog'" class="space-y-3 p-4">
          <div v-if="!availableKpiCatalog.length" class="rounded-lg border border-default bg-elevated/40 px-3 py-4 text-sm text-muted">
            Nenhum KPI cadastrado até o momento.
          </div>

          <article
            v-for="kpi in availableKpiCatalog"
            :key="kpi.id"
            class="space-y-3 rounded-xl border border-default bg-default p-4"
          >
            <div class="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h4 class="text-base font-semibold text-highlighted">{{ kpi.name }}</h4>
              </div>
              <div class="flex flex-wrap gap-2 text-xs">
                <span class="rounded-full border border-default bg-elevated px-2 py-1 text-muted">Tipo: {{ kpiTypeLabels[kpi.type] }}</span>
                <span class="rounded-full border border-default bg-elevated px-2 py-1 text-muted">Alavanca: {{ kpiLeverLabels[kpi.lever] }}</span>
                <span class="rounded-full border border-default bg-elevated px-2 py-1 text-muted">Objetivo: {{ kpiObjectiveLabels[kpi.objective] }}</span>
              </div>
            </div>

            <div class="grid gap-3 md:grid-cols-2">
              <div class="space-y-1">
                <p class="text-xs font-semibold uppercase tracking-[0.08em] text-muted">Descrição</p>
                <p class="text-sm text-highlighted">{{ kpi.description || '—' }}</p>
              </div>
              <div class="space-y-1">
                <p class="text-xs font-semibold uppercase tracking-[0.08em] text-muted">Como calcular</p>
                <p class="text-sm text-highlighted">{{ kpi.calculation || '—' }}</p>
              </div>
            </div>
          </article>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end">
          <UButton label="Fechar" variant="ghost" @click="closeHelpModal" />
        </div>
      </template>
    </UModal>

    <UModal v-model:open="measurementReferenceModalOpen" :ui="{ content: 'sm:max-w-2xl' }">
      <template #header>
        <h3 class="text-lg font-semibold text-highlighted">Lógica da apuração</h3>
      </template>

      <template #body>
        <div v-if="activeMeasurementReferenceLink" class="space-y-4 p-4">
          <div>
            <p class="text-sm font-semibold text-highlighted">{{ activeMeasurementReferenceLink.kpiName }}</p>
            <p class="mt-1 text-xs text-muted">
              Essa referência é única para o KPI vinculado ao épico e vale para todas as apurações.
            </p>
          </div>

          <UFormField label="Link da lógica de apuração" class="w-full">
            <UInput
              :model-value="measurementReferenceDrafts[activeMeasurementReferenceLink.id] ?? ''"
              type="url"
              placeholder="https://..."
              class="w-full"
              @update:model-value="(value) => updateMeasurementReferenceDraft(activeMeasurementReferenceLink.id, value)"
            />
          </UFormField>

          <div v-if="activeMeasurementReferenceLink.measurementReferenceUrl" class="text-sm text-muted">
            <span>Referência atual:</span>
            <a
              :href="activeMeasurementReferenceLink.measurementReferenceUrl"
              target="_blank"
              rel="noreferrer"
              class="ml-1 break-all text-primary hover:underline"
            >
              {{ activeMeasurementReferenceLink.measurementReferenceUrl }}
            </a>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton label="Fechar" variant="ghost" color="neutral" @click="closeMeasurementReferenceModal" />
          <UButton
            v-if="activeMeasurementReferenceLink"
            icon="i-lucide-save"
            label="Salvar referência"
            :loading="kpiLinkSavingDraftId === activeMeasurementReferenceLink.id"
            @click="saveMeasurementReference(activeMeasurementReferenceLink.id)"
          />
        </div>
      </template>
    </UModal>

    <UModal v-model:open="deleteConfirmationOpen" :ui="{ content: 'sm:max-w-md' }">
      <template #header>
        <h3 class="text-lg font-semibold text-highlighted">{{ deleteConfirmationTitle }}</h3>
      </template>

      <template #body>
        <div class="p-4 text-sm text-muted">
          {{ deleteConfirmationDescription }}
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton label="Cancelar" variant="ghost" color="neutral" @click="deleteConfirmationOpen = false" />
          <UButton
            color="error"
            icon="i-lucide-trash-2"
            label="Excluir"
            :loading="(deleteConfirmation?.type === 'kpi-link' && kpiLinkDeletingId === deleteConfirmation.id)
              || (deleteConfirmation?.type === 'measurement' && measurementDeletingId === deleteConfirmation.id)"
            @click="confirmDeletion"
          />
        </div>
      </template>
    </UModal>
  </div>
</template>