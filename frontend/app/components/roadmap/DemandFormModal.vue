<script setup lang="ts">
import type { ApiResponse } from '~/types/api'
import type {
  RoadmapDemand,
  RoadmapProject,
  RoadmapItemType,
  DemandDependencyOption,
  DemandFormData,
  DemandType,
  DemandClassification,
  DemandStatus,
  DeprioritizationReason,
  Kpi,
  DemandKpiLink,
  DemandKpiLinkInput,
  ImpactType,
  ConfidenceLevel,
  KpiMeasurement,
  MeasurementResult,
  CreateDemandKpiMeasurementInput,
  UpdateDemandKpiMeasurementInput,
  IssueLinkInput
} from '~/types/roadmap'
import {
  sanitizeCustomersForItem,
  sanitizeIssueLinksForItem,
  sanitizePromisedDateForItem
} from '~/utils/roadmapDemandPayload'

type DemandFormState = Omit<DemandFormData, 'classification' | 'quarterYear' | 'quarterNumber'> & {
  itemType: RoadmapItemType | ''
  classification: DemandClassification | ''
  quarterYear: number | null
  quarterNumber: number | null
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

type EpicParentOption = {
  id: string
  title: string
  roadmapTitle?: string
  status?: DemandStatus
  projectId?: string
  projectIds?: string[]
}

type RoadmapParentOption = {
  id: string
  title: string
  projectId?: string
  projectIds?: string[]
}

type ParentSelectOption = {
  value: string
  label: string
  description?: string
  searchText: string
}

const api = useApi()
const kpiStore = useKpiStore()
const roadmapStore = useRoadmapStore()
const toast = useToast()

const props = defineProps<{
  open: boolean
  projects: RoadmapProject[]
  defaultItemType?: RoadmapItemType
  defaultParentDemandId?: string
  defaultProjectIds?: string[]
  roadmapOptions?: RoadmapParentOption[]
  epicOptions?: EpicParentOption[]
  dependencyOptions: DemandDependencyOption[]
  customerSuggestions: string[]
  demand?: RoadmapDemand | null
  defaultProjectId?: string
  defaultQuarterYear?: number
  defaultQuarterNumber?: number
  availableKpis?: Kpi[]
  isSaving?: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  submit: [data: DemandFormData]
}>()

const isEdit = computed(() => !!props.demand)
const itemTypeOptions = [
  { value: 'Roadmap', label: 'Roadmap' },
  { value: 'Epic', label: 'Épico' },
  { value: 'Demand', label: 'Demanda' }
]

const itemTypeLabels: Record<RoadmapItemType, string> = {
  Roadmap: 'Roadmap',
  Epic: 'Épico',
  Demand: 'Demanda'
}

const hasSelectedItemType = computed(() => form.itemType !== '')
const isRoadmap = computed(() => form.itemType === 'Roadmap')
const isEpic = computed(() => form.itemType === 'Epic')
const isDemand = computed(() => form.itemType === 'Demand')

const title = computed(() => {
  if (!hasSelectedItemType.value)
    return isEdit.value ? 'Editar item' : 'Novo Item'

  const itemLabel = itemTypeLabels[form.itemType as RoadmapItemType]
  return isEdit.value ? `Editar ${itemLabel}` : `Novo ${itemLabel}`
})

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
  { value: 'InProgress',    label: 'Doing' },
  { value: 'Done',          label: 'Concluído' },
  { value: 'Deprioritized', label: 'Despriorizado' }
]

const deprioritizationReasonOptions = [
  { value: 'Strategic', label: 'Estratégico' },
  { value: 'MandatoryUrgent', label: 'Mandatório/Urgente' },
  { value: 'LowImpact', label: 'Baixo impacto' },
  { value: 'LackOfCapacity', label: 'Falta de capacidade' },
  { value: 'ContextChange', label: 'Mudança de contexto' },
  { value: 'Customizacao', label: 'Customização' }
] as const

const deprioritizationReasonExamples: Record<DeprioritizationReason, string> = {
  Strategic: 'vamos dar foco em algo mais estratégico',
  MandatoryUrgent: 'precisamos encaixar uma demanda fiscal ou urgente',
  LowImpact: 'a demanda não justificativa o esforço neste momento',
  LackOfCapacity: 'erro de planejamento',
  ContextChange: 'Cliente desistiu / Legislação revogada',
  Customizacao: 'Será substituída por uma demanda de customização de outro cliente'
}

const deprioritizationReasonLabels: Record<DeprioritizationReason, string> = {
  Strategic: 'Estratégico',
  MandatoryUrgent: 'Mandatório/Urgente',
  LowImpact: 'Baixo impacto',
  LackOfCapacity: 'Falta de capacidade',
  ContextChange: 'Mudança de contexto',
  Customizacao: 'Customização'
}

type DemandFormTab = 'general' | 'status'

const resultTabs = computed(() => {
  const tabs: Array<{ value: DemandFormTab, label: string }> = [
    { value: 'general', label: 'Geral' }
  ]

  if (hasSelectedItemType.value && !isRoadmap.value)
    tabs.push({ value: 'status', label: 'Status' })

  return tabs
})

const customerInput = ref('')
const dependencySearch = ref('')
const parentSearch = ref('')
const activeTab = ref<DemandFormTab>('general')
const showSubmitHint = ref(false)
let submitHintTimeout: ReturnType<typeof setTimeout> | null = null

const hasStatusTab = computed(() => resultTabs.value.length > 1)

const observationRequired = computed(() => form.status === 'Deprioritized')
const deprioritizationReasonRequired = computed(() => form.status === 'Deprioritized')
const deliveryDateRequired = computed(() => form.status === 'Done')

const form = reactive<DemandFormState>({
  itemType: '',
  parentDemandId: undefined,
  title: '',
  description: '',
  projectId: '',
  projectIds: [],
  quarterYear: null,
  quarterNumber: null,
  type: 'Planned',
  classification: 'Strategic',
  productIds: [],
  status: 'Backlog',
  observation: '',
  deprioritizationReason: undefined,
  replacementDemandId: undefined,
  jiraIssue: '',
  issueLinks: [],
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

const projectNameById = computed(() =>
  new Map(props.projects.map(project => [project.id, project.name] as const))
)

const selectedProjectNames = computed(() =>
  (form.projectIds ?? [])
    .map(projectId => projectNameById.value.get(projectId) ?? '')
    .filter(Boolean)
)

const isHydratingForm = ref(false)
const includeCrossProjectRoadmaps = ref(false)
const includeCrossProjectEpics = ref(false)

const availableRoadmapOptions = computed(() => {
  if (!isEpic.value)
    return props.roadmapOptions ?? []

  const options = props.roadmapOptions ?? []
  const selectedOption = form.parentDemandId
    ? options.find(option => option.id === form.parentDemandId)
    : undefined

  if (includeCrossProjectRoadmaps.value)
    return options

  const selectedProjectIds = new Set(form.projectIds ?? [])
  if (!selectedProjectIds.size)
    return selectedOption ? [selectedOption] : []

  const filteredOptions = options.filter((option) => {
    const optionProjectIds = option.projectId
      ? [option.projectId]
      : (option.projectIds ?? [])

    return optionProjectIds.some(projectId => selectedProjectIds.has(projectId))
  })

  if (selectedOption && !filteredOptions.some(option => option.id === selectedOption.id))
    return [selectedOption, ...filteredOptions]

  return filteredOptions
})

const parentOptions = computed(() => {
  if (!hasSelectedItemType.value)
    return []

  if (form.itemType === 'Epic') {
    return [...availableRoadmapOptions.value]
      .sort((left, right) => left.title.localeCompare(right.title, 'pt-BR'))
      .map((option): ParentSelectOption => {
        const optionProjectIds = option.projectId
          ? [option.projectId]
          : (option.projectIds ?? [])
        const projectNames = optionProjectIds
          .map(projectId => projectNameById.value.get(projectId) ?? '')
          .filter(Boolean)
        const description = projectNames.join(' · ')

        return {
          value: option.id,
          label: option.title,
          description: description || undefined,
          searchText: `${option.title} ${description}`.toLowerCase()
        }
      })
  }

  if (form.itemType === 'Demand') {
    return availableEpicOptions.value
      .map((option): ParentSelectOption => {
        const optionProjectIds = option.projectId
          ? [option.projectId]
          : (option.projectIds ?? [])
        const projectNames = optionProjectIds
          .map(projectId => projectNameById.value.get(projectId) ?? '')
          .filter(Boolean)
        const description = [
          option.roadmapTitle,
          projectNames.join(' · '),
          option.status ? statusOptions.find(status => status.value === option.status)?.label : ''
        ].filter(Boolean).join(' · ')

        return {
          value: option.id,
          label: option.title,
          description: description || undefined,
          searchText: `${option.title} ${option.roadmapTitle ?? ''} ${projectNames.join(' ')} ${option.status ?? ''}`.toLowerCase()
        }
      })
  }

  return []
})

const filteredParentOptions = computed(() => {
  const query = parentSearch.value.trim().toLowerCase()
  if (!query)
    return parentOptions.value

  return parentOptions.value.filter(option => option.searchText.includes(query))
})

const selectedParentOption = computed(() =>
  parentOptions.value.find(option => option.value === form.parentDemandId) ?? null
)

const parentSelectorLabel = computed(() => {
  if (selectedParentOption.value)
    return selectedParentOption.value.label

  if (isEpic.value && !selectedProjectNames.value.length)
    return 'Selecione os projetos primeiro'

  return isEpic.value ? 'Selecione o roadmap pai' : 'Selecione o épico pai'
})

const kpiLinkEdits = ref<EditableDemandKpiLink[]>([])
const kpiMeasurements = ref<KpiMeasurement[]>([])
const measurementDrafts = ref<Record<string, MeasurementEditorState>>({})
const measurementSavingKpiId = ref<string | null>(null)
const measurementDeletingId = ref<string | null>(null)
const epicOptionsByProjectId = ref<Record<string, EpicParentOption[]>>({})

function mapEpicParentOptions(items: RoadmapDemand[]): EpicParentOption[] {
  return items
    .filter(item => item.itemType === 'Epic')
    .map(item => ({
      id: item.id,
      title: item.title,
      roadmapTitle: item.roadmapTitle,
      projectId: item.projectId,
      projectIds: item.projectIds
    }))
}

async function ensureEpicOptionsLoaded(projectId?: string) {
  if (!projectId || epicOptionsByProjectId.value[projectId])
    return

  try {
    const params = new URLSearchParams({ projectId })
    const response = await api.get<ApiResponse<RoadmapDemand[]>>(`/api/roadmap/demands?${params}`)
    epicOptionsByProjectId.value = {
      ...epicOptionsByProjectId.value,
      [projectId]: mapEpicParentOptions(response.data ?? [])
    }
  }
  catch {
    epicOptionsByProjectId.value = {
      ...epicOptionsByProjectId.value,
      [projectId]: []
    }
  }
}

const availableEpicOptions = computed(() => {
  if (form.itemType !== 'Demand')
    return props.epicOptions ?? []

  const options = props.epicOptions ?? []
  const selectedOption = form.parentDemandId
    ? options.find(option => option.id === form.parentDemandId)
    : undefined

  if (includeCrossProjectEpics.value)
    return options

  if (!form.projectId)
    return selectedOption ? [selectedOption] : []

  const cachedOptions = epicOptionsByProjectId.value[form.projectId]
  if (cachedOptions) {
    const filteredOptions = cachedOptions.filter((option) => {
      const optionProjectIds = option.projectId
        ? [option.projectId]
        : (option.projectIds ?? [])

      return optionProjectIds.includes(form.projectId)
    })

    if (selectedOption && !filteredOptions.some(option => option.id === selectedOption.id))
      return [selectedOption, ...filteredOptions]

    return filteredOptions
  }

  const filteredOptions = options.filter((option) => {
    const optionProjectIds = option.projectId
      ? [option.projectId]
      : (option.projectIds ?? [])

    return optionProjectIds.includes(form.projectId)
  })

  if (selectedOption && !filteredOptions.some(option => option.id === selectedOption.id))
    return [selectedOption, ...filteredOptions]

  return filteredOptions
})

const selectedDeprioritizationExample = computed(() => {
  const reason = form.deprioritizationReason
  return reason ? deprioritizationReasonExamples[reason] : ''
})

const replacementDemandOptions = computed(() => {
  const currentDemandId = props.demand?.id

  return props.dependencyOptions
    .filter(option => option.demandId !== currentDemandId)
    .map(option => ({
      value: option.demandId,
      label: `${option.projectName} · ${option.title}`
    }))
})

  const tradeOffHistory = computed(() => props.demand?.tradeOffHistory ?? [])

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
  get: () => {
    if (form.quarterYear == null || form.quarterNumber == null)
      return ''

    return `${form.quarterNumber}-${form.quarterYear}`
  },
  set: (val: string) => {
    if (!val) {
      form.quarterNumber = null
      form.quarterYear = null
      return
    }

    const [q, y] = val.split('-').map(Number)
    form.quarterNumber = q
    form.quarterYear = y
  }
})

const sortedProjects = computed(() =>
  [...props.projects].sort((left, right) => right.name.localeCompare(left.name, 'pt-BR'))
)

const productsForProject = computed(() =>
  props.projects.find(p => p.id === form.projectId)?.products ?? []
)

function syncSingleProductSelection() {
  if (isEdit.value || !isDemand.value)
    return

  if (productsForProject.value.length === 1) {
    form.productIds = [productsForProject.value[0]!.id]
    return
  }

  const availableProductIds = new Set(productsForProject.value.map(product => product.id))
  form.productIds = form.productIds.filter(productId => availableProductIds.has(productId))
}

function populateFormFromDemand(demand: RoadmapDemand) {
  isHydratingForm.value = true
  includeCrossProjectRoadmaps.value = false
  includeCrossProjectEpics.value = false

  form.itemType = demand.itemType
  form.parentDemandId = demand.parentDemandId
  form.title = demand.title
  form.description = demand.description ?? ''
  form.projectId = demand.projectId ?? ''
  form.projectIds = demand.projectIds ?? (demand.projectId ? [demand.projectId] : [])
  form.quarterYear = demand.quarterYear
  form.quarterNumber = demand.quarterNumber
  form.type = demand.type
  form.classification = demand.classification
  form.productIds = demand.products.map(p => p.productId)
  form.status = demand.status
  form.observation = demand.observation ?? ''
  form.deprioritizationReason = demand.deprioritizationReason ?? undefined
  form.replacementDemandId = demand.replacementDemandId ?? undefined
  form.jiraIssue = demand.jiraIssue ?? ''
  form.issueLinks = demand.issueLinks?.length
    ? demand.issueLinks.map(issue => ({ key: issue.key, url: issue.url ?? '' }))
    : (demand.jiraIssue ? [{ key: demand.jiraIssue, url: '' }] : [])
  form.hours = demand.hours ?? undefined
  form.customers = demand.customers ?? []
  form.dependencyDemandIds = demand.dependsOn.map(item => item.demandId)
  form.isBlocked = demand.isBlocked
  form.blockedReason = demand.blockedReason ?? ''
  form.promisedDate = demand.promisedDate ?? ''
  form.deliveryDate = demand.deliveryDate ?? ''
  form.problemClarity = demand.itemType === 'Epic'
    ? demand.problemClarity ?? undefined
    : undefined
  form.hasNoKpi = demand.hasNoKpi ?? false
  form.noKpiClassification = demand.noKpiClassification ?? undefined
  kpiLinkEdits.value = (demand.kpiLinks ?? []).map(l => toEditableKpiLink({
    kpiId: l.kpiId,
    impactType: l.impactType,
    estimatedImpact: l.estimatedImpact,
    confidenceLevel: l.confidenceLevel
  }))
  kpiMeasurements.value = sortMeasurements(demand.kpiMeasurements ?? [])
  measurementDrafts.value = {}
  customerInput.value = ''

  queueMicrotask(() => {
    isHydratingForm.value = false
  })
}

function resetFormForCreate() {
  includeCrossProjectRoadmaps.value = false
  includeCrossProjectEpics.value = false
  form.itemType = props.defaultItemType ?? ''
  form.parentDemandId = props.defaultParentDemandId
  form.title = ''
  form.description = ''
  form.projectId = props.defaultProjectId ?? sortedProjects.value[0]?.id ?? ''
  form.projectIds = props.defaultProjectIds?.length
    ? [...props.defaultProjectIds]
    : (props.defaultProjectId ? [props.defaultProjectId] : [])
  form.quarterYear = props.defaultQuarterYear ?? null
  form.quarterNumber = props.defaultQuarterNumber ?? null
  form.type = 'Planned'
  form.classification = props.defaultItemType === 'Epic' ? '' : 'Strategic'
  form.productIds = []
  form.status = 'Backlog'
  form.observation = ''
  form.deprioritizationReason = undefined
  form.replacementDemandId = undefined
  form.jiraIssue = ''
  form.issueLinks = []
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

watch(
  () => [props.open, props.demand?.id ?? null] as const,
  ([open]) => {
    if (!open) return

    activeTab.value = 'general'
    showSubmitHint.value = false

    if (props.demand) {
      populateFormFromDemand(props.demand)
      return
    }

    resetFormForCreate()
  },
  { immediate: true }
)

watch(() => form.projectId, () => {
  syncSingleProductSelection()
})

watch(
  () => [props.open, form.itemType, form.projectId] as const,
  async ([open, itemType, projectId]) => {
    if (!open || itemType !== 'Demand' || !projectId)
      return

    await ensureEpicOptionsLoaded(projectId)
  },
  { immediate: true }
)

watch(parentOptions, (options) => {
  if (isRoadmap.value || !form.parentDemandId)
    return

  if (!options.some(option => option.value === form.parentDemandId))
    form.parentDemandId = undefined
})

watch(() => [props.open, form.itemType] as const, () => {
  parentSearch.value = ''
})

watch(() => form.itemType, (itemType) => {
  if (isHydratingForm.value)
    return

  if (!itemType) {
    includeCrossProjectRoadmaps.value = false
    includeCrossProjectEpics.value = false
    form.parentDemandId = undefined
    form.projectId = ''
    form.projectIds = []
    form.productIds = []
    form.title = ''
    form.description = ''
    form.type = 'Planned'
    form.classification = ''
    form.status = 'Backlog'
    form.hours = undefined
    form.customers = []
    form.promisedDate = ''
    form.issueLinks = []
    return
  }

  if (itemType === 'Roadmap') {
    includeCrossProjectRoadmaps.value = false
    includeCrossProjectEpics.value = false
    form.parentDemandId = undefined
    form.projectId = ''
    form.projectIds = props.defaultProjectIds?.length
      ? [...props.defaultProjectIds]
      : (props.defaultProjectId ? [props.defaultProjectId] : [])
    form.productIds = []
    form.type = 'Planned'
    form.classification = 'Strategic'
    form.hours = undefined
    form.customers = []
    form.promisedDate = ''
    form.problemClarity = undefined
    form.issueLinks = []
    return
  }

  if (itemType === 'Epic') {
    includeCrossProjectEpics.value = false
    if (!isEdit.value)
      form.parentDemandId = props.defaultParentDemandId

    form.projectId = ''
    form.projectIds = form.projectIds?.length
      ? form.projectIds
      : (props.defaultProjectIds?.length
          ? [...props.defaultProjectIds]
          : (props.defaultProjectId ? [props.defaultProjectId] : []))
    form.productIds = []
    form.type = 'Planned'
    if (!isEdit.value)
      form.classification = ''

    form.hours = undefined
    if (!isEdit.value)
      form.issueLinks = []

    return
  }

  includeCrossProjectRoadmaps.value = false
  includeCrossProjectEpics.value = false
  form.classification = 'Strategic'
  form.problemClarity = undefined
  form.projectIds = []
  if (!form.projectId)
    form.projectId = props.defaultProjectId ?? sortedProjects.value[0]?.id ?? ''

  syncSingleProductSelection()
})

watch(() => props.defaultParentDemandId, (parentDemandId) => {
  if (!props.open || isEdit.value || form.itemType !== 'Epic')
    return

  form.parentDemandId = parentDemandId
})

function addIssueLink() {
  form.issueLinks = [...(form.issueLinks ?? []), { key: '', url: '' }]
}

function removeIssueLink(index: number) {
  form.issueLinks = (form.issueLinks ?? []).filter((_, currentIndex) => currentIndex !== index)
}

function normalizeIssueLinks(issueLinks?: Array<{ key: string, url: string }>): IssueLinkInput[] {
  return (issueLinks ?? [])
    .map(issue => ({ key: issue.key.trim(), url: issue.url.trim() }))
    .filter(issue => issue.key || issue.url)
}

function isValidIssueUrl(url: string) {
  try {
    const parsed = new URL(url)
    return parsed.protocol === 'http:' || parsed.protocol === 'https:'
  }
  catch {
    return false
  }
}

watch(() => form.isBlocked, (val) => {
  if (!val) form.blockedReason = ''
})

watch(() => form.status, (status) => {
  if (status === 'Done') {
    form.isBlocked = false
    form.blockedReason = ''
  }

  if (status !== 'Deprioritized') {
    form.deprioritizationReason = undefined
    form.observation = ''
    form.replacementDemandId = undefined
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
    if (option.itemType !== 'Demand')
      return false

    if (props.demand && option.demandId === props.demand.id)
      return false

    return `${option.projectName} ${option.title} ${option.quarterLabel} ${option.status}`.toLowerCase().includes(query)
  })
})

const selectedDependencyOptions = computed(() => {
  const selectedIds = new Set(form.dependencyDemandIds ?? [])
  return props.dependencyOptions.filter(option => selectedIds.has(option.demandId))
})

const selectedNonDemandProjects = computed(() =>
  props.projects.filter(project => (form.projectIds ?? []).includes(project.id))
)

const nonDemandProjectsLabel = computed(() => {
  const count = selectedNonDemandProjects.value.length
  if (!count)
    return 'Selecione os projetos'

  if (count === 1)
    return selectedNonDemandProjects.value[0]!.name

  return `${count} projetos`
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
  if (checked) {
    form.productIds = [...new Set([...(form.productIds ?? []), id])]
    return
  }

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

function toggleProjectAssociation(projectId: string, checked: boolean) {
  const nextIds = new Set(form.projectIds ?? [])

  if (checked)
    nextIds.add(projectId)
  else
    nextIds.delete(projectId)

  form.projectIds = Array.from(nextIds)
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
  if (!form.itemType)
    return 'Selecione o tipo do item'
  if (!form.title)
    return `Informe o título ${isRoadmap.value ? 'do roadmap' : isEpic.value ? 'do épico' : 'da demanda'}`
  if (!isDemand.value && !(form.projectIds?.length ?? 0))
    return 'Selecione ao menos um projeto'
  if (!isRoadmap.value && !form.parentDemandId)
    return isEpic.value ? 'Selecione o roadmap pai' : 'Selecione o épico pai'
  if (isDemand.value && !form.projectId)
    return 'Selecione o projeto'
  if (isDemand.value && (form.quarterYear == null || form.quarterNumber == null))
    return 'Selecione o quarter'
  if (isEpic.value && !form.classification)
    return 'Selecione a classificação'
  if (isEpic.value && form.problemClarity == null)
    return 'Informe a nota de clareza'
  if (isDemand.value && form.productIds.length === 0)
    return 'Selecione ao menos um produto'
  if (deprioritizationReasonRequired.value && !form.deprioritizationReason)
    return 'Selecione o motivo da despriorização'
  if (observationRequired.value && !form.observation)
    return 'Preencha a observação da despriorização'
  if (deliveryDateRequired.value && !form.deliveryDate)
    return 'Informe a data de entrega para concluir a demanda'
  if (form.isBlocked && !form.blockedReason)
    return 'Preencha o motivo do impedimento'

  const normalizedIssueLinks = normalizeIssueLinks(form.issueLinks)
  if (normalizedIssueLinks.some(issue => !issue.key || !issue.url))
    return 'Preencha a issue e o link em cada linha informada'
  if (normalizedIssueLinks.some(issue => !isValidIssueUrl(issue.url)))
    return 'Informe links válidos para todas as issues'

  return null
})

const isSubmitDisabled = computed(() => !!missingSubmitReason.value)
const isSubmitBlocked = computed(() => isSubmitDisabled.value || !!props.isSaving)
const submitButtonLabel = computed(() => {
  if (!form.itemType)
    return isEdit.value ? 'Salvar item' : 'Criar item'

  return isEdit.value
    ? `Salvar ${itemTypeLabels[form.itemType as RoadmapItemType]}`
    : `Criar ${itemTypeLabels[form.itemType as RoadmapItemType]}`
})

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
    missingSubmitReason.value === 'Selecione o motivo da despriorização'
    || missingSubmitReason.value === 'Preencha a observação da despriorização'
    || missingSubmitReason.value === 'Informe a data de entrega para concluir a demanda'
    || missingSubmitReason.value === 'Preencha o motivo do impedimento'
  ) {
    activeTab.value = hasStatusTab.value ? 'status' : 'general'
    return
  }

  activeTab.value = 'general'
}

function handleSubmitClick() {
  if (props.isSaving)
    return

  if (isSubmitDisabled.value) {
    focusRelevantTab()
    openSubmitHint()
    return
  }

  showSubmitHint.value = false
  handleSubmit()
}

async function handleSubmit() {
  if (isSubmitBlocked.value || !form.itemType) return

  const sanitizedIssueLinks = sanitizeIssueLinksForItem(form.itemType, normalizeIssueLinks(form.issueLinks))
  const normalizedQuarterYear = form.quarterYear ?? 0
  const normalizedQuarterNumber = form.quarterNumber ?? 0

  emit('submit', {
    ...form,
    itemType: form.itemType,
    projectId: form.projectId || undefined,
    projectIds: form.itemType === 'Demand' ? [] : (form.projectIds ?? []),
    quarterYear: normalizedQuarterYear,
    quarterNumber: normalizedQuarterNumber,
    parentDemandId: form.parentDemandId || undefined,
    jiraIssue: undefined,
    issueLinks: sanitizedIssueLinks,
    hours: Number.isNaN(form.hours as number) ? undefined : form.hours,
    customers: sanitizeCustomersForItem(form.itemType, form.customers),
    promisedDate: sanitizePromisedDateForItem(form.itemType, normalizedQuarterYear, normalizedQuarterNumber, form.promisedDate),
    problemClarity: form.itemType === 'Epic' ? form.problemClarity : undefined,
    classification: form.classification as DemandClassification
  })
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
        <div v-if="hasStatusTab" class="flex gap-2 border-b border-default pb-3">
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

        <template v-if="!hasStatusTab || activeTab === 'general'">
        <section class="space-y-4">
          <div v-if="!isDemand">
            <h3 class="text-sm font-semibold text-highlighted">Dados do item</h3>
          </div>

          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Tipo" required>
              <USelect
                v-model="form.itemType"
                :items="itemTypeOptions"
                placeholder="Selecione"
                class="w-full"
                :disabled="isEdit"
              />
            </UFormField>

            <UFormField v-if="hasSelectedItemType" label="Projetos" required>
              <USelect
                v-if="isDemand"
                v-model="form.projectId"
                :items="sortedProjects.map(p => ({ value: p.id, label: p.name }))"
                placeholder="Selecione"
                class="w-full"
                :disabled="isEdit"
              />

              <UPopover v-else :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                <UButton
                  type="button"
                  variant="outline"
                  color="neutral"
                  trailing-icon="i-lucide-chevron-down"
                  class="w-full justify-between"
                >
                  <span class="truncate">{{ nonDemandProjectsLabel }}</span>
                </UButton>

                <template #content>
                  <div class="min-w-72 space-y-1 p-2">
                    <label
                      v-for="project in sortedProjects"
                      :key="project.id"
                      class="flex cursor-pointer items-center gap-2 rounded-lg px-2.5 py-2 text-sm text-highlighted transition-colors hover:bg-elevated"
                    >
                      <input
                        type="checkbox"
                        class="h-4 w-4 accent-primary"
                        :checked="form.projectIds?.includes(project.id)"
                        @change="(event) => toggleProjectAssociation(project.id, (event.target as HTMLInputElement).checked)"
                      >
                      <span class="truncate">{{ project.name }}</span>
                    </label>
                  </div>
                </template>
              </UPopover>
            </UFormField>

          </div>

          <UFormField v-if="hasSelectedItemType && isEpic" label="Roadmap pai" required>
            <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
              <UButton
                type="button"
                variant="outline"
                color="neutral"
                trailing-icon="i-lucide-chevron-down"
                class="w-full justify-between"
              >
                <span class="truncate">{{ parentSelectorLabel }}</span>
              </UButton>

              <template #content>
                <div class="min-w-80 space-y-2 p-2">
                  <div class="flex items-center justify-between gap-3 rounded-lg border border-default bg-elevated/40 px-2.5 py-2">
                    <div class="min-w-0">
                      <p class="text-xs font-medium text-highlighted">Buscar roadmaps de outros projetos</p>
                      <p class="text-[11px] text-muted">Desmarcado: mostra apenas os roadmaps dos projetos selecionados.</p>
                    </div>
                    <USwitch v-model="includeCrossProjectRoadmaps" />
                  </div>

                  <UInput
                    v-model="parentSearch"
                    icon="i-lucide-search"
                    placeholder="Buscar roadmap pai"
                    class="w-full"
                  />

                  <div class="max-h-64 space-y-1 overflow-y-auto rounded-lg border border-default bg-default p-1">
                    <button
                      v-for="option in filteredParentOptions"
                      :key="option.value"
                      type="button"
                      class="flex w-full items-start justify-between gap-3 rounded-lg px-2.5 py-2 text-left transition-colors hover:bg-elevated"
                      :class="form.parentDemandId === option.value ? 'bg-primary/5 text-primary' : 'text-highlighted'"
                      @click="form.parentDemandId = option.value"
                    >
                      <div class="min-w-0">
                        <p class="truncate text-sm font-medium">{{ option.label }}</p>
                        <p v-if="option.description" class="truncate text-xs text-muted">{{ option.description }}</p>
                      </div>
                      <UIcon v-if="form.parentDemandId === option.value" name="i-lucide-check" class="mt-0.5 h-4 w-4 shrink-0" />
                    </button>

                    <p v-if="!filteredParentOptions.length" class="px-2.5 py-3 text-xs italic text-muted">
                      {{ !selectedProjectNames.length
                        ? 'Selecione ao menos um projeto para buscar roadmaps.'
                        : includeCrossProjectRoadmaps
                          ? 'Nenhum roadmap encontrado na busca atual.'
                          : 'Nenhum roadmap encontrado para os projetos selecionados. Ative a busca em outros projetos para ampliar a lista.' }}
                    </p>
                  </div>
                </div>
              </template>
            </UPopover>
          </UFormField>

          <div v-if="!hasSelectedItemType" class="rounded-xl border border-dashed border-default bg-elevated/40 px-4 py-6 text-sm text-muted">
            Selecione o tipo do item para carregar os campos de cadastro.
          </div>

          <div v-if="hasSelectedItemType" class="space-y-3">
          <UFormField v-if="isDemand" label="Épico pai" required>
            <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
              <UButton
                type="button"
                variant="outline"
                color="neutral"
                trailing-icon="i-lucide-chevron-down"
                class="w-full justify-between"
              >
                <span class="truncate">{{ parentSelectorLabel }}</span>
              </UButton>

              <template #content>
                <div class="min-w-80 space-y-2 p-2">
                  <div class="flex items-center justify-between gap-3 rounded-lg border border-default bg-elevated/40 px-2.5 py-2">
                    <div class="min-w-0">
                      <p class="text-xs font-medium text-highlighted">Buscar épicos de outros projetos</p>
                      <p class="text-[11px] text-muted">Desmarcado: mostra apenas os épicos do projeto selecionado.</p>
                    </div>
                    <USwitch v-model="includeCrossProjectEpics" />
                  </div>

                  <UInput
                    v-model="parentSearch"
                    icon="i-lucide-search"
                    placeholder="Buscar épico pai"
                    class="w-full"
                  />

                  <div class="max-h-64 space-y-1 overflow-y-auto rounded-lg border border-default bg-default p-1">
                    <button
                      v-for="option in filteredParentOptions"
                      :key="option.value"
                      type="button"
                      class="flex w-full items-start justify-between gap-3 rounded-lg px-2.5 py-2 text-left transition-colors hover:bg-elevated"
                      :class="form.parentDemandId === option.value ? 'bg-primary/5 text-primary' : 'text-highlighted'"
                      @click="form.parentDemandId = option.value"
                    >
                      <div class="min-w-0">
                        <p class="truncate text-sm font-medium">{{ option.label }}</p>
                        <p v-if="option.description" class="truncate text-xs text-muted">{{ option.description }}</p>
                      </div>
                      <UIcon v-if="form.parentDemandId === option.value" name="i-lucide-check" class="mt-0.5 h-4 w-4 shrink-0" />
                    </button>

                    <p v-if="!filteredParentOptions.length" class="px-2.5 py-3 text-xs italic text-muted">
                      {{ !form.projectId
                        ? 'Selecione um projeto para buscar épicos.'
                        : includeCrossProjectEpics
                          ? 'Nenhum épico encontrado na busca atual.'
                          : 'Nenhum épico encontrado para o projeto selecionado. Ative a busca em outros projetos para ampliar a lista.' }}
                    </p>
                  </div>
                </div>
              </template>
            </UPopover>
          </UFormField>

          <UFormField v-if="isDemand" label="Produto" required>
            <div
              v-if="productsForProject.length"
              class="flex flex-wrap gap-2 rounded-lg border border-default bg-elevated p-3"
            >
              <label
                v-for="product in productsForProject"
                :key="product.id"
                class="flex cursor-pointer items-center gap-2 rounded-lg px-2.5 py-1.5 transition-colors select-none hover:bg-default"
                :class="form.productIds.includes(product.id)
                  ? 'bg-primary/10 border border-primary/30'
                  : 'border border-transparent'"
              >
                <input
                  type="checkbox"
                  :value="product.id"
                  :checked="form.productIds.includes(product.id)"
                  class="h-3.5 w-3.5 accent-primary"
                  @change="(e) => toggleProduct(product.id, (e.target as HTMLInputElement).checked)"
                >
                <span class="text-sm">{{ product.name }}</span>
              </label>
            </div>
            <p
              v-else
              class="text-xs italic text-muted"
            >
              Selecione um projeto primeiro.
            </p>
          </UFormField>

          <UFormField label="Título" required>
            <UInput
              v-model="form.title"
              :placeholder="isRoadmap ? 'Nome do roadmap' : isEpic ? 'Nome do épico' : 'Descreva a demanda brevemente'"
              class="w-full"
            />
          </UFormField>

          <UFormField label="Descrição">
            <UTextarea
              v-model="form.description"
              placeholder="Detalhes adicionais (opcional)"
              :rows="4"
              class="w-full"
            />
          </UFormField>

          <div v-if="isDemand" class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
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

            <UFormField label="Data prometida">
              <UInput
                v-model="form.promisedDate"
                type="date"
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
          </div>

          <div v-if="isEpic" class="grid gap-3 md:grid-cols-3">
            <UFormField label="Classificação" required>
              <USelect
                v-model="form.classification"
                :items="classificationOptions"
                placeholder="Selecione"
                class="w-full"
              />
            </UFormField>

            <UFormField label="Data prometida">
              <UInput
                v-model="form.promisedDate"
                type="date"
                class="w-full"
              />
            </UFormField>

            <div class="relative pt-6">
              <div class="absolute inset-x-0 top-0 flex items-center justify-between gap-2 text-sm leading-none">
                <span class="font-medium text-highlighted">
                  Nota de clareza <span class="text-error">*</span>
                </span>
                <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                  <UButton
                    type="button"
                    icon="i-lucide-circle-help"
                    variant="ghost"
                    color="neutral"
                    size="xs"
                    class="h-4 min-h-0 w-4 min-w-0 p-0"
                    aria-label="Explicar clareza do problema"
                  />
                  <template #content>
                    <div class="max-w-sm p-4 text-sm text-highlighted">
                      Clareza do Problema: Sabemos qual problema estamos resolvendo - ou so estamos construindo alguma coisa?
                    </div>
                  </template>
                </UPopover>
              </div>
              <div>
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
            </div>
          </div>

          <div v-if="isRoadmap" class="grid gap-3 md:grid-cols-2">
            <UFormField label="Status">
              <USelect
                v-model="form.status as DemandStatus"
                :items="statusOptions"
                class="w-full"
              />
            </UFormField>
          </div>

          <UFormField v-if="!isRoadmap" label="Issues (Jira)"><div class="space-y-2">
            <div
              v-for="(issue, index) in form.issueLinks"
              :key="index"
              class="grid gap-2 md:grid-cols-[minmax(0,180px)_minmax(0,1fr)_auto]"
            >
              <UInput
                v-model="issue.key"
                :placeholder="isEpic ? 'Ex: EPIC-123' : 'Ex: DEM-123'"
                class="w-full"
              />
              <UInput
                v-model="issue.url"
                placeholder="https://..."
                class="w-full"
              />
              <UButton
                type="button"
                color="neutral"
                variant="ghost"
                icon="i-lucide-trash-2"
                aria-label="Remover issue"
                @click="removeIssueLink(index)"
              />
            </div>

             <div class="flex flex-wrap items-center gap-3">
              <UButton
                type="button"
                icon="i-lucide-plus"
                label="Adicionar issue"
                variant="soft"
                size="sm"
                @click="addIssueLink"
              />
            </div>
          </div></UFormField>

          <UFormField v-if="isEpic" label="Clientes envolvidos">
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

          </div>
        </section>

        <section v-if="hasSelectedItemType && !isRoadmap" class="space-y-4 border-t border-default pt-4">
          <div>
            <h3 class="text-sm font-semibold text-highlighted">Dependências entre épicos e demandas</h3>
            <p class="mt-1 text-xs text-muted">
              Relacione demandas que precisam ser concluídas antes deste item seguir adiante.
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
                Nenhum item encontrado para vincular.
              </p>
            </div>
          </div>
        </section>
        </template>

        <template v-else-if="activeTab === 'status'">
        <section v-if="!isRoadmap" class="space-y-4">
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

            <UFormField v-if="!isRoadmap" label="Impedimento">
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

            <UFormField v-if="!isRoadmap && deliveryDateRequired" label="Data de entrega" required>
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
            v-if="deprioritizationReasonRequired"
            label="Motivo da despriorização"
            required
          >
            <USelect
              v-model="form.deprioritizationReason"
              :items="deprioritizationReasonOptions"
              placeholder="Selecione o motivo"
              class="w-full"
              :class="!form.deprioritizationReason ? 'ring-2 ring-red-400' : ''"
            />
          </UFormField>

          <div
            v-if="selectedDeprioritizationExample"
            class="text-sm text-muted"
          >
            <span class="font-medium text-highlighted">Exemplo:</span>
            {{ selectedDeprioritizationExample }}
          </div>

          <UFormField
            v-if="observationRequired"
            label="Demanda priorizada no lugar"
            hint="Opcional"
          >
            <USelect
              v-model="form.replacementDemandId"
              :items="replacementDemandOptions"
              placeholder="Selecione uma demanda"
              class="w-full"
            />
          </UFormField>

          <UFormField
            v-if="observationRequired"
            label="Observação despriorização"
            required
          >
            <UTextarea
              v-model="form.observation"
              placeholder="Detalhe o contexto da despriorização"
              :rows="2"
              class="w-full"
              :class="!form.observation ? 'ring-2 ring-red-400' : ''"
            />
          </UFormField>

          <section v-if="tradeOffHistory.length" class="space-y-3 border-t border-default pt-4">
            <div>
              <h4 class="text-sm font-semibold text-highlighted">Histórico de trade-offs de despriorização</h4>
              <p class="mt-1 text-xs text-muted">
                Estes registros permanecem vinculados ao projeto e ao quarter em que a despriorização aconteceu.
              </p>
            </div>

            <article
              v-for="tradeOff in tradeOffHistory"
              :key="tradeOff.id"
              class="rounded-lg border border-default bg-elevated p-3 space-y-2"
            >
              <div class="flex flex-wrap items-center gap-2">
                <UBadge variant="subtle" color="neutral">{{ tradeOff.projectName }}</UBadge>
                <UBadge variant="subtle" color="primary">{{ tradeOff.quarterLabel }}</UBadge>
                <UBadge variant="subtle" color="warning">{{ deprioritizationReasonLabels[tradeOff.reason] }}</UBadge>
              </div>

              <p v-if="tradeOff.replacementDemandTitle" class="text-sm text-highlighted">
                Priorizada no lugar: {{ tradeOff.replacementDemandTitle }}
              </p>

              <p v-if="tradeOff.observation" class="text-sm text-muted">
                {{ tradeOff.observation }}
              </p>

              <p class="text-xs text-muted">
                Registrado em {{ new Intl.DateTimeFormat('pt-BR').format(new Date(tradeOff.createdAt)) }}
              </p>
            </article>
          </section>
        </section>
        </template>

      </form>
    </template>

    <template #footer>
      <div class="flex flex-col items-end gap-2">
        <div
          v-if="showSubmitHint && missingSubmitReason"
          class="w-full rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/20 dark:text-red-300"
        >
          {{ missingSubmitReason }}
        </div>

        <div class="flex justify-end gap-2">
        <UButton
          variant="outline"
          color="neutral"
          label="Cancelar"
          @click="emit('update:open', false)"
        />
        <div class="relative flex items-center">
          <UButton
            :loading="props.isSaving"
            :label="submitButtonLabel"
            icon="i-lucide-check"
            :color="isSubmitBlocked ? 'neutral' : 'primary'"
            :class="isSubmitBlocked ? 'opacity-60 cursor-not-allowed' : ''"
            :disabled="!!props.isSaving"
            @click="handleSubmitClick"
          />
        </div>
        </div>
      </div>
    </template>
  </UModal>
</template>
