<script setup lang="ts">
import type { RoadmapDemand, RoadmapProject, DemandDependencyOption, DemandFormData, DemandType, DemandClassification, DemandStatus } from '~/types/roadmap'

const props = defineProps<{
  open: boolean
  projects: RoadmapProject[]
  dependencyOptions: DemandDependencyOption[]
  demand?: RoadmapDemand | null
  defaultProjectId?: string
  defaultQuarterYear?: number
  defaultQuarterNumber?: number
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
  { value: 'Homologation',  label: 'Homologação' }
]

const statusOptions = [
  { value: 'Backlog',       label: 'Backlog' },
  { value: 'InProgress',    label: 'Em andamento' },
  { value: 'Done',          label: 'Concluído' },
  { value: 'Deprioritized', label: 'Despriorizado' }
]

const customerInput = ref('')
const dependencySearch = ref('')

const observationRequired = computed(() => form.status === 'Deprioritized')
const deliveryDateRequired = computed(() => form.status === 'Done')

const form = reactive<DemandFormData>({
  title: '',
  description: '',
  projectId: '',
  quarterYear: currentYear,
  quarterNumber: 1,
  type: 'Planned',
  classification: 'Evolution',
  productIds: [],
  status: 'Backlog',
  observation: '',
  jiraIssue: '',
  hours: undefined,
  customers: [],
  dependencyDemandIds: [],
  isBlocked: false,
  blockedReason: '',
  deliveryDate: ''
})

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

watch(() => props.open, (open) => {
  if (!open) return

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
    customerInput.value = ''
  }
  else {
    form.title = ''
    form.description = ''
    form.projectId = props.defaultProjectId ?? props.projects[0]?.id ?? ''
    form.quarterYear = props.defaultQuarterYear ?? currentYear
    form.quarterNumber = props.defaultQuarterNumber ?? 1
    form.type = 'Planned'
    form.classification = 'Evolution'
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
    customerInput.value = ''
  }
})

watch(() => form.projectId, () => {
  form.productIds = []
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

const filteredDependencyOptions = computed(() => {
  const query = dependencySearch.value.trim().toLowerCase()

  return props.dependencyOptions.filter(option => {
    if (props.demand && option.demandId === props.demand.id)
      return false

    if (!query)
      return true

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

const isSubmitDisabled = computed(() =>
  !form.title
  || !form.projectId
  || form.productIds.length === 0
  || (observationRequired.value && !form.observation)
  || (deliveryDateRequired.value && !form.deliveryDate)
  || (form.isBlocked && !form.blockedReason)
)

const isSubmitting = ref(false)

async function handleSubmit() {
  if (isSubmitDisabled.value) return
  isSubmitting.value = true
  try {
    emit('submit', { ...form })
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
    :description="isEdit ? 'Edite os dados da demanda.' : 'Preencha os dados para criar uma nova demanda no roadmap.'"
    :ui="{ content: 'sm:max-w-4xl' }"
    @update:open="emit('update:open', $event)"
  >
    <template #body>
      <form
        class="space-y-4"
        @submit.prevent="handleSubmit"
      >
        <!-- Título -->
        <UFormField label="Título" required>
          <UInput
            v-model="form.title"
            placeholder="Descreva a demanda brevemente"
            class="w-full"
          />
        </UFormField>

        <!-- Descrição -->
        <UFormField label="Descrição">
          <UTextarea
            v-model="form.description"
            placeholder="Detalhes adicionais (opcional)"
            :rows="2"
            class="w-full"
          />
        </UFormField>

        <!-- Projeto + Quarter + Tipo + Classificação: 4 cols -->
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
              v-model="form.classification as DemandClassification"
              :items="classificationOptions"
              class="w-full"
            />
          </UFormField>
        </div>

        <!-- Issue Jira + Horas + Data entrega + Clientes: 4 cols -->
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <UFormField label="Issue (Jira)">
            <UInput
              v-model="form.jiraIssue"
              placeholder="Ex: PROJ-1234"
              class="w-full"
            />
          </UFormField>

          <UFormField label="Horas">
            <UInput
              v-model.number="form.hours"
              type="number"
              min="0"
              step="0.5"
              placeholder="Ex: 8"
              class="w-full"
            />
          </UFormField>

          <UFormField
            v-if="isEdit"
            :label="deliveryDateRequired ? 'Data de entrega *' : 'Data de entrega'"
          >
            <UInput
              v-model="form.deliveryDate"
              type="date"
              class="w-full"
              :class="deliveryDateRequired && !form.deliveryDate ? 'ring-2 ring-red-400' : ''"
            />
          </UFormField>

          <UFormField label="Clientes envolvidos">
            <div class="space-y-2">
              <div
                v-if="customerTags.length"
                class="flex min-h-10 flex-wrap gap-2 rounded-lg border border-default bg-elevated p-2"
              >
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
              </div>
              <div class="flex gap-2">
                <UInput
                  v-model="customerInput"
                  placeholder="Digite um cliente e pressione Enter"
                  class="w-full"
                  @keydown.enter.prevent="addCustomerTag(customerInput)"
                />
                <UButton
                  type="button"
                  variant="soft"
                  color="neutral"
                  icon="i-lucide-plus"
                  @click="addCustomerTag(customerInput)"
                />
              </div>
            </div>
          </UFormField>
        </div>

        <!-- Produtos (obrigatório, multi-select) -->
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

        <UFormField label="Dependências entre demandas">
          <div class="space-y-2">
            <UInput
              v-model="dependencySearch"
              placeholder="Buscar por projeto, título, quarter ou status"
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

            <div class="max-h-56 space-y-2 overflow-y-auto rounded-lg border border-default bg-elevated p-2.5">
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
        </UFormField>

        <!-- Status + Marcação de Impedimento (somente edição) -->
        <div
          v-if="isEdit"
          class="grid grid-cols-2 gap-3 items-start"
        >
          <UFormField label="Status">
            <USelect
              v-model="form.status as DemandStatus"
              :items="statusOptions"
              class="w-full"
            />
          </UFormField>

          <!-- Impedimento toggle -->
          <UFormField label="Impedimento">
            <div class="flex items-center gap-3 h-9">
              <label class="flex items-center gap-2 cursor-pointer select-none">
                <input
                  v-model="form.isBlocked"
                  type="checkbox"
                  class="accent-red-500 w-4 h-4"
                >
                <span class="text-sm" :class="form.isBlocked ? 'text-red-600 dark:text-red-400 font-medium' : 'text-muted'">
                  {{ form.isBlocked ? 'Demanda impedida' : 'Sem impedimento' }}
                </span>
              </label>
            </div>
          </UFormField>
        </div>

        <!-- Motivo do impedimento -->
        <UFormField
          v-if="form.isBlocked"
          label="Motivo do impedimento *"
        >
          <UInput
            v-model="form.blockedReason"
            placeholder="Descreva o motivo do impedimento"
            class="w-full"
            :class="!form.blockedReason ? 'ring-2 ring-red-400' : ''"
          />
          <p
            v-if="!form.blockedReason"
            class="text-xs text-red-500 mt-1"
          >
            Obrigatório ao marcar impedimento.
          </p>
        </UFormField>

        <!-- Observação (obrigatória para Despriorizado) -->
        <UFormField
          v-if="isEdit && (observationRequired || form.observation)"
          :label="observationRequired ? 'Observação *' : 'Observação'"
        >
          <UTextarea
            v-model="form.observation"
            :placeholder="observationRequired
              ? 'Justifique o motivo do status (obrigatório)'
              : 'Observação opcional'"
            :rows="2"
            class="w-full"
            :class="observationRequired && !form.observation ? 'ring-2 ring-red-400' : ''"
          />
          <p
            v-if="observationRequired && !form.observation"
            class="text-xs text-red-500 mt-1"
          >
            Obrigatório para status Despriorizado.
          </p>
        </UFormField>

        <!-- Data de entrega obrigatória quando Done (alerta) -->
        <p
          v-if="isEdit && deliveryDateRequired && !form.deliveryDate"
          class="text-xs text-red-500"
        >
          Informe a data de entrega para concluir a demanda.
        </p>
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
          :loading="isSubmitting"
          :disabled="isSubmitDisabled"
          :label="isEdit ? 'Salvar' : 'Criar Demanda'"
          icon="i-lucide-check"
          @click="handleSubmit"
        />
      </div>
    </template>
  </UModal>
</template>
