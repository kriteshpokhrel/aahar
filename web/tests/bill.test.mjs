import assert from 'node:assert/strict'
import { test } from 'node:test'
import { activeTableOrders, billReadiness, tableLabel } from '../src/bill.ts'
import { readPendingPayment, readPendingRound } from '../src/billRecovery.ts'
import { defaultPreferences, parsePreferences } from '../src/preferences.ts'

test('checkout requires all rounds ready and respects legacy completion', () => {
  const sent = { id: 1, status: 'Sent', roundCount: 2 }
  const tickets = [{ orderId: 1, status: 'Ready' }, { orderId: 1, status: 'Preparing' }]
  assert.equal(billReadiness(sent, tickets), false)
  assert.equal(billReadiness(sent, tickets.slice(0, 1)), false)
  tickets[1].status = 'Ready'
  assert.equal(billReadiness(sent, tickets), true)
  assert.equal(billReadiness({ id: 1, status: 'Sent' }, tickets), false)
  assert.equal(billReadiness({ id: 1, status: 'Completed' }, []), true)
  assert.equal(billReadiness({ id: 1, status: 'Paid', roundCount: 2 }, tickets), false)
})

test('table bill selection excludes paid and cancelled history', () => {
  const orders = [{ id: 1, tableId: 3, status: 'Sent' }, { id: 2, tableId: 3, status: 'Paid' }, { id: 3, tableId: 4, status: 'Draft' }]
  assert.deepEqual(activeTableOrders(orders, 3).map(order => order.id), [1])
  assert.equal(tableLabel(orders[0], [{ id: 3, number: 12 }]), 'Table 12')
  assert.equal(tableLabel({ tableId: null }, []), 'Takeaway')
})

test('recovery preserves the exact round and rejects malformed stored operations', () => {
  const round = { orderId: 12, requestId: '1cbd293d-1cd6-4453-8942-0ae187f4ea49', items: [{ menuItemId: 5, quantity: 2, notes: 'No onion' }] }
  assert.deepEqual(readPendingRound(JSON.stringify(round)), round)
  assert.deepEqual(readPendingPayment('{"orderId":12,"method":"Card"}'), { orderId: 12, method: 'Card' })
  assert.equal(readPendingRound(null), null)
  assert.throws(() => readPendingRound('{"orderId":12,"requestId":"x","items":[]}'))
  assert.throws(() => readPendingPayment('{"orderId":12,"method":"Other"}'))
})

test('preferences default safely and round-trip browser choices', () => {
  assert.deepEqual(parsePreferences(null), defaultPreferences)
  const value = { autoSendToKitchen: true, density: 'comfortable', defaultPayment: 'Card', autoOpenReceipt: false }
  assert.deepEqual(parsePreferences(JSON.stringify(value)), value)
  assert.throws(() => parsePreferences('{"autoSendToKitchen":"yes"}'))
})
