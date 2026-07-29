import type { KpiUnit, KpiIndicator, KpiOperation, ConfidenceLevel, MeasurementResult } from '~/types/roadmap'

export const KPI_INDICATOR_LABELS: Record<KpiIndicator, string> = {
  Mrr: 'MRR', Stores: 'Lojas', Time: 'Tempo', Clicks: 'Cliques', StepsScreens: 'Etapas/Telas'
}

export const CONFIDENCE_LABELS: Record<ConfidenceLevel, string> = {
  High: 'Alta', Medium: 'Média', Low: 'Baixa'
}

// Resultado calculado AO VIVO a partir da operação do KPI + meta + apurado
// (mesma regra do workspace do épico). Não usa o measurement.result armazenado.
export function liveKpiResult(operation: KpiOperation, estimated: number | null, apurado: number | null): MeasurementResult {
  if (estimated == null || apurado == null)
    return 'Neutral'
  if (operation === 'LowerIsBetter')
    return apurado <= estimated ? 'Positive' : 'Negative'
  return apurado >= estimated ? 'Positive' : 'Negative'
}

// % de atingimento da meta, orientado pela operação do KPI (>100% = superou).
export function kpiAttainmentPct(operation: KpiOperation, estimated: number | null, apurado: number | null): number | null {
  if (estimated == null || apurado == null)
    return null
  if (operation === 'LowerIsBetter') {
    if (apurado === 0)
      return estimated === 0 ? 100 : Infinity
    return (estimated / apurado) * 100
  }
  if (estimated === 0)
    return apurado === 0 ? 100 : Infinity
  return (apurado / estimated) * 100
}

export function formatKpiValue(value: number | null | undefined, unit: KpiUnit): string {
  if (value == null)
    return '—'
  if (unit === 'Currency')
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', maximumFractionDigits: 0 }).format(value)
  const formatted = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value)
  if (unit === 'Percentage')
    return `${formatted}%`
  if (unit === 'TimeSeconds')
    return `${formatted}s`
  return formatted
}

export function formatKpiAttainment(value: number | null): string {
  if (value == null || !Number.isFinite(value))
    return '—'
  return `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 }).format(value)}%`
}

export function formatKpiDate(value: string | null | undefined): string {
  if (!value)
    return '—'
  // Aceita tanto "YYYY-MM-DD" quanto ISO com hora ("YYYY-MM-DDTHH:mm:ss").
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  if (!year || !month || !day)
    return value
  return new Intl.DateTimeFormat('pt-BR').format(new Date(year, month - 1, day))
}
