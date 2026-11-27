import assert from 'node:assert/strict'
import { test } from 'node:test'
import { pendingRequestCount, serviceOverview, statusClass } from '../src/overviewMetrics.ts'

test('overview counts actionable service states without including finished or cancelled orders', () => {
  const tickets = ['Queued', 'queued', 'Preparing', 'Ready', 'Cancelled'].map(status => ({ status }))
  const orders = ['Draft', 'draft', 'Completed', 'Paid', 'Cancelled', 'Sent'].map(status => ({ status }))
  const tables = [{ isOccupied: true }, { isOccupied: false }, { isOccupied: false }]
  assert.deepEqual(serviceOverview(tickets, orders, tables), {
    queued: 2, preparing: 1, ready: 1, drafts: 2, completed: 1, availableTables: 2, totalTables: 3,
  })
  assert.deepEqual(serviceOverview([], [], []), {
    queued: 0, preparing: 0, ready: 0, drafts: 0, completed: 0, availableTables: 0, totalTables: 0,
  })
})

test('pending requests show a lower bound when the first page is full', () => {
  assert.equal(pendingRequestCount(0), '0')
  assert.equal(pendingRequestCount(19), '19')
  assert.equal(pendingRequestCount(20), '20+')
})

test('operational statuses receive distinct visual treatments with a neutral fallback', () => {
  assert.equal(statusClass('Queued'), 'status-attention')
  assert.equal(statusClass('PREPARING'), 'status-progress')
  assert.equal(statusClass('Ready'), 'status-success')
  assert.equal(statusClass('Paid'), 'status-success')
  assert.equal(statusClass('Rejected'), 'status-closed')
  assert.equal(statusClass('unknown'), 'status-neutral')
})
