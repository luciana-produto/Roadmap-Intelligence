type QuarterInfo = {
  quarterYear: number
  quarterNumber: number
  quarterLabel?: string
}

export function hasPlannedQuarter(item: Pick<QuarterInfo, 'quarterYear' | 'quarterNumber'>) {
  return item.quarterYear > 0 && item.quarterNumber > 0
}

export function buildDemandDueSearchText(
  item: QuarterInfo,
  formattedConclusionDate?: string
) {
  return [hasPlannedQuarter(item) ? item.quarterLabel : '', formattedConclusionDate ?? '']
    .filter(Boolean)
    .join(' ')
    .toLowerCase()
}

export function buildDueSortKey(
  dueDate?: string,
  item?: Pick<QuarterInfo, 'quarterYear' | 'quarterNumber'>
) {
  const normalizedDate = dueDate?.trim() || '9999-12-31'
  const quarterYear = item?.quarterYear ?? 9999
  const quarterNumber = item?.quarterNumber ?? 9

  return `${normalizedDate}:${String(quarterYear).padStart(4, '0')}:${String(quarterNumber).padStart(2, '0')}`
}