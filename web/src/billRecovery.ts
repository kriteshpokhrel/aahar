import type { OrderLine, PaymentMethod } from './api.ts'

export type PendingRound = { orderId: number; requestId: string; items: OrderLine[] }
export type PendingPayment = { orderId: number; method: PaymentMethod }

export function readPendingRound(raw: string | null): PendingRound | null {
  if (raw === null) return null
  const value: unknown = JSON.parse(raw)
  if (!value || typeof value !== 'object' || !('orderId' in value) || !Number.isInteger(value.orderId) ||
      !('requestId' in value) || typeof value.requestId !== 'string' || !value.requestId ||
      !('items' in value) || !Array.isArray(value.items) || !value.items.length ||
      !value.items.every((item: unknown) => item && typeof item === 'object' && 'menuItemId' in item && Number.isInteger(item.menuItemId) &&
        'quantity' in item && Number.isInteger(item.quantity) && 'notes' in item && (item.notes === null || typeof item.notes === 'string'))) {
    throw new Error('Pending round data is invalid. Check the bill and kitchen before continuing.')
  }
  return value as PendingRound
}

export function readPendingPayment(raw: string | null): PendingPayment | null {
  if (raw === null) return null
  const value: unknown = JSON.parse(raw)
  if (!value || typeof value !== 'object' || !('orderId' in value) || !Number.isInteger(value.orderId) ||
      !('method' in value) || (value.method !== 'Cash' && value.method !== 'Card')) {
    throw new Error('Pending payment data is invalid. Check the bill before continuing.')
  }
  return value as PendingPayment
}
