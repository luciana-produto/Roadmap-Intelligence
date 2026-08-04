import type { RoadmapDemand } from '~/types/roadmap'
import { isSpecialBacklogQuarter } from '~/utils/roadmapQuarter'

export type DeprioritizationGuardAction = 'confirm' | 'copy' | 'cancel'

export interface DeprioritizationGuardContext {
  items: RoadmapDemand[]
  changeType: 'status' | 'quarter' | 'both'
  targetQuarterYear?: number
  targetQuarterNumber?: number
  targetStatus?: string
}

/**
 * Returns true when the deprioritization warning should be shown for a single item.
 * Conditions: item is Deprioritized AND its current quarter is NOT backlog/backlog prioritário.
 */
export function needsDeprioritizationWarning(item: RoadmapDemand): boolean {
  return item.status === 'Deprioritized'
    && !isSpecialBacklogQuarter(item.quarterYear, item.quarterNumber)
}

export function useDeprioritizationGuard() {
  const isOpen = ref(false)
  const context = ref<DeprioritizationGuardContext | null>(null)
  const resolveAction = ref<((action: DeprioritizationGuardAction) => void) | null>(null)

  function prompt(ctx: DeprioritizationGuardContext): Promise<DeprioritizationGuardAction> {
    context.value = ctx
    isOpen.value = true
    return new Promise<DeprioritizationGuardAction>((resolve) => {
      resolveAction.value = resolve
    })
  }

  function respond(action: DeprioritizationGuardAction) {
    isOpen.value = false
    resolveAction.value?.(action)
    resolveAction.value = null
    context.value = null
  }

  return {
    isOpen,
    context,
    prompt,
    respond
  }
}
