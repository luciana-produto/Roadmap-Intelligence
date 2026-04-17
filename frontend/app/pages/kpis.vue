<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import type { Kpi, KpiFormData, KpiType, KpiLever, KpiObjective } from '~/types/roadmap'

useSeoMeta({ title: 'KPIs · ProductHub' })

const roadmapStore = useRoadmapStore()
const kpiStore = useKpiStore()
const toast = useToast()

const { projects, selectedProjectId } = storeToRefs(roadmapStore)
const { kpis, isLoading } = storeToRefs(kpiStore)

// ─── Init ────────────────────────────────────────────────────────────────────
onMounted(async () => {
  if (!projects.value.length) await roadmapStore.fetchProjects()
  if (!selectedProjectId.value && projects.value.length)
    selectedProjectId.value = projects.value[0]!.id
  if (selectedProjectId.value) await kpiStore.fetchKpis(selectedProjectId.value)
})

watch(selectedProjectId, async (id) => {
  if (id) await kpiStore.fetchKpis(id)
})

// ─── Constants ───────────────────────────────────────────────────────────────
const kpiTypeOptions: { value: KpiType, label: string }[] = [
  { value: 'Business', label: 'Negócio' },
  { value: 'Product', label: 'Produto' },
  { value: 'Quality', label: 'Qualidade' },
  { value: 'Usability', label: 'Usabilidade' }
]

const kpiLeverOptions: { value: KpiLever, label: string }[] = [
  { value: 'Growth', label: 'Crescer' },
  { value: 'Efficiency', label: 'Eficiência' },
  { value: 'Customer', label: 'Cliente' }
]

const kpiObjectiveOptions: { value: KpiObjective, label: string }[] = [
  { value: 'Increase', label: 'Aumentar' },
  { value: 'Decrease', label: 'Reduzir' }
]

const kpiTypeLabels: Record<KpiType, string> = {
  Business: 'Negócio',
  Product: 'Produto',
  Quality: 'Qualidade',
  Usability: 'Usabilidade'
}

const kpiLeverLabels: Record<KpiLever, string> = {
  Growth: 'Crescer',
  Efficiency: 'Eficiência',
  Customer: 'Cliente'
}

const kpiObjectiveLabels: Record<KpiObjective, string> = {
  Increase: 'Aumentar',
  Decrease: 'Reduzir'
}

const kpiTypeBadgeColor: Record<KpiType, string> = {
  Business: 'primary',
  Product: 'info',
  Quality: 'warning',
  Usability: 'success'
}

const kpiLeverBadgeColor: Record<KpiLever, string> = {
  Growth: 'success',
  Efficiency: 'info',
  Customer: 'warning'
}

// ─── Table columns ───────────────────────────────────────────────────────────
const columns: TableColumn<Kpi>[] = [
  { accessorKey: 'name', header: 'Nome' },
  { accessorKey: 'type', header: 'Tipo' },
  { accessorKey: 'lever', header: 'Alavanca' },
  { accessorKey: 'objective', header: 'Objetivo' },
  { accessorKey: 'target', header: 'Meta' },
  { accessorKey: 'currentValue', header: 'Valor Atual' },
  { accessorKey: 'linkedDemandsCount', header: 'Demandas' },
  { accessorKey: 'actions', header: '' }
]

// ─── Form state ──────────────────────────────────────────────────────────────
const showFormModal = ref(false)
const editingKpi = ref<Kpi | null>(null)
const formData = ref<KpiFormData>(emptyForm())

function emptyForm(): KpiFormData {
  return {
    projectId: selectedProjectId.value ?? '',
    name: '',
    type: 'Business',
    lever: 'Growth',
    objective: 'Increase',
    description: '',
    calculation: '',
    target: undefined,
    currentValue: undefined
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
    projectId: kpi.projectId,
    name: kpi.name,
    type: kpi.type,
    lever: kpi.lever,
    objective: kpi.objective,
    description: kpi.description ?? '',
    calculation: kpi.calculation ?? '',
    target: kpi.target,
    currentValue: kpi.currentValue
  }
  showFormModal.value = true
}

async function submitForm() {
  try {
    if (editingKpi.value) {
      await kpiStore.updateKpi(editingKpi.value.id, formData.value)
      toast.add({ title: 'KPI atualizado', color: 'success' })
    }
    else {
      formData.value.projectId = selectedProjectId.value ?? ''
      await kpiStore.createKpi(formData.value)
      toast.add({ title: 'KPI criado', color: 'success' })
    }
    showFormModal.value = false
  }
  catch { /* handled by useApi */ }
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
const filterLever = ref<KpiLever | ''>('')

const filteredKpis = computed(() => {
  let result = kpis.value
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    result = result.filter(k => k.name.toLowerCase().includes(q) || k.description?.toLowerCase().includes(q))
  }
  if (filterType.value) result = result.filter(k => k.type === filterType.value)
  if (filterLever.value) result = result.filter(k => k.lever === filterLever.value)
  return result
})

// ─── Summary ─────────────────────────────────────────────────────────────────
const summary = computed(() => {
  const total = kpis.value.length
  const withTarget = kpis.value.filter(k => k.target != null).length
  const onTrack = kpis.value.filter(k => k.target != null && k.currentValue != null && k.currentValue >= k.target).length
  const linked = kpis.value.filter(k => k.linkedDemandsCount > 0).length
  return { total, withTarget, onTrack, linked }
})

function formatNumber(val?: number): string {
  if (val == null) return '—'
  return val.toLocaleString('pt-BR', { maximumFractionDigits: 2 })
}

function getProgressPercent(kpi: Kpi): number | null {
  if (kpi.target == null || kpi.target === 0 || kpi.currentValue == null) return null
  return Math.min(Math.round((kpi.currentValue / kpi.target) * 100), 100)
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
        <USelectMenu
          v-model="selectedProjectId"
          :items="projects.map(p => ({ value: p.id, label: p.name }))"
          placeholder="Selecionar projeto"
          class="w-48"
        />
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
        <div class="text-sm text-muted">Com Meta</div>
        <div class="text-2xl font-bold text-highlighted">{{ summary.withTarget }}</div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="text-sm text-muted">Atingindo Meta</div>
        <div class="text-2xl font-bold text-success">{{ summary.onTrack }}</div>
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
        v-model="filterLever"
        :items="[{ value: '', label: 'Todas as alavancas' }, ...kpiLeverOptions]"
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
          <p v-if="row.original.description" class="text-xs text-muted truncate max-w-xs mt-0.5">
            {{ row.original.description }}
          </p>
        </div>
      </template>

      <template #type-cell="{ row }">
        <UBadge :color="(kpiTypeBadgeColor[row.original.type] as any)" variant="subtle" size="sm">
          {{ kpiTypeLabels[row.original.type] }}
        </UBadge>
      </template>

      <template #lever-cell="{ row }">
        <UBadge :color="(kpiLeverBadgeColor[row.original.lever] as any)" variant="subtle" size="sm">
          {{ kpiLeverLabels[row.original.lever] }}
        </UBadge>
      </template>

      <template #objective-cell="{ row }">
        <span class="text-sm text-highlighted">{{ kpiObjectiveLabels[row.original.objective] }}</span>
      </template>

      <template #target-cell="{ row }">
        <span class="text-sm">{{ formatNumber(row.original.target) }}</span>
      </template>

      <template #currentValue-cell="{ row }">
        <div class="flex items-center gap-2">
          <span class="text-sm">{{ formatNumber(row.original.currentValue) }}</span>
          <UBadge
            v-if="getProgressPercent(row.original) != null"
            :color="getProgressPercent(row.original)! >= 100 ? 'success' : getProgressPercent(row.original)! >= 70 ? 'warning' : 'error'"
            variant="subtle"
            size="xs"
          >
            {{ getProgressPercent(row.original) }}%
          </UBadge>
        </div>
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
      <p>Nenhum KPI cadastrado para este projeto.</p>
      <UButton label="Criar primeiro KPI" variant="soft" class="mt-3" @click="openCreate" />
    </div>

    <!-- Form Modal ──────────────────────────────────────────────────────── -->
    <UModal v-model:open="showFormModal">
      <template #header>
        <h3 class="text-lg font-semibold text-highlighted">
          {{ editingKpi ? 'Editar KPI' : 'Novo KPI' }}
        </h3>
      </template>

      <template #body>
        <div class="space-y-4 p-4">
          <UFormField label="Nome" required>
            <UInput v-model="formData.name" placeholder="Ex: Taxa de churn mensal" class="w-full" />
          </UFormField>

          <div class="grid grid-cols-3 gap-4">
            <UFormField label="Tipo" required>
              <USelectMenu
                v-model="formData.type"
                :items="kpiTypeOptions"
                class="w-full"
              />
            </UFormField>
            <UFormField label="Alavanca" required>
              <USelectMenu
                v-model="formData.lever"
                :items="kpiLeverOptions"
                class="w-full"
              />
            </UFormField>
            <UFormField label="Objetivo" required>
              <USelectMenu
                v-model="formData.objective"
                :items="kpiObjectiveOptions"
                class="w-full"
              />
            </UFormField>
          </div>

          <UFormField label="Descrição">
            <UTextarea v-model="formData.description" placeholder="Descrição do indicador..." :rows="2" class="w-full" />
          </UFormField>

          <UFormField label="Como calcular?">
            <UTextarea v-model="formData.calculation" placeholder="Fórmula ou método de cálculo..." :rows="2" class="w-full" />
          </UFormField>

          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Meta">
              <UInput v-model.number="formData.target" type="number" step="0.01" placeholder="0" class="w-full" />
            </UFormField>
            <UFormField label="Valor Atual">
              <UInput v-model.number="formData.currentValue" type="number" step="0.01" placeholder="0" class="w-full" />
            </UFormField>
          </div>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton label="Cancelar" variant="ghost" @click="showFormModal = false" />
          <UButton
            :label="editingKpi ? 'Salvar' : 'Criar'"
            :disabled="!formData.name"
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
