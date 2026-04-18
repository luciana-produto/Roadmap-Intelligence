<script setup lang="ts">
import type {
  RoadmapDemand,
  RoadmapProject,
  DemandDependencyOption,
  DemandFormData,
  DemandType,
  DemandClassification,
  DemandStatus,
  Kpi,
  DemandKpiLink,
  DemandKpiLinkInput,
  ImpactType,
  ConfidenceLevel,
  KpiMeasurement,
  MeasurementResult,
  CreateDemandKpiMeasurementInput,
  UpdateDemandKpiMeasurementInput
} from '~/types/roadmap'

type DemandFormState = Omit<DemandFormData, 'classification'> & {
  classification: DemandClassification | ''
}

type ImpactDisplayType = 'Percentage' | 'Number' | 'Currency'

type EditableDemandKpiLink = DemandKpiLinkInput & {
  impactDisplayType: ImpactDisplayType
  estimatedImpactInput: string
}

type MeasurementEditorState = {
  id?: string
  kpiId: string
  measuredValueInput: string
  measurementDate: string
  result: MeasurementResult
  observation: string
}

const kpiStore = useKpiStore()
const roadmapStore = useRoadmapStore()
const toast = useToast()

const props = defineProps<{
  open: boolean
  projects: RoadmapProject[]
  dependencyOptions: DemandDependencyOption[]
  customerSuggestions: string[]
  demand?: RoadmapDemand | null
  defaultProjectId?: string
  defaultQuarterYear?: number
  defaultQuarterNumber?: number
  availableKpis?: Kpi[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  submit: [data: DemandFormData]
}>()

const isEdit = computed(() => !!props.demand)
const title = computed(() => isEdit.value ? 'Editar Demanda' : 'Nova Demanda')

const currentYear = new Date().getFullYear()
const quarters = [
  { value: '0-0', label: 'Backlog' },
  ...[currentYear, currentYear + 1].flatMap(y =>
    [1, 2, 3, 4].map(q => ({
      value: `${q}-${y}`,
      label: `Q${q}/${String(y).slice(2)}`
    }))
  )
]

const typeOptions = [
  { value: 'Planned',    label: 'Planejado' },
  { value: 'Spillover',  label: 'Transbordo' },
  { value: 'Unplanned',  label: 'Não Planejado' },
  { value: 'Additional', label: 'Adicional' }
]

const classificationOptions = [
  { value: 'TechnicalDebtSecurity', label: 'Débito Técnico' },
  { value: 'Strategic',     label: 'Estratégico' },
  { value: 'Evolution',     label: 'Evolução' },
  { value: 'ImprovementGap', label: 'Melhoria/Gap' },
  { value: 'Mandatory',     label: 'Mandatório' },
  { value: 'Homologation',  label: 'Homologação' },
  { value: 'Customizacao',  label: 'Customização' }
]

const statusOptions = [
  { value: 'Backlog',       label: 'Backlog' },
  { value: 'InProgress',    label: 'Em andamento' },
  { value: 'Done',          label: 'Concluído' },
  { value: 'Deprioritized', label: 'Despriorizado' }
]

type DemandFormTab = 'general' | 'status'

const resultTabs = computed(() => {
  const tabs: Array<{ value: DemandFormTab, label: string }> = [
    { value: 'general', label: 'Geral' }
  ]

  if (isEdit.value)
    tabs.push({ value: 'status', label: 'Status' })

  return tabs
})

const customerInput = ref('')
const dependencySearch = ref('')
const activeTab = ref<DemandFormTab>('general')
const showSubmitHint = ref(false)
let submitHintTimeout: ReturnType<typeof setTimeout> | null = null

const observationRequired = computed(() => form.status === 'Deprioritized')
const deliveryDateRequired = computed(() => form.status === 'Done')

const form = reactive<DemandFormState>({
  title: '',
  description: '',
  projectId: '',
  quarterYear: currentYear,
  quarterNumber: 1,
  type: 'Planned',
  classification: '',
  productIds: [],
  status: 'Backlog',
  observation: '',
  jiraIssue: '',
  hours: undefined,
  customers: [],
  dependencyDemandIds: [],
  isBlocked: false,
  blockedReason: '',
  promisedDate: '',
  deliveryDate: '',
  problemClarity: undefined,
  hasNoKpi: false,
  noKpiClassification: undefined
})

const kpiLinkEdits = ref<EditableDemandKpiLink[]>([])
const kpiMeasurements = ref<KpiMeasurement[]>([])
const measurementDrafts = ref<Record<string, MeasurementEditorState>>({})
const measurementSavingKpiId = ref<string | null>(null)
const measurementDeletingId = ref<string | null>(null)

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

const measurementResultOptions = [
  { value: 'Positive', label: 'Positivo' },
  { value: 'Neutral', label: 'Neutro' },
  { value: 'Negative', label: 'Negativo' }
]

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

const selectedQuarter = computed({
  get: () => `${form.quarterNumber}-${form.quarterYear}`,
  set: (val: string) => {
    const [q, y] = val.split('-').map(Number)
    form.quarterNumber = q
    form.quarterYear = y
  }
})

const productsForProject = computed(() =>
  props.projects.find(p => p.id === form.projectId)?.products ?? []
)

function syncSingleProductSelection() {
  if (isEdit.value)
    return

  if (productsForProject.value.length === 1) {
    form.productIds = [productsForProject.value[0]!.id]
    return
  }

  form.productIds = []
}

watch(() => props.open, (open) => {
  if (!open) return

  activeTab.value = 'general'
  showSubmitHint.value = false

  if (props.demand) {
    form.title = props.demand.title
    form.description = props.demand.description ?? ''
    form.projectId = props.demand.projectId
    form.quarterYear = props.demand.quarterYear
    form.quarterNumber = props.demand.quarterNumber
    form.type = props.demand.type
    form.classification = props.demand.classification
    form.productIds = props.demand.products.map(p => p.productId)
    form.status = props.demand.status
    form.observation = props.demand.observation ?? ''
    form.jiraIssue = props.demand.jiraIssue ?? ''
    form.hours = props.demand.hours ?? undefined
    form.customers = props.demand.customers ?? []
    form.dependencyDemandIds = props.demand.dependsOn.map(item => item.demandId)
    form.isBlocked = props.demand.isBlocked
    form.blockedReason = props.demand.blockedReason ?? ''
    form.promisedDate = props.demand.promisedDate ?? ''
    form.deliveryDate = props.demand.deliveryDate ?? ''
    form.problemClarity = props.demand.problemClarity ?? undefined
    form.hasNoKpi = props.demand.hasNoKpi ?? false
    form.noKpiClassification = props.demand.noKpiClassification ?? undefined
    kpiLinkEdits.value = (props.demand.kpiLinks ?? []).map(l => toEditableKpiLink({
      kpiId: l.kpiId,
      impactType: l.impactType,
      estimatedImpact: l.estimatedImpact,
      confidenceLevel: l.confidenceLevel
    }))
    kpiMeasurements.value = sortMeasurements(props.demand.kpiMeasurements ?? [])
    measurementDrafts.value = {}
    customerInput.value = ''
  }
  else {
    form.title = ''
    form.description = ''
    form.projectId = props.defaultProjectId ?? props.projects[0]?.id ?? ''
    form.quarterYear = props.defaultQuarterYear ?? currentYear
    form.quarterNumber = props.defaultQuarterNumber ?? 1
    form.type = 'Planned'
    form.classification = ''
    form.productIds = []
    form.status = 'Backlog'
    form.observation = ''
    form.jiraIssue = ''
    form.hours = undefined
    form.customers = []
    form.dependencyDemandIds = []
    form.isBlocked = false
    form.blockedReason = ''
    form.promisedDate = ''
    form.deliveryDate = ''
    form.problemClarity = undefined
    form.hasNoKpi = false
    form.noKpiClassification = undefined
    kpiLinkEdits.value = []
    kpiMeasurements.value = []
    measurementDrafts.value = {}
    customerInput.value = ''
    syncSingleProductSelection()
  }
})

watch(() => form.projectId, () => {
  syncSingleProductSelection()
})

watch(() => form.isBlocked, (val) => {
  if (!val) form.blockedReason = ''
})

watch(() => form.status, (status) => {
  if (status === 'Done') {
    form.isBlocked = false
    form.blockedReason = ''
  }
})

const customerTags = computed(() =>
  form.customers ?? []
)

const hasCustomerQuery = computed(() => customerInput.value.trim().length > 0)

const filteredCustomerSuggestions = computed(() => {
  const query = customerInput.value.trim().toLowerCase()
  if (!query)
    return []

  const selected = new Set(customerTags.value.map(customer => customer.toLowerCase()))

  return props.customerSuggestions
    .filter(customer => !selected.has(customer.toLowerCase()))
    .filter(customer => customer.toLowerCase().includes(query))
    .slice(0, 8)
})

const canCreateCustomerFromInput = computed(() => {
  const normalized = customerInput.value.trim()
  if (!normalized)
    return false

  return !customerTags.value.some(customer => customer.toLowerCase() === normalized.toLowerCase())
})

const hasDependencyQuery = computed(() => dependencySearch.value.trim().length > 0)

const filteredDependencyOptions = computed(() => {
  const query = dependencySearch.value.trim().toLowerCase()
  if (!query)
    return []

  return props.dependencyOptions.filter(option => {
    if (props.demand && option.demandId === props.demand.id)
      return false

    return `${option.projectName} ${option.title} ${option.quarterLabel} ${option.status}`.toLowerCase().includes(query)
  })
})

const selectedDependencyOptions = computed(() => {
  const selectedIds = new Set(form.dependencyDemandIds ?? [])
  return props.dependencyOptions.filter(option => selectedIds.has(option.demandId))
})

function setCustomerTags(tags: string[]) {
  form.customers = [...new Set(tags.map(tag => tag.trim()).filter(Boolean))]
}

function addCustomerTag(value: string) {
  const normalized = value.trim()
  if (!normalized) return
  setCustomerTags([...customerTags.value, normalized])
  customerInput.value = ''
}

function handleCustomerEnter() {
  const firstSuggestion = filteredCustomerSuggestions.value[0]
  addCustomerTag(firstSuggestion ?? customerInput.value)
}

function removeCustomerTag(tag: string) {
  setCustomerTags(customerTags.value.filter(customer => customer !== tag))
}

function toggleProduct(id: string, checked: boolean) {
  if (checked)
    form.productIds = [...form.productIds, id]
  else
    form.productIds = form.productIds.filter(p => p !== id)
}

function toggleDependency(demandId: string, checked: boolean) {
  if (checked) {
    form.dependencyDemandIds = [...new Set([...(form.dependencyDemandIds ?? []), demandId])]
    return
  }

  form.dependencyDemandIds = (form.dependencyDemandIds ?? []).filter(id => id !== demandId)
}

function removeDependency(demandId: string) {
  form.dependencyDemandIds = (form.dependencyDemandIds ?? []).filter(id => id !== demandId)
}

function updateHours(value: string | number | null | undefined) {
  if (value === '' || value == null) {
    form.hours = undefined
    return
  }

  const parsed = typeof value === 'number' ? value : Number(value)
  form.hours = Number.isNaN(parsed) ? undefined : parsed
}

function updateProblemClarity(value: string | number | null | undefined) {
  if (value === '' || value == null) {
    form.problemClarity = undefined
    return
  }

  const parsed = typeof value === 'number' ? value : Number(value)
  if (Number.isNaN(parsed)) {
    form.problemClarity = undefined
    return
  }

  form.problemClarity = Math.min(10, Math.max(0, Math.round(parsed)))
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
    estimatedImpactInput: formatEstimatedImpact(estimatedImpact, impactDisplayType)
  }
}

function updateImpactDisplayType(index: number, value: string | undefined) {
  const link = kpiLinkEdits.value[index]
  if (!link)
    return

  const nextType = (value ?? 'Number') as ImpactDisplayType
  link.impactDisplayType = nextType
  link.estimatedImpactInput = formatEstimatedImpact(link.estimatedImpact, nextType)
}

function updateEstimatedImpactInput(index: number, value: string | number | null | undefined) {
  const link = kpiLinkEdits.value[index]
  if (!link)
    return

  const rawValue = typeof value === 'number' ? String(value) : (value ?? '')
  const parsed = parseMaskedNumber(rawValue)
  link.estimatedImpact = parsed
  link.estimatedImpactInput = parsed == null
    ? ''
    : formatEstimatedImpact(parsed, link.impactDisplayType)
}

function addKpiLink() {
  kpiLinkEdits.value.push(toEditableKpiLink())
}

function removeKpiLink(index: number) {
  kpiLinkEdits.value.splice(index, 1)
}

const availableKpisForLink = computed(() => {
  const usedIds = new Set(kpiLinkEdits.value.map(l => l.kpiId))
  return (props.availableKpis ?? []).filter(k => !usedIds.has(k.id))
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
  if (!isEdit.value || !props.demand?.id)
    return { enabled: false, message: 'Salve a demanda primeiro para registrar apurações.' }

  if (form.hasNoKpi || persistedKpiLinks.value.length === 0)
    return { enabled: false, message: 'A apuração só fica disponível para demandas com KPI vinculado.' }

  if (form.status !== 'Done')
    return { enabled: false, message: 'A apuração fica disponível após a entrega da demanda.' }

  return { enabled: true, message: '' }
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

  measurementSavingKpiId.value = kpiId

  try {
    if (draft.id) {
      const payload: UpdateDemandKpiMeasurementInput = {
        measuredValue,
        measurementDate: draft.measurementDate,
        result: draft.result,
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
        result: draft.result,
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

function isKpiLinkComplete(link: EditableDemandKpiLink) {
  return !!link.kpiId
}

const missingSubmitReason = computed(() => {
  if (!form.title)
    return 'Informe o título da demanda'
  if (!form.projectId)
    return 'Selecione o projeto'
  if (!form.classification)
    return 'Selecione a classificação'
  if (form.productIds.length === 0)
    return 'Selecione ao menos um produto'
  if (observationRequired.value && !form.observation)
    return 'Preencha a observação para demanda despriorizada'
  if (deliveryDateRequired.value && !form.deliveryDate)
    return 'Informe a data de entrega para concluir a demanda'
  if (form.isBlocked && !form.blockedReason)
    return 'Preencha o motivo do impedimento'

  return null
})

const isSubmitDisabled = computed(() => !!missingSubmitReason.value)
const submitButtonLabel = computed(() => isEdit.value ? 'Editar demanda' : 'Criar demanda')

const isSubmitting = ref(false)

function clearSubmitHintTimer() {
  if (submitHintTimeout) {
    clearTimeout(submitHintTimeout)
    submitHintTimeout = null
  }
}

function openSubmitHint() {
  showSubmitHint.value = true
  clearSubmitHintTimer()
  submitHintTimeout = setTimeout(() => {
    showSubmitHint.value = false
    submitHintTimeout = null
  }, 2500)
}

function focusRelevantTab() {
  if (
    missingSubmitReason.value === 'Preencha a observação para demanda despriorizada'
    || missingSubmitReason.value === 'Informe a data de entrega para concluir a demanda'
    || missingSubmitReason.value === 'Preencha o motivo do impedimento'
  ) {
    activeTab.value = isEdit.value ? 'status' : 'general'
    return
  }

  activeTab.value = 'general'
}

function handleSubmitClick() {
  if (isSubmitDisabled.value) {
    focusRelevantTab()
    openSubmitHint()
    return
  }

  showSubmitHint.value = false
  handleSubmit()
}

async function handleSubmit() {
  if (isSubmitDisabled.value) return
  isSubmitting.value = true
  try {
    emit('submit', {
      ...form,
      hours: Number.isNaN(form.hours as number) ? undefined : form.hours,
      classification: form.classification as DemandClassification
    })
  }
  finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <UModal
    :open="open"
    :title="title"
    :ui="{ content: 'sm:max-w-4xl' }"
    @update:open="emit('update:open', $event)"
  >
    <template #body>
      <form
        class="min-h-[38rem] space-y-4"
        @submit.prevent="handleSubmit"
      >
        <div class="flex gap-2 border-b border-default pb-3">
          <button
            v-for="tab in resultTabs"
            :key="tab.value"
            type="button"
            class="rounded-lg px-3 py-2 text-sm font-medium transition-colors"
            :class="activeTab === tab.value ? 'bg-primary text-inverted' : 'bg-elevated text-muted hover:text-highlighted'"
            @click="activeTab = tab.value"
          >
            {{ tab.label }}
          </button>
        </div>

        <template v-if="activeTab === 'general'">
        <section class="space-y-4">
          <div>
            <h3 class="text-sm font-semibold text-highlighted">Dados da demanda</h3>
          </div>

          <UFormField label="Título" required>
            <UInput
              v-model="form.title"
              placeholder="Descreva a demanda brevemente"
              class="w-full"
            />
          </UFormField>

          <UFormField label="Descrição">
            <UTextarea
              v-model="form.description"
              placeholder="Detalhes adicionais (opcional)"
              :rows="2"
              class="w-full"
            />
          </UFormField>

          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <UFormField label="Projeto" required>
              <USelect
                v-model="form.projectId"
                :items="projects.map(p => ({ value: p.id, label: p.name }))"
                placeholder="Selecione"
                class="w-full"
                :disabled="isEdit"
              />
            </UFormField>

            <UFormField label="Quarter" required>
              <USelect
                v-model="selectedQuarter"
                :items="quarters"
                placeholder="Selecione"
                class="w-full"
              />
            </UFormField>

            <UFormField label="Tipo" required>
              <USelect
                v-model="form.type as DemandType"
                :items="typeOptions"
                class="w-full"
              />
            </UFormField>

            <UFormField label="Classificação" required>
              <USelect
                v-model="form.classification"
                :items="classificationOptions"
                placeholder="Selecione"
                class="w-full"
              />
            </UFormField>
          </div>

          <div class="grid grid-cols-2 gap-3 md:grid-cols-4">
            <UFormField label="">
              <div class="space-y-1.5">
                <div class="flex items-center gap-1.5">
                  <span class="text-sm text-highlighted">Nota da clareza</span>
                  <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                    <UButton
                      type="button"
                      icon="i-lucide-circle-help"
                      variant="ghost"
                      color="neutral"
                      size="xs"
                      aria-label="Explicar clareza do problema"
                    />
                    <template #content>
                      <div class="max-w-sm p-4 text-sm text-highlighted">
                        Clareza do Problema: Sabemos qual problema estamos resolvendo - ou só estamos construindo alguma coisa?
                      </div>
                    </template>
                  </UPopover>
                </div>
                <UInput
                  :model-value="form.problemClarity ?? ''"
                  type="number"
                  min="0"
                  max="10"
                  step="1"
                  placeholder="0 a 10"
                  class="w-full"
                  @update:model-value="updateProblemClarity"
                />
              </div>
            </UFormField>

            <UFormField label="Issue (Jira)">
              <UInput
                v-model="form.jiraIssue"
                placeholder="Ex: PROJ-1234"
                class="w-full"
              />
            </UFormField>

            <UFormField label="Horas">
              <UInput
                :model-value="form.hours ?? ''"
                type="number"
                min="0"
                step="0.5"
                placeholder="Ex: 8"
                class="w-full"
                @update:model-value="updateHours"
              />
            </UFormField>

            <UFormField label="Data prometida">
              <UInput
                v-model="form.promisedDate"
                type="date"
                class="w-full"
              />
            </UFormField>
          </div>

          <UFormField label="Clientes envolvidos">
            <div class="space-y-2">
              <div class="rounded-lg border border-default bg-elevated p-2">
                <div class="flex min-h-10 flex-wrap items-center gap-2">
                  <span
                    v-for="customer in customerTags"
                    :key="customer"
                    class="inline-flex items-center gap-1 rounded-full border border-primary/20 bg-primary/10 px-2 py-1 text-xs text-primary"
                  >
                    {{ customer }}
                    <button
                      type="button"
                      class="inline-flex h-4 w-4 items-center justify-center rounded-full hover:bg-primary/15"
                      @click="removeCustomerTag(customer)"
                    >
                      <UIcon name="i-lucide-x" class="h-3 w-3" />
                    </button>
                  </span>

                  <input
                    v-model="customerInput"
                    type="text"
                    class="min-w-[12rem] flex-1 bg-transparent px-1 py-1 text-sm text-highlighted outline-none placeholder:text-muted"
                    placeholder="Digite para buscar ou criar um cliente"
                    @keydown.enter.prevent="handleCustomerEnter"
                  >
                </div>
              </div>

              <div
                v-if="hasCustomerQuery && (filteredCustomerSuggestions.length || canCreateCustomerFromInput)"
                class="rounded-lg border border-default bg-default shadow-sm"
              >
                <p class="border-b border-default px-3 py-2 text-xs font-semibold uppercase tracking-[0.08em] text-muted">
                  Sugestões
                </p>

                <button
                  v-for="customer in filteredCustomerSuggestions"
                  :key="customer"
                  type="button"
                  class="flex w-full items-center justify-between px-3 py-2 text-left text-sm text-highlighted transition-colors hover:bg-elevated"
                  @click="addCustomerTag(customer)"
                >
                  <span class="truncate">{{ customer }}</span>
                </button>

                <button
                  v-if="canCreateCustomerFromInput"
                  type="button"
                  class="flex w-full items-center justify-between border-t border-default px-3 py-2 text-left text-sm text-highlighted transition-colors hover:bg-elevated"
                  @click="addCustomerTag(customerInput)"
                >
                  <span class="truncate"><strong>{{ customerInput.trim() }}</strong> (Novo Cliente)</span>
                </button>
              </div>
            </div>
          </UFormField>

          <UFormField label="Produtos" required>
            <div
              v-if="productsForProject.length"
              class="flex flex-wrap gap-2 p-3 rounded-lg border border-default bg-elevated"
            >
              <label
                v-for="product in productsForProject"
                :key="product.id"
                class="flex items-center gap-2 cursor-pointer px-2.5 py-1.5 rounded-lg hover:bg-default transition-colors select-none"
                :class="form.productIds.includes(product.id)
                  ? 'bg-primary/10 border border-primary/30'
                  : 'border border-transparent'"
              >
                <input
                  type="checkbox"
                  :value="product.id"
                  :checked="form.productIds.includes(product.id)"
                  class="accent-primary w-3.5 h-3.5"
                  @change="(e) => toggleProduct(product.id, (e.target as HTMLInputElement).checked)"
                >
                <span class="text-sm">{{ product.name }}</span>
              </label>
            </div>
            <p
              v-else
              class="text-xs text-muted italic"
            >
              Selecione um projeto primeiro.
            </p>
          </UFormField>
        </section>

        <section class="space-y-4 border-t border-default pt-4">
          <div>
            <h3 class="text-sm font-semibold text-highlighted">Dependências entre demandas</h3>
            <p class="mt-1 text-xs text-muted">
              Relacione demandas que precisam ser concluídas antes desta seguir adiante.
            </p>
          </div>

          <div class="space-y-2">
            <UInput
              v-model="dependencySearch"
              placeholder="Digite para buscar por projeto, título, quarter ou status"
              icon="i-lucide-search"
              class="w-full"
            />

            <div
              v-if="selectedDependencyOptions.length"
              class="flex flex-wrap gap-2 rounded-lg border border-default bg-elevated p-2"
            >
              <span
                v-for="dependency in selectedDependencyOptions"
                :key="dependency.demandId"
                class="inline-flex items-center gap-1 rounded-full border border-primary/20 bg-primary/10 px-2 py-1 text-xs text-primary"
              >
                {{ dependency.projectName }} · {{ dependency.title }}
                <button
                  type="button"
                  class="inline-flex h-4 w-4 items-center justify-center rounded-full hover:bg-primary/15"
                  @click="removeDependency(dependency.demandId)"
                >
                  <UIcon name="i-lucide-x" class="h-3 w-3" />
                </button>
              </span>
            </div>

            <div
              v-if="hasDependencyQuery"
              class="max-h-56 space-y-2 overflow-y-auto rounded-lg border border-default bg-elevated p-2.5"
            >
              <label
                v-for="dependency in filteredDependencyOptions"
                :key="dependency.demandId"
                class="flex cursor-pointer items-start gap-3 rounded-lg border border-transparent px-2.5 py-2 transition-colors hover:bg-default"
                :class="form.dependencyDemandIds?.includes(dependency.demandId) ? 'border-primary/30 bg-primary/5' : ''"
              >
                <input
                  type="checkbox"
                  class="mt-0.5 h-4 w-4 accent-primary"
                  :checked="form.dependencyDemandIds?.includes(dependency.demandId)"
                  @change="(event) => toggleDependency(dependency.demandId, (event.target as HTMLInputElement).checked)"
                >
                <div class="min-w-0">
                  <p class="truncate text-sm font-medium text-highlighted">{{ dependency.title }}</p>
                  <p class="text-xs text-muted">{{ dependency.projectName }} · {{ dependency.quarterLabel }} · {{ dependency.status }}</p>
                </div>
              </label>

              <p v-if="!filteredDependencyOptions.length" class="text-xs italic text-muted">
                Nenhuma demanda encontrada para vincular.
              </p>
            </div>
          </div>
        </section>
        </template>

        <template v-else-if="activeTab === 'status'">
        <section v-if="isEdit" class="space-y-4">
          <div>
            <h3 class="text-sm font-semibold text-highlighted">Status e acompanhamento</h3>
          </div>

          <div class="grid grid-cols-1 gap-3 md:grid-cols-4">
            <UFormField label="Status">
              <USelect
                v-model="form.status as DemandStatus"
                :items="statusOptions"
                class="w-full"
              />
            </UFormField>

            <UFormField label="Impedimento">
              <label class="flex h-10 items-center gap-2 cursor-pointer select-none">
                <input
                  v-model="form.isBlocked"
                  type="checkbox"
                  class="h-4 w-4 accent-red-500"
                >
                <span class="text-sm" :class="form.isBlocked ? 'text-red-600 dark:text-red-400 font-medium' : 'text-muted'">
                  {{ form.isBlocked ? 'Demanda impedida' : 'Sem impedimento' }}
                </span>
              </label>
            </UFormField>

            <UFormField v-if="deliveryDateRequired" label="Data prometida">
              <UInput
                v-model="form.promisedDate"
                type="date"
                class="w-full"
              />
            </UFormField>

            <UFormField v-if="deliveryDateRequired" label="Data de entrega" required>
              <UInput
                v-model="form.deliveryDate"
                type="date"
                class="w-full"
                :class="!form.deliveryDate ? 'ring-2 ring-red-400' : ''"
              />
            </UFormField>
          </div>

          <UFormField
            v-if="form.isBlocked"
            label="Motivo do impedimento"
            required
          >
            <UInput
              v-model="form.blockedReason"
              placeholder="Descreva o motivo do impedimento"
              class="w-full"
              :class="!form.blockedReason ? 'ring-2 ring-red-400' : ''"
            />
          </UFormField>

          <UFormField
            v-if="observationRequired"
            label="Motivo despriorização"
            required
          >
            <UTextarea
              v-model="form.observation"
              placeholder="Justifique o motivo da despriorização"
              :rows="2"
              class="w-full"
              :class="!form.observation ? 'ring-2 ring-red-400' : ''"
            />
          </UFormField>
        </section>
        </template>

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
        <div class="relative flex items-center">
          <div
            v-if="showSubmitHint && missingSubmitReason"
            class="absolute left-full top-1/2 z-10 ml-2 w-72 -translate-y-1/2 rounded-lg border border-red-200 bg-white px-3 py-2 text-xs text-red-600 shadow-lg dark:border-red-800 dark:bg-neutral-900 dark:text-red-300"
          >
            {{ missingSubmitReason }}
          </div>

          <UButton
            :loading="isSubmitting"
            :label="submitButtonLabel"
            icon="i-lucide-check"
            :color="isSubmitDisabled ? 'neutral' : 'primary'"
            :class="isSubmitDisabled ? 'opacity-60 cursor-not-allowed' : ''"
            @click="handleSubmitClick"
          />
        </div>
      </div>
    </template>
  </UModal>
</template>
