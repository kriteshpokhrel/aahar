import { ApiError } from './api.ts'
import type { MenuItem } from './api.ts'

export function uncertainMutationOutcome(error: unknown): boolean {
  return !(error instanceof ApiError) || ![400, 401, 403, 409, 429].includes(error.status)
}

export function canAddToCart(cart: Record<number, number>, id: number): boolean {
  return (cart[id] ?? 0) < 1000 && (!!cart[id] || Object.keys(cart).length < 100)
}

export function unavailableCartIds(cart: Record<number, number>, menu: MenuItem[]): number[] {
  return Object.keys(cart).map(Number).filter(id => !menu.some(item => item.id === id && item.isAvailable))
}

export function canSubmitGuestRequest(state: {
  busy: boolean; uncertain: boolean; menuLoading: boolean; menuError: string
  invalidTable: boolean; missingItems: boolean; quoteStale: boolean; hasLines: boolean; hasSubmission: boolean
}): boolean {
  return !state.busy && !state.uncertain && !state.menuLoading && !state.menuError && !state.invalidTable &&
    !state.missingItems && !state.quoteStale && state.hasLines && !state.hasSubmission
}
