import { defineStore } from 'pinia'
import type { ApiResponse } from '~/types/api'
import type {
  Kpi,
  KpiFormData,
  DemandKpiLink,
  DemandKpiLinkInput,
  KpiMeasurement,
  CreateDemandKpiMeasurementInput,
  UpdateDemandKpiMeasurementInput
} from '~/types/roadmap'

export const useKpiStore = defineStore('kpi', () => {
  const api = useApi()

  const kpis = ref<Kpi[]>([])
  const isLoading = ref(false)

  async function fetchKpis() {
    isLoading.value = true
    try {
      const response = await api.get<ApiResponse<Kpi[]>>('/api/kpis')
      kpis.value = response.data ?? []
    }
    finally {
      isLoading.value = false
    }
  }

  async function createKpi(data: KpiFormData): Promise<Kpi> {
    const response = await api.post<ApiResponse<Kpi>>('/api/kpis', data as unknown as Record<string, unknown>)
    const kpi = response.data!
    kpis.value.push(kpi)
    return kpi
  }

  async function updateKpi(id: string, data: KpiFormData): Promise<Kpi> {
    const response = await api.put<ApiResponse<Kpi>>(`/api/kpis/${id}`, data as unknown as Record<string, unknown>)
    const kpi = response.data!
    const index = kpis.value.findIndex(k => k.id === id)
    if (index >= 0) kpis.value[index] = kpi
    return kpi
  }

  async function deleteKpi(id: string) {
    await api.del(`/api/kpis/${id}`)
    kpis.value = kpis.value.filter(k => k.id !== id)
  }

  async function updateDemandKpiLinks(demandId: string, links: DemandKpiLinkInput[]): Promise<DemandKpiLink[]> {
    const response = await api.put<ApiResponse<DemandKpiLink[]>>(
      `/api/kpis/demands/${demandId}/links`,
      { demandId, links } as unknown as Record<string, unknown>
    )
    return response.data ?? []
  }

  async function fetchDemandKpiMeasurements(demandId: string): Promise<KpiMeasurement[]> {
    const response = await api.get<ApiResponse<KpiMeasurement[]>>(`/api/kpis/demands/${demandId}/measurements`)
    return response.data ?? []
  }

  async function createDemandKpiMeasurement(demandId: string, data: CreateDemandKpiMeasurementInput): Promise<KpiMeasurement> {
    const response = await api.post<ApiResponse<KpiMeasurement>>(
      `/api/kpis/demands/${demandId}/measurements`,
      data as unknown as Record<string, unknown>
    )
    return response.data!
  }

  async function updateDemandKpiMeasurement(id: string, data: UpdateDemandKpiMeasurementInput): Promise<KpiMeasurement> {
    const response = await api.put<ApiResponse<KpiMeasurement>>(
      `/api/kpis/measurements/${id}`,
      data as unknown as Record<string, unknown>
    )
    return response.data!
  }

  async function deleteDemandKpiMeasurement(id: string) {
    await api.del(`/api/kpis/measurements/${id}`)
  }

  return {
    kpis,
    isLoading,
    fetchKpis,
    createKpi,
    updateKpi,
    deleteKpi,
    updateDemandKpiLinks,
    fetchDemandKpiMeasurements,
    createDemandKpiMeasurement,
    updateDemandKpiMeasurement,
    deleteDemandKpiMeasurement
  }
})
