import { useAppStore } from '~/stores/app'
import { AppConstants } from '~/utils/constants'

export const useApi = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase
  const appStore = useAppStore()
  const toast = useToast()
  const logger = useLogger('useApi')
  const { getHeader: getCorrelationHeader, get: getCorrelationId } = useCorrelationId()

  const buildHeaders = (): Record<string, string> => getCorrelationHeader()

  const handleError = (error: unknown, correlationId: string) => {
    const err = error as { data?: { title?: string, detail?: string, correlationId?: string }, status?: number }
    const detail = err?.data?.detail ?? AppConstants.MESSAGES.GENERIC_ERROR
    const serverCorrelationId = err?.data?.correlationId ?? correlationId

    logger.error('Falha em requisição HTTP', {
      status: err?.status,
      detail,
      correlationId: serverCorrelationId,
      error
    })

    toast.add({
      title: err?.data?.title ?? 'Erro',
      description: `${detail}\n\nID de rastreamento: ${serverCorrelationId}`,
      color: 'error'
    })

    appStore.setError(detail)
    throw error
  }

  const request = async <T>(
    path: string,
    options: Parameters<typeof $fetch>[1] = {}
  ): Promise<T> => {
    const correlationId = getCorrelationId()
    appStore.clearError()
    let slowAlertTriggered = false

    const slowTimer = setTimeout(() => {
      slowAlertTriggered = true
      logger.warn('Requisição lenta detectada', {
        path,
        method: options.method ?? 'GET',
        correlationId
      })
      toast.add({
        title: 'Aguarde...',
        description: AppConstants.MESSAGES.SLOW_REQUEST,
        color: 'warning',
        duration: AppConstants.SLOW_REQUEST_THRESHOLD_MS
      })
    }, AppConstants.SLOW_REQUEST_THRESHOLD_MS)

    try {
      const response = await $fetch<T>(path, {
        baseURL,
        headers: { ...buildHeaders(), ...(options.headers as Record<string, string> ?? {}) },
        ...options
      })
      if (slowAlertTriggered) {
        logger.info('Requisição lenta concluída com sucesso', {
          path,
          method: options.method ?? 'GET',
          correlationId
        })
      }
      return response
    }
    catch (error) {
      return handleError(error, correlationId) as never
    }
    finally {
      clearTimeout(slowTimer)
    }
  }

  return {
    get: <T>(path: string, options = {}) => request<T>(path, { method: 'GET', ...options }),
    post: <T>(path: string, body: Record<string, unknown>, options = {}) => request<T>(path, { method: 'POST', body, ...options }),
    put: <T>(path: string, body: Record<string, unknown>, options = {}) => request<T>(path, { method: 'PUT', body, ...options }),
    patch: <T>(path: string, body: Record<string, unknown>, options = {}) => request<T>(path, { method: 'PATCH', body, ...options }),
    del: <T>(path: string, options = {}) => request<T>(path, { method: 'DELETE', ...options })
  }
}
