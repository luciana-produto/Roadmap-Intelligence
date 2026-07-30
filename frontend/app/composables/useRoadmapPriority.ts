import type { Ref } from 'vue'
import type { RoadmapDemand, Kpi, ConfidenceLevel, NoKpiClassification, DemandStatus } from '~/types/roadmap'
import { KPI_INDICATOR_LABELS, CONFIDENCE_LABELS, formatKpiValue } from '~/utils/kpiApuracao'
import { formatQuarterLabel, isBacklogQuarter, isPrioritizedBacklogQuarter, BACKLOG_QUARTER } from '~/utils/roadmapQuarter'

// ─────────────────────────────────────────────────────────────────────────────
// Fonte ÚNICA das regras de priorização (score, esforço, quarter e matriz).
// A Lista e a Matriz visual consomem os mesmos `rows` — qualquer mudança de regra
// aqui reflete automaticamente nas duas visões.
// ─────────────────────────────────────────────────────────────────────────────

export type MatrixKind = 'ganhos' | 'apostas' | 'quando' | 'evitar' | 'tecnico' | 'mandatorio'
export type MatrixInfo = { label: string, tone: string, rank: number, desc: string }

export type KpiCell = {
  key: string
  indicatorLabel: string
  isBusiness: boolean
  metaText: string
  metaTooltip: string
  typeTooltip: string
  confLabel: string
  confTooltip: string
  score: number
}

export type PriorityRow = {
  epicId: string
  epicTitle: string
  status: DemandStatus
  statusLabel: string
  quarterLabel: string
  quarterOrder: number
  promisedDate: string | null
  effort: number
  score: number | null
  scoreTooltip: string
  confidence: ConfidenceLevel | null
  matrixInfo: MatrixInfo | null
  matrixKind: MatrixKind | null
  matrixRank: number
  kpis: KpiCell[]
  kpiFallbackText: string
  isPendente: boolean
}

export const NO_KPI_CLASS_LABELS: Record<NoKpiClassification, string> = {
  Relationship: 'Relacionamento', Mandatory: 'Mandatório', Technical: 'Técnico'
}

export const statusLabels: Record<DemandStatus, string> = {
  Backlog: 'Backlog', InProgress: 'Doing', Done: 'Concluído', Deprioritized: 'Despriorizado',
  Blocked: 'Impedido', Spillover: 'Transbordo', UX: 'UX', Prioritized: 'Priorizado'
}

export const statusTone: Record<DemandStatus, string> = {
  Backlog: 'bg-neutral-100 text-neutral-600 dark:bg-neutral-800 dark:text-neutral-300',
  InProgress: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300',
  Done: 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300',
  Deprioritized: 'bg-pink-100 text-pink-700 dark:bg-pink-900/30 dark:text-pink-300',
  Blocked: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300',
  Spillover: 'bg-orange-100 text-orange-700 dark:bg-orange-900/30 dark:text-orange-300',
  UX: 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-300',
  Prioritized: 'bg-cyan-100 text-cyan-700 dark:bg-cyan-900/30 dark:text-cyan-300'
}

export const confDot: Record<ConfidenceLevel, string> = {
  High: 'bg-green-500 dark:bg-green-400',
  Medium: 'bg-amber-500 dark:bg-amber-400',
  Low: 'bg-red-500 dark:bg-red-400'
}

export const matrixMeta: Record<MatrixKind, MatrixInfo> = {
  ganhos: { label: '1. Ganhos rápidos', rank: 1, tone: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-300', desc: 'Quick Wins — alto impacto e baixo esforço → fazer primeiro.' },
  apostas: { label: '2. Grandes apostas', rank: 2, tone: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300', desc: 'Big Bets — alto impacto e alto esforço → iniciativas estratégicas.' },
  quando: { label: '3. Quando possível', rank: 3, tone: 'bg-slate-100 text-slate-700 dark:bg-slate-800/50 dark:text-slate-300', desc: 'Fill-ins — baixo impacto e baixo esforço → fazer quando houver capacidade.' },
  evitar: { label: '4. Evitar', rank: 4, tone: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300', desc: 'Money Pit — baixo impacto e alto esforço → evitar ou desafiar a necessidade.' },
  mandatorio: { label: 'Mandatório', rank: 5, tone: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300', desc: 'Não compete com a matriz. Priorizar conforme prazo da obrigatoriedade.' },
  tecnico: { label: 'Técnico', rank: 6, tone: 'bg-violet-100 text-violet-700 dark:bg-violet-900/30 dark:text-violet-300', desc: 'Não compete com a matriz. Priorizar conforme % do capacity estabelecido.' }
}

export function fmtScore(n: number): string {
  return n.toLocaleString('pt-BR', { maximumFractionDigits: 1 })
}

function median(nums: number[]): number {
  if (!nums.length) return 0
  const s = [...nums].sort((a, b) => a - b)
  const mid = Math.floor(s.length / 2)
  return s.length % 2 ? s[mid]! : (s[mid - 1]! + s[mid]!) / 2
}

export function useRoadmapPriority(
  demands: Ref<RoadmapDemand[]>,
  allDemands: Ref<RoadmapDemand[]>,
  kpis: Ref<Kpi[]>
) {
  const confWeight: Record<ConfidenceLevel, number> = { High: 3, Medium: 2, Low: 1 }
  const kpiById = computed(() => new Map(kpis.value.map(k => [k.id, k] as const)))

  // Épicos em escopo (por Time/Quarter já filtrado em `demands`).
  const scopedEpics = computed(() => {
    const ids = new Set<string>()
    for (const d of demands.value) {
      if (d.itemType === 'Epic') ids.add(d.id)
      else if (d.epicId) ids.add(d.epicId)
    }
    return allDemands.value.filter(e => e.itemType === 'Epic' && ids.has(e.id))
  })

  const childDemandsByEpic = computed(() => {
    const map = new Map<string, RoadmapDemand[]>()
    for (const d of allDemands.value) {
      if (d.itemType === 'Demand' && d.epicId) {
        const arr = map.get(d.epicId) ?? []
        arr.push(d)
        map.set(d.epicId, arr)
      }
    }
    return map
  })

  function epicEffort(epic: RoadmapDemand): number {
    if (epic.isSimple) return epic.hours ?? 0
    return (childDemandsByEpic.value.get(epic.id) ?? []).reduce((sum, d) => sum + (d.hours ?? 0), 0)
  }

  // Escala de "avanço" do quarter, do mais antigo ao mais avançado:
  // quarters reais (cronológica) < Backlog Prioritário < Backlog.
  function quarterRank(year: number, numberQ: number): number {
    if (isBacklogQuarter(year, numberQ)) return Number.MAX_SAFE_INTEGER
    if (isPrioritizedBacklogQuarter(year, numberQ)) return Number.MAX_SAFE_INTEGER - 1
    return year * 4 + numberQ
  }

  // Intervalo de quarters do épico: início = demanda mais antiga; fim = demanda mais avançada.
  // Rótulo colapsa quando início = fim; ordena-se pelo início. Composto sem demandas → Backlog.
  function epicQuarter(epic: RoadmapDemand): { label: string, order: number } {
    let startY: number, startN: number, endY: number, endN: number
    if (epic.isSimple) {
      startY = endY = epic.quarterYear
      startN = endN = epic.quarterNumber
    } else {
      const children = childDemandsByEpic.value.get(epic.id) ?? []
      if (children.length) {
        let earliest = children[0]!, latest = children[0]!
        for (const c of children) {
          if (quarterRank(c.quarterYear, c.quarterNumber) < quarterRank(earliest.quarterYear, earliest.quarterNumber)) earliest = c
          if (quarterRank(c.quarterYear, c.quarterNumber) > quarterRank(latest.quarterYear, latest.quarterNumber)) latest = c
        }
        startY = earliest.quarterYear; startN = earliest.quarterNumber
        endY = latest.quarterYear; endN = latest.quarterNumber
      } else {
        startY = endY = BACKLOG_QUARTER.year
        startN = endN = BACKLOG_QUARTER.number
      }
    }
    const startLabel = formatQuarterLabel(startY, startN)
    const endLabel = formatQuarterLabel(endY, endN)
    return {
      label: startLabel === endLabel ? startLabel : `${startLabel} → ${endLabel}`,
      order: quarterRank(startY, startN)
    }
  }

  function typeWeightOf(kpiId: string): number {
    return kpiById.value.get(kpiId)?.type === 'Business' ? 2 : 1
  }

  // Maior impacto estimado por KPI, considerando apenas os épicos EM ESCOPO — a régua
  // acompanha o que está sendo comparado hoje, não o histórico inteiro.
  const maxImpactByKpi = computed(() => {
    const map = new Map<string, number>()
    for (const epic of scopedEpics.value) {
      for (const link of epic.kpiLinks) {
        if (link.estimatedImpact != null && link.estimatedImpact > 0)
          map.set(link.kpiId, Math.max(map.get(link.kpiId) ?? 0, link.estimatedImpact))
      }
    }
    return map
  })

  function kpiBase(kpiId: string, confidence: ConfidenceLevel): number {
    return typeWeightOf(kpiId) * (confWeight[confidence] ?? 1)
  }
  // Magnitude 0..1 relativa ao maior impacto do mesmo KPI. Impacto vazio = 0 (mínima).
  function kpiMagnitude(kpiId: string, estimatedImpact: number | null | undefined): number {
    const max = maxImpactByKpi.value.get(kpiId) ?? 0
    if (max <= 0) return 1 // sem dados de impacto para esse KPI: não dá p/ normalizar → base cheia
    return estimatedImpact != null && estimatedImpact > 0 ? Math.min(estimatedImpact / max, 1) : 0
  }
  function kpiContribution(kpiId: string, confidence: ConfidenceLevel, estimatedImpact: number | null | undefined): number {
    return kpiBase(kpiId, confidence) * kpiMagnitude(kpiId, estimatedImpact)
  }
  function epicImpact(epic: RoadmapDemand): number {
    return epic.kpiLinks.reduce((sum, l) => sum + kpiContribution(l.kpiId, l.confidenceLevel, l.estimatedImpact), 0)
  }
  function epicConfidenceBand(epic: RoadmapDemand): ConfidenceLevel | null {
    if (!epic.kpiLinks.length) return null
    const avg = epic.kpiLinks.reduce((s, l) => s + (confWeight[l.confidenceLevel] ?? 1), 0) / epic.kpiLinks.length
    return avg >= 2.5 ? 'High' : avg >= 1.5 ? 'Medium' : 'Low'
  }

  const rows = computed<PriorityRow[]>(() => {
    const interim = scopedEpics.value.map((epic) => {
      const hasKpi = !epic.hasNoKpi && epic.kpiLinks.length > 0
      const effort = epicEffort(epic)
      let score: number | null
      let special: 'tecnico' | 'mandatorio' | null = null

      if (hasKpi) {
        score = epicImpact(epic)
      }
      else if (epic.hasNoKpi) {
        if (epic.noKpiClassification === 'Technical') { score = null; special = 'tecnico' }
        else if (epic.noKpiClassification === 'Mandatory') { score = null; special = 'mandatorio' }
        else score = 0
      }
      else {
        score = 0
      }
      return { epic, hasKpi, effort, score, special }
    })

    const scored = interim.filter(r => r.score != null)
    const medImpact = median(scored.map(r => r.score as number))
    const medEffort = median(scored.map(r => r.effort))

    return interim.map(({ epic, hasKpi, effort, score, special }) => {
      let matrixKind: MatrixKind | null = null
      if (special) {
        matrixKind = special
      }
      else if (score != null) {
        // Score 0 é sempre impacto BAIXO (evita o "0 >= mediana 0" cair em Ganhos/Apostas).
        const highImpact = score > 0 && score >= medImpact
        const highEffort = effort > medEffort
        matrixKind = highImpact ? (highEffort ? 'apostas' : 'ganhos') : (highEffort ? 'evitar' : 'quando')
      }
      const matrixInfo = matrixKind ? matrixMeta[matrixKind] : null

      const confBand = epicConfidenceBand(epic)

      const kpis: KpiCell[] = hasKpi
        ? epic.kpiLinks.map((link) => {
            const kpi = kpiById.value.get(link.kpiId)
            const arrow = kpi?.operation === 'HigherIsBetter' ? '↑' : kpi?.operation === 'LowerIsBetter' ? '↓' : ''
            const metaVal = link.estimatedImpact != null ? formatKpiValue(link.estimatedImpact, link.unit) : '—'
            const isBusiness = kpi?.type === 'Business'
            return {
              key: link.kpiId,
              indicatorLabel: kpi ? KPI_INDICATOR_LABELS[kpi.indicator] : link.kpiName,
              isBusiness,
              metaText: arrow ? `${arrow} ${metaVal}` : metaVal,
              metaTooltip: `${metaVal} · ${link.kpiName}`,
              typeTooltip: isBusiness ? 'Negócio · Peso 2' : 'Produto · Peso 1',
              confLabel: CONFIDENCE_LABELS[link.confidenceLevel] ?? '',
              confTooltip: `Confiança ${CONFIDENCE_LABELS[link.confidenceLevel] ?? ''}`,
              score: kpiContribution(link.kpiId, link.confidenceLevel, link.estimatedImpact)
            }
          }).sort((a, b) => (a.isBusiness === b.isBusiness ? b.score - a.score : (a.isBusiness ? -1 : 1)))
        : []

      // Tooltip do Score (valor + confiança + cálculo).
      let scoreTooltip: string
      if (score == null) {
        scoreTooltip = 'Sem score — não compete com a matriz.'
      }
      else if (!hasKpi) {
        scoreTooltip = 'Score 0 · sem KPI.'
      }
      else {
        const lines = [`Score ${fmtScore(score)} · confiança ${confBand ? CONFIDENCE_LABELS[confBand] : '—'}`, 'Cálculo (peso do tipo × confiança × magnitude):']
        for (const link of epic.kpiLinks) {
          const tw = typeWeightOf(link.kpiId)
          const tl = kpiById.value.get(link.kpiId)?.type === 'Business' ? 'Negócio' : 'Produto'
          const cw = confWeight[link.confidenceLevel] ?? 1
          const cl = CONFIDENCE_LABELS[link.confidenceLevel] ?? ''
          const mag = kpiMagnitude(link.kpiId, link.estimatedImpact)
          lines.push(`• ${link.kpiName}: ${tl} (${tw}) × ${cl} (${cw}) × mag ${fmtScore(mag)} = ${fmtScore(tw * cw * mag)}`)
        }
        scoreTooltip = lines.join('\n')
      }

      const kpiFallbackText = hasKpi
        ? ''
        : (epic.hasNoKpi
            ? (epic.noKpiClassification ? NO_KPI_CLASS_LABELS[epic.noKpiClassification] : 'Marcado como sem KPI')
            : 'Sem KPI')

      const quarter = epicQuarter(epic)
      return {
        epicId: epic.id,
        epicTitle: epic.title,
        status: epic.status,
        statusLabel: statusLabels[epic.status] ?? epic.status,
        quarterLabel: quarter.label,
        quarterOrder: quarter.order,
        promisedDate: epic.promisedDate ?? null,
        effort,
        score,
        scoreTooltip,
        confidence: confBand,
        matrixInfo,
        matrixKind,
        matrixRank: matrixInfo?.rank ?? 99,
        kpis,
        kpiFallbackText,
        isPendente: !hasKpi && !epic.hasNoKpi
      }
    })
  })

  return { rows }
}
