export default defineNuxtRouteMiddleware(async (to) => {
  const config = useRuntimeConfig()
  const authStore = useAuthStore()
  const access = useAccessStore()

  // Modo dev sem SSO: libera tudo.
  if (config.public.bypassAuth === 'true') {
    if (!access.loaded) access.grantAll()
    return
  }

  const publicRoutes = ['/login', '/auth/callback']

  if (!authStore.isAuthenticated && !publicRoutes.includes(to.path)) {
    return navigateTo('/login')
  }

  if (authStore.isAuthenticated && to.path === '/login') {
    return navigateTo('/home')
  }

  if (!authStore.isAuthenticated)
    return

  // Garante que as permissões do usuário estão carregadas antes de guardar as rotas.
  if (!access.loaded)
    await access.fetchAccess()

  // Cadastros (times/produtos e KPIs) — exige permissão de Cadastros.
  const registrationsRoutes = ['/products', '/kpis']
  if (registrationsRoutes.some(p => to.path === p || to.path.startsWith(`${p}/`)) && !access.canManageRegistrations) {
    return navigateTo('/home')
  }

  // Gestão de acessos — exige super-admin.
  if ((to.path === '/acessos' || to.path.startsWith('/acessos/')) && !access.canManageAccess) {
    return navigateTo('/home')
  }

  // Lixeira — exige permissão de edição do roadmap.
  if ((to.path === '/lixeira' || to.path.startsWith('/lixeira/')) && !access.canEditRoadmap) {
    return navigateTo('/home')
  }
})
