import assert from 'node:assert/strict'
import { test } from 'node:test'
import { api, ApiError } from '../src/api.ts'

test('session mutations obtain CSRF tokens, use same-origin credentials, and surface API errors', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  let token = 0
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url === '/api/auth/csrf') return Response.json({ requestToken: `token-${++token}` })
    if (url === '/api/auth/me') return Response.json({ username: 'staff', role: 'Manager' })
    if (url === '/api/orders') return Response.json({ detail: 'Table is unavailable' }, { status: 409 })
    return new Response(null, { status: 204 })
  }
  try {
    assert.deepEqual(await api.login('staff', 'password'), { username: 'staff', role: 'Manager' })
    assert.equal(calls[1].options.headers.get('X-CSRF-TOKEN'), 'token-1')
    assert.equal(calls[1].options.credentials, 'same-origin')
    await assert.rejects(api.createOrder(1, [{ menuItemId: 2, quantity: 1 }]), error => {
      assert.ok(error instanceof ApiError)
      assert.equal(error.status, 409)
      assert.equal(error.message, 'Table is unavailable')
      return true
    })

    assert.equal(calls.at(-1).options.headers.get('X-CSRF-TOKEN'), 'token-2')
    await api.logout()
    await api.login('staff', 'password')
    assert.equal(calls.filter(call => call.url === '/api/auth/csrf').length, 3)
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('cancellation and card payment send the selected values to the API', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url === '/api/auth/csrf') return Response.json({ requestToken: 'token' })
    return Response.json({ id: 7 })
  }
  try {
    await api.cancelOrder(7, 'Guest changed their mind', 'Return')
    await api.cancelOrder(8, 'Food discarded', 'Waste')
    await api.cancelOrder(9, 'Mistake', 'None')
    await api.payOrder(7, 'Card')
    const mutations = calls.filter(call => call.options.method === 'POST')
    assert.deepEqual(mutations.map(call => [call.url, JSON.parse(call.options.body)]), [
      ['/api/orders/7/cancel', { reason: 'Guest changed their mind', stockDisposition: 'Return' }],
      ['/api/orders/8/cancel', { reason: 'Food discarded', stockDisposition: 'Waste' }],
      ['/api/orders/9/cancel', { reason: 'Mistake', stockDisposition: 'None' }],
      ['/api/orders/7/payments', { method: 'Card' }],
    ])
    assert.ok(mutations.every(call => call.options.headers.get('X-CSRF-TOKEN') === 'token'))
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('takeaway, draft editing and kitchen ticket transitions use the confirmed contract', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url === '/api/kitchen/tickets') return Response.json([{ id: 8, orderId: 7, status: 'Queued', isReconstructed: true }])
    if (url.endsWith('/ready')) return Response.json({ detail: 'Ticket changed; refresh and retry' }, { status: 409 })
    return Response.json({ id: 7 })
  }
  try {
    const items = [{ menuItemId: 2, quantity: 2, notes: 'No onion' }]
    await api.createOrder(null, items)
    await api.editOrder(7, items)
    const tickets = await api.kitchenTickets()
    assert.equal(tickets[0].isReconstructed, true)
    await api.prepareTicket(8)
    await assert.rejects(api.readyTicket(8), error => error instanceof ApiError && error.status === 409 && error.message === 'Ticket changed; refresh and retry')
    assert.deepEqual(calls.map(call => [call.url, call.options.method ?? 'GET', call.options.body ? JSON.parse(call.options.body) : null]), [
      ['/api/orders', 'POST', { tableId: null, items }],
      ['/api/orders/7/items', 'PUT', { items }],
      ['/api/kitchen/tickets', 'GET', null],
      ['/api/kitchen/tickets/8/prepare', 'POST', null],
      ['/api/kitchen/tickets/8/ready', 'POST', null],
    ])
    assert.ok(calls.filter(call => call.options.method).every(call => call.options.headers.get('X-CSRF-TOKEN') === 'token'))
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('guest token stays in request header and staff review uses the correct endpoints', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url === '/api/guest/requests') return Response.json({ id: 5, status: 'Pending', accessToken: 'guest-secret' })
    if (url === '/api/guest/requests/5') return Response.json({ id: 5, status: 'Pending', orderId: null })
    if (url.startsWith('/api/guest-requests?')) return Response.json([])
    return Response.json({ id: 5, status: 'Rejected' })
  }
  try {
    const items = [{ menuItemId: 2, quantity: 1, notes: 'No onion', quotedUnitPrice: 300 }]
    const receipt = await api.submitGuestRequest(4, items)
    await api.guestRequestStatus(receipt.id, receipt.accessToken)
    await api.guestRequests('Pending', 10)
    await api.acceptGuestRequest(5)
    await api.rejectGuestRequest(5, 'Unavailable')
    assert.deepEqual(JSON.parse(calls[0].options.body), { tableId: 4, items })
    assert.equal(calls[0].options.headers.get('X-CSRF-TOKEN'), 'token')
    assert.equal(calls[1].options.headers.get('X-Guest-Request-Token'), 'guest-secret')
    assert.ok(calls.every(call => !call.url.includes('guest-secret')))
    assert.equal(calls[2].url, '/api/guest-requests?status=Pending&limit=20&beforeId=10')
    assert.equal(calls[3].url, '/api/guest-requests/5/accept')
    assert.deepEqual(JSON.parse(calls[4].options.body), { reason: 'Unavailable' })
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('reports send inclusive date parameters and preserve snapshot rows', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url.startsWith('/api/reports/sales')) return Response.json({ fromDate: '2024-01-01', toDate: '2024-12-31', timeZone: 'Asia/Kathmandu', ordersPaid: 2, total: 700 })
    return Response.json({ items: [{ menuItemId: 1, name: 'Old name', unitPrice: 300, quantity: 1, revenue: 300 }, { menuItemId: 1, name: 'New name', unitPrice: 400, quantity: 1, revenue: 400 }] })
  }
  try {
    const sales = await api.salesReport('2024-01-01', '2024-12-31')
    const best = await api.bestSellingReport('2024-01-01', '2024-12-31')
    assert.equal(sales.timeZone, 'Asia/Kathmandu')
    assert.equal(best.items.length, 2)
    assert.deepEqual(calls.map(call => call.url), [
      '/api/reports/sales?fromDate=2024-01-01&toDate=2024-12-31',
      '/api/reports/best-selling?fromDate=2024-01-01&toDate=2024-12-31&limit=20',
    ])
    assert.ok(calls.every(call => call.options.credentials === 'same-origin'))
  } finally {
    globalThis.fetch = originalFetch
  }
})
