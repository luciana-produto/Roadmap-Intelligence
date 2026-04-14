import { defineStore } from 'pinia'
import type { SsoUser } from '~/types/auth'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<SsoUser | null>(null)
  const sessionId = ref<string | null>(null)

  const isAuthenticated = computed(() => !!user.value && !!sessionId.value)

  function setSession(newSessionId: string, newUser: SsoUser) {
    sessionId.value = newSessionId
    user.value = newUser
  }

  function restoreSession(newSessionId: string | null, newUser: SsoUser | null) {
    sessionId.value = newSessionId
    user.value = newUser
  }

  function clearSession() {
    sessionId.value = null
    user.value = null
  }

  return { user, sessionId, isAuthenticated, setSession, restoreSession, clearSession }
})
