import type { DemandStatus, DemandType, DemandClassification } from '~/types/roadmap'

// Seleção emitida ao clicar num item do dashboard. O pai decide o que fazer:
// na planejamento aplica o filtro na própria lista; na home abre uma nova aba filtrada.
export type DashboardSelection =
  | { kind: 'status', value: DemandStatus }
  | { kind: 'classification', value: DemandClassification }
  | { kind: 'customer', value: string }
  | { kind: 'type', value: DemandType }
  | { kind: 'problem', value: 'overdueOpen' | 'deliveredLate' | 'noJira' | 'noKpi' | 'doneNoKpi' }
  | { kind: 'inconsistentDeps' }
  | { kind: 'spilloverReason', value: string }
  | { kind: 'deprioritizationReason', value: string }
  | { kind: 'delayReason', value: string }

// Filtros ativos (para destacar o item selecionado). Opcional — na home não há destaque.
export interface DashboardActiveFilters {
  statuses?: string[]
  classifications?: string[]
  customers?: string[]
  types?: string[]
  problems?: string[]
  inconsistentDeps?: boolean
  spilloverReasons?: string[]
  deprioritizationReasons?: string[]
  delayReasons?: string[]
}
