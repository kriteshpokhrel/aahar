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
