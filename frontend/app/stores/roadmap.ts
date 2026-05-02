import { defineStore } from 'pinia'
import type { ApiResponse } from '~/types/api'
import type {
  RoadmapProject,
  RoadmapDemand,
  DemandDependencyOption,
  RoadmapCapacitySummary,
  CapacityFormData,
  DemandFormData,
  DemandStatus,
  RoadmapItemType
} from '~/types/roadmap'
import {
  buildCreateDemandPayload,
  buildStatusPatchPayload,
  buildUpdateDemandPayload,
  sanitizeCustomersForItem
} from '~/utils/roadmapDemandPayload'

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

  function isSameDemandScope(
    left: Pick<RoadmapDemand, 'projectId' | 'quarterYear' | 'quarterNumber'>,
    right: Pick<RoadmapDemand, 'projectId' | 'quarterYear' | 'quarterNumber'>
  ) {
    return left.projectId === right.projectId
      && left.quarterYear === right.quarterYear
      && left.quarterNumber === right.quarterNumber
  }

  function upsertDependencyOptionFromDemand(demand: RoadmapDemand) {
    if (demand.itemType === 'Roadmap')
      return

    const projectName = demand.projectId
      ? projects.value.find(project => project.id === demand.projectId)?.name ?? ''
      : ''

    const nextOption: DemandDependencyOption = {
      demandId: demand.id,
      itemType: demand.itemType,
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
    if (selectedProjectId.value && demand.projectId && demand.projectId !== selectedProjectId.value)
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

  function syncSelectedProject() {
    if (projects.value.length === 0) {
      selectedProjectId.value = null
      return
    }

    if (!selectedProjectId.value || !projects.value.some(project => project.id === selectedProjectId.value))
      selectedProjectId.value = projects.value[0].id
  }

  async function fetchProjects() {
    const res = await api.get<ApiResponse<RoadmapProject[]>>('/api/projects')
    projects.value = res.data ?? []
    syncSelectedProject()
  }

  async function createProject(payload: { name: string }): Promise<RoadmapProject> {
    const res = await api.post<ApiResponse<RoadmapProject>>(
      '/api/projects',
      {
        name: payload.name
      }
    )

    await fetchProjects()
    return res.data
  }

  async function updateProject(id: string, payload: { name: string }): Promise<RoadmapProject> {
    const res = await api.put<ApiResponse<RoadmapProject>>(
      `/api/projects/${id}`,
      {
        name: payload.name
      }
    )

    await fetchProjects()
    return res.data
  }

  async function deleteProject(id: string) {
    await api.del(`/api/projects/${id}`)
    await fetchProjects()
  }

  async function createProduct(projectId: string, payload: { name: string }): Promise<void> {
    await api.post<ApiResponse<unknown>>(
      `/api/projects/${projectId}/products`,
      { name: payload.name }
    )

    await fetchProjects()
  }

  async function updateProduct(projectId: string, productId: string, payload: { name: string }): Promise<void> {
    await api.put<ApiResponse<unknown>>(
      `/api/projects/${projectId}/products/${productId}`,
      { name: payload.name }
    )

    await fetchProjects()
  }

  async function deleteProduct(projectId: string, productId: string) {
    await api.del(`/api/projects/${projectId}/products/${productId}`)
    await fetchProjects()
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
    const body = buildCreateDemandPayload(payload)
    const res = await api.post<ApiResponse<RoadmapDemand>>(
      '/api/roadmap/demands',
      body as unknown as Record<string, unknown>
    )
    await fetchDemands()
    if (res.data)
      upsertDependencyOptionFromDemand(res.data)

    customerSuggestions.value = [...new Set([
      ...customerSuggestions.value,
      ...sanitizeCustomersForItem(payload.itemType, payload.customers)
    ])].sort((left, right) => left.localeCompare(right, 'pt-BR'))

    return res.data
  }

  async function updateDemand(id: string, payload: DemandFormData): Promise<RoadmapDemand> {
    const body = buildUpdateDemandPayload(id, payload)
    const res = await api.put<ApiResponse<RoadmapDemand>>(
      `/api/roadmap/demands/${id}`,
      body as unknown as Record<string, unknown>
    )
    await fetchDemands()

    if (res.data)
      upsertDependencyOptionFromDemand(res.data)

    customerSuggestions.value = [...new Set([
      ...customerSuggestions.value,
      ...sanitizeCustomersForItem(payload.itemType, payload.customers)
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
    const body = buildStatusPatchPayload(demand, status)
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
    createProject,
    updateProject,
    deleteProject,
    createProduct,
    updateProduct,
    deleteProduct,
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
