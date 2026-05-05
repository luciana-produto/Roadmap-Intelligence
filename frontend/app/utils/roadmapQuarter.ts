export const BACKLOG_QUARTER = {
  year: 0,
  number: 0,
  value: 'backlog',
  label: 'Backlog'
} as const

export const PRIORITIZED_BACKLOG_QUARTER = {
  year: 0,
  number: -1,
  value: 'backlog-prioritario',
  label: 'Backlog - Prioritário'
} as const

export const PRE_REGISTERED_QUARTER_END_YEAR = 2030

export function isBacklogQuarter(year: number, number: number) {
  return year === BACKLOG_QUARTER.year && number === BACKLOG_QUARTER.number
}

export function isPrioritizedBacklogQuarter(year: number, number: number) {
  return year === PRIORITIZED_BACKLOG_QUARTER.year && number === PRIORITIZED_BACKLOG_QUARTER.number
}

export function isSpecialBacklogQuarter(year: number, number: number) {
  return isBacklogQuarter(year, number) || isPrioritizedBacklogQuarter(year, number)
}

export function formatQuarterLabel(year: number, number: number) {
  if (isBacklogQuarter(year, number))
    return BACKLOG_QUARTER.label

  if (isPrioritizedBacklogQuarter(year, number))
    return PRIORITIZED_BACKLOG_QUARTER.label

  return `Q${number}/${String(year).slice(2)}`
}

export function buildQuarterValue(year: number, number: number) {
  if (isBacklogQuarter(year, number))
    return BACKLOG_QUARTER.value

  if (isPrioritizedBacklogQuarter(year, number))
    return PRIORITIZED_BACKLOG_QUARTER.value

  return `${number}-${year}`
}

export function parseQuarterValue(value: string) {
  if (value === BACKLOG_QUARTER.value)
    return { quarterYear: BACKLOG_QUARTER.year, quarterNumber: BACKLOG_QUARTER.number }

  if (value === PRIORITIZED_BACKLOG_QUARTER.value)
    return { quarterYear: PRIORITIZED_BACKLOG_QUARTER.year, quarterNumber: PRIORITIZED_BACKLOG_QUARTER.number }

  const [quarterNumber, quarterYear] = value.split('-').map(Number)
  return { quarterYear, quarterNumber }
}

export function buildPreRegisteredQuarterYears(startYear: number, endYear = PRE_REGISTERED_QUARTER_END_YEAR) {
  const normalizedEndYear = Math.max(startYear, endYear)
  return Array.from({ length: normalizedEndYear - startYear + 1 }, (_, index) => startYear + index)
}