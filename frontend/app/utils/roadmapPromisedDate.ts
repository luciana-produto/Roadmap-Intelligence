import { isSpecialBacklogQuarter } from '~/utils/roadmapQuarter'

type PromisedDateSource = {
  quarterYear: number
  quarterNumber: number
  promisedDate?: string
  effectivePromisedDate?: string
}

export function formatIsoDate(year: number, month: number, day: number) {
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`
}

export function getQuarterFallbackPromisedDate(quarterYear: number, quarterNumber: number) {
  if (isSpecialBacklogQuarter(quarterYear, quarterNumber))
    return ''

  const lastDay = new Date(quarterYear, quarterNumber * 3, 0).getDate()
  return formatIsoDate(quarterYear, quarterNumber * 3, lastDay)
}

export function getLatestPromisedDate(items: PromisedDateSource[]) {
  if (!items.length)
    return ''

  if (items.some(item => isSpecialBacklogQuarter(item.quarterYear, item.quarterNumber)))
    return ''

  let latestPromisedDate = ''

  items.forEach((item) => {
    const promisedDate = item.effectivePromisedDate
      ?? item.promisedDate
      ?? getQuarterFallbackPromisedDate(item.quarterYear, item.quarterNumber)

    if (promisedDate > latestPromisedDate)
      latestPromisedDate = promisedDate
  })

  return latestPromisedDate
}