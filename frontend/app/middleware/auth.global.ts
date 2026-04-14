export default defineNuxtRouteMiddleware((to) => {
  const config = useRuntimeConfig()
  if (config.public.bypassAuth === 'true') return

  const publicRoutes = ['/login', '/auth/callback']
  const authStore = useAuthStore()

  if (!authStore.isAuthenticated && !publicRoutes.includes(to.path)) {
    return navigateTo('/login')
  }

  if (authStore.isAuthenticated && to.path === '/login') {
    return navigateTo('/home')
  }
})
