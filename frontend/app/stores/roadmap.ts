import { defineStore } from 'pinia'
import type { ApiResponse } from '~/types/api'
import type {
  RoadmapProject,
  RoadmapDemand,
  DemandDependencyOption,
  RoadmapCapacitySummary,
  CapacityFormData,
  DemandFormData,
  DemandStatus
} from '~/types/roadmap'

export const useRoadmapStore = defineStore('roadmap', () => {
  const api = useApi()

  const projects = ref<RoadmapProject[]>([])
  const demands = ref<RoadmapDemand[]>([])
  const dependencyOptions = ref<DemandDependencyOption[]>([])
  const customerSuggestions = ref<string[]>([])
  const capacitySummary = ref<RoadmapCapacitySummary | null>(null)
  const isLoading = ref(false)
  const isCapacityLoading = ref(false)
  const selectedProjectId = ref<string | null>(null)
  const selectedQuarterYear = ref<number | null>(null)
  const selectedQuarterNumber = ref<number | null>(null)

  const selectedProject = computed(() =>
    projects.value.find(p => p.id === selectedProjectId.value) ?? null
  )

  function normalizeCustomers(customers?: string[]) {
    return [...new Set((customers ?? []).map(customer => customer.trim()).filter(Boolean))]
  }

  function isSameDemandScope(
    left: Pick<RoadmapDemand, 'projectId' | 'quarterYear' | 'quarterNumber'>,
    right: Pick<RoadmapDemand, 'projectId' | 'quarterYear' | 'quarterNumber'>
  ) {
    return left.projectId === right.projectId
      && left.quarterYear === right.quarterYear
      && left.quarterNumber === right.quarterNumber
  }

  function upsertDependencyOptionFromDemand(demand: RoadmapDemand) {
    const projectName = projects.value.find(project => project.id === demand.projectId)?.name ?? ''
    const nextOption: DemandDependencyOption = {
      demandId: demand.id,
      projectId: demand.projectId,
      projectName,
      title: demand.title,
      quarterLabel: demand.quarterLabel,
      status: demand.status
    }

    const existingIndex = dependencyOptions.value.findIndex(option => option.demandId === demand.id)
    if (existingIndex >= 0) {
      dependencyOptions.value.splice(existingIndex, 1, nextOption)
      return
    }

    dependencyOptions.value.push(nextOption)
  }

  function removeDependencyOption(demandId: string) {
    dependencyOptions.value = dependencyOptions.value.filter(option => option.demandId !== demandId)
  }

  function applyReorderedDemandState(id: string, status: DemandStatus, orderedDemandIds: string[]) {
    const movedDemand = demands.value.find(demand => demand.id === id)
    if (!movedDemand) return

    const sortOrderById = new Map(orderedDemandIds.map((demandId, index) => [demandId, (index + 1) * 10]))

    demands.value = demands.value.map(demand => {
      if (!isSameDemandScope(demand, movedDemand))
        return demand

      const nextSortOrder = sortOrderById.get(demand.id)
      if (!nextSortOrder)
        return demand

      const updatedDemand = {
        ...demand,
        status: demand.id === id ? status : demand.status,
        sortOrder: nextSortOrder
      }

      upsertDependencyOptionFromDemand(updatedDemand)
      return updatedDemand
    })
  }

  function isDemandVisibleInCurrentStoreScope(demand: Pick<RoadmapDemand, 'projectId' | 'quarterYear' | 'quarterNumber'>) {
    if (selectedProjectId.value && demand.projectId !== selectedProjectId.value)
      return false

    if (selectedQuarterYear.value != null && demand.quarterYear !== selectedQuarterYear.value)
      return false

    if (selectedQuarterNumber.value != null && demand.quarterNumber !== selectedQuarterNumber.value)
      return false

    return true
  }

  function applyUpdatedDemandState(updatedDemand: RoadmapDemand) {
    const existingIndex = demands.value.findIndex(demand => demand.id === updatedDemand.id)
    const isVisible = isDemandVisibleInCurrentStoreScope(updatedDemand)

    if (!isVisible) {
      if (existingIndex >= 0)
        demands.value.splice(existingIndex, 1)

      upsertDependencyOptionFromDemand(updatedDemand)
      return
    }

    if (existingIndex >= 0) {
      demands.value.splice(existingIndex, 1, updatedDemand)
      upsertDependencyOptionFromDemand(updatedDemand)
      return
    }

    demands.value.push(updatedDemand)
    upsertDependencyOptionFromDemand(updatedDemand)
  }

  async function fetchProjects() {
    const res = await api.get<ApiResponse<RoadmapProject[]>>('/api/projects')
    projects.value = res.data ?? []
    if (projects.value.length > 0 && !selectedProjectId.value)
      selectedProjectId.value = projects.value[0].id
  }

  async function fetchDemands() {
    if (!selectedProjectId.value) return
    isLoading.value = true
    try {
      const params = new URLSearchParams({ projectId: selectedProjectId.value })
      if (selectedQuarterYear.value) params.set('quarterYear', String(selectedQuarterYear.value))
      if (selectedQuarterNumber.value) params.set('quarterNumber', String(selectedQuarterNumber.value))
      const res = await api.get<ApiResponse<RoadmapDemand[]>>(`/api/roadmap/demands?${params}`)
      demands.value = res.data ?? []
    }
    finally {
      isLoading.value = false
    }
  }

  async function fetchDependencyOptions() {
    const res = await api.get<ApiResponse<DemandDependencyOption[]>>('/api/roadmap/demands/dependency-options')
    dependencyOptions.value = res.data ?? []
  }

  async function fetchCustomerSuggestions() {
    const res = await api.get<ApiResponse<string[]>>('/api/roadmap/demands/customer-suggestions')
    customerSuggestions.value = res.data ?? []
  }

  async function fetchCapacity(projectId: string, quarterYear: number, quarterNumber: number) {
    isCapacityLoading.value = true
    try {
      const params = new URLSearchParams({
        projectId,
        quarterYear: String(quarterYear),
        quarterNumber: String(quarterNumber)
      })
      const res = await api.get<ApiResponse<RoadmapCapacitySummary>>(`/api/roadmap/capacity?${params}`)
      capacitySummary.value = res.data
    }
    finally {
      isCapacityLoading.value = false
    }
  }

  async function upsertCapacity(payload: CapacityFormData) {
    const body = {
      projectId: payload.projectId,
      quarterYear: payload.quarterYear,
      quarterNumber: payload.quarterNumber,
      capacityHours: payload.capacityHours,
      observation: payload.observation || undefined
    }

    const res = await api.put<ApiResponse<RoadmapCapacitySummary>>(
      '/api/roadmap/capacity',
      body as unknown as Record<string, unknown>
    )

    capacitySummary.value = res.data
    return res.data
  }

  function clearCapacity() {
    capacitySummary.value = null
  }

  async function createDemand(payload: DemandFormData): Promise<RoadmapDemand> {
    const body = {
      title: payload.title,
      description: payload.description || undefined,
      projectId: payload.projectId,
      quarterYear: payload.quarterYear,
      quarterNumber: payload.quarterNumber,
      type: payload.type,
      classification: payload.classification,
      productIds: payload.productIds,
      dependencyDemandIds: payload.dependencyDemandIds ?? [],
      jiraIssue: payload.jiraIssue || undefined,
      hours: payload.hours ?? undefined,
      customers: normalizeCustomers(payload.customers),
      isBlocked: payload.isBlocked ?? false,
      blockedReason: payload.blockedReason || undefined
    }
    const res = await api.post<ApiResponse<RoadmapDemand>>(
      '/api/roadmap/demands',
      body as unknown as Record<string, unknown>
    )
    await fetchDemands()
    if (res.data)
      upsertDependencyOptionFromDemand(res.data)

    customerSuggestions.value = [...new Set([
      ...customerSuggestions.value,
      ...normalizeCustomers(payload.customers)
    ])].sort((left, right) => left.localeCompare(right, 'pt-BR'))

    return res.data
  }

  async function updateDemand(id: string, payload: DemandFormData): Promise<RoadmapDemand> {
    const body = {
      id,
      title: payload.title,
      description: payload.description || undefined,
      projectId: payload.projectId,
      quarterYear: payload.quarterYear,
      quarterNumber: payload.quarterNumber,
      status: payload.status ?? 'Backlog',
      type: payload.type,
      classification: payload.classification,
      productIds: payload.productIds,
      dependencyDemandIds: payload.dependencyDemandIds ?? [],
      observation: payload.observation || undefined,
      jiraIssue: payload.jiraIssue || undefined,
      hours: payload.hours ?? undefined,
      customers: normalizeCustomers(payload.customers),
      isBlocked: payload.isBlocked ?? false,
      blockedReason: payload.blockedReason || undefined,
      deliveryDate: payload.deliveryDate || undefined
    }
    const res = await api.put<ApiResponse<RoadmapDemand>>(
      `/api/roadmap/demands/${id}`,
      body as unknown as Record<string, unknown>
    )
    if (res.data)
      applyUpdatedDemandState(res.data)

    customerSuggestions.value = [...new Set([
      ...customerSuggestions.value,
      ...normalizeCustomers(payload.customers)
    ])].sort((left, right) => left.localeCompare(right, 'pt-BR'))

    return res.data
  }

  async function deleteDemand(id: string) {
    await api.del(`/api/roadmap/demands/${id}`)
    await fetchDemands()
    removeDependencyOption(id)
  }

  async function reorderDemand(id: string, status: DemandStatus, orderedDemandIds: string[]) {
    await api.put<ApiResponse<null>>(
      '/api/roadmap/demands/reorder',
      {
        demandId: id,
        status,
        orderedDemandIds
      }
    )
    applyReorderedDemandState(id, status, orderedDemandIds)
  }

  async function patchDemandStatus(id: string, status: DemandStatus) {
    const demand = demands.value.find(d => d.id === id)
    if (!demand) return
    const body = {
      id,
      title: demand.title,
      description: demand.description || undefined,
      projectId: demand.projectId,
      quarterYear: demand.quarterYear,
      quarterNumber: demand.quarterNumber,
      status,
      type: demand.type,
      classification: demand.classification,
      productIds: demand.products.map(p => p.productId),
      observation: demand.observation || undefined,
      jiraIssue: demand.jiraIssue || undefined,
      hours: demand.hours ?? undefined,
      customers: normalizeCustomers(demand.customers),
      isBlocked: demand.isBlocked ?? false,
      blockedReason: demand.blockedReason || undefined,
      deliveryDate: demand.deliveryDate || undefined
    }
    await api.put<ApiResponse<RoadmapDemand>>(
      `/api/roadmap/demands/${id}`,
      body as unknown as Record<string, unknown>
    )
    await fetchDemands()
  }

  function selectProject(id: string) {
    selectedProjectId.value = id
    fetchDemands()
  }

  function selectQuarter(year: number | null, number: number | null) {
    selectedQuarterYear.value = year
    selectedQuarterNumber.value = number
    fetchDemands()
  }

  return {
    projects,
    demands,
    dependencyOptions,
    customerSuggestions,
    capacitySummary,
    isLoading,
    isCapacityLoading,
    selectedProjectId,
    selectedQuarterYear,
    selectedQuarterNumber,
    selectedProject,
    fetchProjects,
    fetchDemands,
    fetchDependencyOptions,
    fetchCustomerSuggestions,
    fetchCapacity,
    upsertCapacity,
    clearCapacity,
    createDemand,
    updateDemand,
    deleteDemand,
    reorderDemand,
    patchDemandStatus,
    selectProject,
    selectQuarter
  }
})
