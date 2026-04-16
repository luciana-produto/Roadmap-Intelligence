import { defineStore } from 'pinia'
import type { ApiResponse } from '~/types/api'
import type {
  Kpi,
  KpiFormData,
  DemandKpiLink,
  DemandKpiLinkInput
} from '~/types/roadmap'

export const useKpiStore = defineStore('kpi', () => {
  const api = useApi()

  const kpis = ref<Kpi[]>([])
  const isLoading = ref(false)

  async function fetchKpis(projectId: string) {
    isLoading.value = true
    try {
      const response = await api.get<ApiResponse<Kpi[]>>(`/api/kpis?projectId=${projectId}`)
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

  return {
    kpis,
    isLoading,
    fetchKpis,
    createKpi,
    updateKpi,
    deleteKpi,
    updateDemandKpiLinks
  }
})
