export type DemandStatus = 'Backlog' | 'InProgress' | 'Done' | 'Deprioritized'
export type DemandType = 'Planned' | 'Spillover' | 'Unplanned' | 'Additional'
export type DemandClassification =
  | 'TechnicalDebtSecurity'
  | 'Strategic'
  | 'Evolution'
  | 'ImprovementGap'
  | 'Mandatory'
  | 'Homologation'

export type KpiType = 'Business' | 'Product' | 'Quality' | 'Usability'
export type KpiLever = 'Growth' | 'Efficiency' | 'Customer'
export type ImpactType = 'Increase' | 'Decrease'
export type ConfidenceLevel = 'High' | 'Medium' | 'Low'

export interface DemandProduct {
  productId: string
  name: string
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
  projectId: string
  projectName: string
  title: string
  quarterLabel: string
  quarterYear: number
  quarterNumber: number
  status: DemandStatus
}

export interface DemandDependencyOption {
  demandId: string
  projectId: string
  projectName: string
  title: string
  quarterLabel: string
  quarterYear: number
  quarterNumber: number
  status: DemandStatus
}

export interface RoadmapDemand {
  id: string
  title: string
  description?: string
  projectId: string
  quarterLabel: string
  quarterYear: number
  quarterNumber: number
  sortOrder: number
  status: DemandStatus
  type: DemandType
  classification: DemandClassification
  products: DemandProduct[]
  observation?: string
  jiraIssue?: string
  hours?: number
  customers?: string[]
  isBlocked: boolean
  blockedReason?: string
  dependsOn: DemandDependency[]
  dependedOnBy: DemandDependency[]
  deliveryDate?: string
  problemClarity?: number
  hasNoKpi: boolean
  kpiLinks: DemandKpiLink[]
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
  title: string
  description: string
  projectId: string
  quarterYear: number
  quarterNumber: number
  type: DemandType
  classification: DemandClassification
  productIds: string[]
  status?: DemandStatus
  observation?: string
  jiraIssue?: string
  hours?: number
  customers?: string[]
  dependencyDemandIds?: string[]
  isBlocked?: boolean
  blockedReason?: string
  deliveryDate?: string
  problemClarity?: number
  hasNoKpi?: boolean
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
  projectId: string
  name: string
  type: KpiType
  lever: KpiLever
  description?: string
  calculation?: string
  target?: number
  currentValue?: number
  linkedDemandsCount: number
  createdAt: string
  updatedAt?: string
}

export interface KpiFormData {
  projectId: string
  name: string
  type: KpiType
  lever: KpiLever
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
}

export interface DemandKpiLinkInput {
  kpiId: string
  impactType: ImpactType
  estimatedImpact?: number
  confidenceLevel: ConfidenceLevel
}
