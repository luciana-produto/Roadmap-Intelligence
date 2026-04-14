import { AppConstants } from '~/utils/constants'

export const useCorrelationId = () => {
  const STORAGE_KEY = 'producthub-cid'

  const generate = (): string =>
    typeof crypto !== 'undefined'
      ? crypto.randomUUID().replace(/-/g, '')
      : Math.random().toString(36).substring(2, 18)

  const get = (): string => {
    if (import.meta.server) return generate()

    let id = sessionStorage.getItem(STORAGE_KEY)
    if (!id) {
      id = generate()
      sessionStorage.setItem(STORAGE_KEY, id)
    }
    return id
  }

  const refresh = (): string => {
    const id = generate()
    if (import.meta.client)
      sessionStorage.setItem(STORAGE_KEY, id)
    return id
  }

  const getHeader = (): Record<string, string> => ({
    [AppConstants.CORRELATION_ID_HEADER]: get()
  })

  return { get, refresh, getHeader }
}
