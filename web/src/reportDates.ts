export function reportDateError(fromDate: string, toDate: string): string | null {
  const parse = (value: string) => {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || value < '1900-01-01' || value > '9998-12-30') return null
    const time = Date.parse(`${value}T00:00:00Z`)
    return Number.isFinite(time) && new Date(time).toISOString().slice(0, 10) === value ? time : null
  }
  const from = parse(fromDate)
  const to = parse(toDate)
  if (from === null || to === null) return 'Choose valid dates between 1900-01-01 and 9998-12-30.'
  if (to < from) return 'The end date must be on or after the start date.'
  if ((to - from) / 86400000 + 1 > 366) return 'Choose a period of at most 366 days.'
  return null
}
