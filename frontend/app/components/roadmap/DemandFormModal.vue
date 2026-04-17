<script setup lang="ts">
import type { RoadmapDemand, RoadmapProject, DemandDependencyOption, DemandFormData, DemandType, DemandClassification, DemandStatus, Kpi, DemandKpiLinkInput, ImpactType, ConfidenceLevel } from '~/types/roadmap'

type DemandFormState = Omit<DemandFormData, 'classification'> & {
  classification: DemandClassification | ''
}

type ImpactDisplayType = 'Percentage' | 'Number' | 'Currency'

type EditableDemandKpiLink = DemandKpiLinkInput & {
  impactDisplayType: ImpactDisplayType
  estimatedImpactInput: string
}

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
  submit: [data: DemandFormData, links: DemandKpiLinkInput[]]
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
  { value: 'Homologation',  label: 'Homologação' }
]

const statusOptions = [
  { value: 'Backlog',       label: 'Backlog' },
  { value: 'InProgress',    label: 'Em andamento' },
  { value: 'Done',          label: 'Concluído' },
  { value: 'Deprioritized', label: 'Despriorizado' }
]

type DemandFormTab = 'general' | 'status' | 'result'

const resultTabs = computed(() => {
  const tabs: Array<{ value: DemandFormTab, label: string }> = [
    { value: 'general', label: 'Geral' }
  ]

  if (isEdit.value)
    tabs.push({ value: 'status', label: 'Status' })

  tabs.push({ value: 'result', label: 'KPIs' })

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
  deliveryDate: '',
  problemClarity: undefined,
  hasNoKpi: false
})

const kpiLinkEdits = ref<EditableDemandKpiLink[]>([])

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
    form.deliveryDate = props.demand.deliveryDate ?? ''
    form.problemClarity = props.demand.problemClarity ?? undefined
    form.hasNoKpi = props.demand.hasNoKpi ?? false
    kpiLinkEdits.value = (props.demand.kpiLinks ?? []).map(l => toEditableKpiLink({
      kpiId: l.kpiId,
      impactType: l.impactType,
      estimatedImpact: l.estimatedImpact,
      confidenceLevel: l.confidenceLevel
    }))
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
    form.deliveryDate = ''
    form.problemClarity = undefined
    form.hasNoKpi = false
    kpiLinkEdits.value = []
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
  if (form.problemClarity == null)
    return 'Informe a nota de clareza do problema'
  if (!form.hasNoKpi && kpiLinkEdits.value.length === 0)
    return 'Adicione ao menos um KPI impactado ou marque a demanda como sem KPI'
  if (!form.hasNoKpi && kpiLinkEdits.value.some(link => !isKpiLinkComplete(link)))
    return 'Preencha todos os KPIs vinculados antes de salvar'

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

  if (
    missingSubmitReason.value === 'Informe a nota de clareza do problema'
    || missingSubmitReason.value === 'Adicione ao menos um KPI impactado ou marque a demanda como sem KPI'
    || missingSubmitReason.value === 'Preencha todos os KPIs vinculados antes de salvar'
  ) {
    activeTab.value = 'result'
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
    const validLinks = form.hasNoKpi
      ? []
      : kpiLinkEdits.value.filter(link => link.kpiId)

    emit('submit', {
      ...form,
      hours: Number.isNaN(form.hours as number) ? undefined : form.hours,
      classification: form.classification as DemandClassification
    }, validLinks.map(link => ({
      kpiId: link.kpiId,
      impactType: link.impactType,
      estimatedImpact: link.estimatedImpact,
      confidenceLevel: link.confidenceLevel
    })))
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

          <div class="grid grid-cols-2 gap-3">
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

          <div class="grid grid-cols-1 gap-3 md:grid-cols-3">
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

        <template v-else>
        <section class="space-y-4">
          <div>
            <h3 class="text-sm font-semibold text-highlighted">Clareza do problema</h3>
          </div>

          <div class="grid grid-cols-1 gap-3">
            <div class="space-y-2">
              <div class="flex items-center gap-1">
                <label class="text-sm font-medium text-highlighted">
                  Nota da clareza <span class="text-error-500">*</span>
                </label>
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
                placeholder="0 = Vago, 10 = validado"
                class="w-full max-w-40"
                @update:model-value="(v: string | number | null | undefined) => form.problemClarity = v === '' || v == null ? undefined : Number(v)"
              />
            </div>
          </div>
        </section>

        <section class="space-y-4 border-t border-default pt-4">
          <div>
            <h3 class="text-sm font-semibold text-highlighted">KPIs impactados</h3>
            <p class="mt-1 text-xs text-muted">
              Relacione a demanda aos indicadores que devem ser influenciados pela entrega.
            </p>
          </div>

          <label class="flex items-center gap-2 cursor-pointer select-none">
            <input
              v-model="form.hasNoKpi"
              type="checkbox"
              class="h-4 w-4 accent-primary"
            >
            <span class="text-sm" :class="form.hasNoKpi ? 'text-warning font-medium' : 'text-muted'">
              Marcar demanda como sem KPI
            </span>
          </label>

          <div v-if="form.hasNoKpi" class="rounded-lg border border-dashed border-warning/40 bg-warning/5 p-3 text-sm text-muted">
            Esta demanda foi marcada como sem KPI vinculado.
          </div>

          <div v-else class="space-y-3">
            <div class="overflow-x-auto rounded-lg border border-default bg-elevated">
              <table class="min-w-full table-fixed">
                <thead class="border-b border-default bg-default/80">
                  <tr>
                    <th class="w-[18rem] px-3 py-2 text-left text-xs font-semibold uppercase tracking-[0.08em] text-muted">KPI relacionado</th>
                    <th class="px-3 py-2 text-left text-xs font-semibold uppercase tracking-[0.08em] text-muted">Tipo de impacto</th>
                    <th class="px-3 py-2 text-left text-xs font-semibold uppercase tracking-[0.08em] text-muted">Impacto estimado</th>
                    <th class="px-3 py-2 text-left text-xs font-semibold uppercase tracking-[0.08em] text-muted">
                      <div class="flex items-center gap-1">
                        <span>Nível de confiança</span>
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
                                Nível de confiança = o quanto você acredita que aquele épico realmente vai impactar o KPI
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
                    </th>
                    <th class="w-14 px-3 py-2 text-right text-xs font-semibold uppercase tracking-[0.08em] text-muted">Ação</th>
                  </tr>
                </thead>
                <tbody>
                  <template
                    v-for="(link, idx) in kpiLinkEdits"
                    :key="idx"
                  >
                    <tr>
                      <td class="w-[18rem] px-3 py-3 align-top">
                        <USelect
                          v-model="link.kpiId"
                          :items="getKpiOptionsForRow(link.kpiId)"
                          placeholder="Selecione um KPI"
                          class="w-full"
                        />
                      </td>
                      <td class="px-3 py-3 align-top">
                        <USelect
                          :model-value="link.impactDisplayType"
                          :items="impactTypeOptions"
                          class="w-full"
                          @update:model-value="(value) => updateImpactDisplayType(idx, value as string | undefined)"
                        />
                      </td>
                      <td class="px-3 py-3 align-top">
                        <UInput
                          :model-value="link.estimatedImpactInput"
                          :placeholder="link.impactDisplayType === 'Percentage'
                            ? 'Ex: 12,5%'
                            : link.impactDisplayType === 'Currency'
                              ? 'Ex: R$ 1.500,00'
                              : 'Ex: 1500'"
                          class="w-full"
                          @update:model-value="(value) => updateEstimatedImpactInput(idx, value)"
                        />
                      </td>
                      <td class="px-3 py-3 align-top">
                        <USelect
                          v-model="link.confidenceLevel"
                          :items="confidenceLevelOptions"
                          class="w-full"
                        />
                      </td>
                      <td class="px-3 py-3 align-top text-right">
                        <UButton
                          icon="i-lucide-trash-2"
                          variant="ghost"
                          size="xs"
                          color="error"
                          @click="removeKpiLink(idx)"
                        />
                      </td>
                    </tr>
                    <tr
                      v-if="getKpiImpactSummary(link)"
                      class="border-b border-default last:border-b-0"
                    >
                      <td colspan="5" class="px-3 pb-3 pt-0 text-xs leading-relaxed text-muted">
                        {{ getKpiImpactSummary(link) }}
                      </td>
                    </tr>
                    <tr v-else class="border-b border-default last:border-b-0" />
                  </template>
                </tbody>
              </table>
            </div>

            <UButton
              v-if="availableKpisForLink.length || !kpiLinkEdits.length"
              type="button"
              icon="i-lucide-plus"
              label="Adicionar KPI impactado"
              variant="soft"
              size="sm"
              @click="addKpiLink"
            />

            <p v-if="!(props.availableKpis ?? []).length" class="text-xs text-muted italic">
              Nenhum KPI cadastrado para este projeto. Cadastre na página de KPIs.
            </p>
          </div>
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
