import assert from 'node:assert/strict'
import { test } from 'node:test'
import { api, ApiError } from '../src/api.ts'

test('round retries use the same request ID and unchanged items', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  let attempts = 0
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url === '/api/auth/csrf') return Response.json({ requestToken: 'round-token' })
    if (url.endsWith('/rounds')) {
      if (++attempts === 1) return Response.json({ detail: 'Concurrent request' }, { status: 409 })
      return Response.json({ order: { id: 7 }, ticket: { id: 8 } })
    }
    return new Response(null, { status: 204 })
  }
  try {
    const items = [{ menuItemId: 2, quantity: 2, notes: 'No onion' }]
    const requestId = '1cbd293d-1cd6-4453-8942-0ae187f4ea49'
    await assert.rejects(api.addRound(7, requestId, items), error => error instanceof ApiError && error.status === 409)
    await api.addRound(7, requestId, items)
    const rounds = calls.filter(call => call.url === '/api/orders/7/rounds')
    assert.equal(rounds.length, 2)
    assert.deepEqual(rounds.map(call => JSON.parse(call.options.body)), [{ requestId, items }, { requestId, items }])
    assert.ok(rounds.every(call => call.options.headers.get('X-CSRF-TOKEN') === 'round-token'))
  } finally { await api.logout(); globalThis.fetch = originalFetch }
})

test('password change uses CSRF and obtains a fresh token before signing in again', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  let token = 0
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url === '/api/auth/csrf') return Response.json({ requestToken: `change-token-${++token}` })
    if (url === '/api/auth/me') return Response.json({ username: 'operator', role: 'Staff' })
    return new Response(null, { status: 204 })
  }
  try {
    await api.changePassword('old-example', 'new-example-password')
    const change = calls.find(call => call.url === '/api/auth/change-password')
    assert.equal(change.options.method, 'POST')
    assert.equal(change.options.credentials, 'same-origin')
    assert.equal(change.options.headers.get('X-CSRF-TOKEN'), 'change-token-1')
    assert.deepEqual(JSON.parse(change.options.body), { currentPassword: 'old-example', newPassword: 'new-example-password' })
    await api.login('operator', 'new-example-password')
    const login = calls.find(call => call.url === '/api/auth/login')
    assert.equal(login.options.headers.get('X-CSRF-TOKEN'), 'change-token-2')
  } finally { await api.logout(); globalThis.fetch = originalFetch }
})

test('password change errors are surfaced and do not leave a cached CSRF token', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  let token = 0
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url === '/api/auth/csrf') return Response.json({ requestToken: `error-token-${++token}` })
    if (url === '/api/auth/change-password') return Response.json({ detail: 'Password change rejected' }, { status: 409 })
    if (url === '/api/auth/me') return Response.json({ username: 'operator', role: 'Staff' })
    return new Response(null, { status: 204 })
  }
  try {
    await assert.rejects(api.changePassword('old-example', 'new-example-password'), error => error instanceof ApiError && error.status === 409)
    await api.login('operator', 'new-example-password')
    assert.equal(calls.find(call => call.url === '/api/auth/login').options.headers.get('X-CSRF-TOKEN'), 'error-token-2')
  } finally { await api.logout(); globalThis.fetch = originalFetch }
})

test('menu image mutations send raw image content and CSRF protection', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url === '/api/auth/csrf') return Response.json({ requestToken: 'image-token' })
    return new Response(null, { status: 204 })
  }
  try {
    const image = new File(['image bytes'], 'menu.png', { type: 'image/png' })
    await api.uploadMenuImage(7, image)
    await api.deleteMenuImage(7)
    const mutations = calls.filter(call => call.url === '/api/menu/7/image')
    assert.deepEqual(mutations.map(call => call.options.method), ['PUT', 'DELETE'])
    assert.equal(mutations[0].options.body, image)
    assert.equal(mutations[0].options.headers.get('Content-Type'), 'image/png')
    assert.ok(mutations.every(call => call.options.credentials === 'same-origin' && call.options.headers.get('X-CSRF-TOKEN')))
  } finally { await api.logout(); globalThis.fetch = originalFetch }
})

test('staff account creation uses the authenticated CSRF-protected endpoint', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url === '/api/auth/csrf') return Response.json({ requestToken: 'staff-token' })
    return Response.json({ username: 'operator', role: 'Kitchen' })
  }
  try {
    assert.deepEqual(await api.createUser('operator', 'sample-password', 'Kitchen'), { username: 'operator', role: 'Kitchen' })
    const request = calls.find(call => call.url === '/api/users')
    assert.equal(request.options.method, 'POST')
    assert.equal(request.options.credentials, 'same-origin')
    assert.ok(request.options.headers.get('X-CSRF-TOKEN'))
    assert.deepEqual(JSON.parse(request.options.body), { username: 'operator', password: 'sample-password', role: 'Kitchen' })
  } finally { await api.logout(); globalThis.fetch = originalFetch }
})

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

test('inventory mutations use exact units, audit reasons and receipt lines; conflicts are surfaced', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url === '/api/purchase-receipts') return Response.json({ detail: 'Supplier reference already exists' }, { status: 409 })
    return Response.json({ id: 1 })
  }
  try {
    await api.createIngredient('Flour', 'kg', 5.125, 1)
    await api.addStock(1, 0.125, 'kg', 'Correction')
    await api.waste(1, 0.5, 'kg', 'Spoilage')
    await api.setThreshold(1, 2.5, 'Review')
    await api.createSupplier('Mill', null, 'Initial')
    await api.updateSupplier(3, 'Mill', 'Contact', 'Changed')
    const lines = [{ ingredientId: 1, quantity: 1.125, unit: 'kg', unitCost: 25.5 }]
    await assert.rejects(api.receivePurchase(3, 'REF-1', 'Delivery', lines), error => error instanceof ApiError && error.status === 409 && error.message === 'Supplier reference already exists')
    assert.deepEqual(calls.map(call => [call.url, call.options.method, JSON.parse(call.options.body)]), [
      ['/api/ingredients', 'POST', { name: 'Flour', unit: 'kg', stock: 5.125, lowStockThreshold: 1 }],
      ['/api/ingredients/1/stock', 'POST', { quantity: 0.125, unit: 'kg', reason: 'Correction' }],
      ['/api/ingredients/1/waste', 'POST', { quantity: 0.5, unit: 'kg', reason: 'Spoilage' }],
      ['/api/ingredients/1/threshold', 'PUT', { threshold: 2.5, reason: 'Review' }],
      ['/api/suppliers', 'POST', { name: 'Mill', contact: null, reason: 'Initial' }],
      ['/api/suppliers/3', 'PUT', { name: 'Mill', contact: 'Contact', reason: 'Changed' }],
      ['/api/purchase-receipts', 'POST', { supplierId: 3, reference: 'REF-1', reason: 'Delivery', lines }],
    ])
    assert.ok(calls.every(call => call.options.headers.get('X-CSRF-TOKEN') === 'token'))
    assert.equal(calls.filter(call => call.url === '/api/purchase-receipts').length, 1)
  } finally { globalThis.fetch = originalFetch }
})

test('inventory history uses cursor pagination and receipt detail endpoint', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  globalThis.fetch = async (url, options) => { calls.push({ url, options }); return Response.json([]) }
  try {
    await api.lowStock()
    await api.movements(1, 44)
    await api.thresholdHistory(1)
    await api.supplierHistory(3, 10)
    await api.purchaseReceipts(21)
    await api.purchaseReceipt(4)
    assert.deepEqual(calls.map(call => call.url), [
      '/api/ingredients/low-stock', '/api/ingredients/1/movements?limit=20&beforeId=44',
      '/api/ingredients/1/threshold-history?limit=20', '/api/suppliers/3/history?limit=20&beforeId=10',
      '/api/purchase-receipts?limit=20&beforeId=21', '/api/purchase-receipts/4',
    ])
    assert.ok(calls.every(call => call.options.credentials === 'same-origin'))
  } finally { globalThis.fetch = originalFetch }
})

test('recipe read and replacement preserve ingredient quantities and surface conflicts', async () => {
  const calls = []
  const originalFetch = globalThis.fetch
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options })
    if (url === '/api/auth/csrf') return Response.json({ requestToken: 'recipe-token' })
    if (!options.method) return Response.json({ ingredients: [{ ingredientId: 4, quantity: 0.125 }] })
    if (JSON.parse(options.body).ingredients.length === 0) return new Response(null, { status: 204 })
    return Response.json({ detail: 'Recipe changed' }, { status: 409 })
  }
  try {
    assert.deepEqual(await api.recipe(3), { ingredients: [{ ingredientId: 4, quantity: 0.125 }] })
    await api.saveRecipe(3, [])
    await assert.rejects(api.saveRecipe(3, [{ ingredientId: 4, quantity: 0.125 }]), error => error instanceof ApiError && error.status === 409 && error.message === 'Recipe changed')
    const mutations = calls.filter(call => call.options.method === 'PUT')
    assert.deepEqual(mutations.map(call => [call.url, JSON.parse(call.options.body)]), [
      ['/api/menu/3/recipe', { ingredients: [] }],
      ['/api/menu/3/recipe', { ingredients: [{ ingredientId: 4, quantity: 0.125 }] }],
    ])
    assert.ok(mutations.every(call => call.options.headers.get('X-CSRF-TOKEN')))
  } finally { globalThis.fetch = originalFetch }
})
