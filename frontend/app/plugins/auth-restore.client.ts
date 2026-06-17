// Restaura a sessão dos cookies ao iniciar o app, antes do middleware de rota,
// para que o usuário não seja deslogado ao recarregar a página (F5).
export default defineNuxtPlugin(() => {
  useAuth().restoreFromCookies()
})
