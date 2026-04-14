import type { SsoUser } from '~/types/auth'

interface SsoConfig {
  ssoBaseUrl: string
  ssoTenantKey: string
  ssoClientId: string
}

interface AuthDeps {
  ssoConfig?: SsoConfig
  routerPush?: (path: string) => Promise<unknown>
  toastAdd?: (options: Record<string, unknown>) => void
  logger?: Pick<ReturnType<typeof useLogger>, 'info' | 'warn' | 'error'>
  store?: ReturnType<typeof useAuthStore>
}

export function useAuth(deps: AuthDeps = {}) {
  const authStore = deps.store ?? useAuthStore()
  const router = deps.routerPush ? null : useRouter()
  const logger = deps.logger ?? useLogger('useAuth')
  const toast = deps.toastAdd ? null : useToast()

  let ssoBaseUrl: string
  let tenantKey: string
  let clientId: string

  if (deps.ssoConfig) {
    ssoBaseUrl = deps.ssoConfig.ssoBaseUrl
    tenantKey = deps.ssoConfig.ssoTenantKey
    clientId = deps.ssoConfig.ssoClientId
  }
  else {
    const config = useRuntimeConfig()
    ssoBaseUrl = config.public.ssoBaseUrl as string
    tenantKey = config.public.ssoTenantKey as string
    clientId = config.public.ssoClientId as string
  }

  const push = deps.routerPush ?? ((path: string) => router!.push(path))
  const addToast = deps.toastAdd ?? ((options: Record<string, unknown>) => toast!.add(options as Parameters<NonNullable<typeof toast>['add']>[0]))

  const COOKIE_OPTS = { maxAge: 60 * 60 * 8, sameSite: 'strict' as const, path: '/' }
  const sessionCookie = useCookie<string | null>('producthub_session', COOKIE_OPTS)
  const userCookie = useCookie<SsoUser | null>('producthub_user', COOKIE_OPTS)

  function persistSession(newSessionId: string, user: SsoUser) {
    sessionCookie.value = newSessionId
    userCookie.value = user
  }

  function clearSessionCookies() {
    sessionCookie.value = null
    userCookie.value = null
  }

  function buildLoginUrl(callbackUrl: string): string {
    const params = new URLSearchParams({
      clientId,
      'returnUrl': callbackUrl,
      'return-method': 'GET'
    })
    return `${ssoBaseUrl}/api/auth/${tenantKey}?${params.toString()}`
  }

  function redirectToSso() {
    const callbackUrl = `${window.location.origin}/auth/callback`
    logger.info('Iniciando fluxo SSO', { tenant: tenantKey, callbackUrl })
    window.location.href = buildLoginUrl(callbackUrl)
  }

  async function handleCallback(sessionId: string): Promise<boolean> {
    try {
      logger.info('Processando callback SSO', { sessionId })

      const user = await $fetch<SsoUser>(
        `${ssoBaseUrl}/api/auth/${tenantKey}/user/${sessionId}`
      )

      authStore.setSession(sessionId, user)
      persistSession(sessionId, user)
      logger.info('Usuário autenticado com sucesso', { userId: user.id, email: user.email })

      return true
    }
    catch (error) {
      logger.error('Falha ao processar callback SSO', { error })
      addToast({
        title: 'Erro de autenticação',
        description: 'Não foi possível completar o login. Tente novamente.',
        color: 'error'
      })
      return false
    }
  }

  async function logout() {
    const currentSessionId = authStore.sessionId

    try {
      if (currentSessionId) {
        await $fetch(
          `${ssoBaseUrl}/api/auth/logout/${tenantKey}?sessionId=${currentSessionId}`
        )
      }
    }
    catch (error) {
      logger.warn('Falha ao notificar logout no SSO', { error })
    }
    finally {
      authStore.clearSession()
      clearSessionCookies()
      logger.info('Sessão encerrada')
      await push('/login')
    }
  }

  return { redirectToSso, handleCallback, logout, persistSession }
}
