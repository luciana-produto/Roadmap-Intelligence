import type { DashboardSelection } from '~/types/roadmapDashboards'

// Monta a URL da visão planejamento com os filtros de time/quarter + o item de dashboard clicado.
// Esquema: /roadmap?teams=<ids>&quarters=<vals>&dfk=<kind>&dfv=<value>
export function buildPlanningDashboardUrl(opts: {
  teams: string[]
  quarters: string[]
  selection: DashboardSelection
}): string {
  const params = new URLSearchParams()
  // Sempre enviamos teams/quarters (mesmo vazios = "todos") para a nova aba espelhar
  // exatamente o escopo selecionado na home, sem cair no cache da planejamento.
  params.set('teams', opts.teams.join(','))
  params.set('quarters', opts.quarters.join(','))
  params.set('dfk', opts.selection.kind)
  if ('value' in opts.selection && opts.selection.value != null)
    params.set('dfv', String(opts.selection.value))
  return `/roadmap?${params.toString()}`
}

// Monta a URL do relatório detalhado de motivos (transbordo/despriorização/atraso),
// levando o escopo de Time/Quarter selecionado no dashboard.
export function buildReasonReportUrl(opts: {
  tipo: 'atraso-transbordo' | 'deprioritization'
  teams: string[]
  quarters: string[]
}): string {
  const params = new URLSearchParams()
  params.set('tipo', opts.tipo)
  params.set('teams', opts.teams.join(','))
  params.set('quarters', opts.quarters.join(','))
  return `/relatorios/motivos?${params.toString()}`
}
