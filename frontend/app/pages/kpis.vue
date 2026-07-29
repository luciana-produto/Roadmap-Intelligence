<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import type { Kpi, KpiFormData, KpiType, KpiCategory, KpiIndicator, KpiUnit, KpiOperation } from '~/types/roadmap'

useSeoMeta({ title: 'KPIs · ProductHub' })

const roadmapStore = useRoadmapStore()
const kpiStore = useKpiStore()
const toast = useToast()

const { kpis, isLoading } = storeToRefs(kpiStore)

// ─── Init ────────────────────────────────────────────────────────────────────
onMounted(async () => {
  await kpiStore.fetchKpis()
})

// ─── Constants ───────────────────────────────────────────────────────────────
type KpiSelectOption<T extends string> = { value: T, label: string }

const kpiTypeOptions: KpiSelectOption<KpiType>[] = [
  { value: 'Business', label: 'Negócio' },
  { value: 'Product', label: 'Produto' }
]

const kpiCategoryOptions: KpiSelectOption<KpiCategory>[] = [
  { value: 'Financial', label: 'Financeiro' },
  { value: 'Growth', label: 'Crescimento' },
  { value: 'Efficiency', label: 'Eficiência' }
]

const kpiIndicatorOptions: KpiSelectOption<KpiIndicator>[] = [
  { value: 'Mrr', label: 'MRR' },
  { value: 'Stores', label: 'Lojas' },
  { value: 'Time', label: 'Tempo' },
  { value: 'Clicks', label: 'Cliques' },
  { value: 'StepsScreens', label: 'Etapas/Telas' }
]

const kpiOperationOptions: KpiSelectOption<KpiOperation>[] = [
  { value: 'HigherIsBetter', label: 'Quanto maior melhor' },
  { value: 'LowerIsBetter', label: 'Quanto menor melhor' }
]

const kpiUnitOptions: KpiSelectOption<KpiUnit>[] = [
  { value: 'Currency', label: 'Valor R$' },
  { value: 'Number', label: 'Número' },
  { value: 'Percentage', label: 'Percentual %' },
  { value: 'TimeSeconds', label: 'Tempo (segundos)' }
]

const kpiTypeLabels: Record<KpiType, string> = {
  Business: 'Negócio',
  Product: 'Produto'
}

const kpiCategoryLabels: Record<KpiCategory, string> = {
  Financial: 'Financeiro',
  Growth: 'Crescimento',
  Efficiency: 'Eficiência'
}

const kpiIndicatorLabels: Record<KpiIndicator, string> = {
  Mrr: 'MRR',
  Stores: 'Lojas',
  Time: 'Tempo',
  Clicks: 'Cliques',
  StepsScreens: 'Etapas/Telas'
}

const kpiOperationLabels: Record<KpiOperation, string> = {
  HigherIsBetter: 'Quanto maior melhor',
  LowerIsBetter: 'Quanto menor melhor'
}

const kpiUnitLabels: Record<KpiUnit, string> = {
  Currency: 'Valor R$',
  Number: 'Número',
  Percentage: 'Percentual %',
  TimeSeconds: 'Tempo (segundos)'
}

const kpiTypeBadgeColor: Record<KpiType, string> = {
  Business: 'warning',
  Product: 'success'
}

const kpiCategoryBadgeColor: Record<KpiCategory, string> = {
  Financial: 'success',
  Growth: 'info',
  Efficiency: 'warning'
}

// ─── Table columns ───────────────────────────────────────────────────────────
const columns: TableColumn<Kpi>[] = [
  { accessorKey: 'name', header: 'Nome' },
  { accessorKey: 'type', header: 'Tipo' },
  { accessorKey: 'category', header: 'Categoria' },
  { accessorKey: 'indicator', header: 'Indicador' },
  { accessorKey: 'operation', header: 'Operação' },
  { accessorKey: 'allowedUnits', header: 'Unidades' },
  { accessorKey: 'linkedDemandsCount', header: 'Demandas' },
  { accessorKey: 'actions', header: '' }
]

// ─── Form state ──────────────────────────────────────────────────────────────
const showFormModal = ref(false)
const editingKpi = ref<Kpi | null>(null)
const formData = ref<KpiFormData>(emptyForm())
const isSubmitting = ref(false)

function emptyForm(): KpiFormData {
  return {
    name: '',
    type: 'Business',
    category: 'Financial',
    indicator: 'Mrr',
    operation: 'HigherIsBetter',
    allowedUnits: [],
    description: ''
  }
}

function openCreate() {
  editingKpi.value = null
  formData.value = emptyForm()
  showFormModal.value = true
}

function openEdit(kpi: Kpi) {
  editingKpi.value = kpi
  formData.value = {
    name: kpi.name,
    type: kpi.type,
    category: kpi.category,
    indicator: kpi.indicator,
    operation: kpi.operation,
    allowedUnits: [...(kpi.allowedUnits ?? [])],
    description: kpi.description ?? ''
  }
  showFormModal.value = true
}

function toggleAllowedUnit(unit: KpiUnit) {
  const units = formData.value.allowedUnits
  formData.value.allowedUnits = units.includes(unit)
    ? units.filter(u => u !== unit)
    : [...units, unit]
}

function optionByValue<T extends string>(options: KpiSelectOption<T>[], value: T): KpiSelectOption<T> {
  return options.find(option => option.value === value) ?? options[0]
}

const selectedTypeOption = computed({
  get: () => optionByValue(kpiTypeOptions, formData.value.type),
  set: (option: KpiSelectOption<KpiType> | null) => {
    formData.value.type = option?.value ?? 'Business'
  }
})

const selectedCategoryOption = computed({
  get: () => optionByValue(kpiCategoryOptions, formData.value.category),
  set: (option: KpiSelectOption<KpiCategory> | null) => {
    formData.value.category = option?.value ?? 'Financial'
  }
})

const selectedIndicatorOption = computed({
  get: () => optionByValue(kpiIndicatorOptions, formData.value.indicator),
  set: (option: KpiSelectOption<KpiIndicator> | null) => {
    formData.value.indicator = option?.value ?? 'Mrr'
  }
})

const selectedOperationOption = computed({
  get: () => optionByValue(kpiOperationOptions, formData.value.operation),
  set: (option: KpiSelectOption<KpiOperation> | null) => {
    formData.value.operation = option?.value ?? 'HigherIsBetter'
  }
})

const submitDisabled = computed(() =>
  isSubmitting.value || !formData.value.name || formData.value.allowedUnits.length === 0
)

async function submitForm() {
  if (submitDisabled.value)
    return

  isSubmitting.value = true
  try {
    if (editingKpi.value) {
      await kpiStore.updateKpi(editingKpi.value.id, formData.value)
      toast.add({ title: 'KPI atualizado', color: 'success' })
    }
    else {
      await kpiStore.createKpi(formData.value)
      toast.add({ title: 'KPI criado', color: 'success' })
    }
    showFormModal.value = false
  }
  catch { /* handled by useApi */ }
  finally {
    isSubmitting.value = false
  }
}

// ─── Delete ──────────────────────────────────────────────────────────────────
const showDeleteConfirm = ref(false)
const deletingKpi = ref<Kpi | null>(null)

function confirmDelete(kpi: Kpi) {
  deletingKpi.value = kpi
  showDeleteConfirm.value = true
}

async function executeDelete() {
  if (!deletingKpi.value) return
  try {
    await kpiStore.deleteKpi(deletingKpi.value.id)
    toast.add({ title: 'KPI removido', color: 'success' })
  }
  catch { /* handled by useApi */ }
  finally {
    showDeleteConfirm.value = false
    deletingKpi.value = null
  }
}

// ─── Filters ─────────────────────────────────────────────────────────────────
const searchQuery = ref('')
const filterType = ref<KpiType | ''>('')
const filterCategory = ref<KpiCategory | ''>('')

const filteredKpis = computed(() => {
  let result = kpis.value
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    result = result.filter(k => k.name.toLowerCase().includes(q) || k.description?.toLowerCase().includes(q))
  }
  if (filterType.value) result = result.filter(k => k.type === filterType.value)
  if (filterCategory.value) result = result.filter(k => k.category === filterCategory.value)
  return result
})

// ─── Summary ─────────────────────────────────────────────────────────────────
const summary = computed(() => {
  const total = kpis.value.length
  const linked = kpis.value.filter(k => k.linkedDemandsCount > 0).length
  const business = kpis.value.filter(k => k.type === 'Business').length
  const product = kpis.value.filter(k => k.type === 'Product').length
  return { total, linked, business, product }
})

function allowedUnitsLabel(units?: KpiUnit[]): string {
  if (!units || units.length === 0) return '—'
  return units.map(u => kpiUnitLabels[u]).join(', ')
}
</script>

<template>
  <div class="space-y-6">
    <!-- Header ──────────────────────────────────────────────────────────── -->
    <div class="flex items-center justify-between gap-4 flex-wrap">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">KPIs</h1>
        <p class="text-sm text-muted mt-1">
          Indicadores estratégicos vinculados ao roadmap
        </p>
      </div>
      <div class="flex items-center gap-3">
        <UButton icon="i-lucide-plus" label="Novo KPI" @click="openCreate" />
      </div>
    </div>

    <!-- Summary Cards ──────────────────────────────────────────────────── -->
    <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
      <UCard :ui="{ body: 'p-4' }">
        <div class="text-sm text-muted">Total de KPIs</div>
        <div class="text-2xl font-bold text-highlighted">{{ summary.total }}</div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="text-sm text-muted">De Negócio</div>
        <div class="text-2xl font-bold text-highlighted">{{ summary.business }}</div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="text-sm text-muted">De Produto</div>
        <div class="text-2xl font-bold text-highlighted">{{ summary.product }}</div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="text-sm text-muted">Vinculados a Demandas</div>
        <div class="text-2xl font-bold text-highlighted">{{ summary.linked }}</div>
      </UCard>
    </div>

    <!-- Filters ─────────────────────────────────────────────────────────── -->
    <div class="flex items-center gap-3 flex-wrap">
      <UInput
        v-model="searchQuery"
        icon="i-lucide-search"
        placeholder="Buscar KPI..."
        class="w-64"
      />
      <USelectMenu
        v-model="filterType"
        :items="[{ value: '', label: 'Todos os tipos' }, ...kpiTypeOptions]"
        class="w-44"
      />
      <USelectMenu
        v-model="filterCategory"
        :items="[{ value: '', label: 'Todas as categorias' }, ...kpiCategoryOptions]"
        class="w-44"
      />
    </div>

    <!-- Table ───────────────────────────────────────────────────────────── -->
    <UTable
      :data="filteredKpis"
      :columns="columns"
      :loading="isLoading"
      class="w-full"
    >
      <template #name-cell="{ row }">
        <div>
          <button class="font-medium text-highlighted hover:underline text-left" @click="openEdit(row.original)">
            {{ row.original.name }}
          </button>
          <p v-if="row.original.description" :title="row.original.description" class="text-xs text-muted truncate max-w-xs mt-0.5">
            {{ row.original.description }}
          </p>
        </div>
      </template>

      <template #type-cell="{ row }">
        <UBadge :color="(kpiTypeBadgeColor[row.original.type] as any)" variant="solid" size="sm">
          {{ kpiTypeLabels[row.original.type] }}
        </UBadge>
      </template>

      <template #category-cell="{ row }">
        <UBadge :color="(kpiCategoryBadgeColor[row.original.category] as any)" variant="subtle" size="sm">
          {{ kpiCategoryLabels[row.original.category] }}
        </UBadge>
      </template>

      <template #indicator-cell="{ row }">
        <span class="text-sm text-highlighted">{{ kpiIndicatorLabels[row.original.indicator] }}</span>
      </template>

      <template #operation-cell="{ row }">
        <span class="text-sm text-muted">{{ kpiOperationLabels[row.original.operation] }}</span>
      </template>

      <template #allowedUnits-cell="{ row }">
        <span class="text-sm text-muted">{{ allowedUnitsLabel(row.original.allowedUnits) }}</span>
      </template>

      <template #linkedDemandsCount-cell="{ row }">
        <UBadge
          :color="row.original.linkedDemandsCount > 0 ? 'info' : 'neutral'"
          variant="subtle"
          size="sm"
        >
          {{ row.original.linkedDemandsCount }}
        </UBadge>
      </template>

      <template #actions-cell="{ row }">
        <div class="flex items-center gap-1 justify-end">
          <UButton
            icon="i-lucide-pencil"
            variant="ghost"
            size="xs"
            @click="openEdit(row.original)"
          />
          <UButton
            icon="i-lucide-trash-2"
            variant="ghost"
            size="xs"
            color="error"
            @click="confirmDelete(row.original)"
          />
        </div>
      </template>
    </UTable>

    <div v-if="!isLoading && !filteredKpis.length" class="text-center py-12 text-muted">
      <UIcon name="i-lucide-bar-chart-2" class="text-4xl mb-2" />
      <p>Nenhum KPI cadastrado.</p>
      <UButton label="Criar primeiro KPI" variant="soft" class="mt-3" @click="openCreate" />
    </div>

    <!-- Form Modal ──────────────────────────────────────────────────────── -->
    <UModal v-model:open="showFormModal" :ui="{ content: 'sm:max-w-5xl' }">
      <template #header>
        <h3 class="text-lg font-semibold text-highlighted">
          {{ editingKpi ? 'Editar KPI' : 'Novo KPI' }}
        </h3>
      </template>

      <template #body>
        <div class="space-y-5 p-5">
          <UFormField label="Nome" required>
            <UInput
              v-model="formData.name"
              placeholder="Ex: Taxa de churn mensal"
              class="w-full"
            />
          </UFormField>

          <div class="grid grid-cols-3 gap-4">
            <UFormField label="Tipo" required>
              <USelectMenu
                v-model="selectedTypeOption"
                :items="kpiTypeOptions"
                class="w-full"
              />
            </UFormField>
            <UFormField label="Categoria" required>
              <USelectMenu
                v-model="selectedCategoryOption"
                :items="kpiCategoryOptions"
                class="w-full"
              />
            </UFormField>
            <UFormField label="Indicador" required>
              <USelectMenu
                v-model="selectedIndicatorOption"
                :items="kpiIndicatorOptions"
                class="w-full"
              />
            </UFormField>
          </div>

          <UFormField label="Operação" required>
            <USelectMenu
              v-model="selectedOperationOption"
              :items="kpiOperationOptions"
              class="w-full"
            />
          </UFormField>

          <UFormField label="Unidades permitidas" required>
            <div class="flex flex-wrap gap-2">
              <button
                v-for="unit in kpiUnitOptions"
                :key="unit.value"
                type="button"
                class="rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors"
                :class="formData.allowedUnits.includes(unit.value)
                  ? 'border-primary/50 bg-primary/10 text-primary'
                  : 'border-default text-highlighted hover:border-primary/40'"
                @click="toggleAllowedUnit(unit.value)"
              >
                {{ unit.label }}
              </button>
            </div>
            <p class="mt-1.5 text-xs text-muted">Selecione uma ou mais unidades que o KPI aceita.</p>
          </UFormField>

          <UFormField label="Descrição">
            <UTextarea v-model="formData.description" placeholder="Descrição do indicador..." :rows="4" class="w-full" />
          </UFormField>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton label="Cancelar" variant="ghost" @click="showFormModal = false" />
          <UButton
            :label="editingKpi ? 'Salvar' : 'Criar'"
            :loading="isSubmitting"
            :disabled="submitDisabled"
            @click="submitForm"
          />
        </div>
      </template>
    </UModal>

    <!-- Delete Confirmation ─────────────────────────────────────────────── -->
    <UModal v-model:open="showDeleteConfirm">
      <template #header>
        <h3 class="text-lg font-semibold text-error">Remover KPI</h3>
      </template>
      <template #body>
        <p class="p-4 text-sm text-muted">
          Tem certeza que deseja remover o KPI <strong>{{ deletingKpi?.name }}</strong>?
          Todos os vínculos com demandas e medições serão removidos.
        </p>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton label="Cancelar" variant="ghost" @click="showDeleteConfirm = false" />
          <UButton label="Remover" color="error" @click="executeDelete" />
        </div>
      </template>
    </UModal>
  </div>
</template>
