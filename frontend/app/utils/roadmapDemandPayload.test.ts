import type { DemandFormData, RoadmapDemand } from '~/types/roadmap'
import {
  buildCreateDemandPayload,
  buildStatusPatchPayload,
  buildUpdateDemandPayload,
  isBacklogDemand,
  sanitizeCustomersForItem,
  sanitizeIssueLinksForItem,
  sanitizePromisedDateForItem
} from '~/utils/roadmapDemandPayload'
import { getLatestPromisedDate, getQuarterFallbackPromisedDate } from '~/utils/roadmapPromisedDate'

function createBaseFormData(overrides: Partial<DemandFormData> = {}): DemandFormData {
  return {
    itemType: 'Demand',
    title: 'Nova demanda',
    description: 'Descricao',
    projectId: 'project-1',
    projectIds: ['project-1'],
    quarterYear: 2026,
    quarterNumber: 2,
    type: 'Planned',
    classification: 'Strategic',
    productIds: ['product-1'],
    customers: [' Cliente A ', 'Cliente A', ''],
    issueLinks: [{ key: 'DEM-123', url: 'https://jira.local/DEM-123' }],
    promisedDate: '2026-05-10',
    ...overrides
  }
}

describe('roadmapDemandPayload', () => {
  it('identifies backlog demand correctly', () => {
    expect(isBacklogDemand('Demand', 0, 0)).toBe(true)
    expect(isBacklogDemand('Epic', 0, 0)).toBe(false)
    expect(isBacklogDemand('Demand', 2026, 2)).toBe(false)
  })

  it('sanitizes demand customers and roadmap issue links by item type', () => {
    expect(sanitizeCustomersForItem('Demand', ['Cliente A'])).toEqual([])
    expect(sanitizeCustomersForItem('Epic', [' Cliente A ', 'Cliente A', ''])).toEqual(['Cliente A'])
    expect(sanitizeIssueLinksForItem('Roadmap', [{ key: 'ROAD-1', url: 'https://jira.local/ROAD-1' }])).toEqual([])
  })

  it('clears promised date for backlog demand only', () => {
    expect(sanitizePromisedDateForItem('Demand', 0, 0, '2026-05-10')).toBeUndefined()
    expect(sanitizePromisedDateForItem('Epic', 0, 0, '2026-05-10')).toBe('2026-05-10')
    expect(sanitizePromisedDateForItem('Demand', 2026, 2, ' 2026-05-10 ')).toBe('2026-05-10')
  })

  it('builds create and update payloads with sanitized demand invariants', () => {
    const payload = createBaseFormData({
      quarterYear: 0,
      quarterNumber: 0,
      customers: ['Cliente A'],
      projectIds: ['project-1', 'project-2']
    })

    expect(buildCreateDemandPayload(payload)).toMatchObject({
      projectIds: [],
      customers: [],
      promisedDate: undefined,
      jiraIssue: 'DEM-123'
    })

    expect(buildUpdateDemandPayload('demand-1', payload)).toMatchObject({
      id: 'demand-1',
      status: 'Backlog',
      projectIds: [],
      customers: [],
      promisedDate: undefined
    })
  })

  it('builds status patch payload from roadmap demand with sanitized customers', () => {
    const demand: RoadmapDemand = {
      id: 'demand-1',
      itemType: 'Demand',
      title: 'Demanda existente',
      quarterLabel: 'Q2/2026',
      quarterYear: 2026,
      quarterNumber: 2,
      sortOrder: 10,
      status: 'Backlog',
      type: 'Planned',
      classification: 'Strategic',
      products: [{ productId: 'product-1', name: 'Produto' }],
      customers: ['Cliente legado'],
      isBlocked: false,
      dependsOn: [],
      dependedOnBy: [],
      isOverdue: false,
      isDeliveredLate: false,
      hasNoKpi: false,
      tradeOffHistory: [],
      kpiLinks: [],
      kpiMeasurements: [],
      createdAt: '2026-05-01T10:00:00Z'
    }

    expect(buildStatusPatchPayload(demand, 'Done')).toMatchObject({
      id: 'demand-1',
      status: 'Done',
      productIds: ['product-1'],
      customers: []
    })
  })

  it('derives the latest promised date with quarter fallback and backlog guard', () => {
    expect(getQuarterFallbackPromisedDate(2026, 2)).toBe('2026-06-30')

    expect(getLatestPromisedDate([
      { quarterYear: 2026, quarterNumber: 1, promisedDate: '2026-03-10' },
      { quarterYear: 2026, quarterNumber: 2 },
      { quarterYear: 2026, quarterNumber: 3, effectivePromisedDate: '2026-08-15' }
    ])).toBe('2026-09-30')

    expect(getLatestPromisedDate([
      { quarterYear: 2026, quarterNumber: 2, promisedDate: '2026-06-10' },
      { quarterYear: 0, quarterNumber: 0 }
    ])).toBe('')
  })
})