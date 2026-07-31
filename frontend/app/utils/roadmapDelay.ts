// Lista de motivos compartilhada entre Transbordo e Atraso (mesma lista, conforme regra de negócio).
export const SPILLOVER_REASON_OPTIONS = [
  { value: 'ScopeChange', label: 'Mudança de escopo' },
  { value: 'PriorityChangeNoTradeOff', label: 'Mudança de prioridade (sem trade-off)' },
  { value: 'ExternalDependency', label: 'Dependência externa' },
  { value: 'TechnicalBlock', label: 'Impedimento técnico' },
  { value: 'IncorrectEstimate', label: 'Estimativa incorreta' },
  { value: 'InsufficientCapacity', label: 'Capacidade insuficiente' },
  { value: 'QualityIssues', label: 'Problemas de qualidade' }
] as const

const REASON_LABEL_BY_VALUE = new Map(SPILLOVER_REASON_OPTIONS.map(o => [o.value, o.label]))

export function delayReasonLabel(value?: string | null): string {
  return value ? (REASON_LABEL_BY_VALUE.get(value as (typeof SPILLOVER_REASON_OPTIONS)[number]['value']) ?? value) : ''
}

// Último dia do quarter (YYYY-MM-DD). Backlog/quarter inválido → null.
export function quarterEndDate(quarterYear: number, quarterNumber: number): string | null {
  if (quarterYear <= 0 || quarterNumber <= 0) return null
  const month = quarterNumber * 3 // Q1→3, Q2→6, Q3→9, Q4→12
  const lastDay = new Date(quarterYear, month, 0).getDate() // dia 0 do mês seguinte = último dia de `month`
  return `${quarterYear}-${String(month).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`
}

// Regra de atraso (espelha o backend): entrega após a data prometida — ou, sem data prometida,
// após o último dia do quarter. Datas em 'YYYY-MM-DD' comparam cronologicamente como string.
export function isDeliveryLate(
  deliveryDate: string | null | undefined,
  promisedDate: string | null | undefined,
  quarterYear: number,
  quarterNumber: number
): boolean {
  const delivery = (deliveryDate ?? '').trim()
  if (!delivery) return false
  const effective = (promisedDate ?? '').trim() || quarterEndDate(quarterYear, quarterNumber)
  if (!effective) return false
  return delivery > effective
}
