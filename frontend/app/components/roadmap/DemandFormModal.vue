<script setup lang="ts">
import type { ApiResponse } from '~/types/api'
import type {
  RoadmapDemand,
  RoadmapProject,
  RoadmapItemType,
  CustomerRename,
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
import {
  BACKLOG_QUARTER,
  PRIORITIZED_BACKLOG_QUARTER,
  PRE_REGISTERED_QUARTER_END_YEAR,
  buildPreRegisteredQuarterYears,
  buildQuarterValue,
  formatQuarterLabel,
  parseQuarterValue
} from '~/utils/roadmapQuarter'

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
  defaultType?: DemandFormData['type']
  defaultHours?: number
  defaultProductIds?: string[]
  forceSimpleEpic?: boolean
  availableKpis?: Kpi[]
  isSaving?: boolean
  focusField?: string
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  submit: [data: DemandFormData]
  'create-spillover': [demandId: string, targetYear: number, targetNumber: number, reason: string, observation: string]
  'trade-off-deleted': [tradeOffId: string]
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

// Epic mode decision: null = not yet decided (new epic), true = simple, false = composite
const epicModeDecided = ref(false)
const isSimpleEpic = computed(() => isEpic.value && !!form.isSimple)
const isCompositeEpic = computed(() => isEpic.value && !form.isSimple)
const showEpicModeSelection = computed(() =>
  isEpic.value && !isEdit.value && !epicModeDecided.value
)

function selectEpicMode(simple: boolean) {
  form.isSimple = simple
  epicModeDecided.value = true
  // A simple epic can only be linked to a single project.
  if (simple && (form.projectIds?.length ?? 0) > 1)
    form.projectIds = form.projectIds!.slice(0, 1)
}

// Step-by-step reveal when creating a new epic:
//   tipo → (pergunta simples/composto) → Projetos → Roadmap pai → restante.
// Quando o épico vem do "+" na linha do roadmap (defaultParentDemandId definido), os campos
// já vêm preenchidos, então o restante é exibido logo após escolher o modo.
const isNewEpic = computed(() => isEpic.value && !isEdit.value)
const epicPrefilledFromRoadmap = computed(() => isNewEpic.value && !!props.defaultParentDemandId)

const showProjectsField = computed(() => {
  if (!hasSelectedItemType.value) return false
  if (!isEpic.value) return true
  if (isEdit.value) return true
  return epicModeDecided.value
})

const showRoadmapParentField = computed(() => {
  if (!isEpic.value || !hasSelectedItemType.value) return false
  if (isEdit.value) return true
  if (!epicModeDecided.value) return false
  if (epicPrefilledFromRoadmap.value) return true
  return (form.projectIds?.length ?? 0) > 0
})

const showRestFields = computed(() => {
  if (!hasSelectedItemType.value) return false
  if (!isEpic.value) return true
  if (isEdit.value) return true
  if (!epicModeDecided.value) return false
  // Simple epics gain a progressive "Produto" step between the roadmap parent and the rest —
  // this gate applies even when prefilled from a roadmap line (which doesn't fill the product).
  if (isSimpleEpic.value)
    return (form.projectIds?.length ?? 0) > 0 && !!form.parentDemandId && form.productIds.length > 0
  if (epicPrefilledFromRoadmap.value) return true
  return (form.projectIds?.length ?? 0) > 0 && !!form.parentDemandId
})

// Progressive disclosure for demand creation: Tipo+Time -> Épico pai -> Produto -> restante.
// Each step requires ALL previous steps (not just its own field) so that auto-filling a single
// product doesn't reveal the rest before the épico pai is chosen.
const showDemandEpicPaiField = computed(() =>
  isDemand.value && (isEdit.value || !!form.projectId)
)
const showDemandProductField = computed(() =>
  isDemand.value && (isEdit.value || (!!form.projectId && !!form.parentDemandId))
)
const showDemandRestAfterProduct = computed(() =>
  isDemand.value && (isEdit.value || (!!form.projectId && !!form.parentDemandId && form.productIds.length > 0))
)

// Progressive disclosure for simple epics: Time -> Roadmap pai -> Produto -> restante.
const showSimpleEpicProductField = computed(() =>
  isSimpleEpic.value
  && (isEdit.value || ((form.projectIds?.length ?? 0) > 0 && !!form.parentDemandId))
)
const showSimpleEpicRestAfterProduct = computed(() =>
  // Require the full chain before revealing the rest, even when prefilled from a roadmap line.
  isSimpleEpic.value
  && (isEdit.value || ((form.projectIds?.length ?? 0) > 0 && !!form.parentDemandId && form.productIds.length > 0))
)

// Whether the "rest" fields (and dependencies section) should be visible, accounting for the
// per-item progressive disclosure. Composite epics / roadmaps rely on showRestFields alone.
const showRestAfterProgressive = computed(() => {
  if (isDemand.value) return showDemandRestAfterProduct.value
  if (isSimpleEpic.value) return showSimpleEpicRestAfterProduct.value
  return true
})

function setSingleEpicProject(projectId?: string) {
  form.projectIds = projectId ? [projectId] : []
  // Simple epics derive products from the single project — drop products that no longer apply.
  const availableProductIds = new Set(productsForSimpleEpic.value.map(product => product.id))
  form.productIds = form.productIds.filter(id => availableProductIds.has(id))
}

const productsForSimpleEpic = computed(() => {
  if (!isSimpleEpic.value) return []
  const selectedProjectIds = new Set(form.projectIds ?? [])
  return props.projects
    .filter(p => selectedProjectIds.has(p.id))
    .flatMap(p => p.products)
})

const title = computed(() => {
  if (!hasSelectedItemType.value)
    return isEdit.value ? 'Editar item' : 'Novo Item'

  const itemLabel = itemTypeLabels[form.itemType as RoadmapItemType]
  return isEdit.value ? `Editar ${itemLabel}` : `Novo ${itemLabel}`
})

const currentYear = new Date().getFullYear()
const quarters = [
  { value: BACKLOG_QUARTER.value, label: BACKLOG_QUARTER.label },
  { value: PRIORITIZED_BACKLOG_QUARTER.value, label: PRIORITIZED_BACKLOG_QUARTER.label },
  ...buildPreRegisteredQuarterYears(currentYear, PRE_REGISTERED_QUARTER_END_YEAR).flatMap(y =>
    [1, 2, 3, 4].map(q => ({
      value: buildQuarterValue(y, q),
      label: formatQuarterLabel(y, q)
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
  { value: 'Deprioritized', label: 'Despriorizado' },
  { value: 'Blocked',       label: 'Impedido' },
  { value: 'UX',            label: 'UX' },
  { value: 'Prioritized',   label: 'Priorizado' },
  { value: 'Spillover',     label: 'Transbordo' }
]

const statusOptionsForForm = computed(() => {
  const canSpillover = (isDemand.value || isSimpleEpic.value) && !props.demand?.successorDemandId
  const hadSpillover = (isDemand.value || isSimpleEpic.value) && !!props.demand?.successorDemandId
  if (canSpillover || hadSpillover || props.demand?.status === 'Spillover') return statusOptions
  return statusOptions.filter(o => o.value !== 'Spillover')
})

const statusOptionsForRoadmap = statusOptions.filter(o => !['Spillover', 'UX', 'Prioritized'].includes(o.value))

const deprioritizationReasonOptions = [
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
] as const

const deprioritizationReasonLabels: Record<string, string> = {
  Strategic: 'Estratégico',
  MandatoryUrgent: 'Mandatório/Urgente',
  LowImpact: 'Baixo impacto',
  LackOfCapacity: 'Falta de capacidade',
  ContextChange: 'Mudança de contexto',
  Customizacao: 'Customização',
  StrategyChange: 'Mudança de estratégia',
  HigherValuePrioritization: 'Priorização de maior valor',
  LowCustomerDemand: 'Baixa demanda de clientes',
  LowExpectedReturn: 'Baixo retorno esperado',
  BusinessDefinitionDependency: 'Dependência de definição de negócio',
  AlternativeSolutionAvailable: 'Solução alternativa disponível',
  RegulatoryRequirementChanged: 'Requisito regulatório alterado',
  CustomerWithdrew: 'Cliente desistiu',
  ReplacedByOtherInitiative: 'Substituída por outra iniciativa',
  UndefinedScope: 'Escopo indefinido'
}

const spilloverReasonOptions = [
  { value: 'ScopeChange', label: 'Mudança de escopo' },
  { value: 'PriorityChangeNoTradeOff', label: 'Mudança de prioridade (sem trade-off)' },
  { value: 'ExternalDependency', label: 'Dependência externa' },
  { value: 'TechnicalBlock', label: 'Impedimento técnico' },
  { value: 'IncorrectEstimate', label: 'Estimativa incorreta' },
  { value: 'InsufficientCapacity', label: 'Capacidade insuficiente' },
  { value: 'QualityIssues', label: 'Problemas de qualidade' }
] as const

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
const customerInputRef = useTemplateRef<HTMLInputElement>('customerInput')
const issueLinksContainerRef = useTemplateRef<HTMLElement>('issueLinksContainer')
const dependencyResultsRef = useTemplateRef<HTMLElement>('dependencyResults')
const customerRenameSource = ref<string | null>(null)
const pendingCustomerRenames = ref<CustomerRename[]>([])
let submitHintTimeout: ReturnType<typeof setTimeout> | null = null

const hasStatusTab = computed(() => resultTabs.value.length > 1)

const observationRequired = computed(() => form.status === 'Deprioritized')
const deprioritizationReasonRequired = computed(() => form.status === 'Deprioritized')
const deliveryDateRequired = computed(() => form.status === 'Done')
const blockedReasonRequired = computed(() => form.status === 'Blocked')
const isTransitioningToSpillover = computed(() => form.status === 'Spillover' && props.demand?.status !== 'Spillover' && !props.demand?.successorDemandId)
const isAlreadySpillover = computed(() => form.status === 'Spillover' && (props.demand?.status === 'Spillover' || !!props.demand?.successorDemandId))
const spilloverFieldsRequired = computed(() => form.status === 'Spillover')

const spilloverTargetYear = ref<number | null>(null)
const spilloverTargetNumber = ref<number | null>(null)

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
  isSimple: false,
  status: 'Backlog',
  observation: '',
  deprioritizationReason: undefined,
  replacementDemandId: undefined,
  jiraIssue: '',
  issueLinks: [],
  hours: undefined,
  customers: [],
  dependencyDemandIds: [],
  blockedReason: '',
  promisedDate: '',
  deliveryDate: '',
  problemClarity: undefined,
  hasNoKpi: false,
  noKpiClassification: undefined,
  spilloverReason: undefined,
  spilloverObservation: ''
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
    return 'Selecione os times primeiro'

  return isEpic.value ? 'Selecione o roadmap pai' : 'Selecione o épico pai'
})

const kpiLinkEdits = ref<EditableDemandKpiLink[]>([])
const kpiMeasurements = ref<KpiMeasurement[]>([])
const measurementDrafts = ref<Record<string, MeasurementEditorState>>({})
const measurementSavingKpiId = ref<string | null>(null)
const measurementDeletingId = ref<string | null>(null)
const tradeOffDeletingId = ref<string | null>(null)
const removedTradeOffIds = ref<string[]>([])
const removedDependedOnByIds = ref<string[]>([])

// "Este item bloqueia" links the user has marked for removal are hidden immediately.
const visibleDependedOnBy = computed(() =>
  (props.demand?.dependedOnBy ?? []).filter(dep => !removedDependedOnByIds.value.includes(dep.demandId))
)

function removeDependedOnBy(demandId: string) {
  if (!removedDependedOnByIds.value.includes(demandId))
    removedDependedOnByIds.value = [...removedDependedOnByIds.value, demandId]
}
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
  return ''
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

const tradeOffHistory = computed(() =>
  (props.demand?.tradeOffHistory ?? []).filter(tradeOff => !removedTradeOffIds.value.includes(tradeOff.id))
)

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

    return buildQuarterValue(form.quarterYear, form.quarterNumber)
  },
  set: (val: string) => {
    if (!val) {
      form.quarterNumber = null
      form.quarterYear = null
      return
    }

    const { quarterYear, quarterNumber } = parseQuarterValue(val)
    form.quarterNumber = quarterNumber
    form.quarterYear = quarterYear
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

// Fired only on a USER change of the demand's "Time" (not on form population). The team owns the
// products, so drop products that don't exist in the new team and auto-select when it has a single
// product — the user re-picks otherwise. Works on edit too (changing an existing demand's team).
function onDemandProjectChange() {
  const availableProductIds = new Set(productsForProject.value.map(product => product.id))
  form.productIds = form.productIds.filter(productId => availableProductIds.has(productId))

  if (form.productIds.length === 0 && productsForProject.value.length === 1)
    form.productIds = [productsForProject.value[0]!.id]
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
  form.spilloverReason = demand.spilloverReason ?? undefined
  form.spilloverObservation = demand.spilloverObservation ?? ''
  form.jiraIssue = demand.jiraIssue ?? ''
  form.issueLinks = demand.issueLinks?.length
    ? demand.issueLinks.map(issue => ({ key: issue.key, url: issue.url ?? '' }))
    : (demand.jiraIssue ? [{ key: demand.jiraIssue, url: '' }] : [])
  form.hours = demand.hours ?? undefined
  form.isSimple = demand.isSimple ?? false
  epicModeDecided.value = true
  form.customers = demand.customers ?? []
  customerRenameSource.value = null
  pendingCustomerRenames.value = []
  form.dependencyDemandIds = demand.dependsOn.map(item => item.demandId)
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
  // Epics created from a roadmap line only pre-select the project when the roadmap has exactly
  // one project; with more than one, the field is left blank for the user to choose.
  form.projectIds = (props.defaultItemType === 'Epic' && (props.defaultProjectIds?.length ?? 0) > 1)
    ? []
    : (props.defaultProjectIds?.length
        ? [...props.defaultProjectIds]
        : (props.defaultProjectId ? [props.defaultProjectId] : []))
  form.quarterYear = props.defaultQuarterYear ?? null
  form.quarterNumber = props.defaultQuarterNumber ?? null
  form.type = props.defaultType ?? 'Planned'
  form.classification = props.defaultItemType === 'Epic' ? '' : 'Strategic'
  form.productIds = props.defaultProductIds ? [...props.defaultProductIds] : []
  form.status = 'Backlog'
  form.observation = ''
  form.deprioritizationReason = undefined
  form.replacementDemandId = undefined
  form.jiraIssue = ''
  form.issueLinks = []
  form.hours = props.defaultHours ?? undefined
  form.isSimple = false
  epicModeDecided.value = false
  form.customers = []
  customerRenameSource.value = null
  pendingCustomerRenames.value = []
  form.dependencyDemandIds = []
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

    removedTradeOffIds.value = []
    removedDependedOnByIds.value = []
    quickDepOpen.value = false
    dependencySearch.value = ''

    activeTab.value = 'general'
    showSubmitHint.value = false

    if (props.demand) {
      populateFormFromDemand(props.demand)

      // Convert an empty composite epic into a simple one: open in simple mode so the
      // user fills the now-required quarter/type/hours/product fields.
      if (props.forceSimpleEpic && props.demand.itemType === 'Epic') {
        form.isSimple = true
        epicModeDecided.value = true
      }
    }
    else {
      resetFormForCreate()
    }

    if (props.focusField === 'jiraIssue') {
      setTimeout(() => {
        if (!form.issueLinks.length)
          addIssueLink()
        setTimeout(() => {
          const input = issueLinksContainerRef.value?.querySelector<HTMLInputElement>('input[placeholder="https://..."]')
          input?.focus()
        }, 50)
      }, 50)
    }
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
      : ((props.defaultProjectIds?.length ?? 0) > 1
          ? []
          : (props.defaultProjectIds?.length
              ? [...props.defaultProjectIds]
              : (props.defaultProjectId ? [props.defaultProjectId] : [])))
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

function extractIssueKeyFromUrl(url: string) {
  const trimmed = url.trim()
  if (!trimmed)
    return ''

  const match = trimmed.match(/\/browse\/([A-Z][A-Z0-9]+-\d+)(?:[/?#].*)?$/i)
  return match?.[1]?.toUpperCase() ?? ''
}

function updateIssueUrl(index: number, value: string | number | null | undefined) {
  const nextUrl = String(value ?? '')
  const nextIssueLinks = [...(form.issueLinks ?? [])]
  const issue = nextIssueLinks[index]
  if (!issue)
    return

  issue.url = nextUrl

  const extractedKey = extractIssueKeyFromUrl(nextUrl)
  if (extractedKey)
    issue.key = extractedKey

  form.issueLinks = nextIssueLinks
}

function normalizeIssueLinks(issueLinks?: Array<{ key: string, url: string }>): IssueLinkInput[] {
  return (issueLinks ?? [])
    .map((issue) => {
      const url = issue.url.trim()
      const extractedKey = extractIssueKeyFromUrl(url)
      return {
        key: (extractedKey || issue.key).trim().toUpperCase(),
        url
      }
    })
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

watch(() => form.status, (status) => {
  if (status !== 'Blocked')
    form.blockedReason = ''

  if (status !== 'Deprioritized') {
    form.deprioritizationReason = undefined
    form.observation = ''
    form.replacementDemandId = undefined
  }

  if (status === 'Spillover' && !props.demand?.successorDemandId) {
    const qy = form.quarterYear ?? props.demand?.quarterYear ?? new Date().getFullYear()
    const qn = form.quarterNumber ?? props.demand?.quarterNumber ?? 1
    spilloverTargetYear.value = qn === 4 ? qy + 1 : qy
    spilloverTargetNumber.value = qn === 4 ? 1 : qn + 1
  }

  if (status !== 'Spillover') {
    form.spilloverReason = undefined
    form.spilloverObservation = ''
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

// Bring the dependency results (and the "create & link" option) into view when a search starts or
// the quick-create opens. We locate the modal's actual scroll container and scroll it, because
// scrollIntoView can fail to find the right ancestor inside the modal.
function scrollDependencyAreaIntoView() {
  nextTick(() => {
    requestAnimationFrame(() => {
      const anchor = dependencyResultsRef.value
      if (!anchor)
        return

      let parent = anchor.parentElement
      while (parent) {
        const style = getComputedStyle(parent)
        if (/(auto|scroll)/.test(style.overflowY) && parent.scrollHeight > parent.clientHeight) {
          parent.scrollTo({ top: parent.scrollHeight, behavior: 'smooth' })
          return
        }
        parent = parent.parentElement
      }

      anchor.scrollIntoView({ block: 'end', behavior: 'smooth' })
    })
  })
}

watch(hasDependencyQuery, (hasQuery) => {
  if (hasQuery)
    scrollDependencyAreaIntoView()
})

const filteredDependencyOptions = computed(() => {
  const query = dependencySearch.value.trim().toLowerCase()
  if (!query)
    return []

  return props.dependencyOptions.filter(option => {
    // Both demands and epics can be dependencies (roadmaps are already excluded by the backend).
    if (option.itemType === 'Roadmap')
      return false

    if (props.demand && option.demandId === props.demand.id)
      return false

    const typeLabel = option.itemType === 'Epic' ? 'épico' : 'demanda'
    return `${typeLabel} ${option.projectName} ${option.title} ${option.quarterLabel} ${option.status}`.toLowerCase().includes(query)
  })
})

const selectedDependencyOptions = computed(() => {
  const selectedIds = new Set(form.dependencyDemandIds ?? [])
  return props.dependencyOptions.filter(option => selectedIds.has(option.demandId))
})

// ── Quick-create a dependency demand inline (when it doesn't exist yet) ──
const quickDepOpen = ref(false)
const quickDepSaving = ref(false)
const quickDepTitle = ref('')
const quickDepProjectId = ref('')
const quickDepParentEpicId = ref('')
const quickDepProductIds = ref<string[]>([])
const quickDepQuarter = ref('')

const quickDepProducts = computed(() => props.projects.find(project => project.id === quickDepProjectId.value)?.products ?? [])
const quickDepEpicOptions = computed(() => epicOptionsByProjectId.value[quickDepProjectId.value] ?? [])

// Epic options for the select, always including the preselected epic with its NAME (so it never
// shows a raw id while the project's epics load, or when it belongs to another team).
const quickDepEpicSelectItems = computed(() => {
  const items = quickDepEpicOptions.value.map(epic => ({ value: epic.id, label: epic.title }))
  const selectedId = quickDepParentEpicId.value
  if (selectedId && !items.some(item => item.value === selectedId)) {
    const title = (props.demand?.epicId === selectedId ? props.demand?.epicTitle : undefined)
      ?? (props.epicOptions ?? []).find(option => option.id === selectedId)?.title
      ?? 'Épico selecionado'
    items.unshift({ value: selectedId, label: title })
  }
  return items
})

const quickDepProductsLabel = computed(() => {
  const selected = quickDepProducts.value.filter(product => quickDepProductIds.value.includes(product.id))
  if (!selected.length)
    return quickDepProducts.value.length ? 'Selecione os produtos' : 'Selecione um time primeiro'
  if (selected.length === 1)
    return selected[0]!.name
  return `${selected.length} produtos`
})

function openQuickDependencyCreate() {
  quickDepTitle.value = dependencySearch.value.trim()
  // Épico pai defaults to the current demand's parent epic (shown by name, editable). The TIME is
  // NOT prefilled — a dependency usually belongs to another team — and the product list follows
  // the chosen time.
  quickDepProjectId.value = ''
  quickDepParentEpicId.value = isDemand.value ? (form.parentDemandId || '') : ''
  quickDepProductIds.value = []
  quickDepQuarter.value = (form.quarterYear != null && form.quarterNumber != null)
    ? buildQuarterValue(form.quarterYear, form.quarterNumber)
    : ''
  quickDepOpen.value = true
  scrollDependencyAreaIntoView()
}

function toggleQuickDepProduct(productId: string, checked: boolean) {
  quickDepProductIds.value = checked
    ? [...new Set([...quickDepProductIds.value, productId])]
    : quickDepProductIds.value.filter(id => id !== productId)
}

watch(quickDepProjectId, (projectId) => {
  void ensureEpicOptionsLoaded(projectId)
  const availableProducts = new Set((props.projects.find(project => project.id === projectId)?.products ?? []).map(product => product.id))
  quickDepProductIds.value = quickDepProductIds.value.filter(id => availableProducts.has(id))
})

const canCreateQuickDependency = computed(() =>
  !!quickDepTitle.value.trim()
  && !!quickDepProjectId.value
  && !!quickDepParentEpicId.value
  && quickDepProductIds.value.length > 0
  && !!quickDepQuarter.value
)

async function createQuickDependency() {
  if (!canCreateQuickDependency.value || quickDepSaving.value)
    return

  quickDepSaving.value = true
  try {
    const { quarterYear, quarterNumber } = parseQuarterValue(quickDepQuarter.value)
    const created = await roadmapStore.createDemand({
      itemType: 'Demand',
      parentDemandId: quickDepParentEpicId.value,
      title: quickDepTitle.value.trim(),
      description: '',
      projectId: quickDepProjectId.value,
      projectIds: [],
      quarterYear,
      quarterNumber,
      type: 'Planned',
      classification: 'Strategic',
      productIds: [...quickDepProductIds.value],
      status: 'Backlog',
      issueLinks: [],
      customers: [],
      dependencyDemandIds: [],
      hasNoKpi: false
    })

    if (created?.id) {
      form.dependencyDemandIds = [...new Set([...(form.dependencyDemandIds ?? []), created.id])]
      // Refresh the shared dependency options so the new demand shows in the selected list.
      await roadmapStore.fetchDependencyOptions()
    }

    quickDepOpen.value = false
    dependencySearch.value = ''
  }
  catch {
    // handled by useApi
  }
  finally {
    quickDepSaving.value = false
  }
}

const selectedNonDemandProjects = computed(() =>
  props.projects.filter(project => (form.projectIds ?? []).includes(project.id))
)

const nonDemandProjectsLabel = computed(() => {
  const count = selectedNonDemandProjects.value.length
  if (!count)
    return 'Selecione os times'

  if (count === 1)
    return selectedNonDemandProjects.value[0]!.name

  return `${count} times`
})

const demandProductsLabel = computed(() => {
  const selected = productsForProject.value.filter(product => form.productIds.includes(product.id))
  if (!selected.length)
    return productsForProject.value.length ? 'Selecione os produtos' : 'Selecione um time primeiro'

  if (selected.length === 1)
    return selected[0]!.name

  return `${selected.length} produtos`
})

const simpleEpicProductsLabel = computed(() => {
  const selected = productsForSimpleEpic.value.filter(product => form.productIds.includes(product.id))
  if (!selected.length)
    return productsForSimpleEpic.value.length ? 'Selecione os produtos' : 'Selecione um time primeiro'

  if (selected.length === 1)
    return selected[0]!.name

  return `${selected.length} produtos`
})

function setCustomerTags(tags: string[]) {
  form.customers = [...new Set(tags.map(tag => tag.trim()).filter(Boolean))]
}

function registerCustomerRename(from: string, to: string) {
  const original = from.trim()
  const renamed = to.trim()

  if (!original || !renamed || original === renamed)
    return

  pendingCustomerRenames.value = [
    ...pendingCustomerRenames.value.filter(rename => rename.from !== original),
    { from: original, to: renamed }
  ]
}

function addCustomerTag(value: string) {
  const normalized = value.trim()
  if (!normalized) return

  if (customerRenameSource.value)
    registerCustomerRename(customerRenameSource.value, normalized)

  setCustomerTags([...customerTags.value, normalized])
  customerRenameSource.value = null
  customerInput.value = ''
}

function handleCustomerEnter() {
  const firstSuggestion = filteredCustomerSuggestions.value[0]
  addCustomerTag(firstSuggestion ?? customerInput.value)
}

function removeCustomerTag(tag: string) {
  setCustomerTags(customerTags.value.filter(customer => customer !== tag))
}

function editCustomerTag(tag: string) {
  customerRenameSource.value = tag
  customerInput.value = tag
  removeCustomerTag(tag)
  queueMicrotask(() => customerInputRef.value?.focus())
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

async function deleteTradeOff(tradeOffId: string) {
  tradeOffDeletingId.value = tradeOffId

  try {
    await api.del(`/api/roadmap/trade-offs/${tradeOffId}`)
    removedTradeOffIds.value = [...removedTradeOffIds.value, tradeOffId]
    emit('trade-off-deleted', tradeOffId)
    toast.add({ title: 'Trade-off removido', color: 'success' })
  }
  catch {
    // error handled by useApi
  }
  finally {
    tradeOffDeletingId.value = null
  }
}

function isKpiLinkComplete(link: EditableDemandKpiLink) {
  return !!link.kpiId
}

const missingSubmitReason = computed(() => {
  if (!form.itemType)
    return 'Selecione o tipo do item'
  if (showEpicModeSelection.value)
    return 'Selecione o modo do épico'
  // For demands: validate in progressive-disclosure order (Time → Epic → Product → Title → rest)
  if (isDemand.value && !form.projectId)
    return 'Selecione o time'
  if (isDemand.value && !form.parentDemandId)
    return 'Selecione o épico pai'
  if (isDemand.value && form.productIds.length === 0)
    return 'Selecione ao menos um produto'
  // For simple epics: validate in disclosure order (Time → Roadmap pai → Produto → restante)
  if (isSimpleEpic.value && !(form.projectIds?.length ?? 0))
    return 'Selecione o time'
  if (isSimpleEpic.value && !form.parentDemandId)
    return 'Selecione o roadmap pai'
  if (isSimpleEpic.value && form.productIds.length === 0)
    return 'Selecione ao menos um produto para o épico'
  if (!form.title)
    return `Informe o título ${isRoadmap.value ? 'do roadmap' : isEpic.value ? 'do épico' : 'da demanda'}`
  if (!isDemand.value && !(form.projectIds?.length ?? 0))
    return 'Selecione ao menos um time'
  if (!isRoadmap.value && !isDemand.value && !form.parentDemandId)
    return 'Selecione o roadmap pai'
  if (isDemand.value && (form.quarterYear == null || form.quarterNumber == null))
    return 'Selecione o quarter'
  if (isSimpleEpic.value && (form.quarterYear == null || form.quarterNumber == null))
    return 'Selecione o quarter do épico'
  if (isEpic.value && !form.classification)
    return 'Selecione a classificação'
  if (deprioritizationReasonRequired.value && !form.deprioritizationReason)
    return 'Selecione o motivo da despriorização'
  if (deprioritizationReasonRequired.value && (form.deprioritizationReason === 'ReplacedByOtherInitiative' || form.deprioritizationReason === 'HigherValuePrioritization') && !form.replacementDemandId)
    return 'Selecione a demanda priorizada no lugar'
  if (observationRequired.value && !form.observation)
    return 'Preencha a observação da despriorização'
  if (deliveryDateRequired.value && !form.deliveryDate)
    return 'Informe a data de entrega para concluir a demanda'
  if (blockedReasonRequired.value && !form.blockedReason.trim())
    return 'Preencha o motivo do impedimento'
  if (spilloverFieldsRequired.value && !form.spilloverReason)
    return 'Selecione o motivo do transbordo'
  if (spilloverFieldsRequired.value && !form.spilloverObservation?.trim())
    return 'Preencha a observação do transbordo'
  if (isTransitioningToSpillover.value && (!spilloverTargetYear.value || !spilloverTargetNumber.value))
    return 'Informe o ano e quarter de destino do transbordo'

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
    || missingSubmitReason.value === 'Selecione o motivo do transbordo'
    || missingSubmitReason.value === 'Preencha a observação do transbordo'
    || missingSubmitReason.value === 'Informe o ano e quarter de destino do transbordo'
    || missingSubmitReason.value === 'Selecione a demanda priorizada no lugar'
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

  // When transitioning to Spillover: emit create-spillover instead of regular submit
  if (isTransitioningToSpillover.value && props.demand) {
    emit('create-spillover',
      props.demand.id,
      spilloverTargetYear.value ?? 0,
      spilloverTargetNumber.value ?? 0,
      form.spilloverReason ?? '',
      form.spilloverObservation?.trim() ?? ''
    )
    return
  }

  const sanitizedIssueLinks = sanitizeIssueLinksForItem(form.itemType, normalizeIssueLinks(form.issueLinks))
  const normalizedQuarterYear = form.quarterYear ?? 0
  const normalizedQuarterNumber = form.quarterNumber ?? 0

  emit('submit', {
    ...form,
    itemType: form.itemType,
    removedDependedOnByIds: removedDependedOnByIds.value,
    projectId: form.projectId || undefined,
    projectIds: form.itemType === 'Demand' ? [] : (form.projectIds ?? []),
    quarterYear: normalizedQuarterYear,
    quarterNumber: normalizedQuarterNumber,
    parentDemandId: form.parentDemandId || undefined,
    jiraIssue: undefined,
    issueLinks: sanitizedIssueLinks,
    hours: Number.isNaN(form.hours as number) ? undefined : form.hours,
    customers: sanitizeCustomersForItem(form.itemType, form.customers),
    customerRenames: form.itemType === 'Epic' ? pendingCustomerRenames.value : [],
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

            <UFormField v-if="showProjectsField" :label="isSimpleEpic || isDemand ? 'Time' : 'Times'" required>
              <USelect
                v-if="isDemand"
                v-model="form.projectId"
                :items="sortedProjects.map(p => ({ value: p.id, label: p.name }))"
                placeholder="Selecione"
                class="w-full"
                @update:model-value="onDemandProjectChange"
              />

              <!-- Épico simples: apenas 1 projeto -->
              <USelect
                v-else-if="isSimpleEpic"
                :model-value="form.projectIds?.[0] ?? ''"
                :items="sortedProjects.map(p => ({ value: p.id, label: p.name }))"
                placeholder="Selecione"
                class="w-full"
                @update:model-value="(value) => setSingleEpicProject(value as string | undefined)"
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

          <!-- Épicos: Roadmap pai e (para épico simples) Produto lado a lado, em sequência. -->
          <div v-if="showRoadmapParentField" class="grid gap-3" :class="showSimpleEpicProductField ? 'md:grid-cols-2' : ''">
          <UFormField label="Roadmap pai" required>
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
                      <p class="text-xs font-medium text-highlighted">Buscar roadmaps de outros times</p>
                      <p class="text-[11px] text-muted">Desmarcado: mostra apenas os roadmaps dos times selecionados.</p>
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
                        ? 'Selecione ao menos um time para buscar roadmaps.'
                        : includeCrossProjectRoadmaps
                          ? 'Nenhum roadmap encontrado na busca atual.'
                          : 'Nenhum roadmap encontrado para os times selecionados. Ative a busca em outros times para ampliar a lista.' }}
                    </p>
                  </div>
                </div>
              </template>
            </UPopover>
          </UFormField>

          <UFormField v-if="showSimpleEpicProductField" label="Produto" required>
            <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
              <UButton
                type="button"
                variant="outline"
                color="neutral"
                trailing-icon="i-lucide-chevron-down"
                class="w-full justify-between"
                :disabled="!productsForSimpleEpic.length"
              >
                <span class="truncate">{{ simpleEpicProductsLabel }}</span>
              </UButton>

              <template #content>
                <div class="min-w-72 space-y-1 p-2">
                  <label
                    v-for="product in productsForSimpleEpic"
                    :key="product.id"
                    class="flex cursor-pointer items-center gap-2 rounded-lg px-2.5 py-2 text-sm text-highlighted transition-colors hover:bg-elevated"
                  >
                    <input
                      type="checkbox"
                      class="h-4 w-4 accent-primary"
                      :checked="form.productIds.includes(product.id)"
                      @change="(event) => toggleProduct(product.id, (event.target as HTMLInputElement).checked)"
                    >
                    <span class="truncate">{{ product.name }}</span>
                  </label>

                  <p v-if="!productsForSimpleEpic.length" class="px-2.5 py-3 text-xs italic text-muted">
                    Selecione um time primeiro.
                  </p>
                </div>
              </template>
            </UPopover>
          </UFormField>
          </div>

          <div v-if="!hasSelectedItemType" class="rounded-xl border border-dashed border-default bg-elevated/40 px-4 py-6 text-sm text-muted">
            Selecione o tipo do item para carregar os campos de cadastro.
          </div>

          <!-- Epic mode selection (only for new epics) -->
          <div v-if="showEpicModeSelection" class="rounded-xl border border-primary/20 bg-primary/5 p-5 text-center">
            <h4 class="text-sm font-semibold text-highlighted">Esse épico terá demandas ou dependência de outros times?</h4>
            <p class="mt-1 text-xs text-muted">Escolha como este épico será planejado.</p>
            <div class="mt-4 flex justify-center gap-3">
              <UButton
                type="button"
                color="neutral"
                variant="outline"
                icon="i-lucide-git-branch"
                @click="selectEpicMode(true)"
              >
                Não — Planejar pelo épico
              </UButton>
              <UButton
                type="button"
                color="neutral"
                variant="outline"
                icon="i-lucide-list-todo"
                @click="selectEpicMode(false)"
              >
                Sim — Épico com demandas
              </UButton>
            </div>
          </div>

          <div v-if="showRestFields" class="space-y-3">
          <!-- Step 2+3: Épico pai and Produto (progressive disclosure) -->
          <div v-if="showDemandEpicPaiField" class="grid gap-3" :class="showDemandProductField ? 'md:grid-cols-2' : ''">
            <UFormField label="Épico pai" required>
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
                        <p class="text-xs font-medium text-highlighted">Buscar épicos de outros times</p>
                        <p class="text-[11px] text-muted">Desmarcado: mostra apenas os épicos do time selecionado.</p>
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
                          ? 'Selecione um time para buscar épicos.'
                          : includeCrossProjectEpics
                            ? 'Nenhum épico encontrado na busca atual.'
                            : 'Nenhum épico encontrado para o time selecionado. Ative a busca em outros times para ampliar a lista.' }}
                      </p>
                    </div>
                  </div>
                </template>
              </UPopover>
            </UFormField>

            <UFormField v-if="showDemandProductField" label="Produto" required>
              <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                <UButton
                  type="button"
                  variant="outline"
                  color="neutral"
                  trailing-icon="i-lucide-chevron-down"
                  class="w-full justify-between"
                  :disabled="!productsForProject.length"
                >
                  <span class="truncate">{{ demandProductsLabel }}</span>
                </UButton>

                <template #content>
                  <div class="min-w-72 space-y-1 p-2">
                    <label
                      v-for="product in productsForProject"
                      :key="product.id"
                      class="flex cursor-pointer items-center gap-2 rounded-lg px-2.5 py-2 text-sm text-highlighted transition-colors hover:bg-elevated"
                    >
                      <input
                        type="checkbox"
                        class="h-4 w-4 accent-primary"
                        :checked="form.productIds.includes(product.id)"
                        @change="(event) => toggleProduct(product.id, (event.target as HTMLInputElement).checked)"
                      >
                      <span class="truncate">{{ product.name }}</span>
                    </label>

                    <p v-if="!productsForProject.length" class="px-2.5 py-3 text-xs italic text-muted">
                      Selecione um time primeiro.
                    </p>
                  </div>
                </template>
              </UPopover>
            </UFormField>
          </div>

          <template v-if="showRestAfterProgressive">
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

          <!-- Simple epic: Quarter, Tipo, Produtos, Horas -->
          <div v-if="isSimpleEpic" class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
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

          <div v-if="isEpic && !isSimpleEpic" class="grid gap-3 md:grid-cols-2">
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
          </div>

          <UFormField v-if="isSimpleEpic" label="Classificação" required>
            <USelect
              v-model="form.classification"
              :items="classificationOptions"
              placeholder="Selecione"
              class="w-full"
            />
          </UFormField>

          <div v-if="isRoadmap" class="grid gap-3 md:grid-cols-2">
            <UFormField label="Status">
              <USelect
                v-model="form.status as DemandStatus"
                :items="statusOptionsForRoadmap"
                class="w-full"
              />
            </UFormField>
          </div>

          <UFormField v-if="!isRoadmap" label="Issues (Jira)"><div ref="issueLinksContainer" class="space-y-2">
            <div
              v-for="(issue, index) in form.issueLinks"
              :key="index"
              class="grid gap-2 md:grid-cols-[minmax(0,1fr)_minmax(0,180px)_auto]"
            >
              <UInput
                :model-value="issue.url"
                placeholder="https://..."
                class="w-full"
                @update:model-value="(value) => updateIssueUrl(index, value)"
              />
              <UInput
                v-model="issue.key"
                placeholder="Preenchida automaticamente"
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
                      title="Editar cliente"
                      @click="editCustomerTag(customer)"
                    >
                      <UIcon name="i-lucide-pencil" class="h-3 w-3" />
                    </button>
                    <button
                      type="button"
                      class="inline-flex h-4 w-4 items-center justify-center rounded-full hover:bg-primary/15"
                      title="Remover cliente"
                      @click="removeCustomerTag(customer)"
                    >
                      <UIcon name="i-lucide-x" class="h-3 w-3" />
                    </button>
                  </span>

                  <input
                    ref="customerInput"
                    v-model="customerInput"
                    type="text"
                    class="min-w-[12rem] flex-1 bg-transparent px-1 py-1 text-sm text-highlighted outline-none placeholder:text-muted"
                    placeholder="Digite para buscar, criar ou editar um cliente"
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

          </template>
          </div>
        </section>

        <section v-if="showRestFields && !isRoadmap && showRestAfterProgressive" class="space-y-4 border-t border-default pt-4">
          <div>
            <h3 class="text-sm font-semibold text-highlighted">Dependências entre épicos e demandas</h3>
            <p class="mt-1 text-xs text-muted">
              Relacione demandas que precisam ser concluídas antes deste item seguir adiante.
            </p>
          </div>

          <div v-if="visibleDependedOnBy.length" class="space-y-1">
            <p class="text-xs font-medium text-muted uppercase tracking-wide">Este item bloqueia</p>
            <div class="flex flex-wrap gap-2">
              <span
                v-for="dep in visibleDependedOnBy"
                :key="dep.demandId"
                class="inline-flex items-center gap-1 rounded-full border border-orange-200/70 bg-orange-50/60 px-2 py-1 text-xs text-orange-700 dark:border-orange-800/50 dark:bg-orange-900/15 dark:text-orange-300/90"
              >
                <UIcon name="i-lucide-lock" class="h-3 w-3 shrink-0" />
                {{ dep.projectName }} · {{ dep.title }}
                <button
                  type="button"
                  class="inline-flex h-4 w-4 items-center justify-center rounded-full hover:bg-orange-200/60 dark:hover:bg-orange-800/40"
                  title="Remover este vínculo de bloqueio"
                  @click="removeDependedOnBy(dep.demandId)"
                >
                  <UIcon name="i-lucide-x" class="h-3 w-3" />
                </button>
              </span>
            </div>
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
                <span class="rounded bg-white/60 px-1 text-[10px] font-semibold uppercase dark:bg-black/20">{{ dependency.itemType === 'Epic' ? 'Épico' : 'Demanda' }}</span>
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
                  <div class="flex items-center gap-1.5">
                    <span
                      class="shrink-0 rounded px-1 py-0.5 text-[10px] font-semibold uppercase tracking-wide"
                      :class="dependency.itemType === 'Epic' ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300' : 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300'"
                    >
                      {{ dependency.itemType === 'Epic' ? 'Épico' : 'Demanda' }}
                    </span>
                    <p class="truncate text-sm font-medium text-highlighted">{{ dependency.title }}</p>
                  </div>
                  <p class="text-xs text-muted">{{ dependency.projectName }} · {{ dependency.quarterLabel }} · {{ dependency.status }}</p>
                </div>
              </label>

              <p v-if="!filteredDependencyOptions.length" class="text-xs italic text-muted">
                Nenhum item encontrado para vincular.
              </p>
            </div>

            <!-- Criar a demanda na hora e já vincular como dependência -->
            <button
              v-if="hasDependencyQuery && !quickDepOpen"
              type="button"
              class="flex w-full items-center gap-2 rounded-lg border border-dashed border-primary/40 px-3 py-2 text-left text-sm text-primary transition-colors hover:bg-primary/5"
              @click="openQuickDependencyCreate"
            >
              <UIcon name="i-lucide-plus" class="h-4 w-4 shrink-0" />
              <span class="truncate">Criar demanda “{{ dependencySearch.trim() }}” e vincular</span>
            </button>

            <div v-if="quickDepOpen" class="space-y-3 rounded-lg border border-primary/30 bg-primary/5 p-3">
              <p class="text-xs font-semibold uppercase tracking-wide text-primary">Nova demanda para vincular</p>

              <UFormField label="Título" required>
                <UInput v-model="quickDepTitle" placeholder="Descreva a demanda brevemente" class="w-full" />
              </UFormField>

              <div class="grid gap-3 md:grid-cols-2">
                <UFormField label="Time" required>
                  <USelect
                    v-model="quickDepProjectId"
                    :items="sortedProjects.map(project => ({ value: project.id, label: project.name }))"
                    placeholder="Selecione"
                    class="w-full"
                  />
                </UFormField>

                <UFormField label="Épico pai" required>
                  <USelect
                    v-model="quickDepParentEpicId"
                    :items="quickDepEpicSelectItems"
                    placeholder="Selecione"
                    class="w-full"
                  />
                </UFormField>

                <UFormField label="Quarter" required>
                  <USelect
                    v-model="quickDepQuarter"
                    :items="quarters"
                    placeholder="Selecione"
                    class="w-full"
                  />
                </UFormField>

                <UFormField label="Produto" required>
                  <UPopover :content="{ side: 'bottom', align: 'start', sideOffset: 8 }">
                    <UButton
                      type="button"
                      variant="outline"
                      color="neutral"
                      trailing-icon="i-lucide-chevron-down"
                      class="w-full justify-between"
                      :disabled="!quickDepProducts.length"
                    >
                      <span class="truncate">{{ quickDepProductsLabel }}</span>
                    </UButton>

                    <template #content>
                      <div class="min-w-64 space-y-1 p-2">
                        <label
                          v-for="product in quickDepProducts"
                          :key="product.id"
                          class="flex cursor-pointer items-center gap-2 rounded-lg px-2.5 py-2 text-sm text-highlighted transition-colors hover:bg-elevated"
                        >
                          <input
                            type="checkbox"
                            class="h-4 w-4 accent-primary"
                            :checked="quickDepProductIds.includes(product.id)"
                            @change="(event) => toggleQuickDepProduct(product.id, (event.target as HTMLInputElement).checked)"
                          >
                          <span class="truncate">{{ product.name }}</span>
                        </label>

                        <p v-if="!quickDepProducts.length" class="px-2.5 py-3 text-xs italic text-muted">
                          Selecione um time primeiro.
                        </p>
                      </div>
                    </template>
                  </UPopover>
                </UFormField>
              </div>

              <div class="flex justify-end gap-2">
                <UButton size="xs" color="neutral" variant="ghost" @click="quickDepOpen = false">Cancelar</UButton>
                <UButton
                  size="xs"
                  icon="i-lucide-link"
                  :loading="quickDepSaving"
                  :disabled="!canCreateQuickDependency"
                  @click="createQuickDependency"
                >
                  Criar e vincular
                </UButton>
              </div>
            </div>

            <!-- Scroll anchor: brings the results + "create & link" into view when searching. -->
            <div ref="dependencyResults" aria-hidden="true"></div>
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
                :items="statusOptionsForForm"
                class="w-full"
              />
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

          <UFormField v-if="blockedReasonRequired" label="Motivo do impedimento" required>
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
            :hint="(form.deprioritizationReason === 'ReplacedByOtherInitiative' || form.deprioritizationReason === 'HigherValuePrioritization') ? undefined : 'Opcional'"
            :required="form.deprioritizationReason === 'ReplacedByOtherInitiative' || form.deprioritizationReason === 'HigherValuePrioritization'"
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

          <template v-if="isTransitioningToSpillover">
            <p class="text-sm text-muted">
              O histórico {{ form.itemType === 'Epic' ? 'do épico' : 'da demanda' }} será preservado
              no quarter atual e uma cópia do tipo <em>Spillover</em> será criada no quarter de destino.
            </p>
            <div class="grid grid-cols-2 gap-3">
              <UFormField label="Ano destino" required>
                <UInput
                  v-model="spilloverTargetYear"
                  type="number"
                  :min="2020"
                  :max="2040"
                  placeholder="Ano"
                />
              </UFormField>
              <UFormField label="Quarter destino" required>
                <USelect
                  v-model="spilloverTargetNumber"
                  :items="[{ label: 'Q1', value: 1 }, { label: 'Q2', value: 2 }, { label: 'Q3', value: 3 }, { label: 'Q4', value: 4 }]"
                  value-key="value"
                  label-key="label"
                />
              </UFormField>
            </div>
          </template>

          <UFormField v-if="spilloverFieldsRequired" label="Motivo do transbordo" required>
            <USelect
              v-model="form.spilloverReason"
              :items="spilloverReasonOptions"
              value-key="value"
              option-attribute="label"
              placeholder="Selecione o motivo"
              class="w-full"
              :class="!form.spilloverReason ? 'ring-2 ring-red-400' : ''"
            />
          </UFormField>

          <UFormField v-if="spilloverFieldsRequired" label="Observação transbordo" required>
            <UTextarea
              v-model="form.spilloverObservation"
              placeholder="Descreva o motivo do transbordo"
              :rows="2"
              class="w-full"
              :class="!form.spilloverObservation ? 'ring-2 ring-red-400' : ''"
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
              <div class="flex items-start justify-between gap-3">
                <div class="flex flex-wrap items-center gap-2">
                  <UBadge variant="subtle" color="neutral">{{ tradeOff.projectName }}</UBadge>
                  <UBadge variant="subtle" color="primary">{{ tradeOff.quarterLabel }}</UBadge>
                  <UBadge variant="subtle" color="warning">{{ deprioritizationReasonLabels[tradeOff.reason] }}</UBadge>
                </div>

                <UButton
                  size="xs"
                  color="error"
                  variant="ghost"
                  icon="i-lucide-trash-2"
                  :loading="tradeOffDeletingId === tradeOff.id"
                  :disabled="tradeOffDeletingId !== null && tradeOffDeletingId !== tradeOff.id"
                  @click="deleteTradeOff(tradeOff.id)"
                >
                  Excluir
                </UButton>
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
