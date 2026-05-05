export type DemandStatus = 'Backlog' | 'InProgress' | 'Done' | 'Deprioritized'
export type DemandType = 'Planned' | 'Spillover' | 'Unplanned' | 'Additional'
export type RoadmapItemType = 'Roadmap' | 'Epic' | 'Demand'
export type DemandClassification =
  | 'TechnicalDebtSecurity'
  | 'Strategic'
  | 'Evolution'
  | 'ImprovementGap'
  | 'Mandatory'
  | 'Homologation'
  | 'Customizacao'
export type NoKpiClassification = 'Relationship' | 'Mandatory' | 'Technical'
export type DeprioritizationReason =
  | 'Strategic'
  | 'MandatoryUrgent'
  | 'LowImpact'
  | 'LackOfCapacity'
  | 'ContextChange'
  | 'Customizacao'

export type KpiType = 'Business' | 'Product'
export type KpiLever = 'Growth' | 'Efficiency' | 'Customer'
export type KpiObjective = 'Increase' | 'Decrease'
export type ImpactType = 'Increase' | 'Decrease'
export type ConfidenceLevel = 'High' | 'Medium' | 'Low'
export type MeasurementResult = 'Positive' | 'Negative' | 'Neutral'

export interface DemandProduct {
  productId: string
  name: string
}

export interface IssueLink {
  key: string
  url?: string
}

export interface IssueLinkInput {
  key: string
  url: string
}

export interface RoadmapProduct {
  id: string
  name: string
  projectId: string
}

export interface RoadmapProject {
  id: string
  name: string
  slug: string
  products: RoadmapProduct[]
}

export interface DemandDependency {
  demandId: string
  itemType: RoadmapItemType
  projectId?: string
  projectName: string
  title: string
  quarterLabel: string
  quarterYear: number
  quarterNumber: number
  status: DemandStatus
}

export interface DemandDependencyOption {
  demandId: string
  itemType: RoadmapItemType
  projectId?: string
  projectName: string
  title: string
  quarterLabel: string
  quarterYear: number
  quarterNumber: number
  status: DemandStatus
}

export interface DemandTradeOffHistory {
  id: string
  projectId: string
  projectName: string
  quarterLabel: string
  quarterYear: number
  quarterNumber: number
  deprioritizedDemandId: string
  deprioritizedDemandTitle: string
  replacementDemandId?: string
  replacementDemandTitle?: string
  reason: DeprioritizationReason
  observation?: string
  createdAt: string
}

export interface RoadmapDemand {
  id: string
  itemType: RoadmapItemType
  parentDemandId?: string
  parentTitle?: string
  roadmapId?: string
  roadmapTitle?: string
  epicId?: string
  epicTitle?: string
  title: string
  description?: string
  projectId?: string
  projectIds?: string[]
  quarterLabel: string
  quarterYear: number
  quarterNumber: number
  sortOrder: number
  status: DemandStatus
  type: DemandType
  classification: DemandClassification
  products: DemandProduct[]
  observation?: string
  deprioritizationReason?: DeprioritizationReason
  replacementDemandId?: string
  jiraIssue?: string
  issueLinks?: IssueLink[]
  hours?: number
  customers?: string[]
  isBlocked: boolean
  blockedReason?: string
  promisedDate?: string
  dependsOn: DemandDependency[]
  dependedOnBy: DemandDependency[]
  effectivePromisedDate?: string
  deliveryDate?: string
  isOverdue: boolean
  isDeliveredLate: boolean
  problemClarity?: number
  hasNoKpi: boolean
  noKpiClassification?: NoKpiClassification
  tradeOffHistory: DemandTradeOffHistory[]
  kpiLinks: DemandKpiLink[]
  kpiMeasurements: KpiMeasurement[]
  createdAt: string
  updatedAt?: string
}

export interface RoadmapCapacitySummary {
  id?: string
  projectId: string
  quarterLabel: string
  quarterYear: number
  quarterNumber: number
  capacityHours?: number
  observation?: string
  committedHours: number
  additionalHours: number
  totalDemandHours: number
  nonEstimatedDemandCount?: number
  remainingHours?: number
  overCapacityHours?: number
}

export interface DemandFormData {
  itemType: RoadmapItemType
  parentDemandId?: string
  title: string
  description: string
  projectId?: string
  projectIds?: string[]
  quarterYear: number
  quarterNumber: number
  type: DemandType
  classification: DemandClassification
  productIds: string[]
  status?: DemandStatus
  observation?: string
  deprioritizationReason?: DeprioritizationReason
  replacementDemandId?: string
  jiraIssue?: string
  issueLinks?: IssueLinkInput[]
  hours?: number
  customers?: string[]
  dependencyDemandIds?: string[]
  isBlocked?: boolean
  blockedReason?: string
  promisedDate?: string
  deliveryDate?: string
  problemClarity?: number
  hasNoKpi?: boolean
  noKpiClassification?: NoKpiClassification
}

export interface CapacityFormData {
  projectId: string
  quarterYear: number
  quarterNumber: number
  capacityHours: number
  observation?: string
}

export interface Kpi {
  id: string
  projectId?: string
  name: string
  type: KpiType
  lever: KpiLever
  objective: KpiObjective
  description?: string
  calculation?: string
  target?: number
  currentValue?: number
  linkedDemandsCount: number
  createdAt: string
  updatedAt?: string
}

export interface KpiFormData {
  name: string
  type: KpiType
  lever: KpiLever
  objective: KpiObjective
  description?: string
  calculation?: string
  target?: number
  currentValue?: number
}

export interface DemandKpiLink {
  id: string
  demandId: string
  kpiId: string
  kpiName: string
  impactType: ImpactType
  estimatedImpact?: number
  confidenceLevel: ConfidenceLevel
  observation?: string
  measurementReferenceUrl?: string
}

export interface DemandKpiLinkInput {
  kpiId: string
  impactType: ImpactType
  estimatedImpact?: number
  confidenceLevel: ConfidenceLevel
  observation?: string
  measurementReferenceUrl?: string
}

export interface KpiMeasurement {
  id: string
  kpiId: string
  kpiName: string
  demandId?: string
  demandTitle?: string
  measuredValue: number
  measurementDate: string
  result: MeasurementResult
  observation?: string
  createdAt: string
}

export interface CreateDemandKpiMeasurementInput {
  kpiId: string
  measuredValue: number
  measurementDate: string
  result: MeasurementResult
  observation?: string
}

export interface UpdateDemandKpiMeasurementInput {
  measuredValue: number
  measurementDate: string
  result: MeasurementResult
  observation?: string
}
