import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiError } from '../src/api.ts'
import { canAddToCart, canSubmitGuestRequest, unavailableCartIds, uncertainMutationOutcome } from '../src/orderSafety.ts'

test('cart respects order limits while allowing additions to existing lines', () => {
  const cart = Object.fromEntries(Array.from({ length: 100 }, (_, index) => [index + 1, 1]))
  assert.equal(canAddToCart(cart, 101), false)
  assert.equal(canAddToCart(cart, 1), true)
  cart[1] = 1000
  assert.equal(canAddToCart(cart, 1), false)
  delete cart[2]
  assert.equal(canAddToCart(cart, 101), true)
})

test('cart detects both unavailable and missing menu items', () => {
  const menu = [
    { id: 1, name: 'Available', price: 10, isAvailable: true },
    { id: 2, name: 'Unavailable', price: 10, isAvailable: false },
  ]
  assert.deepEqual(unavailableCartIds({ 1: 1, 2: 2, 3: 1 }, menu), [2, 3])
  assert.deepEqual(unavailableCartIds({ 1: 1 }, menu), [])
})

test('lost and timeout responses are treated as uncertain mutation outcomes', () => {
  for (const error of [new TypeError('network'), new ApiError('timeout', 408), new ApiError('server', 503)]) {
    assert.equal(uncertainMutationOutcome(error), true)
  }
  for (const status of [400, 401, 403, 409, 429]) {
    assert.equal(uncertainMutationOutcome(new ApiError('rejected', status)), false)
  }
})

test('guest submission requires a successfully loaded current menu', () => {
  const state = { busy: false, uncertain: false, menuLoading: false, menuError: '', invalidTable: false, missingItems: false, quoteStale: false, hasLines: true, hasSubmission: false }
  assert.equal(canSubmitGuestRequest(state), true)
  assert.equal(canSubmitGuestRequest({ ...state, menuLoading: true }), false)
  assert.equal(canSubmitGuestRequest({ ...state, menuError: 'Menu unavailable' }), false)
  assert.equal(canSubmitGuestRequest({ ...state, quoteStale: true }), false)
  assert.equal(canSubmitGuestRequest({ ...state, uncertain: true }), false)
})
