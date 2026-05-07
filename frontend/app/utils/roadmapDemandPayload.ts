import type {
  DemandFormData,
  DemandStatus,
  IssueLink,
  IssueLinkInput,
  NoKpiClassification,
  RoadmapDemand,
  RoadmapItemType
} from '~/types/roadmap'
import { isSpecialBacklogQuarter } from '~/utils/roadmapQuarter'

type SharedDemandMutationPayload = {
  itemType: RoadmapItemType
  parentDemandId?: string
  title: string
  description?: string
  projectId?: string
  projectIds: string[]
  quarterYear: number
  quarterNumber: number
  type: DemandFormData['type']
  classification: DemandFormData['classification']
  productIds: string[]
  dependencyDemandIds: string[]
  replacementDemandId?: string
  jiraIssue?: string
  issueLinks: IssueLinkInput[]
  hours?: number
  promisedDate?: string
  customers: string[]
  isBlocked: boolean
  blockedReason?: string
  deprioritizationReason?: DemandFormData['deprioritizationReason']
  problemClarity?: number
  hasNoKpi: boolean
  noKpiClassification?: NoKpiClassification
}

function normalizeCustomers(customers?: string[]) {
  return [...new Set((customers ?? []).map(customer => customer.trim()).filter(Boolean))]
}

export function isBacklogDemand(itemType: RoadmapItemType, quarterYear: number, quarterNumber: number) {
  return itemType === 'Demand' && isSpecialBacklogQuarter(quarterYear, quarterNumber)
}

export function sanitizeCustomersForItem(itemType: RoadmapItemType, customers?: string[]) {
  return itemType === 'Demand' ? [] : normalizeCustomers(customers)
}

export function sanitizeIssueLinksForItem<T extends Pick<IssueLink, 'key' | 'url'>>(itemType: RoadmapItemType, issueLinks?: T[]) {
  return itemType === 'Roadmap' ? [] : [...(issueLinks ?? [])]
}

export function sanitizePromisedDateForItem(
  itemType: RoadmapItemType,
  quarterYear: number,
  quarterNumber: number,
  promisedDate?: string
) {
  if (isBacklogDemand(itemType, quarterYear, quarterNumber))
    return undefined

  const normalizedDate = promisedDate?.trim()
  return normalizedDate ? normalizedDate : undefined
}

function buildSharedDemandMutationPayload(payload: DemandFormData): SharedDemandMutationPayload {
  const issueLinks = sanitizeIssueLinksForItem(payload.itemType, payload.issueLinks)
  const isDeprioritized = payload.status === 'Deprioritized'
  const isBlocked = payload.status === 'Blocked'

  return {
    itemType: payload.itemType,
    parentDemandId: payload.parentDemandId || undefined,
    title: payload.title,
    description: payload.description || undefined,
    projectId: payload.projectId,
    projectIds: payload.itemType === 'Demand' ? [] : (payload.projectIds ?? []),
    quarterYear: payload.quarterYear,
    quarterNumber: payload.quarterNumber,
    type: payload.type,
    classification: payload.classification,
    productIds: payload.productIds,
    dependencyDemandIds: payload.dependencyDemandIds ?? [],
    replacementDemandId: isDeprioritized ? payload.replacementDemandId || undefined : undefined,
    jiraIssue: issueLinks[0]?.key || payload.jiraIssue || undefined,
    issueLinks,
    hours: payload.hours ?? undefined,
    promisedDate: sanitizePromisedDateForItem(payload.itemType, payload.quarterYear, payload.quarterNumber, payload.promisedDate),
    customers: sanitizeCustomersForItem(payload.itemType, payload.customers),
    isBlocked,
    blockedReason: isBlocked ? payload.blockedReason?.trim() || undefined : undefined,
    deprioritizationReason: isDeprioritized ? payload.deprioritizationReason || undefined : undefined,
    problemClarity: payload.itemType === 'Epic' ? payload.problemClarity ?? undefined : undefined,
    hasNoKpi: payload.hasNoKpi ?? false,
    noKpiClassification: payload.hasNoKpi ? payload.noKpiClassification ?? undefined : undefined
  }
}

export function buildCreateDemandPayload(payload: DemandFormData) {
  return {
    ...buildSharedDemandMutationPayload(payload),
    status: payload.status ?? 'Backlog',
    observation: payload.observation || undefined,
    deliveryDate: payload.deliveryDate || undefined
  }
}

export function buildUpdateDemandPayload(id: string, payload: DemandFormData) {
  return {
    id,
    ...buildSharedDemandMutationPayload(payload),
    status: payload.status ?? 'Backlog',
    observation: payload.observation || undefined,
    deliveryDate: payload.deliveryDate || undefined
  }
}

export function buildStatusPatchPayload(demand: RoadmapDemand, status: DemandStatus) {
  const issueLinks = sanitizeIssueLinksForItem(demand.itemType, demand.issueLinks)
  const isBlocked = status === 'Blocked'

  return {
    id: demand.id,
    itemType: demand.itemType,
    parentDemandId: demand.parentDemandId || undefined,
    title: demand.title,
    description: demand.description || undefined,
    projectId: demand.projectId,
    projectIds: demand.itemType === 'Demand' ? [] : (demand.projectIds ?? []),
    quarterYear: demand.quarterYear,
    quarterNumber: demand.quarterNumber,
    status,
    type: demand.type,
    classification: demand.classification,
    productIds: demand.products.map(product => product.productId),
    observation: demand.observation || undefined,
    jiraIssue: issueLinks[0]?.key || demand.jiraIssue || undefined,
    issueLinks,
    hours: demand.hours ?? undefined,
    customers: sanitizeCustomersForItem(demand.itemType, demand.customers),
    isBlocked,
    blockedReason: isBlocked ? demand.blockedReason || undefined : undefined,
    deliveryDate: demand.deliveryDate || undefined,
    problemClarity: demand.itemType === 'Epic' ? demand.problemClarity ?? undefined : undefined,
    hasNoKpi: demand.hasNoKpi ?? false,
    noKpiClassification: demand.hasNoKpi ? demand.noKpiClassification ?? undefined : undefined
  }
}