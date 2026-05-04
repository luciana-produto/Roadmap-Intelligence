import { buildDemandDueSearchText, buildDueSortKey, hasPlannedQuarter } from '~/utils/roadmapDue'

describe('roadmapDue', () => {
  it('identifies planned quarters from quarter values instead of status assumptions', () => {
    expect(hasPlannedQuarter({ quarterYear: 2026, quarterNumber: 2 })).toBe(true)
    expect(hasPlannedQuarter({ quarterYear: 0, quarterNumber: 0 })).toBe(false)
  })

  it('builds demand due search text with quarter label only when a real quarter exists', () => {
    expect(buildDemandDueSearchText({ quarterYear: 2026, quarterNumber: 2, quarterLabel: 'Q2/26' }, '10/05/2026'))
      .toBe('q2/26 10/05/2026')

    expect(buildDemandDueSearchText({ quarterYear: 0, quarterNumber: 0, quarterLabel: 'Backlog' }, '10/05/2026'))
      .toBe('10/05/2026')
  })

  it('builds sortable due keys from raw iso dates with quarter fallback ordering', () => {
    expect(buildDueSortKey('2026-05-10', { quarterYear: 2026, quarterNumber: 2 }))
      .toBe('2026-05-10:2026:02')

    expect(buildDueSortKey('', { quarterYear: 2027, quarterNumber: 1 }))
      .toBe('9999-12-31:2027:01')
  })
})