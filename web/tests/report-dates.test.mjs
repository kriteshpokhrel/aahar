import assert from 'node:assert/strict'
import { test } from 'node:test'
import { reportDateError } from '../src/reportDates.ts'

test('report date validation uses inclusive calendar days and real dates', () => {
  assert.equal(reportDateError('2024-01-01', '2024-12-31'), null)
  assert.equal(reportDateError('2024-01-01', '2025-01-01'), 'Choose a period of at most 366 days.')
  assert.equal(reportDateError('2025-05-01', '2025-04-30'), 'The end date must be on or after the start date.')
  assert.match(reportDateError('2025-02-29', '2025-03-01'), /valid dates/)
  assert.match(reportDateError('1899-12-31', '1900-01-01'), /valid dates/)
  assert.match(reportDateError('9998-12-31', '9998-12-31'), /valid dates/)
})
