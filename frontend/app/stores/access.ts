import { defineStore } from 'pinia'
import type { ApiResponse } from '~/types/api'

interface EffectiveAccess {
  canEditRoadmap: boolean
  canManageRegistrations: boolean
  canManageAccess: boolean
}

export const useAccessStore = defineStore('access', () => {
  const canEditRoadmap = ref(false)
  const canManageRegistrations = ref(false)
  const canManageAccess = ref(false)
  const loaded = ref(false)

  async function fetchAccess() {
    const api = useApi()
    try {
      const res = await api.get<ApiResponse<EffectiveAccess>>('/api/access/me')
      const data = res.data
      canEditRoadmap.value = !!data?.canEditRoadmap
      canManageRegistrations.value = !!data?.canManageRegistrations
      canManageAccess.value = !!data?.canManageAccess
    }
    catch {
      // Em falha, mantém tudo bloqueado (somente leitura). Erro já é notificado pelo useApi.
      canEditRoadmap.value = false
      canManageRegistrations.value = false
      canManageAccess.value = false
    }
    finally {
      loaded.value = true
    }
  }

  // Modo bypassAuth (dev sem SSO): libera tudo para não travar o desenvolvimento.
  function grantAll() {
    canEditRoadmap.value = true
    canManageRegistrations.value = true
    canManageAccess.value = true
    loaded.value = true
  }

  function reset() {
    canEditRoadmap.value = false
    canManageRegistrations.value = false
    canManageAccess.value = false
    loaded.value = false
  }

  return { canEditRoadmap, canManageRegistrations, canManageAccess, loaded, fetchAccess, grantAll, reset }
})
